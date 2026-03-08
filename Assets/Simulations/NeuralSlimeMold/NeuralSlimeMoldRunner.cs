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

    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private Vector2[] foodNodes = Array.Empty<Vector2>();
    private SeededRng rng;
    private Vector2 mapSize;
    private BoundaryMode boundaryMode;
    private float wallMargin;
    private float foodRadius;

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public Vector2[] FoodNodes => foodNodes;
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
        Vector2[] manualFoodNodes)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);
        this.boundaryMode = boundaryMode;
        this.wallMargin = Mathf.Max(0f, wallMargin);
        this.foodRadius = Mathf.Max(0.25f, foodRadius);

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, this.boundaryMode == BoundaryMode.Wrap);
        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];

        BuildFoodNodes(enableFoodNodes, Mathf.Max(0, foodNodeCount), spawnFoodFromSeed, manualFoodNodes);

        for (var i = 0; i < agents.Length; i++)
        {
            var radial = rng.InsideUnitCircle();
            var pos = new Vector2(radial.x * this.mapSize.x * 0.25f, radial.y * this.mapSize.y * 0.25f);
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

    public void ResetWithSeed(int seed, int agentCount, Vector2Int trailResolution, Vector2 mapSize, float speed, float turnRate, float sensorAngle, float sensorDistance, float depositAmount, BoundaryMode boundaryMode, float wallMargin, bool enableFoodNodes, int foodNodeCount, float foodRadius, bool spawnFoodFromSeed, Vector2[] manualFoodNodes)
    {
        Initialize(seed, agentCount, mapSize, trailResolution, speed, turnRate, sensorAngle, sensorDistance, depositAmount, boundaryMode, wallMargin, enableFoodNodes, foodNodeCount, foodRadius, spawnFoodFromSeed, manualFoodNodes);
    }

    public void Tick(float dt, float diffusion, float decay, bool indirectFoodBias, float liveFoodStrength)
    {
        if (agents == null || Field == null)
        {
            return;
        }

        for (var i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];

            var leftDir = Direction(agent.heading - agent.sensorAngle);
            var centerDir = Direction(agent.heading);
            var rightDir = Direction(agent.heading + agent.sensorAngle);

            var left = Field.SampleBilinear(agent.position + (leftDir * agent.sensorDistance));
            var center = Field.SampleBilinear(agent.position + (centerDir * agent.sensorDistance));
            var right = Field.SampleBilinear(agent.position + (rightDir * agent.sensorDistance));
            var density = (left + center + right) / 3f;

            var weighted = (left * agent.controller.leftWeight)
                         + (center * agent.controller.centerWeight)
                         + (right * agent.controller.rightWeight);

            var edge = right - left;
            var centerBias = center - ((left + right) * 0.5f);
            var noise = (rng.NextFloat01() * 2f) - 1f;
            var boundaryTurn = ComputeBoundaryTurn(agent.position, agent.heading);
            var foodTurn = indirectFoodBias ? ComputeFoodTurn(agent.position, agent.heading, liveFoodStrength) : 0f;

            var steer = weighted * 0.2f
                        + edge
                        + (centerBias * agent.controller.densityWeight)
                        + (noise * agent.controller.noiseWeight)
                        + foodTurn
                        + boundaryTurn;

            var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
            agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

            var speedMod = 1f + Mathf.Clamp(center * 0.15f, -0.2f, 0.4f);
            var moveDelta = Direction(agent.heading) * (agent.speed * speedMod * dt);
            agent.position += moveDelta;
            ApplyBoundary(ref agent.position, ref agent.heading);

            var deposit = agent.depositAmount * (0.8f + Mathf.Clamp01(density) * 0.7f);
            Field.Deposit(agent.position, deposit);

            agents[i] = agent;
        }

        Field.Step(diffusion, decay, dt);
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

        var radius = Mathf.Max(0.001f, foodRadius);
        var invRadius = 1f / radius;
        var weightedDirection = Vector2.zero;
        var totalInfluence = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var toNode = foodNodes[i] - position;
            var distance = toNode.magnitude;
            if (distance <= 0.001f)
            {
                continue;
            }

            var normalizedDistance = distance * invRadius;
            var influence = Mathf.Clamp01(1f - normalizedDistance);
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

    private void BuildFoodNodes(bool enableFoodNodes, int foodNodeCount, bool spawnFoodFromSeed, Vector2[] manualFoodNodes)
    {
        if (!enableFoodNodes)
        {
            foodNodes = Array.Empty<Vector2>();
            return;
        }

        if (manualFoodNodes != null && manualFoodNodes.Length > 0)
        {
            foodNodes = new Vector2[manualFoodNodes.Length];
            for (var i = 0; i < manualFoodNodes.Length; i++)
            {
                foodNodes[i] = ClampNodeInsideBounds(manualFoodNodes[i]);
            }

            return;
        }

        var count = Mathf.Max(1, foodNodeCount);
        foodNodes = new Vector2[count];
        var placementRng = spawnFoodFromSeed
            ? new SeededRng(StableHashUtility.CombineSeed(Seed, "slime-food-nodes"))
            : new SeededRng((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));

        var safeHalfX = Mathf.Max(1f, (mapSize.x * 0.5f) - Mathf.Max(wallMargin, foodRadius * 0.65f));
        var safeHalfY = Mathf.Max(1f, (mapSize.y * 0.5f) - Mathf.Max(wallMargin, foodRadius * 0.65f));

        for (var i = 0; i < count; i++)
        {
            foodNodes[i] = new Vector2(
                placementRng.Range(-safeHalfX, safeHalfX),
                placementRng.Range(-safeHalfY, safeHalfY));
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
