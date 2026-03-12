using UnityEngine;
using UnityEngine.Rendering;

[GspBootstrap(GspBootstrapKind.Simulation, "Reaction-diffusion simulation bootstrap")]
public sealed class ReactionDiffusionBootstrap : MonoBehaviour
{
    private const int ThreadGroupSize = 8;
    private const int MaxOverlayPulses = 8;

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
    [SerializeField, Min(0f)] private float feedDriftAmplitude = 0.0008f;
    [SerializeField, Min(0f)] private float killDriftAmplitude = 0.0005f;
    [SerializeField, Min(1f)] private float feedDriftPeriodSeconds = 40f;
    [SerializeField, Min(1f)] private float killDriftPeriodSeconds = 55f;
    [SerializeField] private float killDriftPhaseOffsetRadians = 1.7f;

    [Header("Micro Reseeding")]
    [SerializeField] private bool enableMicroReseeding = true;
    [SerializeField, Min(0f)] private float microReseedStartDelaySeconds = 4f;
    [SerializeField, Min(0.25f)] private float microReseedIntervalSeconds = 7f;
    [SerializeField, Range(1, 8)] private int microReseedCount = 2;
    [SerializeField, Range(0.002f, 0.08f)] private float microReseedRadius = 0.035f;
    [SerializeField, Range(0f, 1f)] private float microReseedStrength = 0.65f;
    [SerializeField, Range(0f, 0.45f)] private float microReseedBorderPadding = 0.08f;

    [Header("Color Presentation")]
    [SerializeField] private ReactionDiffusionPalettePreset palettePreset = ReactionDiffusionPalettePreset.Ember;
    [SerializeField] private ReactionDiffusionColorMode colorMode = ReactionDiffusionColorMode.TwoColor;
    [SerializeField, Min(0.1f)] private float overlayPulseLifetimeSeconds = 8f;
    [SerializeField, Min(0f)] private float overlayPulseGlow = 2.0f;

    [Header("Display")]
    [SerializeField] private ReactionDiffusionDisplayMode displayMode = ReactionDiffusionDisplayMode.ChemicalB;
    [SerializeField, Min(0.1f)] private float simulationScale = 1f;
    [SerializeField] private bool fitMainCameraToDisplay = true;
    [SerializeField, Min(0f)] private float cameraPadding = 0f;
    [SerializeField] private ComputeShader simulationShader;
    [SerializeField] private Shader displayShader;

    private RenderTexture stateA;
    private RenderTexture stateB;
    private RenderTexture displayTexture;
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

    private readonly Vector4[] pulseCenters = new Vector4[MaxOverlayPulses];
    private readonly float[] pulseAges = new float[MaxOverlayPulses];
    private readonly float[] pulseRadii = new float[MaxOverlayPulses];
    private readonly float[] pulseStrengths = new float[MaxOverlayPulses];
    private int pulseWriteIndex;

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
        feedDriftAmplitude = 0.0008f;
        killDriftAmplitude = 0.0005f;
        feedDriftPeriodSeconds = 40f;
        killDriftPeriodSeconds = 55f;
        killDriftPhaseOffsetRadians = 1.7f;

        enableMicroReseeding = true;
        microReseedStartDelaySeconds = 4f;
        microReseedIntervalSeconds = 7f;
        microReseedCount = 2;
        microReseedRadius = 0.035f;
        microReseedStrength = 0.65f;
        microReseedBorderPadding = 0.08f;

        palettePreset = ReactionDiffusionPalettePreset.Ember;
        colorMode = ReactionDiffusionColorMode.TwoColor;
        overlayPulseLifetimeSeconds = 8f;
        overlayPulseGlow = 2.0f;

        displayMode = ReactionDiffusionDisplayMode.ChemicalB;
        simulationScale = 1f;
        fitMainCameraToDisplay = true;
        cameraPadding = 0f;

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

        feedDriftAmplitude = Mathf.Max(0f, feedDriftAmplitude);
        killDriftAmplitude = Mathf.Max(0f, killDriftAmplitude);
        feedDriftPeriodSeconds = Mathf.Max(1f, feedDriftPeriodSeconds);
        killDriftPeriodSeconds = Mathf.Max(1f, killDriftPeriodSeconds);

        microReseedStartDelaySeconds = Mathf.Max(0f, microReseedStartDelaySeconds);
        microReseedIntervalSeconds = Mathf.Max(0.25f, microReseedIntervalSeconds);
        microReseedCount = Mathf.Clamp(microReseedCount, 1, 8);
        microReseedRadius = Mathf.Clamp(microReseedRadius, 0.002f, 0.08f);
        microReseedStrength = Mathf.Clamp01(microReseedStrength);
        microReseedBorderPadding = Mathf.Clamp(microReseedBorderPadding, 0f, 0.45f);

        overlayPulseLifetimeSeconds = Mathf.Max(0.1f, overlayPulseLifetimeSeconds);
        overlayPulseGlow = Mathf.Max(0f, overlayPulseGlow);

