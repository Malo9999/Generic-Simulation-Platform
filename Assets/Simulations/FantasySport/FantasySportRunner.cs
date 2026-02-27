using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private int playersPerTeam = 14;
    private int teamCount = 2;
    private int TotalAthletes => playersPerTeam * teamCount;
    private int GoalkeeperIndexPerTeam => 1;

    private const float TeamSeparationRadius = 2.8f;
    private const float BoundaryAvoidanceDistance = 3f;
    private const float VerticalWallSoftMargin = 4.5f;
    private const float CenterYBias = 0.22f;
    private const float PressureRadius = 2.2f;
    private const float OpenThreshold = 2.6f;
    private const float PassLeadTime = 0.3f;
    private const float BaseHomePullWeight = 0.42f;
    private const float ShootSpeed = 23f;
    private const float ShootRange = 16f;
    private const float PassCooldownSeconds = 0.65f;
    private const float ReceiverLockSeconds = 0.45f;
    private const float FreeBallMagnetStrength = 2f;
    private const float FreeBallFailSafeSeconds = 0.8f;
    private const float PickupRadiusFailSafeMultiplier = 1.3f;
    private const float StealRadiusMultiplier = 0.55f;
    private const float FacingTurnRate = 12f;
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
    private const int GoalCooldownTicks = 18;
    private const float StaminaDrainRun = 0.95f;
    private const float StaminaRecoverIdle = 0.8f;
    private const float StaminaRecoverShape = 0.48f;
    private const float TackleStaminaCost = 0.85f;
    private const float ThrowStaminaCost = 0.55f;
    private const float BoostPadExtraDrain = 0.75f;
    private const float StaminaBarWidth = 0.72f;
    private const float WingMaxYFrac = 0.42f;
    private const float LaneNarrowFrac = 0.16f;
    private const float LaneWideFrac = 0.34f;
    private const float DefenderWideBonus = 0.02f;
    private const float DefensiveAttackerTuckScale = 0.85f;
    private const float LineSlideYBallFollowScale = 0.18f;
    private const float LineSlideYMaxFrac = 0.08f;
    private const float ReturnToShapeBoostSeconds = 1.4f;
    private static readonly Vector2 PadSize = new Vector2(4.2f, 2.7f);
    private static readonly Color Team0Color = new Color(0.2f, 0.78f, 1f, 1f);
    private static readonly Color Team1Color = new Color(1f, 0.45f, 0.25f, 1f);

    private enum AthleteState { BallCarrier, ChaseFreeBall, SupportAttack, PressCarrier, MarkLane, GoalkeeperHome }
    private enum Role { Sweeper, Defender, Midfielder, Attacker }
    private enum Lane { Left, LeftCenter, Center, RightCenter, Right }
    private enum AttackSide { Left, Right }
    private enum TeamPhase { Attack, Defend, Transition }

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
    [SerializeField] private float shotClockSeconds = 6f;
    [SerializeField] private bool showDebugPlayerState;
    [SerializeField] private bool showMechanicDebug;
    [SerializeField] private bool showLinesDebug;

    private Transform[] athletes;
    private Transform[] athleteIconRoots;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] stunTimers;
    private float[] tackleCooldowns;
    private float[] passCooldowns;
    private int[] teamIdByIndex;
    private int[] teamLocalIndexByIndex;
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
    private Vector2[] facingDir;

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
    private float matchTimeSeconds;
    private float freeBallTime;
    private bool matchFinished;
    private int scoreTeam0;
    private int scoreTeam1;

    private int lastThrowTeam = -1;
    private float lastThrowTime = -999f;
    private int intendedReceiverIndex = -1;
    private float receiverLockUntilTime = -999f;
    private int lastPickupDebugSecond = -1;

    private Text hudText;
    private Text scoreboardText;
    private int lastHudSecond = -1;
    private int lastScoreboardSecond = -1;
    private int lastScoreboardTeam0 = int.MinValue;
    private int lastScoreboardTeam1 = int.MinValue;
    private int previousPossessionTeam = int.MinValue;
    private readonly float[] possessionTimeByTeam = new float[2];
    private readonly AttackSide[] currentAttackSide = { AttackSide.Left, AttackSide.Right };
    private readonly float[] attackPlanUntilTime = new float[2];
    private readonly float[] bestBallXProgressByTeam = new float[2];
    private readonly float[] stagnantTimeByTeam = new float[2];
    private readonly bool[] wingBiasActiveByTeam = new bool[2];
    private int lastPossessingTeam = -1;
    private readonly TeamPhase[] teamPhase = { TeamPhase.Transition, TeamPhase.Transition };
    private int nextEntityId;
    private bool kickoffSanityLogPending;
    private int simulationSeed;
    private bool scoreboardMissingLogged;
    private int goalCooldownUntilTick = -999;
    private int currentTickIndex;
    private readonly int[] primaryChaserByTeam = { -1, -1 };
    private readonly int[] secondaryChaserByTeam = { -1, -1 };
    private readonly float[] defLineXByTeam = new float[2];
    private readonly float[] midLineXByTeam = new float[2];
    private readonly float[] attLineXByTeam = new float[2];
    private float[] returnToShapeUntilByAthlete;
    private bool[] leftShapeByAthlete;
    private readonly SpriteRenderer[,] lineDebugRenderers = new SpriteRenderer[2, 3];
    private static Sprite cachedWhitePixelSprite;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "FantasySport");
        EnsureMainCamera();
        SetScoreboardVisible(true);
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

        currentTickIndex = tickIndex;
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

        matchTimeSeconds = elapsedMatchTime;
        freeBallTime = ballOwnerIndex == -1 ? (freeBallTime + dt) : 0f;

        for (var i = 0; i < athletes.Length; i++)
        {
            stunTimers[i] = Mathf.Max(0f, stunTimers[i] - dt);
            tackleCooldowns[i] = Mathf.Max(0f, tackleCooldowns[i] - dt);
            passCooldowns[i] = Mathf.Max(0f, passCooldowns[i] - dt);
        }

        RefreshAssignments();
        UpdateShotClock(dt);
        UpdatePossessionProgress(dt);
        ResolveBallCarrierDecision();
        UpdateAthletes(dt);
        ResolveTackleEvents();
        UpdateBall(dt);
        ResolvePickup(dt);
        ResolveGoal(tickIndex);
        UpdateFacingDirections(dt);
        ApplyTransforms(dt);
        LogPickupDebugOncePerSecond();
        UpdateHud(force: false);
        UpdateScoreboardUI(force: false);
    }

    public void Shutdown()
    {
        SetScoreboardVisible(false);
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
        playersPerTeam = 14;
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
        athleteIconRoots = new Transform[TotalAthletes];
        identities = new EntityIdentity[TotalAthletes];
        positions = new Vector2[TotalAthletes];
        velocities = new Vector2[TotalAthletes];
        stunTimers = new float[TotalAthletes];
        tackleCooldowns = new float[TotalAthletes];
        passCooldowns = new float[TotalAthletes];
        teamIdByIndex = new int[TotalAthletes];
        teamLocalIndexByIndex = new int[TotalAthletes];
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
        facingDir = new Vector2[TotalAthletes];
        returnToShapeUntilByAthlete = new float[TotalAthletes];
        leftShapeByAthlete = new bool[TotalAthletes];

        ResolveArtPipeline();

        var spawnRng = RngService.Fork("SIM:FantasySport:SPAWN");
        for (var i = 0; i < TotalAthletes; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{simulationSeed}:{i}");
            var teamId = i < playersPerTeam ? 0 : 1;
            var teamLocalIndex = i % playersPerTeam;
            teamIdByIndex[i] = teamId;
            teamLocalIndexByIndex[i] = teamLocalIndex;
            roleByIndex[i] = ResolveRoleForTeamIndex(teamLocalIndex);
            laneByIndex[i] = ResolveLaneForTeamIndex(teamLocalIndex);
            profiles[i] = GenerateProfile(teamId, teamLocalIndex);
            staminaMaxByIndex[i] = Mathf.Lerp(6f, 12f, profiles[i].staminaMax);
            stamina[i] = staminaMaxByIndex[i];
            var role = IsTrueKeeper(i) ? "goalkeeper" : (roleByIndex[i] == Role.Attacker ? "offense" : "defense");

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
            athleteIconRoots[i] = iconRoot.transform;
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
        var endzoneDepth = GetEndzoneDepth();
        var endzoneHalfHeight = GetEndzoneHalfHeight();
        speedPads = FantasySportHazards.GenerateSymmetricPads(halfWidth, halfHeight, PadSize, rules.goalDepth);
        var keepouts = FantasySportHazards.GetPadKeepouts(speedPads, 1.2f);
        bumpers = FantasySportHazards.GenerateBumpers(rng, BumperCount, halfWidth, halfHeight, BumperRadius, BumperMinDistance, endzoneDepth, endzoneHalfHeight, keepouts);

        var oldRoot = sceneGraph.WorldObjectsRoot.Find("FantasySportHazards");
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        var root = new GameObject("FantasySportHazards").transform;
        root.SetParent(sceneGraph.WorldObjectsRoot, false);

        BuildEndzoneVisual(root, 0, endzoneDepth, endzoneHalfHeight);
        BuildEndzoneVisual(root, 1, endzoneDepth, endzoneHalfHeight);
        CreateLineDebugVisuals(root);

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
        matchTimeSeconds = 0f;
        freeBallTime = 0f;
        lastPickupDebugSecond = -1;
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
        goalCooldownUntilTick = -999;
        currentTickIndex = 0;
        primaryChaserByTeam[0] = -1;
        primaryChaserByTeam[1] = -1;
        secondaryChaserByTeam[0] = -1;
        secondaryChaserByTeam[1] = -1;
        possessionTimeByTeam[0] = 0f;
        possessionTimeByTeam[1] = 0f;
        lastPossessingTeam = -1;
        teamPhase[0] = TeamPhase.Transition;
        teamPhase[1] = TeamPhase.Transition;
        currentAttackSide[0] = AttackSide.Left;
        currentAttackSide[1] = AttackSide.Right;
        attackPlanUntilTime[0] = 0f;
        attackPlanUntilTime[1] = 0f;
        bestBallXProgressByTeam[0] = 0f;
        bestBallXProgressByTeam[1] = 0f;
        stagnantTimeByTeam[0] = 0f;
        stagnantTimeByTeam[1] = 0f;
        wingBiasActiveByTeam[0] = false;
        wingBiasActiveByTeam[1] = false;
        if (returnToShapeUntilByAthlete != null && leftShapeByAthlete != null)
        {
            for (var i = 0; i < returnToShapeUntilByAthlete.Length; i++)
            {
                returnToShapeUntilByAthlete[i] = 0f;
                leftShapeByAthlete[i] = false;
            }
        }
    }

    private void ResetKickoff()
    {
        ResetAthleteFormationAndVelocities();
        kickoffSanityLogPending = true;
        ballPos = Vector2.zero;
        previousBallPos = Vector2.zero;
        ballVel = Vector2.zero;
        freeBallTime = 0f;
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
        lastThrowTeam = -1;
        lastThrowTime = -999f;
        matchTimeSeconds = elapsedMatchTime;
        lastPickupDebugSecond = -1;
        possessionTimeByTeam[0] = 0f;
        possessionTimeByTeam[1] = 0f;
        lastPossessingTeam = -1;
        teamPhase[0] = TeamPhase.Transition;
        teamPhase[1] = TeamPhase.Transition;
        currentAttackSide[0] = AttackSide.Left;
        currentAttackSide[1] = AttackSide.Right;
        attackPlanUntilTime[0] = 0f;
        attackPlanUntilTime[1] = 0f;
        bestBallXProgressByTeam[0] = 0f;
        bestBallXProgressByTeam[1] = 0f;
        stagnantTimeByTeam[0] = 0f;
        stagnantTimeByTeam[1] = 0f;
        wingBiasActiveByTeam[0] = false;
        wingBiasActiveByTeam[1] = false;
        if (returnToShapeUntilByAthlete != null && leftShapeByAthlete != null)
        {
            for (var i = 0; i < returnToShapeUntilByAthlete.Length; i++)
            {
                returnToShapeUntilByAthlete[i] = 0f;
                leftShapeByAthlete[i] = false;
            }
        }
    }

    private void ResetAthleteFormationAndVelocities()
    {
        UpdateTeamLineModel();
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
            facingDir[i] = identities[i].teamId == 0 ? Vector2.right : Vector2.left;
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
        if (team0AvgX >= 0f || team1AvgX <= 0f)
        {
            Debug.LogWarning($"[FantasySport] Kickoff side sanity failed. Team0 count={team0Count} avgX={team0AvgX:F2}, Team1 count={team1Count} avgX={team1AvgX:F2}");
        }
#endif
    }

    private void RefreshAssignments()
    {
        UpdateTeamPhases();
        UpdateTeamLineModel();
        previousPossessionTeam = ballOwnerTeam;
        AssignActiveChasers(0);
        AssignActiveChasers(1);

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
                var primary = primaryChaserByTeam[teamId];
                var secondary = secondaryChaserByTeam[teamId];
                athleteStates[i] = (i == primary || i == secondary) ? AthleteState.ChaseFreeBall : AthleteState.MarkLane;
                continue;
            }

            if (ballOwnerTeam == teamId)
            {
                athleteStates[i] = AthleteState.SupportAttack;
                continue;
            }

            var press = primaryChaserByTeam[teamId];
            var supportPress = secondaryChaserByTeam[teamId];
            athleteStates[i] = (i == press || i == supportPress) ? AthleteState.PressCarrier : AthleteState.MarkLane;
        }
    }

    private void AssignActiveChasers(int teamId)
    {
        var target = ballPos;
        if (ballOwnerIndex >= 0 && identities[ballOwnerIndex].teamId != teamId)
        {
            target = positions[ballOwnerIndex];
        }

        var primary = FindClosestPlayerToPoint(teamId, target, includeGoalkeeper: false);
        var secondary = FindSecondClosestPlayerToPoint(teamId, target, primary);

        if (primary == ballOwnerIndex)
        {
            primary = FindSecondClosestPlayerToPoint(teamId, target, primary);
        }

        if (secondary == ballOwnerIndex)
        {
            secondary = FindSecondClosestPlayerToPoint(teamId, target, primary);
        }

        primaryChaserByTeam[teamId] = primary;
        secondaryChaserByTeam[teamId] = secondary;
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
        var centralBlocked = IsCentralLaneBlocked(carrier);
        var stagnant = stagnantTimeByTeam[teamId];

        if (passCooldowns[carrier] <= 0f)
        {
            var forceWing = wingBiasActiveByTeam[teamId] || stagnant > 1.2f || centralBlocked;
            var forceSwitch = (forceWing && stagnant > 2.2f) || (centralBlocked && stagnant > 1.6f);
            if (pressured || forceWing)
            {
                var openTeammate = FindBestOpenTeammate(carrier, forceWing, forceSwitch);
                if (openTeammate >= 0)
                {
                    var leadTime = GetPassLeadTime(carrier);
                    var target = positions[openTeammate] + (velocities[openTeammate] * leadTime);
                    ThrowBall(carrier, target, openTeammate, false);
                    passCooldowns[carrier] = PassCooldownSeconds;
                    return;
                }
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

    private void UpdateTeamPhases()
    {
        if (ballOwnerTeam < 0)
        {
            teamPhase[0] = TeamPhase.Transition;
            teamPhase[1] = TeamPhase.Transition;
            return;
        }

        teamPhase[ballOwnerTeam] = TeamPhase.Attack;
        teamPhase[1 - ballOwnerTeam] = TeamPhase.Defend;
        if (attackPlanUntilTime[ballOwnerTeam] <= matchTimeSeconds)
        {
            PickAttackSidePlan(ballOwnerTeam, forceRefresh: false);
        }
    }

    private void UpdateTeamLineModel()
    {
        var endzoneDepth = GetEndzoneDepth();
        var margin = 1.2f;

        for (var teamId = 0; teamId < 2; teamId++)
        {
            var ownGoalX = teamId == 0 ? -halfWidth : halfWidth;
            var towardCenter = teamId == 0 ? 1f : -1f;
            var progress = teamId == 0
                ? (ballPos.x + halfWidth) / (2f * halfWidth)
                : (-ballPos.x + halfWidth) / (2f * halfWidth);
            var progress01 = Mathf.Clamp01(progress);

            var defBase = endzoneDepth + 7f;
            var midBase = endzoneDepth + 15f;
            var attBase = endzoneDepth + 23f;

            var push = 0f;
            if (ballOwnerTeam == teamId)
            {
                push = Mathf.Lerp(2f, 10f, progress01);
            }
            else if (ballOwnerTeam == (1 - teamId))
            {
                push = Mathf.Lerp(-2f, -8f, 1f - progress01);
            }

            var defLine = ownGoalX + (towardCenter * (defBase + (push * 0.70f)));
            var midLine = ownGoalX + (towardCenter * (midBase + (push * 0.95f)));
            var attLine = ownGoalX + (towardCenter * (attBase + (push * 1.10f)));

            if (towardCenter > 0f)
            {
                midLine = Mathf.Max(midLine, defLine + 3f);
                attLine = Mathf.Max(attLine, midLine + 3f);
            }
            else
            {
                midLine = Mathf.Min(midLine, defLine - 3f);
                attLine = Mathf.Min(attLine, midLine - 3f);
            }

            if (ballOwnerTeam == (1 - teamId))
            {
                var maxDef = ballPos.x - (towardCenter * 1.1f);
                defLine = towardCenter > 0f ? Mathf.Min(defLine, maxDef) : Mathf.Max(defLine, maxDef);
            }

            defLine = Mathf.Clamp(defLine, -halfWidth + margin, halfWidth - margin);
            midLine = Mathf.Clamp(midLine, -halfWidth + margin, halfWidth - margin);
            attLine = Mathf.Clamp(attLine, -halfWidth + margin, halfWidth - margin);

            defLineXByTeam[teamId] = defLine;
            midLineXByTeam[teamId] = midLine;
            attLineXByTeam[teamId] = attLine;
        }

        UpdateLineDebugRenderers();
    }

    private void UpdatePossessionProgress(float dt)
    {
        if (ballOwnerTeam < 0)
        {
            return;
        }

        var teamId = ballOwnerTeam;
        var progress = teamId == 0 ? ballPos.x : -ballPos.x;
        if (bestBallXProgressByTeam[teamId] == float.MinValue)
        {
            bestBallXProgressByTeam[teamId] = progress;
            stagnantTimeByTeam[teamId] = 0f;
            return;
        }

        if (progress > bestBallXProgressByTeam[teamId] + 0.5f)
        {
            bestBallXProgressByTeam[teamId] = progress;
            stagnantTimeByTeam[teamId] = 0f;
            wingBiasActiveByTeam[teamId] = false;
            return;
        }

        stagnantTimeByTeam[teamId] += dt;
        if (stagnantTimeByTeam[teamId] > 1.2f)
        {
            wingBiasActiveByTeam[teamId] = true;
        }

        if (stagnantTimeByTeam[teamId] > 2.2f)
        {
            currentAttackSide[teamId] = currentAttackSide[teamId] == AttackSide.Left ? AttackSide.Right : AttackSide.Left;
            attackPlanUntilTime[teamId] = matchTimeSeconds + 2.4f;
            stagnantTimeByTeam[teamId] = 0f;
        }
    }

    private void PickAttackSidePlan(int teamId, bool forceRefresh)
    {
        if (!forceRefresh && attackPlanUntilTime[teamId] > matchTimeSeconds && !IsCentralLaneBlocked(ballOwnerIndex))
        {
            return;
        }

        var leftOpen = EvaluateWingOpenness(teamId, AttackSide.Left);
        var rightOpen = EvaluateWingOpenness(teamId, AttackSide.Right);
        var parityLeft = ((currentTickIndex + simulationSeed + teamId) & 1) == 0;
        currentAttackSide[teamId] = Mathf.Abs(leftOpen - rightOpen) > 0.4f
            ? (leftOpen >= rightOpen ? AttackSide.Left : AttackSide.Right)
            : (parityLeft ? AttackSide.Left : AttackSide.Right);
        attackPlanUntilTime[teamId] = matchTimeSeconds + 5f + ((((simulationSeed + teamId + currentTickIndex) & 1) == 0) ? 0f : 1f);
    }

    private float EvaluateWingOpenness(int teamId, AttackSide side)
    {
        var desiredSign = side == AttackSide.Left ? -1f : 1f;
        var bestOpen = 0f;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId != teamId || IsTrueKeeper(i))
            {
                continue;
            }

            if (Mathf.Sign(positions[i].y) != desiredSign)
            {
                continue;
            }

            bestOpen = Mathf.Max(bestOpen, FindNearestOpponentDistance(i));
        }

        return bestOpen;
    }

    private float GetAttackWingSign(int teamId) => currentAttackSide[teamId] == AttackSide.Left ? -1f : 1f;

    private bool IsCentralLaneBlocked(int carrier)
    {
        if (carrier < 0)
        {
            return false;
        }

        var teamId = identities[carrier].teamId;
        var towardGoal = (GetOpponentGoalCenter(teamId) - positions[carrier]).normalized;
        var probe = positions[carrier] + towardGoal * 4f;
        var blockers = 0;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId == teamId)
            {
                continue;
            }

            var pos = positions[i];
            if (Mathf.Abs(pos.y - probe.y) <= 3.2f && (pos.x - positions[carrier].x) * towardGoal.x > 0f && Vector2.Distance(pos, probe) < 6.5f)
            {
                blockers++;
            }
        }

        return blockers >= 2;
    }

    private void UpdateShotClock(float dt)
    {
        var possessingTeam = (ballOwnerIndex >= 0 && teamIdByIndex != null) ? teamIdByIndex[ballOwnerIndex] : -1;
        if (possessingTeam != lastPossessingTeam)
        {
            TriggerReturnToShapeBoost();
            possessionTimeByTeam[0] = 0f;
            possessionTimeByTeam[1] = 0f;
            bestBallXProgressByTeam[0] = float.MinValue;
            bestBallXProgressByTeam[1] = float.MinValue;
            stagnantTimeByTeam[0] = 0f;
            stagnantTimeByTeam[1] = 0f;
            wingBiasActiveByTeam[0] = false;
            wingBiasActiveByTeam[1] = false;
            lastPossessingTeam = possessingTeam;
            if (possessingTeam >= 0)
            {
                PickAttackSidePlan(possessingTeam, forceRefresh: true);
            }
        }

        if (possessingTeam < 0 || shotClockSeconds <= 0f)
        {
            return;
        }

        possessionTimeByTeam[possessingTeam] += dt;
        if (possessionTimeByTeam[possessingTeam] < Mathf.Max(6f, shotClockSeconds))
        {
            return;
        }

        ForceShotClockAction(ballOwnerIndex, possessingTeam);
        possessionTimeByTeam[0] = 0f;
        possessionTimeByTeam[1] = 0f;
    }

    private void ForceShotClockAction(int carrier, int teamId)
    {
        if (carrier < 0 || ballOwnerIndex != carrier)
        {
            return;
        }

        var toGoal = GetOpponentGoalCenter(teamId) - positions[carrier];
        if (toGoal.magnitude <= ShootRange)
        {
            ThrowBall(carrier, GetOpponentGoalCenter(teamId), -1, true);
            passCooldowns[carrier] = PassCooldownSeconds * 0.7f;
            Debug.Log($"[FantasySport] ShotClock forced SHOT by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        var openTeammate = FindBestOpenTeammate(carrier, preferWing: true, forceSwitch: stagnantTimeByTeam[teamId] > 2f);
        if (openTeammate >= 0)
        {
            var leadTime = GetPassLeadTime(carrier);
            var target = positions[openTeammate] + (velocities[openTeammate] * leadTime);
            ThrowBall(carrier, target, openTeammate, false);
            passCooldowns[carrier] = PassCooldownSeconds;
            Debug.Log($"[FantasySport] ShotClock forced PASS by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - positions[carrier]).normalized;
        ThrowBall(carrier, positions[carrier] + (towardGoal * 16f), -1, false);
        passCooldowns[carrier] = PassCooldownSeconds;
        Debug.Log($"[FantasySport] ShotClock forced DUMP by team {teamId} at t={elapsedMatchTime:F2}s");
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
        var stamina01 = GetStamina01(carrierIndex);
        var staminaMultiplier = Mathf.Lerp(0.85f, 1.25f, stamina01);
        var baseThrowSpeed = isShot ? ShootSpeed : Mathf.Lerp(10f, 12f, stats.throwPower);
        var throwSpeed = baseThrowSpeed * staminaMultiplier;
        var maxErr = isShot ? 14f : 18f;
        var minErr = isShot ? 2f : 4f;
        var errDeg = Mathf.Lerp(maxErr, minErr, stats.throwAccuracy) * Mathf.Lerp(1f, 1.8f, 1f - stamina01);
        var deterministicError01 = DeterministicSignedRange(currentTickIndex, carrierIndex, receiverIndex + 7);
        var finalDir = Rotate(dir, deterministicError01 * errDeg);

        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        freeBallTime = 0f;
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
            if (returnToShapeUntilByAthlete != null && i < returnToShapeUntilByAthlete.Length && returnToShapeUntilByAthlete[i] > 0f)
            {
                returnToShapeUntilByAthlete[i] = Mathf.Max(0f, returnToShapeUntilByAthlete[i] - dt);
            }

            var desired = stunTimers[i] > 0f || matchFinished ? Vector2.zero : ComputeDesiredVelocity(i);
            var fatigue = GetFatigue01(i);
            var effectiveAccel = Mathf.Lerp(6f, 14f, profiles[i].accel) * (0.55f + (0.45f * fatigue));
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
        var maxSpeed = baseMaxSpeed * (0.45f + (0.55f * fatigue));
        maxSpeed *= GetPadSpeedMultiplierAtPosition(positions[i]);

        var objective = ComputeHomePosition(i);
        switch (athleteStates[i])
        {
            case AthleteState.BallCarrier:
                if (keeper)
                {
                    var open = FindBestOpenTeammate(i, preferWing: true);
                    objective = open >= 0 ? positions[open] : ComputeHomePosition(i);
                }
                else
                {
                    var teamId = identities[i].teamId;
                    var attackWingY = GetAttackWingSign(teamId) * GetLaneWideY();
                    var centralBlocked = IsCentralLaneBlocked(i);
                    objective = centralBlocked
                        ? new Vector2(GetOpponentGoalCenter(teamId).x * 0.65f, attackWingY)
                        : GetOpponentGoalCenter(teamId);
                }
                break;
            case AthleteState.ChaseFreeBall:
                objective = ballPos;
                break;
            case AthleteState.PressCarrier:
                objective = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
                break;
            case AthleteState.MarkLane:
                objective = Vector2.Lerp(GetOwnGoalCenter(identities[i].teamId), ballPos, 0.4f) + new Vector2(0f, LaneToFloat(laneByIndex[i]) * 0.15f);
                break;
            case AthleteState.GoalkeeperHome:
                objective = ComputeGoalkeeperTarget(i);
                break;
        }

        if (IsActiveChaser(i))
        {
            objective = GetActiveChaseTarget(i);
        }

        if (ShouldApplyPatrolJitter(i))
        {
            objective = ComputeHomePosition(i) + ComputePatrolJitter(i);
        }

        var home = ComputeHomePosition(i);
        var desired = BuildSteeringVelocity(i, objective, home, maxSpeed);
        if (desired.magnitude < 0.25f && stunTimers[i] <= 0f)
        {
            var towardTarget = objective - positions[i];
            if (towardTarget.sqrMagnitude < 0.0001f)
            {
                towardTarget = home - positions[i];
            }

            if (towardTarget.sqrMagnitude > 0.0001f)
            {
                desired = towardTarget.normalized * 1f;
            }
        }

        return desired;
    }

    private Vector2 ComputeHomePosition(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        if (IsTrueKeeper(athleteIndex))
        {
            return ComputeGoalkeeperTarget(athleteIndex);
        }

        var laneBonus = roleByIndex[athleteIndex] == Role.Defender ? halfHeight * DefenderWideBonus : 0f;
        var laneY = GetLaneY(laneByIndex[athleteIndex], halfHeight, laneBonus);
        var lineSlide = Mathf.Clamp(ballPos.y * LineSlideYBallFollowScale, -(halfHeight * LineSlideYMaxFrac), halfHeight * LineSlideYMaxFrac);
        laneY += lineSlide;

        if (roleByIndex[athleteIndex] == Role.Attacker && teamPhase[teamId] == TeamPhase.Defend)
        {
            laneY *= DefensiveAttackerTuckScale;
        }

        if (roleByIndex[athleteIndex] == Role.Attacker && laneByIndex[athleteIndex] != Lane.Center)
        {
            var wingSign = laneByIndex[athleteIndex] == Lane.Left ? -1f : 1f;
            var preferredWingSign = GetAttackWingSign(teamId);
            var wingTether = wingSign == preferredWingSign ? 1f : 0.65f;
            laneY = wingSign * Mathf.Lerp(Mathf.Abs(laneY), Mathf.Abs(laneY) + 0.8f, wingTether);
        }

        var homeX = roleByIndex[athleteIndex] switch
        {
            Role.Sweeper => defLineXByTeam[teamId] + (TowardCenterSign(teamId) * -2.8f),
            Role.Defender => defLineXByTeam[teamId],
            Role.Midfielder => midLineXByTeam[teamId],
            _ => attLineXByTeam[teamId]
        };

        homeX = Mathf.Clamp(homeX, -halfWidth + 1.1f, halfWidth - 1.1f);
        laneY = Mathf.Clamp(laneY, -halfHeight + 2f, halfHeight - 2f);
        return new Vector2(homeX, laneY);
    }

    private Vector2 ComputeGoalkeeperTarget(int i)
    {
        var keeperBox = GetKeeperBox(identities[i].teamId);
        var isBallInsideKeeperBox = keeperBox.Contains(ballPos);
        if (isBallInsideKeeperBox)
        {
            return new Vector2(
                Mathf.Clamp(ballPos.x, keeperBox.xMin + 0.5f, keeperBox.xMax - 0.5f),
                Mathf.Clamp(ballPos.y, keeperBox.yMin + 0.4f, keeperBox.yMax - 0.4f));
        }

        var depth = keeperBox.width;
        var homeX = identities[i].teamId == 0
            ? (-halfWidth + (depth * 0.55f))
            : (halfWidth - (depth * 0.55f));
        var homeY = Mathf.Clamp(ballPos.y, keeperBox.yMin + 0.4f, keeperBox.yMax - 0.4f);
        return new Vector2(homeX, homeY);
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
        var yLimit = halfHeight - VerticalWallSoftMargin;
        if (athletePos.y < -yLimit)
        {
            boundary.y += Mathf.Clamp01((-yLimit - athletePos.y) / Mathf.Max(0.01f, VerticalWallSoftMargin));
        }
        else if (athletePos.y > yLimit)
        {
            boundary.y -= Mathf.Clamp01((athletePos.y - yLimit) / Mathf.Max(0.01f, VerticalWallSoftMargin));
        }

        var teamId = identities[athleteIndex].teamId;
        var isCarrier = athleteIndex == ballOwnerIndex;
        var isPrimaryChaser = athleteIndex == primaryChaserByTeam[teamId];
        var isSecondaryChaser = athleteIndex == secondaryChaserByTeam[teamId];
        var isImmediateTackler = IsImmediateTackler(athleteIndex);
        var isImmediateInterceptor = IsImmediateInterceptor(athleteIndex);
        var isEngagedRole = isCarrier || isPrimaryChaser || isSecondaryChaser || isImmediateTackler || isImmediateInterceptor;

        var roamDistance = toHome.magnitude;
        var roamRadius = GetRoleRoamRadius(roleByIndex[athleteIndex]);
        if (isEngagedRole && roamDistance > roamRadius)
        {
            leftShapeByAthlete[athleteIndex] = true;
        }

        var centerBiasScale = (!isEngagedRole && !IsGoalkeeper(athleteIndex)) ? 1.25f : 1f;
        var centerBias = IsGoalkeeper(athleteIndex) ? Vector2.zero : (Vector2.up * (-athletePos.y) * CenterYBias * centerBiasScale);

        var homeWeight = BaseHomePullWeight * GetRoleShapeWeight(roleByIndex[athleteIndex]) * GetOpportunityTetherScale(athleteIndex);
        if (!isEngagedRole && !IsGoalkeeper(athleteIndex))
        {
            homeWeight *= 2.8f;
        }
        else
        {
            homeWeight *= 0.25f;
            if (HasOpportunityBreak(athleteIndex))
            {
                homeWeight *= 0.7f;
            }
        }

        if (returnToShapeUntilByAthlete != null && athleteIndex < returnToShapeUntilByAthlete.Length && returnToShapeUntilByAthlete[athleteIndex] > 0f)
        {
            homeWeight *= 1.75f;
        }

        homeWeight *= Mathf.Lerp(0.85f, 1.2f, 1f - profiles[athleteIndex].aggression);

        var roamOver = Mathf.Max(0f, roamDistance - roamRadius);
        var roamPullWeight = roamOver > 0f ? (roamOver * roamOver) * (isEngagedRole ? 0.08f : 0.14f) : 0f;

        var separationWeight = (!isEngagedRole && !IsGoalkeeper(athleteIndex)) ? 2.35f : 1.95f;
        var steering = (objective * 1.45f) + (homePull * (homeWeight + roamPullWeight)) + (separation * separationWeight) + (boundary * 1.45f) + centerBias;
        return steering.sqrMagnitude < 0.001f ? Vector2.zero : steering.normalized * maxSpeed;
    }

    private bool HasOpportunityBreak(int athleteIndex)
    {
        if (IsGoalkeeper(athleteIndex) || athleteIndex == ballOwnerIndex)
        {
            return false;
        }

        var ballDistance = Vector2.Distance(positions[athleteIndex], ballPos);
        if (ballDistance <= 7f)
        {
            return true;
        }

        if (DistanceToBallPath(positions[athleteIndex]) <= 3.2f)
        {
            return true;
        }

        return roleByIndex[athleteIndex] == Role.Attacker && IsAttackerOpenNearGoal(athleteIndex);
    }

    private float GetRoleShapeWeight(Role role)
    {
        return role switch
        {
            Role.Sweeper => 1.5f,
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
            Role.Sweeper => 6.5f,
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

        if (athleteIndex == primaryChaserByTeam[teamId] || athleteIndex == secondaryChaserByTeam[teamId])
        {
            tetherScale = Mathf.Min(tetherScale, 0.22f);
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
            freeBallTime = 0f;
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

    private void ResolvePickup(float dt)
    {
        if (ballOwnerIndex >= 0 || matchFinished)
        {
            freeBallTime = 0f;
            return;
        }

        var pickupRadius = rules.pickupRadius;
        if (freeBallTime > FreeBallFailSafeSeconds)
        {
            pickupRadius *= PickupRadiusFailSafeMultiplier;
            var nearest = FindNearestPlayerToBall();
            if (nearest >= 0)
            {
                var toNearest = positions[nearest] - ballPos;
                if (toNearest.sqrMagnitude > 0.0001f)
                {
                    ballPos += toNearest.normalized * (FreeBallMagnetStrength * dt);
                }
            }
        }

        var caughtByKeeper = TryKeeperCatch();
        if (caughtByKeeper >= 0)
        {
            SetBallOwner(caughtByKeeper);
            return;
        }

        var receiverLockActive = matchTimeSeconds < receiverLockUntilTime && intendedReceiverIndex >= 0;
        if (!receiverLockActive)
        {
            intendedReceiverIndex = -1;
        }

        var stealRadius = pickupRadius * StealRadiusMultiplier;
        var closest = -1;
        var closestDist = pickupRadius;
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

            if (receiverLockActive && i != intendedReceiverIndex)
            {
                var isOpponent = identities[i].teamId != identities[intendedReceiverIndex].teamId;
                if (!isOpponent || dist > stealRadius)
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
        ballVel = Vector2.zero;
        freeBallTime = 0f;
        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
    }

    private void ResolveGoal(int tickIndex)
    {
        if (matchFinished)
        {
            return;
        }

        if (tickIndex < goalCooldownUntilTick)
        {
            return;
        }

        var leftEndzone = GetEndzoneRect(0);
        var rightEndzone = GetEndzoneRect(1);

        var enteredLeftEndzone = !leftEndzone.Contains(previousBallPos) && leftEndzone.Contains(ballPos);
        var enteredRightEndzone = !rightEndzone.Contains(previousBallPos) && rightEndzone.Contains(ballPos);

        if (enteredLeftEndzone)
        {
            scoreTeam1++;
            Debug.Log($"[FantasySport] GOAL team=1 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} mode=endzone-left");
            ResetKickoff();
            goalCooldownUntilTick = tickIndex + GoalCooldownTicks;
            previousBallPos = Vector2.zero;
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
        else if (enteredRightEndzone)
        {
            scoreTeam0++;
            Debug.Log($"[FantasySport] GOAL team=0 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} mode=endzone-right");
            ResetKickoff();
            goalCooldownUntilTick = tickIndex + GoalCooldownTicks;
            previousBallPos = Vector2.zero;
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
    }

    private void ApplyTransforms(float dt)
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            athletes[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            ApplyFacingToIcon(i);

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

    private void LogPickupDebugOncePerSecond()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!showMechanicDebug)
        {
            return;
        }

        var currentSecond = Mathf.FloorToInt(matchTimeSeconds);
        if (currentSecond == lastPickupDebugSecond)
        {
            return;
        }

        lastPickupDebugSecond = currentSecond;
        var lockRemaining = receiverLockUntilTime - matchTimeSeconds;
        Debug.Log($"[FantasySport] pickup-debug owner={ballOwnerIndex} freeBallTime={freeBallTime:F2} intended={intendedReceiverIndex} lockRemaining={lockRemaining:F2}");
#endif
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

    private void TriggerReturnToShapeBoost()
    {
        if (returnToShapeUntilByAthlete == null || leftShapeByAthlete == null)
        {
            return;
        }

        for (var i = 0; i < TotalAthletes; i++)
        {
            if (!leftShapeByAthlete[i] || IsGoalkeeper(i))
            {
                continue;
            }

            returnToShapeUntilByAthlete[i] = ReturnToShapeBoostSeconds;
            leftShapeByAthlete[i] = false;
        }
    }

    private int FindNearestPlayerToBall()
    {
        var best = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (stunTimers[i] > 0f)
            {
                continue;
            }

            var dist = (positions[i] - ballPos).sqrMagnitude;
            if (dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }

        return best;
    }

    private float GetStamina01(int athleteIndex)
    {
        return Mathf.Clamp01(stamina[athleteIndex] / Mathf.Max(0.001f, staminaMaxByIndex[athleteIndex]));
    }

    private float GetPassLeadTime(int athleteIndex)
    {
        var stamina01 = GetStamina01(athleteIndex);
        var baseLeadTime = Mathf.Lerp(0.18f, 0.38f, profiles[athleteIndex].awareness);
        return baseLeadTime * Mathf.Lerp(0.9f, 1.25f, stamina01);
    }

    private static float DeterministicSignedRange(int tick, int athleteIndex, int salt)
    {
        unchecked
        {
            var hash = (uint)(tick * 73856093) ^ (uint)(athleteIndex * 19349663) ^ (uint)(salt * 83492791);
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return ((hash & 0xFFFFu) / 32767.5f) - 1f;
        }
    }

    private int FindBestOpenTeammate(int carrierIndex, bool preferWing = false, bool forceSwitch = false)
    {
        var teamId = identities[carrierIndex].teamId;
        var attack = teamId == 0 ? 1f : -1f;
        var carrierPos = positions[carrierIndex];
        var best = -1;
        var bestScore = float.MinValue;
        var activeWingSign = GetAttackWingSign(teamId);
        var oppositeWingSign = -activeWingSign;
        var centralCongested = CountOpponentsNear(carrierPos, 4.8f, teamId) >= 3;

        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId)
            {
                continue;
            }

            if (!IsTrueKeeper(carrierIndex) && IsTrueKeeper(i))
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
            var stamina01 = GetStamina01(carrierIndex);
            var tiredDistancePenalty = passDist * (1.2f - stamina01) * 0.25f;
            var score = (progress * 1.25f) + (nearestOpponent * 0.9f) + laneBonus + awarenessBonus - (passDist * 0.25f) - tiredDistancePenalty;

            var wingSign = Mathf.Sign(positions[i].y);
            var width01 = Mathf.InverseLerp(GetLaneNarrowY() * 0.7f, GetLaneWideY(), Mathf.Abs(positions[i].y));
            var aheadBonus = progress > 0f ? 1f : -0.8f;
            if (preferWing || centralCongested)
            {
                score += (width01 * 2.35f) + aheadBonus;
            }

            if (preferWing)
            {
                var desiredSign = forceSwitch ? oppositeWingSign : activeWingSign;
                var wingMatch = Mathf.Abs(positions[i].y) > GetLaneNarrowY() * 0.8f && wingSign == desiredSign;
                score += wingMatch ? 3.4f : -2.2f;
            }

            if (centralCongested && forceSwitch)
            {
                score += wingSign == oppositeWingSign ? 2.6f : -1.1f;
            }

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

    private int CountOpponentsNear(Vector2 point, float radius, int teamId)
    {
        var count = 0;
        var radiusSq = radius * radius;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId == teamId)
            {
                continue;
            }

            if ((positions[i] - point).sqrMagnitude <= radiusSq)
            {
                count++;
            }
        }

        return count;
    }

    private Role ResolveRoleForTeamIndex(int teamIndex)
    {
        return teamIndex switch
        {
            0 => Role.Sweeper,
            1 => Role.Sweeper,
            2 => Role.Sweeper,
            3 => Role.Defender,
            4 => Role.Defender,
            5 => Role.Defender,
            6 => Role.Defender,
            7 => Role.Midfielder,
            8 => Role.Midfielder,
            9 => Role.Midfielder,
            10 => Role.Midfielder,
            _ => Role.Attacker
        };
    }

    private Lane ResolveLaneForTeamIndex(int teamIndex)
    {
        return teamIndex switch
        {
            0 => Lane.Left,
            1 => Lane.Center,
            2 => Lane.Right,
            3 => Lane.Left,
            4 => Lane.LeftCenter,
            5 => Lane.RightCenter,
            6 => Lane.Right,
            7 => Lane.Left,
            8 => Lane.LeftCenter,
            9 => Lane.RightCenter,
            10 => Lane.Right,
            11 => Lane.Left,
            12 => Lane.Center,
            13 => Lane.Right,
            _ => Lane.Center
        };
    }

    private float LaneToFloat(Lane lane)
    {
        return GetLaneY(lane, halfHeight);
    }

    private float GetLaneY(Lane lane, float arenaHalfHeight, float bonus = 0f)
    {
        var wide = (arenaHalfHeight * LaneWideFrac) + bonus;
        var narrow = arenaHalfHeight * LaneNarrowFrac;
        var wingMax = arenaHalfHeight * WingMaxYFrac;
        wide = Mathf.Min(wide, wingMax);

        return lane switch
        {
            Lane.Left => -wide,
            Lane.Right => wide,
            Lane.LeftCenter => -narrow,
            Lane.RightCenter => narrow,
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

    private bool IsGoalkeeper(int athleteIndex)
    {
        return IsTrueKeeper(athleteIndex);
    }

    private bool IsTrueKeeper(int athleteIndex)
    {
        if (teamLocalIndexByIndex != null && athleteIndex >= 0 && athleteIndex < teamLocalIndexByIndex.Length)
        {
            return teamLocalIndexByIndex[athleteIndex] == 1;
        }

        return (athleteIndex % playersPerTeam) == 1;
    }

    private bool IsSweeperBack(int athleteIndex)
    {
        if (athleteIndex < 0 || athleteIndex >= roleByIndex.Length)
        {
            return false;
        }

        return roleByIndex[athleteIndex] == Role.Sweeper && !IsTrueKeeper(athleteIndex);
    }

    private int GetGoalkeeperIndex(int teamId) => (teamId * playersPerTeam) + 1;
    private Color GetTeamColor(int teamId) => teamId == 0 ? Team0Color : Team1Color;
    private float GetOwnGoalX(int teamId) => teamId == 0 ? (-halfWidth + (rules.goalDepth * 0.5f)) : (halfWidth - (rules.goalDepth * 0.5f));
    private float TowardCenterSign(int teamId) => teamId == 0 ? 1f : -1f;

    private Rect GetKeeperBox(int teamId)
    {
        var depth = Mathf.Max(6f, GetEndzoneDepth() * 0.8f);
        var height = GetEndzoneHalfHeight() * 2f * 0.7f;
        return teamId == 0
            ? new Rect(-halfWidth, -height * 0.5f, depth, height)
            : new Rect(halfWidth - depth, -height * 0.5f, depth, height);
    }

    private float GetGoalMouthHalfHeight() => Mathf.Clamp(rules.goalHeight * 0.5f, 2.5f, halfHeight - 3f);
    private float GetGoalHeight() => GetGoalMouthHalfHeight() * 2f;
    private float GetEndzoneDepth() => Mathf.Clamp(rules.goalDepth * 1.5f, 5f, 10f);
    private float GetEndzoneHalfHeight() => Mathf.Clamp(GetGoalMouthHalfHeight() * 2f, 4f, halfHeight - 2f);

    private float GetLaneWideY() => Mathf.Min(halfHeight * LaneWideFrac, halfHeight * WingMaxYFrac);
    private float GetLaneNarrowY() => halfHeight * LaneNarrowFrac;

    private Rect GetEndzoneRect(int teamId)
    {
        var depth = GetEndzoneDepth();
        var halfY = GetEndzoneHalfHeight();
        return teamId == 0
            ? Rect.MinMaxRect(-halfWidth, -halfY, -halfWidth + depth, halfY)
            : Rect.MinMaxRect(halfWidth - depth, -halfY, halfWidth, halfY);
    }

    private Vector2 GetOwnGoalCenter(int teamId) => teamId == 0 ? new Vector2(-halfWidth, 0f) : new Vector2(halfWidth, 0f);
    private Vector2 GetOpponentGoalCenter(int teamId) => teamId == 0 ? new Vector2(halfWidth, 0f) : new Vector2(-halfWidth, 0f);

    private void ClampAthlete(int athleteIndex)
    {
        var p = positions[athleteIndex];
        if (IsTrueKeeper(athleteIndex))
        {
            var box = GetKeeperBox(identities[athleteIndex].teamId);
            p.x = Mathf.Clamp(p.x, box.xMin, box.xMax);
            p.y = Mathf.Clamp(p.y, box.yMin, box.yMax);
        }
        else if (IsSweeperBack(athleteIndex))
        {
            var teamId = identities[athleteIndex].teamId;
            var centerLimit = teamId == 0 ? 0f : 0f;
            var canCrossCenter = teamPhase[teamId] == TeamPhase.Attack && ((teamId == 0 && ballPos.x > 0f) || (teamId == 1 && ballPos.x < 0f));
            if (!canCrossCenter)
            {
                if (teamId == 0)
                {
                    p.x = Mathf.Min(p.x, centerLimit - 0.6f);
                }
                else
                {
                    p.x = Mathf.Max(p.x, centerLimit + 0.6f);
                }
            }
        }

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

    private void UpdateFacingDirections(float dt)
    {
        var blend = 1f - Mathf.Exp(-FacingTurnRate * dt);
        for (var i = 0; i < TotalAthletes; i++)
        {
            var desiredFacing = ComputeDesiredFacing(i);
            if (desiredFacing.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            var current = facingDir[i].sqrMagnitude > 0.0001f ? facingDir[i] : Vector2.right;
            facingDir[i] = Vector2.Lerp(current, desiredFacing, blend).normalized;
        }
    }

    private Vector2 ComputeDesiredFacing(int athleteIndex)
    {
        var toTarget = Vector2.zero;
        if (athleteIndex == ballOwnerIndex)
        {
            toTarget = GetCarrierFacingTarget(athleteIndex) - positions[athleteIndex];
        }
        else
        {
            toTarget = ballPos - positions[athleteIndex];
        }

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return facingDir[athleteIndex];
        }

        return toTarget.normalized;
    }

    private Vector2 GetCarrierFacingTarget(int athleteIndex)
    {
        if (athleteIndex < 0)
        {
            return ballPos;
        }

        var pressured = FindNearestOpponentDistance(athleteIndex) <= PressureRadius;
        if (passCooldowns[athleteIndex] <= 0f && pressured)
        {
            var openTeammate = FindBestOpenTeammate(athleteIndex);
            if (openTeammate >= 0)
            {
                var leadTime = GetPassLeadTime(athleteIndex);
                return positions[openTeammate] + (velocities[openTeammate] * leadTime);
            }
        }

        return GetOpponentGoalCenter(identities[athleteIndex].teamId);
    }

    private void ApplyFacingToIcon(int athleteIndex)
    {
        var icon = athleteIconRoots != null && athleteIndex >= 0 && athleteIndex < athleteIconRoots.Length ? athleteIconRoots[athleteIndex] : athletes[athleteIndex];
        if (icon == null)
        {
            return;
        }

        var dir = facingDir != null && athleteIndex >= 0 && athleteIndex < facingDir.Length ? facingDir[athleteIndex] : Vector2.right;
        if (dir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        icon.localRotation = Quaternion.Euler(0f, 0f, angle);
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

    private void CreateLineDebugVisuals(Transform parent)
    {
        for (var teamId = 0; teamId < 2; teamId++)
        {
            for (var line = 0; line < 3; line++)
            {
                lineDebugRenderers[teamId, line] = CreateLineDebugBand(parent, teamId, line);
            }
        }
    }

    private SpriteRenderer CreateLineDebugBand(Transform parent, int teamId, int lineIndex)
    {
        var go = new GameObject($"LineDebug_{teamId}_{lineIndex}");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(0.45f, (halfHeight * 2f) - 1f, 1f);

        var fill = go.AddComponent<SpriteRenderer>();
        fill.sprite = PrimitiveSpriteLibrary.RoundedRectFill();
        var baseColor = teamId == 0 ? new Color(0.2f, 0.78f, 1f, 0.10f) : new Color(1f, 0.45f, 0.25f, 0.10f);
        if (lineIndex == 1)
        {
            baseColor.a += 0.03f;
        }
        else if (lineIndex == 2)
        {
            baseColor.a += 0.06f;
        }

        fill.color = baseColor;
        fill.enabled = showLinesDebug;
        RenderOrder.Apply(fill, RenderOrder.WorldDeco - 2);
        return fill;
    }

    private void UpdateLineDebugRenderers()
    {
        for (var teamId = 0; teamId < 2; teamId++)
        {
            var defRenderer = lineDebugRenderers[teamId, 0];
            var midRenderer = lineDebugRenderers[teamId, 1];
            var attRenderer = lineDebugRenderers[teamId, 2];
            if (defRenderer == null || midRenderer == null || attRenderer == null)
            {
                continue;
            }

            defRenderer.enabled = showLinesDebug;
            midRenderer.enabled = showLinesDebug;
            attRenderer.enabled = showLinesDebug;

            defRenderer.transform.localPosition = new Vector3(defLineXByTeam[teamId], 0f, 0f);
            midRenderer.transform.localPosition = new Vector3(midLineXByTeam[teamId], 0f, 0f);
            attRenderer.transform.localPosition = new Vector3(attLineXByTeam[teamId], 0f, 0f);
        }
    }

    private void BuildEndzoneVisual(Transform parent, int teamId, float depth, float halfY)
    {
        var rect = teamId == 0
            ? Rect.MinMaxRect(-halfWidth, -halfY, -halfWidth + depth, halfY)
            : Rect.MinMaxRect(halfWidth - depth, -halfY, halfWidth, halfY);

        var zone = new GameObject(teamId == 0 ? "Endzone_Left" : "Endzone_Right");
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = rect.center;
        zone.transform.localScale = new Vector3(rect.width, rect.height, 1f);

        var fill = zone.AddComponent<SpriteRenderer>();
        fill.sprite = GetWhitePixelSprite();
        fill.color = teamId == 0
            ? new Color(0.36f, 0.42f, 0.95f, 0.14f)
            : new Color(0.58f, 0.33f, 0.92f, 0.14f);
        RenderOrder.Apply(fill, RenderOrder.WorldDeco - 1);
    }

    private bool IsActiveChaser(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        return athleteIndex == primaryChaserByTeam[teamId] || athleteIndex == secondaryChaserByTeam[teamId];
    }

    private Vector2 GetActiveChaseTarget(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        if (ballOwnerIndex >= 0 && ballOwnerTeam != teamId)
        {
            return positions[ballOwnerIndex];
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - ballPos).normalized;
        var laneOffset = new Vector2(0f, LaneToFloat(laneByIndex[athleteIndex]) * 0.2f);
        return ballPos + (towardGoal * 1.8f) + laneOffset;
    }

    private bool ShouldApplyPatrolJitter(int athleteIndex)
    {
        if (IsGoalkeeper(athleteIndex) || athleteIndex == ballOwnerIndex || IsActiveChaser(athleteIndex))
        {
            return false;
        }

        if (athleteStates[athleteIndex] == AthleteState.PressCarrier || athleteStates[athleteIndex] == AthleteState.ChaseFreeBall)
        {
            return false;
        }

        return !IsImmediateTackler(athleteIndex) && !IsImmediateInterceptor(athleteIndex) && !IsGoalkeeperEngaged(athleteIndex);
    }

    private Vector2 ComputePatrolJitter(int athleteIndex)
    {
        var angle = (currentTickIndex * 0.02f) + (athleteIndex * 1.7f);
        var jitterRadius = Mathf.Lerp(0.6f, 1.2f, (athleteIndex % 5) / 4f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * jitterRadius;
    }

    private bool IsImmediateTackler(int athleteIndex)
    {
        if (ballOwnerIndex < 0 || identities[athleteIndex].teamId == ballOwnerTeam)
        {
            return false;
        }

        return Vector2.Distance(positions[athleteIndex], positions[ballOwnerIndex]) <= (rules.tackleRadius * 1.35f);
    }

    private bool IsImmediateInterceptor(int athleteIndex)
    {
        return DistanceToBallPath(positions[athleteIndex]) <= 4f;
    }

    private bool IsGoalkeeperEngaged(int athleteIndex)
    {
        if (!IsGoalkeeper(athleteIndex))
        {
            return false;
        }

        return GetKeeperBox(identities[athleteIndex].teamId).Contains(ballPos);
    }

    private static Sprite GetWhitePixelSprite()
    {
        if (cachedWhitePixelSprite != null)
        {
            return cachedWhitePixelSprite;
        }

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "FantasySport_WhitePixel"
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        cachedWhitePixelSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        cachedWhitePixelSprite.name = "FantasySport_WhitePixelSprite";
        return cachedWhitePixelSprite;
    }

    private static void SetScoreboardVisible(bool visible)
    {
        var scoreboardObject = GameObject.Find("ScoreboardText");
        if (scoreboardObject != null && scoreboardObject.activeSelf != visible)
        {
            scoreboardObject.SetActive(visible);
        }
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
