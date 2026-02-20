using System;

[Flags]
public enum EntityStatusFlags
{
    None = 0,
    Carrying = 1 << 0,
    Stunned = 1 << 1,
    Injured = 1 << 2
}

[Serializable]
public struct EntityIdentity
{
    public int entityId;
    public int teamId;
    public string role;
    public int variant;
    public int variantSeed;
    public EntityStatusFlags statusFlags;

    public EntityIdentity(int entityId, int teamId, string role, int variant, int variantSeed, EntityStatusFlags statusFlags = EntityStatusFlags.None)
    {
        this.entityId = entityId;
        this.teamId = teamId;
        this.role = role ?? string.Empty;
        this.variant = variant;
        this.variantSeed = variantSeed;
        this.statusFlags = statusFlags;
    }

    public override string ToString()
    {
        return $"entityId={entityId}, teamId={teamId}, role={role}, variant={variant}, variantSeed={variantSeed}, statusFlags={statusFlags}";
    }
}
