using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    public static class TrackEditorDefaultAssetsMenu
    {
        private const string BaseFolder = "Assets/Tools/TrackEditor/Defaults";
        private const string PieceFolder = BaseFolder + "/Pieces";
        private const string LibraryPath = BaseFolder + "/TrackPieceLibrary.asset";

        private const float L = 10f;
        private const float TrackWidth = 8f;
        private const float HW = TrackWidth * 0.5f;
        private static readonly float D = L / Mathf.Sqrt(2f);

        [MenuItem("GSP/TrackEditor/Create Default Track Pieces")]
        public static void CreateDefaultTrackPieces()
        {
            EnsureFolder("Assets/Tools");
            EnsureFolder("Assets/Tools/TrackEditor");
            EnsureFolder(BaseFolder);
            EnsureFolder(PieceFolder);

            DeleteDefaultPieceAssets();
            DeleteDefaultLibraryAsset();

            var pieces = new List<TrackPieceDef>();

            var mainStraightBase = BuildStraightBase("MainStraightBase", "MAIN — Straight H", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainStraightBase, 0, "MainStraightH", "MAIN — Straight H"));
            pieces.Add(CloneRotated(mainStraightBase, 2, "MainStraightV", "MAIN — Straight V"));
            pieces.Add(CloneRotated(mainStraightBase, 1, "MainStraight45Slash", "MAIN — Straight 45 /"));
            pieces.Add(CloneRotated(mainStraightBase, 3, "MainStraight45Backslash", @"MAIN — Straight 45 \"));

            var mainCornerBase = BuildCorner90Base("MainCorner90ENBase", "MAIN — Corner 90 (E+N)", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainCornerBase, 0, "MainCorner90EN", "MAIN — Corner 90 (E+N)"));
            pieces.Add(CloneRotated(mainCornerBase, 2, "MainCorner90NW", "MAIN — Corner 90 (N+W)"));
            pieces.Add(CloneRotated(mainCornerBase, 4, "MainCorner90WS", "MAIN — Corner 90 (W+S)"));
            pieces.Add(CloneRotated(mainCornerBase, 6, "MainCorner90SE", "MAIN — Corner 90 (S+E)"));

            var hairpinH = BuildHairpin180Horizontal();
            pieces.Add(CreatePiece("MainHairpin180WE", "MAIN — Hairpin 180 (W+E)", "Main", TrackWidth, hairpinH.connectors, hairpinH.segments));
            pieces.Add(CloneRotated(hairpinH, 2, "MainHairpin180NS", "MAIN — Hairpin 180 (N+S)"));

            var pitStraightBase = BuildStraightBase("PitStraightBase", "PIT — Straight H", "PitLane", TrackConnectorRole.Pit);
            pieces.Add(CloneRotated(pitStraightBase, 0, "PitLaneStraightH", "PIT — Straight H"));
            pieces.Add(CloneRotated(pitStraightBase, 2, "PitLaneStraightV", "PIT — Straight V"));
            pieces.Add(CloneRotated(pitStraightBase, 1, "PitLaneStraight45Slash", "PIT — Straight 45 /"));
            pieces.Add(CloneRotated(pitStraightBase, 3, "PitLaneStraight45Backslash", @"PIT — Straight 45 \"));

            var pitCornerBase = BuildCorner90Base("PitCorner90ENBase", "PIT — Corner 90 (E+N)", "PitLane", TrackConnectorRole.Pit);
            pieces.Add(CloneRotated(pitCornerBase, 0, "PitLaneCorner90EN", "PIT — Corner 90 (E+N)"));
            pieces.Add(CloneRotated(pitCornerBase, 2, "PitLaneCorner90NW", "PIT — Corner 90 (N+W)"));
            pieces.Add(CloneRotated(pitCornerBase, 4, "PitLaneCorner90WS", "PIT — Corner 90 (W+S)"));
            pieces.Add(CloneRotated(pitCornerBase, 6, "PitLaneCorner90SE", "PIT — Corner 90 (S+E)"));

            var pitEntry90 = BuildPitEntry90();
            var pitExit90 = BuildPitExit90();
            var pitEntryParallel = BuildPitEntryParallel();
            var pitExitParallel = BuildPitExitParallel();
            foreach (var steps in new[] { 0, 2, 4, 6 })
            {
                pieces.Add(CloneRotated(pitEntry90, steps, $"PitEntry90_{steps}", $"PIT — Entry 90° (Main+Pit) [{steps * 45}°]", "Pit"));
                pieces.Add(CloneRotated(pitExit90, steps, $"PitExit90_{steps}", $"PIT — Exit 90° (Main+Pit) [{steps * 45}°]", "Pit"));
                pieces.Add(CloneRotated(pitEntryParallel, steps, $"PitEntryParallel_{steps}", $"PIT — Entry Parallel (Main+Pit) [{steps * 45}°]", "Pit"));
                pieces.Add(CloneRotated(pitExitParallel, steps, $"PitExitParallel_{steps}", $"PIT — Exit Parallel (Main+Pit) [{steps * 45}°]", "Pit"));
            }

            var library = ScriptableObject.CreateInstance<TrackPieceLibrary>();
            library.pieces = pieces;
            AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"TrackEditor defaults recreated at '{BaseFolder}' with library '{LibraryPath}'.");
        }

        private static TrackPieceDef BuildStraightBase(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var segments = new[]
            {
                Segment(0, 1, role, new[] { c0.localPos, c1.localPos }, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, new[] { c1.localPos, c0.localPos }, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreateTransientPiece(pieceId, displayName, category, TrackWidth, new[] { c0, c1 }, segments);
        }

        private static TrackPieceDef BuildCorner90Base(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var c1 = Connector("N", new Vector2(0f, L), Dir8.N, role);
            var path = MakeCorner90QuarterArc(16);
            var segments = new[]
            {
                Segment(0, 1, role, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, Reverse(path), -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreateTransientPiece(pieceId, displayName, category, TrackWidth, new[] { c0, c1 }, segments);
        }

        private static TrackPieceDef BuildHairpin180Horizontal()
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);

            var control = new[]
            {
                c0.localPos,
                new Vector2(-2f * L, 0f),
                new Vector2(-2f * L, -2f * L),
                new Vector2(2f * L, -2f * L),
                new Vector2(2f * L, 0f),
                c1.localPos
            };
            var path = FilletPolyline(control, L * 0.55f, 8);
            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, TrackConnectorRole.Main, Reverse(path), -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreateTransientPiece("MainHairpin180Base", "MAIN — Hairpin 180 (W+E)", "Main", TrackWidth, new[] { c0, c1 }, segments);
        }

        private static TrackPieceDef BuildPitEntry90()
        {
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var pitPath = MakeCubicBezier(mainIn.localPos, mainIn.localPos + Vector2.right * 7f, pitOut.localPos + Vector2.up * 7f, pitOut.localPos, 14);

            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -mainIn.localDir.ToVector2(), pitOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, Reverse(pitPath), -pitOut.localDir.ToVector2(), mainIn.localDir.ToVector2())
            };

            return CreateTransientPiece("PitEntry90Base", "PIT — Entry 90° (Main+Pit)", "Pit", TrackWidth, new[] { mainIn, mainOut, pitOut }, segments);
        }

        private static TrackPieceDef BuildPitExit90()
        {
            var pitIn = Connector("PitIn", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitPath = MakeCubicBezier(pitIn.localPos, pitIn.localPos + Vector2.up * 7f, mainOut.localPos + Vector2.left * 7f, mainOut.localPos, 14);

            var segments = new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -pitIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, Reverse(pitPath), -mainOut.localDir.ToVector2(), pitIn.localDir.ToVector2())
            };

            return CreateTransientPiece("PitExit90Base", "PIT — Exit 90° (Main+Pit)", "Pit", TrackWidth, new[] { pitIn, mainIn, mainOut }, segments);
        }

        private static TrackPieceDef BuildPitEntryParallel()
        {
            var offset = TrackWidth * 1.2f;
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(L, -offset), Dir8.E, TrackConnectorRole.Pit);
            var pitPath = MakeCubicBezier(mainIn.localPos, mainIn.localPos + Vector2.right * 8f, pitOut.localPos + Vector2.left * 8f, pitOut.localPos, 14);

            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -mainIn.localDir.ToVector2(), pitOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, Reverse(pitPath), -pitOut.localDir.ToVector2(), mainIn.localDir.ToVector2())
            };

            return CreateTransientPiece("PitEntryParallelBase", "PIT — Entry Parallel (Main+Pit)", "Pit", TrackWidth, new[] { mainIn, mainOut, pitOut }, segments);
        }

        private static TrackPieceDef BuildPitExitParallel()
        {
            var offset = TrackWidth * 1.2f;
            var pitIn = Connector("PitIn", new Vector2(-L, -offset), Dir8.W, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitPath = MakeCubicBezier(pitIn.localPos, pitIn.localPos + Vector2.right * 8f, mainOut.localPos + Vector2.left * 8f, mainOut.localPos, 14);

            var segments = new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -pitIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, Reverse(pitPath), -mainOut.localDir.ToVector2(), pitIn.localDir.ToVector2())
            };

            return CreateTransientPiece("PitExitParallelBase", "PIT — Exit Parallel (Main+Pit)", "Pit", TrackWidth, new[] { pitIn, mainIn, mainOut }, segments);
        }

        private static TrackPieceDef CloneRotated(TrackPieceDef baseDef, int steps, string newId, string newName, string category = null)
        {
            var connectors = new TrackConnector[baseDef.connectors.Length];
            for (var i = 0; i < baseDef.connectors.Length; i++)
            {
                var c = baseDef.connectors[i];
                connectors[i] = new TrackConnector
                {
                    id = c.id,
                    localPos = Rot45(c.localPos, steps),
                    localDir = RotDir(c.localDir, steps),
                    role = c.role,
                    trackWidth = c.trackWidth
                };
            }

            var segments = new TrackSegment[baseDef.segments.Length];
            for (var i = 0; i < baseDef.segments.Length; i++)
            {
                var s = baseDef.segments[i];
                segments[i] = new TrackSegment
                {
                    fromConnectorIndex = s.fromConnectorIndex,
                    toConnectorIndex = s.toConnectorIndex,
                    pathRole = s.pathRole,
                    localCenterline = RotatePoints(s.localCenterline, steps),
                    localLeftBoundary = RotatePoints(s.localLeftBoundary, steps),
                    localRightBoundary = RotatePoints(s.localRightBoundary, steps)
                };
            }

            return CreatePiece(newId, newName, category ?? baseDef.category, baseDef.trackWidth, connectors, segments);
        }

        private static Vector2 Rot45(Vector2 p, int steps)
        {
            return TrackMathUtil.Rotate45(p, steps);
        }

        private static Dir8 RotDir(Dir8 d, int steps)
        {
            return d.RotateSteps45(steps);
        }

        private static Vector2[] RotatePoints(Vector2[] points, int steps)
        {
            if (points == null)
            {
                return null;
            }

            var rotated = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                rotated[i] = Rot45(points[i], steps);
            }

            return rotated;
        }

        private static Vector2[] MakeCorner90QuarterArc(int steps)
        {
            var center = new Vector2(L, L);
            var samples = Mathf.Max(1, steps);
            var path = new Vector2[samples + 1];
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var angle = Mathf.Lerp(-Mathf.PI * 0.5f, -Mathf.PI, t);
                path[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * L;
            }

            path[0] = new Vector2(L, 0f);
            path[samples] = new Vector2(0f, L);
            return path;
        }

        private static Vector2[] MakeCubicBezier(Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, int steps)
        {
            var count = Mathf.Max(1, steps);
            var result = new Vector2[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var omt = 1f - t;
                result[i] =
                    omt * omt * omt * p0 +
                    3f * omt * omt * t * c0 +
                    3f * omt * t * t * c1 +
                    t * t * t * p1;
            }

            result[0] = p0;
            result[count] = p1;
            return result;
        }

        private static Vector2[] FilletPolyline(Vector2[] pts, float r, int stepsPerCorner)
        {
            if (pts == null || pts.Length < 2)
            {
                return pts ?? new Vector2[0];
            }

            var result = new List<Vector2> { pts[0] };
            for (var i = 1; i < pts.Length - 1; i++)
            {
                var prev = pts[i - 1];
                var curr = pts[i];
                var next = pts[i + 1];

                var inDir = (curr - prev).normalized;
                var outDir = (next - curr).normalized;
                var turn = Vector2.SignedAngle(inDir, outDir);
                if (Mathf.Abs(turn) < 1f)
                {
                    result.Add(curr);
                    continue;
                }

                var theta = Mathf.Abs(turn) * Mathf.Deg2Rad;
                var maxRadius = Mathf.Min((curr - prev).magnitude, (next - curr).magnitude) * 0.49f;
                var cornerRadius = Mathf.Min(r, maxRadius);
                if (cornerRadius <= 0.001f)
                {
                    result.Add(curr);
                    continue;
                }

                var tangentOffset = cornerRadius / Mathf.Tan(theta * 0.5f);
                var start = curr - inDir * tangentOffset;
                var end = curr + outDir * tangentOffset;

                var sign = Mathf.Sign(turn);
                var nIn = sign > 0f ? new Vector2(-inDir.y, inDir.x) : new Vector2(inDir.y, -inDir.x);
                var center = start + nIn * cornerRadius;

                var startAngle = Mathf.Atan2(start.y - center.y, start.x - center.x);
                var endAngle = Mathf.Atan2(end.y - center.y, end.x - center.x);
                var ccw = Mathf.Repeat(endAngle - startAngle, Mathf.PI * 2f);
                var cw = ccw - Mathf.PI * 2f;
                var delta = sign > 0f ? ccw : cw;

                result.Add(start);
                for (var step = 1; step <= stepsPerCorner; step++)
                {
                    var t = step / (float)(stepsPerCorner + 1);
                    var angle = startAngle + delta * t;
                    result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius);
                }

                result.Add(end);
            }

            result.Add(pts[pts.Length - 1]);
            return result.ToArray();
        }

        private static TrackSegment Segment(
            int from,
            int to,
            TrackConnectorRole role,
            Vector2[] center,
            Vector2 startTravelDirWorldLocal,
            Vector2 endTravelDirWorldLocal)
        {
            var left = new Vector2[center.Length];
            var right = new Vector2[center.Length];
            var startDir = startTravelDirWorldLocal.normalized;
            var endDir = endTravelDirWorldLocal.normalized;

            for (var i = 0; i < center.Length; i++)
            {
                Vector2 tangent;
                if (i == 0)
                {
                    tangent = startDir;
                }
                else if (i == center.Length - 1)
                {
                    tangent = endDir;
                }
                else
                {
                    tangent = center[i + 1] - center[i - 1];
                }

                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = i > 0 ? center[i] - center[i - 1] : Vector2.right;
                }

                var tangentNormalized = tangent.normalized;
                var normal = new Vector2(-tangentNormalized.y, tangentNormalized.x);
                left[i] = center[i] + normal * HW;
                right[i] = center[i] - normal * HW;
            }

            return new TrackSegment
            {
                fromConnectorIndex = from,
                toConnectorIndex = to,
                pathRole = role,
                localCenterline = center,
                localLeftBoundary = left,
                localRightBoundary = right
            };
        }

        private static Vector2[] Reverse(Vector2[] points)
        {
            var output = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                output[i] = points[points.Length - 1 - i];
            }

            return output;
        }

        private static TrackPieceDef CreateTransientPiece(string pieceId, string displayName, string category, float trackWidth, TrackConnector[] connectors, TrackSegment[] segments)
        {
            var piece = ScriptableObject.CreateInstance<TrackPieceDef>();
            piece.pieceId = pieceId;
            piece.displayName = displayName;
            piece.category = category;
            piece.trackWidth = trackWidth;
            piece.connectors = connectors;
            piece.segments = segments;
            piece.localBounds = BuildBounds(connectors, segments);
            return piece;
        }

        private static TrackPieceDef CreatePiece(string pieceId, string displayName, string category, float trackWidth, TrackConnector[] connectors, TrackSegment[] segments)
        {
            var piece = CreateTransientPiece(pieceId, displayName, category, trackWidth, connectors, segments);
            var path = $"{PieceFolder}/{pieceId}.asset";
            AssetDatabase.CreateAsset(piece, path);
            EditorUtility.SetDirty(piece);
            return piece;
        }

        private static TrackConnector Connector(string id, Vector2 pos, Dir8 dir, TrackConnectorRole role)
        {
            return new TrackConnector
            {
                id = id,
                localPos = pos,
                localDir = dir,
                role = role,
                trackWidth = TrackWidth
            };
        }

        private static Rect BuildBounds(IEnumerable<TrackConnector> connectors, IEnumerable<TrackSegment> segments)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var connector in connectors)
            {
                ExpandPoint(connector.localPos, ref minX, ref minY, ref maxX, ref maxY);
            }

            foreach (var segment in segments)
            {
                ExpandBounds(segment.localCenterline, ref minX, ref minY, ref maxX, ref maxY);
                ExpandBounds(segment.localLeftBoundary, ref minX, ref minY, ref maxX, ref maxY);
                ExpandBounds(segment.localRightBoundary, ref minX, ref minY, ref maxX, ref maxY);
            }

            return Rect.MinMaxRect(minX - 0.2f, minY - 0.2f, maxX + 0.2f, maxY + 0.2f);
        }

        private static void ExpandBounds(IEnumerable<Vector2> points, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            if (points == null)
            {
                return;
            }

            foreach (var point in points)
            {
                ExpandPoint(point, ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        private static void ExpandPoint(Vector2 point, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        private static void DeleteDefaultPieceAssets()
        {
            var guids = AssetDatabase.FindAssets("t:Object", new[] { PieceFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".asset"))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void DeleteDefaultLibraryAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TrackPieceLibrary>(LibraryPath) != null || AssetDatabase.AssetPathExists(LibraryPath))
            {
                AssetDatabase.DeleteAsset(LibraryPath);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var split = folder.Split('/');
            var current = split[0];
            for (var i = 1; i < split.Length; i++)
            {
                var next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }
    }
}
