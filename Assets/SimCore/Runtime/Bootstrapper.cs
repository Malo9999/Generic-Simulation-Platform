using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Bootstrapper : MonoBehaviour
{
    [Serializable]
    public struct SimArtSettings
    {
        public ArtMode mode;
        public bool usePlaceholders;
        public DebugPlaceholderMode debugMode;
    }

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
    private ContentPack runtimeMergedPack;
    private ScenarioConfig currentConfig;
    private string currentPresetSource = "<defaults>";
    private string currentRunFolder;
    private int tickCount;
    private float smoothedFps;
    private readonly Dictionary<string, SimArtSettings> artBySim = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentPack> preferredAgentPackBySim = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentSimulationId => currentConfig?.simulationId ?? options?.simulationId ?? "MarbleRace";
    public int CurrentSeed => currentConfig?.seed ?? 0;
    public string CurrentPresetSource => currentPresetSource;
    public bool ShowOverlay => options == null || options.showOverlay;
    public int TickCount => tickCount;
    public float CurrentFps => smoothedFps;
    public string CurrentContentPackName => ContentPackService.Current != null ? ContentPackService.Current.name : "<none>";
    public int CurrentTick => IsReplayMode ? replayDriver?.CurrentTick ?? 0 : simDriver?.CurrentTick ?? 0;
    public bool IsPaused => IsReplayMode ? replayDriver?.IsPaused ?? false : simDriver?.IsPaused ?? false;
    public float TimeScale => IsReplayMode ? replayDriver?.TimeScale ?? 1f : simDriver?.TimeScale ?? 1f;
    public ArtMode CurrentArtMode => GetArt(CurrentSimulationId).mode;
    public bool CurrentUsePlaceholders => GetArt(CurrentSimulationId).usePlaceholders;
    public DebugPlaceholderMode CurrentDebugMode => GetArt(CurrentSimulationId).debugMode;
    public ContentPack CurrentPreferredAgentPack => GetPreferredAgentPack(CurrentSimulationId);

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

        if (options != null)
        {
            EnsureSimSettingsAssets(options);
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

    public void SetArtModeForCurrent(ArtMode mode)
    {
        var s = GetArt(CurrentSimulationId);
        s.mode = mode;
        artBySim[CurrentSimulationId] = s;
        ResetSimulation();
    }

    public void TogglePlaceholdersForCurrent()
    {
        var s = GetArt(CurrentSimulationId);
        s.usePlaceholders = !s.usePlaceholders;
        artBySim[CurrentSimulationId] = s;
        ResetSimulation();
    }

    public void CycleDebugModeForCurrent()
    {
        var s = GetArt(CurrentSimulationId);
        s.debugMode = s.debugMode == DebugPlaceholderMode.Overlay
            ? DebugPlaceholderMode.Replace
            : DebugPlaceholderMode.Overlay;
        artBySim[CurrentSimulationId] = s;
        ResetSimulation();
    }

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

    private SimArtSettings GetArt(string simId)
    {
        if (string.IsNullOrWhiteSpace(simId))
        {
            simId = CurrentSimulationId;
        }

        if (!artBySim.TryGetValue(simId, out var settings))
        {
            var visualSettings = FindVisualSettingsFor(simId, options);
            settings = new SimArtSettings
            {
                mode = ArtMode.Simple,
                usePlaceholders = visualSettings != null && visualSettings.usePrimitiveBaseline,
                debugMode = visualSettings != null ? visualSettings.defaultDebugMode : DebugPlaceholderMode.Overlay
            };
            artBySim[simId] = settings;
        }

        return settings;
    }



    private ContentPack GetPreferredAgentPack(string simId)
    {
        if (string.IsNullOrWhiteSpace(simId))
        {
            return null;
        }

        return preferredAgentPackBySim.TryGetValue(simId, out var pack) ? pack : null;
    }

    private static SimVisualSettings FindVisualSettingsFor(string simulationId, BootstrapOptions bootstrapOptions)
    {
        if (bootstrapOptions == null || string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        if (string.Equals(simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.antColoniesVisual;
        }

        if (string.Equals(simulationId, "MarbleRace", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.marbleRaceVisual;
        }

        if (string.Equals(simulationId, "FantasySport", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.fantasySportVisual;
        }

        if (string.Equals(simulationId, "RaceCar", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.raceCarVisual;
        }

        var extraBindings = bootstrapOptions.extraSimulations;
        if (extraBindings != null)
        {
            for (var i = 0; i < extraBindings.Count; i++)
            {
                var binding = extraBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.simulationId))
                {
                    continue;
                }

                if (string.Equals(binding.simulationId, simulationId, StringComparison.OrdinalIgnoreCase) && binding.visual != null)
                {
                    return binding.visual;
                }
            }
        }

        return null;
    }

    public SimVisualSettings GetCurrentVisualSettings()
    {
        return FindVisualSettingsFor(CurrentSimulationId, options);
    }

    private static SimSettingsBase FindSimSettingsFor(string simulationId, BootstrapOptions bootstrapOptions)
    {
        if (bootstrapOptions == null || string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        if (string.Equals(simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.antColoniesSettings;
        }

        if (string.Equals(simulationId, "MarbleRace", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.marbleRaceSettings;
        }

        if (string.Equals(simulationId, "FantasySport", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.fantasySportSettings;
        }

        if (string.Equals(simulationId, "RaceCar", StringComparison.OrdinalIgnoreCase))
        {
            return bootstrapOptions.raceCarSettings;
        }

        var extraBindings = bootstrapOptions.extraSimulations;
        if (extraBindings != null)
        {
            for (var i = 0; i < extraBindings.Count; i++)
            {
                var binding = extraBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.simulationId))
                {
                    continue;
                }

                if (string.Equals(binding.simulationId, simulationId, StringComparison.OrdinalIgnoreCase) && binding.settings != null)
                {
                    return binding.settings;
                }
            }
        }

        return null;
    }

    private void ApplyVisualDefaults(string simulationId, SimVisualSettings visualSettings)
    {
        var preferredPack = visualSettings != null ? visualSettings.preferredAgentPack : null;
        preferredAgentPackBySim[simulationId] = preferredPack;
        SimVisualSettingsService.SetForSimulation(simulationId, visualSettings);
    }

    private void StartSimulation(string simulationId, bool initialRun)
    {
        simulationId = string.IsNullOrWhiteSpace(simulationId) ? GetFallbackSimulationId() : simulationId;
        SetScoreboardVisible(string.Equals(simulationId, "FantasySport", StringComparison.OrdinalIgnoreCase));
        if (options != null)
        {
            options.simulationId = simulationId;
        }

        simDriver?.SetRunner(null);
        replayDriver?.SetRunner(null);
        EnsureSimulationRoot();

        try
        {
            try
            {
                ShutdownCurrentRunner();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bootstrapper] ShutdownCurrentRunner failed: {ex}");
                activeRunner = null;
                activeRunnerObject = null;
            }

            Debug.Log("[Bootstrapper] Clearing SimulationRoot...");
            ClearSimulationRootChildren();
            SimulationSceneGraph.Rebuild(simulationRoot.transform);
            Debug.Log($"[Bootstrapper] SimulationRoot children after rebuild: {simulationRoot.transform.childCount}");
            Debug.Log("[Bootstrapper] SceneGraph ready.");

            var presetText = LoadPresetJson(simulationId, out currentPresetSource);
            var resolved = ConfigMerge.Merge(ConfigMerge.CreateBaseDefaults(), presetText);
            resolved.simulationId = simulationId;
            resolved.activeSimulation = simulationId;
            ApplyBootstrapOverrides(resolved);

            var simSettings = FindSimSettingsFor(simulationId, options);
            if (simSettings != null)
            {
                simSettings.ApplyTo(resolved);
            }

            var visualSettings = FindVisualSettingsFor(simulationId, options);
            ApplyVisualDefaults(simulationId, visualSettings);
            _ = GetArt(simulationId);

            resolved.seed = ResolveSeed(resolved, simSettings);
            resolved.NormalizeAliases();
            currentConfig = resolved;
            tickCount = 0;

            var resolvedTickDeltaTime = simSettings != null && simSettings.tickDeltaTime > 0f
                ? simSettings.tickDeltaTime
                : tickDeltaTime;
            simDriver?.SetTickDeltaTime(resolvedTickDeltaTime);
            replayDriver?.SetTickDeltaTime(resolvedTickDeltaTime);

            PresentationBoundsSync.ApplyFromConfig(currentConfig);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Clamp(currentConfig.rendering?.targetFps ?? 60, 30, 240);

            ConfigureDeterminism(currentConfig.seed);
            EventBusService.ResetGlobal();
            PrimitiveSpriteLibrary.ClearCache();
            AntAtlasLibrary.ClearCache();

            var selectedContentPack = ResolveContentPack(simulationId);
            Debug.Log($"[Bootstrapper] simId={simulationId} contentPack={DescribeContentPack(selectedContentPack)}");
            if (selectedContentPack != null)
            {
                ContentPackService.Set(selectedContentPack);
            }
            else
            {
                ContentPackService.Clear();
            }

            ArenaBuilder.Build(simulationRoot.transform, currentConfig);
            var runnerSpawned = SpawnRunner(currentConfig);
            SyncCameraBoundsAndFit();

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

                if (runnerSpawned)
                {
                    // Unified tick contract: all live simulation ticks must go through exactly one ITickableSimulationRunner.
                    var tickableRunner = RunnerContract.RequireTickable(activeRunnerObject, currentConfig.simulationId, "bootstrap pre-tick validation");
                    simDriver?.SetRunner(tickableRunner);
                }
                else
                {
                    simDriver?.SetRunner(null);
                }

                replayDriver?.SetRunner(null);
            }

            if (!initialRun)
            {
                Debug.Log($"Switched simulation to {simulationId} mode={currentConfig.mode} seed={currentConfig.seed}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Bootstrapper] StartSimulation FAILED simId={simulationId}\n{ex}");
            EnsureSimulationRoot();
            SimulationSceneGraph.Rebuild(simulationRoot.transform);
            simDriver?.SetRunner(null);
            replayDriver?.SetRunner(null);
            activeRunner = null;
            activeRunnerObject = null;

            var runnerRoot = simulationRoot.transform.Find("RunnerRoot");
            if (runnerRoot != null)
            {
                var go = new GameObject($"{simulationId}Runner_ERROR");
                go.transform.SetParent(runnerRoot, false);
            }
        }
    }

    private static void SetScoreboardVisible(bool visible)
    {
        var scoreboardObject = GameObject.Find("ScoreboardText");
        if (scoreboardObject != null && scoreboardObject.activeSelf != visible)
        {
            scoreboardObject.SetActive(visible);
        }
    }

    private static void SyncCameraBoundsAndFit()
    {
        var arenaBoundsObject = GameObject.Find("ArenaBounds");
        var arenaBoundsCollider = arenaBoundsObject != null ? arenaBoundsObject.GetComponent<Collider2D>() : null;
        var arenaCameraPolicy = UnityEngine.Object.FindAnyObjectByType<ArenaCameraPolicy>();

        if (arenaCameraPolicy != null)
        {
            if (arenaBoundsCollider != null)
            {
                arenaCameraPolicy.BindArenaBounds(arenaBoundsCollider, fitToBounds: true);
            }
            else
            {
                arenaCameraPolicy.FitToBounds();
            }
        }

        var followController = UnityEngine.Object.FindAnyObjectByType<CameraFollowController>();
        if (followController != null)
        {
            followController.arenaCameraPolicy = arenaCameraPolicy;
        }
    }


    private ContentPack ResolveContentPack(string simulationId)
    {
        var entry = options?.simulationCatalog?.FindById(simulationId);
        ContentPack basePack = null;
        if (entry != null && entry.defaultContentPack != null)
        {
            basePack = entry.defaultContentPack;
        }

        var catalog = options?.simulationCatalog;
        if (basePack == null && catalog != null && catalog.GlobalDefaultContentPack != null)
        {
            basePack = catalog.GlobalDefaultContentPack;
        }

        var settings = FindSimSettingsFor(simulationId, options);
        var policyOverridePack = ResolvePolicyContentPack(settings);
        var forceBasic = settings != null && settings.artPolicy.packSelectionMode == SimSettingsBase.PackSelectionMode.ForceBasic;

        var selectedOverride = contentPackOverride != null ? contentPackOverride : policyOverridePack;
        if (forceBasic)
        {
            selectedOverride = null;
        }

        if (selectedOverride == null)
        {
            DestroyRuntimeMergedPack();
            return basePack;
        }

        if (basePack == null)
        {
            DestroyRuntimeMergedPack();
            return selectedOverride;
        }

        return MergeContentPacks(basePack, selectedOverride);
    }

    private static ContentPack ResolvePolicyContentPack(SimSettingsBase settings)
    {
        if (settings == null)
        {
            return null;
        }

        return settings.artPolicy.packSelectionMode switch
        {
            SimSettingsBase.PackSelectionMode.ForcePack => settings.artPolicy.forcedPack,
            SimSettingsBase.PackSelectionMode.AutoBest => FindFirstPack(settings.artPolicy.preferredPacks),
            _ => null
        };
    }

    private static ContentPack FindFirstPack(List<ContentPack> preferredPacks)
    {
        if (preferredPacks == null)
        {
            return null;
        }

        for (var i = 0; i < preferredPacks.Count; i++)
        {
            if (preferredPacks[i] != null)
            {
                return preferredPacks[i];
            }
        }

        return null;
    }

    private ContentPack MergeContentPacks(ContentPack basePack, ContentPack overridePack)
    {
        DestroyRuntimeMergedPack();

        runtimeMergedPack = ScriptableObject.CreateInstance<ContentPack>();
        runtimeMergedPack.name = $"{basePack.name}+{overridePack.name}";

        var textureById = new Dictionary<string, ContentPack.TextureEntry>(StringComparer.Ordinal);
        foreach (var entry in basePack.Textures)
        {
            if (!string.IsNullOrWhiteSpace(entry.id))
            {
                textureById[entry.id] = entry;
            }
        }

        foreach (var entry in overridePack.Textures)
        {
            if (!string.IsNullOrWhiteSpace(entry.id))
            {
                textureById[entry.id] = entry;
            }
        }

        var spriteById = new Dictionary<string, ContentPack.SpriteEntry>(StringComparer.Ordinal);
        foreach (var entry in basePack.Sprites)
        {
            if (!string.IsNullOrWhiteSpace(entry.id))
            {
                spriteById[entry.id] = entry;
            }
        }

        foreach (var entry in overridePack.Sprites)
        {
            if (!string.IsNullOrWhiteSpace(entry.id))
            {
                spriteById[entry.id] = entry;
            }
        }

        var selectionByEntityId = new Dictionary<string, ContentPack.SpeciesSelection>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in basePack.Selections)
        {
            if (!string.IsNullOrWhiteSpace(selection.entityId))
            {
                selectionByEntityId[selection.entityId] = selection;
            }
        }

        foreach (var selection in overridePack.Selections)
        {
            if (!string.IsNullOrWhiteSpace(selection.entityId))
            {
                selectionByEntityId[selection.entityId] = selection;
            }
        }

        var clipByPrefix = new Dictionary<string, ContentPack.ClipMetadataEntry>(StringComparer.Ordinal);
        foreach (var clip in basePack.ClipMetadata)
        {
            if (!string.IsNullOrWhiteSpace(clip.keyPrefix))
            {
                clipByPrefix[clip.keyPrefix] = clip;
            }
        }

        foreach (var clip in overridePack.ClipMetadata)
        {
            if (!string.IsNullOrWhiteSpace(clip.keyPrefix))
            {
                clipByPrefix[clip.keyPrefix] = clip;
            }
        }

        runtimeMergedPack.SetEntries(new List<ContentPack.TextureEntry>(textureById.Values), new List<ContentPack.SpriteEntry>(spriteById.Values));
        runtimeMergedPack.SetSelections(new List<ContentPack.SpeciesSelection>(selectionByEntityId.Values));
        runtimeMergedPack.SetClipMetadata(new List<ContentPack.ClipMetadataEntry>(clipByPrefix.Values));

        return runtimeMergedPack;
    }

    private void DestroyRuntimeMergedPack()
    {
        if (runtimeMergedPack == null)
        {
            return;
        }

        Destroy(runtimeMergedPack);
        runtimeMergedPack = null;
    }

    private static string DescribeContentPack(ContentPack pack)
    {
        if (pack == null)
        {
            return "<none> path=<none>";
        }

#if UNITY_EDITOR
        var path = AssetDatabase.GetAssetPath(pack);
        return string.IsNullOrWhiteSpace(path) ? $"{pack.name} path=<unknown>" : $"{pack.name} path={path}";
#else
        return $"{pack.name} path=<runtime>";
#endif
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
    private static void EnsureSimSettingsAssets(BootstrapOptions bootstrapOptions)
    {
        if (bootstrapOptions == null)
        {
            return;
        }

        const string rootFolder = "Assets/_Bootstrap/SimSettings";
        if (!AssetDatabase.IsValidFolder("Assets/_Bootstrap"))
        {
            AssetDatabase.CreateFolder("Assets", "_Bootstrap");
        }

        if (!AssetDatabase.IsValidFolder(rootFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Bootstrap", "SimSettings");
        }

        bootstrapOptions.antColoniesSettings = EnsureSimSettingsAsset<AntColoniesSimSettings>(
            $"{rootFolder}/AntColoniesSimSettings.asset",
            bootstrapOptions.antColoniesSettings);
        bootstrapOptions.marbleRaceSettings = EnsureSimSettingsAsset<MarbleRaceSimSettings>(
            $"{rootFolder}/MarbleRaceSimSettings.asset",
            bootstrapOptions.marbleRaceSettings);
        bootstrapOptions.fantasySportSettings = EnsureSimSettingsAsset<FantasySportSimSettings>(
            $"{rootFolder}/FantasySportSimSettings.asset",
            bootstrapOptions.fantasySportSettings);
        bootstrapOptions.raceCarSettings = EnsureSimSettingsAsset<RaceCarSimSettings>(
            $"{rootFolder}/RaceCarSimSettings.asset",
            bootstrapOptions.raceCarSettings);

        bootstrapOptions.antColoniesVisual = EnsureSimVisualSettingsAsset(
            $"{rootFolder}/AntColoniesSimVisualSettings.asset",
            "AntColonies",
            BasicShapeKind.Capsule,
            bootstrapOptions.antColoniesVisual);
        bootstrapOptions.marbleRaceVisual = EnsureSimVisualSettingsAsset(
            $"{rootFolder}/MarbleRaceSimVisualSettings.asset",
            "MarbleRace",
            BasicShapeKind.Circle,
            bootstrapOptions.marbleRaceVisual);
        bootstrapOptions.fantasySportVisual = EnsureSimVisualSettingsAsset(
            $"{rootFolder}/FantasySportSimVisualSettings.asset",
            "FantasySport",
            BasicShapeKind.RoundedRect,
            bootstrapOptions.fantasySportVisual);
        bootstrapOptions.raceCarVisual = EnsureSimVisualSettingsAsset(
            $"{rootFolder}/RaceCarSimVisualSettings.asset",
            "RaceCar",
            BasicShapeKind.RoundedRect,
            bootstrapOptions.raceCarVisual);

        EditorUtility.SetDirty(bootstrapOptions);
        AssetDatabase.SaveAssets();
    }

    private static T EnsureSimSettingsAsset<T>(string assetPath, T current) where T : SimSettingsBase
    {
        if (current != null)
        {
            return current;
        }

        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, assetPath);
        return created;
    }

    private static SimVisualSettings EnsureSimVisualSettingsAsset(string assetPath, string simulationId, BasicShapeKind shape, SimVisualSettings current)
    {
        if (current != null)
        {
            if (!string.Equals(current.simulationId, simulationId, StringComparison.Ordinal))
            {
                current.simulationId = simulationId;
                EditorUtility.SetDirty(current);
            }

            return current;
        }

        var existing = AssetDatabase.LoadAssetAtPath<SimVisualSettings>(assetPath);
        if (existing != null)
        {
            if (!string.Equals(existing.simulationId, simulationId, StringComparison.Ordinal))
            {
                existing.simulationId = simulationId;
                EditorUtility.SetDirty(existing);
            }

            return existing;
        }

        var created = ScriptableObject.CreateInstance<SimVisualSettings>();
        created.simulationId = simulationId;
        created.agentShape = shape;
        created.usePrimitiveBaseline = true;
        created.agentOutline = true;
        created.agentSizePx = 64;
        created.defaultDebugMode = DebugPlaceholderMode.Overlay;
        AssetDatabase.CreateAsset(created, assetPath);
        return created;
    }

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
        EnsureSimSettingsAssets(resolvedOptions);
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

    private int ResolveSeed(ScenarioConfig config, SimSettingsBase settings)
    {
        if (string.Equals(config?.mode, "Replay", StringComparison.OrdinalIgnoreCase))
        {
            return config.seed;
        }

        var selectedPolicy = settings != null ? settings.seedPolicy : options != null ? options.seedPolicy : SeedPolicy.FromSystemTime;
        var selectedFixedSeed = settings != null ? settings.fixedSeed : options != null ? options.fixedSeed : Environment.TickCount;

        return selectedPolicy switch
        {
            SeedPolicy.Fixed => selectedFixedSeed,
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
        RngService.SetGlobalSeed(seed);
        Debug.Log($"Bootstrapper: Deterministic RNG signature {RngService.BuildSignature(seed)}");
    }

    private bool SpawnRunner(ScenarioConfig config)
    {
        var prefab = options?.simulationCatalog?.FindById(config.simulationId)?.runnerPrefab;
        prefab ??= SimulationRegistry.LoadRunnerPrefab(config.simulationId);
        var graph = SimulationSceneGraph.Ensure(simulationRoot.transform);
        var parent = graph != null && graph.RunnerRoot != null ? graph.RunnerRoot : simulationRoot.transform;
        GameObject runnerObject;

        if (prefab != null)
        {
            runnerObject = Instantiate(prefab, parent);
        }
        else
        {
            runnerObject = new GameObject($"{config.simulationId}RunnerPlaceholder");
            runnerObject.transform.SetParent(parent, false);
            Debug.LogWarning($"Bootstrapper: Missing prefab for {config.simulationId} at Resources/{SimulationRegistry.GetResourcePath(config.simulationId)}.prefab");
        }

        runnerObject.transform.SetParent(parent, false);
        runnerObject.name = $"{config.simulationId}Runner";

        activeRunnerObject = runnerObject;
        try
        {
            activeRunner = RunnerContract.RequireTickable(activeRunnerObject, config.simulationId, "runner instantiation");
            activeRunner.Initialize(config);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Bootstrapper] SpawnRunner failed for simId={config.simulationId}: {ex}");
            activeRunner = null;
            activeRunnerObject = null;
            var runnerError = new GameObject($"{config.simulationId}RunnerError");
            runnerError.transform.SetParent(parent, false);
            simDriver?.SetRunner(null);
            replayDriver?.SetRunner(null);
            return false;
        }
    }

    private void EnsureSimulationRoot()
    {
        if (!simulationRoot)
        {
            var existing = GameObject.Find(SimulationRootName);
            simulationRoot = existing != null ? existing : new GameObject(SimulationRootName);
        }
    }

    private void ClearSimulationRootChildren()
    {
        if (!simulationRoot)
        {
            return;
        }

        var rootTransform = simulationRoot.transform;
        var toDestroy = new System.Collections.Generic.List<GameObject>(rootTransform.childCount);
        for (var i = 0; i < rootTransform.childCount; i++)
        {
            Transform child = null;
            try
            {
                child = rootTransform.GetChild(i);
            }
            catch
            {
                continue;
            }

            if (!child)
            {
                continue;
            }

            var childObject = child.gameObject;
            if (!childObject)
            {
                continue;
            }

            toDestroy.Add(childObject);
        }

        for (var i = 0; i < toDestroy.Count; i++)
        {
            var go = toDestroy[i];
            if (go)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }

    private void ShutdownCurrentRunner()
    {
        SetScoreboardVisible(false);
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

    private void OnDestroy()
    {
        SimVisualSettingsService.Clear();
        DestroyRuntimeMergedPack();
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
