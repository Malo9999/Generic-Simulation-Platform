using UnityEngine;

public static class SimulationRegistry
{
    public static string GetResourcePath(string simulationId)
    {
        return $"Simulations/{simulationId}/{simulationId}Runner";
    }

    public static GameObject LoadRunnerPrefab(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        return Resources.Load<GameObject>(GetResourcePath(simulationId));
    }
}
