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
            var mainStraight45Base = BuildStraight45Base("MainStraight45Base", "MAIN — Straight 45 /", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainStraightBase, 0, "MainStraightH", "MAIN — Straight H"));
            pieces.Add(CloneRotated(mainStraightBase, 2, "MainStraightV", "MAIN — Straight V"));
            pieces.Add(CloneRotated(mainStraight45Base, 0, "MainStraight45Slash", "MAIN — Straight 45 /"));
            pieces.Add(CloneRotated(mainStraight45Base, 2, "MainStraight45Backslash", @"MAIN — Straight 45 \"));

            var mainCorner45CcwBase = BuildCorner45BaseCcw("MainCorner45CcwBase", "MAIN — Corner 45 CCW (E+NE)", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainCorner45CcwBase, 0, "MainCorner45CcwENE", "MAIN — Corner 45 CCW (E+NE)"));
            pieces.Add(CloneRotated(mainCorner45CcwBase, 2, "MainCorner45CcwNNW", "MAIN — Corner 45 CCW (N+NW)"));
            pieces.Add(CloneRotated(mainCorner45CcwBase, 4, "MainCorner45CcwWSW", "MAIN — Corner 45 CCW (W+SW)"));
            pieces.Add(CloneRotated(mainCorner45CcwBase, 6, "MainCorner45CcwSSE", "MAIN — Corner 45 CCW (S+SE)"));

            var mainCorner45CwBase = BuildCorner45BaseCw("MainCorner45CwBase", "MAIN — Corner 45 CW (E+SE)", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainCorner45CwBase, 0, "MainCorner45CwESE", "MAIN — Corner 45 CW (E+SE)"));
            pieces.Add(CloneRotated(mainCorner45CwBase, 2, "MainCorner45CwSSW", "MAIN — Corner 45 CW (S+SW)"));
            pieces.Add(CloneRotated(mainCorner45CwBase, 4, "MainCorner45CwWNW", "MAIN — Corner 45 CW (W+NW)"));
            pieces.Add(CloneRotated(mainCorner45CwBase, 6, "MainCorner45CwNNE", "MAIN — Corner 45 CW (N+NE)"));

            var mainCornerBase = BuildCorner90Base("MainCorner90ENBase", "MAIN — Corner 90 (E+N)", "Main", TrackConnectorRole.Main);
            pieces.Add(CloneRotated(mainCornerBase, 0, "MainCorner90EN", "MAIN — Corner 90 (E+N)"));
            pieces.Add(CloneRotated(mainCornerBase, 2, "MainCorner90NW", "MAIN — Corner 90 (N+W)"));
            pieces.Add(CloneRotated(mainCornerBase, 4, "MainCorner90WS", "MAIN — Corner 90 (W+S)"));
            pieces.Add(CloneRotated(mainCornerBase, 6, "MainCorner90SE", "MAIN — Corner 90 (S+E)"));

            var hairpinH = BuildHairpin180Horizontal();
            pieces.Add(CreatePiece("MainHairpin180WE", "MAIN — Hairpin 180 (W+E)", "Main", TrackWidth, hairpinH.connectors, hairpinH.segments));
            pieces.Add(CloneRotated(hairpinH, 2, "MainHairpin180NS", "MAIN — Hairpin 180 (N+S)"));

            var pitStraightBase = BuildStraightBase("PitStraightBase", "PIT — Straight H", "PitLane", TrackConnectorRole.Pit);
            var pitStraight45Base = BuildStraight45Base("PitStraight45Base", "PIT — Straight 45 /", "PitLane", TrackConnectorRole.Pit);
            pieces.Add(CloneRotated(pitStraightBase, 0, "PitLaneStraightH", "PIT — Straight H"));
            pieces.Add(CloneRotated(pitStraightBase, 2, "PitLaneStraightV", "PIT — Straight V"));
            pieces.Add(CloneRotated(pitStraight45Base, 0, "PitLaneStraight45Slash", "PIT — Straight 45 /"));
            pieces.Add(CloneRotated(pitStraight45Base, 2, "PitLaneStraight45Backslash", @"PIT — Straight 45 \"));

            var pitCorner45CcwBase = BuildCorner45BaseCcw("PitCorner45CcwBase", "PIT — Corner 45 CCW (E+NE)", "PitLane", TrackConnectorRole.Pit);
            pieces.Add(CloneRotated(pitCorner45CcwBase, 0, "PitLaneCorner45CcwENE", "PIT — Corner 45 CCW (E+NE)"));
            pieces.Add(CloneRotated(pitCorner45CcwBase, 2, "PitLaneCorner45CcwNNW", "PIT — Corner 45 CCW (N+NW)"));
            pieces.Add(CloneRotated(pitCorner45CcwBase, 4, "PitLaneCorner45CcwWSW", "PIT — Corner 45 CCW (W+SW)"));
            pieces.Add(CloneRotated(pitCorner45CcwBase, 6, "PitLaneCorner45CcwSSE", "PIT — Corner 45 CCW (S+SE)"));

            var pitCorner45CwBase = BuildCorner45BaseCw("PitCorner45CwBase", "PIT — Corner 45 CW (E+SE)", "PitLane", TrackConnectorRole.Pit);
            pieces.Add(CloneRotated(pitCorner45CwBase, 0, "PitLaneCorner45CwESE", "PIT — Corner 45 CW (E+SE)"));
            pieces.Add(CloneRotated(pitCorner45CwBase, 2, "PitLaneCorner45CwSSW", "PIT — Corner 45 CW (S+SW)"));
            pieces.Add(CloneRotated(pitCorner45CwBase, 4, "PitLaneCorner45CwWNW", "PIT — Corner 45 CW (W+NW)"));
            pieces.Add(CloneRotated(pitCorner45CwBase, 6, "PitLaneCorner45CwNNE", "PIT — Corner 45 CW (N+NE)"));

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

        private static TrackPieceDef BuildStraight45Base(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("SW", new Vector2(-L, -L), Dir8.SW, role);
            var c1 = Connector("NE", new Vector2(L, L), Dir8.NE, role);
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
            var path = BezierFromConnectors(c0, c1, 18);
            var segments = new[]
            {
                Segment(0, 1, role, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, Reverse(path), -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreateTransientPiece(pieceId, displayName, category, TrackWidth, new[] { c0, c1 }, segments);
        }

        private static TrackPieceDef BuildCorner45BaseCcw(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var c1 = Connector("NE", new Vector2(L, L), Dir8.NE, role);

            var p0 = c0.localPos;
            var p1 = c1.localPos;
            var startTravelDir = -c0.localDir.ToVector2();
            var endTravelDir = c1.localDir.ToVector2();
            var path = BezierByHandleRule(p0, p1, startTravelDir, endTravelDir, 14);

            var segments = new[]
            {
                Segment(0, 1, role, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, Reverse(path), -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreateTransientPiece(pieceId, displayName, category, TrackWidth, new[] { c0, c1 }, segments);
        }

        private static TrackPieceDef BuildCorner45BaseCw(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var c1 = Connector("SE", new Vector2(L, -L), Dir8.SE, role);

            var p0 = c0.localPos;
            var p1 = c1.localPos;
            var startTravelDir = -c0.localDir.ToVector2();
            var endTravelDir = c1.localDir.ToVector2();
            var path = BezierByHandleRule(p0, p1, startTravelDir, endTravelDir, 14);

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

            var p0 = new Vector2(-L, 0f);
            var p1 = new Vector2(-L, -1.6f * L);
            var p2 = new Vector2(L, -1.6f * L);
            var p3 = new Vector2(L, 0f);

            var travelDir = Vector2.right;
            var seg1 = BezierByHandleRule(p0, p1, travelDir, travelDir, 14);
            var seg2 = BezierByHandleRule(p2, p3, travelDir, travelDir, 14);
            var path = CombineUnique(seg1, new[] { p1, p2 }, seg2);
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
            var pitPath = BezierByHandleRule(mainIn.localPos, pitOut.localPos, -mainIn.localDir.ToVector2(), pitOut.localDir.ToVector2(), 14);

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
            var pitPath = BezierByHandleRule(pitIn.localPos, mainOut.localPos, -pitIn.localDir.ToVector2(), mainOut.localDir.ToVector2(), 14);

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
            var pitPath = BezierByHandleRule(mainIn.localPos, pitOut.localPos, -mainIn.localDir.ToVector2(), pitOut.localDir.ToVector2(), 14);

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
            var pitPath = BezierByHandleRule(pitIn.localPos, mainOut.localPos, -pitIn.localDir.ToVector2(), mainOut.localDir.ToVector2(), 14);

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

        private static Vector2[] BezierFromConnectors(TrackConnector from, TrackConnector to, int steps)
        {
            return BezierByHandleRule(from.localPos, to.localPos, -from.localDir.ToVector2(), to.localDir.ToVector2(), steps);
        }

        private static Vector2[] BezierByHandleRule(Vector2 p0, Vector2 p1, Vector2 startDir, Vector2 endDir, int steps)
        {
            var chord = (p1 - p0).magnitude;
            var handleLength = Mathf.Clamp(chord * 0.55f, 4f, 14f);
            var c0 = p0 + startDir.normalized * handleLength;
            var c1 = p1 - endDir.normalized * handleLength;
            return Bezier(p0, c0, c1, p1, steps);
        }

        private static Vector2[] Bezier(Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, int steps)
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

        private static Vector2[] CombineUnique(params Vector2[][] arrays)
        {
            var result = new List<Vector2>();
            foreach (var array in arrays)
            {
                if (array == null)
                {
                    continue;
                }

                foreach (var point in array)
                {
                    if (result.Count == 0 || !Approximately(result[result.Count - 1], point))
                    {
                        result.Add(point);
                    }
                }
            }

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

            // Defensive endcap correction: force boundary points to match exact endpoint tangent planes.
            var startCapNormal = new Vector2(-startDir.y, startDir.x);
            left[0] = center[0] + startCapNormal * HW;
            right[0] = center[0] - startCapNormal * HW;

            var endCapNormal = new Vector2(-endDir.y, endDir.x);
            left[center.Length - 1] = center[center.Length - 1] + endCapNormal * HW;
            right[center.Length - 1] = center[center.Length - 1] - endCapNormal * HW;

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
            ValidatePieceGeometryContract(piece);
            return piece;
        }

        private static void ValidatePieceGeometryContract(TrackPieceDef piece)
        {
            if (piece.category != "Main" && piece.category != "PitLane")
            {
                return;
            }

            const float epsilon = 0.01f;
            for (var i = 0; i < piece.connectors.Length; i++)
            {
                var c = piece.connectors[i];
                if (!IsOnContractLattice(c.localPos, epsilon))
                {
                    Debug.LogError($"[{piece.pieceId}] Connector '{c.id}' is off lattice at {c.localPos}.");
                }
            }

            for (var i = 0; i < piece.segments.Length; i++)
            {
                var segment = piece.segments[i];
                var from = piece.connectors[segment.fromConnectorIndex];
                var to = piece.connectors[segment.toConnectorIndex];
                var center = segment.localCenterline;
                if (center == null || center.Length < 2)
                {
                    Debug.LogError($"[{piece.pieceId}] Segment {i} has invalid centerline.");
                    continue;
                }

                if (!Approximately(center[0], from.localPos, epsilon) || !Approximately(center[center.Length - 1], to.localPos, epsilon))
                {
                    Debug.LogError($"[{piece.pieceId}] Segment {i} centerline endpoints do not match connectors exactly.");
                }

                var startDir = (-from.localDir.ToVector2()).normalized;
                var endDir = to.localDir.ToVector2().normalized;
                var startTangent = (center[1] - center[0]).normalized;
                var endTangent = (center[center.Length - 1] - center[center.Length - 2]).normalized;

                if (Vector2.Dot(startTangent, startDir) <= 0.95f)
                {
                    Debug.LogError($"[{piece.pieceId}] Segment {i} start tangent misaligned. dot={Vector2.Dot(startTangent, startDir):0.###}");
                }

                if (Vector2.Dot(endTangent, endDir) <= 0.95f)
                {
                    Debug.LogError($"[{piece.pieceId}] Segment {i} end tangent misaligned. dot={Vector2.Dot(endTangent, endDir):0.###}");
                }

                Debug.Assert(Vector2.Dot(startTangent, startDir) > 0.95f, $"[{piece.pieceId}] Segment {i} start tangent contract failed.");
                Debug.Assert(Vector2.Dot(endTangent, endDir) > 0.95f, $"[{piece.pieceId}] Segment {i} end tangent contract failed.");
            }
        }

        private static bool IsOnContractLattice(Vector2 p, float epsilon)
        {
            return IsNear(p.x, -L, epsilon) && IsNear(p.y, 0f, epsilon)
                   || IsNear(p.x, L, epsilon) && IsNear(p.y, 0f, epsilon)
                   || IsNear(p.x, 0f, epsilon) && IsNear(p.y, L, epsilon)
                   || IsNear(p.x, 0f, epsilon) && IsNear(p.y, -L, epsilon)
                   || IsNear(p.x, L, epsilon) && IsNear(p.y, L, epsilon)
                   || IsNear(p.x, -L, epsilon) && IsNear(p.y, L, epsilon)
                   || IsNear(p.x, L, epsilon) && IsNear(p.y, -L, epsilon)
                   || IsNear(p.x, -L, epsilon) && IsNear(p.y, -L, epsilon);
        }

        private static bool IsNear(float value, float target, float epsilon)
        {
            return Mathf.Abs(value - target) <= epsilon;
        }

        private static bool Approximately(Vector2 a, Vector2 b, float epsilon = 0.0001f)
        {
            return (a - b).sqrMagnitude <= epsilon * epsilon;
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
