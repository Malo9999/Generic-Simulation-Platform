using UnityEngine;

public enum BasicShapeKind
{
    Circle,
    Capsule,
    RoundedRect
}

[CreateAssetMenu(fileName = "SimVisualSettings", menuName = "GSP/Bootstrap/Sim Visual Settings")]
public class SimVisualSettings : ScriptableObject
{
    public string simulationId = "MarbleRace";
    public bool usePrimitiveBaseline = true;
    public BasicShapeKind agentShape = BasicShapeKind.Circle;
    public bool agentOutline = true;
    public int agentSizePx = 64;
    public ContentPack preferredAgentPack;
    public DebugPlaceholderMode defaultDebugMode = DebugPlaceholderMode.Overlay;
}
