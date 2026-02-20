using System;
using System.Collections.Generic;

public static class SimRegistry
{
    private static readonly Dictionary<string, Func<ISimulation>> Registry = new()
    {
        ["AntColonies"] = () => null,
        ["MarbleRace"] = () => new MarbleRaceSimulation()
    };

    public static bool TryCreate(string simulationId, out ISimulation simulation)
    {
        simulation = null;

        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return false;
        }

        if (!Registry.TryGetValue(simulationId, out var factory) || factory == null)
        {
            return false;
        }

        simulation = factory.Invoke();
        return true;
    }
}
