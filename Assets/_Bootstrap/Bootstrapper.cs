using System;
using System.IO;
using UnityEngine;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private bool createDefaultIfMissing = true;
    [SerializeField] private string configRelativePath = "Configs/scenario_quad_poc.json";

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

    private void Awake()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            Debug.LogError("Bootstrapper: Unable to resolve project root from Application.dataPath.");
            return;
        }

        var configPath = Path.Combine(projectRoot, configRelativePath);
        if (!File.Exists(configPath))
        {
            if (!createDefaultIfMissing)
            {
                Debug.LogWarning($"Bootstrapper: Config file not found at '{configPath}'.");
                return;
            }

            var defaultConfig = CreateDefaultConfig();
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            File.WriteAllText(configPath, JsonUtility.ToJson(defaultConfig, true));
            Debug.Log($"Bootstrapper: Created default scenario config at '{configPath}'.");
        }

        var json = File.ReadAllText(configPath);
        var config = JsonUtility.FromJson<ScenarioConfig>(json);
        if (config == null)
        {
            Debug.LogError($"Bootstrapper: Failed to parse scenario config at '{configPath}'.");
            return;
        }

        config.world ??= new WorldConfig();
        config.recording ??= new RecordingConfig();
        config.rendering ??= new RenderingConfig();

        var resolvedRunsPath = Path.GetFullPath(Path.Combine(projectRoot, config.recording.outputRoot ?? "Runs"));

        Debug.Log(
            "Bootstrapper loaded scenario config\n" +
            $"- scenarioName: {config.scenarioName}\n" +
            $"- seed: {config.seed}\n" +
            $"- activeSimulation: {config.activeSimulation}\n" +
            $"- world: {config.world.arenaWidth}x{config.world.arenaHeight}, walls={config.world.walls}, obstacleDensity={config.world.obstacleDensity}\n" +
            $"- recording: enabled={config.recording.enabled}, snapshotEveryNTicks={config.recording.snapshotEveryNTicks}, eventsEnabled={config.recording.eventsEnabled}\n" +
            $"- runsPath: {resolvedRunsPath}");
    }

    private static ScenarioConfig CreateDefaultConfig()
    {
        return new ScenarioConfig
        {
            schemaVersion = 1,
            scenarioName = "Quad PoC",
            mode = "Sim",
            seed = 12345,
            activeSimulation = "AntColonies",
            world = new WorldConfig
            {
                arenaWidth = 64,
                arenaHeight = 64,
                walls = true,
                obstacleDensity = 0.1f
            },
            recording = new RecordingConfig
            {
                enabled = true,
                snapshotEveryNTicks = 30,
                eventsEnabled = true,
                outputRoot = "Runs"
            },
            rendering = new RenderingConfig
            {
                targetUpscale = 4,
                targetFps = 60
            }
        };
    }
}
