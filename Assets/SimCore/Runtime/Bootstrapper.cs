using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private BootstrapOptions options;

    private const string SimulationRootName = "SimulationRoot";
    private static readonly string[] KnownSimulationIds = { "AntColonies", "MarbleRace", "RaceCar", "FantasySport" };

    private GameObject simulationRoot;
    private ISimulationRunner activeRunner;
    private ScenarioConfig currentConfig;
    private string currentPresetSource = "<defaults>";

    public string CurrentSimulationId => currentConfig?.simulationId ?? options?.simulationId ?? "MarbleRace";
    public int CurrentSeed => currentConfig?.seed ?? 0;
    public string CurrentPresetSource => currentPresetSource;
    public bool ShowOverlay => options == null || options.showOverlay;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (options == null)
        {
            options = EnsureBootstrapOptionsAsset();
        }
    }
#endif

    private void Awake()
    {
        ResolveOptions();
        ValidateDefaultPresets();
        StartSimulation(options?.simulationId ?? "MarbleRace", true);

        if (ShowOverlay && GetComponent<SimulationSelectorOverlay>() == null)
        {
            gameObject.AddComponent<SimulationSelectorOverlay>();
        }
    }

    private void Update()
    {
        if (options == null || !options.allowHotkeySwitch)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame) StartSimulation("AntColonies", false);
        if (Keyboard.current.f2Key.wasPressedThisFrame) StartSimulation("MarbleRace", false);
        if (Keyboard.current.f3Key.wasPressedThisFrame) StartSimulation("RaceCar", false);
        if (Keyboard.current.f4Key.wasPressedThisFrame) StartSimulation("FantasySport", false);
    }

    private void StartSimulation(string simulationId, bool initialRun)
    {
        simulationId = string.IsNullOrWhiteSpace(simulationId) ? "MarbleRace" : simulationId;
        if (options != null)
        {
            options.simulationId = simulationId;
        }

        ShutdownCurrentRunner();
        EnsureSimulationRoot();
        ClearSimulationRootChildren();

        var presetText = LoadPresetJson(simulationId, out currentPresetSource);
        var resolved = ConfigMerge.Merge(ConfigMerge.CreateBaseDefaults(), presetText);
        resolved.simulationId = simulationId;
        resolved.activeSimulation = simulationId;
        ApplyBootstrapOverrides(resolved);
        resolved.seed = ResolveSeed();
        resolved.NormalizeAliases();
        currentConfig = resolved;

        SpawnRunner(currentConfig);
        WriteRunManifest(currentConfig, currentPresetSource);

        if (!initialRun)
        {
            Debug.Log($"Switched simulation to {simulationId} with seed {currentConfig.seed}");
        }
    }

    private void ResolveOptions()
    {
        if (options != null)
        {
            return;
        }

#if UNITY_EDITOR
        options = EnsureBootstrapOptionsAsset();
#endif

        if (options == null)
        {
            options = ScriptableObject.CreateInstance<BootstrapOptions>();
            Debug.LogWarning("Bootstrapper: BootstrapOptions missing, using safe in-memory defaults.");
        }
    }

#if UNITY_EDITOR
    private static BootstrapOptions EnsureBootstrapOptionsAsset()
    {
        const string assetPath = "Assets/_Bootstrap/BootstrapOptions.asset";
        var resolvedOptions = AssetDatabase.LoadAssetAtPath<BootstrapOptions>(assetPath);
        if (resolvedOptions != null)
        {
            return resolvedOptions;
        }

        var guid = AssetDatabase.FindAssets("t:BootstrapOptions BootstrapOptions");
        if (guid.Length > 0)
        {
            var foundPath = AssetDatabase.GUIDToAssetPath(guid[0]);
            resolvedOptions = AssetDatabase.LoadAssetAtPath<BootstrapOptions>(foundPath);
            if (resolvedOptions != null)
            {
                return resolvedOptions;
            }
        }

        var folderPath = "Assets/_Bootstrap";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "_Bootstrap");
        }

        resolvedOptions = ScriptableObject.CreateInstance<BootstrapOptions>();
        AssetDatabase.CreateAsset(resolvedOptions, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Bootstrapper: Created BootstrapOptions asset at {assetPath}");
        return resolvedOptions;
    }
