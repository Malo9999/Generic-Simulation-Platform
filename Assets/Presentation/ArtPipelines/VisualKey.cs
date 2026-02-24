using System;

[Serializable]
public struct VisualKey
{
    public string simulationId;
    public string entityId;
    public string kind;
    public string state;
    public int variantSeed;
    public FacingMode facingMode;
}
