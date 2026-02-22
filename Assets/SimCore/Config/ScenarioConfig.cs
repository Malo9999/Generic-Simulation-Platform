using System;

[Serializable]
public class ScenarioConfig
{
    public int schemaVersion = 1;
    public string scenarioName = "Default Scenario";
    public string mode = "Sim";
    public int seed = 0;
    public string simulationId = "MarbleRace";
    public string activeSimulation;
    public WorldConfig world = new();
    public RecordingConfig recording = new();
    public ReplayConfig replay = new();
    public RenderingConfig rendering = new();
    public AntColoniesConfig antColonies = new();

    public void NormalizeAliases()
    {
        if (!string.IsNullOrWhiteSpace(activeSimulation) && string.IsNullOrWhiteSpace(simulationId))
        {
            simulationId = activeSimulation;
        }

        activeSimulation = simulationId;
        mode = string.Equals(mode, "Replay", StringComparison.OrdinalIgnoreCase) ? "Replay" : "Sim";
        world ??= new WorldConfig();
        recording ??= new RecordingConfig();
        replay ??= new ReplayConfig();
        rendering ??= new RenderingConfig();
        antColonies ??= new AntColoniesConfig();
        antColonies.Normalize();
    }
}

[Serializable]
public class WorldConfig
{
    public int arenaWidth = 64;
    public int arenaHeight = 64;
    public bool walls = true;
    public float obstacleDensity = 0.1f;
}

[Serializable]
public class RecordingConfig
{
    public bool enabled = true;
    public int snapshotEveryNTicks = 30;
    public bool eventsEnabled = true;
    public string outputRoot = "Runs";
}

[Serializable]
public class ReplayConfig
{
    public string runFolder = string.Empty;
    public bool rendererOnly = false;
}

[Serializable]
public class RenderingConfig
{
    public int targetUpscale = 4;
    public int targetFps = 60;
}
