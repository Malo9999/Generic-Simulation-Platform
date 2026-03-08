using System;
using UnityEngine;

public sealed class NeuralSlimeMoldRunner
{
    public readonly struct FoodSpawnDebugInfo
    {
        public FoodSpawnDebugInfo(bool enabled, int requestedCount, int spawnedCount, int rejectedCount)
        {
            Enabled = enabled;
            RequestedCount = requestedCount;
            SpawnedCount = spawnedCount;
            RejectedCount = rejectedCount;
        }

        public bool Enabled { get; }
        public int RequestedCount { get; }
        public int SpawnedCount { get; }
        public int RejectedCount { get; }
    }

    public enum BoundaryMode
    {
        Wrap = 0,
        Reflect = 1,
        SoftWall = 2
    }

    private const float AgentCollisionPadding = 0.12f;

    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private NeuralFoodNodeState[] foodNodes = Array.Empty<NeuralFoodNodeState>();
    private NeuralObstacle[] obstacles = Array.Empty<NeuralObstacle>();
    private int[] nodeVisitCounts = Array.Empty<int>();

    private SeededRng rng;
    private Vector2 mapSize;
    private BoundaryMode boundaryMode;
    private float wallMargin;
    private float foodRadius;
    private float minFoodSpacing;
    private bool debugFoodLifecycleLogging;
    private float simulationTime;

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public NeuralFoodNodeState[] FoodNodes => foodNodes;
    public NeuralObstacle[] Obstacles => obstacles;
    public float FoodRadius => foodRadius;

    public int Seed { get; private set; }
    public int AgentCount => agents?.Length ?? 0;
    public FoodSpawnDebugInfo LastFoodSpawnInfo { get; private set; }

