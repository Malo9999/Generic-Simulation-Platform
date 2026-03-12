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
    private const float ObstacleProbeDistance = 1.2f;
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
    private const float ConnectorReinforceTrailThreshold = 0.035f;
    private const int HubOrbitSampleCount = 10;
    private const float BranchSpawnAngleMinDegrees = 24f;
    private const float BranchSpawnAngleMaxDegrees = 68f;
    private const float BranchSpawnFrontSampleDistance = 2.3f;
    private const float BranchSpawnSideSampleDistance = 2.1f;
    private const float ExploratoryBranchDepositScale = 0.34f;
    private const float PromotedBranchDepositScale = 0.82f;

    private const float ConnectorSoftCorridorOuterMultiplier = 2.4f;
    private const float ConnectorSoftProgressSlack = 1.45f;
    private const float NearHubReturnCrossBias = 0.95f;
    private const int MaxAccumulatedConnectorContributors = 3;

    private NeuralSlimeMoldAgent[] agents = Array.Empty<NeuralSlimeMoldAgent>();
    private NeuralFoodNodeState[] foodNodes = Array.Empty<NeuralFoodNodeState>();
    private int[] foodConsumerCounts = Array.Empty<int>();
    private bool[] foodDepletionLogged = Array.Empty<bool>();
    private bool[] foodIsActive = Array.Empty<bool>();
    private float[] foodRespawnTimers = Array.Empty<float>();

    private float[] agentTrailConfidence = Array.Empty<float>();
    private float[] agentFoodCommitment = Array.Empty<float>();

    private AgentMode[] agentModes = Array.Empty<AgentMode>();
    private float[] agentModeTimers = Array.Empty<float>();
    private int[] agentLastFoodIndex = Array.Empty<int>();

    private float simulationTime;
    private float nextOccupiedFoodLogTime;
    private int tickCounter;

    private SeededRng rng;
    private Vector2 mapSize;

    private float foodRespawnDelaySeconds;
    private float foodRespawnDistanceBias;
    private float outerRingSpawnBias;
    private int maxSimultaneousActiveFood;

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
    private float bridgeReinforcementWeight;
    private float hubOrbitSuppression;
    private float staleCorridorDecayBoost;
    private float connectorSearchRadius;
    private float connectorSteerWeight;
    private float hubTangentialPenalty;
    private float connectorCorridorWidth;
    private float returnOrbitDepositPenalty;
    private float branchSpawnChance;
    private float branchSpawnTrailThreshold;
    private float branchPromotionThreshold;
    private float branchRetractionBoost;
    private float trunkStabilityBoost;
    private float duplicateTubeSuppressionRadius;

    private bool useWorldObstacles;
    private float obstacleAvoidanceStrength;
    private float obstaclePadding;
    private NeuralObstacle[] obstacles = Array.Empty<NeuralObstacle>();
    private NeuralCorridorBand[] corridorBands = Array.Empty<NeuralCorridorBand>();
    private bool[] blockedFieldMask = Array.Empty<bool>();

    public NeuralFieldGrid Field { get; private set; }
    public NeuralSlimeMoldAgent[] Agents => agents;
    public NeuralFoodNodeState[] FoodNodes => foodNodes;
    public NeuralObstacle[] Obstacles => obstacles;

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
        int candidateFoodNodeCount,
        int maxSimultaneousActiveFood,
        float foodRespawnDelay,
        float foodRespawnDistanceBias,
        float outerRingSpawnBias,
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
        float bridgeReinforcementWeight,
        float hubOrbitSuppression,
        float staleCorridorDecayBoost,
        float connectorSearchRadius,
        float connectorSteerWeight,
        float hubTangentialPenalty,
        float connectorCorridorWidth,
        float returnOrbitDepositPenalty,
        float branchSpawnChance,
        float branchSpawnTrailThreshold,
        float branchPromotionThreshold,
        float branchRetractionBoost,
        float trunkStabilityBoost,
        float duplicateTubeSuppressionRadius,
        bool useWorldObstacles,
        NeuralObstacle[] obstacles,
        NeuralCorridorBand[] corridorBands,
        float obstacleAvoidanceStrength,
        float obstaclePadding)
    {
        Seed = seed;
        this.mapSize = new Vector2(Mathf.Max(8f, mapSize.x), Mathf.Max(8f, mapSize.y));
        rng = new SeededRng(seed);

        this.maxSimultaneousActiveFood = Mathf.Max(1, maxSimultaneousActiveFood);
        foodRespawnDelaySeconds = Mathf.Max(0f, foodRespawnDelay);
        this.foodRespawnDistanceBias = Mathf.Clamp01(foodRespawnDistanceBias);
        this.outerRingSpawnBias = Mathf.Clamp01(outerRingSpawnBias);

        this.useColonyHub = useColonyHub;
        this.colonyHub = ClampNodeInsideBounds(colonyHub);
        this.colonyHub = ResolveToOpenPosition(this.colonyHub, 1f);
        this.colonyHubRadius = Mathf.Max(0.25f, colonyHubRadius);
        this.returnToHubWeight = Mathf.Max(0f, returnToHubWeight);
        this.returnTrailBlend = Mathf.Clamp01(returnTrailBlend);
        this.returnDepositBoost = Mathf.Max(0f, returnDepositBoost);
        this.successfulReturnDepositBurst = Mathf.Max(0f, successfulReturnDepositBurst);
        this.hubInfluenceRadius = Mathf.Max(this.colonyHubRadius, hubInfluenceRadius);
        this.nonUsefulLoopPruneStrength = Mathf.Max(0f, nonUsefulLoopPruneStrength);
        this.nonUsefulLoopTrailThreshold = Mathf.Max(0f, nonUsefulLoopTrailThreshold);
        this.nonUsefulLoopCurvatureThreshold = Mathf.Max(0f, nonUsefulLoopCurvatureThreshold);
        this.bridgeReinforcementWeight = Mathf.Max(0f, bridgeReinforcementWeight);
        this.hubOrbitSuppression = Mathf.Max(0f, hubOrbitSuppression);
        this.staleCorridorDecayBoost = Mathf.Max(0f, staleCorridorDecayBoost);
        this.connectorSearchRadius = Mathf.Max(0.1f, connectorSearchRadius);
        this.connectorSteerWeight = Mathf.Max(0f, connectorSteerWeight);
        this.hubTangentialPenalty = Mathf.Max(0f, hubTangentialPenalty);
        this.connectorCorridorWidth = Mathf.Max(0.25f, connectorCorridorWidth);
        this.returnOrbitDepositPenalty = Mathf.Clamp01(returnOrbitDepositPenalty);
        this.branchSpawnChance = Mathf.Max(0f, branchSpawnChance);
        this.branchSpawnTrailThreshold = Mathf.Max(0f, branchSpawnTrailThreshold);
        this.branchPromotionThreshold = Mathf.Max(this.branchSpawnTrailThreshold, branchPromotionThreshold);
        this.branchRetractionBoost = Mathf.Max(0f, branchRetractionBoost);
        this.trunkStabilityBoost = Mathf.Max(0f, trunkStabilityBoost);
        this.duplicateTubeSuppressionRadius = Mathf.Max(0f, duplicateTubeSuppressionRadius);
        this.useWorldObstacles = useWorldObstacles;
        this.obstacleAvoidanceStrength = Mathf.Max(0f, obstacleAvoidanceStrength);
        this.obstaclePadding = Mathf.Max(0f, obstaclePadding);
        this.obstacles = obstacles != null ? (NeuralObstacle[])obstacles.Clone() : Array.Empty<NeuralObstacle>();
        this.corridorBands = corridorBands != null ? (NeuralCorridorBand[])corridorBands.Clone() : Array.Empty<NeuralCorridorBand>();

        Field = new NeuralFieldGrid(trailResolution.x, trailResolution.y, this.mapSize, false);
        BuildBlockedFieldMask();

        agents = new NeuralSlimeMoldAgent[Mathf.Max(1, agentCount)];
        agentTrailConfidence = new float[agents.Length];
        agentFoodCommitment = new float[agents.Length];
        agentModes = new AgentMode[agents.Length];
        agentModeTimers = new float[agents.Length];
        agentLastFoodIndex = new int[agents.Length];

        BuildFoodNodes(
            Mathf.Max(1, foodNodeCount),
            Mathf.Max(1, candidateFoodNodeCount),
            Mathf.Max(0f, foodStrength),
            Mathf.Max(0f, foodCapacity),
            Mathf.Max(0f, consumeRadius),
            Mathf.Max(0f, consumeRate),
            spawnFoodFromSeed,
            manualFoodNodes);
        InitializeFoodActivationState();

        simulationTime = 0f;
        nextOccupiedFoodLogTime = OccupiedFoodLogIntervalSeconds;
        tickCounter = 0;
        ActivityCenter = Vector2.zero;
        ActivityRadius = MinActivityRadius;

        for (var i = 0; i < agents.Length; i++)
        {
            var radial = rng.InsideUnitCircle();
            var pos = new Vector2(
                radial.x * this.mapSize.x * 0.48f,
                radial.y * this.mapSize.y * 0.48f);
            pos = ResolveToOpenPosition(pos, 0.75f);

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

    public void Tick(float dt, float diffusion, float decay, float foodStrength, float explorationTurnNoise, int fieldStepInterval = 1)
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
                    break;

                case AgentMode.ReturnToHub:
                    HandleReturnToHubMode(ref agent, lastFoodIndex, dt, explorationTurnNoise);

                    var returnDepositMultiplier = 1f;
                    if (useColonyHub && IsTangentialHubOrbit(agent.position, agent.heading))
                    {
                        returnDepositMultiplier = returnOrbitDepositPenalty;
                    }

                    if (GetContainingFoodNodeIndex(agent.position, true) >= 0)
                    {
                        Field.ScrubAt(agent.position, ActiveFoodTrailScrub);
                    }

                    var returnDeposit = agent.depositAmount * returnDepositBoost * returnDepositMultiplier;
                    DepositKernelIfOpen(agent.position, returnDeposit);
                    DepositVeinKernelIfOpen(agent.position, returnDeposit * 0.42f);

                    if (useColonyHub && IsInsideHub(agent.position))
                    {
                        var burst = successfulReturnDepositBurst * returnDepositMultiplier;
                        DepositDiscIfOpen(agent.position, colonyHubRadius * 0.95f, burst);
                        DepositVeinDiscIfOpen(agent.position, colonyHubRadius * 0.7f, burst * 0.28f);
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
                    }
                    else if (emptyFoodIndex >= 0)
                    {
                        Field.ScrubAt(agent.position, EmptyFoodTrailScrub);
                    }

                    var movedLocalTrail = Field.SampleBilinear(agent.position);
                    var depositMultiplier = ComputeDepositMultiplier(
                        agent.position,
                        movedLocalTrail,
                        agentTrailConfidence[i],
                        activeFoodIndex >= 0,
                        emptyFoodIndex >= 0,
                        agentFoodCommitment[i]);

                    var seekDeposit = agent.depositAmount * depositMultiplier;
                    DepositKernelIfOpen(agent.position, seekDeposit);
                    if (movedLocalTrail >= branchPromotionThreshold)
                    {
                        DepositVeinKernelIfOpen(agent.position, seekDeposit * 0.08f * Mathf.Clamp01(movedLocalTrail * 2.5f));
                    }
                    TryNucleateBranch(agent.position, agent.heading, agent.depositAmount, dt, activeFoodIndex >= 0);

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
        ApplyNetworkMaintenance(dt);
        LogOccupiedFoodLevels();
        tickCounter++;

        var stepInterval = Mathf.Max(1, fieldStepInterval);
        if (stepInterval <= 1 || tickCounter % stepInterval == 0)
        {
            Field.Step(diffusion, decay, dt * stepInterval, branchPromotionThreshold, trunkStabilityBoost, duplicateTubeSuppressionRadius);
        }

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

        var leftTrail = Field.SampleBilinear(leftSamplePos);
        var centerTrail = Field.SampleBilinear(centerSamplePos);
        var rightTrail = Field.SampleBilinear(rightSamplePos);
        var localTrail = Field.SampleBilinear(agent.position);

        var leftNutrient = SampleNutrientField(leftSamplePos);
        var centerNutrient = SampleNutrientField(centerSamplePos);
        var rightNutrient = SampleNutrientField(rightSamplePos);

        ComputeConnectorSensorSamples(agent.position, agent.heading, agent.sensorAngle, agent.sensorDistance, out var connectorEdge, out var connectorCenterBias, out var connectorStrength);

        var weightedTrail = (leftTrail * agent.controller.leftWeight)
                          + (centerTrail * agent.controller.centerWeight)
                          + (rightTrail * agent.controller.rightWeight);

        var trailEdge = rightTrail - leftTrail;
        var trailCenterBias = centerTrail - ((leftTrail + rightTrail) * 0.5f);

        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * Mathf.Max(0.1f, agent.controller.noiseWeight * 8f);

        var trailConfidence = ComputeTrailConfidence(localTrail, centerTrail, leftTrail, rightTrail);
        agentTrailConfidence[agentIndex] = Mathf.Lerp(agentTrailConfidence[agentIndex], trailConfidence, 0.18f);

        var trailSteer =
            (weightedTrail * 0.12f) +
            trailEdge +
            (trailCenterBias * (0.8f + agent.controller.densityWeight)) +
            (randomTurn * Mathf.Lerp(1f, 0.25f, agentTrailConfidence[agentIndex]));

        var nutrientEdge = rightNutrient - leftNutrient;
        var nutrientCenterBias = centerNutrient - ((leftNutrient + rightNutrient) * 0.5f);
        var nutrientSteer = (nutrientEdge * 1.1f) + (nutrientCenterBias * 0.7f);

        var connectorSteer = (connectorEdge * 1.15f) + (connectorCenterBias * 0.8f);
        var topologySteer = trailSteer;

        if (connectorStrength > 0.0001f)
        {
            var blend = Mathf.Clamp01(connectorStrength * 1.25f);
            var connectorTargetSteer = (connectorSteer * connectorSteerWeight) + (trailSteer * 0.3f);
            topologySteer = Mathf.Lerp(trailSteer * 0.55f, connectorTargetSteer, blend);
        }

        var nutrientStrength = Mathf.Clamp01(Mathf.Max(leftNutrient, Mathf.Max(centerNutrient, rightNutrient)));
        var steer = topologySteer;

        if (nutrientStrength > 0.0001f)
        {
            agentFoodCommitment[agentIndex] = Mathf.Lerp(agentFoodCommitment[agentIndex], nutrientStrength, 0.18f);
            var nutrientBlend = Mathf.Clamp01((nutrientStrength * 0.75f) + (agentFoodCommitment[agentIndex] * 0.25f));
            var nutrientTarget = (nutrientSteer * Mathf.Max(0.5f, foodStrength)) + (connectorSteer * connectorSteerWeight * 0.35f);
            steer = Mathf.Lerp(topologySteer, nutrientTarget, nutrientBlend);
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
                steer = Mathf.Lerp(randomTurn * 0.1f, topologySteer, 0.92f);
            }
        }

        if (IsNearHub(agent.position))
        {
            var outwardSteer = ComputeSteerAwayFromPoint(agent.position, agent.heading, colonyHub);
            steer += outwardSteer * 0.9f;

            if (IsTangentialHubOrbit(agent.position, agent.heading))
            {
                var radial = (agent.position - colonyHub).normalized;
                var tangent = new Vector2(-radial.y, radial.x);
                var tangentDot = Vector2.Dot(Direction(agent.heading), tangent);
                steer += outwardSteer * hubTangentialPenalty;
                steer -= tangentDot * hubTangentialPenalty * 0.65f;
            }
        }

        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);
        steer += ComputeObstacleAvoidanceTurn(agent.position, agent.heading) * obstacleAvoidanceStrength;
        steer += ComputeCorridorSteer(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        var speedMod = 1f;

        if (localTrail >= StrongTrailHighwayThreshold)
        {
            speedMod *= 1.16f;
        }
        else if (localTrail >= MinTrailForHighway)
        {
            speedMod *= 1.06f;
        }

        speedMod *= 1f + Mathf.Clamp(centerTrail * 0.12f, -0.12f, 0.25f);

        if (nearEmptyFood)
        {
            speedMod *= 1.25f;
        }

        if (IsNearHub(agent.position))
        {
            speedMod *= 1.08f;
        }

        MoveWithObstacleCollision(ref agent, agent.speed * speedMod * dt);
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

        var feedStep = feedMoveSpeed * Mathf.Max(0f, dt);
        if (feedStep > 0f)
        {
            var toTargetHeading = Mathf.Atan2(dirToFood.y, dirToFood.x);
            agent.heading = Mathf.Repeat(toTargetHeading, Mathf.PI * 2f);
            MoveWithObstacleCollision(ref agent, feedStep);
        }

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

        var leftTrail = Field.SampleBilinear(leftSamplePos);
        var centerTrail = Field.SampleBilinear(centerSamplePos);
        var rightTrail = Field.SampleBilinear(rightSamplePos);

        ComputeConnectorSensorSamples(agent.position, agent.heading, agent.sensorAngle, agent.sensorDistance, out var connectorEdge, out var connectorCenterBias, out var connectorStrength);

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

        var connectorSteer = (connectorEdge * 1.15f) + (connectorCenterBias * 0.85f);
        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * ReturnNoiseMultiplier;

        var steer = (hubSteer * 0.9f) + awayFoodSteer + (trailSteer * (1f - returnTrailBlend));

        if (connectorStrength > 0.0001f)
        {
            var connectorBlend = Mathf.Clamp01(connectorStrength * 1.3f);
            var connectorTarget = (connectorSteer * connectorSteerWeight) + (hubSteer * 0.85f) + (trailSteer * 0.25f);
            steer = Mathf.Lerp(steer, connectorTarget, connectorBlend);
        }
        else
        {
            steer = Mathf.Lerp(hubSteer + awayFoodSteer, trailSteer + hubSteer, returnTrailBlend);
        }

        if (IsNearHub(agent.position))
        {
            var usefulAlignment = ComputeUsefulConnectorAlignment(agent.position, Direction(agent.heading));
            if (usefulAlignment < 0.45f)
            {
                var towardHub = ComputeSteerTowardPoint(agent.position, agent.heading, colonyHub);
                var outward = ComputeSteerAwayFromPoint(agent.position, agent.heading, colonyHub);

                steer += towardHub * NearHubReturnCrossBias;

                if (IsTangentialHubOrbit(agent.position, agent.heading))
                {
                    steer += towardHub * hubTangentialPenalty;
                    steer += outward * (hubTangentialPenalty * 0.18f);
                }
            }
        }

        steer += randomTurn;
        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);
        steer += ComputeObstacleAvoidanceTurn(agent.position, agent.heading) * obstacleAvoidanceStrength;
        steer += ComputeCorridorSteer(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        var speedMod = 1.10f;
        if (centerTrail >= StrongTrailHighwayThreshold)
        {
            speedMod *= 1.10f;
        }
        else if (centerTrail >= MinTrailForHighway)
        {
            speedMod *= 1.04f;
        }

        MoveWithObstacleCollision(ref agent, agent.speed * speedMod * dt);
    }

    private void HandleExitHubMode(ref NeuralSlimeMoldAgent agent, float dt, float explorationTurnNoise)
    {
        var outwardSteer = ComputeSteerAwayFromPoint(agent.position, agent.heading, colonyHub) * ExitHubSteerStrength;
        var signedNoise = (rng.NextFloat01() * 2f) - 1f;
        var randomTurn = signedNoise * explorationTurnNoise * ExitHubNoiseMultiplier;

        var steer = outwardSteer + randomTurn;
        steer += ComputeBoundaryAvoidanceTurn(agent.position, agent.heading);
        steer += ComputeObstacleAvoidanceTurn(agent.position, agent.heading) * obstacleAvoidanceStrength;
        steer += ComputeCorridorSteer(agent.position, agent.heading);

        var turnStep = Mathf.Clamp(steer, -1f, 1f) * agent.turnRate * dt;
        agent.heading = Mathf.Repeat(agent.heading + turnStep, Mathf.PI * 2f);

        MoveWithObstacleCollision(ref agent, agent.speed * ExitHubSpeedMultiplier * dt);
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
            if (!foodIsActive[i] || node.currentCapacity <= 0f || node.capacity <= 0f)
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

    private float ComputeDepositMultiplier(
        Vector2 position,
        float localTrail,
        float trailConfidence,
        bool onActiveFood,
        bool onEmptyFood,
        float foodCommitment)
    {
        if (onActiveFood || onEmptyFood)
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
        var deltaTime = Mathf.Max(0f, dt);
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var maxCapacity = Mathf.Max(0.01f, node.capacity);

            if (foodIsActive[i])
            {
                var consumed = node.consumeRate * foodConsumerCounts[i] * deltaTime;
                var previousCapacity = node.currentCapacity;
                node.currentCapacity = Mathf.Clamp(node.currentCapacity - consumed, 0f, maxCapacity);
                node.isActive = true;
                node.cooldown01 = 0f;

                if (!foodDepletionLogged[i] && previousCapacity > 0f && node.currentCapacity <= 0f)
                {
                    foodDepletionLogged[i] = true;
                    foodIsActive[i] = false;
                    foodRespawnTimers[i] = foodRespawnDelaySeconds;
                    node.isActive = false;
                    node.cooldown01 = 1f;
                    UnityEngine.Debug.Log($"[NeuralSlimeMold] Food node {i} depleted at t={simulationTime:F2}s.");

                    Field.ScrubDisc(node.position, Mathf.Max(0.6f, node.consumeRadius * 1.15f), 0.35f);
                    Field.ScrubVeinDisc(node.position, Mathf.Max(0.45f, node.consumeRadius * 0.9f), 0.18f);
                }
            }
            else
            {
                node.currentCapacity = 0f;
                node.isActive = false;

                if (foodRespawnTimers[i] > 0f)
                {
                    foodRespawnTimers[i] -= deltaTime;
                }

                node.cooldown01 = foodRespawnDelaySeconds <= 0.0001f
                    ? 0f
                    : Mathf.Clamp01(foodRespawnTimers[i] / foodRespawnDelaySeconds);
            }

            foodNodes[i] = node;
        }

        TryActivateFoodReplacements();
    }

    private void MarkConsumingFoodNodes(Vector2 agentPosition)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            if (!foodIsActive[i] || node.currentCapacity <= 0f)
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
            if (foodIsActive[i] && node.currentCapacity > 0f)
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

            if (requireNonEmpty && (!foodIsActive[i] || node.currentCapacity <= 0f))
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
            if (foodIsActive[i] && node.currentCapacity > 0f)
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
        int candidateFoodNodeCount,
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
                var spawnPosition = ClampNodeInsideBounds(cfg.position);
                spawnPosition = ResolveToOpenPosition(spawnPosition, Mathf.Max(0.5f, cfg.consumeRadius * 0.35f));
                foodNodes[i] = BuildFoodState(
                    spawnPosition,
                    Mathf.Max(0f, cfg.baseStrength),
                    Mathf.Max(0f, cfg.capacity),
                    Mathf.Max(0f, cfg.consumeRadius),
                    Mathf.Max(0f, cfg.consumeRate));
            }

            foodConsumerCounts = new int[foodNodes.Length];
            foodDepletionLogged = new bool[foodNodes.Length];
            foodIsActive = new bool[foodNodes.Length];
            foodRespawnTimers = new float[foodNodes.Length];
            return;
        }

        var candidateCount = Mathf.Max(foodNodeCount, candidateFoodNodeCount);
        foodNodes = new NeuralFoodNodeState[candidateCount];
        var placementRng = spawnFoodFromSeed
            ? new SeededRng(StableHashUtility.CombineSeed(Seed, "slime-food-nodes"))
            : new SeededRng((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));

        for (var i = 0; i < candidateCount; i++)
        {
            var pos = SampleOpenFoodPosition(placementRng);
            foodNodes[i] = BuildFoodState(pos, foodStrength, foodCapacity, consumeRadius, consumeRate);
        }

        foodConsumerCounts = new int[foodNodes.Length];
        foodDepletionLogged = new bool[foodNodes.Length];
        foodIsActive = new bool[foodNodes.Length];
        foodRespawnTimers = new float[foodNodes.Length];
    }

    private void InitializeFoodActivationState()
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return;
        }

        for (var i = 0; i < foodNodes.Length; i++)
        {
            foodIsActive[i] = false;
            foodRespawnTimers[i] = 0f;
            foodDepletionLogged[i] = false;
            var node = foodNodes[i];
            node.currentCapacity = 0f;
            node.isActive = false;
            node.cooldown01 = 0f;
            foodNodes[i] = node;
        }

        var initialActiveTarget = Mathf.Clamp(maxSimultaneousActiveFood, 1, foodNodes.Length);
        for (var i = 0; i < initialActiveTarget; i++)
        {
            var index = SelectBestReplacementFoodIndex();
            if (index < 0)
            {
                break;
            }

            ActivateFoodNode(index);
        }
    }

    private void TryActivateFoodReplacements()
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return;
        }

        var activeCount = 0;
        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (foodIsActive[i] && foodNodes[i].currentCapacity > 0f)
            {
                activeCount++;
            }
        }

        var target = Mathf.Clamp(maxSimultaneousActiveFood, 1, foodNodes.Length);
        while (activeCount < target)
        {
            var replacementIndex = SelectBestReplacementFoodIndex();
            if (replacementIndex < 0)
            {
                break;
            }

            ActivateFoodNode(replacementIndex);
            activeCount++;
        }
    }

    private int SelectBestReplacementFoodIndex()
    {
        var bestScore = float.NegativeInfinity;
        var bestIndex = -1;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (foodIsActive[i] || foodRespawnTimers[i] > 0f)
            {
                continue;
            }

            var score = ComputeReplacementScore(i);
            score += rng.Range(0f, 0.05f);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float ComputeReplacementScore(int candidateIndex)
    {
        var node = foodNodes[candidateIndex];
        var nearestDistance = float.MaxValue;
        var activeCenter = Vector2.zero;
        var activeCount = 0;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (!foodIsActive[i] || foodNodes[i].currentCapacity <= 0f)
            {
                continue;
            }

            activeCenter += foodNodes[i].position;
            activeCount++;
            var dist = Vector2.Distance(node.position, foodNodes[i].position);
            nearestDistance = Mathf.Min(nearestDistance, dist);
        }

        var mapRadius = Mathf.Max(1f, Mathf.Min(mapSize.x, mapSize.y) * 0.5f);
        var hubDistance = useColonyHub ? Vector2.Distance(node.position, colonyHub) : node.position.magnitude;
        var outerBias = Mathf.Clamp01(hubDistance / mapRadius);

        var distanceFromActive = 1f;
        if (nearestDistance < float.MaxValue)
        {
            distanceFromActive = Mathf.Clamp01(nearestDistance / mapRadius);
        }

        var clusterDistance = 1f;
        if (activeCount > 0)
        {
            activeCenter /= activeCount;
            clusterDistance = Mathf.Clamp01(Vector2.Distance(node.position, activeCenter) / mapRadius);
        }

        var spreadScore = Mathf.Lerp(clusterDistance, distanceFromActive, foodRespawnDistanceBias);
        var score = Mathf.Lerp(spreadScore, outerBias, outerRingSpawnBias);
        return score;
    }

    private void ActivateFoodNode(int index)
    {
        if (index < 0 || index >= foodNodes.Length)
        {
            return;
        }

        foodIsActive[index] = true;
        foodRespawnTimers[index] = 0f;
        foodDepletionLogged[index] = false;

        var node = foodNodes[index];
        node.currentCapacity = node.capacity;
        node.isActive = true;
        node.cooldown01 = 0f;
        foodNodes[index] = node;

        Field.ScrubDisc(node.position, Mathf.Max(0.45f, node.consumeRadius * 0.75f), 0.12f);
        UnityEngine.Debug.Log($"[NeuralSlimeMold] Food node {index} activated at t={simulationTime:F2}s.");
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
            consumeRate = consumeRate,
            isActive = false,
            cooldown01 = 0f
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

    private void ApplyNetworkMaintenance(float dt)
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return;
        }

        var step = Mathf.Max(0f, dt);
        if (step <= 0f)
        {
            return;
        }

        var searchRadius = Mathf.Max(0.1f, connectorSearchRadius);

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var active = IsFoodNodeActive(i);
            var usefulBridge = !active && IsFoodNodeBridgeUseful(i, searchRadius);

            if (active || usefulBridge)
            {
                var desirability = ComputeFoodNodeDesirability(i, searchRadius);
                if (desirability > 0f && bridgeReinforcementWeight > 0f)
                {
                    var reinforce = bridgeReinforcementWeight * desirability * step;
                    DepositDiscIfOpen(node.position, Mathf.Max(0.35f, node.consumeRadius * 0.45f), reinforce);
                }
            }
            else if (staleCorridorDecayBoost > 0f)
            {
                var scrub = staleCorridorDecayBoost * step;
                Field.ScrubDisc(node.position, Mathf.Max(0.45f, node.consumeRadius * 1.05f), scrub);
                Field.ScrubVeinDisc(node.position, Mathf.Max(0.35f, node.consumeRadius * 0.85f), scrub * 0.35f);
            }
        }

        if (bridgeReinforcementWeight > 0f)
        {
            ReinforceUsefulConnectors(step, searchRadius);
        }

        if (hubOrbitSuppression > 0f && useColonyHub)
        {
            SuppressHubOrbiting(step, searchRadius);
        }
    }

    private void TryNucleateBranch(Vector2 position, float heading, float depositAmount, float dt, bool nearFood)
    {
        if (branchSpawnChance <= 0f || dt <= 0f)
        {
            return;
        }

        var corridorTrail = Field.SampleBilinear(position);
        if (corridorTrail < branchSpawnTrailThreshold)
        {
            return;
        }

        var spawnProbability = branchSpawnChance * dt * Mathf.Clamp01((corridorTrail - branchSpawnTrailThreshold) / Mathf.Max(0.001f, branchSpawnTrailThreshold + 0.05f));
        if (rng.NextFloat01() > spawnProbability)
        {
            return;
        }

        var branchSign = rng.NextFloat01() < 0.5f ? -1f : 1f;
        var branchAngle = Mathf.Deg2Rad * rng.Range(BranchSpawnAngleMinDegrees, BranchSpawnAngleMaxDegrees) * branchSign;
        var branchDirection = Direction(Mathf.Repeat(heading + branchAngle, Mathf.PI * 2f));
        var branchSamplePos = position + (branchDirection * BranchSpawnSideSampleDistance);
        var aheadSamplePos = position + (Direction(heading) * BranchSpawnFrontSampleDistance);

        var branchTrail = Field.SampleBilinear(branchSamplePos);
        var aheadTrail = Field.SampleBilinear(aheadSamplePos);
        var branchNutrient = Mathf.Clamp01(SampleNutrientField(branchSamplePos));

        var openSpaceBias = Mathf.Clamp01((corridorTrail - aheadTrail) * 2.2f);
        var branchValue = Mathf.Clamp01((branchTrail * 2.4f) + (branchNutrient * 0.9f) + (openSpaceBias * 0.7f));
        var promoted = branchTrail >= branchPromotionThreshold || branchValue >= branchPromotionThreshold;

        var branchDepositScale = promoted ? PromotedBranchDepositScale : ExploratoryBranchDepositScale;
        var branchDeposit = depositAmount * branchDepositScale * Mathf.Lerp(0.65f, 1.12f, branchValue);

        DepositKernelIfOpen(branchSamplePos, branchDeposit);

        if (!promoted && branchRetractionBoost > 0f && !nearFood)
        {
            var retractAmount = branchRetractionBoost * dt * Mathf.Clamp01(1f - branchValue);
            Field.ScrubDisc(branchSamplePos, 0.85f, retractAmount);
            return;
        }

        if (promoted)
        {
            DepositDiscIfOpen(branchSamplePos, 0.6f, branchDeposit * 0.4f);
        }
    }

    private void ReinforceUsefulConnectors(float dt, float searchRadius)
    {
        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (!IsFoodNodeActive(i))
            {
                continue;
            }

            var node = foodNodes[i];
            var desirability = ComputeFoodNodeDesirability(i, searchRadius);

            if (useColonyHub)
            {
                ReinforceConnectorCorridor(colonyHub, node.position, desirability, dt);
            }

            for (var j = i + 1; j < foodNodes.Length; j++)
            {
                if (!IsFoodNodeActive(j))
                {
                    continue;
                }

                var other = foodNodes[j];
                if ((other.position - node.position).sqrMagnitude > (searchRadius * searchRadius * 4f))
                {
                    continue;
                }

                var pairDesirability = Mathf.Min(desirability, ComputeFoodNodeDesirability(j, searchRadius));
                ReinforceConnectorCorridor(node.position, other.position, pairDesirability, dt);
            }
        }
    }

    private void ReinforceConnectorCorridor(Vector2 a, Vector2 b, float desirability, float dt)
    {
        var corridorWidth = Mathf.Max(0.25f, connectorCorridorWidth);
        var segment = b - a;
        var length = segment.magnitude;
        if (length <= 0.0001f)
        {
            return;
        }

        var steps = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.7f, corridorWidth * 0.8f)));
        var dir = segment / length;

        for (var s = 0; s <= steps; s++)
        {
            var t = s / (float)steps;
            var samplePos = Vector2.Lerp(a, b, t);
            var trailAtSample = Field.SampleBilinear(samplePos);
            if (trailAtSample < ConnectorReinforceTrailThreshold)
            {
                continue;
            }

            var progressBoost = 1f - Mathf.Abs((t * 2f) - 1f) * 0.35f;
            var alignmentTrail = Mathf.Max(
                Field.SampleBilinear(samplePos + (dir * 0.6f)),
                Field.SampleBilinear(samplePos - (dir * 0.6f)));

            var reinforce = bridgeReinforcementWeight
                            * desirability
                            * Mathf.Lerp(0.6f, 1.3f, Mathf.Clamp01((trailAtSample * 3.5f) + (alignmentTrail * 1.2f)))
                            * progressBoost
                            * dt;

            DepositDiscIfOpen(samplePos, corridorWidth * 0.45f, reinforce);
            DepositVeinDiscIfOpen(samplePos, corridorWidth * 0.32f, reinforce * 0.5f);
        }
    }

    private void ComputeConnectorSensorSamples(Vector2 position, float heading, float sensorAngle, float sensorDistance, out float connectorEdge, out float connectorCenterBias, out float connectorStrength)
    {
        var leftPos = position + (Direction(heading - sensorAngle) * sensorDistance);
        var centerPos = position + (Direction(heading) * sensorDistance);
        var rightPos = position + (Direction(heading + sensorAngle) * sensorDistance);

        var left = SampleConnectorDemand(leftPos);
        var center = SampleConnectorDemand(centerPos);
        var right = SampleConnectorDemand(rightPos);

        connectorEdge = right - left;
        connectorCenterBias = center - ((left + right) * 0.5f);
        connectorStrength = Mathf.Max(left, Mathf.Max(center, right));
    }

    private float SampleConnectorDemand(Vector2 position)
    {
        if (foodNodes == null || foodNodes.Length == 0)
        {
            return 0f;
        }

        var first = 0f;
        var second = 0f;
        var third = 0f;
        var pairRange = Mathf.Max(0.1f, connectorSearchRadius) * 2f;

        void PushScore(float score)
        {
            if (score > first)
            {
                third = second;
                second = first;
                first = score;
            }
            else if (score > second)
            {
                third = second;
                second = score;
            }
            else if (score > third)
            {
                third = score;
            }
        }

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var node = foodNodes[i];
            var nodeWeight = ComputeConnectorNodeWeight(i, pairRange);
            if (nodeWeight <= 0.0001f)
            {
                continue;
            }

            if (useColonyHub)
            {
                PushScore(EvaluateConnectorDemand(position, colonyHub, node.position, nodeWeight));
            }

            for (var j = i + 1; j < foodNodes.Length; j++)
            {
                var otherWeight = ComputeConnectorNodeWeight(j, pairRange);
                if (otherWeight <= 0.0001f)
                {
                    continue;
                }

                var other = foodNodes[j];
                if ((other.position - node.position).sqrMagnitude > pairRange * pairRange)
                {
                    continue;
                }

                PushScore(EvaluateConnectorDemand(position, node.position, other.position, Mathf.Min(nodeWeight, otherWeight)));
            }
        }

        var combined = first + (second * 0.55f) + (third * 0.30f);

        return Mathf.Clamp01(combined);
    }

    private bool IsTangentialHubOrbit(Vector2 position, float heading)
    {
        if (!IsNearHub(position))
        {
            return false;
        }

        var toAgent = position - colonyHub;
        if (toAgent.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        var radial = toAgent.normalized;
        var tangent = new Vector2(-radial.y, radial.x);
        var forward = Direction(heading);

        var radialDot = Mathf.Abs(Vector2.Dot(forward, radial));
        var tangentialDot = Mathf.Abs(Vector2.Dot(forward, tangent));
        if (tangentialDot <= radialDot * 1.08f)
        {
            return false;
        }

        return ComputeUsefulConnectorAlignment(position, forward) < 0.5f;
    }

    private float ComputeUsefulConnectorAlignment(Vector2 position, Vector2 forward)
    {
        var bestAlignment = 0f;
        var pairRange = Mathf.Max(0.1f, connectorSearchRadius) * 2f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var nodeWeight = ComputeConnectorNodeWeight(i, pairRange);
            if (nodeWeight <= 0.0001f)
            {
                continue;
            }

            if (useColonyHub)
            {
                bestAlignment = Mathf.Max(bestAlignment, ComputeSegmentAlignment(position, forward, colonyHub, foodNodes[i].position));
            }

            for (var j = i + 1; j < foodNodes.Length; j++)
            {
                var otherWeight = ComputeConnectorNodeWeight(j, pairRange);
                if (otherWeight <= 0.0001f)
                {
                    continue;
                }

                if ((foodNodes[j].position - foodNodes[i].position).sqrMagnitude > pairRange * pairRange)
                {
                    continue;
                }

                bestAlignment = Mathf.Max(bestAlignment, ComputeSegmentAlignment(position, forward, foodNodes[i].position, foodNodes[j].position));
            }
        }

        return Mathf.Clamp01(bestAlignment);
    }

    private float ComputeSegmentAlignment(Vector2 position, Vector2 forward, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var length = segment.magnitude;
        if (length <= 0.0001f)
        {
            return 0f;
        }

        var closest = ClosestPointOnSegment(position, a, b);
        var dist = Vector2.Distance(position, closest);
        if (dist > connectorCorridorWidth * ConnectorSoftCorridorOuterMultiplier)
        {
            return 0f;
        }

        var dir = segment / length;
        return Mathf.Abs(Vector2.Dot(forward, dir));
    }

    private float ComputeConnectorNodeWeight(int index, float searchRadius)
    {
        if (index < 0 || index >= foodNodes.Length)
        {
            return 0f;
        }

        var node = foodNodes[index];
        var active = IsFoodNodeActive(index);
        if (active)
        {
            var fill = Mathf.Clamp01(node.currentCapacity / Mathf.Max(0.01f, node.capacity));
            return Mathf.Lerp(0.45f, 1f, fill);
        }

        if (IsFoodNodeBridgeUseful(index, searchRadius))
        {
            return 0.22f;
        }

        return 0f;
    }

    private float EvaluateConnectorDemand(Vector2 position, Vector2 a, Vector2 b, float connectorWeight)
    {
        if (connectorWeight <= 0.0001f)
        {
            return 0f;
        }

        var softOuter = Mathf.Max(0.25f, connectorCorridorWidth * ConnectorSoftCorridorOuterMultiplier);
        var closest = ClosestPointOnSegment(position, a, b);
        var distToSegment = Vector2.Distance(position, closest);
        var corridor = 1f - Mathf.Clamp01(distToSegment / softOuter);
        corridor *= corridor;
        if (corridor <= 0f)
        {
            return 0f;
        }

        var direct = Vector2.Distance(a, b);
        var via = Vector2.Distance(position, a) + Vector2.Distance(position, b);
        var progress = Mathf.Clamp01(1f - Mathf.Max(0f, via - direct) / Mathf.Max(0.01f, direct * ConnectorSoftProgressSlack));

        var localTrail = Mathf.Clamp01(Field.SampleBilinear(position) * 3.2f);
        var support = Mathf.Lerp(0.35f, 1f, localTrail);

        var score = corridor * Mathf.Lerp(0.55f, 1f, progress) * support * connectorWeight;
        return Mathf.Clamp01(score);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSqr = ab.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
        {
            return a;
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
        return a + (ab * t);
    }

    private void SuppressHubOrbiting(float dt, float searchRadius)
    {
        var orbitRadius = Mathf.Max(colonyHubRadius * 1.2f, Mathf.Min(hubInfluenceRadius, colonyHubRadius + searchRadius));

        for (var i = 0; i < HubOrbitSampleCount; i++)
        {
            var t = (i / (float)HubOrbitSampleCount) * Mathf.PI * 2f;
            var radial = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            var tangent = new Vector2(-radial.y, radial.x);
            var samplePos = colonyHub + radial * orbitRadius;

            var tangentTrail = (Field.SampleBilinear(samplePos + (tangent * 0.8f)) + Field.SampleBilinear(samplePos - (tangent * 0.8f))) * 0.5f;
            var radialTrail = (Field.SampleBilinear(samplePos + (radial * 0.8f)) + Field.SampleBilinear(samplePos - (radial * 0.8f))) * 0.5f;

            var tangentialBias = tangentTrail - radialTrail;
            if (tangentialBias <= nonUsefulLoopTrailThreshold * 0.35f)
            {
                continue;
            }

            var useful = IsNearAnyActiveFood(samplePos, searchRadius * 0.45f) || IsConnectorDirectionUseful(samplePos, searchRadius);
            if (useful)
            {
                continue;
            }

            var scrub = hubOrbitSuppression * tangentialBias * dt;
            Field.ScrubDisc(samplePos, 0.95f, scrub);
        }
    }

    private bool IsConnectorDirectionUseful(Vector2 position, float searchRadius)
    {
        var bestScore = 0f;
        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (!IsFoodNodeActive(i))
            {
                continue;
            }

            var node = foodNodes[i];
            var distToNode = Vector2.Distance(position, node.position);
            var distToHub = useColonyHub ? Vector2.Distance(position, colonyHub) : 0f;

            var direct = useColonyHub ? Vector2.Distance(node.position, colonyHub) : Mathf.Max(0.01f, node.consumeRadius);
            var split = distToNode + distToHub;

            var shortening = useColonyHub
                ? Mathf.Clamp01(1f - (split / Mathf.Max(0.01f, direct * 1.5f)))
                : Mathf.Clamp01(1f - (distToNode / Mathf.Max(0.01f, searchRadius * 2f)));

            var proximity = Mathf.Clamp01(1f - (distToNode / Mathf.Max(0.01f, searchRadius * 1.8f)));
            var score = Mathf.Max(shortening, proximity * 0.75f);
            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        return bestScore > 0.2f;
    }

    private bool IsFoodNodeActive(int index)
    {
        if (index < 0 || index >= foodNodes.Length)
        {
            return false;
        }

        var node = foodNodes[index];
        return foodIsActive[index] && node.isActive && node.currentCapacity > 0f;
    }

    private float ComputeFoodNodeDesirability(int index, float searchRadius)
    {
        if (index < 0 || index >= foodNodes.Length)
        {
            return 0f;
        }

        var node = foodNodes[index];
        var active = IsFoodNodeActive(index);
        var activity = 0f;

        if (active)
        {
            var fill = Mathf.Clamp01(node.currentCapacity / Mathf.Max(0.01f, node.capacity));
            activity = Mathf.Lerp(0.25f, 1f, fill);
        }

        var neighborhood = 0f;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (i == index || !IsFoodNodeActive(i))
            {
                continue;
            }

            var dist = Vector2.Distance(node.position, foodNodes[i].position);
            neighborhood += Mathf.Clamp01(1f - (dist / Mathf.Max(0.01f, searchRadius * 2f)));
        }

        neighborhood = Mathf.Clamp01(neighborhood * 0.5f);
        if (!active)
        {
            return neighborhood * 0.5f;
        }

        return Mathf.Clamp01(activity * 0.7f + neighborhood * 0.3f);
    }

    private bool IsFoodNodeBridgeUseful(int index, float searchRadius)
    {
        if (index < 0 || index >= foodNodes.Length)
        {
            return false;
        }

        var pivot = foodNodes[index].position;
        var activeNeighborCount = 0;

        for (var i = 0; i < foodNodes.Length; i++)
        {
            if (i == index || !IsFoodNodeActive(i))
            {
                continue;
            }

            var dist = Vector2.Distance(pivot, foodNodes[i].position);
            if (dist <= searchRadius * 1.75f)
            {
                activeNeighborCount++;
            }
        }

        if (activeNeighborCount >= 2)
        {
            return true;
        }

        if (useColonyHub && activeNeighborCount >= 1)
        {
            return Vector2.Distance(pivot, colonyHub) <= searchRadius * 1.75f;
        }

        return false;
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
            if (!foodIsActive[i] || node.currentCapacity <= 0f)
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

    private float ComputeObstacleAvoidanceTurn(Vector2 position, float heading)
    {
        if (!useWorldObstacles || obstacleAvoidanceStrength <= 0f)
        {
            return 0f;
        }

        var probeDistance = Mathf.Max(0.35f, ObstacleProbeDistance + obstaclePadding);
        var forward = Direction(heading);
        var left = Direction(heading - (Mathf.PI * 0.35f));
        var right = Direction(heading + (Mathf.PI * 0.35f));

        var aheadBlocked = IsBlocked(position + (forward * probeDistance), 0f) ? 1f : 0f;
        var leftBlocked = IsBlocked(position + (left * probeDistance), 0f) ? 1f : 0f;
        var rightBlocked = IsBlocked(position + (right * probeDistance), 0f) ? 1f : 0f;
        var immediateBlocked = IsBlocked(position, 0f) ? 1f : 0f;

        var sideBias = rightBlocked - leftBlocked;
        var frontalDirection = Mathf.Abs(sideBias) > 0.001f
            ? Mathf.Sign(sideBias)
            : Mathf.Sign(Mathf.Sin((position.x * 0.73f) + (position.y * 0.37f) + heading));
        var frontalBias = aheadBlocked * frontalDirection * 0.9f;
        var recoverySign = immediateBlocked > 0f ? ((rng.NextFloat01() < 0.5f) ? -1f : 1f) : 0f;
        var recoveryTurn = immediateBlocked * recoverySign;

        var turn = sideBias + frontalBias + recoveryTurn;
        return Mathf.Clamp(turn, -1f, 1f);
    }

    private float ComputeCorridorSteer(Vector2 position, float heading)
    {
        if (!useWorldObstacles || corridorBands == null || corridorBands.Length == 0)
        {
            return 0f;
        }

        var bestWeight = 0f;
        var bestCenter = Vector2.zero;

        for (var i = 0; i < corridorBands.Length; i++)
        {
            var band = corridorBands[i];
            var halfSize = new Vector2(Mathf.Max(0.1f, band.size.x * 0.5f), Mathf.Max(0.1f, band.size.y * 0.5f));
            var local = Rotate(position - band.center, -band.angleDegrees * Mathf.Deg2Rad);

            var dx = Mathf.Abs(local.x) / halfSize.x;
            var dy = Mathf.Abs(local.y) / halfSize.y;
            var inside = Mathf.Clamp01(1f - Mathf.Max(dx, dy));
            if (inside <= 0f)
            {
                continue;
            }

            var weight = inside * Mathf.Max(0f, band.strength);
            if (weight <= bestWeight)
            {
                continue;
            }

            bestWeight = weight;
            bestCenter = band.center;
        }

        if (bestWeight <= 0f)
        {
            return 0f;
        }

        return ComputeSteerTowardPoint(position, heading, bestCenter) * Mathf.Clamp01(bestWeight);
    }

    private void MoveWithObstacleCollision(ref NeuralSlimeMoldAgent agent, float moveDistance)
    {
        if (moveDistance <= 0f)
        {
            return;
        }

        var candidate = agent.position + (Direction(agent.heading) * moveDistance);
        if (!IsBlocked(candidate, 0f))
        {
            agent.position = candidate;
            return;
        }

        var leftHeading = Mathf.Repeat(agent.heading - (Mathf.PI * 0.5f), Mathf.PI * 2f);
        var leftCandidate = agent.position + (Direction(leftHeading) * moveDistance * 0.65f);
        if (!IsBlocked(leftCandidate, 0f))
        {
            agent.heading = leftHeading;
            agent.position = leftCandidate;
            return;
        }

        var rightHeading = Mathf.Repeat(agent.heading + (Mathf.PI * 0.5f), Mathf.PI * 2f);
        var rightCandidate = agent.position + (Direction(rightHeading) * moveDistance * 0.65f);
        if (!IsBlocked(rightCandidate, 0f))
        {
            agent.heading = rightHeading;
            agent.position = rightCandidate;
            return;
        }

        agent.heading = Mathf.Repeat(agent.heading + Mathf.PI, Mathf.PI * 2f);
    }

    private bool IsBlocked(Vector2 position, float extraPadding)
    {
        if (!useWorldObstacles || obstacles == null || obstacles.Length == 0)
        {
            return false;
        }

        var padding = Mathf.Max(0f, obstaclePadding + extraPadding);
        for (var i = 0; i < obstacles.Length; i++)
        {
            if (Contains(obstacles[i], position, padding))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(NeuralObstacle obstacle, Vector2 point, float padding)
    {
        if (obstacle.shape == NeuralObstacleShape.Circle)
        {
            var radius = Mathf.Max(0.1f, obstacle.radius) + padding;
            return (point - obstacle.center).sqrMagnitude <= (radius * radius);
        }

        var halfSize = new Vector2(
            Mathf.Max(0.1f, obstacle.size.x * 0.5f) + padding,
            Mathf.Max(0.1f, obstacle.size.y * 0.5f) + padding);
        var delta = point - obstacle.center;
        return Mathf.Abs(delta.x) <= halfSize.x && Mathf.Abs(delta.y) <= halfSize.y;
    }

    private void BuildBlockedFieldMask()
    {
        if (Field == null)
        {
            return;
        }

        var maskLength = Field.Width * Field.Height;
        if (!useWorldObstacles || obstacles == null || obstacles.Length == 0)
        {
            blockedFieldMask = Array.Empty<bool>();
            Field.SetBlockedMask(null);
            return;
        }

        blockedFieldMask = new bool[maskLength];
        for (var y = 0; y < Field.Height; y++)
        {
            for (var x = 0; x < Field.Width; x++)
            {
                var idx = (y * Field.Width) + x;
                var world = Field.GridToWorld(x, y);
                blockedFieldMask[idx] = IsBlocked(world, 0f);
            }
        }

        Field.SetBlockedMask(blockedFieldMask);
    }

    private void DepositKernelIfOpen(Vector2 position, float amount)
    {
        if (amount <= 0f || IsBlocked(position, 0f))
        {
            return;
        }

        Field.DepositKernel(position, amount);
    }

    private void DepositDiscIfOpen(Vector2 position, float radius, float amount)
    {
        if (amount <= 0f || radius <= 0f || IsBlocked(position, 0f))
        {
            return;
        }

        Field.DepositDisc(position, radius, amount);
    }

    private void DepositVeinKernelIfOpen(Vector2 position, float amount)
    {
        if (amount <= 0f || IsBlocked(position, 0f))
        {
            return;
        }

        Field.DepositVeinKernel(position, amount);
    }

    private void DepositVeinDiscIfOpen(Vector2 position, float radius, float amount)
    {
        if (amount <= 0f || radius <= 0f || IsBlocked(position, 0f))
        {
            return;
        }

        Field.DepositVeinDisc(position, radius, amount);
    }

    private Vector2 SampleOpenFoodPosition(SeededRng placementRng)
    {
        var best = Vector2.zero;

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var pos = new Vector2(
                placementRng.Range(-mapSize.x * 0.4f, mapSize.x * 0.4f),
                placementRng.Range(-mapSize.y * 0.4f, mapSize.y * 0.4f));
            pos = ClampNodeInsideBounds(pos);
            best = pos;
            if (!IsBlocked(pos, 0f))
            {
                return pos;
            }
        }

        return ResolveToOpenPosition(best, 1.25f);
    }

    private Vector2 ResolveToOpenPosition(Vector2 position, float searchStep)
    {
        var clamped = ClampNodeInsideBounds(position);
        if (!IsBlocked(clamped, 0f))
        {
            return clamped;
        }

        var step = Mathf.Max(0.2f, searchStep);
        for (var ring = 1; ring <= 12; ring++)
        {
            var radius = ring * step;
            const int samples = 16;
            for (var i = 0; i < samples; i++)
            {
                var angle = (i / (float)samples) * Mathf.PI * 2f;
                var candidate = clamped + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                candidate = ClampNodeInsideBounds(candidate);
                if (!IsBlocked(candidate, 0f))
                {
                    return candidate;
                }
            }
        }

        return clamped;
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        var c = Mathf.Cos(radians);
        var s = Mathf.Sin(radians);
        return new Vector2((value.x * c) - (value.y * s), (value.x * s) + (value.y * c));
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
