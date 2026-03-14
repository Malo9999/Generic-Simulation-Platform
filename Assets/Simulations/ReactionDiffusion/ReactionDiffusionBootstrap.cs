using UnityEngine;
using UnityEngine.Rendering;

public enum ReactionDiffusionArchetype
{
    None = 0,
    Mask = 1,
    Jellyfish = 2,
    Butterfly = 3,
    Flower = 4,
    Skull = 5
}

[GspBootstrap(GspBootstrapKind.Simulation, "Reaction-diffusion simulation bootstrap")]
public sealed class ReactionDiffusionBootstrap : MonoBehaviour
{
    private const int ThreadGroupSize = 8;

    [Header("Startup")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private ReactionDiffusionPreset preset = ReactionDiffusionPreset.Chaos;

    [Header("Simulation")]
    [SerializeField, Min(16)] private int gridWidth = 960;
    [SerializeField, Min(16)] private int gridHeight = 540;
    [SerializeField, Min(0f)] private float diffuseA = 1.0f;
    [SerializeField, Min(0f)] private float diffuseB = 0.5f;
    [SerializeField, Min(0f)] private float feed = 0.0420f;
    [SerializeField, Min(0f)] private float kill = 0.0600f;
    [SerializeField, Min(0.0001f)] private float dt = 1f;
    [SerializeField, Min(1)] private int stepsPerFrame = 1;
    [SerializeField] private bool wrapEdges = true;

    [Header("Seeding")]
    [SerializeField] private ReactionDiffusionSeedMode seedMode = ReactionDiffusionSeedMode.RandomPatches;
    [SerializeField] private int randomSeed = 1337;
    [SerializeField] private bool useRandomSeed = true;

    [Header("Parameter Drift")]
    [SerializeField] private bool enableParameterDrift = true;
    [SerializeField, Min(0f)] private float feedDriftAmplitude = 0.00020f;
    [SerializeField, Min(0f)] private float killDriftAmplitude = 0.00012f;
    [SerializeField, Min(1f)] private float feedDriftPeriodSeconds = 42f;
    [SerializeField, Min(1f)] private float killDriftPeriodSeconds = 58f;
    [SerializeField] private float killDriftPhaseOffsetRadians = 1.7f;

    [Header("Regime Morph")]
    [SerializeField] private bool enableRegimeMorph = false;
    [SerializeField] private ReactionDiffusionPreset morphPresetA = ReactionDiffusionPreset.Chaos;
    [SerializeField] private ReactionDiffusionPreset morphPresetB = ReactionDiffusionPreset.Mazes;
    [SerializeField, Min(2f)] private float morphCycleSeconds = 120f;
    [SerializeField, Range(0f, 1f)] private float morphStrength = 0.08f;
    [SerializeField, Min(0f)] private float maxMorphFeedDelta = 0.0008f;
    [SerializeField, Min(0f)] private float maxMorphKillDelta = 0.0005f;

    [Header("Archetype Director")]
    [SerializeField] private bool enableArchetypeDirector = true;
    [SerializeField] private bool useArchetypeSeed = true;
    [SerializeField] private ReactionDiffusionArchetype startArchetype = ReactionDiffusionArchetype.Mask;
    [SerializeField] private bool randomizeSequence = true;
    [SerializeField, Min(1f)] private float archetypeHoldTime = 25f;
    [SerializeField, Min(1f)] private float archetypeTransitionTime = 35f;
    [SerializeField, Range(0f, 1f)] private float archetypeStrength = 0.75f;
    [SerializeField, Range(0.10f, 1.20f)] private float archetypeScale = 0.42f;
    [SerializeField, Range(0f, 0.35f)] private float archetypeJitter = 0.08f;
    [SerializeField, Range(-180f, 180f)] private float archetypeRotationDegrees = 0f;
    [SerializeField] private bool randomizeArchetypePerRun = false;

    [Header("Local Events (Colony Seed)")]
    [SerializeField] private bool enableMicroReseeding = true;
    [SerializeField, Min(0f)] private float microReseedStartDelaySeconds = 6f;
    [SerializeField, Min(0.25f)] private float microReseedIntervalSeconds = 9f;
    [SerializeField, Range(1, 8)] private int microReseedCount = 1;
    [SerializeField, Range(0.002f, 0.08f)] private float microReseedRadius = 0.024f;
    [SerializeField, Range(0f, 1f)] private float microReseedStrength = 0.33f;
    [SerializeField, Range(0f, 0.45f)] private float microReseedBorderPadding = 0.08f;
    [SerializeField, Range(0f, 1f)] private float microReseedRadiusJitter = 0.25f;
    [SerializeField, Range(0f, 1f)] private float microReseedStrengthJitter = 0.20f;

    [Header("Display")]
    [SerializeField] private ReactionDiffusionDisplayMode displayMode = ReactionDiffusionDisplayMode.ChemicalB;
    [SerializeField, Min(0.1f)] private float simulationScale = 1f;
    [SerializeField] private bool fitMainCameraToDisplay = true;
    [SerializeField, Min(0f)] private float cameraPadding = 0f;
    [SerializeField, Min(0f)] private float activityGain = 12f;
    [SerializeField] private ComputeShader simulationShader;
    [SerializeField] private Shader displayShader;

    private RenderTexture stateA;
    private RenderTexture stateB;
    private RenderTexture previousState;
    private Material displayMaterial;
    private Renderer displayRenderer;

    private int initKernel = -1;
    private int stepKernel = -1;
    private int injectKernel = -1;

    private bool initialized;
    private bool running;
    private bool externallyDriven;
    private bool useStateAAsRead = true;
    private int allocatedWidth;
    private int allocatedHeight;
    private ReactionDiffusionPreset lastAppliedPreset = (ReactionDiffusionPreset)(-1);

    private float nextMicroReseedTime;
    private int microReseedIndex;
    private uint eventHashSeed = 1337u;

    private ReactionDiffusionArchetype currentArchetype;
    private ReactionDiffusionArchetype nextArchetype;
    private float archetypeTimer;
    private float archetypeBlend;
    private bool archetypeTransitioning;

    private void Reset()
    {
        ApplyRecommendedLivingDefaults();
    }

    [ContextMenu("Apply Recommended Living Defaults")]
    public void ApplyRecommendedLivingDefaults()
    {
        preset = ReactionDiffusionPreset.Chaos;

        gridWidth = 960;
        gridHeight = 540;
        diffuseA = 1.0f;
        diffuseB = 0.5f;
        feed = 0.0420f;
        kill = 0.0600f;
        dt = 1f;
        stepsPerFrame = 1;
        wrapEdges = true;

        seedMode = ReactionDiffusionSeedMode.RandomPatches;
        randomSeed = 1337;
        useRandomSeed = true;

        enableParameterDrift = true;
        feedDriftAmplitude = 0.00020f;
        killDriftAmplitude = 0.00012f;
        feedDriftPeriodSeconds = 42f;
        killDriftPeriodSeconds = 58f;
        killDriftPhaseOffsetRadians = 1.7f;

        enableRegimeMorph = false;
        morphPresetA = ReactionDiffusionPreset.Chaos;
        morphPresetB = ReactionDiffusionPreset.Mazes;
        morphCycleSeconds = 120f;
        morphStrength = 0.08f;
        maxMorphFeedDelta = 0.0008f;
        maxMorphKillDelta = 0.0005f;

        enableArchetypeDirector = true;
        useArchetypeSeed = true;
        startArchetype = ReactionDiffusionArchetype.Mask;
        randomizeSequence = true;
        archetypeHoldTime = 25f;
        archetypeTransitionTime = 35f;
        archetypeStrength = 0.75f;
        archetypeScale = 0.42f;
        archetypeJitter = 0.08f;
        archetypeRotationDegrees = 0f;
        randomizeArchetypePerRun = false;

        enableMicroReseeding = true;
        microReseedStartDelaySeconds = 6f;
        microReseedIntervalSeconds = 9f;
        microReseedCount = 1;
        microReseedRadius = 0.024f;
        microReseedStrength = 0.33f;
        microReseedBorderPadding = 0.08f;
        microReseedRadiusJitter = 0.25f;
        microReseedStrengthJitter = 0.20f;

        displayMode = ReactionDiffusionDisplayMode.ChemicalB;
        simulationScale = 1f;
        fitMainCameraToDisplay = true;
        cameraPadding = 0f;
        activityGain = 12f;

        lastAppliedPreset = preset;
        OnValidate();
    }

    private void OnValidate()
    {
        gridWidth = Mathf.Max(16, gridWidth);
        gridHeight = Mathf.Max(16, gridHeight);
        dt = Mathf.Max(0.0001f, dt);
        stepsPerFrame = Mathf.Max(1, stepsPerFrame);
        simulationScale = Mathf.Max(0.1f, simulationScale);
        cameraPadding = Mathf.Max(0f, cameraPadding);
        activityGain = Mathf.Max(0f, activityGain);

        feedDriftAmplitude = Mathf.Max(0f, feedDriftAmplitude);
        killDriftAmplitude = Mathf.Max(0f, killDriftAmplitude);
        feedDriftPeriodSeconds = Mathf.Max(1f, feedDriftPeriodSeconds);
        killDriftPeriodSeconds = Mathf.Max(1f, killDriftPeriodSeconds);

        morphCycleSeconds = Mathf.Max(2f, morphCycleSeconds);
        morphStrength = Mathf.Clamp01(morphStrength);
        maxMorphFeedDelta = Mathf.Max(0f, maxMorphFeedDelta);
        maxMorphKillDelta = Mathf.Max(0f, maxMorphKillDelta);

        archetypeHoldTime = Mathf.Max(1f, archetypeHoldTime);
        archetypeTransitionTime = Mathf.Max(1f, archetypeTransitionTime);
        archetypeStrength = Mathf.Clamp01(archetypeStrength);
        archetypeScale = Mathf.Clamp(archetypeScale, 0.10f, 1.20f);
        archetypeJitter = Mathf.Clamp(archetypeJitter, 0f, 0.35f);
        archetypeRotationDegrees = Mathf.Clamp(archetypeRotationDegrees, -180f, 180f);

        microReseedStartDelaySeconds = Mathf.Max(0f, microReseedStartDelaySeconds);
        microReseedIntervalSeconds = Mathf.Max(0.25f, microReseedIntervalSeconds);
        microReseedCount = Mathf.Clamp(microReseedCount, 1, 8);
        microReseedRadius = Mathf.Clamp(microReseedRadius, 0.002f, 0.08f);
        microReseedStrength = Mathf.Clamp01(microReseedStrength);
        microReseedBorderPadding = Mathf.Clamp(microReseedBorderPadding, 0f, 0.45f);
        microReseedRadiusJitter = Mathf.Clamp01(microReseedRadiusJitter);
        microReseedStrengthJitter = Mathf.Clamp01(microReseedStrengthJitter);

        if (preset != ReactionDiffusionPreset.Custom && preset != lastAppliedPreset)
        {
            ReactionDiffusionPresetCatalog.ApplyTo(
                preset,
                ref diffuseA,
                ref diffuseB,
                ref feed,
                ref kill,
                ref dt,
                ref stepsPerFrame);
            lastAppliedPreset = preset;
        }

        if (preset == ReactionDiffusionPreset.Custom)
        {
            lastAppliedPreset = preset;
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            StartOrResetSimulation();
        }
    }

    private void Update()
    {
        if (externallyDriven)
        {
            return;
        }

        Tick(Time.deltaTime);
    }

    public void SetExternallyDriven(bool value)
    {
        externallyDriven = value;
    }

    public void Tick(float frameDt)
    {
        if (!running || !initialized)
        {
            return;
        }

        UpdateArchetypeDirector(frameDt);

        var read = useStateAAsRead ? stateA : stateB;
        var write = useStateAAsRead ? stateB : stateA;

        Graphics.Blit(read, previousState);

        simulationShader.SetInt("_Width", gridWidth);
        simulationShader.SetInt("_Height", gridHeight);
        simulationShader.SetFloat("_DiffuseA", diffuseA);
        simulationShader.SetFloat("_DiffuseB", diffuseB);

        var timeSeconds = Time.timeSinceLevelLoad;
        var runtimeFeed = feed;
        var runtimeKill = kill;

        if (enableRegimeMorph)
        {
            SampleMorphParameters(timeSeconds, out var morphFeed, out var morphKill);

            morphFeed = ClampAround(feed, morphFeed, maxMorphFeedDelta);
            morphKill = ClampAround(kill, morphKill, maxMorphKillDelta);

            runtimeFeed = Mathf.Lerp(feed, morphFeed, morphStrength);
            runtimeKill = Mathf.Lerp(kill, morphKill, morphStrength);
        }

        if (enableParameterDrift)
        {
            var feedOmega = (Mathf.PI * 2f) / Mathf.Max(1f, feedDriftPeriodSeconds);
            var killOmega = (Mathf.PI * 2f) / Mathf.Max(1f, killDriftPeriodSeconds);

            runtimeFeed += Mathf.Sin(timeSeconds * feedOmega) * feedDriftAmplitude;
            runtimeKill += Mathf.Sin(timeSeconds * killOmega + killDriftPhaseOffsetRadians) * killDriftAmplitude;
        }

        simulationShader.SetFloat("_Feed", Mathf.Max(0f, runtimeFeed));
        simulationShader.SetFloat("_Kill", Mathf.Max(0f, runtimeKill));
        simulationShader.SetFloat("_Dt", dt);
        simulationShader.SetInt("_WrapEdges", wrapEdges ? 1 : 0);

        var groupsX = Mathf.CeilToInt(gridWidth / (float)ThreadGroupSize);
        var groupsY = Mathf.CeilToInt(gridHeight / (float)ThreadGroupSize);

        for (var i = 0; i < stepsPerFrame; i++)
        {
            simulationShader.SetTexture(stepKernel, "_ReadState", read);
            simulationShader.SetTexture(stepKernel, "_WriteState", write);
            simulationShader.Dispatch(stepKernel, groupsX, groupsY, 1);

            var temp = read;
            read = write;
            write = temp;
            useStateAAsRead = !useStateAAsRead;
        }

        if (enableMicroReseeding && timeSeconds >= nextMicroReseedTime)
        {
            PerformMicroReseed(groupsX, groupsY, ref read, ref write);
            nextMicroReseedTime = timeSeconds + microReseedIntervalSeconds;
        }

        if (displayMaterial != null)
        {
            displayMaterial.SetTexture("_StateTex", read);
            displayMaterial.SetTexture("_PrevStateTex", previousState);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
            displayMaterial.SetFloat("_ActivityGain", activityGain);
        }
    }

    private void UpdateArchetypeDirector(float deltaTime)
    {
        if (!enableArchetypeDirector)
        {
            archetypeBlend = 0f;
            archetypeTransitioning = false;
            return;
        }

        archetypeTimer += deltaTime;

        if (!archetypeTransitioning)
        {
            if (archetypeTimer >= archetypeHoldTime)
            {
                archetypeTransitioning = true;
                archetypeTimer = 0f;
            }

            archetypeBlend = 0f;
            return;
        }

        archetypeBlend = Mathf.Clamp01(archetypeTimer / archetypeTransitionTime);

        if (archetypeBlend >= 1f)
        {
            currentArchetype = nextArchetype;
            nextArchetype = PickNextArchetype(currentArchetype);
            archetypeTransitioning = false;
            archetypeTimer = 0f;
            archetypeBlend = 0f;
        }
    }

    private static float ClampAround(float center, float value, float delta)
    {
        return Mathf.Clamp(value, center - delta, center + delta);
    }

    private void SampleMorphParameters(float timeSeconds, out float outFeed, out float outKill)
    {
        var a = GetPresetParameters(morphPresetA);
        var b = GetPresetParameters(morphPresetB);

        var phase = Mathf.Sin((timeSeconds / morphCycleSeconds) * Mathf.PI * 2f) * 0.5f + 0.5f;
        phase = Mathf.SmoothStep(0f, 1f, phase);

        outFeed = Mathf.Lerp(a.feed, b.feed, phase);
        outKill = Mathf.Lerp(a.kill, b.kill, phase);
    }

    private static ReactionDiffusionPresetCatalog.Parameters GetPresetParameters(ReactionDiffusionPreset preset)
    {
        if (ReactionDiffusionPresetCatalog.TryGet(preset, out var parameters))
        {
            return parameters;
        }

        return new ReactionDiffusionPresetCatalog.Parameters(1.0f, 0.5f, 0.0420f, 0.0600f, 1f, 1);
    }

    public void ShutdownSimulation()
    {
        running = false;
        initialized = false;

        ReleaseTexture(ref stateA);
        ReleaseTexture(ref stateB);
        ReleaseTexture(ref previousState);

        if (displayMaterial != null)
        {
            Destroy(displayMaterial);
            displayMaterial = null;
        }

        if (displayRenderer != null)
        {
            var target = displayRenderer.gameObject;
            displayRenderer = null;
            Destroy(target);
        }
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartOrResetSimulation()
    {
        if (!EnsureResources())
        {
            return;
        }

        if (!initialized || allocatedWidth != gridWidth || allocatedHeight != gridHeight)
        {
            AllocateTextures();
            BuildDisplaySurface();
            initialized = true;
        }

        ResetSimulationState();
        running = true;
    }

    [ContextMenu("Reseed")]
    public void Reseed()
    {
        if (!initialized)
        {
            StartOrResetSimulation();
            return;
        }

        ResetSimulationState();
    }

    private bool EnsureResources()
    {
        if (simulationShader == null)
        {
            simulationShader = Resources.Load<ComputeShader>("Simulations/ReactionDiffusion/Shaders/ReactionDiffusion");
        }

        if (displayShader == null)
        {
            displayShader = Shader.Find("GSP/ReactionDiffusion/Display");
        }

        if (simulationShader == null || displayShader == null)
        {
            UnityEngine.Debug.LogError("[ReactionDiffusion] Missing compute shader or display shader reference.");
            return false;
        }

        if (initKernel < 0)
        {
            initKernel = simulationShader.FindKernel("Init");
        }

        if (stepKernel < 0)
        {
            stepKernel = simulationShader.FindKernel("Step");
        }

        if (injectKernel < 0)
        {
            injectKernel = simulationShader.FindKernel("Inject");
        }

        return true;
    }

    private void AllocateTextures()
    {
        ReleaseTexture(ref stateA);
        ReleaseTexture(ref stateB);
        ReleaseTexture(ref previousState);

        stateA = CreateStateTexture(gridWidth, gridHeight, "ReactionDiffusionStateA");
        stateB = CreateStateTexture(gridWidth, gridHeight, "ReactionDiffusionStateB");
        previousState = CreateStateTexture(gridWidth, gridHeight, "ReactionDiffusionPreviousState");
        useStateAAsRead = true;
        allocatedWidth = gridWidth;
        allocatedHeight = gridHeight;
    }

    private void BuildDisplaySurface()
    {
        if (displayRenderer != null)
        {
            Destroy(displayRenderer.gameObject);
            displayRenderer = null;
        }

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ReactionDiffusionDisplay";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.identity;

        var cam = Camera.main;
        var targetAspect = cam != null ? cam.aspect : (16f / 9f);

        var height = simulationScale;
        var width = height * targetAspect;
        quad.transform.localScale = new Vector3(width, height, 1f);

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        displayRenderer = quad.GetComponent<MeshRenderer>();
        displayMaterial = new Material(displayShader)
        {
            name = "ReactionDiffusionDisplayRuntime"
        };
        displayMaterial.SetTexture("_StateTex", stateA);
        displayMaterial.SetTexture("_PrevStateTex", previousState);
        displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
        displayMaterial.SetFloat("_ActivityGain", activityGain);
        displayRenderer.sharedMaterial = displayMaterial;

        if (fitMainCameraToDisplay)
        {
            FitMainCameraToQuad(height);
        }
    }

    private void FitMainCameraToQuad(float quadHeight)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        cam.orthographicSize = (quadHeight * 0.5f) + cameraPadding;

        var pos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 0f, pos.z);
    }