    public void Initialize(
        int seed,
        int agentCount,
        Vector2 mapSize,
        Vector2Int trailResolution,
        float speed,
        float turnRate,
        float sensorAngle,
        float sensorDistance,
        float depositAmount,
        BoundaryMode boundaryMode,
        float wallMargin,
        bool enableFoodNodes,
        int foodNodeCount,
        float foodRadius,
        float defaultFoodCapacity,
        float defaultFoodDepletionRate,
        float defaultFoodRegrowRate,
        bool spawnFoodFromSeed,
        Vector2[] manualFoodNodes,
        NeuralFoodNodeConfig[] manualFoodConfigs,
        NeuralObstacle[] worldObstacles,
        bool debugFoodLifecycleLogging)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);
        this.boundaryMode = boundaryMode;
        this.wallMargin = Mathf.Max(0f, wallMargin);
        this.foodRadius = Mathf.Max(0.25f, foodRadius);
        minFoodSpacing = this.foodRadius * 0.5f;
        this.debugFoodLifecycleLogging = debugFoodLifecycleLogging;
        simulationTime = 0f;

        obstacles = worldObstacles ?? Array.Empty<NeuralObstacle>();
        ValidateObstaclesInBounds();

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, this.boundaryMode == BoundaryMode.Wrap);
        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];

        BuildFoodNodes(enableFoodNodes, Mathf.Max(0, foodNodeCount), Mathf.Max(0f, defaultFoodCapacity), Mathf.Max(0f, defaultFoodDepletionRate), Mathf.Max(0f, defaultFoodRegrowRate), spawnFoodFromSeed, manualFoodNodes, manualFoodConfigs);

        for (var i = 0; i < agents.Length; i++)
        {
            var pos = FindSpawnPoint(i);
            var heading = rng.Range(0f, Mathf.PI * 2f);

            agents[i] = new NeuralSlimeMoldAgent
            {
                position = pos,
                heading = heading,
                speed = speed,
                turnRate = turnRate,
                sensorAngle = sensorAngle,
                sensorDistance = sensorDistance,
                depositAmount = depositAmount,
                controller = CreateControllerProfile(i)
            };

            Field.Deposit(pos, depositAmount * 0.75f);
        }
    }

    public void ResetWithSeed(int seed, int agentCount, Vector2Int trailResolution, Vector2 mapSize, float speed, float turnRate, float sensorAngle, float sensorDistance, float depositAmount, BoundaryMode boundaryMode, float wallMargin, bool enableFoodNodes, int foodNodeCount, float foodRadius, float defaultFoodCapacity, float defaultFoodDepletionRate, float defaultFoodRegrowRate, bool spawnFoodFromSeed, Vector2[] manualFoodNodes, NeuralFoodNodeConfig[] manualFoodConfigs, NeuralObstacle[] worldObstacles, bool debugFoodLifecycleLogging)
    {
        Initialize(seed, agentCount, mapSize, trailResolution, speed, turnRate, sensorAngle, sensorDistance, depositAmount, boundaryMode, wallMargin, enableFoodNodes, foodNodeCount, foodRadius, defaultFoodCapacity, defaultFoodDepletionRate, defaultFoodRegrowRate, spawnFoodFromSeed, manualFoodNodes, manualFoodConfigs, worldObstacles, debugFoodLifecycleLogging);
    }

    public void Tick(
        float dt,
        float diffusion,
        float decay,
        bool indirectFoodBias,
        float liveFoodStrength,
        bool allowFoodRegrowth,
        float trailFollowWeight,
        float foodAttractionWeight,
        float foodSenseRadius,
        float foodTurnBias,
        float depletedFoodStrengthMultiplier,
        float foodReactivationDelay,
        float foodReactivationThreshold,
        float migrationRestlessness,
        float turnNoise,
        float localLoopSuppression,
        float depositNearFoodMultiplier,
        float pathPersistenceBias,
        bool foodPulseEnabled,
        float foodPulsePeriod,
        float foodPulseStrength,
        bool localTrailScrubEnabled,
        float localTrailScrubThreshold,
        float localTrailScrubAmount)
    {
        if (agents == null || Field == null)
        {
            return;
        }

        if (nodeVisitCounts.Length != foodNodes.Length)
        {
            nodeVisitCounts = new int[foodNodes.Length];
        }

        Array.Clear(nodeVisitCounts, 0, nodeVisitCounts.Length);

        simulationTime += Mathf.Max(0f, dt);
        var pulseStrength = ComputeFoodPulseStrength(foodPulseEnabled, foodPulsePeriod, foodPulseStrength);

        for (var i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];

            var leftDir = Direction(agent.heading - agent.sensorAngle);
            var centerDir = Direction(agent.heading);
            var rightDir = Direction(agent.heading + agent.sensorAngle);

            var left = SampleFieldWithObstacles(agent.position + (leftDir * agent.sensorDistance));
            var center = SampleFieldWithObstacles(agent.position + (centerDir * agent.sensorDistance));
            var right = SampleFieldWithObstacles(agent.position + (rightDir * agent.sensorDistance));
            var density = (left + center + right) / 3f;

            var weighted = (left * agent.controller.leftWeight)
                         + (center * agent.controller.centerWeight)
                         + (right * agent.controller.rightWeight);

            var edge = right - left;
            var centerBias = center - ((left + right) * 0.5f);
            var noise = (rng.NextFloat01() * 2f) - 1f;
            var boundaryTurn = ComputeBoundaryTurn(agent.position, agent.heading);
            var foodTurn = indirectFoodBias
                ? ComputeFoodTurn(agent.position, agent.heading, liveFoodStrength, foodAttractionWeight, foodSenseRadius, foodTurnBias, depletedFoodStrengthMultiplier, pulseStrength, simulationTime)
                : 0f;
            var obstacleTurn = ComputeObstacleTurn(agent.position, agent.heading, agent.sensorDistance);
            var loopSuppressionTurn = ComputeLoopSuppressionTurn(left, center, right, localLoopSuppression);
            var restlessnessTurn = ComputeRestlessnessTurn(agent.position, agent.heading, migrationRestlessness);

            var steer = (weighted * 0.2f * Mathf.Max(0f, trailFollowWeight))
                        + (edge * Mathf.Max(0f, trailFollowWeight))
                        + (centerBias * agent.controller.densityWeight * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(localLoopSuppression)))
                        + (noise * agent.controller.noiseWeight * Mathf.Max(0f, turnNoise))
                        + foodTurn
                        + obstacleTurn
                        + loopSuppressionTurn
                        + restlessnessTurn
                        + boundaryTurn;

            var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
            agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

            var speedMod = 1f + Mathf.Clamp(center * 0.15f, -0.2f, 0.4f);
            var moveDelta = Direction(agent.heading) * (agent.speed * speedMod * dt);
            var previousPosition = agent.position;
            var candidatePosition = previousPosition + moveDelta;
            ApplyBoundary(ref candidatePosition, ref agent.heading);
            ResolveObstacleMove(previousPosition, ref candidatePosition, ref agent.heading);
            agent.position = candidatePosition;

            AccumulateFoodVisits(agent.position);

            var foodProximity = ComputeFoodProximity(agent.position, foodSenseRadius, depletedFoodStrengthMultiplier, pulseStrength);
            var depositBoost = 1f + ((Mathf.Max(1f, depositNearFoodMultiplier) - 1f) * foodProximity);
            var persistence = Mathf.Clamp01(pathPersistenceBias);
            if (persistence > 0f)
            {
                var bridgeSignal = ComputeInterNodeBridgeSignal(agent.position, foodSenseRadius, depletedFoodStrengthMultiplier, pulseStrength);
                depositBoost *= 1f + (bridgeSignal * persistence * 0.9f);
                depositBoost = Mathf.Lerp(depositBoost, 1f, persistence * 0.35f * foodProximity);
            }

            var deposit = agent.depositAmount * (0.8f + Mathf.Clamp01(density) * 0.7f) * depositBoost;
            Field.Deposit(agent.position, deposit);
            ApplyLocalTrailScrub(agent.position, density, dt, localTrailScrubEnabled, localTrailScrubThreshold, localTrailScrubAmount);

            agents[i] = agent;
        }

        UpdateFoodNodes(dt, allowFoodRegrowth, foodReactivationDelay, foodReactivationThreshold);
        Field.Step(diffusion, decay, dt);
    }


    private float ComputeFoodPulseStrength(bool foodPulseEnabled, float foodPulsePeriod, float foodPulseStrength)
    {
        if (!foodPulseEnabled || foodPulseStrength <= 0f)
        {
            return 1f;
        }

        var period = Mathf.Max(0.5f, foodPulsePeriod);
        var phase01 = Mathf.Repeat(simulationTime / period, 1f);
        var wave = Mathf.Sin(phase01 * Mathf.PI * 2f);
        return Mathf.Max(0.1f, 1f + (wave * Mathf.Max(0f, foodPulseStrength)));
    }

    private void ApplyLocalTrailScrub(Vector2 position, float density, float dt, bool localTrailScrubEnabled, float localTrailScrubThreshold, float localTrailScrubAmount)
    {
        if (!localTrailScrubEnabled || localTrailScrubAmount <= 0f)
        {
            return;
        }

        var threshold = Mathf.Clamp01(localTrailScrubThreshold);
        if (density <= threshold)
        {
            return;
        }

        var overshoot = Mathf.InverseLerp(threshold, 1f, Mathf.Clamp01(density));
        var scrubAmount = Mathf.Clamp01(localTrailScrubAmount * overshoot * Mathf.Clamp(dt * 20f, 0.1f, 1.5f));
        if (scrubAmount <= 0.0001f)
        {
            return;
        }

        Field.ScrubAt(position, scrubAmount);
    }

    private void UpdateFoodNodes(float dt, bool allowFoodRegrowth, float foodReactivationDelay, float foodReactivationThreshold)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var wasActive = node.active;
            var previousCapacity = node.capacity;
            var depletedFor = node.timeSinceDepleted;
            node.timeSinceDepleted = node.active ? 0f : node.timeSinceDepleted + Mathf.Max(0f, dt);
            if (node.maxCapacity <= 0f)
            {
                node.capacity = 0f;
                node.active = false;
                foodNodes[i] = node;
                continue;
            }

            if (node.active)
            {
                var localVisits = nodeVisitCounts[i];
                var occupancyWeight = 1f + (Mathf.Clamp01(localVisits / 24f) * 1.8f);
                var consumed = localVisits * node.depletionRate * dt * occupancyWeight;
                node.capacity = Mathf.Max(0f, node.capacity - consumed);
                if (node.capacity <= 0.0001f)
                {
                    node.capacity = 0f;
                    node.active = false;
                    node.timeSinceDepleted = 0f;
                }
            }

            if (!node.active && allowFoodRegrowth && node.regrowRate > 0f)
            {
                node.capacity = Mathf.Min(node.maxCapacity, node.capacity + (node.regrowRate * dt));
                var reactivateThreshold = Mathf.Clamp01(foodReactivationThreshold) * node.maxCapacity;
                var canReactivate = node.timeSinceDepleted >= Mathf.Max(0f, foodReactivationDelay);
                if (canReactivate && node.capacity > reactivateThreshold)
                {
                    node.active = true;
                    node.timeSinceDepleted = 0f;
                }
            }

            if (debugFoodLifecycleLogging)
            {
                if (wasActive && !node.active)
                {
                    Debug.Log($"[NeuralSlimeMold] food[{i}] depleted at pos={node.position} cap={previousCapacity:F2}->{node.capacity:F2}");
                }
                else if (!wasActive && node.active)
                {
                    Debug.Log($"[NeuralSlimeMold] food[{i}] reactivated at pos={node.position} cap={previousCapacity:F2}->{node.capacity:F2} depletedFor={depletedFor:F2}s");
                }
            }

            foodNodes[i] = node;
        }
    }

    private void AccumulateFoodVisits(Vector2 agentPosition)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (!node.active)
            {
                continue;
            }

            var captureRadius = Mathf.Max(0.2f, node.radius);
            if ((agentPosition - node.position).sqrMagnitude <= captureRadius * captureRadius)
            {
                nodeVisitCounts[i]++;
            }
        }
    }

    private float SampleFieldWithObstacles(Vector2 samplePosition)
    {
        if (IsPointInObstacle(samplePosition))
        {
            return 0f;
        }

        return Field.SampleBilinear(samplePosition);
    }

    private float ComputeObstacleTurn(Vector2 position, float heading, float sensorDistance)
    {
        if (obstacles.Length == 0)
        {
            return 0f;
        }

        var forward = Direction(heading);
        var probe = position + (forward * Mathf.Max(0.2f, sensorDistance * 1.1f));
        if (!TryGetObstacleNormal(probe, out var normal, out var depth))
        {
            return 0f;
        }

        var toSafe = -normal;
        var cross = (forward.x * toSafe.y) - (forward.y * toSafe.x);
        return cross * Mathf.Clamp01(depth * 0.8f);
    }

    private void ResolveObstacleMove(Vector2 previous, ref Vector2 candidate, ref float heading)
    {
        if (obstacles.Length == 0)
        {
            return;
        }

        if (!TryGetObstacleNormal(candidate, out var normal, out _))
        {
            return;
        }

        candidate = previous;
        var reflected = Vector2.Reflect(Direction(heading), normal);
        heading = Mathf.Repeat(Mathf.Atan2(reflected.y, reflected.x), Mathf.PI * 2f);
    }

    private bool TryGetObstacleNormal(Vector2 point, out Vector2 normal, out float penetration)
    {
        normal = Vector2.zero;
        penetration = 0f;
        var hasHit = false;

        for (var i = 0; i < obstacles.Length; i++)
        {
            var obstacle = obstacles[i];
            if (obstacle.shape == NeuralObstacleShape.Rectangle)
            {
                if (!TryRectCollision(point, obstacle, out var rectNormal, out var rectDepth))
                {
                    continue;
                }

                if (!hasHit || rectDepth > penetration)
                {
                    hasHit = true;
                    penetration = rectDepth;
                    normal = rectNormal;
                }

                continue;
            }

            var radius = Mathf.Max(0.1f, obstacle.radius) + AgentCollisionPadding;
            var delta = point - obstacle.center;
            var dist = delta.magnitude;
            if (dist >= radius)
            {
                continue;
            }

            var depth = radius - Mathf.Max(0.0001f, dist);
            if (!hasHit || depth > penetration)
            {
                hasHit = true;
                penetration = depth;
                normal = dist > 0.0001f ? delta / dist : Vector2.up;
            }
        }

        return hasHit;
    }

    private static bool TryRectCollision(Vector2 point, NeuralObstacle obstacle, out Vector2 normal, out float depth)
    {
        var half = new Vector2(Mathf.Max(0.15f, obstacle.size.x * 0.5f), Mathf.Max(0.15f, obstacle.size.y * 0.5f));
        var local = point - obstacle.center;
        var overlapX = half.x + AgentCollisionPadding - Mathf.Abs(local.x);
        var overlapY = half.y + AgentCollisionPadding - Mathf.Abs(local.y);

        if (overlapX <= 0f || overlapY <= 0f)
        {
            normal = Vector2.zero;
            depth = 0f;
            return false;
        }

        if (overlapX < overlapY)
        {
            normal = new Vector2(Mathf.Sign(local.x), 0f);
            depth = overlapX;
            return true;
        }

        normal = new Vector2(0f, Mathf.Sign(local.y));
        depth = overlapY;
        return true;
    }

    private bool IsPointInObstacle(Vector2 point)
    {
        return TryGetObstacleNormal(point, out _, out _);
    }

    private NeuralControllerParams CreateControllerProfile(int index)
    {
        var profileRng = new SeededRng(StableHashUtility.CombineSeed(Seed, $"slime-agent-{index}"));
        return new NeuralControllerParams
        {
            leftWeight = profileRng.Range(0.5f, 1.4f),
            centerWeight = profileRng.Range(0.5f, 1.8f),
            rightWeight = profileRng.Range(0.5f, 1.4f),
            densityWeight = profileRng.Range(0.2f, 1.2f),
            noiseWeight = profileRng.Range(0.01f, 0.25f)
        };
    }

    private float ComputeFoodTurn(Vector2 position, float heading, float liveFoodStrength, float foodAttractionWeight, float foodSenseRadius, float foodTurnBias, float depletedFoodStrengthMultiplier, float pulseStrength, float time)
    {
        if (foodNodes == null || foodNodes.Length == 0 || liveFoodStrength <= 0f || foodAttractionWeight <= 0f)
        {
            return 0f;
        }

        var weightedDirection = Vector2.zero;
        var totalInfluence = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var effectiveStrength = ComputeEffectiveStrength(node, depletedFoodStrengthMultiplier);
            if (effectiveStrength <= 0f)
            {
                continue;
            }

            var toNode = node.position - position;
            var distance = toNode.magnitude;
            if (distance <= 0.001f)
            {
                continue;
            }

            var influenceRange = Mathf.Max(0.001f, Mathf.Max(node.radius, foodSenseRadius));
            var normalizedDistance = Mathf.Clamp01(distance / influenceRange);
            var distanceFalloff = (1f - normalizedDistance);
            distanceFalloff = distanceFalloff * distanceFalloff;
            var coreBoost = Mathf.SmoothStep(1.55f, 1f, normalizedDistance);
            var influence = distanceFalloff * effectiveStrength * coreBoost;
            if (influence <= 0f)
            {
                continue;
            }

            weightedDirection += (toNode / distance) * influence;
            totalInfluence += influence;
        }

        if (totalInfluence <= 0f)
        {
            return 0f;
        }

        var toTarget = (weightedDirection / totalInfluence).normalized;
        var forward = Direction(heading);
        var cross = (forward.x * toTarget.y) - (forward.y * toTarget.x);
        var alignment = Vector2.Dot(forward, toTarget);
        var misalignment = Mathf.Clamp01(1f - ((alignment + 1f) * 0.5f));
        var turnAmplifier = 1f + (misalignment * Mathf.Max(0f, foodTurnBias));
        turnAmplifier *= 1f + Mathf.Clamp01(totalInfluence) * 0.55f;
        return cross * Mathf.Max(0f, liveFoodStrength) * foodAttractionWeight * turnAmplifier;
    }

    private float ComputeFoodProximity(Vector2 position, float foodSenseRadius, float depletedFoodStrengthMultiplier, float pulseStrength)
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return 0f;
        }

        var proximity = 0f;
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var effectiveStrength = ComputeEffectiveStrength(node, depletedFoodStrengthMultiplier);
            if (effectiveStrength <= 0f)
            {
                continue;
            }

            var range = Mathf.Max(node.radius, foodSenseRadius);
            var distance = Vector2.Distance(position, node.position);
            var nodeProximity = Mathf.Clamp01(1f - (distance / Mathf.Max(0.001f, range))) * Mathf.Clamp01(effectiveStrength);
            proximity = Mathf.Max(proximity, nodeProximity);
        }

        return proximity;
    }

    private float ComputeInterNodeBridgeSignal(Vector2 position, float foodSenseRadius, float depletedFoodStrengthMultiplier, float pulseStrength)
    {
        if (foodNodes == null || foodNodes.Length < 2)
        {
            return 0f;
        }

        var strongest = 0f;
        var secondStrongest = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var effectiveStrength = ComputeEffectiveStrength(node, depletedFoodStrengthMultiplier);
            if (effectiveStrength <= 0f)
            {
                continue;
            }

            var range = Mathf.Max(node.radius, foodSenseRadius);
            var distance = Vector2.Distance(position, node.position);
            var pull = Mathf.Clamp01(1f - (distance / Mathf.Max(0.001f, range))) * effectiveStrength;

            if (pull > strongest)
            {
                secondStrongest = strongest;
                strongest = pull;
            }
            else if (pull > secondStrongest)
            {
                secondStrongest = pull;
            }
        }

        return Mathf.Clamp01(strongest * secondStrongest);
    }

    private static float ComputeEffectiveStrength(NeuralFoodNodeState node, float depletedFoodStrengthMultiplier)
    {
        if (node.maxCapacity <= 0f || node.strength <= 0f)
        {
            return 0f;
        }

        var capacity01 = Mathf.Clamp01(node.Capacity01);
        var activeStrength = node.strength * (capacity01 * capacity01 * capacity01);
        var depletedStrength = node.strength * Mathf.Clamp01(depletedFoodStrengthMultiplier);
        return node.active ? activeStrength : depletedStrength;
    }

    private float ComputeRestlessnessTurn(Vector2 position, float heading, float migrationRestlessness)
    {
        if (migrationRestlessness <= 0f)
        {
            return 0f;
        }

        var fieldStrength = Mathf.Clamp01(Field.SampleBilinear(position));
        var intensity = Mathf.Clamp01((fieldStrength - 0.55f) / 0.45f);
        if (intensity <= 0f)
        {
            return 0f;
        }

        var phase = (position.x * 0.173f) + (position.y * 0.219f) + (simulationTime * 0.61f) + (heading * 0.37f);
        var deterministicNoise = Mathf.Sin(phase) * 0.5f;
        return deterministicNoise * Mathf.Max(0f, migrationRestlessness) * intensity;
    }

    private float ComputeLoopSuppressionTurn(float left, float center, float right, float localLoopSuppression)
    {
        if (localLoopSuppression <= 0f)
        {
            return 0f;
        }

        var sidePeak = Mathf.Max(left, right);
        var ringSignal = Mathf.Clamp01(sidePeak - (center * 0.9f));
        if (ringSignal <= 0f)
        {
            return 0f;
        }

        var preferredSide = right > left ? -1f : 1f;
        return preferredSide * ringSignal * Mathf.Clamp01(localLoopSuppression) * 0.75f;
    }

    private void BuildFoodNodes(bool enableFoodNodes, int foodNodeCount, float defaultFoodCapacity, float defaultFoodDepletionRate, float defaultFoodRegrowRate, bool spawnFoodFromSeed, Vector2[] manualFoodNodes, NeuralFoodNodeConfig[] manualFoodConfigs)
    {
        var rejectedCount = 0;
        if (!enableFoodNodes)
        {
            foodNodes = Array.Empty<NeuralFoodNodeState>();
            nodeVisitCounts = Array.Empty<int>();
            LastFoodSpawnInfo = new FoodSpawnDebugInfo(false, foodNodeCount, 0, 0);
            return;
        }

        if (manualFoodConfigs != null && manualFoodConfigs.Length > 0)
        {
            foodNodes = new NeuralFoodNodeState[manualFoodConfigs.Length];
            for (var i = 0; i < manualFoodConfigs.Length; i++)
            {
                var cfg = manualFoodConfigs[i];
                foodNodes[i] = BuildFoodState(
                    EnsureFoodNodePosition(cfg.position, i),
                    Mathf.Max(0.25f, cfg.radius),
                    Mathf.Max(0f, cfg.strength),
                    Mathf.Max(0f, cfg.capacity),
                    Mathf.Max(0f, cfg.depletionRate),
                    Mathf.Max(0f, cfg.regrowRate),
                    cfg.startActive);
            }

            nodeVisitCounts = new int[foodNodes.Length];
            LastFoodSpawnInfo = new FoodSpawnDebugInfo(true, manualFoodConfigs.Length, foodNodes.Length, rejectedCount);
            return;
        }

        if (manualFoodNodes != null && manualFoodNodes.Length > 0)
        {
            foodNodes = new NeuralFoodNodeState[manualFoodNodes.Length];
            for (var i = 0; i < manualFoodNodes.Length; i++)
            {
                var position = EnsureFoodNodePosition(manualFoodNodes[i], i);
                foodNodes[i] = BuildFoodState(position, foodRadius, 1f, defaultFoodCapacity, defaultFoodDepletionRate, defaultFoodRegrowRate, true);
            }

            nodeVisitCounts = new int[foodNodes.Length];
            LastFoodSpawnInfo = new FoodSpawnDebugInfo(true, manualFoodNodes.Length, foodNodes.Length, rejectedCount);
            return;
        }

        var count = Mathf.Max(1, foodNodeCount);
        foodNodes = new NeuralFoodNodeState[count];
        var placementRng = spawnFoodFromSeed
            ? new SeededRng(StableHashUtility.CombineSeed(Seed, "slime-food-nodes"))
            : new SeededRng((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));

        for (var i = 0; i < count; i++)
        {
            var placed = TrySampleFoodPosition(placementRng, out var position);
            if (!placed)
            {
                rejectedCount++;
                position = EnsureFoodNodePosition(Vector2.zero + placementRng.InsideUnitCircle() * (mapSize * 0.1f), i);
            }

            foodNodes[i] = BuildFoodState(position, foodRadius, 1f, defaultFoodCapacity, defaultFoodDepletionRate, defaultFoodRegrowRate, true);
        }

        nodeVisitCounts = new int[foodNodes.Length];
        LastFoodSpawnInfo = new FoodSpawnDebugInfo(true, count, foodNodes.Length, rejectedCount);
    }

    private Vector2 EnsureFoodNodePosition(Vector2 candidate, int index)
    {
        var position = ClampNodeInsideBounds(candidate);
        if (!IsPointInObstacle(position))
        {
            return position;
        }

        var placementRng = new SeededRng(StableHashUtility.CombineSeed(Seed, $"slime-food-clear-{index}"));
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var jitter = placementRng.InsideUnitCircle() * Mathf.Lerp(foodRadius * 0.4f, mapSize.magnitude * 0.08f, attempt / 39f);
            var retry = ClampNodeInsideBounds(position + jitter);
            if (!IsPointInObstacle(retry))
            {
                return retry;
            }
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            var retry = new Vector2(
                placementRng.Range(-mapSize.x * 0.5f, mapSize.x * 0.5f),
                placementRng.Range(-mapSize.y * 0.5f, mapSize.y * 0.5f));
            retry = ClampNodeInsideBounds(retry);
            if (!IsPointInObstacle(retry))
            {
                return retry;
            }
        }

        return Vector2.zero;
    }

    private bool TrySampleFoodPosition(SeededRng placementRng, out Vector2 position)
    {
        const int maxAttempts = 80;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            position = new Vector2(
                placementRng.Range(-mapSize.x * 0.5f, mapSize.x * 0.5f),
                placementRng.Range(-mapSize.y * 0.5f, mapSize.y * 0.5f));

            position = ClampNodeInsideBounds(position);
            if (IsPointInObstacle(position))
            {
                continue;
            }

            var overlaps = false;
            for (var i = 0; i < foodNodes.Length; i++)
            {
                var existing = foodNodes[i];
                if ((existing.position - position).sqrMagnitude < minFoodSpacing * minFoodSpacing)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                return true;
            }
        }

        position = Vector2.zero;
        return false;
    }

    private static NeuralFoodNodeState BuildFoodState(Vector2 position, float radius, float strength, float capacity, float depletionRate, float regrowRate, bool startActive)
    {
        return new NeuralFoodNodeState
        {
            position = position,
            radius = radius,
            strength = strength,
            maxCapacity = capacity,
            capacity = startActive ? capacity : 0f,
            depletionRate = depletionRate,
            regrowRate = regrowRate,
            active = startActive && capacity > 0f,
            timeSinceDepleted = 0f
        };
    }

    private Vector2 FindSpawnPoint(int index)
    {
        var seed = StableHashUtility.CombineSeed(Seed, $"slime-spawn-{index}");
        var spawnRng = new SeededRng(seed);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var radial = spawnRng.InsideUnitCircle();
            var pos = new Vector2(radial.x * mapSize.x * 0.25f, radial.y * mapSize.y * 0.25f);
            if (!IsPointInObstacle(pos))
            {
                return pos;
            }
        }

        return Vector2.zero;
    }

    private void ValidateObstaclesInBounds()
    {
        if (obstacles == null)
        {
            obstacles = Array.Empty<NeuralObstacle>();
            return;
        }

        for (var i = 0; i < obstacles.Length; i++)
        {
            var obstacle = obstacles[i];
            obstacle.center = ClampNodeInsideBounds(obstacle.center);
            obstacle.radius = Mathf.Max(0.1f, obstacle.radius);
            obstacle.size = new Vector2(Mathf.Max(0.5f, obstacle.size.x), Mathf.Max(0.5f, obstacle.size.y));
            obstacles[i] = obstacle;
        }
    }

    private float ComputeBoundaryTurn(Vector2 position, float heading)
    {
        if (boundaryMode != BoundaryMode.SoftWall)
        {
            return 0f;
        }

        var margin = Mathf.Clamp(wallMargin, 0.01f, Mathf.Min(mapSize.x, mapSize.y) * 0.45f);
        var halfX = mapSize.x * 0.5f;
        var halfY = mapSize.y * 0.5f;

        var left = position.x + halfX;
        var right = halfX - position.x;
        var bottom = position.y + halfY;
        var top = halfY - position.y;

        var wallNormal = Vector2.zero;

        if (left < margin) wallNormal.x += (margin - left) / margin;
        if (right < margin) wallNormal.x -= (margin - right) / margin;
        if (bottom < margin) wallNormal.y += (margin - bottom) / margin;
        if (top < margin) wallNormal.y -= (margin - top) / margin;

        if (wallNormal.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        var forward = Direction(heading);
        var steerDir = wallNormal.normalized;
        var cross = (forward.x * steerDir.y) - (forward.y * steerDir.x);
        var strength = Mathf.Clamp01(wallNormal.magnitude) * 1.2f;
        return cross * strength;
    }

    private void ApplyBoundary(ref Vector2 position, ref float heading)
    {
        if (boundaryMode == BoundaryMode.Wrap)
        {
            WrapPosition(ref position);
            return;
        }

        ReflectPosition(ref position, ref heading);
    }

    private void WrapPosition(ref Vector2 position)
    {
        var halfX = mapSize.x * 0.5f;
        var halfY = mapSize.y * 0.5f;

        if (position.x > halfX) position.x -= mapSize.x;
        else if (position.x < -halfX) position.x += mapSize.x;

        if (position.y > halfY) position.y -= mapSize.y;
        else if (position.y < -halfY) position.y += mapSize.y;
    }

    private void ReflectPosition(ref Vector2 position, ref float heading)
    {
        var halfX = mapSize.x * 0.5f;
        var halfY = mapSize.y * 0.5f;
        var direction = Direction(heading);
        var touchedWall = false;

        if (position.x > halfX)
        {
            position.x = halfX;
            direction.x = -Mathf.Abs(direction.x);
            touchedWall = true;
        }
        else if (position.x < -halfX)
        {
            position.x = -halfX;
            direction.x = Mathf.Abs(direction.x);
            touchedWall = true;
        }

        if (position.y > halfY)
        {
            position.y = halfY;
            direction.y = -Mathf.Abs(direction.y);
            touchedWall = true;
        }
        else if (position.y < -halfY)
        {
            position.y = -halfY;
            direction.y = Mathf.Abs(direction.y);
            touchedWall = true;
        }

        if (touchedWall)
        {
            heading = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x), Mathf.PI * 2f);
        }
    }

    private Vector2 ClampNodeInsideBounds(Vector2 node)
    {
        var padding = Mathf.Max(wallMargin, foodRadius * 0.65f);
        var halfX = Mathf.Max(0.25f, (mapSize.x * 0.5f) - padding);
        var halfY = Mathf.Max(0.25f, (mapSize.y * 0.5f) - padding);

        node.x = Mathf.Clamp(node.x, -halfX, halfX);
        node.y = Mathf.Clamp(node.y, -halfY, halfY);
        return node;
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
