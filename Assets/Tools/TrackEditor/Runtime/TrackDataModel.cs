using System;
using System.Collections.Generic;
using UnityEngine;

namespace GSP.TrackEditor
{
    public enum TrackConnectorRole
    {
        Main,
        Pit,
        Any
    }

    [Serializable]
    public class TrackConnector
    {
        public string id;
        public Vector2 localPos;
        public Dir8 localDir;
        public TrackConnectorRole role = TrackConnectorRole.Any;
        public float trackWidth = 8f;
    }

    [Serializable]
    public class TrackSegment
    {
        public int fromConnectorIndex;
        public int toConnectorIndex;
        public TrackConnectorRole pathRole = TrackConnectorRole.Main;
        public Vector2[] localCenterline;
        public Vector2[] localLeftBoundary;
        public Vector2[] localRightBoundary;
    }

    [Serializable]
    public class PlacedPiece
    {
        public string guid;
        public TrackPieceDef piece;
        public Vector2 position;
        public int rotationSteps45;
        public bool mirrored;
    }

    [Serializable]
    public class ConnectorLink
    {
        public string pieceGuidA;
        public int connectorIndexA;
        public string pieceGuidB;
        public int connectorIndexB;
    }

    [Serializable]
    public class StartFinishMarker
    {
        public string pieceGuid;
        public int segmentIndex;
        public int connectorIndex;
        public Vector2 worldPos;
        public Vector2 worldDir = Vector2.right;
    }

    [Serializable]
    public class TrackSlot
    {
        public Vector2 pos;
        public Vector2 dir;
    }

}
