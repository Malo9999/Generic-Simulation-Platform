using System;

[Serializable]
public struct VisualKey
{
    public string simulationId;
    // Stable entity type (for content lookup), e.g. "ant", "marble", "car", "athlete".
    public string entityId;
    // Unique per-entity instance id within a simulation run.
    public int instanceId;
    public string kind;
    public string state;
    // Visual variation seed (species/skin choice). Defaults to instanceId when omitted by callers.
    public int variantSeed;
    public FacingMode facingMode;
}
