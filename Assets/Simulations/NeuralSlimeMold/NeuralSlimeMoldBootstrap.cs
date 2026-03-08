using UnityEngine;

public sealed class NeuralSlimeMoldBootstrap : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int agentCount = 1800;
    [SerializeField] private Vector2 mapSize = new(64f, 64f);
    [SerializeField] private Vector2Int trailResolution = new(256, 256);
    [SerializeField, Min(0f)] private float trailDecayPerSecond = 0.6f;
    [SerializeField, Range(0f, 1f)] private float trailDiffusion = 0.23f;

    [Header("Agent Motion")]
    [SerializeField] private float sensorAngleDegrees = 35f;
    [SerializeField] private float sensorDistance = 1.8f;
    [SerializeField] private float speed = 6.7f;
    [SerializeField] private float turnRateDegrees = 180f;
    [SerializeField, Min(0f)] private float depositAmount = 1.2f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;

    [Header("Boundary")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeMoldRunner.BoundaryMode boundaryMode = NeuralSlimeMoldRunner.BoundaryMode.SoftWall;
    [SerializeField, Min(0f), Tooltip("Requires Start / Reset Simulation to apply.")] private float wallMargin = 6f;

    [Header("World Layout")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeWorldPreset worldPreset = NeuralSlimeWorldPreset.OpenField;
    [SerializeField, Tooltip("When Custom, uses Manual Food Nodes/Obstacles arrays.")] private bool useCustomWorldOverrides = false;

    [Header("Food Nodes")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private bool enableFoodNodes = true;
    [SerializeField, Tooltip("Can be toggled during play.")] private bool indirectFoodBias = true;
    [SerializeField, Min(1), Tooltip("Requires Start / Reset Simulation to apply when auto-generated nodes are used.")] private int foodNodeCount = 4;
    [SerializeField, Min(0f), Tooltip("Can be adjusted live.")] private float foodStrength = 0.9f;
    [SerializeField, Min(0.1f), Tooltip("Requires Start / Reset Simulation to apply.")] private float foodRadius = 14f;
    [SerializeField, Tooltip("When enabled, food node placement is deterministic from seed.")] private bool spawnFromSeed = true;
    [SerializeField, Tooltip("Can be toggled during play. Disabled keeps nodes consumable-only.")] private bool allowFoodRegrowth = true;
    [SerializeField, Tooltip("Optional explicit node positions (legacy). Leave empty to auto-generate.")] private Vector2[] manualFoodNodes = null;
    [SerializeField, Tooltip("Optional explicit food configs (position/radius/strength/capacity/depletion/regrowth). Requires reset.")] private NeuralFoodNodeConfig[] manualFoodConfigs = null;

    [Header("Obstacles")]
    [SerializeField, Tooltip("Can be toggled during play.")] private bool enableObstacles = true;
    [SerializeField, Tooltip("Custom obstacle overrides used when world preset is Custom.")] private NeuralObstacle[] manualObstacles = null;

    [Header("Camera")]
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField] private float cameraPadding = 1.1f;

    private NeuralSlimeMoldRunner runner;
    private NeuralSlimeMoldRenderer rendererComponent;
    private bool hasStarted;

    private void Awake()
    {
        runner = new NeuralSlimeMoldRunner();
        rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        if (rendererComponent == null)
        {
            rendererComponent = gameObject.AddComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
    }

    private void Start()
    {
        if (autoStart)
        {
            StartSimulation();
        }
    }

    private void Update()
    {
        if (!hasStarted)
        {
            return;
        }

        runner.Tick(Time.deltaTime, trailDiffusion, trailDecayPerSecond, indirectFoodBias, foodStrength, allowFoodRegrowth);
        rendererComponent.Render(runner);
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartSimulation()
    {
        BuildWorldLayout(out var resolvedFoodNodes, out var resolvedFoodConfigs, out var resolvedObstacles);

        var normalizedSeed = seed;
        var turnRateRadians = turnRateDegrees * Mathf.Deg2Rad;
        var sensorAngleRadians = sensorAngleDegrees * Mathf.Deg2Rad;

        runner.ResetWithSeed(
            normalizedSeed,
            agentCount,
            trailResolution,
            mapSize,
            speed,
            turnRateRadians,
            sensorAngleRadians,
            sensorDistance,
            depositAmount,
            boundaryMode,
            wallMargin,
            enableFoodNodes,
            foodNodeCount,
            foodRadius,
            spawnFromSeed,
            resolvedFoodNodes,
            resolvedFoodConfigs,
            enableObstacles ? resolvedObstacles : null);

        ApplyRendererOverrides();
        rendererComponent.Build(runner);

        if (autoFrameCamera)
        {
            FrameCamera();
        }

        hasStarted = true;
    }

    [ContextMenu("Reseed")]
    public void Reseed()
    {
        seed = StableHashUtility.CombineSeed(seed, "neural-slime-next");
        StartSimulation();
    }

    private void BuildWorldLayout(out Vector2[] resolvedFoodNodes, out NeuralFoodNodeConfig[] resolvedFoodConfigs, out NeuralObstacle[] resolvedObstacles)
    {
        resolvedFoodNodes = manualFoodNodes;
        resolvedFoodConfigs = manualFoodConfigs;
        resolvedObstacles = manualObstacles;

        if (useCustomWorldOverrides || worldPreset == NeuralSlimeWorldPreset.Custom)
        {
            return;
        }

        switch (worldPreset)
        {
            case NeuralSlimeWorldPreset.CorridorTest:
                resolvedFoodConfigs = BuildCorridorFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildCorridorObstacles();
                break;
            case NeuralSlimeWorldPreset.IslandObstacles:
                resolvedFoodConfigs = BuildIslandFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildIslandObstacles();
                break;
            case NeuralSlimeWorldPreset.ClusteredFood:
                resolvedFoodConfigs = BuildClusteredFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildClusterObstacles();
                break;
            default:
                resolvedFoodConfigs = BuildOpenFood();
                resolvedFoodNodes = null;
                resolvedObstacles = System.Array.Empty<NeuralObstacle>();
                break;
        }
    }

    private NeuralFoodNodeConfig[] BuildOpenFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-18f, 12f), 12f, 1f, 120f, 0.85f, 0.2f),
            CreateFood(new Vector2(19f, 14f), 10f, 1f, 95f, 0.75f, 0.15f),
            CreateFood(new Vector2(-20f, -10f), 11f, 1f, 105f, 0.8f, 0.18f),
            CreateFood(new Vector2(17f, -16f), 13f, 1f, 115f, 0.9f, 0.12f)
        };
    }

    private NeuralFoodNodeConfig[] BuildCorridorFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-24f, 0f), 10f, 1f, 120f, 0.9f, 0.08f),
            CreateFood(new Vector2(24f, 0f), 10f, 1f, 120f, 0.9f, 0.08f),
            CreateFood(new Vector2(0f, 20f), 8f, 0.8f, 75f, 0.6f, 0.05f)
        };
    }

    private NeuralFoodNodeConfig[] BuildIslandFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-20f, 18f), 9f, 0.9f, 90f, 0.7f, 0.09f),
            CreateFood(new Vector2(21f, 17f), 9f, 0.9f, 90f, 0.7f, 0.09f),
            CreateFood(new Vector2(0f, -20f), 11f, 1f, 130f, 1f, 0.1f)
        };
    }

    private NeuralFoodNodeConfig[] BuildClusteredFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-10f, -8f), 8f, 1.1f, 100f, 1.2f, 0f),
            CreateFood(new Vector2(-4f, -3f), 7f, 1f, 100f, 1.1f, 0f),
            CreateFood(new Vector2(-12f, 4f), 7f, 1f, 95f, 1f, 0f),
            CreateFood(new Vector2(16f, 16f), 10f, 0.7f, 80f, 0.55f, 0.25f)
        };
    }

    private NeuralObstacle[] BuildCorridorObstacles()
    {
        return new[]
        {
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(0f, 16f), size = new Vector2(42f, 10f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(0f, -16f), size = new Vector2(42f, 10f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(0f, 0f), radius = 5.5f, size = Vector2.zero }
        };
    }

    private NeuralObstacle[] BuildIslandObstacles()
    {
        return new[]
        {
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(-4f, 4f), radius = 8f, size = Vector2.zero },
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(10f, 2f), radius = 6f, size = Vector2.zero },
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(2f, -10f), radius = 7f, size = Vector2.zero }
        };
    }

    private NeuralObstacle[] BuildClusterObstacles()
    {
        return new[]
        {
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(6f, -2f), radius = 4f, size = Vector2.zero },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(14f, 10f), size = new Vector2(9f, 6f), radius = 1f }
        };
    }

    private static NeuralFoodNodeConfig CreateFood(Vector2 position, float radius, float strength, float capacity, float depletionRate, float regrowRate)
    {
        return new NeuralFoodNodeConfig
        {
            position = position,
            radius = radius,
            strength = strength,
            capacity = capacity,
            depletionRate = depletionRate,
            regrowRate = regrowRate,
            startActive = true
        };
    }

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.orthographic = true;
        var halfW = mapSize.x * 0.5f;
        var halfH = mapSize.y * 0.5f;
        var sizeFromHeight = halfH;
        var sizeFromWidth = halfW / Mathf.Max(0.01f, cam.aspect);
        cam.orthographicSize = Mathf.Max(sizeFromHeight, sizeFromWidth, 6f) * Mathf.Max(1f, cameraPadding);
        var pos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 0f, pos.z);

        var arenaCameraPolicy = MainCameraRuntimeSetup.EnsureArenaCameraRig(cam);
        if (arenaCameraPolicy != null)
        {
            arenaCameraPolicy.SetWorldSizeAndRefresh(mapSize);
        }

        var followController = cam.GetComponent<CameraFollowController>();
        if (followController != null)
        {
            followController.enabled = false;
        }
    }

    private void ApplyRendererOverrides()
    {
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.SetShapeToggles(useGlowAgentShape, useFieldBlobOverlay);
    }
}
