using System;
using UnityEngine;

public sealed class NeuralSlimeMoldRunner
{
    private const float MinFoodStrengthFloor = 0.0001f;
    private const float EmptyNodeDepositMultiplier = 0.3f;
    private const float OccupiedFoodLogIntervalSeconds = 4f;

    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private NeuralFoodNodeState[] foodNodes = Array.Empty<NeuralFoodNodeState>();
    private int[] foodConsumerCounts = Array.Empty<int>();
    private bool[] foodDepletionLogged = Array.Empty<bool>();
    private float simulationTime;
    private float nextOccupiedFoodLogTime;

    private SeededRng rng;
    private Vector2 mapSize;

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public NeuralFoodNodeState[] FoodNodes => foodNodes;
    public NeuralObstacle[] Obstacles => Array.Empty<NeuralObstacle>();

    public int Seed { get; private set; }
    public int AgentCount => agents?.Length ?? 0;

    public void ResetWithSeed(
        int seed,
        int agentCount,
        Vector2Int trailResolution,
        Vector2 mapSize,
        float speed,
        float turnRate,
        float sensorAngle,
        float sensorDistance,
        float depositAmount,
        int foodNodeCount,
        float foodStrength,
        float foodCapacity,
        float consumeRadius,
        float consumeRate,
        bool spawnFoodFromSeed,
        NeuralFoodNodeConfig[] manualFoodNodes)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, false);
        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];

        BuildFoodNodes(
            Mathf.Max(1, foodNodeCount),
            Mathf.Max(0f, foodStrength),
            Mathf.Max(0f, foodCapacity),
            Mathf.Max(0f, consumeRadius),
            Mathf.Max(0f, consumeRate),
            spawnFoodFromSeed,
            manualFoodNodes);

        simulationTime = 0f;
        nextOccupiedFoodLogTime = OccupiedFoodLogIntervalSeconds;

        for (var i = 0; i < agents.Length; i++)
        {
            var radial = rng.InsideUnitCircle();
            var pos = new Vector2(radial.x * this.mapSize.x * 0.35f, radial.y * this.mapSize.y * 0.35f);
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

            Field.Deposit(pos, depositAmount * 0.5f);
        }

        Debug.Log($"[NeuralSlimeMold] Initialized {foodNodes.Length} food nodes.");
    }

    public void Tick(float dt, float diffusion, float decay, float foodStrength, float explorationTurnNoise)
    {
        if (agents == null || Field == null)
        {
            return;
        }

        if (foodConsumerCounts == null || foodConsumerCounts.Length != foodNodes.Length)
        {
            foodConsumerCounts = new int[foodNodes.Length];
        }

        Array.Clear(foodConsumerCounts, 0, foodConsumerCounts.Length);
        simulationTime += Mathf.Max(0f, dt);

        for (var i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];

            var leftDir = Direction(agent.heading - agent.sensorAngle);
            var centerDir = Direction(agent.heading);
            var rightDir = Direction(agent.heading + agent.sensorAngle);

            var left = Field.SampleBilinear(agent.position + (leftDir * agent.sensorDistance));
            var center = Field.SampleBilinear(agent.position + (centerDir * agent.sensorDistance));
            var right = Field.SampleBilinear(agent.position + (rightDir * agent.sensorDistance));

            var weighted = (left * agent.controller.leftWeight)
                         + (center * agent.controller.centerWeight)
                         + (right * agent.controller.rightWeight);
            var edge = right - left;
            var centerBias = center - ((left + right) * 0.5f);
            var randomTurn = ((rng.NextFloat01() * 2f) - 1f) * explorationTurnNoise;
            var foodTurn = ComputeFoodTurn(agent.position, agent.heading, foodStrength);

            var steer = (weighted * 0.2f)
                        + edge
                        + (centerBias * agent.controller.densityWeight)
                        + randomTurn
                        + foodTurn;

            var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
            agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

            var speedMod = 1f + Mathf.Clamp(center * 0.15f, -0.2f, 0.35f);
            agent.position += Direction(agent.heading) * (agent.speed * speedMod * dt);
            ApplyReflectBoundary(ref agent.position, ref agent.heading);

            var nearestConsumableFood = FindNearestConsumableFoodNode(agent.position);
            if (nearestConsumableFood >= 0)
            {
                foodConsumerCounts[nearestConsumableFood]++;
            }

            var depositMultiplier = IsInsideEmptyFoodRadius(agent.position) ? EmptyNodeDepositMultiplier : 1f;
            Field.Deposit(agent.position, agent.depositAmount * depositMultiplier);

            agents[i] = agent;
        }

        ConsumeFood(dt);
        LogOccupiedFoodLevels();
        Field.Step(diffusion, decay, dt);
    }

    private void ConsumeFood(float dt)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var consumed = node.consumeRate * foodConsumerCounts[i] * Mathf.Max(0f, dt);
            var previousCapacity = node.currentCapacity;
            node.currentCapacity = Mathf.Clamp(node.currentCapacity - consumed, 0f, Mathf.Max(0f, node.capacity));

            if (!foodDepletionLogged[i] && previousCapacity > 0f && node.currentCapacity <= 0f)
            {
                foodDepletionLogged[i] = true;
                Debug.Log($"[NeuralSlimeMold] Food node {i} depleted at t={simulationTime:F2}s.");
            }

            foodNodes[i] = node;
        }
    }

    private int FindNearestConsumableFoodNode(Vector2 agentPosition)
    {
        var nearestIndex = -1;
        var nearestDistanceSqr = float.MaxValue;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (node.currentCapacity <= 0f)
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            var distanceSqr = (agentPosition - node.position).sqrMagnitude;
            if (distanceSqr <= radius * radius && distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private bool IsInsideEmptyFoodRadius(Vector2 agentPosition)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (node.currentCapacity > 0f)
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            if ((agentPosition - node.position).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private float ComputeFoodTurn(Vector2 position, float heading, float foodStrength)
    {
        if (foodNodes == null || foodNodes.Length == 0 || foodStrength <= 0f)
        {
            return 0f;
        }

        var weightedDirection = Vector2.zero;
        var totalInfluence = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (node.currentCapacity <= 0f || node.capacity <= 0f)
            {
                continue;
            }

            var fill = Mathf.Clamp01(node.currentCapacity / node.capacity);
            var effectiveStrength = node.baseStrength * fill;
            if (effectiveStrength <= MinFoodStrengthFloor)
            {
                continue;
            }

            var toNode = node.position - position;
            var distance = toNode.magnitude;
            if (distance <= 0.001f)
            {
                continue;
            }

            var influenceRange = Mathf.Max(node.consumeRadius * 3f, 1f);
            var distance01 = Mathf.Clamp01(distance / influenceRange);
            var influence = (1f - distance01) * effectiveStrength;
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

        var targetDir = (weightedDirection / totalInfluence).normalized;
        var forward = Direction(heading);
        var cross = (forward.x * targetDir.y) - (forward.y * targetDir.x);
        return cross * foodStrength;
    }

    private void LogOccupiedFoodLevels()
    {
        if (simulationTime < nextOccupiedFoodLogTime)
        {
            return;
        }

        nextOccupiedFoodLogTime += OccupiedFoodLogIntervalSeconds;
        var loggedAny = false;
        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (foodConsumerCounts[i] <= 0)
            {
                continue;
            }

            var node = foodNodes[i];
            if (!loggedAny)
            {
                Debug.Log($"[NeuralSlimeMold] t={simulationTime:F1}s occupied food capacities follow.");
                loggedAny = true;
            }

            Debug.Log($"[NeuralSlimeMold] node={i} consumers={foodConsumerCounts[i]} capacity={node.currentCapacity:F2}/{Mathf.Max(0.01f, node.capacity):F2}");
        }
    }

    private void BuildFoodNodes(
        int foodNodeCount,
        float foodStrength,
        float foodCapacity,
        float consumeRadius,
        float consumeRate,
        bool spawnFoodFromSeed,
        NeuralFoodNodeConfig[] manualFoodNodes)
    {
        if (manualFoodNodes != null && manualFoodNodes.Length > 0)
        {
            foodNodes = new NeuralFoodNodeState[manualFoodNodes.Length];
            for (var i = 0; i < manualFoodNodes.Length; i++)
            {
                var cfg = manualFoodNodes[i];
                foodNodes[i] = BuildFoodState(
                    ClampNodeInsideBounds(cfg.position),
                    Mathf.Max(0f, cfg.baseStrength),
                    Mathf.Max(0f, cfg.capacity),
                    Mathf.Max(0f, cfg.consumeRadius),
                    Mathf.Max(0f, cfg.consumeRate));
            }

            foodConsumerCounts = new int[foodNodes.Length];
            foodDepletionLogged = new bool[foodNodes.Length];
            return;
        }

        foodNodes = new NeuralFoodNodeState[foodNodeCount];
        var placementRng = spawnFoodFromSeed
            ? new SeededRng(StableHashUtility.CombineSeed(Seed, "slime-food-nodes"))
            : new SeededRng((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));

        for (var i = 0; i < foodNodeCount; i++)
        {
            var pos = new Vector2(
                placementRng.Range(-mapSize.x * 0.4f, mapSize.x * 0.4f),
                placementRng.Range(-mapSize.y * 0.4f, mapSize.y * 0.4f));
            pos = ClampNodeInsideBounds(pos);
            foodNodes[i] = BuildFoodState(pos, foodStrength, foodCapacity, consumeRadius, consumeRate);
        }

        foodConsumerCounts = new int[foodNodes.Length];
        foodDepletionLogged = new bool[foodNodes.Length];
    }

    private static NeuralFoodNodeState BuildFoodState(Vector2 position, float baseStrength, float capacity, float consumeRadius, float consumeRate)
    {
        return new NeuralFoodNodeState
        {
            position = position,
            baseStrength = baseStrength,
            capacity = capacity,
            currentCapacity = capacity,
            consumeRadius = consumeRadius,
            consumeRate = consumeRate
        };
    }

    private void ApplyReflectBoundary(ref Vector2 position, ref float heading)
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
        var halfX = (mapSize.x * 0.5f) - 0.5f;
        var halfY = (mapSize.y * 0.5f) - 0.5f;

        node.x = Mathf.Clamp(node.x, -halfX, halfX);
        node.y = Mathf.Clamp(node.y, -halfY, halfY);
        return node;
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private NeuralControllerParams CreateControllerProfile(int index)
    {
        var profileRng = new SeededRng(StableHashUtility.CombineSeed(Seed, $"slime-controller-{index}"));
        var sideBias = profileRng.Range(0.75f, 1.2f);
        return new NeuralControllerParams
        {
            leftWeight = profileRng.Range(0.7f, 1.2f) * sideBias,
            centerWeight = profileRng.Range(0.35f, 0.9f),
            rightWeight = profileRng.Range(0.7f, 1.2f) / sideBias,
            densityWeight = profileRng.Range(-0.2f, 0.35f),
            noiseWeight = profileRng.Range(0.05f, 0.18f)
        };
    }
}
