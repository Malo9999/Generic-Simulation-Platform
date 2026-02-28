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
    public PredatorPreyDocuConfig predatorPreyDocu = new();

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
        predatorPreyDocu ??= new PredatorPreyDocuConfig();
        antColonies.Normalize();
        marbleRace.Normalize();
        fantasySport.Normalize();
        raceCar.Normalize();
        predatorPreyDocu.Normalize();
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


[Serializable]
public class PredatorPreyDocuConfig
{
    public Season season = new();
    public Map map = new();
    public Population pop = new();
    public Visuals visuals = new();
    public Movement movement = new();

    public void Normalize()
    {
        season ??= new Season();
        map ??= new Map();
        pop ??= new Population();
        visuals ??= new Visuals();
        movement ??= new Movement();

        season.Normalize();
        map.Normalize();
        pop.Normalize();
        visuals.Normalize();
        movement.Normalize();
    }
}

[Serializable]
public class Season
{
    public int wetTicks = 18000;
    public int dryTicks = 18000;

    public void Normalize()
    {
        wetTicks = UnityEngine.Mathf.Max(300, wetTicks);
        dryTicks = UnityEngine.Mathf.Max(300, dryTicks);
    }
}

[Serializable]
public class Map
{
    public float riverWidth = 10f;
    public float floodplainWidth = 26f;
    public int creekCount = 6;
    public float creekWidth = 2.2f;
    public int treeClusterCount = 55;

    public void Normalize()
    {
        riverWidth = UnityEngine.Mathf.Clamp(riverWidth, 0.5f, 80f);
        floodplainWidth = UnityEngine.Mathf.Clamp(floodplainWidth, 1f, 120f);
        creekCount = UnityEngine.Mathf.Clamp(creekCount, 0, 64);
        creekWidth = UnityEngine.Mathf.Clamp(creekWidth, 0.2f, 8f);
        treeClusterCount = UnityEngine.Mathf.Clamp(treeClusterCount, 0, 500);
    }
}

[Serializable]
public class Population
{
    public int herdCount = 4;
    public int preyPerHerd = 120;
    public int prideCount = 6;
    public int lionsPerPride = 10;
    public int roamingCoalitions = 2;
    public int coalitionSize = 2;

    public void Normalize()
    {
        herdCount = UnityEngine.Mathf.Clamp(herdCount, 1, 32);
        preyPerHerd = UnityEngine.Mathf.Clamp(preyPerHerd, 1, 400);
        prideCount = UnityEngine.Mathf.Clamp(prideCount, 1, 24);
        lionsPerPride = UnityEngine.Mathf.Clamp(lionsPerPride, 1, 40);
        roamingCoalitions = UnityEngine.Mathf.Clamp(roamingCoalitions, 0, 12);
        coalitionSize = UnityEngine.Mathf.Clamp(coalitionSize, 1, 8);
    }
}

[Serializable]
public class Visuals
{
    public float preyScale = 0.55f;
    public float lionScale = 0.68f;
    public float treeScale = 1.15f;
    public bool showPackAccent = true;
    public bool showMaleRing = true;

    public void Normalize()
    {
        preyScale = UnityEngine.Mathf.Clamp(preyScale, 0.2f, 2f);
        lionScale = UnityEngine.Mathf.Clamp(lionScale, 0.2f, 2f);
        treeScale = UnityEngine.Mathf.Clamp(treeScale, 0.2f, 2f);
    }
}

[Serializable]
public class Movement
{
    public float herdCruiseSpeed = 4f;
    public float herdWander = 0.65f;
    public float preyJitter = 0.35f;
    public float pridePatrolSpeed = 2.2f;
    public float lionWander = 0.45f;
    public float edgeMargin = 4f;
    public float herdRadius = 7.5f;

    public void Normalize()
    {
        herdCruiseSpeed = UnityEngine.Mathf.Clamp(herdCruiseSpeed, 0.1f, 20f);
        herdWander = UnityEngine.Mathf.Clamp(herdWander, 0f, 3f);
        preyJitter = UnityEngine.Mathf.Clamp(preyJitter, 0f, 3f);
        pridePatrolSpeed = UnityEngine.Mathf.Clamp(pridePatrolSpeed, 0.1f, 15f);
        lionWander = UnityEngine.Mathf.Clamp(lionWander, 0f, 3f);
        edgeMargin = UnityEngine.Mathf.Clamp(edgeMargin, 0.5f, 30f);
        herdRadius = UnityEngine.Mathf.Clamp(herdRadius, 0.5f, 25f);
    }
}
