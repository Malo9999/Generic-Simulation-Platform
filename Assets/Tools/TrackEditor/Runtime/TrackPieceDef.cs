using UnityEngine;

namespace GSP.TrackEditor
{
    [CreateAssetMenu(menuName = "GSP/Track Editor/Track Piece Definition", fileName = "TrackPieceDef")]
    public class TrackPieceDef : ScriptableObject
    {
        public string pieceId;
        public string displayName;
        public string category;
        public float trackWidth = 8f;
        public TrackConnector[] connectors;
        public TrackSegment[] segments;
        public Rect localBounds = new Rect(-10f, -10f, 20f, 20f);
    }
}
