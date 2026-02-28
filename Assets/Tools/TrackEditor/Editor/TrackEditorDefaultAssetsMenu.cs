using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    // HOW TO TEST
    // 1) Unity: GSP -> TrackEditor -> Create Default Track Pieces
    // 2) Open: GSP -> TrackEditor -> TrackEditor
    // 3) New Layout
    // 4) Drag Straight, then Corner90, Corner45, Corner180: corners are rounded and snap cleanly.
    // 5) Place PitEntry/PitExit: pit branch + merge are smooth; pit snaps only to pit.
    // 6) Click Generate Start Grid (10): visible yellow slots appear and follow track dragging.
    // 7) LMB drag moves connected group; Shift+drag moves selected only and prunes broken links.
    // 8) Delete selected via Delete/Backspace or Delete Selected button; links are removed.
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

            var pieces = new List<TrackPieceDef>
            {
                CreateStraight("StraightMain", "Straight (Main)", "Straight (Main)", TrackConnectorRole.Main),
                CreateStraight45("Straight45Main", "Straight 45 (Main)", "Straight 45 (Main)", TrackConnectorRole.Main),
                CreateCorner45("Corner45Main", "Corner 45 (Main)", "Corner 45 (Main)", TrackConnectorRole.Main),
                CreateCorner90("Corner90Main", "Corner 90 (Main)", "Corner 90 (Main)", TrackConnectorRole.Main),
                CreateCorner180(),
                CreateStraight("PitStraight", "Pit Straight (Pit)", "Pit Straight (Pit)", TrackConnectorRole.Pit),
                CreateStraight45("PitStraight45", "Pit Straight 45 (Pit)", "Pit Straight 45 (Pit)", TrackConnectorRole.Pit),
                CreateCorner45("PitCorner45", "Pit Corner 45 (Pit)", "Pit Corner 45 (Pit)", TrackConnectorRole.Pit),
                CreateCorner90("PitCorner90", "Pit Corner 90 (Pit)", "Pit Corner 90 (Pit)", TrackConnectorRole.Pit),
                CreatePitEntry(),
                CreatePitExit()
            };

            var library = ScriptableObject.CreateInstance<TrackPieceLibrary>();
            library.pieces = pieces;
            AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TrackEditor defaults recreated at '{BaseFolder}' with library '{LibraryPath}'.");
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

        private static TrackPieceDef CreateStraight(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var segments = new[]
            {
                Segment(0, 1, role, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, role, new[] { c1.localPos, c0.localPos })
            };
            return CreatePiece(pieceId, displayName, category, new[] { c0, c1 }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateStraight45(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("SW", new Vector2(-D, -D), Dir8.SW, role);
            var c1 = Connector("NE", new Vector2(D, D), Dir8.NE, role);
            var segments = new[]
            {
                Segment(0, 1, role, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, role, new[] { c1.localPos, c0.localPos })
            };
            return CreatePiece(pieceId, displayName, category, new[] { c0, c1 }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateCorner45(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("NW", new Vector2(-D, D), Dir8.NW, role);
            var path = MakeArc(c0.localPos, c1.localPos, L, true, 6);
            var segments = new[]
            {
                Segment(0, 1, role, path),
                Segment(1, 0, role, Reverse(path))
            };
            return CreatePiece(pieceId, displayName, category, new[] { c0, c1 }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateCorner90(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("N", new Vector2(0f, L), Dir8.N, role);
            var path = MakeArc(c0.localPos, c1.localPos, L, true, 10);
            var segments = new[]
            {
                Segment(0, 1, role, path),
                Segment(1, 0, role, Reverse(path))
            };
            return CreatePiece(pieceId, displayName, category, new[] { c0, c1 }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateCorner180()
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var p0 = new Vector2(-L, 0f);
            var p1 = new Vector2(L, 0f);
            var cp0 = p0 + Vector2.right * (L * 0.8f) + Vector2.up * (L * 1.2f);
            var cp1 = p1 + Vector2.right * (L * 0.8f) + Vector2.up * (L * 1.2f);
            var path = MakeCubicBezier(p0, cp0, cp1, p1, 18);
            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path),
                Segment(1, 0, TrackConnectorRole.Main, Reverse(path))
            };
            return CreatePiece("Corner180", "Corner 180", "Corner", new[] { c0, c1 }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreatePitEntry()
        {
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var pitPath = MakeArc(mainIn.localPos, pitOut.localPos, L, false, 10);
            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath)
            };
            return CreatePiece("PitEntry", "Pit Entry", "Pit", new[] { mainIn, mainOut, pitOut }, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreatePitExit()
        {
            var pitIn = Connector("PitIn", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitPath = MakeArc(pitIn.localPos, mainOut.localPos, L, false, 10);
            var segments = new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath)
            };
            return CreatePiece("PitExit", "Pit Exit", "Pit", new[] { pitIn, mainIn, mainOut }, segments, BuildBounds(segments));
        }

        private static Vector2[] MakeArc(Vector2 p0, Vector2 p1, float radius, bool useUpperSide, int steps)
        {
            var chord = p1 - p0;
            var c = chord.magnitude;
            if (c <= 1e-5f)
            {
                return new[] { p0, p1 };
            }

            radius = Mathf.Max(radius, c * 0.5f);
            var m = (p0 + p1) * 0.5f;
            var perp = new Vector2(-chord.y, chord.x).normalized;
            var h = Mathf.Sqrt(Mathf.Max(0f, radius * radius - (c * 0.5f) * (c * 0.5f)));
            var center = m + perp * (useUpperSide ? h : -h);

            var a0 = Mathf.Atan2(p0.y - center.y, p0.x - center.x);
            var a1 = Mathf.Atan2(p1.y - center.y, p1.x - center.x);

            var ccw = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
            var cw = ccw - Mathf.PI * 2f;
            var delta = Mathf.Abs(ccw) <= Mathf.Abs(cw) ? ccw : cw;

            var count = Mathf.Max(1, steps);
            var result = new Vector2[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var angle = a0 + delta * t;
                result[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            result[0] = p0;
            result[count] = p1;
            return result;
        }

        private static Vector2[] MakeCubicBezier(Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, int steps)
        {
            var count = Mathf.Max(2, steps);
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

        private static Vector2[] Reverse(Vector2[] points)
        {
            var output = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                output[i] = points[points.Length - 1 - i];
            }

            return output;
        }

        private static TrackPieceDef CreatePiece(string pieceId, string displayName, string category, TrackConnector[] connectors, TrackSegment[] segments, Rect bounds)
        {
            var piece = ScriptableObject.CreateInstance<TrackPieceDef>();
            piece.pieceId = pieceId;
            piece.displayName = displayName;
            piece.category = category;
            piece.trackWidth = TrackWidth;
            piece.connectors = connectors;
            piece.segments = segments;
            piece.localBounds = bounds;

            var path = $"{PieceFolder}/{pieceId}.asset";
            AssetDatabase.CreateAsset(piece, path);
            EditorUtility.SetDirty(piece);
            return piece;
        }

        private static TrackConnector Connector(string id, Vector2 pos, Dir8 dir, TrackConnectorRole role)
        {
            return new TrackConnector { id = id, localPos = pos, localDir = dir, role = role, trackWidth = TrackWidth };
        }

        private static TrackSegment Segment(int from, int to, TrackConnectorRole role, Vector2[] center)
        {
            var left = new Vector2[center.Length];
            var right = new Vector2[center.Length];

            for (var i = 0; i < center.Length; i++)
            {
                var tangent = i == 0
                    ? center[1] - center[0]
                    : i == center.Length - 1
                        ? center[center.Length - 1] - center[center.Length - 2]
                        : center[i + 1] - center[i - 1];

                var normal = tangent.sqrMagnitude > 0.0001f
                    ? Vector2.Perpendicular(tangent.normalized)
                    : Vector2.up;

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

        private static Rect BuildBounds(IEnumerable<TrackSegment> segments)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

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
            foreach (var point in points)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
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