    private void ResetSimulationState()
    {
        if (!initialized)
        {
            return;
        }

        if (preset != ReactionDiffusionPreset.Custom)
        {
            ReactionDiffusionPresetCatalog.ApplyTo(
                preset,
                ref diffuseA,
                ref diffuseB,
                ref feed,
                ref kill,
                ref dt,
                ref stepsPerFrame);
            lastAppliedPreset = preset;
        }

        var activeSeed = useRandomSeed ? (randomSeed ^ System.Environment.TickCount) : randomSeed;
        randomSeed = activeSeed;
        eventHashSeed = (uint)activeSeed;

        currentArchetype = ResolveStartArchetype();
        nextArchetype = PickNextArchetype(currentArchetype);
        archetypeTimer = 0f;
        archetypeBlend = 0f;
        archetypeTransitioning = false;

        simulationShader.SetInt("_Width", gridWidth);
        simulationShader.SetInt("_Height", gridHeight);
        simulationShader.SetInt("_SeedMode", (int)seedMode);
        simulationShader.SetInt("_Seed", activeSeed);
        simulationShader.SetInt("_UseArchetypeSeed", useArchetypeSeed && enableArchetypeDirector ? 1 : 0);
        simulationShader.SetInt("_CurrentArchetype", (int)currentArchetype);
        simulationShader.SetInt("_NextArchetype", (int)nextArchetype);
        simulationShader.SetFloat("_ArchetypeBlend", 0f);
        simulationShader.SetFloat("_ArchetypeStrength", archetypeStrength);
        simulationShader.SetFloat("_ArchetypeScale", archetypeScale);
        simulationShader.SetFloat("_ArchetypeJitter", archetypeJitter);
        simulationShader.SetFloat("_ArchetypeRotationDegrees", archetypeRotationDegrees);
        simulationShader.SetTexture(initKernel, "_WriteState", stateA);

        var groupsX = Mathf.CeilToInt(gridWidth / (float)ThreadGroupSize);
        var groupsY = Mathf.CeilToInt(gridHeight / (float)ThreadGroupSize);
        simulationShader.Dispatch(initKernel, groupsX, groupsY, 1);

        Graphics.Blit(stateA, stateB);
        Graphics.Blit(stateA, previousState);
        useStateAAsRead = true;

        nextMicroReseedTime = Time.timeSinceLevelLoad + microReseedStartDelaySeconds;
        microReseedIndex = 0;

        if (displayMaterial != null)
        {
            displayMaterial.SetTexture("_StateTex", stateA);
            displayMaterial.SetTexture("_PrevStateTex", previousState);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
            displayMaterial.SetFloat("_ActivityGain", activityGain);
        }
    }

