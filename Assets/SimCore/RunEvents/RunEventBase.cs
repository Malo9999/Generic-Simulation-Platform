using System;
using System.Collections.Generic;

[Serializable]
public struct RunVec2
{
    public float x;
    public float y;

    public RunVec2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public static class RunEventTypes
{
    public const string Spawn = "Spawn";
    public const string Despawn = "Despawn";
    public const string Score = "Score";
    public const string Hit = "Hit";
    public const string Pickup = "Pickup";
    public const string Dropoff = "Dropoff";
    public const string Lap = "Lap";
    public const string Overtake = "Overtake";
    public const string Goal = "Goal";
    public const string HighlightTag = "HighlightTag";
}

[Serializable]
public class RunEventBase
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public int tick;
    public string eventType;
    public string entityId;
    public string teamId;
    public RunVec2? position;
    public Dictionary<string, object> payload;

    protected RunEventBase(string eventType)
    {
        this.eventType = eventType;
        payload = new Dictionary<string, object>();
    }

    public RunEventBase SetEntity(string value)
    {
        entityId = value;
        return this;
    }

    public RunEventBase SetTeam(string value)
    {
        teamId = value;
        return this;
    }

    public RunEventBase SetPosition(float x, float y)
    {
        position = new RunVec2(x, y);
        return this;
    }

    public RunEventBase ClearPosition()
    {
        position = null;
        return this;
    }

    public T GetPayloadValue<T>(string key, T fallback = default)
    {
        if (payload != null && payload.TryGetValue(key, out var value) && value is T cast)
        {
            return cast;
        }

        return fallback;
    }

    protected void SetPayload(string key, object value)
    {
        if (payload == null)
        {
            payload = new Dictionary<string, object>();
        }

        payload[key] = value;
    }
}

public sealed class SpawnEvent : RunEventBase
{
    public SpawnEvent() : base(RunEventTypes.Spawn) { }

    public SpawnEvent SetPrefab(string prefab) { SetPayload("prefab", prefab); return this; }
    public SpawnEvent SetKind(string kind) { SetPayload("kind", kind); return this; }
    public SpawnEvent SetVelocity(float x, float y)
    {
        SetPayload("velocity", new Dictionary<string, object> { { "x", x }, { "y", y } });
        return this;
    }
}

public sealed class DespawnEvent : RunEventBase
{
    public DespawnEvent() : base(RunEventTypes.Despawn) { }

    public DespawnEvent SetReason(string reason) { SetPayload("reason", reason); return this; }
}

public sealed class ScoreEvent : RunEventBase
{
    public ScoreEvent() : base(RunEventTypes.Score) { }

    public ScoreEvent SetDelta(int delta) { SetPayload("delta", delta); return this; }
    public ScoreEvent SetTotal(int total) { SetPayload("total", total); return this; }
}

public sealed class HitEvent : RunEventBase
{
    public HitEvent() : base(RunEventTypes.Hit) { }

    public HitEvent SetTargetId(string targetEntityId) { SetPayload("targetEntityId", targetEntityId); return this; }
    public HitEvent SetDamage(float amount) { SetPayload("damage", amount); return this; }
    public HitEvent SetWeapon(string weapon) { SetPayload("weapon", weapon); return this; }
}

public sealed class PickupEvent : RunEventBase
{
    public PickupEvent() : base(RunEventTypes.Pickup) { }

    public PickupEvent SetItemId(string itemId) { SetPayload("itemId", itemId); return this; }
    public PickupEvent SetItemKind(string itemKind) { SetPayload("itemKind", itemKind); return this; }
}

public sealed class DropoffEvent : RunEventBase
{
    public DropoffEvent() : base(RunEventTypes.Dropoff) { }

    public DropoffEvent SetItemId(string itemId) { SetPayload("itemId", itemId); return this; }
    public DropoffEvent SetDestination(string destination) { SetPayload("destination", destination); return this; }
}

public sealed class LapEvent : RunEventBase
{
    public LapEvent() : base(RunEventTypes.Lap) { }

    public LapEvent SetLapNumber(int lapNumber) { SetPayload("lapNumber", lapNumber); return this; }
    public LapEvent SetLapTimeMs(int lapTimeMs) { SetPayload("lapTimeMs", lapTimeMs); return this; }
}

public sealed class OvertakeEvent : RunEventBase
{
    public OvertakeEvent() : base(RunEventTypes.Overtake) { }

    public OvertakeEvent SetPassedEntityId(string passedEntityId) { SetPayload("passedEntityId", passedEntityId); return this; }
    public OvertakeEvent SetNewPosition(int newPosition) { SetPayload("newPosition", newPosition); return this; }
}

public sealed class GoalEvent : RunEventBase
{
    public GoalEvent() : base(RunEventTypes.Goal) { }

    public GoalEvent SetGoalType(string goalType) { SetPayload("goalType", goalType); return this; }
    public GoalEvent SetValue(float value) { SetPayload("value", value); return this; }
}

public sealed class HighlightTagEvent : RunEventBase
{
    public HighlightTagEvent() : base(RunEventTypes.HighlightTag) { }

    public HighlightTagEvent SetTag(string tag) { SetPayload("tag", tag); return this; }
    public HighlightTagEvent SetSeverity(string severity) { SetPayload("severity", severity); return this; }
}
