using UnityEngine;

public interface ISimulation
{
    string Id { get; }
    void Initialize(ScenarioConfig cfg, Transform simRoot, IRng rng);
    void Tick(float dt);
    void Dispose();
}
