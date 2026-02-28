using System.Collections.Generic;
using UnityEngine;

namespace GSP.TrackEditor
{
    [CreateAssetMenu(menuName = "GSP/Track Editor/Track Piece Library", fileName = "TrackPieceLibrary")]
    public class TrackPieceLibrary : ScriptableObject
    {
        public List<TrackPieceDef> pieces = new();
    }
}
