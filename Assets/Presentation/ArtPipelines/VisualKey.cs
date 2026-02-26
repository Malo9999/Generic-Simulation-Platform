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
    // Team/group id used for placeholder palette mapping; -1 when unknown.
    public int groupId;
    public FacingMode facingMode;
}
