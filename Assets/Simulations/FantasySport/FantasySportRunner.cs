using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private int playersPerTeam = 8;
    private int teamCount = 2;
    private int TotalAthletes => playersPerTeam * teamCount;
    private int GoalkeeperIndexPerTeam => playersPerTeam - 1;

    private const float TeamSeparationRadius = 2.8f;
    private const float BoundaryAvoidanceDistance = 3f;
    private const float PressureRadius = 2.2f;
    private const float OpenThreshold = 2.6f;
    private const float PassLeadTime = 0.3f;
    private const float BaseHomePullWeight = 0.42f;
    private const float ShootSpeed = 23f;
    private const float ShootRange = 16f;
    private const float PassCooldownSeconds = 0.65f;
    private const float ReceiverLockSeconds = 0.45f;
    private const float StealDistance = 0.7f;
    private const float AthleteRadius = 0.75f;
    private const float BallRadius = 0.35f;
    private const float BumperRadius = 1.05f;
    private const float BumperMinDistance = 4.5f;
    private const float BumperBallRestitution = 0.9f;
    private const float BumperAthleteRestitution = 0.1f;
    private const float BumperCollisionEpsilon = 0.0001f;
    private const float BumperNudge = 0.001f;
    private const float AthleteMinSlideSpeed = 0.65f;
    private const float AthleteUnstickThresholdSeconds = 0.25f;
    private const float AthleteUnstickBoost = 2.4f;
    private const float BallMinSlideSpeed = 0.15f;
    private const int BumperCount = 6;
    private const float StaminaDrainRun = 0.95f;
    private const float StaminaRecoverIdle = 0.8f;
    private const float StaminaRecoverShape = 0.48f;
    private const float TackleStaminaCost = 0.85f;
    private const float ThrowStaminaCost = 0.55f;
    private const float BoostPadExtraDrain = 0.75f;
    private const float StaminaBarWidth = 0.72f;
    private static readonly Vector2 PadSize = new Vector2(4.2f, 2.7f);
    private static readonly Color Team0Color = new Color(0.2f, 0.78f, 1f, 1f);
    private static readonly Color Team1Color = new Color(1f, 0.45f, 0.25f, 1f);

    private enum AthleteState { BallCarrier, ChaseFreeBall, SupportAttack, PressCarrier, MarkLane, GoalkeeperHome }
    private enum Role { Goalkeeper, Defender, Midfielder, Attacker }
    private enum Lane { Left, Center, Right }

    private struct PlayerProfile
    {
        public float speed;
        public float accel;
        public float staminaMax;
        public float throwPower;
        public float throwAccuracy;
        public float tacklePower;
        public float tackleCooldown;
        public float awareness;
        public float aggression;
    }

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
    private Lane[] laneByIndex;
    private Role[] roleByIndex;
    private PlayerProfile[] profiles;
    private float[] stamina;
    private float[] staminaMaxByIndex;
    private SpriteRenderer[] staminaBarFills;
    private IRng[] athleteRngs;
    private AthleteState[] athleteStates;
    private int[] assignedTargetIndex;
    private TextMesh[] debugStateLabels;
    private SpriteRenderer[] possessionRings;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private int[] lastBumperHitIndex;
    private float[] stuckTimeByAthlete;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;

    private SimulationSceneGraph sceneGraph;
    private Transform ballTransform;
    private Vector2 ballPos;
    private Vector2 previousBallPos;
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
    private bool kickoffSanityLogPending;
    private int simulationSeed;
    private bool scoreboardMissingLogged;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "FantasySport");
        EnsureMainCamera();
        ApplySimulationConfig(config);
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

        LogKickoffTeamSideSanity();

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

    private void ApplySimulationConfig(ScenarioConfig config)
    {
        teamCount = 2;
        playersPerTeam = Mathf.Max(2, config?.fantasySport?.playersPerTeam ?? 8);
        simulationSeed = config?.seed ?? 0;

        if (rules != null)
        {
            rules.matchSeconds = Mathf.Max(15f, config?.fantasySport?.periodLength ?? rules.matchSeconds);
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
        laneByIndex = new Lane[TotalAthletes];
        roleByIndex = new Role[TotalAthletes];
        profiles = new PlayerProfile[TotalAthletes];
        stamina = new float[TotalAthletes];
        staminaMaxByIndex = new float[TotalAthletes];
        staminaBarFills = new SpriteRenderer[TotalAthletes];
        athleteRngs = new IRng[TotalAthletes];
        athleteStates = new AthleteState[TotalAthletes];
        assignedTargetIndex = new int[TotalAthletes];
        debugStateLabels = new TextMesh[TotalAthletes];
        possessionRings = new SpriteRenderer[TotalAthletes];
        pipelineRenderers = new GameObject[TotalAthletes];
        visualKeys = new VisualKey[TotalAthletes];
        lastBumperHitIndex = new int[TotalAthletes];
        stuckTimeByAthlete = new float[TotalAthletes];

        ResolveArtPipeline();

        var spawnRng = RngService.Fork("SIM:FantasySport:SPAWN");
        for (var i = 0; i < TotalAthletes; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{simulationSeed}:{i}");
            var teamId = i < playersPerTeam ? 0 : 1;
            var teamLocalIndex = i % playersPerTeam;
            roleByIndex[i] = ResolveRoleForTeamIndex(teamLocalIndex);
            laneByIndex[i] = ResolveLaneForTeamIndex(teamLocalIndex);
            profiles[i] = GenerateProfile(teamId, teamLocalIndex);
            staminaMaxByIndex[i] = Mathf.Lerp(6f, 12f, profiles[i].staminaMax);
            stamina[i] = staminaMaxByIndex[i];
            var role = roleByIndex[i] == Role.Goalkeeper ? "goalkeeper" : (roleByIndex[i] == Role.Attacker ? "offense" : "defense");

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
            BuildStaminaBar(i, iconRoot.transform);

            var ring = new GameObject("PossessionRing").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(athlete.transform, false);
            ring.sprite = PrimitiveSpriteLibrary.CircleOutline();
            ring.color = GetTeamColor(teamId);
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
            lastBumperHitIndex[i] = -1;
            stuckTimeByAthlete[i] = 0f;
        }

        ResetAthleteFormationAndVelocities();
    }

    private void BuildHazards()
    {
        var rng = RngService.Fork("SIM:FantasySport:HAZARDS");
        var goalHeight = GetGoalHeight();
        speedPads = FantasySportHazards.GenerateSymmetricPads(halfWidth, halfHeight, PadSize, rules.goalDepth);
        var keepouts = FantasySportHazards.GetPadKeepouts(speedPads, 1.2f);
        bumpers = FantasySportHazards.GenerateBumpers(rng, BumperCount, halfWidth, halfHeight, BumperRadius, BumperMinDistance, rules.goalDepth, goalHeight, keepouts);

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
            fill.color = isSpeedPad ? new Color(0.62f, 0.30f, 0.92f, 0.28f) : new Color(0.44f, 0.22f, 0.72f, 0.28f);
            RenderOrder.Apply(fill, RenderOrder.WorldDeco);

            var outline = new GameObject("Outline").AddComponent<SpriteRenderer>();
            outline.transform.SetParent(go.transform, false);
            outline.sprite = PrimitiveSpriteLibrary.RoundedRectOutline();
            outline.color = isSpeedPad ? new Color(0.20f, 0.08f, 0.30f, 0.78f) : new Color(0.18f, 0.06f, 0.26f, 0.78f);
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
        previousBallPos = Vector2.zero;
        scoreboardMissingLogged = false;
    }

    private void ResetKickoff()
    {
        ResetAthleteFormationAndVelocities();
        kickoffSanityLogPending = true;
        ballPos = Vector2.zero;
        previousBallPos = Vector2.zero;
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
        for (var i = 0; i < TotalAthletes; i++)
        {
            positions[i] = ComputeHomePosition(i);
            velocities[i] = Vector2.zero;
            stunTimers[i] = 0f;
            tackleCooldowns[i] = 0f;
            passCooldowns[i] = 0f;
            stamina[i] = staminaMaxByIndex[i];
            lastBumperHitIndex[i] = -1;
            stuckTimeByAthlete[i] = 0f;
        }
    }

    private void LogKickoffTeamSideSanity()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!kickoffSanityLogPending || positions == null || identities == null)
        {
            return;
        }

        kickoffSanityLogPending = false;
        var team0SumX = 0f;
        var team1SumX = 0f;
        var team0Count = 0;
        var team1Count = 0;

        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId == 0)
            {
                team0SumX += positions[i].x;
                team0Count++;
            }
            else
            {
                team1SumX += positions[i].x;
                team1Count++;
            }
        }

        var team0AvgX = team0Count > 0 ? team0SumX / team0Count : 0f;
        var team1AvgX = team1Count > 0 ? team1SumX / team1Count : 0f;
        Debug.Log($"[FantasySport] Kickoff side sanity => team0 avgX={team0AvgX:F2}, team1 avgX={team1AvgX:F2}");
        Debug.Assert(team0AvgX < 0f, "[FantasySport] Team0 expected on LEFT at kickoff (avgX < 0).");
        Debug.Assert(team1AvgX > 0f, "[FantasySport] Team1 expected on RIGHT at kickoff (avgX > 0).");
