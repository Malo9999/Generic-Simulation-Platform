using System.Collections.Generic;
using UnityEngine;

namespace GSP.TrackEditor
{
    [CreateAssetMenu(menuName = "GSP/Track Editor/Track Baked Data", fileName = "TrackBakedData")]
    public class TrackBakedData : ScriptableObject
    {
        public float trackWidth = 8f;
        public Vector2[] mainCenterline;
        public Vector2[] mainLeftBoundary;
        public Vector2[] mainRightBoundary;
        public float lapLength;
        public List<TrackSlot> startGridSlots = new();
        public Vector2[] pitCenterline;
        public Vector2[] pitLeftBoundary;
        public Vector2[] pitRightBoundary;
        public float[] cumulativeMainLength;
        public Vector2 startFinishPos;
        public Vector2 startFinishDir;
        public float startFinishDistance;
    }
}
