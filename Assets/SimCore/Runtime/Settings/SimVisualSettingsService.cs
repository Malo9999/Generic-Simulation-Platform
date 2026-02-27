using System;
using System.Collections.Generic;

public static class SimVisualSettingsService
{
    private static readonly Dictionary<string, SimVisualSettings> SettingsBySim = new(StringComparer.OrdinalIgnoreCase);

    public static string ActiveSimulationId { get; private set; }

    public static void SetForSimulation(string simulationId, SimVisualSettings settings)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return;
        }

        if (settings == null)
        {
            SettingsBySim.Remove(simulationId);
            if (string.Equals(ActiveSimulationId, simulationId, StringComparison.OrdinalIgnoreCase))
            {
                ActiveSimulationId = simulationId;
            }
            return;
        }

        SettingsBySim[simulationId] = settings;
        ActiveSimulationId = simulationId;
    }

    public static SimVisualSettings CurrentForActiveSim()
    {
        return CurrentForSimulation(ActiveSimulationId);
    }

    public static SimVisualSettings CurrentForSimulation(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        return SettingsBySim.TryGetValue(simulationId, out var settings) ? settings : null;
    }

    public static void Clear()
    {
        SettingsBySim.Clear();
        ActiveSimulationId = null;
    }
}
