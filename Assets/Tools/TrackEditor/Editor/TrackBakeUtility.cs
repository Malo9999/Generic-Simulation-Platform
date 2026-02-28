using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TrackBakeUtility
{
    public class ValidationReport
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public bool IsValid => Errors.Count == 0;
    }

    public static ValidationReport Validate(TrackLayout layout)
    {
        var report = new ValidationReport();
        if (layout == null)
        {
            report.Errors.Add("No TrackLayout selected.");
            return report;
        }

        var pieceMap = layout.pieces.Where(p => p?.piece != null).ToDictionary(p => p.guid, p => p);
        if (pieceMap.Count == 0)
        {
            report.Errors.Add("Layout has no placed pieces.");
            return report;
        }

        ValidateWidths(layout, pieceMap, report);
        ValidateMainLoop(layout, pieceMap, report);
        ValidateOverlaps(layout, pieceMap.Values.ToList(), report);
        ValidatePit(layout, pieceMap, report);

        return report;
    }

    public static TrackBakedData Bake(TrackLayout layout, string outputAssetPath)
    {
        var report = Validate(layout);
        if (!report.IsValid)
        {
            throw new InvalidOperationException(string.Join("\n", report.Errors));
        }

        var baked = AssetDatabase.LoadAssetAtPath<TrackBakedData>(outputAssetPath);
        if (baked == null)
        {
            baked = ScriptableObject.CreateInstance<TrackBakedData>();
            AssetDatabase.CreateAsset(baked, outputAssetPath);
        }

        var pieceMap = layout.pieces.ToDictionary(p => p.guid, p => p);
        var orderedMainSegments = BuildOrderedSegments(layout, pieceMap, TrackConnectorRole.Main);
        var mainCenter = Stitch(orderedMainSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localCenterline)).ToList());
        var mainLeft = Stitch(orderedMainSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localLeftBoundary)).ToList());
        var mainRight = Stitch(orderedMainSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localRightBoundary)).ToList());

        baked.trackWidth = layout.pieces.First().piece.trackWidth;
        baked.mainCenterline = mainCenter;
        baked.mainLeftBoundary = mainLeft;
        baked.mainRightBoundary = mainRight;
        baked.lapLength = TrackMathUtil.PolylineLength(mainCenter);
        baked.cumulativeMainLength = BuildCumulative(mainCenter);
        baked.startGridSlots = layout.startGridSlots != null ? new List<TrackSlot>(layout.startGridSlots) : new List<TrackSlot>();

        var pitSegments = BuildOrderedSegments(layout, pieceMap, TrackConnectorRole.Pit, allowOpenPath: true);
        if (pitSegments.Count > 0)
        {
            baked.pitCenterline = Stitch(pitSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localCenterline)).ToList());
            baked.pitLeftBoundary = Stitch(pitSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localLeftBoundary)).ToList());
            baked.pitRightBoundary = Stitch(pitSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localRightBoundary)).ToList());
        }
        else
        {
            baked.pitCenterline = null;
            baked.pitLeftBoundary = null;
            baked.pitRightBoundary = null;
        }

        EditorUtility.SetDirty(baked);
        AssetDatabase.SaveAssets();
        return baked;
    }

    private static void ValidateWidths(TrackLayout layout, Dictionary<string, PlacedPiece> pieceMap, ValidationReport report)
    {
        foreach (var link in layout.links)
        {
            if (!pieceMap.TryGetValue(link.pieceGuidA, out var a) || !pieceMap.TryGetValue(link.pieceGuidB, out var b))
            {
                report.Errors.Add("Link references missing piece.");
                continue;
            }

            var wA = a.piece.connectors[link.connectorIndexA].trackWidth;
            var wB = b.piece.connectors[link.connectorIndexB].trackWidth;
            if (Mathf.Abs(wA - wB) > 0.01f)
            {
                report.Errors.Add($"Width mismatch in link {a.guid}->{b.guid} ({wA} vs {wB}).");
            }
        }
    }

    private static void ValidateMainLoop(TrackLayout layout, Dictionary<string, PlacedPiece> pieceMap, ValidationReport report)
    {
        var graph = BuildConnectorGraph(layout, pieceMap, TrackConnectorRole.Main);
        if (graph.Count == 0)
        {
            report.Errors.Add("No main loop connectors found.");
            return;
        }

        var visited = new HashSet<string>();
        var components = 0;
        foreach (var node in graph.Keys)
        {
            if (visited.Contains(node))
            {
                continue;
            }

            components++;
            var queue = new Queue<string>();
            queue.Enqueue(node);
            visited.Add(node);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in graph[current])
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        if (components != 1)
        {
            report.Errors.Add("Main track is not one connected component.");
        }

        foreach (var kvp in graph)
        {
            if (kvp.Value.Count != 2)
            {
                report.Errors.Add($"Main connector {kvp.Key} is dangling or over-connected (degree {kvp.Value.Count}).");
            }
        }
    }

    private static void ValidateOverlaps(TrackLayout layout, List<PlacedPiece> pieces, ValidationReport report)
    {
        for (var i = 0; i < pieces.Count; i++)
        {
            for (var j = i + 1; j < pieces.Count; j++)
            {
                var a = TransformBounds(pieces[i]);
                var b = TransformBounds(pieces[j]);
                if (a.Overlaps(b))
                {
                    report.Warnings.Add($"Piece overlap detected between {pieces[i].guid} and {pieces[j].guid}.");
                }
            }
        }
    }

    private static void ValidatePit(TrackLayout layout, Dictionary<string, PlacedPiece> pieceMap, ValidationReport report)
    {
        var pitEntries = layout.pieces.Where(p => p.piece.category == "Pit" && p.piece.pieceId.Contains("PitEntry")).ToList();
        var pitExits = layout.pieces.Where(p => p.piece.category == "Pit" && p.piece.pieceId.Contains("PitExit")).ToList();
        if (pitEntries.Count != 1 || pitExits.Count != 1)
        {
            report.Errors.Add("Pit lane requires exactly one PitEntry and one PitExit piece.");
            return;
        }

        var pitGraph = BuildConnectorGraph(layout, pieceMap, TrackConnectorRole.Pit);
        if (pitGraph.Count == 0)
        {
            report.Errors.Add("Pit path is missing pit links/segments.");
            return;
        }

        var entryPrefix = $"{pitEntries[0].guid}:";
        var exitPrefix = $"{pitExits[0].guid}:";
        var starts = pitGraph.Keys.Where(k => k.StartsWith(entryPrefix)).ToList();
        var targets = new HashSet<string>(pitGraph.Keys.Where(k => k.StartsWith(exitPrefix)));
        var reachedExit = false;
        foreach (var start in starts)
        {
            var stack = new Stack<string>();
            var seen = new HashSet<string>();
            stack.Push(start);
            seen.Add(start);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (targets.Contains(cur))
                {
                    reachedExit = true;
                    break;
                }

                if (!pitGraph.TryGetValue(cur, out var neighbours))
                {
                    continue;
                }

                foreach (var next in neighbours)
                {
                    if (seen.Add(next))
                    {
                        stack.Push(next);
                    }
                }
            }
        }

        if (!reachedExit)
        {
            report.Errors.Add("Pit path does not connect PitEntry to PitExit.");
        }
    }

    private static Dictionary<string, HashSet<string>> BuildConnectorGraph(TrackLayout layout, Dictionary<string, PlacedPiece> pieceMap, TrackConnectorRole role)
    {
        var graph = new Dictionary<string, HashSet<string>>();

        foreach (var placed in pieceMap.Values)
        {
            if (placed.piece?.segments == null)
            {
                continue;
            }

            foreach (var segment in placed.piece.segments)
            {
                if (role == TrackConnectorRole.Pit && segment.pathRole != TrackConnectorRole.Pit)
                {
                    continue;
                }

                if (role == TrackConnectorRole.Main && segment.pathRole == TrackConnectorRole.Pit)
                {
                    continue;
                }

                var from = $"{placed.guid}:{segment.fromConnectorIndex}";
                var to = $"{placed.guid}:{segment.toConnectorIndex}";
                if (!graph.TryAdd(from, new HashSet<string>())) { }
                if (!graph.TryAdd(to, new HashSet<string>())) { }
                graph[from].Add(to);
                graph[to].Add(from);
            }
        }

        foreach (var link in layout.links)
        {
            var a = $"{link.pieceGuidA}:{link.connectorIndexA}";
            var b = $"{link.pieceGuidB}:{link.connectorIndexB}";
            if (!graph.TryAdd(a, new HashSet<string>())) { }
            if (!graph.TryAdd(b, new HashSet<string>())) { }
            graph[a].Add(b);
            graph[b].Add(a);
        }

        return graph;
    }

    private static List<(string pieceGuid, TrackSegment segment)> BuildOrderedSegments(
        TrackLayout layout,
        Dictionary<string, PlacedPiece> pieceMap,
        TrackConnectorRole role,
        bool allowOpenPath = false)
    {
        var result = new List<(string pieceGuid, TrackSegment segment)>();

        foreach (var placed in pieceMap.Values)
        {
            if (placed.piece?.segments == null)
            {
                continue;
            }

            foreach (var segment in placed.piece.segments)
            {
                if (role == TrackConnectorRole.Main && segment.pathRole == TrackConnectorRole.Pit)
                {
                    continue;
                }

                if (role == TrackConnectorRole.Pit && segment.pathRole != TrackConnectorRole.Pit)
                {
                    continue;
                }

                result.Add((placed.guid, segment));
            }
        }

        return result;
    }

    private static Vector2[] TransformPolyline(PlacedPiece placed, Vector2[] local)
    {
        if (local == null)
        {
            return Array.Empty<Vector2>();
        }

        var output = new Vector2[local.Length];
        for (var i = 0; i < local.Length; i++)
        {
            output[i] = TrackMathUtil.ToWorld(placed, local[i]);
        }

        return output;
    }

    private static Vector2[] Stitch(List<Vector2[]> polylines)
    {
        var points = new List<Vector2>();
        foreach (var line in polylines)
        {
            for (var i = 0; i < line.Length; i++)
            {
                if (points.Count > 0 && Vector2.Distance(points[^1], line[i]) < TrackMathUtil.Epsilon)
                {
                    continue;
                }

                points.Add(line[i]);
            }
        }

        if (points.Count > 1 && Vector2.Distance(points[0], points[^1]) > TrackMathUtil.Epsilon)
        {
            points.Add(points[0]);
        }

        return points.ToArray();
    }

    private static Rect TransformBounds(PlacedPiece placed)
    {
        var local = placed.piece.localBounds;
        var corners = new[]
        {
            new Vector2(local.xMin, local.yMin),
            new Vector2(local.xMin, local.yMax),
            new Vector2(local.xMax, local.yMin),
            new Vector2(local.xMax, local.yMax)
        };

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            var w = TrackMathUtil.ToWorld(placed, c);
            min = Vector2.Min(min, w);
            max = Vector2.Max(max, w);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float[] BuildCumulative(Vector2[] polyline)
    {
        if (polyline == null || polyline.Length == 0)
        {
            return Array.Empty<float>();
        }

        var cumulative = new float[polyline.Length];
        var sum = 0f;
        cumulative[0] = 0f;
        for (var i = 1; i < polyline.Length; i++)
        {
            sum += Vector2.Distance(polyline[i - 1], polyline[i]);
            cumulative[i] = sum;
        }

        return cumulative;
    }
}
