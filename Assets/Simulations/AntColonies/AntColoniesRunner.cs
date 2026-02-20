using UnityEngine;

public class AntColoniesRunner : MonoBehaviour, ISimulationRunner
{
    public void Initialize(ScenarioConfig config)
    {
        Debug.Log($"AntColoniesRunner Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Shutdown()
    {
        Debug.Log("AntColoniesRunner Shutdown");
    }
}
