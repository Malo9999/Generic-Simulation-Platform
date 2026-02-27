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
    public PresentationConfig presentation = new();
    public AntColoniesConfig antColonies = new();
    public MarbleRaceConfig marbleRace = new();
    public FantasySportConfig fantasySport = new();
    public RaceCarConfig raceCar = new();

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
        presentation ??= new PresentationConfig();
        antColonies ??= new AntColoniesConfig();
        marbleRace ??= new MarbleRaceConfig();
        fantasySport ??= new FantasySportConfig();
        raceCar ??= new RaceCarConfig();
        antColonies.Normalize();
        marbleRace.Normalize();
        fantasySport.Normalize();
        raceCar.Normalize();
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

[Serializable]
public class PresentationConfig
{
    public bool showHealthBars = false;
}

[Serializable]
public class MarbleRaceConfig
{
    public int marbleCount = 12;
    public int laps = 3;
    public string trackPreset = "Auto";

    public void Normalize()
    {
        marbleCount = UnityEngine.Mathf.Clamp(marbleCount, 2, 64);
        laps = UnityEngine.Mathf.Clamp(laps, 1, 20);
        trackPreset ??= "Auto";
    }
}

[Serializable]
public class FantasySportConfig
{
    public int teamCount = 2;
    public int playersPerTeam = 8;
    public float periodLength = 180f;

    public void Normalize()
    {
        teamCount = 2;
        playersPerTeam = UnityEngine.Mathf.Clamp(playersPerTeam, 2, 16);
        periodLength = UnityEngine.Mathf.Clamp(periodLength, 15f, 1800f);
    }
}

[Serializable]
public class RaceCarConfig
{
    public int carCount = 10;
    public int laps = 3;
    public string trackPreset = "Auto";

    public void Normalize()
    {
        carCount = UnityEngine.Mathf.Clamp(carCount, 2, 64);
        laps = UnityEngine.Mathf.Clamp(laps, 1, 20);
        trackPreset ??= "Auto";
    }
}
