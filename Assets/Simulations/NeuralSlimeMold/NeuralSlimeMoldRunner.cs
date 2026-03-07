using System;
using UnityEngine;

public sealed class NeuralSlimeMoldRunner
{
    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private Vector2[] attractors = Array.Empty<Vector2>();
    private SeededRng rng;
    private Vector2 mapSize;

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public Vector2[] Attractors => attractors;

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
        bool useAttractors,
        int attractorCount)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize);
        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];
        attractors = useAttractors ? new Vector2[Mathf.Max(1, attractorCount)] : Array.Empty<Vector2>();

        for (var i = 0; i < attractors.Length; i++)
        {
            attractors[i] = new Vector2(
                rng.Range(-this.mapSize.x * 0.42f, this.mapSize.x * 0.42f),
                rng.Range(-this.mapSize.y * 0.42f, this.mapSize.y * 0.42f));
        }

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

    public void ResetWithSeed(int seed, int agentCount, Vector2Int trailResolution, Vector2 mapSize, float speed, float turnRate, float sensorAngle, float sensorDistance, float depositAmount, bool useAttractors, int attractorCount)
    {
        Initialize(seed, agentCount, mapSize, trailResolution, speed, turnRate, sensorAngle, sensorDistance, depositAmount, useAttractors, attractorCount);
    }

    public void Tick(float dt, float diffusion, float decay, bool indirectAttractorBias)
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
            var attractorTurn = indirectAttractorBias ? ComputeAttractorTurn(agent.position, agent.heading) : 0f;

            var steer = weighted * 0.2f
                        + edge
                        + (centerBias * agent.controller.densityWeight)
                        + (noise * agent.controller.noiseWeight)
                        + attractorTurn;

            var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
            agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

            var speedMod = 1f + Mathf.Clamp(center * 0.15f, -0.2f, 0.4f);
            var moveDelta = Direction(agent.heading) * (agent.speed * speedMod * dt);
            agent.position += moveDelta;
            WrapPosition(ref agent.position);

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

    private float ComputeAttractorTurn(Vector2 position, float heading)
    {
        if (attractors == null || attractors.Length == 0)
        {
            return 0f;
        }

        var bestSq = float.MaxValue;
        Vector2 nearest = position;

        for (var i = 0; i < attractors.Length; i++)
        {
            var sq = (attractors[i] - position).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                nearest = attractors[i];
            }
        }

        var toTarget = (nearest - position).normalized;
        var forward = Direction(heading);
        var cross = (forward.x * toTarget.y) - (forward.y * toTarget.x);
        var influence = Mathf.Clamp01(1f - (Mathf.Sqrt(bestSq) / Mathf.Max(mapSize.x, mapSize.y)));
        return cross * influence * 0.75f;
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

    private static Vector2 Direction(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
