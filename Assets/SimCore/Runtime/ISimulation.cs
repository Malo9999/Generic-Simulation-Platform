using UnityEngine;

public interface ISimulation
{
    string Id { get; }
    void Initialize(ScenarioConfig cfg, Transform simRoot, IRng rng);
    void Tick(float dt);
    void Dispose();
}

public interface IRecordable
{
    object CaptureState();
}

public interface IReplayableState
{
    void ApplyReplayState(object state);
    void ApplyReplayEvent(string eventType, object payload);
}
