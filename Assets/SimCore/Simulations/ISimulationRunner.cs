public interface ISimulationRunner
{
    void Initialize(ScenarioConfig config);
    void Shutdown();
}

public interface ITickableSimulationRunner : ISimulationRunner
{
    void Tick(int tickIndex, float dt);
}

public interface IStateSnapshotProvider
{
    object CaptureState();
}
