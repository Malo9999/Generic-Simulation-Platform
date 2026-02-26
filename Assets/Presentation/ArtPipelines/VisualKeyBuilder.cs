public static class VisualKeyBuilder
{
    public static VisualKey Create(
        string simulationId,
        string entityType,
        int instanceId,
        string kind,
        string state,
        FacingMode facingMode = FacingMode.Auto,
        int? variantSeed = null,
        int groupId = -1)
    {
        return new VisualKey
        {
            simulationId = simulationId,
            entityId = entityType,
            instanceId = instanceId,
            kind = string.IsNullOrWhiteSpace(kind) ? string.Empty : kind,
            state = string.IsNullOrWhiteSpace(state) ? "idle" : state,
            variantSeed = variantSeed ?? instanceId,
            groupId = groupId,
            facingMode = facingMode
        };
    }
}
