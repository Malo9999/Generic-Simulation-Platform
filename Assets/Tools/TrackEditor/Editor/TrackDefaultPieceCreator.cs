using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    public static class TrackDefaultPieceCreator
    {
        private const string BaseFolder = "Assets/Tools/TrackEditor/Defaults";
        private const string PieceFolder = BaseFolder + "/Pieces";

        [MenuItem("Tools/GSP/Track Editor/Create Default Track Pieces")]
        public static void CreateDefaults()
        {
            EnsureFolder("Assets/Tools");
            EnsureFolder("Assets/Tools/TrackEditor");
            EnsureFolder(BaseFolder);
            EnsureFolder(PieceFolder);

            var pieces = new List<TrackPieceDef>
            {
                CreateStraightEW(),
                CreateStraightDiagonal(),
                CreateCorner45(),
                CreateCorner90(),
                CreatePitEntry(),
                CreatePitExit()
            };

            var libraryPath = BaseFolder + "/TrackPieceLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<TrackPieceLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<TrackPieceLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }

            library.pieces = pieces;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static TrackPieceDef CreateStraightEW()
        {
            var c0 = Connector("West", new Vector2(-10f, 0f), Dir8.W);
            var c1 = Connector("East", new Vector2(10f, 0f), Dir8.E);
            return CreatePiece("Straight_EW", "Straight EW", "Straight", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, c0.localPos })
            }, new Rect(-10f, -4f, 20f, 8f));
        }

        private static TrackPieceDef CreateStraightDiagonal()
        {
            var c0 = Connector("SW", new Vector2(-7f, -7f), Dir8.SW);
            var c1 = Connector("NE", new Vector2(7f, 7f), Dir8.NE);
            return CreatePiece("Straight_NE_SW", "Straight Diagonal", "Straight", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, c0.localPos })
            }, new Rect(-8f, -8f, 16f, 16f));
        }

        private static TrackPieceDef CreateCorner45()
        {
            var c0 = Connector("E", new Vector2(8f, 0f), Dir8.E);
            var c1 = Connector("NE", new Vector2(6f, 6f), Dir8.NE);
            return CreatePiece("Corner_E_to_NE", "Corner 45", "Corner", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, new Vector2(7f, 3f), c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, new Vector2(7f, 3f), c0.localPos })
            }, new Rect(0f, -4f, 10f, 12f));
        }

        private static TrackPieceDef CreateCorner90()
        {
            var c0 = Connector("E", new Vector2(8f, 0f), Dir8.E);
            var c1 = Connector("N", new Vector2(0f, 8f), Dir8.N);
            return CreatePiece("Corner_E_to_N", "Corner 90", "Corner", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, new Vector2(6f, 6f), c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, new Vector2(6f, 6f), c0.localPos })
            }, new Rect(-1f, -1f, 10f, 10f));
        }

        private static TrackPieceDef CreatePitEntry()
        {
            var mainIn = Connector("MainIn", new Vector2(-10f, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(10f, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(0f, -8f), Dir8.S, TrackConnectorRole.Pit);

            return CreatePiece("PitEntry_3Conn", "Pit Entry", "Pit", new[] { mainIn, mainOut, pitOut }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, new[] { mainIn.localPos, new Vector2(-4f, -5f), pitOut.localPos })
            }, new Rect(-10f, -9f, 20f, 13f));
        }

        private static TrackPieceDef CreatePitExit()
        {
            var pitIn = Connector("PitIn", new Vector2(0f, -8f), Dir8.S, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-10f, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(10f, 0f), Dir8.E, TrackConnectorRole.Main);

            return CreatePiece("PitExit_3Conn", "Pit Exit", "Pit", new[] { pitIn, mainIn, mainOut }, new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, new[] { pitIn.localPos, new Vector2(4f, -5f), mainOut.localPos })
            }, new Rect(-10f, -9f, 20f, 13f));
        }

        private static TrackPieceDef CreatePiece(string pieceId, string displayName, string category, TrackConnector[] connectors, TrackSegment[] segments, Rect bounds)
        {
            var path = $"{PieceFolder}/{pieceId}.asset";
            var piece = AssetDatabase.LoadAssetAtPath<TrackPieceDef>(path);
            if (piece == null)
            {
                piece = ScriptableObject.CreateInstance<TrackPieceDef>();
                AssetDatabase.CreateAsset(piece, path);
            }

            piece.pieceId = pieceId;
            piece.displayName = displayName;
            piece.category = category;
            piece.trackWidth = 8f;
            piece.connectors = connectors;
            piece.segments = segments;
            piece.localBounds = bounds;
            EditorUtility.SetDirty(piece);
            return piece;
        }

        private static TrackConnector Connector(string id, Vector2 pos, Dir8 dir, TrackConnectorRole role = TrackConnectorRole.Any)
        {
            return new TrackConnector { id = id, localPos = pos, localDir = dir, role = role, trackWidth = 8f };
        }

        private static TrackSegment Segment(int from, int to, TrackConnectorRole role, Vector2[] center)
        {
            var offset = Vector2.Perpendicular((center[^1] - center[0]).normalized) * 4f;
            var left = new Vector2[center.Length];
            var right = new Vector2[center.Length];
            for (var i = 0; i < center.Length; i++)
            {
                left[i] = center[i] + offset;
                right[i] = center[i] - offset;
            }

            left[0] = center[0] + offset;
            left[^1] = center[^1] + offset;
            right[0] = center[0] - offset;
            right[^1] = center[^1] - offset;

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