#endif
    }

    private void RefreshAssignments()
    {
        previousPossessionTeam = ballOwnerTeam;
        var freePrimaryChaser0 = FindClosestPlayerToPoint(0, ballPos, includeGoalkeeper: false);
        var freePrimaryChaser1 = FindClosestPlayerToPoint(1, ballPos, includeGoalkeeper: false);
        var freeSecondaryChaser0 = FindSecondClosestPlayerToPoint(0, ballPos, freePrimaryChaser0);
        var freeSecondaryChaser1 = FindSecondClosestPlayerToPoint(1, ballPos, freePrimaryChaser1);

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
                var primary = teamId == 0 ? freePrimaryChaser0 : freePrimaryChaser1;
                var secondary = teamId == 0 ? freeSecondaryChaser0 : freeSecondaryChaser1;
                athleteStates[i] = (i == primary || i == secondary) ? AthleteState.ChaseFreeBall : AthleteState.SupportAttack;
                continue;
            }

            if (ballOwnerTeam == teamId)
            {
                athleteStates[i] = AthleteState.SupportAttack;
                continue;
            }

            var press = FindClosestPlayerToPoint(teamId, positions[ballOwnerIndex], includeGoalkeeper: false);
            var supportPress = FindSecondClosestPlayerToPoint(teamId, positions[ballOwnerIndex], press);
            athleteStates[i] = (i == press || i == supportPress) ? AthleteState.PressCarrier : AthleteState.MarkLane;
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
                var leadTime = Mathf.Lerp(0.18f, 0.38f, profiles[carrier].awareness);
                var target = positions[openTeammate] + (velocities[openTeammate] * leadTime);
                ThrowBall(carrier, target, openTeammate, false);
                passCooldowns[carrier] = PassCooldownSeconds;
                return;
            }
        }

        var toGoal = GetOpponentGoalCenter(teamId) - carrierPos;
        if (toGoal.magnitude <= ShootRange)
        {
            var yJitter = athleteRngs[carrier].Range(-GetGoalHeight() * 0.22f, GetGoalHeight() * 0.22f);
            var target = GetOpponentGoalCenter(teamId) + new Vector2(0f, yJitter);
            ThrowBall(carrier, target, -1, true);
        }
    }

    private void ThrowBall(int carrierIndex, Vector2 target, int receiverIndex, bool isShot)
    {
        var start = positions[carrierIndex];
        var dir = target - start;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = identities[carrierIndex].teamId == 0 ? Vector2.right : Vector2.left;
        }

        dir.Normalize();
        var stats = profiles[carrierIndex];
        var fatigue = GetFatigue01(carrierIndex);
        var throwSpeed = isShot ? ShootSpeed : Mathf.Lerp(9f, 15f, stats.throwPower);
        var errDeg = Mathf.Lerp(18f, 4f, stats.throwAccuracy) * Mathf.Lerp(1.35f, 0.9f, fatigue);
        var err = athleteRngs[carrierIndex].Range(-errDeg, errDeg);
        var finalDir = Rotate(dir, err);

        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        ballPos = start;
        ballVel = (finalDir * throwSpeed) + (velocities[carrierIndex] * 0.15f);

        lastThrowTeam = identities[carrierIndex].teamId;
        lastThrowTime = elapsedMatchTime;
        intendedReceiverIndex = receiverIndex;
        receiverLockUntilTime = elapsedMatchTime + ReceiverLockSeconds;
        stamina[carrierIndex] = Mathf.Max(0f, stamina[carrierIndex] - ThrowStaminaCost);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isShot && receiverIndex >= 0 && showMechanicDebug)
        {
            Debug.Log($"[FantasySport] PASS team={lastThrowTeam} from={carrierIndex} to={receiverIndex}");
        }
