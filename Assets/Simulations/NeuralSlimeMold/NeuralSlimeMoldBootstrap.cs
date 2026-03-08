using UnityEngine;
using System.Collections.Generic;

public sealed class NeuralSlimeMoldBootstrap : MonoBehaviour
{
    public enum SimulationMode
    {
        Classic = 0,
        Experimental = 1
    }

    [Header("Simulation")]
    [SerializeField] private SimulationMode simulationMode = SimulationMode.Classic;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int agentCount = 1800;
    [SerializeField] private Vector2 mapSize = new(64f, 64f);
    [SerializeField] private Vector2Int trailResolution = new(256, 256);
    [SerializeField, Min(0f)] private float trailDecayPerSecond = 0.38f;
    [SerializeField, Range(0f, 1f)] private float trailDiffusion = 0.23f;

    [Header("Agent Motion")]
    [SerializeField] private float sensorAngleDegrees = 35f;
    [SerializeField] private float sensorDistance = 1.8f;
    [SerializeField] private float speed = 6.7f;
    [SerializeField] private float turnRateDegrees = 180f;
    [SerializeField, Min(0f)] private float depositAmount = 1.2f;

    [Header("Steering Tuning (Experimental)")]
    [SerializeField, Min(0f), Tooltip("Scales trail sensing influence (left/center/right) in steering.")] private float trailFollowWeight = 0.66f;
    [SerializeField, Min(0f), Tooltip("Overall multiplier for food-directed turning.")] private float foodAttractionWeight = 4.45f;
    [SerializeField, Min(0.1f), Tooltip("Additional food sensing distance beyond node radius.")] private float foodSenseRadius = 22f;
    [SerializeField, Min(0f), Tooltip("Extra food turn boost when heading is misaligned from food direction.")] private float foodTurnBias = 0.95f;
    [SerializeField, Min(0f), Tooltip("Scales heading jitter; >0 helps escape repetitive loops.")] private float turnNoise = 0.42f;
    [SerializeField, Range(0f, 1f), Tooltip("Damps local circular self-reinforcement when side sensors dominate.")] private float localLoopSuppression = 0.64f;
    [SerializeField, Min(1f), Tooltip("Deposit multiplier when close to active food nodes.")] private float depositNearFoodMultiplier = 1.75f;
    [SerializeField, Range(0f, 1f), Tooltip("Biases trail persistence toward inter-node bridges and lowers local ring reinforcement.")] private float pathPersistenceBias = 0.42f;

    [Header("Activity Sustain (Experimental)")]
    [SerializeField] private bool foodPulseEnabled = false;
    [SerializeField, Min(0.5f)] private float foodPulsePeriod = 8f;
    [SerializeField, Min(0f)] private float foodPulseStrength = 0.35f;
    [SerializeField] private bool localTrailScrubEnabled = false;
    [SerializeField, Min(0.01f)] private float localTrailScrubThreshold = 0.78f;
    [SerializeField, Range(0f, 1f)] private float localTrailScrubAmount = 0.11f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;
    [SerializeField] private Color backgroundColor = new(0.01f, 0.02f, 0.04f, 1f);

    [Header("Food Influence Debug (Experimental)")]
    [SerializeField, Tooltip("Applies an obvious food-biased steering regime and higher-contrast food markers for quick verification.")] private bool foodInfluenceDebug = false;

