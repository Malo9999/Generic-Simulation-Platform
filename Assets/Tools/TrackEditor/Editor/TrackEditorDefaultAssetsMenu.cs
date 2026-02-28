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

        // HOW TO TEST
        // 1) In Unity, wait for compilation.
        // 2) Menu bar: GSP -> TrackEditor -> Create Default Track Pieces
        //    - Confirm NO "No script asset..." errors.
        //    - Confirm assets appear under Assets/Tools/TrackEditor/Defaults/
        //    - Click a TrackPieceDef asset -> Inspector shows script TrackPieceDef (not Missing Script).
        // 3) Menu bar: GSP -> TrackEditor -> TrackEditor (opens window).
        // 4) Menu bar: GSP -> Art -> Create Default Species Blueprints (exists).
        //    - Confirm the old menu location is gone.
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
                CreateCorner(),
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
            var c0 = Connector("West", new Vector2(-10f, 0f), Dir8.W);
            var c1 = Connector("East", new Vector2(10f, 0f), Dir8.E);
            return CreatePiece("Straight", "Straight", "Straight", new[] { c0, c1 }, new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { c0.localPos, c1.localPos }),
                Segment(1, 0, TrackConnectorRole.Main, new[] { c1.localPos, c0.localPos })
            }, new Rect(-10f, -4f, 20f, 8f));
        }

        private static TrackPieceDef CreateCorner()
        {
            var c0 = Connector("E", new Vector2(8f, 0f), Dir8.E);
            var c1 = Connector("N", new Vector2(0f, 8f), Dir8.N);
            return CreatePiece("Corner", "Corner", "Corner", new[] { c0, c1 }, new[]
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

            return CreatePiece("PitEntry", "Pit Entry", "Pit", new[] { mainIn, mainOut, pitOut }, new[]
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

            return CreatePiece("PitExit", "Pit Exit", "Pit", new[] { pitIn, mainIn, mainOut }, new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }),
                Segment(0, 2, TrackConnectorRole.Pit, new[] { pitIn.localPos, new Vector2(4f, -5f), mainOut.localPos })
            }, new Rect(-10f, -9f, 20f, 13f));
        }

        private static TrackPieceDef CreatePiece(string pieceId, string displayName, string category, TrackConnector[] connectors, TrackSegment[] segments, Rect bounds)
        {
            var piece = ScriptableObject.CreateInstance<TrackPieceDef>();
            piece.pieceId = pieceId;
            piece.displayName = displayName;
            piece.category = category;
            piece.trackWidth = 8f;
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
