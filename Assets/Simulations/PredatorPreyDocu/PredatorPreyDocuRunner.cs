using UnityEngine;

public class PredatorPreyDocuRunner : MonoBehaviour, ITickableSimulationRunner
{
    public void Initialize(ScenarioConfig config)
    {
        SceneGraphUtil.PrepareRunner(transform, "PredatorPreyDocu");
        Debug.Log("PredatorPreyDocuRunner Initialize simulationId=PredatorPreyDocu, seed=" + (config != null ? config.seed : 0));
    }

    public void Tick(int tickIndex, float dt)
    {
    }

    public void Shutdown()
    {
        Debug.Log("PredatorPreyDocuRunner Shutdown");
    }
}
