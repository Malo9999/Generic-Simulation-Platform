using UnityEngine;

public sealed class ReactionDiffusionRunner : MonoBehaviour, ITickableSimulationRunner
{
    private ReactionDiffusionBootstrap bootstrap;

    public void Initialize(ScenarioConfig config)
    {
        bootstrap = GetComponent<ReactionDiffusionBootstrap>();
        if (bootstrap == null)
        {
            bootstrap = gameObject.AddComponent<ReactionDiffusionBootstrap>();
        }

        bootstrap.StartOrResetSimulation();
    }

    public void Tick(int tickIndex, float dt)
    {
        if (bootstrap == null)
        {
            return;
        }

        bootstrap.Tick(dt);
    }

    public void Shutdown()
    {
        if (bootstrap == null)
        {
            return;
        }

        bootstrap.ShutdownSimulation();
    }
}
