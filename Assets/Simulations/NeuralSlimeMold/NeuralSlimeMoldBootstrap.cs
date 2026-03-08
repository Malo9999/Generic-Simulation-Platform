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

    [Header("Steering Tuning")]
    [SerializeField, Min(0f), Tooltip("Scales trail sensing influence (left/center/right) in steering.")] private float trailFollowWeight = 0.78f;
    [SerializeField, Min(0f), Tooltip("Overall multiplier for food-directed turning.")] private float foodAttractionWeight = 3.8f;
    [SerializeField, Min(0.1f), Tooltip("Additional food sensing distance beyond node radius.")] private float foodSenseRadius = 22f;
    [SerializeField, Min(0f), Tooltip("Extra food turn boost when heading is misaligned from food direction.")] private float foodTurnBias = 0.95f;
    [SerializeField, Min(0f), Tooltip("Scales heading jitter; >0 helps escape repetitive loops.")] private float turnNoise = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("Damps local circular self-reinforcement when side sensors dominate.")] private float localLoopSuppression = 0.58f;
    [SerializeField, Min(1f), Tooltip("Deposit multiplier when close to active food nodes.")] private float depositNearFoodMultiplier = 1.75f;
    [SerializeField, Range(0f, 1f), Tooltip("Biases trail persistence toward inter-node bridges and lowers local ring reinforcement.")] private float pathPersistenceBias = 0.58f;

    [Header("Activity Sustain")]
    [SerializeField] private bool foodPulseEnabled = true;
    [SerializeField, Min(0.5f)] private float foodPulsePeriod = 8f;
    [SerializeField, Min(0f)] private float foodPulseStrength = 0.35f;
    [SerializeField] private bool localTrailScrubEnabled = true;
    [SerializeField, Min(0.01f)] private float localTrailScrubThreshold = 0.78f;
    [SerializeField, Range(0f, 1f)] private float localTrailScrubAmount = 0.11f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;
    [SerializeField] private Color backgroundColor = new(0.01f, 0.02f, 0.04f, 1f);

    [Header("Food Influence Debug")]
    [SerializeField, Tooltip("Applies an obvious food-biased steering regime and higher-contrast food markers for quick verification.")] private bool foodInfluenceDebug = false;

    [Header("Boundary")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeMoldRunner.BoundaryMode boundaryMode = NeuralSlimeMoldRunner.BoundaryMode.SoftWall;
    [SerializeField, Min(0f), Tooltip("Requires Start / Reset Simulation to apply.")] private float wallMargin = 6f;

    [Header("World Layout")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeWorldPreset worldPreset = NeuralSlimeWorldPreset.CorridorCross;
    [SerializeField, Tooltip("When Custom, uses Manual Food Nodes/Obstacles arrays.")] private bool useCustomWorldOverrides = false;

    [Header("Food Nodes")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private bool enableFoodNodes = true;
    [SerializeField, Tooltip("Can be toggled during play.")] private bool indirectFoodBias = true;
    [SerializeField, Min(1), Tooltip("Requires Start / Reset Simulation to apply when auto-generated nodes are used.")] private int foodNodeCount = 4;
    [SerializeField, Min(0f), Tooltip("Can be adjusted live.")] private float foodStrength = 1.05f;
    [SerializeField, Min(0.1f), Tooltip("Requires Start / Reset Simulation to apply.")] private float foodRadius = 14f;
    [SerializeField, Tooltip("When enabled, food node placement is deterministic from seed.")] private bool spawnFromSeed = true;
    [SerializeField, Tooltip("Can be toggled during play. Disabled keeps nodes consumable-only.")] private bool allowFoodRegrowth = true;
    [SerializeField, Min(0f), Tooltip("Default capacity for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float foodCapacity = 110f;
    [SerializeField, Min(0f), Tooltip("Default depletion rate for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float depletionRate = 0.85f;
    [SerializeField, Min(0f), Tooltip("Default regrow rate for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float regrowRate = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("Minimum residual attraction for weakened/depleted food to preserve migration corridors.")] private float depletedFoodStrengthMultiplier = 0.18f;
    [SerializeField, Tooltip("Optional explicit node positions (legacy). Leave empty to auto-generate.")] private Vector2[] manualFoodNodes = null;
    [SerializeField, Tooltip("Optional explicit food configs (position/radius/strength/capacity/depletion/regrowth). Requires reset.")] private NeuralFoodNodeConfig[] manualFoodConfigs = null;

    [Header("Food Debug")]
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool showFoodGizmos = true;
    [SerializeField] private bool debugFoodLogging = true;
    [SerializeField] private NeuralFoodDebugPreset debugFoodPreset = NeuralFoodDebugPreset.Off;
    [SerializeField] private bool strongFoodDebugMode = false;
    [SerializeField, Min(1f)] private float strongFoodStrengthMultiplier = 6f;

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

        var liveFoodStrength = strongFoodDebugMode ? foodStrength * Mathf.Max(1f, strongFoodStrengthMultiplier) : foodStrength;
        var effectiveTrailFollowWeight = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? trailFollowWeight * 0.5f : trailFollowWeight;
        var effectiveFoodAttractionWeight = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? foodAttractionWeight * 2.1f : foodAttractionWeight;
        var effectiveFoodSenseRadius = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? foodSenseRadius * 1.45f : foodSenseRadius;
        var effectiveFoodTurnBias = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? foodTurnBias * 1.35f : foodTurnBias;
        var effectiveTurnNoise = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? turnNoise * 0.55f : turnNoise;
        var effectiveLoopSuppression = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? Mathf.Clamp01(localLoopSuppression + 0.22f) : localLoopSuppression;
        var effectiveDepositNearFoodMultiplier = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? depositNearFoodMultiplier * 1.4f : depositNearFoodMultiplier;

        runner.Tick(
            Time.deltaTime,
            trailDiffusion * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(pathPersistenceBias)),
            trailDecayPerSecond * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(pathPersistenceBias)),
            indirectFoodBias,
            liveFoodStrength,
            allowFoodRegrowth,
            effectiveTrailFollowWeight,
            effectiveFoodAttractionWeight,
            effectiveFoodSenseRadius,
            effectiveFoodTurnBias,
            depletedFoodStrengthMultiplier,
            effectiveTurnNoise,
            effectiveLoopSuppression,
            effectiveDepositNearFoodMultiplier,
            pathPersistenceBias,
            foodPulseEnabled,
            foodPulsePeriod,
            foodPulseStrength,
            localTrailScrubEnabled,
            localTrailScrubThreshold,
            localTrailScrubAmount);
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
            foodCapacity,
            depletionRate,
            regrowRate,
            spawnFromSeed,
            resolvedFoodNodes,
            resolvedFoodConfigs,
            enableObstacles ? resolvedObstacles : null,
            debugFoodLogging);

        ApplyRendererOverrides();
        rendererComponent.Build(runner);
        LogFoodDebugSummary();
        LogStartupSummary(enableObstacles ? resolvedObstacles.Length : 0);

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

        ApplyFoodDebugPreset(ref resolvedFoodNodes, ref resolvedFoodConfigs);

        if (debugFoodPreset != NeuralFoodDebugPreset.Off)
        {
            return;
        }

        if (useCustomWorldOverrides || worldPreset == NeuralSlimeWorldPreset.Custom)
        {
            return;
        }

        switch (worldPreset)
        {
            case NeuralSlimeWorldPreset.CorridorCross:
                // CorridorTest is an enum alias of CorridorCross (both value 1),
                // so it resolves here without needing a duplicate case label.
                resolvedFoodConfigs = BuildCorridorCrossFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildCorridorCrossObstacles();
                break;
            case NeuralSlimeWorldPreset.CorridorMazeLite:
                // IslandObstacles is an enum alias of CorridorMazeLite (both value 2).
            case NeuralSlimeWorldPreset.ClusteredFood:
                resolvedFoodConfigs = BuildCorridorMazeLiteFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildCorridorMazeLiteObstacles();
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

    private NeuralFoodNodeConfig[] BuildCorridorCrossFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-24f, 0f), 10f, 1f, 120f, 0.9f, 0.08f),
            CreateFood(new Vector2(24f, 0f), 10f, 1f, 120f, 0.9f, 0.08f),
            CreateFood(new Vector2(0f, 20f), 8f, 0.8f, 75f, 0.6f, 0.05f)
        };
    }

    private NeuralFoodNodeConfig[] BuildCorridorMazeLiteFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-25f, -20f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(26f, -20f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(-26f, 20f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(25f, 20f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(0f, 0f), 6.5f, 1.15f, 135f, 0.9f, 0.16f)
        };
    }

    private NeuralObstacle[] BuildCorridorCrossObstacles()
    {
        return new[]
        {
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(0f, 12f), size = new Vector2(64f, 14f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(0f, -12f), size = new Vector2(64f, 14f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(-12f, 0f), size = new Vector2(14f, 64f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(12f, 0f), size = new Vector2(14f, 64f), radius = 1f }
        };
    }

    private NeuralObstacle[] BuildCorridorMazeLiteObstacles()
    {
        return new[]
        {
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(-8f, 14f), size = new Vector2(34f, 8f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(12f, 6f), size = new Vector2(32f, 8f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(-12f, -2f), size = new Vector2(32f, 8f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(10f, -10f), size = new Vector2(30f, 8f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(-10f, -18f), size = new Vector2(30f, 8f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(-28f, 0f), size = new Vector2(8f, 36f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Rectangle, center = new Vector2(28f, 0f), size = new Vector2(8f, 36f), radius = 1f },
            new NeuralObstacle { shape = NeuralObstacleShape.Circle, center = new Vector2(0f, 0f), radius = 4.5f, size = Vector2.zero }
        };
    }

    private void ApplyFoodDebugPreset(ref Vector2[] resolvedFoodNodes, ref NeuralFoodNodeConfig[] resolvedFoodConfigs)
    {
        if (debugFoodPreset == NeuralFoodDebugPreset.Off)
        {
            return;
        }

        resolvedFoodNodes = null;
        switch (debugFoodPreset)
        {
            case NeuralFoodDebugPreset.Center3:
                resolvedFoodConfigs = new[]
                {
                    CreateFood(new Vector2(-6f, 0f), 9f, 1f, 120f, 0.4f, 0f),
                    CreateFood(new Vector2(0f, 0f), 9f, 1f, 120f, 0.4f, 0f),
                    CreateFood(new Vector2(6f, 0f), 9f, 1f, 120f, 0.4f, 0f)
                };
                break;
            case NeuralFoodDebugPreset.Corners4:
                var half = mapSize * 0.5f;
                var inset = new Vector2(Mathf.Max(6f, half.x * 0.45f), Mathf.Max(6f, half.y * 0.45f));
                resolvedFoodConfigs = new[]
                {
                    CreateFood(new Vector2(-inset.x, -inset.y), 8f, 1f, 120f, 0.4f, 0f),
                    CreateFood(new Vector2(-inset.x, inset.y), 8f, 1f, 120f, 0.4f, 0f),
                    CreateFood(new Vector2(inset.x, -inset.y), 8f, 1f, 120f, 0.4f, 0f),
                    CreateFood(new Vector2(inset.x, inset.y), 8f, 1f, 120f, 0.4f, 0f)
                };
                break;
            case NeuralFoodDebugPreset.FoodDominanceTest:
                resolvedFoodConfigs = new[]
                {
                    CreateFood(new Vector2(-14f, 0f), 12f, 1.2f, 150f, 0.5f, 0f),
                    CreateFood(new Vector2(14f, 0f), 12f, 1.2f, 150f, 0.5f, 0f),
                    CreateFood(new Vector2(0f, 14f), 10f, 1f, 130f, 0.45f, 0f)
                };
                break;
            case NeuralFoodDebugPreset.FoodInfluenceDebug:
                resolvedFoodConfigs = new[]
                {
                    CreateFood(new Vector2(-18f, -6f), 11f, 1.25f, 220f, 0.2f, 0f),
                    CreateFood(new Vector2(0f, 12f), 12f, 1.35f, 240f, 0.25f, 0f),
                    CreateFood(new Vector2(18f, -6f), 11f, 1.25f, 220f, 0.2f, 0f)
                };
                break;
        }
    }

    private void LogFoodDebugSummary()
    {
        if (!debugFoodLogging || runner == null)
        {
            return;
        }

        var info = runner.LastFoodSpawnInfo;
        Debug.Log($"[NeuralSlimeMold] food enabled={info.Enabled} requested={info.RequestedCount} spawned={info.SpawnedCount} rejected={info.RejectedCount} preset={debugFoodPreset} strongMode={strongFoodDebugMode}");
        var debugSteeringMode = foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug ? "FoodInfluenceDebug" : "Default";
        var logTrailFollowWeight = debugSteeringMode == "FoodInfluenceDebug" ? trailFollowWeight * 0.5f : trailFollowWeight;
        var logFoodAttractionWeight = debugSteeringMode == "FoodInfluenceDebug" ? foodAttractionWeight * 2.1f : foodAttractionWeight;
        var logFoodSenseRadius = debugSteeringMode == "FoodInfluenceDebug" ? foodSenseRadius * 1.45f : foodSenseRadius;
        var logTurnNoise = debugSteeringMode == "FoodInfluenceDebug" ? turnNoise * 0.55f : turnNoise;
        Debug.Log($"[NeuralSlimeMold] steering mode={debugSteeringMode} trailFollowWeight={logTrailFollowWeight:F2} foodAttractionWeight={logFoodAttractionWeight:F2} foodSenseRadius={logFoodSenseRadius:F2} foodTurnBias={foodTurnBias:F2} turnNoise={logTurnNoise:F2} localLoopSuppression={localLoopSuppression:F2} depositNearFoodMultiplier={depositNearFoodMultiplier:F2} pathPersistenceBias={pathPersistenceBias:F2}");
        Debug.Log($"[NeuralSlimeMold] visual backgroundColor={backgroundColor} clearMode=CameraSolidColor");
        Debug.Log($"[NeuralSlimeMold] lifecycle defaults capacity={foodCapacity:F2} depletionRate={depletionRate:F2} regrowRate={regrowRate:F2} allowRegrowth={allowFoodRegrowth} lifecycleLogs={debugFoodLogging}");

        var nodes = runner.FoodNodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            Debug.Log($"[NeuralSlimeMold] food[{i}] pos={node.position} radius={node.radius:F2} strength={node.strength:F2} active={node.active} cap={node.capacity:F2}/{node.maxCapacity:F2}");
        }
    }


    private void LogStartupSummary(int obstacleCount)
    {
        if (!debugFoodLogging)
        {
            return;
        }

        Debug.Log($"[NeuralSlimeMold] worldPreset={worldPreset} pulseEnabled={foodPulseEnabled} pulsePeriod={foodPulsePeriod:F2}s pulseStrength={foodPulseStrength:F2} localTrailScrubEnabled={localTrailScrubEnabled} scrubThreshold={localTrailScrubThreshold:F2} scrubAmount={localTrailScrubAmount:F2}");
        Debug.Log($"[NeuralSlimeMold] obstacles enabled={enableObstacles} obstacleCount={obstacleCount} boundary={boundaryMode} wallMargin={wallMargin:F2}");
        Debug.Log($"[NeuralSlimeMold] steering trailFollowWeight={trailFollowWeight:F2} foodAttractionWeight={foodAttractionWeight:F2} foodTurnBias={foodTurnBias:F2} turnNoise={turnNoise:F2} localLoopSuppression={localLoopSuppression:F2}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!showFoodGizmos)
        {
            return;
        }

        var nodes = ResolveGizmoFoodNodes();
        if (nodes == null || nodes.Length == 0)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.25f, 0.9f, 0.95f);
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            Gizmos.DrawWireSphere(new Vector3(node.position.x, node.position.y, 0f), Mathf.Max(0.2f, node.radius));
        }
    }

    private NeuralFoodNodeConfig[] ResolveGizmoFoodNodes()
    {
        BuildWorldLayout(out _, out var configs, out _);
        return configs;
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
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
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
        rendererComponent.SetFoodDebugVisuals(showFoodMarkers);
        rendererComponent.SetFoodInfluenceDebugVisuals(foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug);
        rendererComponent.SetBackgroundColor(backgroundColor);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }
    }
}