        if (preset != ReactionDiffusionPreset.Custom && preset != lastAppliedPreset)
        {
            ReactionDiffusionPresetCatalog.ApplyTo(preset, ref diffuseA, ref diffuseB, ref feed, ref kill, ref dt, ref stepsPerFrame);
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

        var read = useStateAAsRead ? stateA : stateB;
        var write = useStateAAsRead ? stateB : stateA;

        simulationShader.SetInt("_Width", gridWidth);
        simulationShader.SetInt("_Height", gridHeight);
        simulationShader.SetFloat("_DiffuseA", diffuseA);
        simulationShader.SetFloat("_DiffuseB", diffuseB);

        var timeSeconds = Time.timeSinceLevelLoad;
        var runtimeFeed = feed;
        var runtimeKill = kill;

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

        UpdatePulseAges(frameDt);

        if (displayMaterial != null)
        {
            ApplyPaletteToMaterial();
            displayMaterial.SetTexture("_StateTex", read);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
            displayMaterial.SetFloat("_OverlayPulseGlow", overlayPulseGlow);
            displayMaterial.SetFloat("_ColorMode", (float)colorMode);
            displayMaterial.SetVectorArray("_PulseCenters", pulseCenters);
            displayMaterial.SetFloatArray("_PulseAges", pulseAges);
            displayMaterial.SetFloatArray("_PulseRadii", pulseRadii);
            displayMaterial.SetFloatArray("_PulseStrengths", pulseStrengths);
        }
    }

    public void ShutdownSimulation()
    {
        running = false;
        initialized = false;

        ReleaseTexture(ref stateA);
        ReleaseTexture(ref stateB);
        ReleaseTexture(ref displayTexture);

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
        ReleaseTexture(ref displayTexture);

        stateA = CreateStateTexture(gridWidth, gridHeight, "ReactionDiffusionStateA");
        stateB = CreateStateTexture(gridWidth, gridHeight, "ReactionDiffusionStateB");
        displayTexture = CreateDisplayTexture(gridWidth, gridHeight, "ReactionDiffusionDisplay");
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

        ApplyPaletteToMaterial();
        displayMaterial.SetTexture("_StateTex", stateA);
        displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
        displayMaterial.SetFloat("_OverlayPulseGlow", overlayPulseGlow);
        displayMaterial.SetFloat("_ColorMode", (float)colorMode);
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
            ReactionDiffusionPresetCatalog.ApplyTo(preset, ref diffuseA, ref diffuseB, ref feed, ref kill, ref dt, ref stepsPerFrame);
            lastAppliedPreset = preset;
        }

        var activeSeed = useRandomSeed ? (randomSeed ^ System.Environment.TickCount) : randomSeed;
        randomSeed = activeSeed;

        simulationShader.SetInt("_Width", gridWidth);
        simulationShader.SetInt("_Height", gridHeight);
        simulationShader.SetInt("_SeedMode", (int)seedMode);
        simulationShader.SetInt("_Seed", activeSeed);
        simulationShader.SetTexture(initKernel, "_WriteState", stateA);
        simulationShader.SetTexture(initKernel, "_DisplayTex", displayTexture);

        var groupsX = Mathf.CeilToInt(gridWidth / (float)ThreadGroupSize);
        var groupsY = Mathf.CeilToInt(gridHeight / (float)ThreadGroupSize);
        simulationShader.Dispatch(initKernel, groupsX, groupsY, 1);

        Graphics.Blit(stateA, stateB);
        useStateAAsRead = true;

        nextMicroReseedTime = Time.timeSinceLevelLoad + microReseedStartDelaySeconds;
        microReseedIndex = 0;
        pulseWriteIndex = 0;

        for (var i = 0; i < MaxOverlayPulses; i++)
        {
            pulseCenters[i] = Vector4.zero;
            pulseAges[i] = 0f;
            pulseRadii[i] = 0f;
            pulseStrengths[i] = 0f;
        }

        if (displayMaterial != null)
        {
            ApplyPaletteToMaterial();
            displayMaterial.SetTexture("_StateTex", stateA);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
        }
    }

    private void PerformMicroReseed(int groupsX, int groupsY, ref RenderTexture read, ref RenderTexture write)
    {
        for (var i = 0; i < microReseedCount; i++)
        {
            var center = GetMicroReseedCenter(microReseedIndex++);
            simulationShader.SetTexture(injectKernel, "_ReadState", read);
            simulationShader.SetTexture(injectKernel, "_WriteState", write);
            simulationShader.SetVector("_InjectCenter", new Vector4(center.x, center.y, 0f, 0f));
            simulationShader.SetFloat("_InjectRadius", microReseedRadius);
            simulationShader.SetFloat("_InjectStrength", microReseedStrength);
            simulationShader.Dispatch(injectKernel, groupsX, groupsY, 1);

            AddOverlayPulse(center, microReseedRadius, microReseedStrength);

            var temp = read;
            read = write;
            write = temp;
            useStateAAsRead = !useStateAAsRead;
        }
    }

    private void AddOverlayPulse(Vector2 center, float radius, float strength)
    {
        pulseCenters[pulseWriteIndex] = new Vector4(center.x, center.y, 0f, 0f);
        pulseAges[pulseWriteIndex] = 1f;
        pulseRadii[pulseWriteIndex] = radius;
        pulseStrengths[pulseWriteIndex] = strength;
        pulseWriteIndex = (pulseWriteIndex + 1) % MaxOverlayPulses;
    }

    private void UpdatePulseAges(float frameDt)
    {
        var decay = frameDt / Mathf.Max(0.1f, overlayPulseLifetimeSeconds);
        for (var i = 0; i < MaxOverlayPulses; i++)
        {
            pulseAges[i] = Mathf.Max(0f, pulseAges[i] - decay);
        }
    }

    private void ApplyPaletteToMaterial()
    {
        if (displayMaterial == null)
        {
            return;
        }

        var palette = ReactionDiffusionPaletteCatalog.Get(palettePreset);
        displayMaterial.SetColor("_BaseColor", palette.baseColor);
        displayMaterial.SetColor("_ReseedColor", palette.reseedColor);
        displayMaterial.SetColor("_HotspotColor", palette.hotspotColor);
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

    private static RenderTexture CreateDisplayTexture(int width, int height, string textureName)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
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