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
            }.SetPrefab("RunnerBot").SetKind("racer").SetVelocity(0.5f, 0.0f).SetEntity("runner-1").SetTeam("blue").SetPosition(1.5f, 2.0f),

            new SpawnEvent
            {
                tick = 0
            }.SetPrefab("RunnerBot").SetKind("racer").SetVelocity(0.4f, 0.1f).SetEntity("runner-2").SetTeam("red").SetPosition(2.5f, 2.0f),

            new PickupEvent
            {
                tick = 12
            }.SetItemId("flag-a").SetItemKind("flag").SetEntity("runner-1").SetTeam("blue").SetPosition(3.0f, 2.4f),

            new HitEvent
            {
                tick = 20
            }.SetTargetId("runner-1").SetDamage(15f).SetWeapon("pulse").SetEntity("runner-2").SetTeam("red").SetPosition(3.8f, 2.4f),

            new DropoffEvent
            {
                tick = 30
            }.SetItemId("flag-a").SetDestination("base-blue").SetEntity("runner-1").SetTeam("blue").SetPosition(5.5f, 2.5f),

            new ScoreEvent
            {
                tick = 31
            }.SetDelta(1).SetTotal(1).SetEntity("runner-1").SetTeam("blue"),

            new OvertakeEvent
            {
                tick = 44
            }.SetPassedEntityId("runner-3").SetNewPosition(2).SetEntity("runner-2").SetTeam("red").SetPosition(6.1f, 2.6f),

            new LapEvent
            {
                tick = 50
            }.SetLapNumber(1).SetLapTimeMs(60420).SetEntity("runner-2").SetTeam("red"),

            new GoalEvent
            {
                tick = 62
            }.SetGoalType("checkpoint").SetValue(0.75f).SetEntity("runner-1").SetTeam("blue"),

            new HighlightTagEvent
            {
                tick = 70
            }.SetTag("photo_finish").SetSeverity("high").SetEntity("runner-2"),

            new DespawnEvent
            {
                tick = 100
            }.SetReason("eliminated").SetEntity("runner-3").SetTeam("green")
        };
    }
}
