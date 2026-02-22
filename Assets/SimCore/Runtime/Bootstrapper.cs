using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private BootstrapOptions options;
    [SerializeField, Min(0.0001f)] private float tickDeltaTime = 1f / 60f;
    [SerializeField] private ContentPack contentPackOverride;

    private const string SimulationRootName = "SimulationRoot";
    private static readonly string[] KnownSimulationIds = { "AntColonies", "MarbleRace", "RaceCar", "FantasySport" };

    private GameObject simulationRoot;
    private GameObject activeRunnerObject;
    private ITickableSimulationRunner activeRunner;
    private SimDriver simDriver;
    private ReplayDriver replayDriver;
    private ScenarioConfig currentConfig;
    private string currentPresetSource = "<defaults>";
    private string currentRunFolder;
    private int tickCount;
    private float smoothedFps;

    public string CurrentSimulationId => currentConfig?.simulationId ?? options?.simulationId ?? "MarbleRace";
    public int CurrentSeed => currentConfig?.seed ?? 0;
    public string CurrentPresetSource => currentPresetSource;
    public bool ShowOverlay => options == null || options.showOverlay;
    public int TickCount => tickCount;
    public float CurrentFps => smoothedFps;
    public int CurrentTick => IsReplayMode ? replayDriver?.CurrentTick ?? 0 : simDriver?.CurrentTick ?? 0;
    public bool IsPaused => IsReplayMode ? replayDriver?.IsPaused ?? false : simDriver?.IsPaused ?? false;
    public float TimeScale => IsReplayMode ? replayDriver?.TimeScale ?? 1f : simDriver?.TimeScale ?? 1f;

    private bool IsReplayMode => string.Equals(currentConfig?.mode, "Replay", StringComparison.OrdinalIgnoreCase);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (options == null)
        {
            options = EnsureBootstrapOptionsAsset();
        }

        if (options != null && options.simulationCatalog == null)
        {
            options.simulationCatalog = EnsureSimulationCatalogAsset();
            EditorUtility.SetDirty(options);
        }
    }