    [Header("Boundary (Experimental)")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeMoldRunner.BoundaryMode boundaryMode = NeuralSlimeMoldRunner.BoundaryMode.SoftWall;
    [SerializeField, Min(0f), Tooltip("Requires Start / Reset Simulation to apply.")] private float wallMargin = 6f;

    [Header("World Layout (Experimental)")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeWorldPreset worldPreset = NeuralSlimeWorldPreset.FoodDecayMigration;
    [SerializeField, Tooltip("When Custom, uses Manual Food Nodes/Obstacles arrays.")] private bool useCustomWorldOverrides = false;

    [Header("Food Nodes")]
    [SerializeField, Tooltip("Classic: toggles simple static food nodes.")] private bool enableStaticFood = true;
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private bool enableFoodNodes = true;
    [SerializeField, Tooltip("Can be toggled during play.")] private bool indirectFoodBias = true;
    [SerializeField, Min(1), Tooltip("Requires Start / Reset Simulation to apply when auto-generated nodes are used.")] private int foodNodeCount = 4;
    [SerializeField, Min(0f), Tooltip("Can be adjusted live.")] private float foodStrength = 1.2f;
    [SerializeField, Min(0.1f), Tooltip("Requires Start / Reset Simulation to apply.")] private float foodRadius = 14f;
    [SerializeField, Tooltip("When enabled, food node placement is deterministic from seed.")] private bool spawnFromSeed = true;
    [SerializeField, Tooltip("Can be toggled during play. Disabled keeps nodes consumable-only.")] private bool allowFoodRegrowth = false;
    [SerializeField, Min(0f), Tooltip("Default capacity for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float foodCapacity = 62f;
    [SerializeField, Min(0f), Tooltip("Default depletion rate for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float depletionRate = 1.9f;
    [SerializeField, Min(0f), Tooltip("Default regrow rate for generated/legacy food nodes. Requires Start / Reset Simulation to apply.")] private float regrowRate = 0.03f;
    [SerializeField, Range(0f, 1f), Tooltip("Minimum residual attraction for weakened/depleted food to preserve migration corridors.")] private float depletedFoodStrengthMultiplier = 0.03f;
    [SerializeField, Min(0f), Tooltip("Seconds before depleted food can reactivate.")] private float foodReactivationDelay = 12f;
    [SerializeField, Range(0f, 1f), Tooltip("Capacity fraction required to reactivate after delay.")] private float foodReactivationThreshold = 0.35f;
    [SerializeField, Min(0f), Tooltip("Extra deterministic steering pressure to break stable local rings over time.")] private float migrationRestlessness = 0.2f;
    [SerializeField, Tooltip("Optional explicit node positions (legacy). Leave empty to auto-generate.")] private Vector2[] manualFoodNodes = null;
    [SerializeField, Tooltip("Optional explicit food configs (position/radius/strength/capacity/depletion/regrowth). Requires reset.")] private NeuralFoodNodeConfig[] manualFoodConfigs = null;

    [Header("Food Debug (Experimental)")]
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool showFoodGizmos = true;
    [SerializeField] private bool debugFoodLogging = true;
    [SerializeField] private NeuralFoodDebugPreset debugFoodPreset = NeuralFoodDebugPreset.Off;
    [SerializeField] private bool strongFoodDebugMode = false;
    [SerializeField, Min(1f)] private float strongFoodStrengthMultiplier = 6f;

    [Header("Obstacles (Experimental)")]
    [SerializeField, Tooltip("Can be toggled during play.")] private bool enableObstacles = false;
    [SerializeField, Tooltip("Custom obstacle overrides used when world preset is Custom.")] private NeuralObstacle[] manualObstacles = null;
    [SerializeField, Min(1f), Tooltip("Base thickness used by generated corridor blockers.")] private float obstacleThickness = 6f;
    [SerializeField, Min(2f), Tooltip("Gap size left inside generated corridor barriers.")] private float corridorGapSize = 12f;
    [SerializeField, Range(0.08f, 0.45f), Tooltip("Approximate blocker span relative to map size for generated presets.")] private float obstacleCoverage = 0.2f;
    [SerializeField, Range(0.5f, 1.5f), Tooltip("Scales generated preset dimensions inward/outward from map center.")] private float presetScale = 1f;
    [SerializeField, Range(2, 6), Tooltip("How many small blockers to use in CorridorMazeLite.")] private int smallBlockerCount = 3;

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

        var experimental = simulationMode == SimulationMode.Experimental;
        var debugInfluence = experimental && (foodInfluenceDebug || debugFoodPreset == NeuralFoodDebugPreset.FoodInfluenceDebug);

        var liveFoodStrength = experimental && strongFoodDebugMode
            ? foodStrength * Mathf.Max(1f, strongFoodStrengthMultiplier)
            : foodStrength;
        var effectiveTrailFollowWeight = debugInfluence ? trailFollowWeight * 0.5f : trailFollowWeight;
        var effectiveFoodAttractionWeight = debugInfluence ? foodAttractionWeight * 2.1f : foodAttractionWeight;
        var effectiveFoodSenseRadius = debugInfluence ? foodSenseRadius * 1.45f : foodSenseRadius;
        var effectiveFoodTurnBias = debugInfluence ? foodTurnBias * 1.35f : foodTurnBias;
        var effectiveTurnNoise = debugInfluence ? turnNoise * 0.55f : turnNoise;
        var effectiveLoopSuppression = debugInfluence ? Mathf.Clamp01(localLoopSuppression + 0.22f) : localLoopSuppression;
        var effectiveDepositNearFoodMultiplier = debugInfluence ? depositNearFoodMultiplier * 1.4f : depositNearFoodMultiplier;

        var tickDiffusion = experimental
            ? trailDiffusion * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(pathPersistenceBias))
            : trailDiffusion;
        var tickDecay = experimental
            ? trailDecayPerSecond * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(pathPersistenceBias))
            : trailDecayPerSecond;

        runner.Tick(
            Time.deltaTime,
            tickDiffusion,
            tickDecay,
            enableStaticFood,
            liveFoodStrength,
            experimental && allowFoodRegrowth,
            experimental ? effectiveTrailFollowWeight : 1f,
            experimental ? effectiveFoodAttractionWeight : 1f,
            experimental ? effectiveFoodSenseRadius : Mathf.Max(foodRadius, sensorDistance * 4f),
            experimental ? effectiveFoodTurnBias : 0f,
            experimental ? depletedFoodStrengthMultiplier : 0f,
            experimental ? foodReactivationDelay : 0f,
            experimental ? foodReactivationThreshold : 0f,
            experimental ? migrationRestlessness : 0f,
            experimental ? effectiveTurnNoise : 0f,
            experimental ? effectiveLoopSuppression : 0f,
            experimental ? effectiveDepositNearFoodMultiplier : 1f,
            experimental ? pathPersistenceBias : 0f,
            experimental && foodPulseEnabled,
            foodPulsePeriod,
            foodPulseStrength,
            experimental && localTrailScrubEnabled,
            localTrailScrubThreshold,
            localTrailScrubAmount);
        rendererComponent.Render(runner);
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartSimulation()
    {
        var experimental = simulationMode == SimulationMode.Experimental;
        BuildWorldLayout(experimental, out var resolvedFoodNodes, out var resolvedFoodConfigs, out var resolvedObstacles);

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
            experimental ? boundaryMode : NeuralSlimeMoldRunner.BoundaryMode.SoftWall,
            experimental ? wallMargin : Mathf.Max(4f, wallMargin),
            enableStaticFood && enableFoodNodes,
            foodNodeCount,
            foodRadius,
            foodCapacity,
            depletionRate,
            regrowRate,
            spawnFromSeed,
            resolvedFoodNodes,
            resolvedFoodConfigs,
            experimental && enableObstacles ? resolvedObstacles : null,
            experimental && debugFoodLogging);

        ApplyRendererOverrides();
        rendererComponent.Build(runner);
        LogFoodDebugSummary();
        LogStartupSummary(experimental && enableObstacles ? resolvedObstacles.Length : 0);

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

    private void BuildWorldLayout(bool experimental, out Vector2[] resolvedFoodNodes, out NeuralFoodNodeConfig[] resolvedFoodConfigs, out NeuralObstacle[] resolvedObstacles)
    {
        if (!experimental)
        {
            resolvedFoodNodes = null;
            resolvedFoodConfigs = BuildClassicFood();
            resolvedObstacles = System.Array.Empty<NeuralObstacle>();
            return;
        }

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
                resolvedFoodConfigs = BuildCorridorCrossFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildCorridorCrossObstacles();
                break;
            case NeuralSlimeWorldPreset.CorridorMazeLite:
            case NeuralSlimeWorldPreset.ClusteredFood:
                resolvedFoodConfigs = BuildCorridorMazeLiteFood();
                resolvedFoodNodes = null;
                resolvedObstacles = BuildCorridorMazeLiteObstacles();
                break;
            case NeuralSlimeWorldPreset.FoodDecayMigration:
                resolvedFoodConfigs = BuildFoodDecayMigrationFood();
                resolvedFoodNodes = null;
                resolvedObstacles = System.Array.Empty<NeuralObstacle>();
                break;
            default:
                resolvedFoodConfigs = BuildOpenFood();
                resolvedFoodNodes = null;
                resolvedObstacles = System.Array.Empty<NeuralObstacle>();
                break;
        }
    }


    private NeuralFoodNodeConfig[] BuildClassicFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-18f, 12f), 10f, 1f, 1f, 0f, 0f),
            CreateFood(new Vector2(19f, 14f), 10f, 1f, 1f, 0f, 0f),
            CreateFood(new Vector2(-20f, -10f), 10f, 1f, 1f, 0f, 0f),
            CreateFood(new Vector2(17f, -16f), 10f, 1f, 1f, 0f, 0f)
        };
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


