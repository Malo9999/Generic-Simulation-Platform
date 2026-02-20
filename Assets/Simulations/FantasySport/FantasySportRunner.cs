using UnityEngine;

public class FantasySportRunner : MonoBehaviour, ISimulationRunner
{
    public void Initialize(ScenarioConfig config)
    {
        Debug.Log($"FantasySportRunner Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Shutdown()
    {
        Debug.Log("FantasySportRunner Shutdown");
    }
}
