using System;
using System.Collections.Generic;
using UnityEngine;

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

[CreateAssetMenu(menuName = "GSP/Track Editor/Track Piece Library", fileName = "TrackPieceLibrary")]
public class TrackPieceLibrary : ScriptableObject
{
    public List<TrackPieceDef> pieces = new();

#if UNITY_EDITOR
    public void RefreshFromAssets()
    {
        pieces.Clear();
        var guids = UnityEditor.AssetDatabase.FindAssets("t:TrackPieceDef");
        foreach (var guid in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var piece = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackPieceDef>(path);
            if (piece != null)
            {
                pieces.Add(piece);
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
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

[CreateAssetMenu(menuName = "GSP/Track Editor/Track Layout", fileName = "TrackLayout")]
public class TrackLayout : ScriptableObject
{
    public List<PlacedPiece> pieces = new();
    public List<ConnectorLink> links = new();
    public StartFinishMarker startFinish = new();
    public List<TrackSlot> startGridSlots = new();
    public string pitEntryGuid;
    public string pitExitGuid;
    public Vector2 pan;
    public float zoom = 1f;
}

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
}
