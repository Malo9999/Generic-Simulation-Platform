using System.Collections.Generic;
using UnityEngine;

namespace GSP.TrackEditor
{
    [CreateAssetMenu(menuName = "GSP/Track Editor/Track Layout", fileName = "TrackLayout")]
    public class TrackLayout : ScriptableObject
    {
        public List<PlacedPiece> pieces = new();
        public List<ConnectorLink> links = new();
        public StartFinishMarker startFinish;
        public List<TrackSlot> startGridSlots = new();
        public string pitEntryGuid;
        public string pitExitGuid;
        public Vector2 pan;
        public float zoom = 1f;
    }
}
