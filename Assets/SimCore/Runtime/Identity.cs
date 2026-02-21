using System;

public enum EntityStatus
{
    Active = 0,
    Inactive = 1,
    Eliminated = 2
}

[Serializable]
public struct EntityIdentity
{
    public int entityId;
    public int teamId;
    public string role;
    public int variant;
    public int appearanceSeed;
    public EntityStatus status;

    public EntityIdentity(int entityId, int teamId, string role, int variant, int appearanceSeed, EntityStatus status = EntityStatus.Active)
    {
        this.entityId = entityId;
        this.teamId = teamId;
        this.role = role ?? string.Empty;
        this.variant = variant;
        this.appearanceSeed = appearanceSeed;
        this.status = status;
    }

    public override string ToString()
    {
        return $"entityId={entityId}, teamId={teamId}, role={role}, variant={variant}, appearanceSeed={appearanceSeed}, status={status}";
    }
}
