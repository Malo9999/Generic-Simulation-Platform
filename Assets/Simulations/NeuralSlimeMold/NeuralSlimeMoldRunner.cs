// ==============================
// FILE: NeuralSlimeMoldRunner.cs
// ==============================
using System;
using UnityEngine;

public sealed class NeuralSlimeMoldRunner
{
    private enum AgentMode : byte
    {
        SeekFood = 0,
        Feed = 1,
        ReturnToHub = 2,
        ExitHub = 3
    }

    private const float MinFoodStrengthFloor = 0.0001f;
    private const float OccupiedFoodLogIntervalSeconds = 4f;

    private const float FoodSeekRangeMultiplier = 2.2f;
    private const float MinFoodSeekRange = 1.2f;

    private const float MinTrailForHighway = 0.02f;
    private const float StrongTrailHighwayThreshold = 0.12f;
    private const float MaxHighwayDepositBoost = 1.9f;
    private const float WeakTrailDepositPenalty = 0.72f;

    private const float ActivitySmoothing = 0.08f;
    private const float MinActivityRadius = 4f;

    private const float BoundaryAvoidanceMargin = 5f;
    private const float BoundaryAvoidanceStrength = 0.95f;
    private const float FoodCrowdingRadiusMultiplier = 1.15f;
    private const float FoodCrowdingPenaltyStrength = 0.65f;
    private const float ActiveFoodTrailScrub = 0.10f;
    private const float EmptyFoodTrailScrub = 0.18f;

    private const float FeedDurationMin = 0.35f;
    private const float FeedDurationMax = 0.85f;
    private const float ReturnDurationMin = 1.2f;
    private const float ReturnDurationMax = 3.0f;
    private const float FeedJitterStrength = 0.025f;

    private const float ReturnFoodRepulsionStrength = 0.45f;
    private const float ReturnNoiseMultiplier = 0.30f;

    private const float LoopCurvatureSampleRadius = 1.2f;
    private const float LoopPruneRadius = 1.0f;

    private const float ExitHubDurationMin = 0.65f;
    private const float ExitHubDurationMax = 1.35f;
    private const float ExitHubSteerStrength = 1.75f;
    private const float ExitHubSpeedMultiplier = 1.18f;
    private const float ExitHubNoiseMultiplier = 0.22f;
    private const float HubRingScrubStrength = 0.22f;
    private const float HubSeekDepositSuppression = 0.08f;

    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private NeuralFoodNodeState[] foodNodes = Array.Empty<NeuralFoodNodeState>();
    private int[] foodConsumerCounts = Array.Empty<int>();
    private bool[] foodDepletionLogged = Array.Empty<bool>();
    private bool[] foodIsDepleted = Array.Empty<bool>();
    private float[] foodRegrowDelayTimers = Array.Empty<float>();

    private float[] agentTrailConfidence = Array.Empty<float>();
    private float[] agentFoodCommitment = Array.Empty<float>();

    private AgentMode[] agentModes = Array.Empty<AgentMode>();
    private float[] agentModeTimers = Array.Empty<float>();
    private int[] agentLastFoodIndex = Array.Empty<int>();

    private float simulationTime;
    private float nextOccupiedFoodLogTime;

    private SeededRng rng;
    private Vector2 mapSize;

    private bool allowFoodRegrowth;
    private float foodRegrowDelaySeconds;
    private float foodRegrowPerSecondFraction;
    private float foodReactivationThreshold;

    private bool useColonyHub;
    private Vector2 colonyHub;
    private float colonyHubRadius;
    private float returnToHubWeight;
    private float returnTrailBlend;
    private float returnDepositBoost;
    private float successfulReturnDepositBurst;
    private float hubInfluenceRadius;
    private float nonUsefulLoopPruneStrength;
    private float nonUsefulLoopTrailThreshold;
    private float nonUsefulLoopCurvatureThreshold;

    private float seekExplorationDepositMultiplier;
    private float seekTransportDepositMultiplier;
    private float returnExplorationDepositMultiplier;
    private float explorationFollowWeight;
    private float transportFollowWeight;
    private float returnTransportFollowWeight;
    private float returnExplorationFollowWeight;
    private float explorationDecayMultiplier;
    private float explorationDiffusionMultiplier;