    private NeuralFoodNodeConfig[] BuildFoodDecayMigrationFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-22f, -10f), 11f, 1.3f, 52f, 2.15f, 0.02f),
            CreateFood(new Vector2(-2f, 15f), 10f, 1.05f, 70f, 1.35f, 0.02f),
            CreateFood(new Vector2(19f, 11f), 11f, 1.2f, 84f, 1.1f, 0.02f),
            CreateFood(new Vector2(18f, -15f), 10f, 1.15f, 76f, 1.2f, 0.02f)
        };
    }

    private NeuralFoodNodeConfig[] BuildCorridorCrossFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-20f, 16f), 9f, 1f, 120f, 0.85f, 0.1f),
            CreateFood(new Vector2(20f, 16f), 9f, 1f, 120f, 0.85f, 0.1f),
            CreateFood(new Vector2(-20f, -16f), 9f, 1f, 120f, 0.85f, 0.1f),
            CreateFood(new Vector2(20f, -16f), 9f, 1f, 120f, 0.85f, 0.1f)
        };
    }

    private NeuralFoodNodeConfig[] BuildCorridorMazeLiteFood()
    {
        return new[]
        {
            CreateFood(new Vector2(-22f, -14f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(22f, -14f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(-22f, 14f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(22f, 14f), 7f, 1f, 120f, 0.82f, 0.1f),
            CreateFood(new Vector2(0f, 0f), 6f, 1.1f, 130f, 0.86f, 0.14f)
        };
    }

    private NeuralObstacle[] BuildCorridorCrossObstacles()
    {
        var blockers = new List<NeuralObstacle>(4);
        var scale = Mathf.Clamp(presetScale, 0.5f, 1.5f);
        var thickness = Mathf.Max(1f, obstacleThickness * scale);
        var gap = Mathf.Max(thickness * 1.2f, corridorGapSize * scale);
        var coverage = Mathf.Clamp(obstacleCoverage, 0.08f, 0.45f);
        var horizontalSpan = Mathf.Max(thickness * 2f, mapSize.x * coverage * scale);
        var verticalSpan = Mathf.Max(thickness * 2f, mapSize.y * coverage * scale);

        var hOffset = (gap + horizontalSpan) * 0.5f;
        blockers.Add(CreateRectObstacle(new Vector2(-hOffset, 0f), new Vector2(horizontalSpan, thickness)));
        blockers.Add(CreateRectObstacle(new Vector2(hOffset, 0f), new Vector2(horizontalSpan, thickness)));

        var vOffset = (gap + verticalSpan) * 0.5f;
        blockers.Add(CreateRectObstacle(new Vector2(0f, -vOffset), new Vector2(thickness, verticalSpan)));
        blockers.Add(CreateRectObstacle(new Vector2(0f, vOffset), new Vector2(thickness, verticalSpan)));

        return blockers.ToArray();
    }

    private NeuralObstacle[] BuildCorridorMazeLiteObstacles()
    {
        var blockers = new List<NeuralObstacle>(6);
        var scale = Mathf.Clamp(presetScale, 0.5f, 1.5f);
        var thickness = Mathf.Max(1f, obstacleThickness * 0.9f * scale);
        var gap = Mathf.Max(4f, corridorGapSize * scale);
        var coverage = Mathf.Clamp(obstacleCoverage, 0.08f, 0.45f);
        var reachX = mapSize.x * 0.5f * coverage * scale;
        var reachY = mapSize.y * 0.5f * coverage * scale;

        // Three primary chokepoints with large open regions around them.
        blockers.Add(CreateRectObstacle(new Vector2(-gap * 0.55f, reachY), new Vector2(mapSize.x * 0.34f * scale, thickness)));
        blockers.Add(CreateRectObstacle(new Vector2(gap * 0.55f, -reachY), new Vector2(mapSize.x * 0.34f * scale, thickness)));
        blockers.Add(CreateRectObstacle(new Vector2(0f, 0f), new Vector2(thickness, mapSize.y * Mathf.Max(0.16f, coverage * 0.85f) * scale)));

        var optionalSmallBlockers = new[]
        {
            CreateRectObstacle(new Vector2(-reachX, 0f), new Vector2(thickness, mapSize.y * 0.2f * scale)),
            CreateRectObstacle(new Vector2(reachX, 0f), new Vector2(thickness, mapSize.y * 0.2f * scale)),
            CreateRectObstacle(new Vector2(0f, reachY * 1.35f), new Vector2(mapSize.x * 0.18f * scale, thickness))
        };

        var count = Mathf.Clamp(smallBlockerCount, 2, 6);
        for (var i = 0; i < optionalSmallBlockers.Length && blockers.Count < count; i++)
        {
            blockers.Add(optionalSmallBlockers[i]);
        }

        return blockers.ToArray();
    }

    private static NeuralObstacle CreateRectObstacle(Vector2 center, Vector2 size)
    {
        return new NeuralObstacle
        {
            shape = NeuralObstacleShape.Rectangle,
            center = center,
            size = size,
            radius = 1f
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
            case NeuralFoodDebugPreset.FoodDecayMigration:
                resolvedFoodConfigs = new[]
                {
                    CreateFood(new Vector2(-20f, -12f), 10f, 1.15f, 56f, 1.9f, 0.045f),
                    CreateFood(new Vector2(-4f, 14f), 10f, 1.2f, 62f, 1.75f, 0.05f),
                    CreateFood(new Vector2(16f, 10f), 10f, 1.1f, 58f, 1.85f, 0.04f),
                    CreateFood(new Vector2(18f, -14f), 10f, 1.2f, 60f, 1.8f, 0.05f)
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
        Debug.Log($"[NeuralSlimeMold] steering mode={debugSteeringMode} trailFollowWeight={logTrailFollowWeight:F2} foodAttractionWeight={logFoodAttractionWeight:F2} foodSenseRadius={logFoodSenseRadius:F2} foodTurnBias={foodTurnBias:F2} turnNoise={logTurnNoise:F2} localLoopSuppression={localLoopSuppression:F2} migrationRestlessness={migrationRestlessness:F2} depositNearFoodMultiplier={depositNearFoodMultiplier:F2} pathPersistenceBias={pathPersistenceBias:F2}");
        Debug.Log($"[NeuralSlimeMold] visual backgroundColor={backgroundColor} clearMode=CameraSolidColor");
        Debug.Log($"[NeuralSlimeMold] lifecycle defaults capacity={foodCapacity:F2} depletionRate={depletionRate:F2} regrowRate={regrowRate:F2} depletedMultiplier={depletedFoodStrengthMultiplier:F2} reactivateDelay={foodReactivationDelay:F2}s reactivateThreshold={foodReactivationThreshold:F2} allowRegrowth={allowFoodRegrowth} lifecycleLogs={debugFoodLogging}");

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
        Debug.Log($"[NeuralSlimeMold] steering trailFollowWeight={trailFollowWeight:F2} foodAttractionWeight={foodAttractionWeight:F2} foodTurnBias={foodTurnBias:F2} turnNoise={turnNoise:F2} localLoopSuppression={localLoopSuppression:F2} migrationRestlessness={migrationRestlessness:F2}");
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
        BuildWorldLayout(simulationMode == SimulationMode.Experimental, out _, out var configs, out _);
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
