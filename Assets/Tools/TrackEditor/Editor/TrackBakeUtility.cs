using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
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

            var implicitMainLinks = BuildImplicitLinks(pieceMap, TrackConnectorRole.Main, 0.06f);
            var implicitPitLinks = BuildImplicitLinks(pieceMap, TrackConnectorRole.Pit, 0.06f);

            ValidateMainLoop(layout, pieceMap, report, implicitMainLinks);
            ValidateOverlaps(layout, pieceMap.Values.ToList(), report);
            ValidatePit(layout, pieceMap, report, implicitPitLinks);

            if (layout.startFinish == null)
            {
                report.Errors.Add("Start/Finish is not set.");
            }
            else
            {
                ValidateStartFinishOnMainTrack(layout, pieceMap, report, implicitMainLinks);
            }

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

            var pieceMap = layout.pieces.Where(p => p?.piece != null).ToDictionary(p => p.guid, p => p);
            var implicitMainLinks = BuildImplicitLinks(pieceMap, TrackConnectorRole.Main, 0.06f);
            var orderedMainSegments = BuildOrderedMainLoopSegments(layout, pieceMap, implicitMainLinks);
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

            var implicitPitLinks = BuildImplicitLinks(pieceMap, TrackConnectorRole.Pit, 0.06f);
            var pitSegments = BuildOrderedSegments(layout, pieceMap, TrackConnectorRole.Pit, implicitPitLinks, allowOpenPath: true);
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

            PopulateStartFinishData(layout, baked);

            EditorUtility.SetDirty(baked);
            AssetDatabase.SaveAssets();
            return baked;
        }

        public static List<(string aGuid, int aIdx, string bGuid, int bIdx)> GetImplicitLinks(TrackLayout layout, TrackConnectorRole role, float epsilonWorld = 0.06f)
        {
            if (layout == null)
            {
                return new List<(string aGuid, int aIdx, string bGuid, int bIdx)>();
            }

            var pieceMap = layout.pieces.Where(p => p?.piece != null).ToDictionary(p => p.guid, p => p);
            return BuildImplicitLinks(pieceMap, role, epsilonWorld);
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

                if (!IsConnectorIndexValid(a, link.connectorIndexA) || !IsConnectorIndexValid(b, link.connectorIndexB))
                {
                    report.Errors.Add("Link references invalid connector index.");
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

        private static void ValidateMainLoop(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            ValidationReport report,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> implicitLinks)
        {
            var graph = BuildConnectorGraph(layout, pieceMap, TrackConnectorRole.Main, implicitLinks);
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

            var dangling = graph.Where(kvp => kvp.Value.Count != 2).Select(kvp => kvp.Key).Take(5).ToList();
            var danglingCount = graph.Count(kvp => kvp.Value.Count != 2);
            var allDegreeTwo = danglingCount == 0;

            if (components != 1 || !allDegreeTwo)
            {
                var danglingPreview = dangling.Count > 0 ? string.Join(", ", dangling) : "none";
                report.Errors.Add($"Main track must form exactly one closed loop. Components={components}, dangling connectors={danglingCount}, sample={danglingPreview}.");
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

        private static void ValidatePit(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            ValidationReport report,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> implicitLinks)
        {
            var hasPitRelatedPieces = layout.pieces.Any(p => p?.piece != null && (p.piece.category == "Pit" || p.piece.category == "PitLane"));
            if (!hasPitRelatedPieces)
            {
                return;
            }

            var pitEntries = layout.pieces.Where(p => p.piece.category == "Pit" && p.piece.pieceId.Contains("PitEntry")).ToList();
            var pitExits = layout.pieces.Where(p => p.piece.category == "Pit" && p.piece.pieceId.Contains("PitExit")).ToList();
            if (pitEntries.Count != 1 || pitExits.Count != 1)
            {
                report.Errors.Add("Pit pieces present, but missing PitEntry/PitExit. Add exactly one of each or remove pit pieces.");
                return;
            }

            var pitGraph = BuildConnectorGraph(layout, pieceMap, TrackConnectorRole.Pit, implicitLinks);
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

        private static void ValidateStartFinishOnMainTrack(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            ValidationReport report,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> implicitLinks)
        {
            if (layout.startFinish.worldDir.sqrMagnitude < 0.0001f)
            {
                report.Errors.Add("Start/Finish direction is invalid.");
                return;
            }

            var orderedMainSegments = BuildOrderedMainLoopSegments(layout, pieceMap, implicitLinks);
            var mainCenter = Stitch(orderedMainSegments.Select(s => TransformPolyline(pieceMap[s.pieceGuid], s.segment.localCenterline)).ToList());
            if (!TryFindClosestPointOnPolyline(mainCenter, layout.startFinish.worldPos, out _, out _, out _, out _))
            {
                report.Errors.Add("Main centerline is empty; cannot validate Start/Finish.");
                return;
            }

            var trackWidth = layout.pieces.FirstOrDefault(p => p?.piece != null)?.piece?.trackWidth ?? 8f;
            if (!TryFindClosestPointOnPolyline(mainCenter, layout.startFinish.worldPos, out _, out _, out _, out var distance) || distance > trackWidth * 0.75f)
            {
                report.Errors.Add("Start/Finish is not on the main track.");
            }
        }

        private static Dictionary<string, HashSet<string>> BuildConnectorGraph(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            TrackConnectorRole role,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> extraLinks = null)
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
                    if (!SegmentMatchesRole(segment, role))
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

            if (layout?.links != null)
            {
                foreach (var link in layout.links)
                {
                    if (!TryGetConnector(pieceMap, link.pieceGuidA, link.connectorIndexA, out var connectorA) ||
                        !TryGetConnector(pieceMap, link.pieceGuidB, link.connectorIndexB, out var connectorB))
                    {
                        continue;
                    }

                    if (!ConnectorRoleMatches(connectorA.role, role) || !ConnectorRoleMatches(connectorB.role, role))
                    {
                        continue;
                    }

                    var a = $"{link.pieceGuidA}:{link.connectorIndexA}";
                    var b = $"{link.pieceGuidB}:{link.connectorIndexB}";
                    if (!graph.TryAdd(a, new HashSet<string>())) { }
                    if (!graph.TryAdd(b, new HashSet<string>())) { }
                    graph[a].Add(b);
                    graph[b].Add(a);
                }
            }

            if (extraLinks != null)
            {
                foreach (var link in extraLinks)
                {
                    var a = $"{link.aGuid}:{link.aIdx}";
                    var b = $"{link.bGuid}:{link.bIdx}";
                    if (!graph.TryAdd(a, new HashSet<string>())) { }
                    if (!graph.TryAdd(b, new HashSet<string>())) { }
                    graph[a].Add(b);
                    graph[b].Add(a);
                }
            }

            return graph;
        }

        private static List<(string aGuid, int aIdx, string bGuid, int bIdx)> BuildImplicitLinks(
            Dictionary<string, PlacedPiece> pieceMap,
            TrackConnectorRole role,
            float epsilonWorld)
        {
            var candidates = new List<(string guid, int idx, Vector2 worldPos, Dir8 worldDir, float width)>();
            foreach (var placed in pieceMap.Values)
            {
                if (placed?.piece?.connectors == null)
                {
                    continue;
                }

                for (var i = 0; i < placed.piece.connectors.Length; i++)
                {
                    var connector = placed.piece.connectors[i];
                    if (connector == null || !ConnectorRoleMatches(connector.role, role))
                    {
                        continue;
                    }

                    candidates.Add((
                        placed.guid,
                        i,
                        TrackMathUtil.ToWorld(placed, connector.localPos),
                        TrackMathUtil.ToWorld(placed, connector.localDir),
                        connector.trackWidth));
                }
            }

            var links = new List<(string aGuid, int aIdx, string bGuid, int bIdx)>();
            var dedupe = new HashSet<string>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var a = candidates[i];
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var b = candidates[j];
                    if (a.guid == b.guid)
                    {
                        continue;
                    }

                    if (Vector2.Distance(a.worldPos, b.worldPos) > epsilonWorld)
                    {
                        continue;
                    }

                    if (a.worldDir != b.worldDir.Opposite())
                    {
                        continue;
                    }

                    if (Mathf.Abs(a.width - b.width) > 0.01f)
                    {
                        continue;
                    }

                    var normalized = NormalizeLink(a.guid, a.idx, b.guid, b.idx);
                    var dedupeKey = $"{normalized.aGuid}:{normalized.aIdx}|{normalized.bGuid}:{normalized.bIdx}";
                    if (dedupe.Add(dedupeKey))
                    {
                        links.Add(normalized);
                    }
                }
            }

            links.Sort((x, y) =>
            {
                var cmp = string.CompareOrdinal(x.aGuid, y.aGuid);
                if (cmp != 0) return cmp;
                cmp = x.aIdx.CompareTo(y.aIdx);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(x.bGuid, y.bGuid);
                return cmp != 0 ? cmp : x.bIdx.CompareTo(y.bIdx);
            });

            return links;
        }

        private static List<(string pieceGuid, TrackSegment segment)> BuildOrderedMainLoopSegments(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> implicitLinks)
        {
            var ordered = new List<(string pieceGuid, TrackSegment segment)>();
            var graph = BuildConnectorGraph(layout, pieceMap, TrackConnectorRole.Main, implicitLinks);
            if (graph.Count == 0)
            {
                return ordered;
            }

            var start = graph.Keys.First();
            if (graph[start].Count == 0)
            {
                return ordered;
            }

            string prev = null;
            var cur = start;
            var next = graph[cur].First();
            var guardMax = graph.Count * 4;
            var guardSteps = 0;

            while (guardSteps++ < guardMax)
            {
                if (TryParseNode(cur, out var curGuid, out var curIdx) &&
                    TryParseNode(next, out var nextGuid, out var nextIdx) &&
                    curGuid == nextGuid &&
                    pieceMap.TryGetValue(curGuid, out var placed) &&
                    placed.piece?.segments != null)
                {
                    var matchingSegment = placed.piece.segments.FirstOrDefault(s =>
                        s.fromConnectorIndex == curIdx &&
                        s.toConnectorIndex == nextIdx &&
                        SegmentMatchesRole(s, TrackConnectorRole.Main));
                    if (matchingSegment != null)
                    {
                        ordered.Add((curGuid, matchingSegment));
                    }
                }

                prev = cur;
                cur = next;

                var neighbors = graph[cur].Where(n => n != prev).ToList();
                if (neighbors.Count == 0)
                {
                    break;
                }

                next = neighbors[0];
                if (cur == start)
                {
                    break;
                }
            }

            return ordered;
        }

        private static List<(string pieceGuid, TrackSegment segment)> BuildOrderedSegments(
            TrackLayout layout,
            Dictionary<string, PlacedPiece> pieceMap,
            TrackConnectorRole role,
            List<(string aGuid, int aIdx, string bGuid, int bIdx)> extraLinks = null,
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
                    if (!SegmentMatchesRole(segment, role))
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

        private static bool SegmentMatchesRole(TrackSegment segment, TrackConnectorRole role)
        {
            if (segment == null)
            {
                return false;
            }

            if (role == TrackConnectorRole.Pit)
            {
                return segment.pathRole == TrackConnectorRole.Pit;
            }

            if (role == TrackConnectorRole.Main)
            {
                return segment.pathRole != TrackConnectorRole.Pit;
            }

            return true;
        }

        private static bool ConnectorRoleMatches(TrackConnectorRole connectorRole, TrackConnectorRole requiredRole)
        {
            return connectorRole == TrackConnectorRole.Any || requiredRole == TrackConnectorRole.Any || connectorRole == requiredRole;
        }

        private static (string aGuid, int aIdx, string bGuid, int bIdx) NormalizeLink(string aGuid, int aIdx, string bGuid, int bIdx)
        {
            var leftKey = $"{aGuid}:{aIdx:D4}";
            var rightKey = $"{bGuid}:{bIdx:D4}";
            return string.CompareOrdinal(leftKey, rightKey) <= 0
                ? (aGuid, aIdx, bGuid, bIdx)
                : (bGuid, bIdx, aGuid, aIdx);
        }

        private static bool TryParseNode(string node, out string guid, out int connectorIndex)
        {
            guid = string.Empty;
            connectorIndex = -1;
            if (string.IsNullOrWhiteSpace(node))
            {
                return false;
            }

            var split = node.Split(':');
            if (split.Length != 2 || !int.TryParse(split[1], out connectorIndex))
            {
                return false;
            }

            guid = split[0];
            return true;
        }

        private static bool TryGetConnector(
            Dictionary<string, PlacedPiece> pieceMap,
            string pieceGuid,
            int connectorIndex,
            out TrackConnector connector)
        {
            connector = null;
            return pieceMap.TryGetValue(pieceGuid, out var placed) &&
                   IsConnectorIndexValid(placed, connectorIndex) &&
                   (connector = placed.piece.connectors[connectorIndex]) != null;
        }

        private static bool IsConnectorIndexValid(PlacedPiece placed, int idx)
        {
            return placed?.piece?.connectors != null && idx >= 0 && idx < placed.piece.connectors.Length;
        }

        private static void PopulateStartFinishData(TrackLayout layout, TrackBakedData baked)
        {
            baked.startFinishPos = Vector2.zero;
            baked.startFinishDir = Vector2.right;
            baked.startFinishDistance = 0f;

            if (layout?.startFinish == null || baked.mainCenterline == null || baked.mainCenterline.Length < 2)
            {
                return;
            }

            if (!TryFindClosestPointOnPolyline(
                    baked.mainCenterline,
                    layout.startFinish.worldPos,
                    out var closestPoint,
                    out var tangent,
                    out var distanceAlong,
                    out _))
            {
                return;
            }

            baked.startFinishPos = closestPoint;
            baked.startFinishDir = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.right;
            baked.startFinishDistance = distanceAlong;
        }

        private static bool TryFindClosestPointOnPolyline(
            Vector2[] polyline,
            Vector2 point,
            out Vector2 closestPoint,
            out Vector2 tangent,
            out float distanceAlong,
            out float distanceToPolyline)
        {
            closestPoint = Vector2.zero;
            tangent = Vector2.right;
            distanceAlong = 0f;
            distanceToPolyline = float.MaxValue;

            if (polyline == null || polyline.Length < 2)
            {
                return false;
            }

            var found = false;
            var accumulated = 0f;
            for (var i = 0; i < polyline.Length - 1; i++)
            {
                var a = polyline[i];
                var b = polyline[i + 1];
                var ab = b - a;
                var abLen = ab.magnitude;
                if (abLen < 0.0001f)
                {
                    continue;
                }

                var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / (abLen * abLen));
                var c = a + ab * t;
                var d = Vector2.Distance(point, c);
                if (d < distanceToPolyline)
                {
                    found = true;
                    distanceToPolyline = d;
                    closestPoint = c;
                    tangent = ab / abLen;
                    distanceAlong = accumulated + abLen * t;
                }

                accumulated += abLen;
            }

            return found;
        }
    }
}