#endif

    private void Awake()
    {
        simDriver = new SimDriver(tickDeltaTime);
        replayDriver = new ReplayDriver(tickDeltaTime);
        ResolveOptions();
        EnsureSimulationCatalogReference();

        StartSimulation(options?.simulationId ?? GetFallbackSimulationId(), true);

        if (ShowOverlay && GetComponent<SimulationSelectorOverlay>() == null)
        {
            gameObject.AddComponent<SimulationSelectorOverlay>();
        }
    }

    private void Update()
    {
        if (IsReplayMode)
        {
            replayDriver?.Advance(Time.unscaledDeltaTime);
        }
        else
        {
            simDriver?.Advance(Time.unscaledDeltaTime);
        }

        if (options == null || !options.allowHotkeySwitch)
        {
            return;
        }

        tickCount++;
        var instantaneousFps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
        smoothedFps = smoothedFps <= 0f ? instantaneousFps : Mathf.Lerp(smoothedFps, instantaneousFps, 0.15f);
    }


    public void PauseOrResume()
    {
        if (IsPaused)
        {
            ResumeSimulation();
        }
        else
        {
            PauseSimulation();
        }
    }

    public void StepSimulation()
    {
        StepSimulationOnce();
    }

    public void ResetSimulation() => StartSimulation(CurrentSimulationId, false);

    public void SwitchToNextSimulation()
    {
        var simulationId = GetSimulationIdAtOffset(1);
        StartSimulation(simulationId, false);
    }

    public void SwitchToPreviousSimulation()
    {
        var simulationId = GetSimulationIdAtOffset(-1);
        StartSimulation(simulationId, false);
    }

    private string GetSimulationIdAtOffset(int direction)
    {
        var catalog = options?.simulationCatalog;
        if (catalog == null || catalog.Simulations.Count == 0)
        {
            return CurrentSimulationId;
        }

        var currentIndex = 0;
        for (var i = 0; i < catalog.Simulations.Count; i++)
        {
            if (string.Equals(catalog.Simulations[i]?.simulationId, CurrentSimulationId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = (currentIndex + direction + catalog.Simulations.Count) % catalog.Simulations.Count;
        var simulationId = catalog.Simulations[nextIndex]?.simulationId;
        return string.IsNullOrWhiteSpace(simulationId) ? CurrentSimulationId : simulationId;
    }

    private void StartSimulation(string simulationId, bool initialRun)
    {
        simulationId = string.IsNullOrWhiteSpace(simulationId) ? GetFallbackSimulationId() : simulationId;
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
        resolved.seed = ResolveSeed(resolved);
        resolved.NormalizeAliases();
        currentConfig = resolved;
        tickCount = 0;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Clamp(currentConfig.rendering?.targetFps ?? 60, 30, 240);

        ConfigureDeterminism(currentConfig.seed);
        EventBusService.ResetGlobal();
        PrimitiveSpriteLibrary.ClearCache();
        AntAtlasLibrary.ClearCache();

        var selectedContentPack = ResolveContentPack(simulationId);
        Debug.Log($"[Bootstrapper] sim={simulationId} contentPack={(selectedContentPack != null ? selectedContentPack.name : "<none>")}");
        if (selectedContentPack != null)
        {
            ContentPackService.Set(selectedContentPack);
        }
        else
        {
            ContentPackService.Clear();
        }

        ArenaBuilder.Build(simulationRoot.transform, currentConfig);
        SpawnRunner(currentConfig);

        if (IsReplayMode)
        {
            currentRunFolder = currentConfig.replay?.runFolder;
            replayDriver?.SetRunner(activeRunner);
            var replayLoaded = replayDriver?.Load(currentConfig) ?? false;
            if (replayLoaded && replayDriver != null && !replayDriver.ValidateRunnerForLoadedConfig(currentConfig.simulationId))
            {
                var required = currentConfig.replay != null && currentConfig.replay.rendererOnly
                    ? "IReplayableSimulationRunner"
                    : "IReplayableSimulationRunner or ITickableSimulationRunner";
                throw new InvalidOperationException(
                    $"Bootstrapper: Replay runner contract mismatch for simulation '{currentConfig.simulationId}'. " +
                    $"Runner '{activeRunnerObject?.name ?? "<null>"}' must implement {required} for replay mode.");
            }

            simDriver?.SetRunner(null);
            simDriver?.ConfigureRecording(currentConfig, null, EventBusService.Global);
        }
        else
        {
            currentRunFolder = WriteRunManifest(currentConfig, currentPresetSource);
            simDriver?.ConfigureRecording(currentConfig, currentRunFolder, EventBusService.Global);

            // Unified tick contract: all live simulation ticks must go through exactly one ITickableSimulationRunner.
            var tickableRunner = RunnerContract.RequireTickable(activeRunnerObject, currentConfig.simulationId, "bootstrap pre-tick validation");
            simDriver?.SetRunner(tickableRunner);

            replayDriver?.SetRunner(null);
        }

        if (!initialRun)
        {
            Debug.Log($"Switched simulation to {simulationId} mode={currentConfig.mode} seed={currentConfig.seed}");
        }
    }


    private ContentPack ResolveContentPack(string simulationId)
    {
        if (contentPackOverride != null)
        {
            return contentPackOverride;
        }

        var entry = options?.simulationCatalog?.FindById(simulationId);
        if (entry != null && entry.defaultContentPack != null)
        {
            return entry.defaultContentPack;
        }
        var catalog = options?.simulationCatalog;
        if (catalog != null && catalog.GlobalDefaultContentPack != null)
        {
            return catalog.GlobalDefaultContentPack;
        }

        return null;
    }

    private string GetFallbackSimulationId()
    {
        var catalog = options?.simulationCatalog;
        if (catalog != null && catalog.Simulations.Count > 0)
        {
            return catalog.Simulations[0]?.simulationId ?? "MarbleRace";
        }

        return "MarbleRace";
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

    private void EnsureSimulationCatalogReference()
    {
        if (options == null || options.simulationCatalog != null)
        {
            return;
        }

#if UNITY_EDITOR
        options.simulationCatalog = EnsureSimulationCatalogAsset();
        options.simulationCatalog?.AssignGlobalDefaultContentPackIfMissing();
        EditorUtility.SetDirty(options);
#else
        options.simulationCatalog = Resources.Load<SimulationCatalog>("SimulationCatalog");
#endif
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
        resolvedOptions.simulationCatalog = EnsureSimulationCatalogAsset();
        AssetDatabase.CreateAsset(resolvedOptions, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Bootstrapper: Created BootstrapOptions asset at {assetPath}");
        return resolvedOptions;
    }

    private static SimulationCatalog EnsureSimulationCatalogAsset()
    {
        const string assetPath = "Assets/_Bootstrap/SimulationCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(assetPath);
        if (catalog != null)
        {
            return catalog;
        }

        var guid = AssetDatabase.FindAssets("t:SimulationCatalog SimulationCatalog");
        if (guid.Length > 0)
        {
            var foundPath = AssetDatabase.GUIDToAssetPath(guid[0]);
            catalog = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(foundPath);
            if (catalog != null)
            {
                return catalog;
            }
        }

        catalog = ScriptableObject.CreateInstance<SimulationCatalog>();
        AssetDatabase.CreateAsset(catalog, assetPath);
        catalog.AutoDiscoverSimulations();
        catalog.AssignGlobalDefaultContentPackIfMissing();
        AssetDatabase.SaveAssets();
        Debug.Log($"Bootstrapper: Created SimulationCatalog asset at {assetPath}");
        return catalog;
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

        var entry = options?.simulationCatalog?.FindById(simulationId);
        if (entry != null && entry.defaultPreset != null)
        {
            source = $"Catalog/{simulationId}";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return entry.defaultPreset.text;
        }

        var streamingPath = GetStreamingPresetPath(simulationId);
        if (TryReadStreamingPreset(streamingPath, out var streamingJson))
        {
            source = $"StreamingAssets/{GetStreamingPresetRelativePath(simulationId)}";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return streamingJson;
        }

        var resourcePath = $"Simulations/{simulationId}/Presets/default";
        var resourcePreset = Resources.Load<TextAsset>(resourcePath);
        if (resourcePreset != null)
        {
            source = $"Resources/{resourcePath}.json";
            Debug.Log($"Bootstrapper: Using preset from {source} for simulation '{simulationId}'.");
            return resourcePreset.text;
        }

        source = "<base-defaults>";
        Debug.LogWarning($"Bootstrapper: No preset found for simulation '{simulationId}'. Using hardcoded defaults.");
        return string.Empty;
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

    private int ResolveSeed(ScenarioConfig config)
    {
        if (string.Equals(config?.mode, "Replay", StringComparison.OrdinalIgnoreCase))
        {
            return config.seed;
        }

        if (options == null)
        {
            return Environment.TickCount;
        }

        return options.seedPolicy switch
        {
            SeedPolicy.Fixed => options.fixedSeed,
            SeedPolicy.FromSystemTime => Environment.TickCount,
            _ => Environment.TickCount
        };
    }

    private void ConfigureDeterminism(int seed)
    {
        UnityEngine.Random.InitState(seed);

        var sampleA = UnityEngine.Random.value;
        var sampleB = UnityEngine.Random.value;
        var sampleC = UnityEngine.Random.value;
        Debug.Log($"Bootstrapper: RNG sanity check seed={seed} values=[{sampleA:F6}, {sampleB:F6}, {sampleC:F6}]");

        UnityEngine.Random.InitState(seed);
        RngService.SetGlobal(new SeededRng(seed));
    }

    private void SpawnRunner(ScenarioConfig config)
    {
        var prefab = options?.simulationCatalog?.FindById(config.simulationId)?.runnerPrefab;
        prefab ??= SimulationRegistry.LoadRunnerPrefab(config.simulationId);
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

        activeRunnerObject = runnerObject;
        activeRunner = RunnerContract.RequireTickable(activeRunnerObject, config.simulationId, "runner instantiation");

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
        activeRunnerObject = null;
        simDriver?.SetRunner(null);
        replayDriver?.SetRunner(null);
    }

    public void PauseSimulation()
    {
        if (IsReplayMode)
        {
            replayDriver?.Pause();
            return;
        }

        simDriver?.Pause();
    }

    public void ResumeSimulation()
    {
        if (IsReplayMode)
        {
            replayDriver?.Resume();
            return;
        }

        simDriver?.Resume();
    }

    public void StepSimulationOnce()
    {
        if (IsReplayMode)
        {
            replayDriver?.RequestSingleStep();
            return;
        }

        simDriver?.RequestSingleStep();
    }

    public void SetSimulationTimeScale(float timeScale)
    {
        if (IsReplayMode)
        {
            replayDriver?.SetTimeScale(timeScale);
            return;
        }

        simDriver?.SetTimeScale(timeScale);
    }

    private string WriteRunManifest(ScenarioConfig config, string presetSource)
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
        return runFolder;
    }
}
