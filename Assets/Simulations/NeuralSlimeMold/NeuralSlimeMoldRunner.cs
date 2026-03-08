using System;
using UnityEngine;

public sealed class NeuralSlimeMoldRunner
{
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

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public NeuralFoodNodeState[] FoodNodes => foodNodes;
    public NeuralObstacle[] Obstacles => obstacles;
    public float FoodRadius => foodRadius;

    public int Seed { get; private set; }
    public int AgentCount => agents?.Length ?? 0;

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
        bool spawnFoodFromSeed,
        Vector2[] manualFoodNodes,
        NeuralFoodNodeConfig[] manualFoodConfigs,
        NeuralObstacle[] worldObstacles)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);
        this.boundaryMode = boundaryMode;
        this.wallMargin = Mathf.Max(0f, wallMargin);
        this.foodRadius = Mathf.Max(0.25f, foodRadius);
        minFoodSpacing = this.foodRadius * 0.5f;

        obstacles = worldObstacles ?? Array.Empty<NeuralObstacle>();
        ValidateObstaclesInBounds();

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, this.boundaryMode == BoundaryMode.Wrap);
        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];

        BuildFoodNodes(enableFoodNodes, Mathf.Max(0, foodNodeCount), spawnFoodFromSeed, manualFoodNodes, manualFoodConfigs);

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

    public void ResetWithSeed(int seed, int agentCount, Vector2Int trailResolution, Vector2 mapSize, float speed, float turnRate, float sensorAngle, float sensorDistance, float depositAmount, BoundaryMode boundaryMode, float wallMargin, bool enableFoodNodes, int foodNodeCount, float foodRadius, bool spawnFoodFromSeed, Vector2[] manualFoodNodes, NeuralFoodNodeConfig[] manualFoodConfigs, NeuralObstacle[] worldObstacles)
    {
        Initialize(seed, agentCount, mapSize, trailResolution, speed, turnRate, sensorAngle, sensorDistance, depositAmount, boundaryMode, wallMargin, enableFoodNodes, foodNodeCount, foodRadius, spawnFoodFromSeed, manualFoodNodes, manualFoodConfigs, worldObstacles);
    }

    public void Tick(float dt, float diffusion, float decay, bool indirectFoodBias, float liveFoodStrength, bool allowFoodRegrowth)
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
            var foodTurn = indirectFoodBias ? ComputeFoodTurn(agent.position, agent.heading, liveFoodStrength) : 0f;
            var obstacleTurn = ComputeObstacleTurn(agent.position, agent.heading, agent.sensorDistance);

            var steer = weighted * 0.2f
                        + edge
                        + (centerBias * agent.controller.densityWeight)
                        + (noise * agent.controller.noiseWeight)
                        + foodTurn
                        + obstacleTurn
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

            var deposit = agent.depositAmount * (0.8f + Mathf.Clamp01(density) * 0.7f);
            Field.Deposit(agent.position, deposit);

            agents[i] = agent;
        }

        UpdateFoodNodes(dt, allowFoodRegrowth);
        Field.Step(diffusion, decay, dt);
    }

    private void UpdateFoodNodes(float dt, bool allowFoodRegrowth)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (node.maxCapacity <= 0f)
            {
                node.capacity = 0f;
                node.active = false;
                foodNodes[i] = node;
                continue;
            }

            if (node.active)
            {
                var consumed = nodeVisitCounts[i] * node.depletionRate * dt;
                node.capacity = Mathf.Max(0f, node.capacity - consumed);
                if (node.capacity <= 0.0001f)
                {
                    node.capacity = 0f;
                    node.active = false;
                }
            }

            if (!node.active && allowFoodRegrowth && node.regrowRate > 0f)
            {
                node.capacity = Mathf.Min(node.maxCapacity, node.capacity + (node.regrowRate * dt));
                if (node.capacity > node.maxCapacity * 0.05f)
                {
                    node.active = true;
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

    private float ComputeFoodTurn(Vector2 position, float heading, float liveFoodStrength)
    {
        if (foodNodes == null || foodNodes.Length == 0 || liveFoodStrength <= 0f)
        {
            return 0f;
        }

        var weightedDirection = Vector2.zero;
        var totalInfluence = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var effectiveStrength = node.EffectiveStrength;
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

            var influenceRange = Mathf.Max(0.001f, node.radius);
            var normalizedDistance = distance / influenceRange;
            var influence = Mathf.Clamp01(1f - normalizedDistance) * effectiveStrength;
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
        return cross * Mathf.Max(0f, liveFoodStrength);
    }

    private void BuildFoodNodes(bool enableFoodNodes, int foodNodeCount, bool spawnFoodFromSeed, Vector2[] manualFoodNodes, NeuralFoodNodeConfig[] manualFoodConfigs)
    {
        if (!enableFoodNodes)
        {
            foodNodes = Array.Empty<NeuralFoodNodeState>();
            nodeVisitCounts = Array.Empty<int>();
            return;
        }

        if (manualFoodConfigs != null && manualFoodConfigs.Length > 0)
        {
            foodNodes = new NeuralFoodNodeState[manualFoodConfigs.Length];
            for (var i = 0; i < manualFoodConfigs.Length; i++)
            {
                var cfg = manualFoodConfigs[i];
                foodNodes[i] = BuildFoodState(
                    ClampNodeInsideBounds(cfg.position),
                    Mathf.Max(0.25f, cfg.radius),
                    Mathf.Max(0f, cfg.strength),
                    Mathf.Max(0f, cfg.capacity),
                    Mathf.Max(0f, cfg.depletionRate),
                    Mathf.Max(0f, cfg.regrowRate),
                    cfg.startActive);
            }

            nodeVisitCounts = new int[foodNodes.Length];
            return;
        }

        if (manualFoodNodes != null && manualFoodNodes.Length > 0)
        {
            foodNodes = new NeuralFoodNodeState[manualFoodNodes.Length];
            for (var i = 0; i < manualFoodNodes.Length; i++)
            {
                var position = ClampNodeInsideBounds(manualFoodNodes[i]);
                foodNodes[i] = BuildFoodState(position, foodRadius, 1f, 100f, 1f, 0f, true);
            }

            nodeVisitCounts = new int[foodNodes.Length];
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
                position = ClampNodeInsideBounds(Vector2.zero + placementRng.InsideUnitCircle() * (mapSize * 0.1f));
            }

            foodNodes[i] = BuildFoodState(position, foodRadius, 1f, 100f, 1f, 0f, true);
        }

        nodeVisitCounts = new int[foodNodes.Length];
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
            active = startActive && capacity > 0f
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