#endif

    private string LoadPresetJson(string simulationId, out string source)
    {
        if (options != null && options.presetJson != null)
        {
            source = $"TextAsset override '{options.presetJson.name}'";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return options.presetJson.text;
        }

        var streamingAssetsPath = GetStreamingPresetPath(simulationId);
        if (TryReadStreamingPreset(streamingAssetsPath, out var streamingJson))
        {
            source = $"StreamingAssets/{GetStreamingPresetRelativePath(simulationId)}";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return streamingJson;
        }

        var resourcePath = $"Simulations/{simulationId}/Presets/default";
        var presetAsset = Resources.Load<TextAsset>(resourcePath);
        if (presetAsset != null)
        {
            source = $"Resources/{resourcePath}.json";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return presetAsset.text;
        }

        source = "<base-defaults>";
        Debug.LogWarning($"Bootstrapper: No preset found for simulation '{simulationId}'. Using hardcoded defaults.");
        return string.Empty;
    }

    private void ValidateDefaultPresets()
    {
        foreach (var simulationId in KnownSimulationIds)
        {
            if (!HasDefaultPreset(simulationId))
            {
                Debug.LogWarning($"Bootstrapper: Missing default preset for simulation '{simulationId}'. Expected StreamingAssets or Resources fallback.");
            }
        }
    }

    private static bool HasDefaultPreset(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return false;
        }

        if (File.Exists(GetStreamingPresetPath(simulationId)))
        {
            return true;
        }

        var resourcePath = $"Simulations/{simulationId}/Presets/default";
        return Resources.Load<TextAsset>(resourcePath) != null;
    }

    private static string GetStreamingPresetPath(string simulationId)
    {
        return Path.Combine(Application.streamingAssetsPath, GetStreamingPresetRelativePath(simulationId));
    }

    private static string GetStreamingPresetRelativePath(string simulationId)
    {
        return Path.Combine("Simulations", simulationId, "Presets", "default.json");
    }

    private static bool TryReadStreamingPreset(string absolutePath, out string json)
    {
        json = string.Empty;

        try
        {
            if (File.Exists(absolutePath))
            {
                json = File.ReadAllText(absolutePath);
                return !string.IsNullOrWhiteSpace(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Bootstrapper: Failed to read StreamingAssets preset at '{absolutePath}'. {ex.Message}");
        }

        return false;
    }

    private void ApplyBootstrapOverrides(ScenarioConfig config)
    {
        if (options == null)
        {
            return;
        }

        config.simulationId = options.simulationId;
        config.activeSimulation = options.simulationId;
    }

    private int ResolveSeed()
    {
        if (options == null)
        {
            return UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        return options.seedPolicy switch
        {
            SeedPolicy.Fixed => options.fixedSeed,
            SeedPolicy.FromSystemTime => Environment.TickCount,
            _ => UnityEngine.Random.Range(int.MinValue, int.MaxValue)
        };
    }

    private void SpawnRunner(ScenarioConfig config)
    {
        var prefab = SimulationRegistry.LoadRunnerPrefab(config.simulationId);
        GameObject runnerObject;

        if (prefab != null)
        {
            runnerObject = Instantiate(prefab, simulationRoot.transform);
        }
        else
        {
            runnerObject = new GameObject($"{config.simulationId}RunnerPlaceholder");
            runnerObject.transform.SetParent(simulationRoot.transform, false);
            Debug.LogWarning($"Bootstrapper: Missing prefab for {config.simulationId} at Resources/{SimulationRegistry.GetResourcePath(config.simulationId)}.prefab");
        }

        activeRunner = runnerObject.GetComponent<ISimulationRunner>();
        if (activeRunner == null)
        {
            Debug.LogWarning($"Bootstrapper: Runner GameObject '{runnerObject.name}' does not implement ISimulationRunner.");
            return;
        }

        activeRunner.Initialize(config);
    }

    private void EnsureSimulationRoot()
    {
        if (simulationRoot == null)
        {
            var existing = GameObject.Find(SimulationRootName);
            simulationRoot = existing != null ? existing : new GameObject(SimulationRootName);
        }
    }

    private void ClearSimulationRootChildren()
    {
        if (simulationRoot == null)
        {
            return;
        }

        for (var i = simulationRoot.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(simulationRoot.transform.GetChild(i).gameObject);
        }
    }

    private void ShutdownCurrentRunner()
    {
        activeRunner?.Shutdown();
        activeRunner = null;
    }

    private void WriteRunManifest(ScenarioConfig config, string presetSource)
    {
        var runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{config.simulationId}";
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        var outputRoot = string.IsNullOrWhiteSpace(config.recording?.outputRoot) ? "Runs" : config.recording.outputRoot;
        var runFolder = Path.Combine(projectRoot, outputRoot, runId);
        Directory.CreateDirectory(runFolder);

        var manifest = new JObject
        {
            ["runId"] = runId,
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["simulationId"] = config.simulationId,
            ["seed"] = config.seed,
            ["resolvedConfig"] = JObject.Parse(ConfigMerge.ToPrettyJson(config)),
            ["presetSource"] = presetSource
        };

        var manifestPath = Path.Combine(runFolder, "run_manifest.json");
        File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented));
        Debug.Log($"Bootstrapper: Run manifest written to {manifestPath}");
    }
}
