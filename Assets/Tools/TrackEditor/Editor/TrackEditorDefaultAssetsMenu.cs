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
        private const float HalfLen = 10f;
        private const float DiagHalf = HalfLen / 1.41421356f;
        private const float TrackWidth = 8f;
        private const float HalfWidth = TrackWidth * 0.5f;

        // HOW TO TEST
        // 1) Unity: GSP -> TrackEditor -> Create Default Track Pieces
        // 2) Open: GSP -> TrackEditor -> TrackEditor
        // 3) New Layout
        // 4) Drag Straight, then drag Corner90 near its open connector:
        //    - It should snap cleanly (endpoints line up) with no skew.
        // 5) Try Corner45 and Straight45 to build diagonals.
        // 6) Drag with LMB on a piece:
        //    - By default, the whole connected chunk moves.
        //    - Hold SHIFT while dragging to move only the selected piece.
        // 7) Confirm Pit connectors only snap to Pit connectors (no accidental Main<->Pit snaps).
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
                CreateStraight(),
                CreateStraight45(),
                CreateCorner45(),
                CreateCorner90(),
                CreateCorner135(),
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
                if (!path.EndsWith(".asset"))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void DeleteDefaultLibraryAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TrackPieceLibrary>(LibraryPath) != null || AssetDatabase.AssetPathExists(LibraryPath))
            {
                AssetDatabase.DeleteAsset(LibraryPath);
            }
        }

        private static TrackPieceDef CreateStraight()
        {
            var c0 = Connector("W", new Vector2(-HalfLen, 0f), Dir8.W);
            var c1 = Connector("E", new Vector2(HalfLen, 0f), Dir8.E);
            return CreatePiece("Straight", "Straight", "Straight", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, c0.localPos })
            }, new Rect(-HalfLen, -HalfWidth, HalfLen * 2f, TrackWidth));
        }

        private static TrackPieceDef CreateStraight45()
        {
            var c0 = Connector("SW", new Vector2(-DiagHalf, -DiagHalf), Dir8.SW);
            var c1 = Connector("NE", new Vector2(DiagHalf, DiagHalf), Dir8.NE);
            return CreatePiece("Straight45", "Straight 45", "Straight", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, c0.localPos })
            }, BuildBounds(new[] { c0.localPos, c1.localPos }));
        }

        private static TrackPieceDef CreateCorner45()
        {
            var c0 = Connector("W", new Vector2(-HalfLen, 0f), Dir8.W);
            var c1 = Connector("NW", new Vector2(-DiagHalf, DiagHalf), Dir8.NW);
            var path = new[] { c0.localPos, new Vector2(-9f, 3f), c1.localPos };

            return CreatePiece("Corner45", "Corner 45", "Corner", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, new Vector2(-9f, 3f), c0.localPos })
            }, BuildBounds(c0.localPos, new Vector2(-9f, 3f), c1.localPos));
        }

        private static TrackPieceDef CreateCorner90()
        {
            var c0 = Connector("W", new Vector2(-HalfLen, 0f), Dir8.W);
            var c1 = Connector("N", new Vector2(0f, HalfLen), Dir8.N);
            var path = new[] { c0.localPos, new Vector2(-6f, 6f), c1.localPos };

            return CreatePiece("Corner90", "Corner 90", "Corner", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, new Vector2(-6f, 6f), c0.localPos })
            }, BuildBounds(c0.localPos, new Vector2(-6f, 6f), c1.localPos));
        }

        private static TrackPieceDef CreateCorner135()
        {
            var c0 = Connector("W", new Vector2(-HalfLen, 0f), Dir8.W);
            var c1 = Connector("NE", new Vector2(DiagHalf, DiagHalf), Dir8.NE);
            var path = new[] { c0.localPos, new Vector2(-8f, 8f), new Vector2(0f, 12f), c1.localPos };

            return CreatePiece("Corner135", "Corner 135", "Corner", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, new Vector2(0f, 12f), new Vector2(-8f, 8f), c0.localPos })
            }, BuildBounds(c0.localPos, new Vector2(-8f, 8f), new Vector2(0f, 12f), c1.localPos));
        }

        private static TrackPieceDef CreatePitEntry()
        {
            var mainIn = Connector("MainIn", new Vector2(-HalfLen, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(HalfLen, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(0f, -HalfLen), Dir8.S, TrackConnectorRole.Pit);
            var pitCurve = new[] { mainIn.localPos, new Vector2(-7.5f, -4f), new Vector2(-3.5f, -8f), pitOut.localPos };

            return CreatePiece("PitEntry", "Pit Entry", "Pit", new[] { mainIn, mainOut, pitOut }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, pitCurve)
            }, BuildBounds(new[] { mainIn.localPos, mainOut.localPos, pitOut.localPos, new Vector2(-7.5f, -4f), new Vector2(-3.5f, -8f) }));
        }

        private static TrackPieceDef CreatePitExit()
        {
            var pitIn = Connector("PitIn", new Vector2(0f, -HalfLen), Dir8.S, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-HalfLen, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(HalfLen, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitCurve = new[] { pitIn.localPos, new Vector2(3.5f, -8f), new Vector2(7.5f, -4f), mainOut.localPos };

            return CreatePiece("PitExit", "Pit Exit", "Pit", new[] { pitIn, mainIn, mainOut }, new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, pitCurve)
            }, BuildBounds(new[] { pitIn.localPos, mainIn.localPos, mainOut.localPos, new Vector2(3.5f, -8f), new Vector2(7.5f, -4f) }));
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

        private static TrackConnector Connector(string id, Vector2 pos, Dir8 dir, TrackConnectorRole role = TrackConnectorRole.Any)
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
                        ? center[^1] - center[^2]
                        : center[i + 1] - center[i - 1];
                var normal = tangent.sqrMagnitude > 0.0001f
                    ? Vector2.Perpendicular(tangent.normalized)
                    : Vector2.up;

                left[i] = center[i] + normal * HalfWidth;
                right[i] = center[i] - normal * HalfWidth;
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

        private static Rect BuildBounds(params Vector2[] points)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var point in points)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX - HalfWidth, minY - HalfWidth, maxX + HalfWidth, maxY + HalfWidth);
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