    private ReactionDiffusionArchetype ResolveStartArchetype()
    {
        if (!enableArchetypeDirector)
        {
            return ReactionDiffusionArchetype.None;
        }

        if (!randomizeArchetypePerRun)
        {
            return startArchetype;
        }

        return PickNextArchetype(ReactionDiffusionArchetype.None);
    }

    private ReactionDiffusionArchetype PickNextArchetype(ReactionDiffusionArchetype current)
    {
        if (!randomizeSequence)
        {
            return startArchetype == current ? ReactionDiffusionArchetype.Jellyfish : startArchetype;
        }

        ReactionDiffusionArchetype[] pool =
        {
            ReactionDiffusionArchetype.Mask,
            ReactionDiffusionArchetype.Jellyfish,
            ReactionDiffusionArchetype.Butterfly,
            ReactionDiffusionArchetype.Flower,
            ReactionDiffusionArchetype.Skull
        };

        var attempts = 0;
        var candidate = current;

        while (attempts < 16)
        {
            candidate = pool[Mathf.Abs((int)(SignedHash01(microReseedIndex + attempts + 101) * 100000f)) % pool.Length];
            if (candidate != current)
            {
                return candidate;
            }

            attempts++;
        }

        return pool[0];
    }

    private void PerformMicroReseed(int groupsX, int groupsY, ref RenderTexture read, ref RenderTexture write)
    {
        for (var i = 0; i < microReseedCount; i++)
        {
            var center = GetMicroReseedCenter(microReseedIndex++);
            var radius = microReseedRadius * (1f + SignedHash01(microReseedIndex * 13) * microReseedRadiusJitter);
            var strength = microReseedStrength * (1f + SignedHash01(microReseedIndex * 29) * microReseedStrengthJitter);

            radius = Mathf.Clamp(radius, 0.002f, 0.08f);
            strength = Mathf.Clamp01(strength);

            simulationShader.SetTexture(injectKernel, "_ReadState", read);
            simulationShader.SetTexture(injectKernel, "_WriteState", write);
            simulationShader.SetVector("_InjectCenter", new Vector4(center.x, center.y, 0f, 0f));
            simulationShader.SetFloat("_InjectRadius", radius);
            simulationShader.SetFloat("_InjectStrength", strength);
            simulationShader.SetInt("_CurrentArchetype", (int)currentArchetype);
            simulationShader.SetInt("_NextArchetype", (int)nextArchetype);
            simulationShader.SetFloat("_ArchetypeBlend", archetypeBlend);
            simulationShader.SetFloat("_ArchetypeStrength", archetypeStrength);
            simulationShader.SetFloat("_ArchetypeScale", archetypeScale);
            simulationShader.SetFloat("_ArchetypeJitter", archetypeJitter);
            simulationShader.SetFloat("_ArchetypeRotationDegrees", archetypeRotationDegrees);
            simulationShader.Dispatch(injectKernel, groupsX, groupsY, 1);

            var temp = read;
            read = write;
            write = temp;
            useStateAAsRead = !useStateAAsRead;
        }
    }

    private float SignedHash01(int salt)
    {
        uint x = eventHashSeed ^ (uint)(salt * 747796405);
        x ^= x >> 16;
        x *= 2246822519u;
        x ^= x >> 13;
        x *= 3266489917u;
        x ^= x >> 16;
        return ((x & 0x00ffffffu) / 16777215f) * 2f - 1f;
    }

    private Vector2 GetMicroReseedCenter(int index)
    {
        var gx = Frac(0.173 + index * 0.61803398875);
        var gy = Frac(0.619 + index * 0.38196601125);

        var min = microReseedBorderPadding;
        var max = 1f - microReseedBorderPadding;

        return new Vector2(
            Mathf.Lerp(min, max, gx),
            Mathf.Lerp(min, max, gy));
    }

    private static float Frac(double value)
    {
        return (float)(value - System.Math.Floor(value));
    }

    private static RenderTexture CreateStateTexture(int width, int height, string textureName)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
        {
            name = textureName,
            enableRandomWrite = true,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        texture.Create();
        return texture;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        Destroy(texture);
        texture = null;
    }

    private void OnDestroy()
    {
        ShutdownSimulation();
    }
}
