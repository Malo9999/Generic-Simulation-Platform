using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    public static class TrackPieceLibraryEditorUtility
    {
        public static void RefreshFromAssets(TrackPieceLibrary library)
        {
            if (library == null)
            {
                return;
            }

            var refreshedPieces = new List<TrackPieceDef>();
            var guids = AssetDatabase.FindAssets("t:TrackPieceDef");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var piece = AssetDatabase.LoadAssetAtPath<TrackPieceDef>(path);
                if (piece != null)
                {
                    refreshedPieces.Add(piece);
                }
            }

            library.pieces = refreshedPieces;
            EditorUtility.SetDirty(library);
        }
    }
}
