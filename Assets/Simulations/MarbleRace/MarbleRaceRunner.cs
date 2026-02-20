using UnityEngine;

public class MarbleRaceRunner : MonoBehaviour, ISimulationRunner
{
    public void Initialize(ScenarioConfig config)
    {
        Debug.Log($"MarbleRaceRunner Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Shutdown()
    {
        Debug.Log("MarbleRaceRunner Shutdown");
    }
}
