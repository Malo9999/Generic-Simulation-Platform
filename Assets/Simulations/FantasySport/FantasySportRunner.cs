using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int PlayersPerTeam = 8;
    private const int TotalAthletes = PlayersPerTeam * 2;
    private const int GoalkeeperIndexPerTeam = PlayersPerTeam - 1;

    private const float TeamSeparationRadius = 2.4f;
    private const float BoundaryAvoidanceDistance = 3f;
    private const float PressureRadius = 2.2f;
    private const float OpenThreshold = 2.6f;
    private const float PassLeadTime = 0.3f;
    private const float PassSpeed = 15f;
    private const float ShootSpeed = 23f;
    private const float ShootRange = 16f;
    private const float PassCooldownSeconds = 0.65f;
    private const float ThrowWindowSeconds = 1.5f;
    private const float ReceiverLockSeconds = 0.45f;
    private const float StealDistance = 0.7f;
    private const float AthleteRadius = 0.75f;
    private const float BallRadius = 0.35f;
    private const float BumperRadius = 1.05f;
    private const float BumperMinDistance = 4.5f;
    private const float BumperBallRestitution = 1.06f;
    private const float BumperAthleteRestitution = 0.3f;
    private const int BumperCount = 6;
    private static readonly Vector2 PadSize = new Vector2(4.2f, 2.7f);
    private static readonly Color Team0Color = new Color(0.2f, 0.78f, 1f, 1f);
    private static readonly Color Team1Color = new Color(1f, 0.45f, 0.25f, 1f);

    private enum AthleteState { BallCarrier, ChaseFreeBall, SupportAttack, PressCarrier, MarkLane, GoalkeeperHome }

    [SerializeField] private FantasySportRules rules = new FantasySportRules();
    [SerializeField] private float athleteIconScaleMultiplier = 1.3f;
    [SerializeField] private bool showDebugPlayerState;
    [SerializeField] private bool showMechanicDebug;

    private Transform[] athletes;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] stunTimers;
    private float[] tackleCooldowns;
    private float[] passCooldowns;
    private float[] laneByPlayer;
    private IRng[] athleteRngs;
    private AthleteState[] athleteStates;
    private int[] assignedTargetIndex;
    private TextMesh[] debugStateLabels;
    private SpriteRenderer[] possessionRings;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;

    private SimulationSceneGraph sceneGraph;
    private Transform ballTransform;
    private Vector2 ballPos;
    private Vector2 ballVel;
    private int ballOwnerIndex = -1;
    private int ballOwnerTeam = -1;

    private FantasySportHazards.Bumper[] bumpers = System.Array.Empty<FantasySportHazards.Bumper>();
    private FantasySportHazards.SpeedPad[] speedPads = System.Array.Empty<FantasySportHazards.SpeedPad>();

    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private float elapsedMatchTime;
    private bool matchFinished;
    private int scoreTeam0;
    private int scoreTeam1;

    private int lastThrowTeam = -1;
    private float lastThrowTime = -999f;
    private int intendedReceiverIndex = -1;
    private float receiverLockUntilTime = -999f;

    private Text hudText;
    private Text scoreboardText;
    private int lastHudSecond = -1;
    private int lastScoreboardSecond = -1;
    private int lastScoreboardTeam0 = int.MinValue;
    private int lastScoreboardTeam1 = int.MinValue;
    private int previousPossessionTeam = int.MinValue;
    private int nextEntityId;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "FantasySport");
        EnsureMainCamera();
        BuildAthletes(config);
        EnsureBall();
        BuildHazards();
        FindHudText();
        FindScoreboardText();
        ResetMatchState();
        ResetKickoff();
        UpdateHud(force: true);
        UpdateScoreboardUI(force: true);
    }

    public void Tick(int tickIndex, float dt)
    {
        if (athletes == null || rules == null)
        {
            return;
        }

        if (!matchFinished)
        {
            elapsedMatchTime += dt;
            if (elapsedMatchTime >= rules.matchSeconds)
            {
                elapsedMatchTime = rules.matchSeconds;
                matchFinished = true;
                ballVel = Vector2.zero;
            }
        }

        for (var i = 0; i < athletes.Length; i++)
        {
            stunTimers[i] = Mathf.Max(0f, stunTimers[i] - dt);
            tackleCooldowns[i] = Mathf.Max(0f, tackleCooldowns[i] - dt);
            passCooldowns[i] = Mathf.Max(0f, passCooldowns[i] - dt);
        }

        RefreshAssignments();
        ResolveBallCarrierDecision();
        UpdateAthletes(dt);
        ResolveTackleEvents();
        UpdateBall(dt);
        ResolvePickup();
        ResolveGoal(tickIndex);
        ApplyTransforms(dt);
        UpdateHud(force: false);
        UpdateScoreboardUI(force: false);
    }

    public void Shutdown()
    {
        if (athletes != null)
        {
            for (var i = 0; i < athletes.Length; i++)
            {
                if (athletes[i] != null)
                {
                    Destroy(athletes[i].gameObject);
                }
            }
        }

        if (ballTransform != null)
        {
            Destroy(ballTransform.gameObject);
            ballTransform = null;
        }

        var hazardRoot = sceneGraph != null ? sceneGraph.WorldObjectsRoot.Find("FantasySportHazards") : null;
        if (hazardRoot != null)
        {
            Destroy(hazardRoot.gameObject);
        }
    }

    private void BuildAthletes(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;
        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64f) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64f) * 0.5f);

        athletes = new Transform[TotalAthletes];
        identities = new EntityIdentity[TotalAthletes];
        positions = new Vector2[TotalAthletes];
        velocities = new Vector2[TotalAthletes];
        stunTimers = new float[TotalAthletes];
        tackleCooldowns = new float[TotalAthletes];
        passCooldowns = new float[TotalAthletes];
        laneByPlayer = new float[TotalAthletes];
        athleteRngs = new IRng[TotalAthletes];
        athleteStates = new AthleteState[TotalAthletes];
        assignedTargetIndex = new int[TotalAthletes];
        debugStateLabels = new TextMesh[TotalAthletes];
        possessionRings = new SpriteRenderer[TotalAthletes];
        pipelineRenderers = new GameObject[TotalAthletes];
        visualKeys = new VisualKey[TotalAthletes];

        ResolveArtPipeline();

        var spawnRng = RngService.Fork("SIM:FantasySport:SPAWN");
        for (var i = 0; i < TotalAthletes; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{i}");
            laneByPlayer[i] = ResolveLaneForTeamIndex(i % PlayersPerTeam);
            var teamId = i < PlayersPerTeam ? 0 : 1;
            var teamIndex = i % PlayersPerTeam;
            var role = teamIndex == GoalkeeperIndexPerTeam ? "goalkeeper" : (teamIndex <= 2 ? "offense" : "defense");

            var identity = IdentityService.Create(nextEntityId++, teamId, role, 3, config?.seed ?? 0, "FantasySport");
            var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, teamId);
            var athlete = new GameObject($"Sim_{identity.entityId:0000}");
            athlete.transform.SetParent(groupRoot, false);

            var visualKey = VisualKeyBuilder.Create("FantasySport", "athlete", identity.entityId, role, "run", FacingMode.Auto, identity.teamId);
            var visualParent = athlete.transform;
            if (activePipeline != null)
            {
                pipelineRenderers[i] = activePipeline.CreateRenderer(visualKey, athlete.transform);
                if (pipelineRenderers[i] != null)
                {
                    visualParent = pipelineRenderers[i].transform;
                }
            }

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(visualParent, false);
            EntityIconFactory.BuildAthlete(iconRoot.transform, identity);
            iconRoot.transform.localScale *= Mathf.Max(0.1f, athleteIconScaleMultiplier);
            TintAthlete(iconRoot.transform, teamId, role == "goalkeeper");

            var ring = new GameObject("PossessionRing").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(athlete.transform, false);
            ring.sprite = PrimitiveSpriteLibrary.CircleOutline();
            ring.color = teamId == 0 ? Team0Color : Team1Color;
            ring.transform.localScale = Vector3.one * 1.1f;
            ring.enabled = false;
            RenderOrder.Apply(ring, RenderOrder.SelectionRing);
            possessionRings[i] = ring;

            var label = new GameObject("DebugState").AddComponent<TextMesh>();
            label.transform.SetParent(athlete.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            label.characterSize = 0.1f;
            label.fontSize = 64;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            debugStateLabels[i] = label;

            athlete.transform.localScale = Vector3.one * spawnRng.Range(0.95f, 1.1f);
            athletes[i] = athlete.transform;
            identities[i] = identity;
            visualKeys[i] = visualKey;
            assignedTargetIndex[i] = -1;
        }

        ResetAthleteFormationAndVelocities();
    }

    private void BuildHazards()
    {
        var rng = RngService.Fork("SIM:FantasySport:HAZARDS");
        var goalHeight = GetGoalHeight();
        bumpers = FantasySportHazards.GenerateBumpers(rng, BumperCount, halfWidth, halfHeight, BumperRadius, BumperMinDistance, rules.goalDepth, goalHeight);
        speedPads = FantasySportHazards.GenerateSymmetricPads(halfWidth, halfHeight, PadSize, rules.goalDepth, bumpers);

        var oldRoot = sceneGraph.WorldObjectsRoot.Find("FantasySportHazards");
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        var root = new GameObject("FantasySportHazards").transform;
        root.SetParent(sceneGraph.WorldObjectsRoot, false);

        for (var i = 0; i < speedPads.Length; i++)
        {
            var go = new GameObject($"SpeedPad_{i}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = speedPads[i].area.center;
            go.transform.localScale = new Vector3(speedPads[i].area.width, speedPads[i].area.height, 1f);
            var fill = go.AddComponent<SpriteRenderer>();
            fill.sprite = PrimitiveSpriteLibrary.RoundedRectFill();
            var isSpeedPad = speedPads[i].speedMultiplier >= 1f;
            fill.color = isSpeedPad ? new Color(0.46f, 1f, 0.55f, 0.26f) : new Color(0.56f, 0.33f, 0.86f, 0.28f);
            RenderOrder.Apply(fill, RenderOrder.WorldDeco);

            var outline = new GameObject("Outline").AddComponent<SpriteRenderer>();
            outline.transform.SetParent(go.transform, false);
            outline.sprite = PrimitiveSpriteLibrary.RoundedRectOutline();
            outline.color = isSpeedPad ? new Color(0.08f, 0.2f, 0.08f, 0.68f) : new Color(0.2f, 0.08f, 0.3f, 0.72f);
            RenderOrder.Apply(outline, RenderOrder.WorldAbove);
        }

        for (var i = 0; i < bumpers.Length; i++)
        {
            var go = new GameObject($"Bumper_{i}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = bumpers[i].position;
            go.transform.localScale = Vector3.one * (bumpers[i].radius * 2f);
            var fill = go.AddComponent<SpriteRenderer>();
            fill.sprite = PrimitiveSpriteLibrary.CircleFill();
            fill.color = new Color(0.8f, 0.82f, 0.88f, 1f);
            RenderOrder.Apply(fill, RenderOrder.WorldDeco + 4);

            var outline = new GameObject("Outline").AddComponent<SpriteRenderer>();
            outline.transform.SetParent(go.transform, false);
            outline.sprite = PrimitiveSpriteLibrary.CircleOutline();
            outline.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            RenderOrder.Apply(outline, RenderOrder.WorldDeco + 5);
        }
    }

    private void ResetMatchState()
    {
        elapsedMatchTime = 0f;
        matchFinished = false;
        scoreTeam0 = 0;
        scoreTeam1 = 0;
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        ballVel = Vector2.zero;
        lastThrowTeam = -1;
        lastThrowTime = -999f;
        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
        previousPossessionTeam = int.MinValue;
        lastScoreboardSecond = -1;
        lastScoreboardTeam0 = int.MinValue;
        lastScoreboardTeam1 = int.MinValue;
    }

    private void ResetKickoff()
    {
        ResetAthleteFormationAndVelocities();
        ballPos = Vector2.zero;
        ballVel = Vector2.zero;
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
        lastThrowTeam = -1;
        lastThrowTime = -999f;
    }

    private void ResetAthleteFormationAndVelocities()
    {
        var yBand = halfHeight * 0.62f;
        for (var i = 0; i < TotalAthletes; i++)
        {
            var teamId = identities[i].teamId;
            var teamIndex = i % PlayersPerTeam;
            var attackSign = teamId == 0 ? 1f : -1f;
            var goalX = teamId == 0 ? -halfWidth : halfWidth;

            if (teamIndex == GoalkeeperIndexPerTeam)
            {
                positions[i] = new Vector2(goalX + (teamId == 0 ? 2f : -2f), 0f);
            }
            else
            {
                var laneY = laneByPlayer[i] * yBand;
                var isOffense = teamIndex <= 2;
                var x = attackSign * (halfWidth * (isOffense ? 0.2f : 0.38f));
                positions[i] = new Vector2(x, laneY);
            }

            velocities[i] = Vector2.zero;
            stunTimers[i] = 0f;
            tackleCooldowns[i] = 0f;
            passCooldowns[i] = 0f;
        }
    }

    private void RefreshAssignments()
    {
        previousPossessionTeam = ballOwnerTeam;
        var chaser0 = FindClosestPlayerToPoint(0, ballPos, includeGoalkeeper: false);
        var chaser1 = FindClosestPlayerToPoint(1, ballPos, includeGoalkeeper: false);

        for (var i = 0; i < TotalAthletes; i++)
        {
            var teamId = identities[i].teamId;
            var isKeeper = IsGoalkeeper(i);

            if (i == ballOwnerIndex)
            {
                athleteStates[i] = AthleteState.BallCarrier;
                continue;
            }

            if (isKeeper)
            {
                athleteStates[i] = AthleteState.GoalkeeperHome;
                continue;
            }

            if (ballOwnerIndex < 0)
            {
                athleteStates[i] = i == (teamId == 0 ? chaser0 : chaser1) ? AthleteState.ChaseFreeBall : AthleteState.SupportAttack;
                continue;
            }

            if (ballOwnerTeam == teamId)
            {
                athleteStates[i] = AthleteState.SupportAttack;
                continue;
            }

            var press = FindClosestPlayerToPoint(teamId, positions[ballOwnerIndex], includeGoalkeeper: false);
            athleteStates[i] = i == press ? AthleteState.PressCarrier : AthleteState.MarkLane;
        }
    }

    private void ResolveBallCarrierDecision()
    {
        if (ballOwnerIndex < 0 || matchFinished || stunTimers[ballOwnerIndex] > 0f)
        {
            return;
        }

        var carrier = ballOwnerIndex;
        var teamId = identities[carrier].teamId;
        var carrierPos = positions[carrier];
        var nearestOpponentDist = FindNearestOpponentDistance(carrier);
        var pressured = nearestOpponentDist <= PressureRadius;

        if (passCooldowns[carrier] <= 0f)
        {
            var openTeammate = FindBestOpenTeammate(carrier);
            if (pressured && openTeammate >= 0)
            {
                var target = positions[openTeammate] + (velocities[openTeammate] * PassLeadTime);
                ThrowBall(carrier, target, PassSpeed, openTeammate);
                passCooldowns[carrier] = PassCooldownSeconds;
                return;
            }
        }

        var toGoal = GetOpponentGoalCenter(teamId) - carrierPos;
        if (toGoal.magnitude <= ShootRange)
        {
            var yJitter = athleteRngs[carrier].Range(-GetGoalHeight() * 0.22f, GetGoalHeight() * 0.22f);
            var target = GetOpponentGoalCenter(teamId) + new Vector2(0f, yJitter);
            ThrowBall(carrier, target, ShootSpeed, -1);
        }
    }

    private void ThrowBall(int carrierIndex, Vector2 target, float speed, int receiverIndex)
    {
        var start = positions[carrierIndex];
        var dir = target - start;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = identities[carrierIndex].teamId == 0 ? Vector2.right : Vector2.left;
        }

        dir.Normalize();
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        ballPos = start;
        ballVel = (dir * speed) + (velocities[carrierIndex] * 0.15f);

        lastThrowTeam = identities[carrierIndex].teamId;
        lastThrowTime = elapsedMatchTime;
        intendedReceiverIndex = receiverIndex;
        receiverLockUntilTime = elapsedMatchTime + ReceiverLockSeconds;
    }

    private void UpdateAthletes(float dt)
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            var desired = stunTimers[i] > 0f || matchFinished ? Vector2.zero : ComputeDesiredVelocity(i);
            velocities[i] = Vector2.MoveTowards(velocities[i], desired, rules.accel * dt);
            positions[i] += velocities[i] * dt;
            ClampAthlete(i);
            ResolveAthleteBumperCollision(i);
        }
    }

    private Vector2 ComputeDesiredVelocity(int i)
    {
        var identity = identities[i];
        var keeper = IsGoalkeeper(i);
        var maxSpeed = keeper ? rules.athleteSpeedDefense * 0.85f : (identity.role == "offense" ? rules.athleteSpeedOffense : rules.athleteSpeedDefense);
        maxSpeed *= GetPadSpeedMultiplierAtPosition(positions[i]);

        var target = ComputeHomePosition(i);
        switch (athleteStates[i])
        {
            case AthleteState.BallCarrier:
                if (keeper)
                {
                    var open = FindBestOpenTeammate(i);
                    target = open >= 0 ? positions[open] : ComputeHomePosition(i);
                }
                else
                {
                    target = GetOpponentGoalCenter(identity.teamId);
                }
                break;
            case AthleteState.ChaseFreeBall:
                target = ballPos;
                break;
            case AthleteState.PressCarrier:
                target = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
                break;
            case AthleteState.MarkLane:
                target = Vector2.Lerp(GetOwnGoalCenter(identity.teamId), ballPos, 0.4f) + new Vector2(0f, laneByPlayer[i] * 4f);
                break;
            case AthleteState.GoalkeeperHome:
                target = ComputeGoalkeeperTarget(i);
                break;
        }

        return BuildSteeringVelocity(i, target, maxSpeed);
    }

    private Vector2 ComputeHomePosition(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        if (IsGoalkeeper(athleteIndex))
        {
            return ComputeGoalkeeperTarget(athleteIndex);
        }

        var attack = teamId == 0 ? 1f : -1f;
        var laneY = laneByPlayer[athleteIndex] * halfHeight * 0.65f;
        var baseX = attack * (halfWidth * 0.2f);
        if (ballOwnerTeam == teamId)
        {
            baseX = attack * (halfWidth * 0.28f);
        }
        else if (ballOwnerTeam >= 0)
        {
            baseX = attack * (halfWidth * 0.08f);
        }

        return new Vector2(baseX, Mathf.Clamp(laneY, -halfHeight + 2f, halfHeight - 2f));
    }

    private Vector2 ComputeGoalkeeperTarget(int i)
    {
        var keeperBox = GetKeeperBox(identities[i].teamId);
        var x = identities[i].teamId == 0 ? keeperBox.xMin + 1.5f : keeperBox.xMax - 1.5f;
        var y = Mathf.Clamp(ballPos.y, keeperBox.yMin + 0.5f, keeperBox.yMax - 0.5f);
        return new Vector2(x, y);
    }

    private Vector2 BuildSteeringVelocity(int athleteIndex, Vector2 target, float maxSpeed)
    {
        var athletePos = positions[athleteIndex];
        var seek = (target - athletePos).normalized;
        var separation = Vector2.zero;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == athleteIndex || identities[i].teamId != identities[athleteIndex].teamId)
            {
                continue;
            }

            var delta = athletePos - positions[i];
            var dist = delta.magnitude;
            if (dist > 0.001f && dist < TeamSeparationRadius)
            {
                separation += (delta / dist) * (1f - dist / TeamSeparationRadius);
            }
        }

        var boundary = Vector2.zero;
        if (athletePos.x < -halfWidth + BoundaryAvoidanceDistance) boundary.x += 1f;
        if (athletePos.x > halfWidth - BoundaryAvoidanceDistance) boundary.x -= 1f;
        if (athletePos.y < -halfHeight + BoundaryAvoidanceDistance) boundary.y += 1f;
        if (athletePos.y > halfHeight - BoundaryAvoidanceDistance) boundary.y -= 1f;

        var steering = seek + (separation * 1.4f) + boundary;
        return steering.sqrMagnitude < 0.001f ? Vector2.zero : steering.normalized * maxSpeed;
    }

    private void ResolveTackleEvents()
    {
        if (ballOwnerIndex < 0 || matchFinished)
        {
            return;
        }

        var victim = ballOwnerIndex;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == victim || identities[i].teamId == identities[victim].teamId || tackleCooldowns[i] > 0f)
            {
                continue;
            }

            var dist = Vector2.Distance(positions[i], positions[victim]);
            if (dist > rules.tackleRadius)
            {
                continue;
            }

            var impulse = (positions[victim] - positions[i]).normalized;
            stunTimers[victim] = rules.stunSeconds;
            tackleCooldowns[i] = rules.tackleCooldownSeconds;
            ballOwnerIndex = -1;
            ballOwnerTeam = -1;
            ballPos = positions[victim];
            ballVel = (impulse * rules.tackleImpulse) + (velocities[victim] * 0.2f);
            intendedReceiverIndex = -1;
            receiverLockUntilTime = -999f;
            return;
        }
    }

    private void UpdateBall(float dt)
    {
        if (ballOwnerIndex >= 0)
        {
            var ownerVel = velocities[ballOwnerIndex];
            ballPos = positions[ballOwnerIndex] + (ownerVel.sqrMagnitude > 0.001f ? ownerVel.normalized * rules.carrierForwardOffset : Vector2.zero);
            ballVel = ownerVel;
            return;
        }

        if (matchFinished)
        {
            return;
        }

        ballPos += ballVel * dt;
        ballVel *= Mathf.Clamp01(1f - (rules.ballDamping * dt));

        for (var i = 0; i < speedPads.Length; i++)
        {
            if (speedPads[i].area.Contains(ballPos))
            {
                if (speedPads[i].speedMultiplier >= 1f)
                {
                    ballVel *= 1f + (0.22f * dt);
                }
                else
                {
                    ballVel *= 1f - (0.28f * dt);
                }
            }
        }

        ResolveBallBumperCollision();
        ClampBall();
    }

    private void ResolvePickup()
    {
        if (ballOwnerIndex >= 0 || matchFinished)
        {
            return;
        }

        var caughtByKeeper = TryKeeperCatch();
        if (caughtByKeeper >= 0)
        {
            SetBallOwner(caughtByKeeper);
            return;
        }

        var closest = -1;
        var closestDist = rules.pickupRadius;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (stunTimers[i] > 0f)
            {
                continue;
            }

            var dist = Vector2.Distance(positions[i], ballPos);
            if (dist > closestDist)
            {
                continue;
            }

            if (elapsedMatchTime <= receiverLockUntilTime && intendedReceiverIndex >= 0 && i != intendedReceiverIndex)
            {
                var intendedDist = Vector2.Distance(positions[intendedReceiverIndex], ballPos);
                var canSteal = identities[i].teamId != identities[intendedReceiverIndex].teamId && dist + StealDistance < intendedDist;
                if (!canSteal)
                {
                    continue;
                }
            }

            closest = i;
            closestDist = dist;
        }

        if (closest >= 0)
        {
            SetBallOwner(closest);
        }
    }

    private int TryKeeperCatch()
    {
        for (var team = 0; team <= 1; team++)
        {
            var keeper = GetGoalkeeperIndex(team);
            var box = GetKeeperBox(team);
            if (box.Contains(ballPos) && stunTimers[keeper] <= 0f)
            {
                return keeper;
            }
        }

        return -1;
    }

    private void SetBallOwner(int index)
    {
        ballOwnerIndex = index;
        ballOwnerTeam = identities[index].teamId;
        ballVel = velocities[index];
        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
    }

    private void ResolveGoal(int tickIndex)
    {
        if (matchFinished || ballOwnerIndex >= 0)
        {
            return;
        }

        var throwFresh = elapsedMatchTime - lastThrowTime <= ThrowWindowSeconds;
        if (!throwFresh || lastThrowTeam < 0)
        {
            return;
        }

        if (IsInLeftGoal(ballPos) && lastThrowTeam == 1)
        {
            scoreTeam1++;
            Debug.Log($"[FantasySport] THROW GOAL team=1 score={scoreTeam0}-{scoreTeam1} tick={tickIndex}");
            ResetKickoff();
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
        else if (IsInRightGoal(ballPos) && lastThrowTeam == 0)
        {
            scoreTeam0++;
            Debug.Log($"[FantasySport] THROW GOAL team=0 score={scoreTeam0}-{scoreTeam1} tick={tickIndex}");
            ResetKickoff();
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
    }

    private void ApplyTransforms(float dt)
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            athletes[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            FaceVelocity(athletes[i], velocities[i]);

            if (activePipeline != null && pipelineRenderers[i] != null)
            {
                activePipeline.ApplyVisual(pipelineRenderers[i], visualKeys[i], velocities[i], dt);
            }

            if (debugStateLabels[i] != null)
            {
                debugStateLabels[i].gameObject.SetActive(showDebugPlayerState);
                if (showDebugPlayerState)
                {
                    var dbg = showMechanicDebug ? $" LT:{lastThrowTeam}@{lastThrowTime:0.0} IR:{intendedReceiverIndex}" : string.Empty;
                    debugStateLabels[i].text = $"T{identities[i].teamId} {identities[i].role[0]} {athleteStates[i]}{dbg}";
                }
            }

            possessionRings[i].enabled = ballOwnerIndex == i;
        }

        if (ballTransform != null)
        {
            ballTransform.localPosition = new Vector3(ballPos.x, ballPos.y, 0f);
        }
    }

    private void UpdateHud(bool force)
    {
        if (hudText == null)
        {
            return;
        }

        var remaining = Mathf.Max(0f, rules.matchSeconds - elapsedMatchTime);
        var remainingWhole = Mathf.CeilToInt(remaining);
        if (!force && remainingWhole == lastHudSecond)
        {
            return;
        }

        lastHudSecond = remainingWhole;
        var minutes = remainingWhole / 60;
        var seconds = remainingWhole % 60;
        var possession = ballOwnerTeam >= 0 ? $"Team{ballOwnerTeam}" : "Free";
        hudText.text = $"FantasySport  Team0 {scoreTeam0} : {scoreTeam1} Team1   Time: {minutes:00}:{seconds:00}   Possession: {possession}";
    }


    private void UpdateScoreboardUI(bool force)
    {
        if (scoreboardText == null)
        {
            FindScoreboardText();
            if (scoreboardText == null)
            {
                return;
            }
        }

        var remainingWhole = Mathf.CeilToInt(Mathf.Max(0f, rules.matchSeconds - elapsedMatchTime));
        if (!force && remainingWhole == lastScoreboardSecond && scoreTeam0 == lastScoreboardTeam0 && scoreTeam1 == lastScoreboardTeam1)
        {
            return;
        }

        lastScoreboardSecond = remainingWhole;
        lastScoreboardTeam0 = scoreTeam0;
        lastScoreboardTeam1 = scoreTeam1;
        var minutes = remainingWhole / 60;
        var seconds = remainingWhole % 60;
        scoreboardText.text = $"BLUE {scoreTeam0}  —  {scoreTeam1} ORANGE   {minutes:00}:{seconds:00}";
    }

    private void ResolveArtPipeline()
    {
        artSelector = Object.FindFirstObjectByType<ArtModeSelector>() ?? Object.FindAnyObjectByType<ArtModeSelector>();
        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
    }

    private void EnsureBall()
    {
        if (ballTransform != null)
        {
            Destroy(ballTransform.gameObject);
        }

        var ballObject = new GameObject("Ball");
        ballObject.transform.SetParent(sceneGraph.EntitiesRoot, false);
        ballObject.transform.localScale = Vector3.one * 0.68f;

        var fill = ballObject.AddComponent<SpriteRenderer>();
        fill.sprite = PrimitiveSpriteLibrary.CircleFill();
        fill.color = new Color(0.97f, 0.97f, 0.94f, 1f);
        RenderOrder.Apply(fill, RenderOrder.EntityBody + 1);

        var outline = new GameObject("Outline").AddComponent<SpriteRenderer>();
        outline.transform.SetParent(ballObject.transform, false);
        outline.sprite = PrimitiveSpriteLibrary.CircleOutline();
        outline.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
        RenderOrder.Apply(outline, RenderOrder.EntityBody + 2);

        ballTransform = ballObject.transform;
    }

    private void TintAthlete(Transform iconRoot, int teamId, bool isGoalkeeper)
    {
        var teamColor = teamId == 0 ? Team0Color : Team1Color;
        var fillColor = isGoalkeeper ? teamColor * 0.78f : teamColor;
        var renderers = iconRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.Contains("Outline"))
            {
                renderers[i].color = new Color(0.08f, 0.08f, 0.1f, 1f);
            }
            else
            {
                renderers[i].color = fillColor;
            }
        }

        if (isGoalkeeper)
        {
            var band = new GameObject("KeeperBand").AddComponent<SpriteRenderer>();
            band.transform.SetParent(iconRoot, false);
            band.sprite = PrimitiveSpriteLibrary.CapsuleFill();
            band.transform.localScale = new Vector3(0.42f, 0.08f, 1f);
            band.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            band.color = Color.white;
            RenderOrder.Apply(band, RenderOrder.EntityBody + 6);
        }
    }

    private void ResolveBallBumperCollision()
    {
        for (var i = 0; i < bumpers.Length; i++)
        {
            var delta = ballPos - bumpers[i].position;
            var minDist = bumpers[i].radius + BallRadius;
            var dist = delta.magnitude;
            if (dist >= minDist || dist < 0.0001f)
            {
                continue;
            }

            var normal = delta / dist;
            ballPos = bumpers[i].position + (normal * minDist);
            ballVel = Vector2.Reflect(ballVel, normal) * BumperBallRestitution;
        }
    }

    private void ResolveAthleteBumperCollision(int athleteIndex)
    {
        for (var i = 0; i < bumpers.Length; i++)
        {
            var delta = positions[athleteIndex] - bumpers[i].position;
            var minDist = bumpers[i].radius + AthleteRadius;
            var dist = delta.magnitude;
            if (dist >= minDist || dist < 0.0001f)
            {
                continue;
            }

            var normal = delta / dist;
            positions[athleteIndex] = bumpers[i].position + (normal * minDist);
            velocities[athleteIndex] = Vector2.Reflect(velocities[athleteIndex], normal) * BumperAthleteRestitution;
        }
    }

    private float GetPadSpeedMultiplierAtPosition(Vector2 worldPos)
    {
        for (var i = 0; i < speedPads.Length; i++)
        {
            if (speedPads[i].area.Contains(worldPos))
            {
                return speedPads[i].speedMultiplier;
            }
        }

        return 1f;
    }

    private int FindClosestPlayerToPoint(int teamId, Vector2 point, bool includeGoalkeeper)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId != teamId || stunTimers[i] > 0f || (!includeGoalkeeper && IsGoalkeeper(i)))
            {
                continue;
            }

            var dist = (positions[i] - point).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private float FindNearestOpponentDistance(int athleteIndex)
    {
        var nearest = float.MaxValue;
        var teamId = identities[athleteIndex].teamId;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId == teamId)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, Vector2.Distance(positions[athleteIndex], positions[i]));
        }

        return nearest;
    }

    private int FindBestOpenTeammate(int carrierIndex)
    {
        var teamId = identities[carrierIndex].teamId;
        var attack = teamId == 0 ? 1f : -1f;
        var carrierPos = positions[carrierIndex];
        var best = -1;
        var bestScore = float.MinValue;

        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId)
            {
                continue;
            }

            if (!IsGoalkeeper(carrierIndex) && IsGoalkeeper(i))
            {
                continue;
            }

            var passDist = Vector2.Distance(carrierPos, positions[i]);
            if (passDist < 2f)
            {
                continue;
            }

            var nearestOpponent = float.MaxValue;
            for (var j = 0; j < TotalAthletes; j++)
            {
                if (identities[j].teamId == teamId)
                {
                    continue;
                }

                nearestOpponent = Mathf.Min(nearestOpponent, Vector2.Distance(positions[i], positions[j]));
            }

            if (nearestOpponent < OpenThreshold)
            {
                continue;
            }

            var progress = (positions[i].x - carrierPos.x) * attack;
            var score = (progress * 1.1f) + (nearestOpponent * 0.8f) - (passDist * 0.25f);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private float ResolveLaneForTeamIndex(int teamIndex)
    {
        if (teamIndex == GoalkeeperIndexPerTeam)
        {
            return 0f;
        }

        return teamIndex switch
        {
            0 => -1f,
            1 => 0f,
            2 => 1f,
            3 => -0.66f,
            4 => 0.66f,
            5 => -0.33f,
            _ => 0.33f
        };
    }

    private bool IsGoalkeeper(int athleteIndex) => (athleteIndex % PlayersPerTeam) == GoalkeeperIndexPerTeam;
    private int GetGoalkeeperIndex(int teamId) => (teamId * PlayersPerTeam) + GoalkeeperIndexPerTeam;

    private Rect GetKeeperBox(int teamId)
    {
        var depth = Mathf.Max(5.2f, rules.goalDepth + 3.6f);
        var height = GetGoalHeight() * 0.55f;
        return teamId == 0
            ? new Rect(-halfWidth, -height * 0.5f, depth, height)
            : new Rect(halfWidth - depth, -height * 0.5f, depth, height);
    }

    private float GetGoalHeight() => Mathf.Max(rules.goalHeight, halfHeight * 1.3f);

    private Vector2 GetOwnGoalCenter(int teamId) => teamId == 0 ? new Vector2(-halfWidth, 0f) : new Vector2(halfWidth, 0f);
    private Vector2 GetOpponentGoalCenter(int teamId) => teamId == 0 ? new Vector2(halfWidth, 0f) : new Vector2(-halfWidth, 0f);

    private bool IsInLeftGoal(Vector2 point) => point.x >= -halfWidth && point.x <= -halfWidth + rules.goalDepth && Mathf.Abs(point.y) <= GetGoalHeight() * 0.5f;
    private bool IsInRightGoal(Vector2 point) => point.x <= halfWidth && point.x >= halfWidth - rules.goalDepth && Mathf.Abs(point.y) <= GetGoalHeight() * 0.5f;

    private void ClampAthlete(int athleteIndex)
    {
        var p = positions[athleteIndex];
        if (p.x < -halfWidth || p.x > halfWidth)
        {
            p.x = Mathf.Clamp(p.x, -halfWidth, halfWidth);
            velocities[athleteIndex].x *= -0.35f;
        }

        if (p.y < -halfHeight || p.y > halfHeight)
        {
            p.y = Mathf.Clamp(p.y, -halfHeight, halfHeight);
            velocities[athleteIndex].y *= -0.35f;
        }

        positions[athleteIndex] = p;
    }

    private void ClampBall()
    {
        if (ballPos.x < -halfWidth || ballPos.x > halfWidth)
        {
            ballPos.x = Mathf.Clamp(ballPos.x, -halfWidth, halfWidth);
            ballVel.x *= -rules.ballBounce;
        }

        if (ballPos.y < -halfHeight || ballPos.y > halfHeight)
        {
            ballPos.y = Mathf.Clamp(ballPos.y, -halfHeight, halfHeight);
            ballVel.y *= -rules.ballBounce;
        }
    }

    private static void FaceVelocity(Transform target, Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        target.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FindHudText()
    {
        var hudObject = GameObject.Find("HUDText");
        hudText = hudObject != null ? hudObject.GetComponent<Text>() : null;
    }

    private void FindScoreboardText()
    {
        var scoreboardObject = GameObject.Find("ScoreboardText");
        scoreboardText = scoreboardObject != null ? scoreboardObject.GetComponent<Text>() : null;
    }

    private void EnsureMainCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = Mathf.Max(halfHeight + 2f, 10f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }
}