    public NeuralFieldGrid Field { get; private set; }
    public NeuralFieldGrid ExplorationField { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public NeuralFoodNodeState[] FoodNodes => foodNodes;
    public NeuralObstacle[] Obstacles => Array.Empty<NeuralObstacle>();

    public int Seed { get; private set; }
    public int AgentCount => agents?.Length ?? 0;

    public Vector2 ActivityCenter { get; private set; }
    public float ActivityRadius { get; private set; }

    public bool UseColonyHub => useColonyHub;
    public Vector2 ColonyHub => colonyHub;
    public float ColonyHubRadius => colonyHubRadius;

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
        bool allowFoodRegrowth,
        float regrowDelaySeconds,
        float regrowRate,
        float reactivationThreshold,
        bool spawnFoodFromSeed,
        NeuralFoodNodeConfig[] manualFoodNodes,
        bool useColonyHub,
        Vector2 colonyHub,
        float colonyHubRadius,
        float returnToHubWeight,
        float returnTrailBlend,
        float returnDepositBoost,
        float successfulReturnDepositBurst,
        float hubInfluenceRadius,
        float nonUsefulLoopPruneStrength,
        float nonUsefulLoopTrailThreshold,
        float nonUsefulLoopCurvatureThreshold,
        float seekExplorationDepositMultiplier,
        float seekTransportDepositMultiplier,
        float returnExplorationDepositMultiplier,
        float explorationFollowWeight,
        float transportFollowWeight,
        float returnTransportFollowWeight,
        float returnExplorationFollowWeight,
        float explorationDecayMultiplier,
        float explorationDiffusionMultiplier)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);

        this.allowFoodRegrowth = allowFoodRegrowth;
        foodRegrowDelaySeconds = Mathf.Max(0f, regrowDelaySeconds);
        foodRegrowPerSecondFraction = Mathf.Max(0f, regrowRate);
        foodReactivationThreshold = Mathf.Clamp01(reactivationThreshold);

        this.useColonyHub = useColonyHub;
        this.colonyHub = ClampNodeInsideBounds(colonyHub);
        this.colonyHubRadius = Mathf.Max(0.25f, colonyHubRadius);
        this.returnToHubWeight = Mathf.Max(0f, returnToHubWeight);
        this.returnTrailBlend = Mathf.Clamp01(returnTrailBlend);
        this.returnDepositBoost = Mathf.Max(0f, returnDepositBoost);
        this.successfulReturnDepositBurst = Mathf.Max(0f, successfulReturnDepositBurst);
        this.hubInfluenceRadius = Mathf.Max(this.colonyHubRadius, hubInfluenceRadius);
        this.nonUsefulLoopPruneStrength = Mathf.Max(0f, nonUsefulLoopPruneStrength);
        this.nonUsefulLoopTrailThreshold = Mathf.Max(0f, nonUsefulLoopTrailThreshold);
        this.nonUsefulLoopCurvatureThreshold = Mathf.Max(0f, nonUsefulLoopCurvatureThreshold);

        this.seekExplorationDepositMultiplier = Mathf.Max(0f, seekExplorationDepositMultiplier);
        this.seekTransportDepositMultiplier = Mathf.Max(0f, seekTransportDepositMultiplier);
        this.returnExplorationDepositMultiplier = Mathf.Max(0f, returnExplorationDepositMultiplier);
        this.explorationFollowWeight = Mathf.Max(0f, explorationFollowWeight);
        this.transportFollowWeight = Mathf.Max(0f, transportFollowWeight);
        this.returnTransportFollowWeight = Mathf.Max(0f, returnTransportFollowWeight);
        this.returnExplorationFollowWeight = Mathf.Max(0f, returnExplorationFollowWeight);
        this.explorationDecayMultiplier = Mathf.Max(0.1f, explorationDecayMultiplier);
        this.explorationDiffusionMultiplier = Mathf.Max(0.1f, explorationDiffusionMultiplier);

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, false);
        ExplorationField = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, false);

        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];
        agentTrailConfidence = new float[agents.Length];
        agentFoodCommitment = new float[agents.Length];
        agentModes = new AgentMode[agents.Length];
        agentModeTimers = new float[agents.Length];
        agentLastFoodIndex = new int[agents.Length];

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
        ActivityCenter = Vector2.zero;
        ActivityRadius = MinActivityRadius;

        for (var i = 0; i < agents.Length; i++)
        {
            var radial = rng.InsideUnitCircle();
            var pos = new Vector2(
                radial.x * this.mapSize.x * 0.48f,
                radial.y * this.mapSize.y * 0.48f);

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

            agentTrailConfidence[i] = 0f;
            agentFoodCommitment[i] = 0f;
            agentModes[i] = AgentMode.SeekFood;
            agentModeTimers[i] = 0f;
            agentLastFoodIndex[i] = -1;
        }

        UnityEngine.Debug.Log($"[NeuralSlimeMold] Initialized {foodNodes.Length} food nodes.");
    }

    public void Tick(float dt, float diffusion, float decay, float foodStrength, float explorationTurnNoise)
    {
        if (agents == null || Field == null || ExplorationField == null)
        {
            return;
        }

        if (foodConsumerCounts == null || foodConsumerCounts.Length != foodNodes.Length)
        {
            foodConsumerCounts = new int[foodNodes.Length];
        }

        Array.Clear(foodConsumerCounts, 0, foodConsumerCounts.Length);
        simulationTime += Mathf.Max(0f, dt);

        Vector2 activityAccumulator = Vector2.zero;
        var maxDistanceFromCenter = 0f;

        for (var i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            var mode = agentModes[i];
            var modeTimer = Mathf.Max(0f, agentModeTimers[i] - Mathf.Max(0f, dt));
            var lastFoodIndex = agentLastFoodIndex[i];

            var activeFoodIndex = GetContainingFoodNodeIndex(agent.position, true);
            var emptyFoodIndex = GetContainingEmptyFoodNodeIndex(agent.position);

            if (mode == AgentMode.Feed && (activeFoodIndex < 0 || modeTimer <= 0f))
            {
                mode = useColonyHub ? AgentMode.ReturnToHub : AgentMode.SeekFood;
                modeTimer = useColonyHub ? rng.Range(ReturnDurationMin, ReturnDurationMax) : 0f;
            }

            if (mode == AgentMode.ReturnToHub && modeTimer <= 0f)
            {
                mode = AgentMode.SeekFood;
                modeTimer = 0f;
                lastFoodIndex = -1;
            }

            if (mode == AgentMode.ExitHub && (modeTimer <= 0f || !IsNearHub(agent.position)))
            {
                mode = AgentMode.SeekFood;
                modeTimer = 0f;
                lastFoodIndex = -1;
            }

            if (mode == AgentMode.SeekFood && activeFoodIndex >= 0)
            {
                mode = AgentMode.Feed;
                modeTimer = rng.Range(FeedDurationMin, FeedDurationMax);
                lastFoodIndex = activeFoodIndex;
            }

            switch (mode)
            {
                case AgentMode.Feed:
                    HandleFeedMode(ref agent, activeFoodIndex >= 0 ? activeFoodIndex : lastFoodIndex, dt);
                    MarkConsumingFoodNodes(agent.position);
                    Field.ScrubAt(agent.position, ActiveFoodTrailScrub);
                    ExplorationField.ScrubAt(agent.position, ActiveFoodTrailScrub);
                    break;

                case AgentMode.ReturnToHub:
                    HandleReturnToHubMode(ref agent, lastFoodIndex, dt, explorationTurnNoise);

                    if (GetContainingFoodNodeIndex(agent.position, true) >= 0)
                    {
                        Field.ScrubAt(agent.position, ActiveFoodTrailScrub);
                        ExplorationField.ScrubAt(agent.position, ActiveFoodTrailScrub);
                    }

                    Field.DepositKernel(agent.position, agent.depositAmount * returnDepositBoost);
                    if (returnExplorationDepositMultiplier > 0f)
                    {
                        ExplorationField.DepositKernel(agent.position, agent.depositAmount * returnExplorationDepositMultiplier);
                    }

                    if (useColonyHub && IsInsideHub(agent.position))
                    {
                        Field.DepositDisc(agent.position, colonyHubRadius * 0.95f, successfulReturnDepositBurst);
                        Field.ScrubDisc(agent.position, colonyHubRadius * 0.75f, HubRingScrubStrength);

                        mode = AgentMode.ExitHub;
                        modeTimer = rng.Range(ExitHubDurationMin, ExitHubDurationMax);
                        agent.heading = Mathf.Repeat(
                            Mathf.Atan2(agent.position.y - colonyHub.y, agent.position.x - colonyHub.x),
                            Mathf.PI * 2f);
                    }

                    break;

                case AgentMode.ExitHub:
                    HandleExitHubMode(ref agent, dt, explorationTurnNoise);
                    Field.ScrubDisc(agent.position, colonyHubRadius * 0.65f, HubRingScrubStrength * Mathf.Max(0f, dt) * 6f);
                    break;

                default:
                    HandleSeekMode(ref agent, i, dt, foodStrength, explorationTurnNoise, emptyFoodIndex >= 0);

                    activeFoodIndex = GetContainingFoodNodeIndex(agent.position, true);
                    emptyFoodIndex = GetContainingEmptyFoodNodeIndex(agent.position);

                    if (activeFoodIndex >= 0)
                    {
                        Field.ScrubAt(agent.position, ActiveFoodTrailScrub);
                        ExplorationField.ScrubAt(agent.position, ActiveFoodTrailScrub);
                    }
                    else if (emptyFoodIndex >= 0)
                    {
                        Field.ScrubAt(agent.position, EmptyFoodTrailScrub);
                        ExplorationField.ScrubAt(agent.position, EmptyFoodTrailScrub);
                    }

                    var movedTransportTrail = Field.SampleBilinear(agent.position);
                    var depositMultiplier = ComputeSeekTransportDepositMultiplier(
                        agent.position,
                        movedTransportTrail,
                        agentTrailConfidence[i],
                        activeFoodIndex >= 0,
                        emptyFoodIndex >= 0,
                        agentFoodCommitment[i]);

                    if (seekExplorationDepositMultiplier > 0f)
                    {
                        ExplorationField.DepositKernel(agent.position, agent.depositAmount * seekExplorationDepositMultiplier);
                    }

                    if (depositMultiplier > 0f)
                    {
                        Field.DepositKernel(agent.position, agent.depositAmount * depositMultiplier);
                    }

                    ApplyNonUsefulLoopPrune(
                        agent.position,
                        activeFoodIndex >= 0,
                        mode == AgentMode.ReturnToHub,
                        dt);

                    break;
            }

            ApplyReflectBoundary(ref agent.position, ref agent.heading);

            agents[i] = agent;
            agentModes[i] = mode;
            agentModeTimers[i] = modeTimer;
            agentLastFoodIndex[i] = lastFoodIndex;

            activityAccumulator += agent.position;
        }

        ConsumeFood(dt);
        LogOccupiedFoodLevels();
        Field.Step(diffusion, decay, dt);
        ExplorationField.Step(
            Mathf.Clamp01(diffusion * explorationDiffusionMultiplier),
            decay * explorationDecayMultiplier,
            dt);

        if (agents.Length > 0)
        {
            var rawCenter = activityAccumulator / agents.Length;
            ActivityCenter = Vector2.Lerp(ActivityCenter, rawCenter, ActivitySmoothing);

            for (var i = 0; i < agents.Length; i++)
            {
                var dist = Vector2.Distance(agents[i].position, ActivityCenter);
                if (dist > maxDistanceFromCenter)
                {
                    maxDistanceFromCenter = dist;
                }
            }

            ActivityRadius = Mathf.Max(MinActivityRadius, Mathf.Lerp(ActivityRadius, maxDistanceFromCenter, ActivitySmoothing));
        }
    }

    private void HandleSeekMode(ref NeuralSlimeMoldAgent agent, int agentIndex, float dt, float foodStrength, float explorationTurnNoise, bool nearEmptyFood)
    {
        var leftDir = Direction(agent.heading - agent.sensorAngle);
        var centerDir = Direction(agent.heading);
        var rightDir = Direction(agent.heading + agent.sensorAngle);

        var leftSamplePos = agent.position + (leftDir * agent.sensorDistance);
        var centerSamplePos = agent.position + (centerDir * agent.sensorDistance);
        var rightSamplePos = agent.position + (rightDir * agent.sensorDistance);

        var leftExploration = ExplorationField.SampleBilinear(leftSamplePos);
        var centerExploration = ExplorationField.SampleBilinear(centerSamplePos);
        var rightExploration = ExplorationField.SampleBilinear(rightSamplePos);

        var leftTransport = Field.SampleBilinear(leftSamplePos);
        var centerTransport = Field.SampleBilinear(centerSamplePos);
        var rightTransport = Field.SampleBilinear(rightSamplePos);
        var localTransport = Field.SampleBilinear(agent.position);

        var leftTrail = (leftExploration * explorationFollowWeight) + (leftTransport * transportFollowWeight);
        var centerTrail = (centerExploration * explorationFollowWeight) + (centerTransport * transportFollowWeight);
        var rightTrail = (rightExploration * explorationFollowWeight) + (rightTransport * transportFollowWeight);

        var leftNutrient = SampleNutrientField(leftSamplePos);
        var centerNutrient = SampleNutrientField(centerSamplePos);
        var rightNutrient = SampleNutrientField(rightSamplePos);

        var weightedTrail = (leftTrail * agent.controller.leftWeight)
                          + (centerTrail * agent.controller.centerWeight)
                          + (rightTrail * agent.controller.rightWeight);

        var trailEdge = rightTrail - leftTrail;
        var trailCenterBias = centerTrail - ((leftTrail + rightTrail) * 0.5f);

        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * Mathf.Max(0.1f, agent.controller.noiseWeight * 8f);

        var trailConfidence = ComputeTrailConfidence(localTransport, centerTrail, leftTrail, rightTrail);
        agentTrailConfidence[agentIndex] = Mathf.Lerp(agentTrailConfidence[agentIndex], trailConfidence, 0.18f);

        var trailSteer =
            (weightedTrail * 0.12f) +
            trailEdge +
            (trailCenterBias * (0.8f + agent.controller.densityWeight)) +
            (randomTurn * Mathf.Lerp(1f, 0.25f, agentTrailConfidence[agentIndex]));

        var nutrientEdge = rightNutrient - leftNutrient;
        var nutrientCenterBias = centerNutrient - ((leftNutrient + rightNutrient) * 0.5f);
        var nutrientSteer = (nutrientEdge * 1.1f) + (nutrientCenterBias * 0.7f);

        var nutrientStrength = Mathf.Clamp01(Mathf.Max(leftNutrient, Mathf.Max(centerNutrient, rightNutrient)));
        var steer = trailSteer;

        if (nutrientStrength > 0.0001f)
        {
            agentFoodCommitment[agentIndex] = Mathf.Lerp(agentFoodCommitment[agentIndex], nutrientStrength, 0.18f);
            var nutrientBlend = Mathf.Clamp01((nutrientStrength * 0.75f) + (agentFoodCommitment[agentIndex] * 0.25f));
            steer = Mathf.Lerp(trailSteer * 0.55f, nutrientSteer * Mathf.Max(0.5f, foodStrength), nutrientBlend);
        }
        else if (nearEmptyFood)
        {
            var awayTurn = ComputeAwayFromEmptyFoodTurn(agent.position, agent.heading);
            agentFoodCommitment[agentIndex] = Mathf.Lerp(agentFoodCommitment[agentIndex], 0f, 0.25f);
            steer = (awayTurn * 1.2f) + randomTurn;
        }
        else
        {
            agentFoodCommitment[agentIndex] = Mathf.Lerp(agentFoodCommitment[agentIndex], 0f, 0.08f);

            if (agentTrailConfidence[agentIndex] > 0.55f)
            {
                steer = Mathf.Lerp(randomTurn * 0.1f, trailSteer, 0.92f);
            }
        }

        if (IsNearHub(agent.position))
        {
            steer += ComputeSteerAwayFromPoint(agent.position, agent.heading, colonyHub) * 0.9f;
        }

        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        var speedMod = 1f;

        if (localTransport >= StrongTrailHighwayThreshold)
        {
            speedMod *= 1.12f;
        }
        else if (localTransport >= MinTrailForHighway)
        {
            speedMod *= 1.04f;
        }

        if (nearEmptyFood)
        {
            speedMod *= 1.25f;
        }

        if (IsNearHub(agent.position))
        {
            speedMod *= 1.08f;
        }

        agent.position += Direction(agent.heading) * (agent.speed * speedMod * dt);
    }

    private void HandleFeedMode(ref NeuralSlimeMoldAgent agent, int foodIndex, float dt)
    {
        if (foodIndex < 0 || foodIndex >= foodNodes.Length)
        {
            return;
        }

        var node = foodNodes[foodIndex];
        var toFood = node.position - agent.position;
        var dist = toFood.magnitude;
        var radius = Mathf.Max(0.01f, node.consumeRadius);

        Vector2 dirToFood;
        if (dist > 0.0001f)
        {
            dirToFood = toFood / dist;
        }
        else
        {
            dirToFood = Direction(agent.heading);
        }

        agent.heading = Mathf.Repeat(Mathf.Atan2(dirToFood.y, dirToFood.x), Mathf.PI * 2f);

        var arrival01 = Mathf.Clamp01(dist / radius);
        var crowdPenalty = ComputeFoodCrowdingPenalty(foodIndex);

        var feedMoveSpeed =
            agent.speed *
            Mathf.Lerp(0.08f, 0.38f, arrival01) *
            Mathf.Lerp(1f, 1f - 0.22f, crowdPenalty);

        agent.position = Vector2.MoveTowards(agent.position, node.position, feedMoveSpeed * Mathf.Max(0f, dt));

        if (dist <= radius * 0.18f)
        {
            var tangent = new Vector2(-dirToFood.y, dirToFood.x);
            var signed = (rng.NextFloat01() * 2f) - 1f;
            agent.position += tangent * (signed * FeedJitterStrength);
        }
    }

    private void HandleReturnToHubMode(ref NeuralSlimeMoldAgent agent, int lastFoodIndex, float dt, float explorationTurnNoise)
    {
        var leftDir = Direction(agent.heading - agent.sensorAngle);
        var centerDir = Direction(agent.heading);
        var rightDir = Direction(agent.heading + agent.sensorAngle);

        var leftSamplePos = agent.position + (leftDir * agent.sensorDistance);
        var centerSamplePos = agent.position + (centerDir * agent.sensorDistance);
        var rightSamplePos = agent.position + (rightDir * agent.sensorDistance);

        var leftTransport = Field.SampleBilinear(leftSamplePos);
        var centerTransport = Field.SampleBilinear(centerSamplePos);
        var rightTransport = Field.SampleBilinear(rightSamplePos);

        var leftExploration = ExplorationField.SampleBilinear(leftSamplePos);
        var centerExploration = ExplorationField.SampleBilinear(centerSamplePos);
        var rightExploration = ExplorationField.SampleBilinear(rightSamplePos);

        var leftTrail = (leftTransport * returnTransportFollowWeight) + (leftExploration * returnExplorationFollowWeight);
        var centerTrail = (centerTransport * returnTransportFollowWeight) + (centerExploration * returnExplorationFollowWeight);
        var rightTrail = (rightTransport * returnTransportFollowWeight) + (rightExploration * returnExplorationFollowWeight);

        var trailEdge = rightTrail - leftTrail;
        var trailCenterBias = centerTrail - ((leftTrail + rightTrail) * 0.5f);
        var trailSteer = trailEdge + (trailCenterBias * 0.95f);

        var hubSteer = 0f;
        if (useColonyHub)
        {
            hubSteer = ComputeSteerTowardPoint(agent.position, agent.heading, colonyHub) * returnToHubWeight;
        }

        var awayFoodSteer = 0f;
        if (lastFoodIndex >= 0 && lastFoodIndex < foodNodes.Length)
        {
            awayFoodSteer = ComputeSteerAwayFromPoint(agent.position, agent.heading, foodNodes[lastFoodIndex].position) * ReturnFoodRepulsionStrength;
        }

        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * ReturnNoiseMultiplier;

        var steer = Mathf.Lerp(hubSteer + awayFoodSteer, trailSteer + hubSteer, returnTrailBlend) + randomTurn;
        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        var speedMod = 1.10f;
        if (centerTransport >= StrongTrailHighwayThreshold)
        {
            speedMod *= 1.10f;
        }
        else if (centerTransport >= MinTrailForHighway)
        {
            speedMod *= 1.04f;
        }

        agent.position += Direction(agent.heading) * (agent.speed * speedMod * dt);
    }

    private void HandleExitHubMode(ref NeuralSlimeMoldAgent agent, float dt, float explorationTurnNoise)
    {
        var outwardSteer = ComputeSteerAwayFromPoint(agent.position, agent.heading, colonyHub) * ExitHubSteerStrength;
        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * ExitHubNoiseMultiplier;

        var steer = outwardSteer + randomTurn;
        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        agent.position += Direction(agent.heading) * (agent.speed * ExitHubSpeedMultiplier * dt);
    }

    private float SampleNutrientField(Vector2 position)
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return 0f;
        }

        var total = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (foodIsDepleted[i] || node.currentCapacity <= 0f || node.capacity <= 0f)
            {
                continue;
            }

            var fill = Mathf.Clamp01(node.currentCapacity / node.capacity);
            var effectiveStrength = node.baseStrength * Mathf.Lerp(0.35f, 1f, fill);
            if (effectiveStrength <= MinFoodStrengthFloor)
            {
                continue;
            }

            var toNode = node.position - position;
            var distance = toNode.magnitude;

            var seekRange = Mathf.Max(node.consumeRadius * FoodSeekRangeMultiplier, MinFoodSeekRange);
            if (distance >= seekRange)
            {
                continue;
            }

            var outer01 = 1f - Mathf.Clamp01(distance / seekRange);
            var innerSuppression = Mathf.InverseLerp(node.consumeRadius * 0.55f, node.consumeRadius * 1.1f, distance);
            innerSuppression = Mathf.Clamp01(innerSuppression);

            var crowdPenalty = ComputeFoodCrowdingPenalty(i);
            var crowdFactor = Mathf.Lerp(1f, 0.45f, crowdPenalty);

            total += effectiveStrength * outer01 * innerSuppression * crowdFactor;
        }

        return Mathf.Clamp01(total);
    }

    private float ComputeSeekTransportDepositMultiplier(
        Vector2 position,
        float localTrail,
        float trailConfidence,
        bool onActiveFood,
        bool onEmptyFood,
        float foodCommitment)
    {
        if (onActiveFood || onEmptyFood || seekTransportDepositMultiplier <= 0f)
        {
            return 0f;
        }

        var multiplier = localTrail < MinTrailForHighway ? WeakTrailDepositPenalty : 1f;

        if (localTrail >= StrongTrailHighwayThreshold)
        {
            multiplier *= Mathf.Lerp(1.2f, MaxHighwayDepositBoost, trailConfidence);
        }
        else if (localTrail >= MinTrailForHighway)
        {
            multiplier *= Mathf.Lerp(1.0f, 1.35f, trailConfidence);
        }

        multiplier *= Mathf.Lerp(1f, 1.14f, foodCommitment);
        multiplier *= seekTransportDepositMultiplier;

        if (IsNearHub(position))
        {
            multiplier *= HubSeekDepositSuppression;
        }

        return Mathf.Max(0f, multiplier);
    }

    private float ComputeTrailConfidence(float localTrail, float center, float left, float right)
    {
        var directionality = Mathf.Clamp01(center - ((left + right) * 0.5f) + 0.5f);
        var density = Mathf.Clamp01(localTrail * 6f);
        var centerStrength = Mathf.Clamp01(center * 5f);
        return Mathf.Clamp01((density * 0.45f) + (centerStrength * 0.35f) + (directionality * 0.20f));
    }

    private float ComputeBoundaryAvoidanceTurn(Vector2 position, float heading)
    {
        var halfX = mapSize.x * 0.5f;
        var halfY = mapSize.y * 0.5f;

        var away = Vector2.zero;

        var leftDistance = position.x + halfX;
        var rightDistance = halfX - position.x;
        var bottomDistance = position.y + halfY;
        var topDistance = halfY - position.y;

        if (leftDistance < BoundaryAvoidanceMargin)
        {
            away.x += 1f - Mathf.Clamp01(leftDistance / BoundaryAvoidanceMargin);
        }

        if (rightDistance < BoundaryAvoidanceMargin)
        {
            away.x -= 1f - Mathf.Clamp01(rightDistance / BoundaryAvoidanceMargin);
        }

        if (bottomDistance < BoundaryAvoidanceMargin)
        {
            away.y += 1f - Mathf.Clamp01(bottomDistance / BoundaryAvoidanceMargin);
        }

        if (topDistance < BoundaryAvoidanceMargin)
        {
            away.y -= 1f - Mathf.Clamp01(topDistance / BoundaryAvoidanceMargin);
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        away.Normalize();

        var forward = Direction(heading);
        var cross = (forward.x * away.y) - (forward.y * away.x);
        var dot = Vector2.Dot(forward, away);
        var signedAngle = Mathf.Atan2(cross, dot);
        return Mathf.Clamp(signedAngle / Mathf.PI, -1f, 1f) * BoundaryAvoidanceStrength;
    }

    private float ComputeFoodCrowdingPenalty(int foodIndex)
    {
        if (foodIndex < 0 || foodIndex >= foodNodes.Length)
        {
            return 0f;
        }

        var node = foodNodes[foodIndex];
        var radius = Mathf.Max(0.01f, node.consumeRadius * FoodCrowdingRadiusMultiplier);
        var radiusSqr = radius * radius;

        var crowdCount = 0;
        for (var i = 0; i < agents.Length; i++)
        {
            if ((agents[i].position - node.position).sqrMagnitude <= radiusSqr)
            {
                crowdCount++;
            }
        }

        var normalized = Mathf.Clamp01(crowdCount / 32f);
        return normalized * FoodCrowdingPenaltyStrength;
    }

    private void ConsumeFood(float dt)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var maxCapacity = Mathf.Max(0.01f, node.capacity);

            if (!foodIsDepleted[i])
            {
                var consumed = node.consumeRate * foodConsumerCounts[i] * Mathf.Max(0f, dt);
                var previousCapacity = node.currentCapacity;
                node.currentCapacity = Mathf.Clamp(node.currentCapacity - consumed, 0f, maxCapacity);

                if (!foodDepletionLogged[i] && previousCapacity > 0f && node.currentCapacity <= 0f)
                {
                    foodDepletionLogged[i] = true;
                    foodIsDepleted[i] = true;
                    foodRegrowDelayTimers[i] = foodRegrowDelaySeconds;
                    UnityEngine.Debug.Log($"[NeuralSlimeMold] Food node {i} depleted at t={simulationTime:F2}s.");
                }
            }
            else
            {
                if (!allowFoodRegrowth)
                {
                    node.currentCapacity = 0f;
                }
                else if (foodRegrowDelayTimers[i] > 0f)
                {
                    foodRegrowDelayTimers[i] -= Mathf.Max(0f, dt);
                }
                else
                {
                    var regrowAmount = maxCapacity * foodRegrowPerSecondFraction * Mathf.Max(0f, dt);
                    node.currentCapacity = Mathf.Min(maxCapacity, node.currentCapacity + regrowAmount);

                    if (node.currentCapacity >= maxCapacity * foodReactivationThreshold)
                    {
                        foodIsDepleted[i] = false;
                        foodDepletionLogged[i] = false;
                        UnityEngine.Debug.Log($"[NeuralSlimeMold] Food node {i} reactivated at t={simulationTime:F2}s.");
                    }
                }
            }

            foodNodes[i] = node;
        }
    }

    private void MarkConsumingFoodNodes(Vector2 agentPosition)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (node.currentCapacity <= 0f || foodIsDepleted[i])
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            var distanceSqr = (agentPosition - node.position).sqrMagnitude;
            if (distanceSqr <= radius * radius)
            {
                foodConsumerCounts[i]++;
            }
        }
    }

    private int GetContainingEmptyFoodNodeIndex(Vector2 agentPosition)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (!foodIsDepleted[i] && node.currentCapacity > 0f)
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            if ((agentPosition - node.position).sqrMagnitude <= radius * radius)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetContainingFoodNodeIndex(Vector2 agentPosition, bool requireNonEmpty)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];

            if (requireNonEmpty && (foodIsDepleted[i] || node.currentCapacity <= 0f))
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            if ((agentPosition - node.position).sqrMagnitude <= radius * radius)
            {
                return i;
            }
        }

        return -1;
    }

    private float ComputeAwayFromEmptyFoodTurn(Vector2 position, float heading)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (!foodIsDepleted[i] && node.currentCapacity > 0f)
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius);
            var toNode = node.position - position;
            if (toNode.sqrMagnitude > radius * radius)
            {
                continue;
            }

            var away = (position - node.position).normalized;
            var forward = Direction(heading);
            var cross = (forward.x * away.y) - (forward.y * away.x);
            var dot = Vector2.Dot(forward, away);
            var signedAngle = Mathf.Atan2(cross, dot);
            return Mathf.Clamp(signedAngle / Mathf.PI, -1f, 1f);
        }

        return 0f;
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
                UnityEngine.Debug.Log($"[NeuralSlimeMold] t={simulationTime:F1}s occupied food capacities follow.");
                loggedAny = true;
            }

            UnityEngine.Debug.Log($"[NeuralSlimeMold] node={i} consumers={foodConsumerCounts[i]} capacity={node.currentCapacity:F2}/{Mathf.Max(0.01f, node.capacity):F2}");
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
            foodIsDepleted = new bool[foodNodes.Length];
            foodRegrowDelayTimers = new float[foodNodes.Length];
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
        foodIsDepleted = new bool[foodNodes.Length];
        foodRegrowDelayTimers = new float[foodNodes.Length];
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

    private void ApplyNonUsefulLoopPrune(Vector2 position, bool onActiveFood, bool returningToHub, float dt)
    {
        if (returningToHub || onActiveFood || nonUsefulLoopPruneStrength <= 0f)
        {
            return;
        }

        if (IsNearHub(position) || IsNearAnyActiveFood(position, hubInfluenceRadius * 0.65f))
        {
            return;
        }

        var localTrail = Field.SampleBilinear(position);
        if (localTrail < nonUsefulLoopTrailThreshold)
        {
            return;
        }

        var curvature = Field.EstimateLocalCurvature(position, LoopCurvatureSampleRadius);
        if (curvature < nonUsefulLoopCurvatureThreshold)
        {
            return;
        }

        Field.ScrubDisc(position, LoopPruneRadius, nonUsefulLoopPruneStrength * Mathf.Max(0f, dt));
    }

    private bool IsInsideHub(Vector2 position)
    {
        if (!useColonyHub)
        {
            return false;
        }

        return (position - colonyHub).sqrMagnitude <= (colonyHubRadius * colonyHubRadius);
    }

    private bool IsNearHub(Vector2 position)
    {
        if (!useColonyHub)
        {
            return false;
        }

        var radius = Mathf.Max(colonyHubRadius, hubInfluenceRadius);
        return (position - colonyHub).sqrMagnitude <= (radius * radius);
    }

    private bool IsNearAnyActiveFood(Vector2 position, float extraRadius)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (foodIsDepleted[i] || node.currentCapacity <= 0f)
            {
                continue;
            }

            var radius = Mathf.Max(0.01f, node.consumeRadius + extraRadius);
            if ((position - node.position).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private float ComputeSteerTowardPoint(Vector2 position, float heading, Vector2 target)
    {
        var toTarget = target - position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        toTarget.Normalize();
        var forward = Direction(heading);
        var cross = (forward.x * toTarget.y) - (forward.y * toTarget.x);
        var dot = Vector2.Dot(forward, toTarget);
        var signedAngle = Mathf.Atan2(cross, dot);
        return Mathf.Clamp(signedAngle / Mathf.PI, -1f, 1f);
    }

    private float ComputeSteerAwayFromPoint(Vector2 position, float heading, Vector2 source)
    {
        var away = position - source;
        if (away.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        away.Normalize();
        var forward = Direction(heading);
        var cross = (forward.x * away.y) - (forward.y * away.x);
        var dot = Vector2.Dot(forward, away);
        var signedAngle = Mathf.Atan2(cross, dot);
        return Mathf.Clamp(signedAngle / Mathf.PI, -1f, 1f);
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

// =================================
// FILE: NeuralSlimeMoldBootstrap.cs
// =================================
using UnityEngine;

public sealed class NeuralSlimeMoldBootstrap : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int agentCount = 600;
    [SerializeField] private Vector2 mapSize = new(64f, 64f);
    [SerializeField] private Vector2Int trailResolution = new(256, 256);
    [SerializeField, Min(0f)] private float trailDecayPerSecond = 0.6f;
    [SerializeField, Range(0f, 1f)] private float trailDiffusion = 0.23f;

    [Header("Agent Motion")]
    [SerializeField] private float sensorAngleDegrees = 35f;
    [SerializeField] private float sensorDistance = 1.4f;
    [SerializeField] private float speed = 6.7f;
    [SerializeField] private float turnRateDegrees = 180f;
    [SerializeField, Min(0f)] private float depositAmount = 1.2f;
    [SerializeField, Min(0f)] private float explorationTurnNoise = 0.08f;

    [Header("Food")]
    [SerializeField, Min(1)] private int foodNodeCount = 10;
    [SerializeField, Min(0f)] private float foodStrength = 1.1f;
    [SerializeField, Min(0f)] private float foodCapacity = 2000f;
    [SerializeField, Min(0f)] private float consumeRadius = 6f;
    [SerializeField, Min(0f)] private float consumeRate = 0.25f;
    [SerializeField] private bool allowFoodRegrowth = true;
    [SerializeField, Min(0f)] private float foodReactivationDelay = 10f;
    [SerializeField, Min(0f)] private float regrowRate = 0.08f;
    [SerializeField, Range(0f, 1f)] private float foodReactivationThreshold = 0.25f;
    [SerializeField] private bool spawnFromSeed = true;
    [SerializeField] private NeuralFoodNodeConfig[] manualFoodConfigs;

    [Header("Colony Hub")]
    [SerializeField] private bool useColonyHub = true;
    [SerializeField] private Vector2 colonyHub = Vector2.zero;
    [SerializeField, Min(0.25f)] private float colonyHubRadius = 4.5f;
    [SerializeField, Min(0f)] private float returnToHubWeight = 1.9f;
    [SerializeField, Range(0f, 1f)] private float returnTrailBlend = 0.55f;
    [SerializeField, Min(0f)] private float returnDepositBoost = 2.8f;
    [SerializeField, Min(0f)] private float successfulReturnDepositBurst = 8.0f;
    [SerializeField, Min(0f)] private float hubInfluenceRadius = 9f;

    [Header("Loop Pruning")]
    [SerializeField, Min(0f)] private float nonUsefulLoopPruneStrength = 0.22f;
    [SerializeField, Min(0f)] private float nonUsefulLoopTrailThreshold = 0.13f;
    [SerializeField, Min(0f)] private float nonUsefulLoopCurvatureThreshold = 0.085f;

    [Header("Dual Field")]
    [SerializeField, Min(0f)] private float seekExplorationDepositMultiplier = 0.95f;
    [SerializeField, Min(0f)] private float seekTransportDepositMultiplier = 0.10f;
    [SerializeField, Min(0f)] private float returnExplorationDepositMultiplier = 0.20f;
    [SerializeField, Min(0f)] private float explorationFollowWeight = 1.00f;
    [SerializeField, Min(0f)] private float transportFollowWeight = 0.28f;
    [SerializeField, Min(0f)] private float returnTransportFollowWeight = 1.00f;
    [SerializeField, Min(0f)] private float returnExplorationFollowWeight = 0.15f;
    [SerializeField, Min(0.1f)] private float explorationDecayMultiplier = 1.9f;
    [SerializeField, Min(0.1f)] private float explorationDiffusionMultiplier = 1.15f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;
    [SerializeField] private Color backgroundColor = new(0.10f, 0.09f, 0.07f, 1f);

    [Header("Rendering")]
    [SerializeField] private bool showFoodMarkers = true;

    [Header("Camera Framing")]
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField] private bool adaptiveCameraFraming = true;
    [SerializeField, Min(0.1f)] private float cameraPadding = 1.1f;
    [SerializeField, Min(0.01f)] private float cameraFollowSmooth = 3.5f;
    [SerializeField, Min(0.01f)] private float cameraZoomSmooth = 2.8f;
    [SerializeField, Min(1f)] private float minimumCameraSize = 8f;
    [SerializeField, Range(0f, 1f)] private float cameraLookAheadToActivity = 0.12f;
    [SerializeField, Min(0f)] private float cameraDeadZoneRadius = 0.75f;

    private NeuralSlimeMoldRunner runner;
    private NeuralSlimeMoldRenderer rendererComponent;
    private bool hasStarted;

    private static readonly Color MoldBackgroundPreset = new(0.10f, 0.09f, 0.07f, 1f);

    private void Awake()
    {
        runner = new NeuralSlimeMoldRunner();
        rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        if (rendererComponent == null)
        {
            rendererComponent = gameObject.AddComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
        ApplyCameraBackground();
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

        runner.Tick(Time.deltaTime, trailDiffusion, trailDecayPerSecond, foodStrength, explorationTurnNoise);
        rendererComponent.Render(runner);

        if (autoFrameCamera && adaptiveCameraFraming)
        {
            UpdateAdaptiveCamera(Time.deltaTime);
        }
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartSimulation()
    {
        var turnRateRadians = turnRateDegrees * Mathf.Deg2Rad;
        var sensorAngleRadians = sensorAngleDegrees * Mathf.Deg2Rad;

        runner.ResetWithSeed(
            seed,
            agentCount,
            trailResolution,
            mapSize,
            speed,
            turnRateRadians,
            sensorAngleRadians,
            sensorDistance,
            depositAmount,
            foodNodeCount,
            foodStrength,
            foodCapacity,
            consumeRadius,
            consumeRate,
            allowFoodRegrowth,
            foodReactivationDelay,
            regrowRate,
            foodReactivationThreshold,
            spawnFromSeed,
            manualFoodConfigs,
            useColonyHub,
            colonyHub,
            colonyHubRadius,
            returnToHubWeight,
            returnTrailBlend,
            returnDepositBoost,
            successfulReturnDepositBurst,
            hubInfluenceRadius,
            nonUsefulLoopPruneStrength,
            nonUsefulLoopTrailThreshold,
            nonUsefulLoopCurvatureThreshold,
            seekExplorationDepositMultiplier,
            seekTransportDepositMultiplier,
            returnExplorationDepositMultiplier,
            explorationFollowWeight,
            transportFollowWeight,
            returnTransportFollowWeight,
            returnExplorationFollowWeight,
            explorationDecayMultiplier,
            explorationDiffusionMultiplier);

        ApplyRendererOverrides();
        ApplyCameraBackground();
        rendererComponent.Build(runner);

        if (autoFrameCamera)
        {
            FrameCameraImmediate();
        }

        hasStarted = true;
    }

    [ContextMenu("Reseed")]
    public void Reseed()
    {
        seed = StableHashUtility.CombineSeed(seed, "neural-slime-next");
        StartSimulation();
    }

    [ContextMenu("Apply Mold Palette Preset")]
    public void ApplyMoldPalettePreset()
    {
        backgroundColor = MoldBackgroundPreset;
        ApplyRendererOverrides();
        ApplyCameraBackground();
    }

    private void ApplyRendererOverrides()
    {
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.SetShapeToggles(useGlowAgentShape, useFieldBlobOverlay);
        rendererComponent.SetFoodDebugVisuals(showFoodMarkers);
        rendererComponent.SetFoodInfluenceDebugVisuals(false);
        rendererComponent.SetBackgroundColor(backgroundColor);
    }

    private void ApplyCameraBackground()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.backgroundColor = backgroundColor;
    }

    private void FrameCameraImmediate()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        var worldTargetSize = ComputeFullMapCameraSize(cam);
        var activityTargetSize = ComputeActivityCameraSize(cam);
        var targetSize = adaptiveCameraFraming
            ? Mathf.Max(minimumCameraSize, Mathf.Min(worldTargetSize, activityTargetSize))
            : Mathf.Max(minimumCameraSize, worldTargetSize);

        cam.orthographicSize = targetSize;

        var targetCenter = adaptiveCameraFraming ? runner.ActivityCenter : Vector2.zero;
        cam.transform.position = new Vector3(targetCenter.x, targetCenter.y, cam.transform.position.z);
    }

    private void UpdateAdaptiveCamera(float dt)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        var targetCenter = runner.ActivityCenter;
        var current2D = new Vector2(cam.transform.position.x, cam.transform.position.y);
        var delta = targetCenter - current2D;

        if (delta.magnitude > cameraDeadZoneRadius)
        {
            var lookAheadCenter = Vector2.Lerp(current2D, targetCenter, 1f + cameraLookAheadToActivity);
            var smoothed = Vector2.Lerp(
                current2D,
                lookAheadCenter,
                1f - Mathf.Exp(-cameraFollowSmooth * Mathf.Max(0f, dt)));
            cam.transform.position = new Vector3(smoothed.x, smoothed.y, cam.transform.position.z);
        }

        var worldTargetSize = ComputeFullMapCameraSize(cam);
        var activityTargetSize = ComputeActivityCameraSize(cam);
        var targetSize = Mathf.Max(minimumCameraSize, Mathf.Min(worldTargetSize, activityTargetSize));
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            1f - Mathf.Exp(-cameraZoomSmooth * Mathf.Max(0f, dt)));
    }

    private float ComputeFullMapCameraSize(Camera cam)
    {
        var halfHeight = mapSize.y * 0.5f;
        var halfWidth = mapSize.x * 0.5f;
        var aspect = Mathf.Max(0.1f, cam.aspect);
        var orthoFromWidth = halfWidth / aspect;
        return Mathf.Max(halfHeight, orthoFromWidth) * Mathf.Max(0.1f, cameraPadding);
    }

    private float ComputeActivityCameraSize(Camera cam)
    {
        var aspect = Mathf.Max(0.1f, cam.aspect);
        var radius = Mathf.Max(1f, runner != null ? runner.ActivityRadius : 1f);
        var paddedRadius = radius * Mathf.Max(0.1f, cameraPadding);
        var vertical = paddedRadius;
        var horizontal = paddedRadius / aspect;
        return Mathf.Max(vertical, horizontal);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.8f, 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(mapSize.x, mapSize.y, 0f));

        if (manualFoodConfigs != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.45f);
            for (var i = 0; i < manualFoodConfigs.Length; i++)
            {
                var node = manualFoodConfigs[i];
                Gizmos.DrawWireSphere(
                    new Vector3(node.position.x, node.position.y, 0f),
                    Mathf.Max(0.01f, node.consumeRadius));
            }
        }

        if (useColonyHub)
        {
            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.5f);
            Gizmos.DrawWireSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), colonyHubRadius);

            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.15f);
            Gizmos.DrawWireSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), Mathf.Max(colonyHubRadius, hubInfluenceRadius));

            Gizmos.color = new Color(0.75f, 1f, 1f, 0.8f);
            Gizmos.DrawSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), 0.25f);
        }

        if (UnityEngine.Application.isPlaying && runner != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(new Vector3(runner.ActivityCenter.x, runner.ActivityCenter.y, 0f), runner.ActivityRadius);

            Gizmos.color = new Color(0.8f, 1f, 1f, 0.7f);
            Gizmos.DrawSphere(new Vector3(runner.ActivityCenter.x, runner.ActivityCenter.y, 0f), 0.35f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        mapSize.x = Mathf.Max(8f, mapSize.x);
        mapSize.y = Mathf.Max(8f, mapSize.y);

        trailResolution.x = Mathf.Max(16, trailResolution.x);
        trailResolution.y = Mathf.Max(16, trailResolution.y);

        agentCount = Mathf.Max(1, agentCount);
        foodNodeCount = Mathf.Max(1, foodNodeCount);

        sensorDistance = Mathf.Max(0f, sensorDistance);
        speed = Mathf.Max(0f, speed);
        depositAmount = Mathf.Max(0f, depositAmount);
        explorationTurnNoise = Mathf.Max(0f, explorationTurnNoise);

        foodStrength = Mathf.Max(0f, foodStrength);
        foodCapacity = Mathf.Max(0f, foodCapacity);
        consumeRadius = Mathf.Max(0f, consumeRadius);
        consumeRate = Mathf.Max(0f, consumeRate);
        foodReactivationDelay = Mathf.Max(0f, foodReactivationDelay);
        regrowRate = Mathf.Max(0f, regrowRate);

        colonyHubRadius = Mathf.Max(0.25f, colonyHubRadius);
        returnToHubWeight = Mathf.Max(0f, returnToHubWeight);
        returnTrailBlend = Mathf.Clamp01(returnTrailBlend);
        returnDepositBoost = Mathf.Max(0f, returnDepositBoost);
        successfulReturnDepositBurst = Mathf.Max(0f, successfulReturnDepositBurst);
        hubInfluenceRadius = Mathf.Max(colonyHubRadius, hubInfluenceRadius);

        nonUsefulLoopPruneStrength = Mathf.Max(0f, nonUsefulLoopPruneStrength);
        nonUsefulLoopTrailThreshold = Mathf.Max(0f, nonUsefulLoopTrailThreshold);
        nonUsefulLoopCurvatureThreshold = Mathf.Max(0f, nonUsefulLoopCurvatureThreshold);

        seekExplorationDepositMultiplier = Mathf.Max(0f, seekExplorationDepositMultiplier);
        seekTransportDepositMultiplier = Mathf.Max(0f, seekTransportDepositMultiplier);
        returnExplorationDepositMultiplier = Mathf.Max(0f, returnExplorationDepositMultiplier);
        explorationFollowWeight = Mathf.Max(0f, explorationFollowWeight);
        transportFollowWeight = Mathf.Max(0f, transportFollowWeight);
        returnTransportFollowWeight = Mathf.Max(0f, returnTransportFollowWeight);
        returnExplorationFollowWeight = Mathf.Max(0f, returnExplorationFollowWeight);
        explorationDecayMultiplier = Mathf.Max(0.1f, explorationDecayMultiplier);
        explorationDiffusionMultiplier = Mathf.Max(0.1f, explorationDiffusionMultiplier);

        cameraPadding = Mathf.Max(0.1f, cameraPadding);
        cameraFollowSmooth = Mathf.Max(0.01f, cameraFollowSmooth);
        cameraZoomSmooth = Mathf.Max(0.01f, cameraZoomSmooth);
        minimumCameraSize = Mathf.Max(1f, minimumCameraSize);
        cameraDeadZoneRadius = Mathf.Max(0f, cameraDeadZoneRadius);

        if (rendererComponent == null)
        {
            rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
        ApplyCameraBackground();
    }
#endif
}

// =======================================
// FILE: NeuralSlimeMoldBootstrapEditor.cs
// =======================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSection("Simulation", new[]
        {
            "autoStart",
            "seed",
            "agentCount",
            "mapSize",
            "trailResolution",
            "trailDecayPerSecond",
            "trailDiffusion"
        });

        DrawSection("Agent Motion", new[]
        {
            "sensorAngleDegrees",
            "sensorDistance",
            "speed",
            "turnRateDegrees",
            "depositAmount",
            "explorationTurnNoise"
        });

        DrawSection("Food", new[]
        {
            "foodNodeCount",
            "foodStrength",
            "foodCapacity",
            "consumeRadius",
            "consumeRate",
            "allowFoodRegrowth",
            "foodReactivationDelay",
            "regrowRate",
            "foodReactivationThreshold",
            "spawnFromSeed",
            "manualFoodConfigs"
        });

        DrawSection("Colony Hub", new[]
        {
            "useColonyHub",
            "colonyHub",
            "colonyHubRadius",
            "returnToHubWeight",
            "returnTrailBlend",
            "returnDepositBoost",
            "successfulReturnDepositBurst",
            "hubInfluenceRadius"
        });

        DrawSection("Loop Pruning", new[]
        {
            "nonUsefulLoopPruneStrength",
            "nonUsefulLoopTrailThreshold",
            "nonUsefulLoopCurvatureThreshold"
        });

        DrawSection("Dual Field", new[]
        {
            "seekExplorationDepositMultiplier",
            "seekTransportDepositMultiplier",
            "returnExplorationDepositMultiplier",
            "explorationFollowWeight",
            "transportFollowWeight",
            "returnTransportFollowWeight",
            "returnExplorationFollowWeight",
            "explorationDecayMultiplier",
            "explorationDiffusionMultiplier"
        });

        DrawSection("Rendering", new[]
        {
            "showFoodMarkers"
        });

        DrawSection("Palette", new[]
        {
            "useGlowAgentShape",
            "useFieldBlobOverlay",
            "backgroundColor"
        });

        DrawSection("Camera Framing", new[]
        {
            "autoFrameCamera",
            "adaptiveCameraFraming",
            "cameraPadding",
            "cameraFollowSmooth",
            "cameraZoomSmooth",
            "minimumCameraSize",
            "cameraLookAheadToActivity",
            "cameraDeadZoneRadius"
        });

        EditorGUILayout.Space(10f);
        DrawRuntimeButtons();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, string[] propertyNames)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        for (var i = 0; i < propertyNames.Length; i++)
        {
            Draw(propertyNames[i]);
        }
    }

    private void DrawRuntimeButtons()
    {
        var bootstrap = target as NeuralSlimeMoldBootstrap;
        if (bootstrap == null)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Start / Reset Simulation"))
            {
                bootstrap.StartSimulation();
                EditorUtility.SetDirty(bootstrap);
            }

            if (GUILayout.Button("Reseed"))
            {
                bootstrap.Reseed();
                EditorUtility.SetDirty(bootstrap);
            }
        }
    }

    private void Draw(string propertyName)
    {
        var prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, true);
        }
    }
}
#endif