#endif
    }

    private void UpdateAthletes(float dt)
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            var desired = stunTimers[i] > 0f || matchFinished ? Vector2.zero : ComputeDesiredVelocity(i);
            var effectiveAccel = Mathf.Lerp(6f, 14f, profiles[i].accel) * Mathf.Lerp(0.65f, 1f, GetFatigue01(i));
            velocities[i] = Vector2.MoveTowards(velocities[i], desired, effectiveAccel * dt);
            positions[i] += velocities[i] * dt;
            ClampAthlete(i);
            ResolveAthleteBumperCollision(i, dt);
            UpdateStamina(i, desired, dt);
        }
    }

    private Vector2 ComputeDesiredVelocity(int i)
    {
        var keeper = IsGoalkeeper(i);
        var baseMaxSpeed = keeper ? Mathf.Lerp(4.5f, 7.5f, profiles[i].speed) * 0.9f : Mathf.Lerp(4.5f, 7.5f, profiles[i].speed);
        var fatigue = GetFatigue01(i);
        var maxSpeed = baseMaxSpeed * Mathf.Lerp(0.55f, 1f, fatigue);
        maxSpeed *= GetPadSpeedMultiplierAtPosition(positions[i]);

        var objective = ComputeHomePosition(i);
        switch (athleteStates[i])
        {
            case AthleteState.BallCarrier:
                if (keeper)
                {
                    var open = FindBestOpenTeammate(i);
                    objective = open >= 0 ? positions[open] : ComputeHomePosition(i);
                }
                else
                {
                    objective = GetOpponentGoalCenter(identities[i].teamId);
                }
                break;
            case AthleteState.ChaseFreeBall:
                objective = ballPos;
                break;
            case AthleteState.PressCarrier:
                objective = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
                break;
            case AthleteState.MarkLane:
                objective = Vector2.Lerp(GetOwnGoalCenter(identities[i].teamId), ballPos, 0.4f) + new Vector2(0f, LaneToFloat(laneByIndex[i]) * 3.4f);
                break;
            case AthleteState.GoalkeeperHome:
                objective = ComputeGoalkeeperTarget(i);
                break;
        }

        var home = ComputeHomePosition(i);
        return BuildSteeringVelocity(i, objective, home, maxSpeed);
    }

    private Vector2 ComputeHomePosition(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        if (IsGoalkeeper(athleteIndex))
        {
            return ComputeGoalkeeperTarget(athleteIndex);
        }

        var attack = teamId == 0 ? 1f : -1f;
        var laneY = LaneToFloat(laneByIndex[athleteIndex]) * halfHeight * 0.58f;
        float phaseOffset;
        switch (roleByIndex[athleteIndex])
        {
            case Role.Defender:
                phaseOffset = ballOwnerTeam == teamId ? 0.15f : 0.06f;
                break;
            case Role.Midfielder:
                phaseOffset = ballOwnerTeam == teamId ? 0.3f : 0.16f;
                break;
            default:
                phaseOffset = ballOwnerTeam == teamId ? 0.45f : 0.24f;
                break;
        }

        var baseX = attack * (halfWidth * phaseOffset);
        return new Vector2(baseX, Mathf.Clamp(laneY, -halfHeight + 2f, halfHeight - 2f));
    }

    private Vector2 ComputeGoalkeeperTarget(int i)
    {
        var keeperBox = GetKeeperBox(identities[i].teamId);
        var x = identities[i].teamId == 0 ? keeperBox.xMin + 1.5f : keeperBox.xMax - 1.5f;
        var y = Mathf.Clamp(ballPos.y, keeperBox.yMin + 0.5f, keeperBox.yMax - 0.5f);
        return new Vector2(x, y);
    }

    private Vector2 BuildSteeringVelocity(int athleteIndex, Vector2 objectiveTarget, Vector2 homeTarget, float maxSpeed)
    {
        var athletePos = positions[athleteIndex];
        var objective = (objectiveTarget - athletePos).normalized;
        var toHome = homeTarget - athletePos;
        var homePull = toHome.sqrMagnitude > 0.0001f ? toHome.normalized : Vector2.zero;

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

        var engagedWeight = BaseHomePullWeight * GetRoleShapeWeight(roleByIndex[athleteIndex]) * GetOpportunityTetherScale(athleteIndex);
        engagedWeight *= Mathf.Lerp(0.85f, 1.2f, 1f - profiles[athleteIndex].aggression);

        var roamDistance = toHome.magnitude;
        var roamRadius = GetRoleRoamRadius(roleByIndex[athleteIndex]);
        var roamOver = Mathf.Max(0f, roamDistance - roamRadius);
        var roamPullWeight = roamOver > 0f ? (roamOver * roamOver) * 0.08f : 0f;

        var steering = (objective * 1.45f) + (homePull * (engagedWeight + roamPullWeight)) + (separation * 1.95f) + (boundary * 1.2f);
        return steering.sqrMagnitude < 0.001f ? Vector2.zero : steering.normalized * maxSpeed;
    }

    private float GetRoleShapeWeight(Role role)
    {
        return role switch
        {
            Role.Goalkeeper => 1.5f,
            Role.Defender => 1.2f,
            Role.Midfielder => 0.9f,
            Role.Attacker => 0.7f,
            _ => 1f
        };
    }

    private float GetRoleRoamRadius(Role role)
    {
        return role switch
        {
            Role.Goalkeeper => 5.2f,
            Role.Defender => 10f,
            Role.Midfielder => 13f,
            Role.Attacker => 15f,
            _ => 12f
        };
    }

    private float GetOpportunityTetherScale(int athleteIndex)
    {
        if (IsGoalkeeper(athleteIndex))
        {
            return 1f;
        }

        var teamId = identities[athleteIndex].teamId;
        var tetherScale = 1f;
        var primaryChaser = FindClosestPlayerToPoint(teamId, ballPos, includeGoalkeeper: false);
        if (athleteIndex == primaryChaser)
        {
            tetherScale = Mathf.Min(tetherScale, 0.25f);
        }

        if (ballOwnerIndex >= 0 && identities[ballOwnerIndex].teamId != teamId)
        {
            var tackleRange = rules.tackleRadius * 1.35f;
            if (Vector2.Distance(positions[athleteIndex], positions[ballOwnerIndex]) <= tackleRange)
            {
                tetherScale = Mathf.Min(tetherScale, 0.35f);
            }
        }

        var interceptRange = 4.6f;
        var interceptDistance = DistanceToBallPath(positions[athleteIndex]);
        if (interceptDistance <= interceptRange)
        {
            tetherScale = Mathf.Min(tetherScale, 0.35f);
        }

        if (roleByIndex[athleteIndex] == Role.Attacker && IsAttackerOpenNearGoal(athleteIndex))
        {
            tetherScale = Mathf.Min(tetherScale, 0.4f);
        }

        return tetherScale;
    }

    private float DistanceToBallPath(Vector2 point)
    {
        var path = ballVel;
        if (path.sqrMagnitude < 0.001f)
        {
            return Vector2.Distance(point, ballPos);
        }

        var pathDir = path.normalized;
        var along = Mathf.Clamp(Vector2.Dot(point - ballPos, pathDir), 0f, 10f);
        var projection = ballPos + (pathDir * along);
        return Vector2.Distance(point, projection);
    }

    private bool IsAttackerOpenNearGoal(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        var toGoal = GetOpponentGoalCenter(teamId) - positions[athleteIndex];
        if (toGoal.magnitude > ShootRange * 1.1f)
        {
            return false;
        }

        return FindNearestOpponentDistance(athleteIndex) >= OpenThreshold;
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
            tackleCooldowns[i] = Mathf.Lerp(1.3f, 0.6f, profiles[i].tackleCooldown);
            ballOwnerIndex = -1;
            ballOwnerTeam = -1;
            ballPos = positions[victim];
            ballVel = (impulse * Mathf.Lerp(7f, 12f, profiles[i].tacklePower)) + (velocities[victim] * 0.2f);
            stamina[i] = Mathf.Max(0f, stamina[i] - TackleStaminaCost);
            intendedReceiverIndex = -1;
            receiverLockUntilTime = -999f;
            return;
        }
    }

    private void UpdateBall(float dt)
    {
        previousBallPos = ballPos;

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
        if (matchFinished)
        {
            return;
        }

        var goalLineLeftX = -halfWidth;
        var goalLineRightX = halfWidth;
        var goalMouthHalfH = GetGoalMouthHalfHeight();
        var crossedLeft = previousBallPos.x > goalLineLeftX && ballPos.x <= goalLineLeftX && Mathf.Abs(ballPos.y) <= goalMouthHalfH;
        var crossedRight = previousBallPos.x < goalLineRightX && ballPos.x >= goalLineRightX && Mathf.Abs(ballPos.y) <= goalMouthHalfH;

        if (crossedLeft)
        {
            scoreTeam1++;
            Debug.Log($"[FantasySport] GOAL team=1 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} crossing=left");
            ResetKickoff();
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
        else if (crossedRight)
        {
            scoreTeam0++;
            Debug.Log($"[FantasySport] GOAL team=0 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} crossing=right");
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
                    debugStateLabels[i].text = $"T{identities[i].teamId} {roleByIndex[i]} {athleteStates[i]} S:{stamina[i] / Mathf.Max(0.001f, staminaMaxByIndex[i]):0.00}{dbg}";
                }
            }

            possessionRings[i].enabled = ballOwnerIndex == i;
        }

        UpdateStaminaBars();

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
                if (!scoreboardMissingLogged)
                {
                    scoreboardMissingLogged = true;
                    Debug.LogWarning("[FantasySport] ScoreboardText not found. Run 'GSP/Dev/Recreate Broadcast UI (Minimap + HUD)' to create it.");
                }
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
        var teamColor = GetTeamColor(teamId);
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
            if (dist >= minDist)
            {
                continue;
            }

            var normal = dist > BumperCollisionEpsilon ? (delta / dist) : Vector2.right;
            var penetration = minDist - dist;
            ballPos += normal * (penetration + BumperNudge);

            var vn = Vector2.Dot(ballVel, normal) * normal;
            var vt = ballVel - vn;
            if (Vector2.Dot(ballVel, normal) < 0f)
            {
                ballVel = vt + ((-vn) * BumperBallRestitution);
            }

            if (vt.magnitude < BallMinSlideSpeed)
            {
                var tangent = new Vector2(-normal.y, normal.x);
                var sign = Vector2.Dot(vt, tangent) >= 0f ? 1f : -1f;
                ballVel += tangent * (BallMinSlideSpeed * sign);
            }
        }
    }

    private void ResolveAthleteBumperCollision(int athleteIndex, float dt)
    {
        var overlappingBumper = -1;
        for (var i = 0; i < bumpers.Length; i++)
        {
            var delta = positions[athleteIndex] - bumpers[i].position;
            var minDist = bumpers[i].radius + AthleteRadius;
            var dist = delta.magnitude;
            if (dist >= minDist)
            {
                continue;
            }

            overlappingBumper = i;
            var normal = dist > BumperCollisionEpsilon ? (delta / dist) : Vector2.right;
            var penetration = minDist - dist;
            positions[athleteIndex] += normal * (penetration + BumperNudge);

            var vel = velocities[athleteIndex];
            var vn = Vector2.Dot(vel, normal) * normal;
            var vt = vel - vn;
            if (Vector2.Dot(vel, normal) < 0f)
            {
                vel = vt + ((-vn) * BumperAthleteRestitution);
            }

            if (vt.magnitude < AthleteMinSlideSpeed)
            {
                var tangent = new Vector2(-normal.y, normal.x);
                var sign = GetDeterministicSlideSign(athleteIndex, tangent, vt);
                vel += tangent * (AthleteMinSlideSpeed * sign);
            }

            velocities[athleteIndex] = vel;
        }

        if (overlappingBumper >= 0)
        {
            if (lastBumperHitIndex[athleteIndex] == overlappingBumper)
            {
                stuckTimeByAthlete[athleteIndex] += dt;
            }
            else
            {
                lastBumperHitIndex[athleteIndex] = overlappingBumper;
                stuckTimeByAthlete[athleteIndex] = 0f;
            }

            if (stuckTimeByAthlete[athleteIndex] > AthleteUnstickThresholdSeconds)
            {
                var normal = (positions[athleteIndex] - bumpers[overlappingBumper].position).normalized;
                var tangent = new Vector2(-normal.y, normal.x);
                var sign = GetDeterministicSlideSign(athleteIndex, tangent, velocities[athleteIndex]);
                velocities[athleteIndex] += tangent * (AthleteUnstickBoost * sign);
                stuckTimeByAthlete[athleteIndex] = 0f;
            }
        }
        else
        {
            lastBumperHitIndex[athleteIndex] = -1;
            stuckTimeByAthlete[athleteIndex] = 0f;
        }
    }

    private float GetDeterministicSlideSign(int athleteIndex, Vector2 tangent, Vector2 reference)
    {
        var along = Vector2.Dot(reference, tangent);
        if (Mathf.Abs(along) > 0.001f)
        {
            return Mathf.Sign(along);
        }

        var teamId = identities[athleteIndex].teamId;
        return ((teamId + athleteIndex) & 1) == 0 ? 1f : -1f;
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

            var progress = (positions[i].x - carrierPos.x) * attack;
            var laneBonus = laneByIndex[i] == laneByIndex[carrierIndex] ? -0.8f : 0.55f;
            var awarenessBonus = profiles[carrierIndex].awareness * 0.8f;
            var score = (progress * 1.25f) + (nearestOpponent * 0.9f) + laneBonus + awarenessBonus - (passDist * 0.25f);
            if (nearestOpponent < OpenThreshold * 0.75f)
            {
                score -= 2f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private Role ResolveRoleForTeamIndex(int teamIndex)
    {
        if (teamIndex == GoalkeeperIndexPerTeam)
        {
            return Role.Goalkeeper;
        }

        if (teamIndex <= 1)
        {
            return Role.Defender;
        }

        if (teamIndex <= 3)
        {
            return Role.Midfielder;
        }

        return Role.Attacker;
    }

    private Lane ResolveLaneForTeamIndex(int teamIndex)
    {
        if (teamIndex == GoalkeeperIndexPerTeam)
        {
            return Lane.Center;
        }

        return teamIndex switch
        {
            0 => Lane.Left,
            1 => Lane.Right,
            2 => Lane.Left,
            3 => Lane.Right,
            4 => Lane.Left,
            5 => Lane.Right,
            _ => Lane.Center
        };
    }

    private static float LaneToFloat(Lane lane)
    {
        return lane switch
        {
            Lane.Left => -1f,
            Lane.Right => 1f,
            _ => 0f
        };
    }

    private int FindSecondClosestPlayerToPoint(int teamId, Vector2 point, int excludeIndex)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == excludeIndex || identities[i].teamId != teamId || stunTimers[i] > 0f || IsGoalkeeper(i))
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

    private PlayerProfile GenerateProfile(int teamId, int teamLocalIndex)
    {
        var rng = RngService.Fork($"FS:PLAYER:{teamId}:{teamLocalIndex}:{simulationSeed}");
        return new PlayerProfile
        {
            speed = rng.Range(0f, 1f),
            accel = rng.Range(0f, 1f),
            staminaMax = rng.Range(0f, 1f),
            throwPower = rng.Range(0f, 1f),
            throwAccuracy = rng.Range(0f, 1f),
            tacklePower = rng.Range(0f, 1f),
            tackleCooldown = rng.Range(0f, 1f),
            awareness = rng.Range(0f, 1f),
            aggression = rng.Range(0f, 1f)
        };
    }

    private void BuildStaminaBar(int athleteIndex, Transform iconRoot)
    {
        var bar = new GameObject("StaminaBar").transform;
        bar.SetParent(iconRoot, false);
        bar.localPosition = new Vector3(0f, 0.72f, 0f);

        var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(bar, false);
        bg.sprite = PrimitiveSpriteLibrary.RoundedRectFill();
        bg.transform.localScale = new Vector3(StaminaBarWidth, 0.13f, 1f);
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.62f);
        RenderOrder.Apply(bg, RenderOrder.EntityBody + 12);

        var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(bar, false);
        fill.sprite = PrimitiveSpriteLibrary.RoundedRectFill();
        fill.transform.localPosition = new Vector3(-StaminaBarWidth * 0.5f, 0f, 0f);
        fill.transform.localScale = new Vector3(StaminaBarWidth, 0.1f, 1f);
        fill.color = Color.green;
        RenderOrder.Apply(fill, RenderOrder.EntityBody + 13);
        staminaBarFills[athleteIndex] = fill;
    }

    private void UpdateStamina(int athleteIndex, Vector2 desiredVelocity, float dt)
    {
        var maxStamina = Mathf.Max(0.01f, staminaMaxByIndex[athleteIndex]);
        var speedFrac = desiredVelocity.magnitude / Mathf.Max(0.01f, Mathf.Lerp(4.5f, 7.5f, profiles[athleteIndex].speed));
        var drain = speedFrac > 0.7f ? StaminaDrainRun * dt : 0f;
        if (GetPadSpeedMultiplierAtPosition(positions[athleteIndex]) > 1f)
        {
            drain += BoostPadExtraDrain * dt;
        }

        var recovering = athleteStates[athleteIndex] == AthleteState.MarkLane || athleteStates[athleteIndex] == AthleteState.SupportAttack || desiredVelocity.magnitude < 1.15f;
        var recovery = recovering ? StaminaRecoverShape * dt : 0f;
        if (desiredVelocity.magnitude < 0.65f)
        {
            recovery += StaminaRecoverIdle * dt;
        }

        stamina[athleteIndex] = Mathf.Clamp(stamina[athleteIndex] - drain + recovery, 0f, maxStamina);
    }

    private float GetFatigue01(int athleteIndex)
    {
        var stamina01 = stamina[athleteIndex] / Mathf.Max(0.01f, staminaMaxByIndex[athleteIndex]);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, stamina01));
    }

    private void UpdateStaminaBars()
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (staminaBarFills[i] == null)
            {
                continue;
            }

            var ratio = Mathf.Clamp01(stamina[i] / Mathf.Max(0.01f, staminaMaxByIndex[i]));
            var scale = staminaBarFills[i].transform.localScale;
            scale.x = StaminaBarWidth * ratio;
            staminaBarFills[i].transform.localScale = scale;
            staminaBarFills[i].color = Color.Lerp(new Color(0.96f, 0.24f, 0.2f, 0.95f), new Color(0.2f, 0.92f, 0.28f, 0.95f), ratio);
        }
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);
        return new Vector2((vector.x * cos) - (vector.y * sin), (vector.x * sin) + (vector.y * cos));
    }

    private bool IsGoalkeeper(int athleteIndex) => (athleteIndex % playersPerTeam) == GoalkeeperIndexPerTeam;
    private int GetGoalkeeperIndex(int teamId) => (teamId * playersPerTeam) + GoalkeeperIndexPerTeam;
    private Color GetTeamColor(int teamId) => teamId == 0 ? Team0Color : Team1Color;
    private float GetOwnGoalX(int teamId) => teamId == 0 ? (-halfWidth + (rules.goalDepth * 0.5f)) : (halfWidth - (rules.goalDepth * 0.5f));
    private float TowardCenterSign(int teamId) => teamId == 0 ? 1f : -1f;

    private Rect GetKeeperBox(int teamId)
    {
        var depth = Mathf.Max(5.2f, rules.goalDepth + 3.6f);
        var height = GetGoalHeight() * 0.55f;
        return teamId == 0
            ? new Rect(-halfWidth, -height * 0.5f, depth, height)
            : new Rect(halfWidth - depth, -height * 0.5f, depth, height);
    }

    private float GetGoalMouthHalfHeight() => Mathf.Clamp(halfHeight * 0.45f, 6f, halfHeight - 3f);
    private float GetGoalHeight() => GetGoalMouthHalfHeight() * 2f;

    private Vector2 GetOwnGoalCenter(int teamId) => teamId == 0 ? new Vector2(-halfWidth, 0f) : new Vector2(halfWidth, 0f);
    private Vector2 GetOpponentGoalCenter(int teamId) => teamId == 0 ? new Vector2(halfWidth, 0f) : new Vector2(-halfWidth, 0f);

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
