using System;

[Serializable]
public class ScenarioConfig
{
    public int schemaVersion;
    public string scenarioName;
    public string mode;
    public int seed;
    public string activeSimulation;
    public WorldConfig world;
    public RecordingConfig recording;
    public RenderingConfig rendering;
}

[Serializable]
public class WorldConfig
{
    public int arenaWidth;
    public int arenaHeight;
    public bool walls;
    public float obstacleDensity;
}

[Serializable]
public class RecordingConfig
{
    public bool enabled;
    public int snapshotEveryNTicks;
    public bool eventsEnabled;
    public string outputRoot;
}

[Serializable]
public class RenderingConfig
{
    public int targetUpscale = 4;
    public int targetFps = 60;
}
