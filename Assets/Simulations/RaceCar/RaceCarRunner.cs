using UnityEngine;

public class RaceCarRunner : MonoBehaviour, ISimulationRunner
{
    public void Initialize(ScenarioConfig config)
    {
        Debug.Log($"RaceCarRunner Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Shutdown()
    {
        Debug.Log("RaceCarRunner Shutdown");
    }
}
