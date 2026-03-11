using UnityEngine;
using UnityEngine.Rendering;

[GspBootstrap(GspBootstrapKind.Simulation, "Reaction-diffusion simulation bootstrap")]
public sealed class ReactionDiffusionBootstrap : MonoBehaviour
{
    private const int ThreadGroupSize = 8;

    [Header("Startup")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private ReactionDiffusionPreset preset = ReactionDiffusionPreset.Default;

    [Header("Simulation")]
    [SerializeField, Min(16)] private int gridWidth = 256;
    [SerializeField, Min(16)] private int gridHeight = 256;
    [SerializeField, Min(0f)] private float diffuseA = 1.0f;
    [SerializeField, Min(0f)] private float diffuseB = 0.5f;
    [SerializeField, Min(0f)] private float feed = 0.0367f;
    [SerializeField, Min(0f)] private float kill = 0.0649f;
    [SerializeField, Min(0.0001f)] private float dt = 1f;
    [SerializeField, Min(1)] private int stepsPerFrame = 6;
    [SerializeField] private bool wrapEdges = true;

    [Header("Seeding")]
    [SerializeField] private ReactionDiffusionSeedMode seedMode = ReactionDiffusionSeedMode.CenterSquare;
    [SerializeField] private int randomSeed = 1337;
    [SerializeField] private bool useRandomSeed;

    [Header("Display")]
    [SerializeField] private ReactionDiffusionDisplayMode displayMode = ReactionDiffusionDisplayMode.ChemicalB;
    [SerializeField, Min(0.1f)] private float simulationScale = 18f;
    [SerializeField] private ComputeShader simulationShader;
    [SerializeField] private Shader displayShader;

    private RenderTexture stateA;
    private RenderTexture stateB;
    private RenderTexture displayTexture;
    private Material displayMaterial;
    private Renderer displayRenderer;

    private int initKernel = -1;
    private int stepKernel = -1;
    private bool initialized;
    private bool running;
    private bool externallyDriven;
    private bool useStateAAsRead = true;
    private int allocatedWidth;
    private int allocatedHeight;
    private ReactionDiffusionPreset lastAppliedPreset = (ReactionDiffusionPreset)(-1);

    private void OnValidate()
    {
        gridWidth = Mathf.Max(16, gridWidth);
        gridHeight = Mathf.Max(16, gridHeight);
        dt = Mathf.Max(0.0001f, dt);
        stepsPerFrame = Mathf.Max(1, stepsPerFrame);
        simulationScale = Mathf.Max(0.1f, simulationScale);

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
        simulationShader.SetFloat("_Feed", feed);
        simulationShader.SetFloat("_Kill", kill);
        simulationShader.SetFloat("_Dt", dt * Mathf.Max(0.0001f, frameDt) * 60f);
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

        if (displayMaterial != null)
        {
            displayMaterial.SetTexture("_StateTex", read);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
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
            Debug.LogError("[ReactionDiffusion] Missing compute shader or display shader reference.");
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
        quad.transform.localPosition = new Vector3(0f, 0f, 0f);
        quad.transform.localRotation = Quaternion.identity;

        var aspect = gridWidth / (float)gridHeight;
        var width = simulationScale;
        var height = width / Mathf.Max(0.01f, aspect);
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
        displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
        displayRenderer.sharedMaterial = displayMaterial;
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

        if (displayMaterial != null)
        {
            displayMaterial.SetTexture("_StateTex", stateA);
            displayMaterial.SetFloat("_DisplayMode", (float)displayMode);
        }
    }

    private static RenderTexture CreateStateTexture(int width, int height, string textureName)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
        {
            name = textureName,
            enableRandomWrite = true,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
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
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
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
