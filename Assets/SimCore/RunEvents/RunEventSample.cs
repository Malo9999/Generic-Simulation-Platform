using System.Collections.Generic;

public static class RunEventSample
{
    public static List<RunEventBase> GenerateSampleEvents()
    {
        return new List<RunEventBase>
        {
            new SpawnEvent
            {
                tick = 0
            }.SetEntity("runner-1").SetTeam("blue").SetPosition(1.5f, 2.0f).SetPrefab("RunnerBot").SetKind("racer").SetVelocity(0.5f, 0.0f),

            new SpawnEvent
            {
                tick = 0
            }.SetEntity("runner-2").SetTeam("red").SetPosition(2.5f, 2.0f).SetPrefab("RunnerBot").SetKind("racer").SetVelocity(0.4f, 0.1f),

            new PickupEvent
            {
                tick = 12
            }.SetEntity("runner-1").SetTeam("blue").SetPosition(3.0f, 2.4f).SetItemId("flag-a").SetItemKind("flag"),

            new HitEvent
            {
                tick = 20
            }.SetEntity("runner-2").SetTeam("red").SetPosition(3.8f, 2.4f).SetTargetId("runner-1").SetDamage(15f).SetWeapon("pulse"),

            new DropoffEvent
            {
                tick = 30
            }.SetEntity("runner-1").SetTeam("blue").SetPosition(5.5f, 2.5f).SetItemId("flag-a").SetDestination("base-blue"),

            new ScoreEvent
            {
                tick = 31
            }.SetEntity("runner-1").SetTeam("blue").SetDelta(1).SetTotal(1),

            new OvertakeEvent
            {
                tick = 44
            }.SetEntity("runner-2").SetTeam("red").SetPosition(6.1f, 2.6f).SetPassedEntityId("runner-3").SetNewPosition(2),

            new LapEvent
            {
                tick = 50
            }.SetEntity("runner-2").SetTeam("red").SetLapNumber(1).SetLapTimeMs(60420),

            new GoalEvent
            {
                tick = 62
            }.SetEntity("runner-1").SetTeam("blue").SetGoalType("checkpoint").SetValue(0.75f),

            new HighlightTagEvent
            {
                tick = 70
            }.SetEntity("runner-2").SetTag("photo_finish").SetSeverity("high"),

            new DespawnEvent
            {
                tick = 100
            }.SetEntity("runner-3").SetTeam("green").SetReason("eliminated")
        };
    }
}
