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
    [SerializeField, Min(0.0001f)] private float tickDeltaTime = 1f / 60f;

    private const string SimulationRootName = "SimulationRoot";

    private GameObject simulationRoot;
    private ISimulationRunner activeRunner;
    private SimDriver simDriver;
    private ScenarioConfig currentConfig;
    private string currentPresetSource = "<defaults>";

    public string CurrentSimulationId => currentConfig?.simulationId ?? options?.simulationId ?? "MarbleRace";
    public int CurrentSeed => currentConfig?.seed ?? 0;
    public string CurrentPresetSource => currentPresetSource;
    public bool ShowOverlay => options == null || options.showOverlay;
    public int CurrentTick => simDriver?.CurrentTick ?? 0;
    public bool IsPaused => simDriver?.IsPaused ?? false;
    public float TimeScale => simDriver?.TimeScale ?? 1f;

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
        simDriver = new SimDriver(tickDeltaTime);
        ResolveOptions();
        StartSimulation(options?.simulationId ?? "MarbleRace", true);

        if (ShowOverlay && GetComponent<SimulationSelectorOverlay>() == null)
        {
            gameObject.AddComponent<SimulationSelectorOverlay>();
        }
    }

    private void Update()
    {
        simDriver?.Advance(Time.unscaledDeltaTime);

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
        TextAsset presetAsset = null;
        if (options != null && options.presetJson != null)
        {
            presetAsset = options.presetJson;
            source = presetAsset.name;
            return presetAsset.text;
        }

#if UNITY_EDITOR
        var assetPath = $"Assets/Simulations/{simulationId}/Presets/default.json";
        presetAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (presetAsset != null)
        {
            source = assetPath;
            return presetAsset.text;
        }
#endif

        var resourcePath = $"Simulations/{simulationId}/Presets/default";
        presetAsset = Resources.Load<TextAsset>(resourcePath);
        if (presetAsset != null)
        {
            source = $"Resources/{resourcePath}.json";
            return presetAsset.text;
        }

        source = "<base-defaults>";
        Debug.LogWarning($"Bootstrapper: No preset found for simulation '{simulationId}'. Using hardcoded defaults.");
        return string.Empty;
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
        simDriver?.SetRunner(activeRunner);
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
        simDriver?.SetRunner(null);
    }

    public void PauseSimulation()
    {
        simDriver?.Pause();
    }

    public void ResumeSimulation()
    {
        simDriver?.Resume();
    }

    public void StepSimulationOnce()
    {
        simDriver?.RequestSingleStep();
    }

    public void SetSimulationTimeScale(float timeScale)
    {
        simDriver?.SetTimeScale(timeScale);
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
