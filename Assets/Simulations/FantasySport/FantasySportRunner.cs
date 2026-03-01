using UnityEngine;
using UnityEngine.UI;
using TeamPhase = FantasySportTactics.TeamPhase;
using RoleGroup = FantasySportTactics.RoleGroup;
using Lane = FantasySportTactics.Lane;
using IntentType = FantasySportTactics.IntentType;
using PlayCall = FantasySportTactics.PlayCall;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int Teams = 2;
    private const int PlayersPerTeam = 14;
    private static readonly float[] FormationXFrac =
    {
        0.83f, // 0 Sweeper-L
        0.92f, // 1 Sweeper-C (keeper)
        0.83f, // 2 Sweeper-R
        0.71f, // 3 Defender-L
        0.71f, // 4 Defender-LC
        0.71f, // 5 Defender-RC
        0.71f, // 6 Defender-R
        0.52f, // 7 Mid-L
        0.52f, // 8 Mid-LC
        0.52f, // 9 Mid-RC
        0.52f, // 10 Mid-R
        0.33f, // 11 Att-L
        0.33f, // 12 Att-C
        0.33f  // 13 Att-R
    };
    private static readonly float[] FormationYFrac =
    {
        -0.44f, // 0 Sweeper-L
        0f,     // 1 Sweeper-C (keeper)
        0.44f,  // 2 Sweeper-R
        -0.44f, // 3 Defender-L
        -0.22f, // 4 Defender-LC
        0.22f,  // 5 Defender-RC
        0.44f,  // 6 Defender-R
        -0.44f, // 7 Mid-L
        -0.22f, // 8 Mid-LC
        0.22f,  // 9 Mid-RC
        0.44f,  // 10 Mid-R
        -0.44f, // 11 Att-L
        0f,     // 12 Att-C
        0.44f   // 13 Att-R
    };
    private int TotalPlayers => Teams * PlayersPerTeam;
    private int TotalAthletes => TotalPlayers;
    private int GoalkeeperIndexPerTeam => 1;

    private const float TeamSeparationRadius = 2.8f;
    private const float BoundaryAvoidanceDistance = 3f;
    private const float VerticalWallSoftMargin = 4.5f;
    private const float CenterYBias = 0.12f;
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
    private const int BumperCount = 8;
    private const int GoalCooldownTicks = 20;
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
    private const float ProgressThreshold = 0.04f;
    private const float ProgressWeight = 12f;
    private const float WingBonus = 2.5f;
    private const float WingOutletBonus = 3.2f;
    private const float KeeperLongShotMinSpeed = 10f;
    private const float KeeperLongShotMaxSpeed = 18f;
    private const float KeeperLongShotStaminaCost = 1.35f;
    private const float BandAnchorSpring = 3.2f;
    private const float DangerLaneBlockDistance = 2.2f;
    private const float DangerLaneIntentSeconds = 1.2f;
    private const float DangerLineDropDistance = 2f;
    private const float KickoffFreezeSeconds = 0.7f;
    private const float DefensiveMarkTtl = 1.6f;
    private const float FinishPlanDuration = 0.8f;
    private const float RunOpenRadius = 3f;
    private const float RunDepth = 8f;
    private const float PlayCallMinTtl = 4f;
    private const float PlayCallMaxTtl = 6f;
    private const float PlayFailFallbackSeconds = 2f;
    private const float ScrumBreakRadius = 2.5f;
    private const float ScrumBreakHoldSeconds = 0.6f;
    private const int ScrumBreakBodiesThreshold = 7;
    private static readonly Vector2 PadSize = new Vector2(4.2f, 2.7f);
    private static readonly Color Team0Color = new Color(0.2f, 0.78f, 1f, 1f);
    private static readonly Color Team1Color = new Color(1f, 0.45f, 0.25f, 1f);
    private const float ArenaBoundsMargin = 2f;
    private const float AthleteBoundsMargin = 0.8f;
    private const float AthleteEdgeRecoveryBand = 0.5f;
    private const float AthleteEdgePushStrength = 2.2f;
    private const float MaxHomeSnapPerTick = 7f;

    private enum AthleteState { BallCarrier, ChaseFreeBall, SupportAttack, PressCarrier, MarkLane, GoalkeeperHome }
    private enum AttackSide { Left, Right }

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

    private struct PlayPlan
    {
        public int teamId;
        public PlayCall call;
        public int shortIdx;
        public int wideIdx;
        public int forwardIdx;
        public int overlapIdx;
        public int cutbackIdx;
        public float untilTime;
        public int side;
    }

    [SerializeField] private FantasySportRules rules = new FantasySportRules();
    [SerializeField] private float athleteIconScaleMultiplier = 1.3f;
    [SerializeField] private float shotClockSeconds = 6f;
    [SerializeField] private bool showDebugPlayerState;
    [SerializeField] private bool showMechanicDebug;
    [SerializeField] private bool showLinesDebug;
    [SerializeField] private bool showTacticsDebug;
    [SerializeField] private bool showDebugGoalRects;

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
    private RoleGroup[] roleByIndex;
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
    private IntentType[] intentType;
    private float[] intentUntilTime;
    private Vector2[] intentTarget;
    private int[] finishPlanType;
    private Vector2[] finishPlanTarget;
    private float[] finishPlanUntil;
    private int[] markTarget;
    private float[] markUntil;
    private Vector2[] prevPosByAthlete;
    private float[] lastTeleportLogTimeByAthlete;
    private float[] lastYInfoLogTimeByAthlete;
    private bool[] safetyGuardLogByAthlete;

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
    private readonly AttackSide[] currentAttackSide = { AttackSide.Left, AttackSide.Right };
    private readonly float[] attackPlanUntilTime = new float[2];
    private readonly float[] attackPressure = { 0.5f, 0.5f };
    private int possessingTeam = -1;
    private int counterPressTeam = -1;
    private float possessionTime;
    private float possessionFlipTime = -999f;
    private float noProgressTime;
    private float bestProgress;
    private float lastProgress;
    private bool possessionCongested;
    private string possessionPlan = "None";
    private int threatMeter;
    private readonly TeamPhase[] teamPhase = { TeamPhase.Transition, TeamPhase.Transition };
    private readonly PlayPlan[] activePlayPlans = new PlayPlan[Teams];
    private readonly int[] triangleShortSupportByTeam = { -1, -1 };
    private readonly int[] triangleWideOutletByTeam = { -1, -1 };
    private readonly int[] triangleForwardRunByTeam = { -1, -1 };
    private int lastPasserIndex = -1;
    private int lastReceiverIndex = -1;
    private float lastPassTime = -999f;
    private int lastPassTeam = -1;
    private int lastTacticsDebugSecond = -1;
    private readonly int[] laneOccupancyScratch = new int[5];
    private readonly int[] topPassOptionIndices = { -1, -1, -1 };
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
    private bool arraysLogPending;
    private bool endzoneDebugLogged;
    private bool goalRectsDebugLogged;
    private readonly int[] idxWingL = { -1, -1 };
    private readonly int[] idxWingR = { -1, -1 };
    private readonly int[] kickoffDuelIndexByTeam = { -1, -1 };
    private readonly float[] playProgressBaseline = new float[Teams];
    private readonly float[] scrumHoldTimeByTeam = new float[Teams];
    private readonly bool[] scrumBreakerArmedByTeam = new bool[Teams];
    private float kickoffFreezeUntilTime = -1f;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "FantasySport");
        EnsureMainCamera();
        EnsureScoreboardText();
        SetScoreboardVisible(true);
        ApplySimulationConfig(config);
        BuildAthletes(config);
        EnsureBall();
        BuildHazards();
        EnsureArenaBoundsAndCameraFit();
        FindHudText();
        FindScoreboardText();
        ResetMatchState();
        ResetKickoff();
        LogGoalRectsForDebug();
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
        UpdatePossessionProgress(dt);
        ResolveScrumBreaker(dt);
        UpdateShotClock(dt);
        ResolveBallCarrierDecision();
        UpdateAthletes(dt);
        DetectAthleteTeleports();
        ResolveTackleEvents();
        UpdateBall(dt);
        ResolvePickup(dt);
        ResolveGoal(tickIndex);
        UpdateFacingDirections(dt);
        ApplyTransforms(dt);
        LogPickupDebugOncePerSecond();
        UpdateHud(force: false);
        UpdateScoreboardUI(force: false);
        UpdateTacticsDebug();
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
        PlayersPerTeam = 14;
        arraysLogPending = true;
        simulationSeed = config?.seed ?? 0;

        if (rules != null)
        {
            rules.matchSeconds = Mathf.Max(15f, config?.fantasySport?.periodLength ?? rules.matchSeconds);
        }
    }


    private void EnsureArrays(int total)
    {
        if (total <= 0)
        {
            return;
        }

        if (athletes == null || athletes.Length != total) athletes = new Transform[total];
        if (athleteIconRoots == null || athleteIconRoots.Length != total) athleteIconRoots = new Transform[total];
        if (identities == null || identities.Length != total) identities = new EntityIdentity[total];
        if (positions == null || positions.Length != total) positions = new Vector2[total];
        if (velocities == null || velocities.Length != total) velocities = new Vector2[total];
        if (stunTimers == null || stunTimers.Length != total) stunTimers = new float[total];
        if (tackleCooldowns == null || tackleCooldowns.Length != total) tackleCooldowns = new float[total];
        if (passCooldowns == null || passCooldowns.Length != total) passCooldowns = new float[total];
        if (teamIdByIndex == null || teamIdByIndex.Length != total) teamIdByIndex = new int[total];
        if (teamLocalIndexByIndex == null || teamLocalIndexByIndex.Length != total) teamLocalIndexByIndex = new int[total];
        if (laneByIndex == null || laneByIndex.Length != total) laneByIndex = new Lane[total];
        if (roleByIndex == null || roleByIndex.Length != total) roleByIndex = new RoleGroup[total];
        if (profiles == null || profiles.Length != total) profiles = new PlayerProfile[total];
        if (stamina == null || stamina.Length != total) stamina = new float[total];
        if (staminaMaxByIndex == null || staminaMaxByIndex.Length != total) staminaMaxByIndex = new float[total];
        if (staminaBarFills == null || staminaBarFills.Length != total) staminaBarFills = new SpriteRenderer[total];
        if (athleteRngs == null || athleteRngs.Length != total) athleteRngs = new IRng[total];
        if (athleteStates == null || athleteStates.Length != total) athleteStates = new AthleteState[total];
        if (assignedTargetIndex == null || assignedTargetIndex.Length != total) assignedTargetIndex = new int[total];
        if (debugStateLabels == null || debugStateLabels.Length != total) debugStateLabels = new TextMesh[total];
        if (possessionRings == null || possessionRings.Length != total) possessionRings = new SpriteRenderer[total];
        if (pipelineRenderers == null || pipelineRenderers.Length != total) pipelineRenderers = new GameObject[total];
        if (visualKeys == null || visualKeys.Length != total) visualKeys = new VisualKey[total];
        if (lastBumperHitIndex == null || lastBumperHitIndex.Length != total) lastBumperHitIndex = new int[total];
        if (stuckTimeByAthlete == null || stuckTimeByAthlete.Length != total) stuckTimeByAthlete = new float[total];
        if (facingDir == null || facingDir.Length != total) facingDir = new Vector2[total];
        if (intentType == null || intentType.Length != total) intentType = new IntentType[total];
        if (intentUntilTime == null || intentUntilTime.Length != total) intentUntilTime = new float[total];
        if (intentTarget == null || intentTarget.Length != total) intentTarget = new Vector2[total];
        if (finishPlanType == null || finishPlanType.Length != total) finishPlanType = new int[total];
        if (finishPlanTarget == null || finishPlanTarget.Length != total) finishPlanTarget = new Vector2[total];
        if (finishPlanUntil == null || finishPlanUntil.Length != total) finishPlanUntil = new float[total];
        if (markTarget == null || markTarget.Length != total) markTarget = new int[total];
        if (markUntil == null || markUntil.Length != total) markUntil = new float[total];
        if (prevPosByAthlete == null || prevPosByAthlete.Length != total) prevPosByAthlete = new Vector2[total];
        if (lastTeleportLogTimeByAthlete == null || lastTeleportLogTimeByAthlete.Length != total) lastTeleportLogTimeByAthlete = new float[total];
        if (lastYInfoLogTimeByAthlete == null || lastYInfoLogTimeByAthlete.Length != total) lastYInfoLogTimeByAthlete = new float[total];
        if (safetyGuardLogByAthlete == null || safetyGuardLogByAthlete.Length != total) safetyGuardLogByAthlete = new bool[total];
        if (returnToShapeUntilByAthlete == null || returnToShapeUntilByAthlete.Length != total) returnToShapeUntilByAthlete = new float[total];
        if (leftShapeByAthlete == null || leftShapeByAthlete.Length != total) leftShapeByAthlete = new bool[total];

        if (arraysLogPending)
        {
            Debug.Log($"[FantasySport] EnsureArrays TotalPlayers={TotalPlayers} positions.Length={positions.Length}");
            arraysLogPending = false;
        }
    }

    private void BuildAthletes(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;
        EnsureArrays(TotalPlayers);
        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64f) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64f) * 0.5f);
        Debug.Log($"[FS] Arena bounds init halfWidth={halfWidth:F2} halfHeight={halfHeight:F2} inset={AthleteBoundsMargin:F2} yMin={(-halfHeight + AthleteBoundsMargin):F2} yMax={(halfHeight - AthleteBoundsMargin):F2}");

        ResolveArtPipeline();

        var spawnRng = RngService.Fork("SIM:FantasySport:SPAWN");
        for (var i = 0; i < TotalAthletes; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{simulationSeed}:{i}");
            var teamId = i < PlayersPerTeam ? 0 : 1;
            var teamLocalIndex = i % PlayersPerTeam;
            teamIdByIndex[i] = teamId;
            teamLocalIndexByIndex[i] = teamLocalIndex;
            roleByIndex[i] = ResolveRoleForTeamIndex(teamLocalIndex);
            laneByIndex[i] = ResolveLaneForTeamIndex(teamLocalIndex);
            profiles[i] = GenerateProfile(teamId, teamLocalIndex);
            staminaMaxByIndex[i] = Mathf.Lerp(6f, 12f, profiles[i].staminaMax);
            stamina[i] = staminaMaxByIndex[i];
            var role = IsTrueKeeper(i) ? "goalkeeper" : (roleByIndex[i] == RoleGroup.Attacker ? "offense" : "defense");

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

        ResolveWingAnchors();
        ResetAthleteFormationAndVelocities();
    }


    private void ResolveWingAnchors()
    {
        for (var teamId = 0; teamId < Teams; teamId++)
        {
            idxWingL[teamId] = FindWingAnchor(teamId, left: true);
            idxWingR[teamId] = FindWingAnchor(teamId, left: false);
        }
    }

    private int FindWingAnchor(int teamId, bool left)
    {
        var desiredLane = left ? Lane.Left : Lane.Right;
        var best = -1;
        var bestScore = float.MinValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (IsGoalkeeper(i) || roleByIndex[i] != RoleGroup.Attacker)
            {
                continue;
            }

            var laneScore = laneByIndex[i] == desiredLane ? 4f : (laneByIndex[i] == Lane.Center ? 1.6f : 0f);
            var score = laneScore + Mathf.Abs(positions[i].y) * 0.08f + profiles[i].speed + profiles[i].throwPower;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private int PickKickoffDuelIndex(int teamId)
    {
        return GetAthleteIndexByLocal(teamId, 12);
    }

    private int TeamOf(int athleteIndex)
    {
        if (teamIdByIndex != null && athleteIndex >= 0 && athleteIndex < teamIdByIndex.Length)
        {
            return teamIdByIndex[athleteIndex];
        }

        return athleteIndex < PlayersPerTeam ? 0 : 1;
    }

    private int LocalIndexOf(int athleteIndex)
    {
        if (teamLocalIndexByIndex != null && athleteIndex >= 0 && athleteIndex < teamLocalIndexByIndex.Length)
        {
            return teamLocalIndexByIndex[athleteIndex];
        }

        return Mathf.Abs(athleteIndex) % Mathf.Max(1, PlayersPerTeam);
    }

    private int GetAthleteIndexByLocal(int teamId, int localIndex)
    {
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (LocalIndexOf(i) == localIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector2 GetKickoffSpot(int teamId, int localIndex)
    {
        localIndex = Mathf.Clamp(localIndex, 0, PlayersPerTeam - 1);
        var halfW = halfWidth;
        var halfH = halfHeight;
        var ownSideSign = teamId == 0 ? -1f : 1f;
        var towardCenter = teamId == 0 ? 1f : -1f;
        var ownBacklineX = ownSideSign * halfW;
        var endzoneDepth = GetEndzoneDepth();

        var xOffset = endzoneDepth + 20.0f;
        switch (localIndex)
        {
            case 1:
                xOffset = endzoneDepth * 0.55f;
                break;
            case 0:
            case 2:
                xOffset = endzoneDepth + 3.5f;
                break;
            case 3:
            case 4:
            case 5:
            case 6:
                xOffset = endzoneDepth + 7.5f;
                break;
            case 7:
            case 8:
            case 9:
            case 10:
                xOffset = endzoneDepth + 14.0f;
                break;
        }

        var x = ownBacklineX + (towardCenter * xOffset);
        var y = FormationYFrac[localIndex] * halfH;
        const float yInset = 1.8f;
        y = Mathf.Clamp(y, -halfH + yInset, halfH - yInset);
        return new Vector2(x, y);
    }

    private void BuildHazards()
    {
        var rng = RngService.Fork("SIM:FantasySport:HAZARDS");
        ComputeGoalRects(out var leftRect, out var rightRect);
        var endzoneDepth = leftRect.width;
        var endzoneHalfHeight = leftRect.height * 0.5f;
        if (!endzoneDebugLogged)
        {
            endzoneDebugLogged = true;
            Debug.Log($"[FantasySport] goalMouthHalfH={GetGoalMouthHalfHeight():F2} endzoneHalfH={endzoneHalfHeight:F2} depth={endzoneDepth:F2}");
        }
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

        BuildEndzoneVisual(root, 0, leftRect);
        BuildEndzoneVisual(root, 1, rightRect);
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
        possessingTeam = -1;
        counterPressTeam = -1;
        possessionFlipTime = -999f;
        possessionTime = 0f;
        noProgressTime = 0f;
        bestProgress = 0f;
        lastProgress = 0f;
        possessionCongested = false;
        possessionPlan = "None";
        threatMeter = 0;
        teamPhase[0] = TeamPhase.Transition;
        teamPhase[1] = TeamPhase.Transition;
        lastPasserIndex = -1;
        lastReceiverIndex = -1;
        lastPassTime = -999f;
        lastPassTeam = -1;
        lastTacticsDebugSecond = -1;
        currentAttackSide[0] = AttackSide.Left;
        currentAttackSide[1] = AttackSide.Right;
        attackPlanUntilTime[0] = 0f;
        attackPlanUntilTime[1] = 0f;
        activePlayPlans[0].untilTime = 0f;
        activePlayPlans[1].untilTime = 0f;
        triangleShortSupportByTeam[0] = -1;
        triangleShortSupportByTeam[1] = -1;
        triangleWideOutletByTeam[0] = -1;
        triangleWideOutletByTeam[1] = -1;
        triangleForwardRunByTeam[0] = -1;
        triangleForwardRunByTeam[1] = -1;
        attackPressure[0] = 0.5f;
        attackPressure[1] = 0.5f;
        playProgressBaseline[0] = 0f;
        playProgressBaseline[1] = 0f;
        scrumHoldTimeByTeam[0] = 0f;
        scrumHoldTimeByTeam[1] = 0f;
        scrumBreakerArmedByTeam[0] = false;
        scrumBreakerArmedByTeam[1] = false;
        if (returnToShapeUntilByAthlete != null && leftShapeByAthlete != null)
        {
            for (var i = 0; i < returnToShapeUntilByAthlete.Length; i++)
            {
                returnToShapeUntilByAthlete[i] = 0f;
                leftShapeByAthlete[i] = false;
            }
        }

        RefreshGoalVisuals();
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
        kickoffFreezeUntilTime = matchTimeSeconds + KickoffFreezeSeconds;
        lastPickupDebugSecond = -1;
        previousPossessionTeam = int.MinValue;
        possessingTeam = -1;
        counterPressTeam = -1;
        possessionFlipTime = -999f;
        possessionTime = 0f;
        noProgressTime = 0f;
        bestProgress = 0f;
        lastProgress = 0f;
        possessionCongested = false;
        possessionPlan = "None";
        threatMeter = 0;
        teamPhase[0] = TeamPhase.Transition;
        teamPhase[1] = TeamPhase.Transition;
        lastPasserIndex = -1;
        lastReceiverIndex = -1;
        lastPassTime = -999f;
        lastPassTeam = -1;
        lastTacticsDebugSecond = -1;
        currentAttackSide[0] = AttackSide.Left;
        currentAttackSide[1] = AttackSide.Right;
        attackPlanUntilTime[0] = 0f;
        attackPlanUntilTime[1] = 0f;
        activePlayPlans[0].untilTime = 0f;
        activePlayPlans[1].untilTime = 0f;
        triangleShortSupportByTeam[0] = -1;
        triangleShortSupportByTeam[1] = -1;
        triangleWideOutletByTeam[0] = -1;
        triangleWideOutletByTeam[1] = -1;
        triangleForwardRunByTeam[0] = -1;
        triangleForwardRunByTeam[1] = -1;
        attackPressure[0] = 0.5f;
        attackPressure[1] = 0.5f;
        playProgressBaseline[0] = 0f;
        playProgressBaseline[1] = 0f;
        scrumHoldTimeByTeam[0] = 0f;
        scrumHoldTimeByTeam[1] = 0f;
        scrumBreakerArmedByTeam[0] = false;
        scrumBreakerArmedByTeam[1] = false;
        if (returnToShapeUntilByAthlete != null && leftShapeByAthlete != null)
        {
            for (var i = 0; i < returnToShapeUntilByAthlete.Length; i++)
            {
                returnToShapeUntilByAthlete[i] = 0f;
                leftShapeByAthlete[i] = false;
            }
        }

        DrawKickoffSpotDebugOnce();
        RefreshGoalVisuals();
    }

    private void ResetAfterGoal()
    {
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        ballPos = Vector2.zero;
        previousBallPos = Vector2.zero;
        ballVel = Vector2.zero;
        freeBallTime = 0f;
        teamPhase[0] = TeamPhase.Transition;
        teamPhase[1] = TeamPhase.Transition;

        ResetAthleteFormationAndVelocities();
        kickoffSanityLogPending = true;

        intendedReceiverIndex = -1;
        receiverLockUntilTime = -999f;
        for (var i = 0; i < finishPlanType.Length; i++)
        {
            finishPlanType[i] = 0;
            finishPlanUntil[i] = -999f;
        }
        lastThrowTeam = -1;
        lastThrowTime = -999f;
        previousPossessionTeam = int.MinValue;
        possessingTeam = -1;
        counterPressTeam = -1;
        possessionFlipTime = -999f;
        possessionTime = 0f;
        noProgressTime = 0f;
        bestProgress = 0f;
        lastProgress = 0f;
        possessionCongested = false;
        possessionPlan = "None";
        threatMeter = 0;

        playProgressBaseline[0] = 0f;
        playProgressBaseline[1] = 0f;
        scrumHoldTimeByTeam[0] = 0f;
        scrumHoldTimeByTeam[1] = 0f;
        scrumBreakerArmedByTeam[0] = false;
        scrumBreakerArmedByTeam[1] = false;

        kickoffFreezeUntilTime = matchTimeSeconds + KickoffFreezeSeconds;
        DrawKickoffSpotDebugOnce();
        RefreshGoalVisuals();
    }

    private void ResetAthleteFormationAndVelocities()
    {
        EnsureArrays(TotalPlayers);
        UpdateTeamLineModel();
        kickoffDuelIndexByTeam[0] = PickKickoffDuelIndex(0);
        kickoffDuelIndexByTeam[1] = PickKickoffDuelIndex(1);

        for (var i = 0; i < TotalAthletes; i++)
        {
            var teamId = TeamOf(i);
            var localIndex = LocalIndexOf(i);
            var kickoffSpot = GetKickoffSpot(teamId, localIndex);
            if (i == kickoffDuelIndexByTeam[teamId])
            {
                kickoffSpot.x += teamId == 0 ? 1.2f : -1.2f;
            }

            positions[i] = kickoffSpot;
            velocities[i] = Vector2.zero;
            stunTimers[i] = 0f;
            tackleCooldowns[i] = 0f;
            passCooldowns[i] = 0f;
            intentType[i] = IntentType.None;
            intentUntilTime[i] = -999f;
            intentTarget[i] = kickoffSpot;
            finishPlanType[i] = 0;
            finishPlanTarget[i] = Vector2.zero;
            finishPlanUntil[i] = -999f;
            markTarget[i] = -1;
            markUntil[i] = -999f;
            stamina[i] = staminaMaxByIndex[i];
            lastBumperHitIndex[i] = -1;
            stuckTimeByAthlete[i] = 0f;
            facingDir[i] = teamId == 0 ? Vector2.right : Vector2.left;
            prevPosByAthlete[i] = positions[i];
            lastTeleportLogTimeByAthlete[i] = -999f;
            lastYInfoLogTimeByAthlete[i] = -999f;
            safetyGuardLogByAthlete[i] = false;
        }
    }

    private void DetectAthleteTeleports()
    {
        if (positions == null || prevPosByAthlete == null || lastTeleportLogTimeByAthlete == null)
        {
            return;
        }

        for (var i = 0; i < TotalAthletes; i++)
        {
            if (matchTimeSeconds < kickoffFreezeUntilTime + 0.05f)
            {
                prevPosByAthlete[i] = positions[i];
                continue;
            }

            var delta = positions[i] - prevPosByAthlete[i];
            if (delta.magnitude > 6f && matchTimeSeconds - lastTeleportLogTimeByAthlete[i] > 0.5f)
            {
                Debug.LogWarning($"[FS] TELEPORT i={i} team={identities[i].teamId} role={roleByIndex[i]} lane={laneByIndex[i]} delta={delta} pos={positions[i]} prev={prevPosByAthlete[i]} vel={velocities[i]} state={athleteStates[i]}");
                if (Mathf.Abs(positions[i].y - (-11.0f)) < 0.05f)
                {
                    Debug.LogWarning($"[FS] TELEPORT_TO_MINUS11 i={i} team={identities[i].teamId} role={roleByIndex[i]} lane={laneByIndex[i]} delta={delta} pos={positions[i]} prev={prevPosByAthlete[i]} vel={velocities[i]} state={athleteStates[i]}");
                }

                if (matchTimeSeconds - lastYInfoLogTimeByAthlete[i] > 1f)
                {
                    var yMin = -halfHeight + AthleteBoundsMargin;
                    var yMax = halfHeight - AthleteBoundsMargin;
                    var bandY = ShouldApplyWidthAnchor(identities[i].teamId, i) ? GetWidthAnchorTargetY(identities[i].teamId, i) : 0f;
                    var homeY = ComputeHomePosition(i).y;
                    Debug.LogWarning($"[FS] YINFO i={i} yMin={yMin:F2} yMax={yMax:F2} bandY={bandY:F2} homeY={homeY:F2} halfH={halfHeight:F2}");
                    lastYInfoLogTimeByAthlete[i] = matchTimeSeconds;
                }

                lastTeleportLogTimeByAthlete[i] = matchTimeSeconds;
            }

            prevPosByAthlete[i] = positions[i];
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
            Debug.LogWarning($"[KickoffSanity] halfWidth={halfWidth:F2} ownBacklineX(team0)={GetOwnBacklineX(0):F2} ownBacklineX(team1)={GetOwnBacklineX(1):F2}");
        }
#endif
    }

    private void DrawKickoffSpotDebugOnce()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var size = 0.35f;
        var duration = 1.0f;
        for (var teamId = 0; teamId < Teams; teamId++)
        {
            var color = teamId == 0 ? Team0Color : Team1Color;
            for (var localIndex = 0; localIndex < PlayersPerTeam; localIndex++)
            {
                var p = GetKickoffSpot(teamId, localIndex);
                if (localIndex == 12)
                {
                    p.x += teamId == 0 ? 1.2f : -1.2f;
                }

                Debug.DrawLine(p + new Vector2(-size, -size), p + new Vector2(size, size), color, duration);
                Debug.DrawLine(p + new Vector2(-size, size), p + new Vector2(size, -size), color, duration);
            }
        }
#endif
    }

    private void RefreshAssignments()
    {
        UpdateTeamPhases();
        UpdateTeamLineModel();
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

        ApplyDangerLaneDefense(0);
        ApplyDangerLaneDefense(1);

        if (ballOwnerTeam >= 0)
        {
            UpdateSupportTriangle(ballOwnerTeam, ballOwnerIndex);
            ApplyPlayPlanIntents(ballOwnerTeam);
            ApplySupportTriangleIntents(ballOwnerTeam);
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

        var inCounterPressWindow = counterPressTeam == teamId && (matchTimeSeconds - possessionFlipTime) <= 1.1f;
        var wingTrap = ballOwnerIndex >= 0 && ballOwnerTeam != teamId && threatMeter > 40 && Mathf.Abs(ballPos.y) > halfHeight * 0.24f;
        var allowSecondary = ballOwnerIndex < 0 || inCounterPressWindow || wingTrap;
        if (!allowSecondary)
        {
            secondary = -1;
        }

        if (inCounterPressWindow && IsValidAthleteIndex(secondary) && roleByIndex[secondary] == RoleGroup.Defender)
        {
            secondary = FindSecondClosestMidfielder(teamId, target, primary);
        }

        primaryChaserByTeam[teamId] = primary;
        secondaryChaserByTeam[teamId] = secondary;
    }


    private int FindSecondClosestMidfielder(int teamId, Vector2 point, int excludeIndex)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (i == excludeIndex || IsGoalkeeper(i) || roleByIndex[i] != RoleGroup.Midfielder)
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

    private void ApplyDangerLaneDefense(int defendingTeam)
    {
        var dangerCarrier = GetDangerCarrierIndex(defendingTeam);
        if (!IsValidAthleteIndex(dangerCarrier))
        {
            return;
        }

        var carrierPos = positions[dangerCarrier];
        var ownGoalX = GetOwnBacklineX(defendingTeam);
        var goalPoint = new Vector2(ownGoalX, Mathf.Clamp(carrierPos.y, -GetEndzoneHalfHeight(), GetEndzoneHalfHeight()));
        var segmentMidpoint = Vector2.Lerp(carrierPos, goalPoint, 0.5f);
        var laneOpen = IsDangerLaneOpen(defendingTeam, carrierPos, goalPoint);
        if (!laneOpen)
        {
            return;
        }

        defLineXByTeam[defendingTeam] = Mathf.Clamp(
            defLineXByTeam[defendingTeam] - (TowardCenterSign(defendingTeam) * DangerLineDropDistance),
            -halfWidth + 1.1f,
            halfWidth - 1.1f);

        var nearestDefender = FindClosestRoleToPoint(defendingTeam, RoleGroup.Defender, segmentMidpoint, -1);
        if (!IsValidAthleteIndex(nearestDefender))
        {
            nearestDefender = FindClosestLaneBlockerToSegment(defendingTeam, carrierPos, goalPoint, -1);
        }

        var secondBlocker = FindClosestRoleToSegment(defendingTeam, RoleGroup.Midfielder, carrierPos, goalPoint, nearestDefender);
        if (!IsValidAthleteIndex(secondBlocker))
        {
            secondBlocker = FindClosestLaneBlockerToSegment(defendingTeam, carrierPos, goalPoint, nearestDefender);
        }

        AssignLaneBlockIntent(nearestDefender, carrierPos, goalPoint, 0.42f, 0.9f);
        AssignLaneBlockIntent(secondBlocker, carrierPos, goalPoint, 0.62f, -0.9f);
    }

    private int GetDangerCarrierIndex(int defendingTeam)
    {
        var attackingTeam = 1 - defendingTeam;
        if (ballOwnerIndex >= 0)
        {
            return identities[ballOwnerIndex].teamId == attackingTeam ? ballOwnerIndex : -1;
        }

        var (start, end) = GetTeamSpan(attackingTeam);
        var best = -1;
        var bestDist = float.MaxValue;
        for (var i = start; i < end; i++)
        {
            if (IsGoalkeeper(i) || roleByIndex[i] != RoleGroup.Attacker)
            {
                continue;
            }

            var dist = (positions[i] - ballPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private bool IsDangerLaneOpen(int defendingTeam, Vector2 laneStart, Vector2 laneEnd)
    {
        var (start, end) = GetTeamSpan(defendingTeam);
        for (var i = start; i < end; i++)
        {
            if (IsGoalkeeper(i))
            {
                continue;
            }

            var role = roleByIndex[i];
            if (role != RoleGroup.Defender && role != RoleGroup.Sweeper && role != RoleGroup.Midfielder)
            {
                continue;
            }

            var dist = DistancePointToSegment(positions[i], laneStart, laneEnd);
            if (dist <= DangerLaneBlockDistance)
            {
                return false;
            }
        }

        return true;
    }

    private void AssignLaneBlockIntent(int athleteIndex, Vector2 laneStart, Vector2 laneEnd, float laneT, float perpendicularSign)
    {
        if (!IsValidAthleteIndex(athleteIndex) || IsGoalkeeper(athleteIndex))
        {
            return;
        }

        var laneDir = laneEnd - laneStart;
        if (laneDir.sqrMagnitude < 0.001f)
        {
            return;
        }

        var normalized = laneDir.normalized;
        var basePoint = Vector2.Lerp(laneStart, laneEnd, Mathf.Clamp01(laneT));
        var offsetDir = new Vector2(-normalized.y, normalized.x) * perpendicularSign;
        var blockPos = basePoint + (offsetDir.normalized * 0.65f);
        SetIntent(athleteIndex, IntentType.MarkLane, blockPos, DangerLaneIntentSeconds);
    }

    private int FindClosestRoleToPoint(int teamId, RoleGroup role, Vector2 point, int exclude)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (i == exclude || IsGoalkeeper(i) || roleByIndex[i] != role)
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

    private int FindClosestRoleToSegment(int teamId, RoleGroup role, Vector2 segmentStart, Vector2 segmentEnd, int exclude)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (i == exclude || IsGoalkeeper(i) || roleByIndex[i] != role)
            {
                continue;
            }

            var dist = DistancePointToSegment(positions[i], segmentStart, segmentEnd);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private int FindClosestLaneBlockerToSegment(int teamId, Vector2 segmentStart, Vector2 segmentEnd, int exclude)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (i == exclude || IsGoalkeeper(i))
            {
                continue;
            }

            var role = roleByIndex[i];
            if (role != RoleGroup.Defender && role != RoleGroup.Sweeper && role != RoleGroup.Midfielder)
            {
                continue;
            }

            var dist = DistancePointToSegment(positions[i], segmentStart, segmentEnd);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        var seg = segmentEnd - segmentStart;
        var segSq = seg.sqrMagnitude;
        if (segSq < 0.0001f)
        {
            return Vector2.Distance(point, segmentStart);
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, seg) / segSq);
        var closest = segmentStart + (seg * t);
        return Vector2.Distance(point, closest);
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
        var stagnant = noProgressTime;
        var shotClockHot = possessionTime >= Mathf.Max(6f, shotClockSeconds);
        var forceFinishAttempt = possessionTime > 6f;
        var progress01 = GetTeamProgress(teamId, carrierPos);
        var finishBias = GetFinishBias(teamId, progress01);
        var inScoringRange = progress01 > 0.72f;
        possessionPlan = "Normal";

        if (finishBias > 0f)
        {
            ApplyFinishModeOffBallRuns(teamId, carrier, carrierPos);
        }

        if (TryExecuteFinishPlan(carrier, teamId, carrierPos, progress01))
        {
            return;
        }

        if (passCooldowns[carrier] <= 0f)
        {
            if (IsGoalkeeper(carrier) && TryKeeperLongShot(carrier, teamId, carrierPos, pressured, shotClockHot))
            {
                passCooldowns[carrier] = PassCooldownSeconds;
                possessionPlan = "KeeperLong";
                return;
            }

            if (TryFinalThirdWingPlay(carrier, teamId, carrierPos, progress01))
            {
                passCooldowns[carrier] = PassCooldownSeconds;
                return;
            }

            if (finishBias > 0f && inScoringRange && (!pressured || shotClockHot))
            {
                PerformFinishShotDump(carrier, teamId);
                passCooldowns[carrier] = PassCooldownSeconds;
                possessionPlan = "FinishShot";
                return;
            }

            var forceWing = possessionCongested || stagnant > 1.2f || centralBlocked || finishBias > 0f;
            var forceSwitch = stagnant > 2.2f;
            if (forceFinishAttempt)
            {
                forceWing = true;
            }
            var progressClockHot = stagnant > 2.6f;
            if (progressClockHot)
            {
                forceWing = true;
                forceSwitch = true;
            }
            if (pressured || forceWing)
            {
                possessionPlan = forceSwitch ? "Switch" : "Wing";
                var openTeammate = FindBestOpenTeammate(carrier, forceWing || progressClockHot, forceSwitch);
                if (openTeammate >= 0)
                {
                    var leadTime = GetPassLeadTime(carrier);
                    var target = positions[openTeammate] + (velocities[openTeammate] * leadTime);
                    ThrowBall(carrier, target, openTeammate, false);
                    passCooldowns[carrier] = PassCooldownSeconds;
                    return;
                }

                if (forceSwitch || centralBlocked || stagnant > 1.2f)
                {
                    var dumpX = Mathf.Clamp(carrierPos.x + (TowardCenterSign(teamId) * halfWidth * 0.20f), -halfWidth + 1.2f, halfWidth - 1.2f);
                    var dumpTarget = new Vector2(dumpX, GetMoreOpenBandY(teamId, carrier));
                    ThrowBall(carrier, dumpTarget, -1, false);
                    passCooldowns[carrier] = PassCooldownSeconds;
                    return;
                }
            }

            if (TryThroughBallPass(carrier, teamId))
            {
                possessionPlan = "Through";
                passCooldowns[carrier] = PassCooldownSeconds;
                return;
            }

            if (shotClockHot)
            {
                possessionPlan = "ShotClock";
                ForceShotClockAction(carrier, teamId);
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


    private bool TryExecuteFinishPlan(int carrier, int teamId, Vector2 carrierPos, float progress01)
    {
        var endzone = GetEndzoneRect(1 - teamId);
        var inFinalThird = progress01 > 0.75f;
        var inCorridor = Mathf.Abs(carrierPos.y) <= endzone.height * 0.5f;
        var shouldCommitFinish = inFinalThird && (inCorridor || possessionTime > 5f);
        if (!shouldCommitFinish)
        {
            finishPlanType[carrier] = 0;
            finishPlanUntil[carrier] = -999f;
            return false;
        }

        if (finishPlanUntil[carrier] < matchTimeSeconds || finishPlanType[carrier] == 0)
        {
            var laneBlocked = !IsFinishLaneOpen(carrier, teamId, carrierPos);
            finishPlanType[carrier] = laneBlocked ? 2 : 1;
            if (finishPlanType[carrier] == 1)
            {
                var runY = Mathf.Clamp(carrierPos.y, endzone.yMin + 0.5f, endzone.yMax - 0.5f);
                finishPlanTarget[carrier] = new Vector2(endzone.center.x, runY);
            }
            else
            {
                finishPlanTarget[carrier] = endzone.center;
            }

            finishPlanUntil[carrier] = matchTimeSeconds + FinishPlanDuration;
        }

        if (finishPlanType[carrier] == 2)
        {
            ThrowBall(carrier, finishPlanTarget[carrier], -1, true);
            passCooldowns[carrier] = PassCooldownSeconds;
            finishPlanType[carrier] = 0;
            finishPlanUntil[carrier] = -999f;
            possessionPlan = "FinishThrow";
            return true;
        }

        possessionPlan = "FinishRun";
        if (endzone.Contains(carrierPos))
        {
            finishPlanType[carrier] = 0;
            finishPlanUntil[carrier] = -999f;
        }

        return false;
    }

    private bool IsFinishLaneOpen(int carrier, int teamId, Vector2 carrierPos)
    {
        var towardGoal = teamId == 0 ? 1f : -1f;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrier || identities[i].teamId == teamId)
            {
                continue;
            }

            var delta = positions[i] - carrierPos;
            var forward = delta.x * towardGoal;
            if (forward <= 0f || forward > 4.5f)
            {
                continue;
            }

            if (Mathf.Abs(delta.y) <= 2.2f)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryFinalThirdWingPlay(int carrierIndex, int teamId, Vector2 carrierPos, float progress01)
    {
        var isFinalThird = teamPhase[teamId] == TeamPhase.FinalThird;
        var wingThreshold = halfHeight * 0.28f;
        var isOnWing = Mathf.Abs(carrierPos.y) > wingThreshold;
        var nearEndzone = progress01 > 0.78f;
        var congested = possessionCongested || CountOpponentsNear(carrierPos, 4.6f, teamId) >= 3;
        if (!isFinalThird || !isOnWing || (!nearEndzone && !congested))
        {
            return false;
        }

        if (TryCrossPlay(carrierIndex, teamId, carrierPos))
        {
            possessionPlan = "Cross";
            return true;
        }

        if (TryCutbackPlay(carrierIndex, teamId, carrierPos))
        {
            possessionPlan = "Cutback";
            return true;
        }

        return false;
    }

    private float GetFinishBias(int teamId, float progress01)
    {
        var finishBias = teamPhase[teamId] == TeamPhase.FinalThird ? 1f : 0f;
        if (progress01 > 0.78f)
        {
            finishBias = 2f;
        }

        return finishBias;
    }

    private void PerformFinishShotDump(int carrier, int teamId)
    {
        var endzone = GetEndzoneRect(1 - teamId);
        var centerY = Mathf.Lerp(endzone.yMin, endzone.yMax, 0.5f);
        var yOffset = DeterministicSignedRange(currentTickIndex, carrier, 509) * (endzone.height * 0.16f);
        var target = new Vector2(endzone.center.x, Mathf.Clamp(centerY + yOffset, endzone.yMin + 0.5f, endzone.yMax - 0.5f));

        ThrowBall(carrier, target, -1, true);
        Debug.Log($"[FINISH] SHOT team={teamId} carrier={carrier} t={matchTimeSeconds:F2}");
    }

    private void ApplyFinishModeOffBallRuns(int teamId, int carrierIndex, Vector2 carrierPos)
    {
        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var attackSign = Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y);
        var endzone = GetEndzoneRect(1 - teamId);
        var endzoneCenter = endzone.center;

        var centralAttacker = FindCentralAttackReceiver(teamId, carrierIndex);
        if (centralAttacker >= 0)
        {
            var runTarget = new Vector2(endzoneCenter.x - (towardGoal.x * 0.7f), Mathf.Clamp(endzoneCenter.y, -halfHeight + 1.2f, halfHeight - 1.2f));
            SetIntent(centralAttacker, IntentType.RunInBehind, runTarget, 2f);
        }

        var farSideWinger = FindNearestByRoleAndSide(teamId, RoleGroup.Attacker, -attackSign, carrierIndex, centralAttacker, carrierPos);
        if (farSideWinger >= 0)
        {
            var backPostY = Mathf.Clamp(-attackSign * (endzone.height * 0.32f), endzone.yMin + 0.7f, endzone.yMax - 0.7f);
            var backPostTarget = new Vector2(endzone.xMax - (teamId == 0 ? 0.9f : endzone.width - 0.9f), backPostY);
            if (teamId == 1)
            {
                backPostTarget.x = endzone.xMin + 0.9f;
            }

            SetIntent(farSideWinger, IntentType.RunInBehind, backPostTarget, 2f);
        }

        var lateMid = FindNearestByRoleAndSide(teamId, RoleGroup.Midfielder, attackSign, carrierIndex, farSideWinger, carrierPos);
        if (lateMid >= 0)
        {
            var cutbackZone = endzoneCenter - (towardGoal * 4.2f);
            SetIntent(lateMid, IntentType.SupportShort, cutbackZone, 2f);
        }
    }

    private bool TryCrossPlay(int carrierIndex, int teamId, Vector2 carrierPos)
    {
        var plan = activePlayPlans[teamId];
        var receiver = IsValidAthleteIndex(plan.forwardIdx) ? plan.forwardIdx : FindCentralAttackReceiver(teamId, carrierIndex);
        if (receiver < 0 || FindNearestOpponentDistance(receiver) < OpenThreshold * 0.9f)
        {
            return false;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var endzoneInnerX = Mathf.Clamp(
            (teamId == 0 ? halfWidth : -halfWidth) - ((teamId == 0 ? 1f : -1f) * (GetEndzoneDepth() * 0.75f)),
            -halfWidth + 1.25f,
            halfWidth - 1.25f);
        var receiverLead = positions[receiver] + (velocities[receiver] * (GetPassLeadTime(carrierIndex) * Mathf.Lerp(0.9f, 1.25f, GetStamina01(carrierIndex))));
        var corridor = new Vector2(endzoneInnerX, Mathf.Clamp(receiverLead.y * 0.35f, -halfHeight * 0.12f, halfHeight * 0.12f));
        var target = Vector2.Lerp(corridor, receiverLead, 0.55f) + (towardGoal * 0.6f);

        ThrowBallSpecialPass(carrierIndex, target, receiver, 1.1f, 1f, 1.8f, "FS:CROSS", applyDefaultFollowUp: false);
        AssignCrossFollowUpIntents(carrierIndex, receiver, teamId, carrierPos, towardGoal);
        Debug.Log($"[CROSS] team={teamId} passer={carrierIndex} receiver={receiver} t={matchTimeSeconds:F2}");
        return true;
    }

    private bool TryCutbackPlay(int carrierIndex, int teamId, Vector2 carrierPos)
    {
        var plan = activePlayPlans[teamId];
        var receiver = IsValidAthleteIndex(plan.cutbackIdx) ? plan.cutbackIdx : FindCutbackReceiver(teamId, carrierIndex, carrierPos);
        if (receiver < 0)
        {
            return false;
        }

        if (receiver == lastPasserIndex && teamId == lastPassTeam && (matchTimeSeconds - lastPassTime) < 1.6f)
        {
            return false;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var lateral = new Vector2(0f, -Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y) * halfHeight * 0.10f);
        var baseTarget = carrierPos - (towardGoal * 3f) + lateral;
        var receiverLead = positions[receiver] + (velocities[receiver] * (GetPassLeadTime(carrierIndex) * 0.9f));
        var target = Vector2.Lerp(baseTarget, receiverLead, 0.65f);

        ThrowBallSpecialPass(carrierIndex, target, receiver, 0.93f, 0.75f, 1.45f, "FS:CUTBACK", applyDefaultFollowUp: false);
        AssignCutbackFollowUpIntents(carrierIndex, receiver, teamId, carrierPos, towardGoal);
        Debug.Log($"[CUTBACK] team={teamId} passer={carrierIndex} receiver={receiver} t={matchTimeSeconds:F2}");
        return true;
    }

    private void UpdateTeamPhases()
    {
        for (var t = 0; t < 2; t++)
        {
            var hasPossession = ballOwnerTeam == t;
            var progress01 = GetTeamProgress(t, ballPos);
            teamPhase[t] = FantasySportTactics.ComputePhase(hasPossession, ballOwnerTeam < 0, progress01);
        }

        if (ballOwnerTeam >= 0 && attackPlanUntilTime[ballOwnerTeam] <= matchTimeSeconds)
        {
            PickAttackSidePlan(ballOwnerTeam, forceRefresh: false);
        }
    }

    private void UpdateTeamLineModel()
    {
        ComputeGoalRects(out var leftRect, out _);
        var endzoneDepth = leftRect.width;
        var margin = 1.2f;

        for (var teamId = 0; teamId < 2; teamId++)
        {
            var ownGoalX = GetOwnBacklineX(teamId);
            var towardCenter = TowardCenterSign(teamId);
            var progress01 = GetTeamProgress(teamId, ballPos);

            var defBase = endzoneDepth + 7f;
            var midBase = endzoneDepth + 15f;
            var attBase = endzoneDepth + 23f;

            var basePush = FantasySportTactics.PhasePush(teamPhase[teamId], progress01);
            var pushExtra = 0f;
            if (ballOwnerTeam == teamId)
            {
                pushExtra = Mathf.Lerp(0f, 10f, attackPressure[teamId]);
            }
            else
            {
                pushExtra = -Mathf.Lerp(0f, 8f, attackPressure[1 - teamId]);
            }

            var inCounterPressWindow = counterPressTeam == teamId && (matchTimeSeconds - possessionFlipTime) <= 1.2f;
            if (inCounterPressWindow && ballOwnerTeam != teamId)
            {
                var retainedPush = Mathf.Lerp(0f, 10f, attackPressure[teamId]) * 0.7f;
                pushExtra = Mathf.Max(pushExtra, retainedPush);
            }

            var defLine = ownGoalX + (towardCenter * (defBase + (basePush * 0.70f) + (pushExtra * 0.95f)));
            var midLine = ownGoalX + (towardCenter * (midBase + (basePush * 0.95f) + (pushExtra * 1.05f)));
            var attLine = ownGoalX + (towardCenter * (attBase + (basePush * 1.10f) + (pushExtra * 1.15f)));

            if (towardCenter > 0f)
            {
                midLine = Mathf.Max(midLine, defLine + 3f);
                attLine = Mathf.Max(attLine, midLine + 3f);
                defLine = Mathf.Min(defLine, midLine - 3f);
            }
            else
            {
                midLine = Mathf.Min(midLine, defLine - 3f);
                attLine = Mathf.Min(attLine, midLine - 3f);
                defLine = Mathf.Max(defLine, midLine + 3f);
            }

            var opponentEndzoneEdge = teamId == 0 ? (halfWidth - endzoneDepth - 0.5f) : (-halfWidth + endzoneDepth + 0.5f);
            defLine = towardCenter > 0f ? Mathf.Min(defLine, opponentEndzoneEdge) : Mathf.Max(defLine, opponentEndzoneEdge);

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
        for (var teamId = 0; teamId < 2; teamId++)
        {
            var delta = ballOwnerTeam == teamId ? dt * 0.25f : -dt * 0.35f;
            attackPressure[teamId] = Mathf.Clamp01(attackPressure[teamId] + delta);
        }

        var newPossessingTeam = ballOwnerIndex >= 0 ? teamIdByIndex[ballOwnerIndex] : -1;
        if (possessingTeam != newPossessingTeam)
        {
            if (possessingTeam >= 0)
            {
                counterPressTeam = possessingTeam;
                possessionFlipTime = matchTimeSeconds;
            }

            possessingTeam = newPossessingTeam;
            possessionTime = 0f;
            noProgressTime = 0f;
            if (possessingTeam >= 0)
            {
                var progress = GetTeamProgress(possessingTeam, ballPos);
                bestProgress = progress;
                lastProgress = progress;
                playProgressBaseline[possessingTeam] = progress;
            }
            else
            {
                bestProgress = 0f;
                lastProgress = 0f;
                threatMeter = 0;
                possessionCongested = false;
                possessionPlan = "None";
                return;
            }
        }

        if (possessingTeam < 0)
        {
            threatMeter = 0;
            return;
        }

        possessionTime += dt;
        var currentProgress = GetTeamProgress(possessingTeam, ballPos);
        lastProgress = currentProgress;
        if (currentProgress > bestProgress + ProgressThreshold)
        {
            bestProgress = currentProgress;
            noProgressTime = 0f;
        }
        else
        {
            noProgressTime += dt;
        }

        var nearbyOpponents = CountOpponentsNear(ballPos, 3f, possessingTeam);
        var nearbyTeammates = CountTeammatesNear(ballPos, 3f, possessingTeam);
        possessionCongested = nearbyOpponents >= 3 || (nearbyOpponents + nearbyTeammates) >= 6;
        threatMeter = ComputeThreatMeter(possessingTeam);
    }

    private int ComputeThreatMeter(int teamId)
    {
        if (teamId < 0)
        {
            return 0;
        }

        var progress01 = GetTeamProgress(teamId, ballPos);
        var centrality01 = 1f - Mathf.Clamp01(Mathf.Abs(ballPos.y) / Mathf.Max(1f, halfHeight));
        var endzone = GetEndzoneRect(1 - teamId);
        var congestionPoint = new Vector2(Mathf.Clamp(ballPos.x, endzone.xMin, endzone.xMax), Mathf.Clamp(ballPos.y, endzone.yMin, endzone.yMax));
        var congestion = Mathf.Clamp01((CountOpponentsNear(congestionPoint, 5f, teamId) + CountTeammatesNear(congestionPoint, 5f, teamId)) / 10f);
        var threat = (progress01 * 62f) + (centrality01 * 20f) + (congestion * 18f);
        return Mathf.Clamp(Mathf.RoundToInt(threat), 0, 100);
    }

    private void PickAttackSidePlan(int teamId, bool forceRefresh)
    {
        if (ballOwnerIndex < 0 || teamId < 0 || teamId >= Teams)
        {
            return;
        }

        var existingPlan = activePlayPlans[teamId];
        if (!forceRefresh && existingPlan.untilTime > matchTimeSeconds && existingPlan.call != PlayCall.None)
        {
            if (noProgressTime > PlayFailFallbackSeconds && lastProgress <= playProgressBaseline[teamId] + 0.01f)
            {
                BuildPlayPlan(teamId, existingPlan.side, PlayCall.Switch, useFallback: true);
            }
            return;
        }

        var leftOpen = EvaluateWingOpenness(teamId, AttackSide.Left);
        var rightOpen = EvaluateWingOpenness(teamId, AttackSide.Right);
        var parityLeft = ((currentTickIndex + simulationSeed + teamId) & 1) == 0;
        currentAttackSide[teamId] = Mathf.Abs(leftOpen - rightOpen) > 0.4f
            ? (leftOpen >= rightOpen ? AttackSide.Left : AttackSide.Right)
            : (parityLeft ? AttackSide.Left : AttackSide.Right);
        var side = currentAttackSide[teamId] == AttackSide.Left ? -1 : 1;

        var playCall = DeterminePlayCall(teamId, side);
        BuildPlayPlan(teamId, side, playCall, useFallback: false);
    }

    private PlayCall DeterminePlayCall(int teamId, int side)
    {
        if (ballOwnerIndex < 0 || teamId != ballOwnerTeam)
        {
            return PlayCall.None;
        }

        var carrier = ballOwnerIndex;
        var progress = GetTeamProgress(teamId, ballPos);
        var nearPressure = CountOpponentsNear(positions[carrier], 2.4f, teamId);
        var onWing = Mathf.Abs(positions[carrier].y) > halfHeight * 0.24f;
        var wingCongested = CountOpponentsNear(positions[carrier], 4.8f, teamId) >= 4;

        if (IsGoalkeeper(carrier) && progress < 0.34f && nearPressure <= 1)
        {
            return PlayCall.BuildOut;
        }

        if (teamPhase[teamId] == TeamPhase.FinalThird && onWing)
        {
            return ((currentTickIndex + teamId + simulationSeed) & 1) == 0 ? PlayCall.FinalThirdCross : PlayCall.FinalThirdCutback;
        }

        if (teamPhase[teamId] == TeamPhase.Advance && onWing)
        {
            var overlap = FindNearestByRoleAndSide(teamId, RoleGroup.Defender, side, carrier, -1, positions[carrier]);
            if (IsValidAthleteIndex(overlap))
            {
                return PlayCall.Overlap;
            }
        }

        if (wingCongested || noProgressTime > 1.2f)
        {
            return PlayCall.Switch;
        }

        return PlayCall.BuildOut;
    }

    private void BuildPlayPlan(int teamId, int side, PlayCall call, bool useFallback)
    {
        if (ballOwnerIndex < 0 || teamId < 0 || teamId >= Teams)
        {
            return;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - ballPos).normalized;
        var ttl = Mathf.Lerp(PlayCallMinTtl, PlayCallMaxTtl, ((simulationSeed + currentTickIndex + teamId) & 1) == 0 ? 0.35f : 0.8f);
        var untilTime = matchTimeSeconds + ttl;
        var carrier = ballOwnerIndex;
        var plan = new PlayPlan
        {
            teamId = teamId,
            call = call,
            side = side,
            untilTime = untilTime,
            shortIdx = FindShortSupportNode(teamId, carrier, towardGoal),
            wideIdx = FindWideOutletNode(teamId, carrier),
            forwardIdx = FindForwardRunNode(teamId, carrier, towardGoal),
            overlapIdx = FindNearestByRoleAndSide(teamId, RoleGroup.Defender, side, carrier, -1, ballPos),
            cutbackIdx = FindNearestByRoleAndSide(teamId, RoleGroup.Midfielder, side, carrier, -1, ballPos)
        };

        if (!IsValidAthleteIndex(plan.wideIdx))
        {
            plan.wideIdx = FindNearestByRoleAndSide(teamId, RoleGroup.Attacker, side, carrier, -1, ballPos);
        }

        if (useFallback && call == PlayCall.Switch)
        {
            plan.wideIdx = FindBestOpenTeammate(carrier, preferWing: true, forceSwitch: true);
            plan.forwardIdx = FindForwardRunNode(teamId, carrier, towardGoal);
        }

        attackPlanUntilTime[teamId] = untilTime;
        activePlayPlans[teamId] = plan;
        playProgressBaseline[teamId] = lastProgress;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PLAN] build t={matchTimeSeconds:F1} team={teamId} call={plan.call} side={plan.side} until={plan.untilTime:F1} nodes={plan.shortIdx}/{plan.wideIdx}/{plan.forwardIdx}/{plan.overlapIdx}/{plan.cutbackIdx}");
#endif
    }

    private void ApplyPlayPlanIntents(int teamId)
    {
        if (teamId < 0 || teamId >= Teams)
        {
            return;
        }

        var plan = activePlayPlans[teamId];
        if (plan.untilTime <= matchTimeSeconds)
        {
            return;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - ballPos).normalized;
        var carrierPos = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
        var wingY = plan.side * GetLaneWideY(teamId);
        var ttl = Mathf.Clamp(plan.untilTime - matchTimeSeconds, 1.6f, PlayCallMaxTtl);

        if (IsValidAthleteIndex(plan.shortIdx))
        {
            var shortDepth = plan.call == PlayCall.BuildOut ? 4f : 2.8f;
            var shortTarget = carrierPos - (towardGoal * shortDepth) + new Vector2(0f, plan.side * 1.1f);
            SetIntent(plan.shortIdx, IntentType.SupportShort, shortTarget, ttl);
        }

        if (IsValidAthleteIndex(plan.wideIdx))
        {
            var widthX = plan.call == PlayCall.Switch ? 8f : 6f;
            var wingTarget = new Vector2(Mathf.Clamp(carrierPos.x + towardGoal.x * widthX, -halfWidth + 1.2f, halfWidth - 1.2f), wingY);
            var intent = plan.call == PlayCall.Switch ? IntentType.SwitchOutlet : IntentType.HoldWidth;
            SetIntent(plan.wideIdx, intent, wingTarget, ttl);
        }

        if (IsValidAthleteIndex(plan.overlapIdx))
        {
            var overlapX = Mathf.Lerp(midLineXByTeam[teamId], attLineXByTeam[teamId], plan.call == PlayCall.Overlap ? 0.72f : 0.6f);
            SetIntent(plan.overlapIdx, IntentType.OverlapRun, new Vector2(overlapX, wingY), ttl);
        }

        if (IsValidAthleteIndex(plan.forwardIdx))
        {
            var laneY = plan.side * GetLaneNarrowY(teamId) * 0.7f;
            var runDepth = plan.call == PlayCall.FinalThirdCross ? RunDepth * 0.85f : RunDepth;
            var runTarget = carrierPos + (towardGoal * runDepth) + new Vector2(0f, laneY);
            SetIntent(plan.forwardIdx, IntentType.RunInBehind, runTarget, ttl);
        }

        if (IsValidAthleteIndex(plan.cutbackIdx))
        {
            var endzone = GetEndzoneRect(1 - teamId);
            var topOfBox = new Vector2(endzone.center.x - (towardGoal.x * Mathf.Min(5f, endzone.width * 0.8f)), plan.side * GetLaneNarrowY(teamId));
            SetIntent(plan.cutbackIdx, IntentType.SupportShort, topOfBox, ttl);
        }
    }

    private void UpdateSupportTriangle(int teamId, int carrierIndex)
    {
        if (teamId < 0 || teamId >= Teams || carrierIndex < 0)
        {
            return;
        }

        var carrierPos = positions[carrierIndex];
        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var shortIdx = FindShortSupportNode(teamId, carrierIndex, towardGoal);
        var wideIdx = FindWideOutletNode(teamId, carrierIndex);
        var forwardIdx = FindForwardRunNode(teamId, carrierIndex, towardGoal);

        triangleShortSupportByTeam[teamId] = shortIdx;
        triangleWideOutletByTeam[teamId] = wideIdx;
        triangleForwardRunByTeam[teamId] = forwardIdx;
    }

    private void ApplySupportTriangleIntents(int teamId)
    {
        if (teamId < 0 || teamId >= Teams || ballOwnerIndex < 0)
        {
            return;
        }

        var carrierPos = positions[ballOwnerIndex];
        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var shortIdx = triangleShortSupportByTeam[teamId];
        var wideIdx = triangleWideOutletByTeam[teamId];
        var forwardIdx = triangleForwardRunByTeam[teamId];

        if (shortIdx >= 0)
        {
            var shortTarget = carrierPos - (towardGoal * 3f) + new Vector2(0f, Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y) * 1.4f);
            SetIntent(shortIdx, IntentType.SupportShort, shortTarget, 1.8f);
        }

        if (wideIdx >= 0)
        {
            var wingY = Mathf.Sign(positions[wideIdx].y == 0f ? -carrierPos.y : positions[wideIdx].y);
            if (Mathf.Abs(wingY) < 0.001f)
            {
                wingY = -GetAttackWingSign(teamId);
            }

            var target = new Vector2(Mathf.Clamp(carrierPos.x + towardGoal.x * 5.5f, -halfWidth + 1.2f, halfWidth - 1.2f), wingY * GetLaneWideY(teamId));
            SetIntent(wideIdx, IntentType.HoldWidth, target, 2.2f);
        }

        if (forwardIdx >= 0)
        {
            var laneY = Mathf.Lerp(0f, GetLaneNarrowY(teamId), 0.8f) * Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y);
            var target = carrierPos + (towardGoal * RunDepth) + new Vector2(0f, laneY * 0.65f);
            SetIntent(forwardIdx, IntentType.RunInBehind, target, 2.3f);
        }
    }

    private int FindShortSupportNode(int teamId, int carrierIndex, Vector2 towardGoal)
    {
        var carrierPos = positions[carrierIndex];
        var best = -1;
        var bestScore = float.MinValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId || IsGoalkeeper(i))
            {
                continue;
            }

            var behind = Vector2.Dot(positions[i] - carrierPos, towardGoal) <= 1f;
            if (!behind)
            {
                continue;
            }

            var roleScore = roleByIndex[i] == RoleGroup.Midfielder ? 4.5f : (roleByIndex[i] == RoleGroup.Defender || roleByIndex[i] == RoleGroup.Sweeper ? 3f : 0f);
            var distScore = -Vector2.Distance(positions[i], carrierPos);
            var openness = FindNearestOpponentDistance(i) * 0.5f;
            var score = roleScore + distScore + openness;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private int FindWideOutletNode(int teamId, int carrierIndex)
    {
        var carrierPos = positions[carrierIndex];
        var desiredSign = Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y) * -1f;
        var best = -1;
        var bestScore = float.MinValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId || IsGoalkeeper(i))
            {
                continue;
            }

            var sign = Mathf.Sign(positions[i].y);
            var signMatch = sign == desiredSign;
            var widthScore = Mathf.Abs(positions[i].y);
            var roleScore = roleByIndex[i] == RoleGroup.Attacker ? 4f : (roleByIndex[i] == RoleGroup.Midfielder ? 2.6f : 1.2f);
            var score = roleScore + (widthScore * 0.3f) + (signMatch ? 4.5f : 0f) + (FindNearestOpponentDistance(i) * 0.4f);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private int FindForwardRunNode(int teamId, int carrierIndex, Vector2 towardGoal)
    {
        var carrierPos = positions[carrierIndex];
        var best = -1;
        var bestScore = float.MinValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId || IsGoalkeeper(i))
            {
                continue;
            }

            var ahead = Vector2.Dot(positions[i] - carrierPos, towardGoal) > 1f;
            if (!ahead)
            {
                continue;
            }

            var roleScore = roleByIndex[i] == RoleGroup.Attacker ? 4.8f : (roleByIndex[i] == RoleGroup.Midfielder ? 2.8f : 0.8f);
            var depth = Vector2.Dot(positions[i] - carrierPos, towardGoal);
            var score = roleScore + (depth * 0.9f) + (FindNearestOpponentDistance(i) * 0.5f);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
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
        var currentPossessingTeam = (ballOwnerIndex >= 0 && teamIdByIndex != null) ? teamIdByIndex[ballOwnerIndex] : -1;
        if (currentPossessingTeam != previousPossessionTeam)
        {
            TriggerReturnToShapeBoost();
            previousPossessionTeam = currentPossessingTeam;
            if (currentPossessingTeam >= 0)
            {
                PickAttackSidePlan(currentPossessingTeam, forceRefresh: true);
            }
        }

        if (currentPossessingTeam < 0 || shotClockSeconds <= 0f)
        {
            return;
        }

        if (possessionTime < Mathf.Max(6f, shotClockSeconds))
        {
            return;
        }

        ForceShotClockAction(ballOwnerIndex, currentPossessingTeam);
        possessionTime = 0f;
    }

    private void ForceShotClockAction(int carrier, int teamId)
    {
        if (carrier < 0 || ballOwnerIndex != carrier)
        {
            return;
        }

        var toGoal = GetOpponentGoalCenter(teamId) - positions[carrier];
        var progress = GetTeamProgress(teamId, positions[carrier]);
        if (possessionTime > 6f && progress > 0.5f)
        {
            ThrowBall(carrier, GetEndzoneRect(1 - teamId).center, -1, true);
            passCooldowns[carrier] = PassCooldownSeconds;
            Debug.Log($"[FantasySport] ShotClock forced ENDZONE FINISH by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        if (toGoal.magnitude <= ShootRange)
        {
            ThrowBall(carrier, GetOpponentGoalCenter(teamId), -1, true);
            passCooldowns[carrier] = PassCooldownSeconds * 0.7f;
            Debug.Log($"[FantasySport] ShotClock forced SHOT by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        var openTeammate = FindBestOpenTeammate(carrier, preferWing: false, forceSwitch: false);
        if (openTeammate >= 0)
        {
            var leadTime = GetPassLeadTime(carrier);
            var target = positions[openTeammate] + (velocities[openTeammate] * leadTime);
            ThrowBall(carrier, target, openTeammate, false);
            passCooldowns[carrier] = PassCooldownSeconds;
            Debug.Log($"[FantasySport] ShotClock forced PROGRESSIVE PASS by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        var switchTeammate = FindBestOpenTeammate(carrier, preferWing: true, forceSwitch: true);
        if (switchTeammate >= 0)
        {
            var leadTime = GetPassLeadTime(carrier);
            var target = positions[switchTeammate] + (velocities[switchTeammate] * leadTime);
            ThrowBall(carrier, target, switchTeammate, false);
            passCooldowns[carrier] = PassCooldownSeconds;
            Debug.Log($"[FantasySport] ShotClock forced SWITCH by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        if (TryThroughBallPass(carrier, teamId))
        {
            passCooldowns[carrier] = PassCooldownSeconds;
            Debug.Log($"[FantasySport] ShotClock forced THROUGH by team {teamId} at t={elapsedMatchTime:F2}s");
            return;
        }

        ThrowBall(carrier, GetEndzoneBandTarget(carrier, teamId), -1, false);
        passCooldowns[carrier] = PassCooldownSeconds;
        Debug.Log($"[FantasySport] ShotClock forced DUMP by team {teamId} at t={elapsedMatchTime:F2}s");
    }

    private bool TryKeeperLongShot(int carrier, int teamId, Vector2 carrierPos, bool pressured, bool shotClockHot)
    {
        if (!IsGoalkeeper(carrier))
        {
            return false;
        }

        var ownThird = GetTeamProgress(teamId, carrierPos) <= 0.34f;
        var heavilyPressured = pressured || FindNearestOpponentDistance(carrier) <= PressureRadius * 0.75f;
        if (!ownThird && !heavilyPressured && !shotClockHot)
        {
            return false;
        }

        var stamina01 = GetStamina01(carrier);
        if (stamina01 <= 0.35f)
        {
            return false;
        }

        var noSafeShortPass = FindBestOpenTeammate(carrier, preferWing: false, forceSwitch: false) < 0;
        var pinned = teamId == 0 ? carrierPos.x < (-halfWidth * 0.58f) : carrierPos.x > (halfWidth * 0.58f);
        if (!noSafeShortPass && !shotClockHot && !pinned && !pressured)
        {
            return false;
        }

        var targetX = teamId == 0 ? halfWidth * 0.10f : -halfWidth * 0.10f;
        var target = new Vector2(targetX, GetMoreOpenBandY(teamId, carrier));

        var start = positions[carrier];
        var dir = target - start;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = teamId == 0 ? Vector2.right : Vector2.left;
        }

        dir.Normalize();
        var shotSpeed = Mathf.Lerp(KeeperLongShotMinSpeed, KeeperLongShotMaxSpeed, stamina01);
        var errDeg = Mathf.Lerp(4f, 18f, 1f - stamina01);
        var rng = RngService.Fork($"FS:KEEPER_PUNT:{currentTickIndex}:{carrier}:{simulationSeed}");
        var finalDir = Rotate(dir, rng.Range(-errDeg, errDeg));

        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        freeBallTime = 0f;
        ballPos = start;
        ballVel = (finalDir * shotSpeed) + (velocities[carrier] * 0.1f);
        lastThrowTeam = teamId;
        lastThrowTime = elapsedMatchTime;
        intendedReceiverIndex = -1;
        receiverLockUntilTime = elapsedMatchTime + ReceiverLockSeconds;
        stamina[carrier] = Mathf.Max(0f, stamina[carrier] - KeeperLongShotStaminaCost);
        return true;
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

        if (!isShot && receiverIndex >= 0)
        {
            lastPasserIndex = carrierIndex;
            lastReceiverIndex = receiverIndex;
            lastPassTime = matchTimeSeconds;
            lastPassTeam = lastThrowTeam;
            AssignFollowUpIntents(carrierIndex, receiverIndex, lastThrowTeam);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isShot && receiverIndex >= 0 && showMechanicDebug)
        {
            Debug.Log($"[FantasySport] PASS team={lastThrowTeam} from={carrierIndex} to={receiverIndex}");
        }
#endif
    }

    private void ThrowBallSpecialPass(int carrierIndex, Vector2 target, int receiverIndex, float speedScale, float errorScale, float tiredErrorScale, string rngTag, bool applyDefaultFollowUp)
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
        var throwSpeed = Mathf.Lerp(10f, 12f, stats.throwPower) * staminaMultiplier * speedScale;
        var errDeg = Mathf.Lerp(18f, 4f, stats.throwAccuracy) * Mathf.Lerp(1f, tiredErrorScale, 1f - stamina01) * errorScale;
        var rng = RngService.Fork($"{rngTag}:{currentTickIndex}:{carrierIndex}:{simulationSeed}");
        var finalDir = Rotate(dir, rng.Range(-errDeg, errDeg));

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
        lastPasserIndex = carrierIndex;
        lastReceiverIndex = receiverIndex;
        lastPassTime = matchTimeSeconds;
        lastPassTeam = lastThrowTeam;

        if (applyDefaultFollowUp && receiverIndex >= 0)
        {
            AssignFollowUpIntents(carrierIndex, receiverIndex, lastThrowTeam);
        }
    }

    private void AssignFollowUpIntents(int passerIndex, int receiverIndex, int teamId)
    {
        var receiverPos = positions[receiverIndex];
        var towardGoal = (GetOpponentGoalCenter(teamId) - receiverPos).normalized;
        var support = receiverPos - (towardGoal * 3.4f);
        SetIntent(passerIndex, IntentType.SupportShort, support, 2f);

        var wideOutlet = triangleWideOutletByTeam[teamId];
        if (wideOutlet >= 0 && wideOutlet != receiverIndex)
        {
            var wingSign = Mathf.Sign(positions[wideOutlet].y == 0f ? GetAttackWingSign(teamId) : positions[wideOutlet].y);
            var outlet = new Vector2(receiverPos.x + (towardGoal.x * 3.2f), Mathf.Clamp(wingSign * GetLaneWideY(teamId), -halfHeight + 1.4f, halfHeight - 1.4f));
            SetIntent(wideOutlet, IntentType.HoldWidth, outlet, 2f);
        }

        var forwardRun = triangleForwardRunByTeam[teamId];
        if (forwardRun >= 0 && forwardRun != receiverIndex)
        {
            var runTarget = receiverPos + (towardGoal * 8f) + new Vector2(0f, Mathf.Sign(receiverPos.y == 0f ? GetAttackWingSign(teamId) : receiverPos.y) * 2f);
            SetIntent(forwardRun, IntentType.RunInBehind, runTarget, 2f);
        }

        var shortSupport = triangleShortSupportByTeam[teamId];
        if (shortSupport >= 0 && shortSupport != passerIndex && shortSupport != receiverIndex)
        {
            var shortTarget = receiverPos - (towardGoal * 3f);
            SetIntent(shortSupport, IntentType.SupportShort, shortTarget, 2f);
        }
    }

    private void AssignCrossFollowUpIntents(int passerIndex, int receiverIndex, int teamId, Vector2 carrierPos, Vector2 towardGoal)
    {
        var side = Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y);
        var farSideWinger = FindNearestByRoleAndSide(teamId, RoleGroup.Attacker, -side, passerIndex, receiverIndex, positions[receiverIndex]);
        if (farSideWinger >= 0)
        {
            var crashTarget = positions[receiverIndex] + (towardGoal * 6.5f) + new Vector2(0f, -side * GetWingLaneAbs() * 0.7f);
            SetIntent(farSideWinger, IntentType.RunInBehind, crashTarget, 2f);
        }

        var topOfBox = positions[receiverIndex] - (towardGoal * 4f);
        var supportMid = FindNearestByRoleAndSide(teamId, RoleGroup.Midfielder, side, passerIndex, receiverIndex, positions[receiverIndex]);
        if (supportMid >= 0)
        {
            SetIntent(supportMid, IntentType.SupportShort, topOfBox, 2f);
        }

        var passerSupport = carrierPos - (towardGoal * 2.6f);
        SetIntent(passerIndex, IntentType.SupportShort, passerSupport, 2f);
    }

    private void AssignCutbackFollowUpIntents(int passerIndex, int receiverIndex, int teamId, Vector2 carrierPos, Vector2 towardGoal)
    {
        var centralAttacker = FindCentralAttackReceiver(teamId, passerIndex, receiverIndex);
        if (centralAttacker >= 0)
        {
            var finish = positions[receiverIndex] + (towardGoal * 5.4f);
            SetIntent(centralAttacker, IntentType.RunInBehind, finish, 2f);
        }

        var wideReset = new Vector2(
            Mathf.Clamp(carrierPos.x - (towardGoal.x * 1.8f), -halfWidth + 1.25f, halfWidth - 1.25f),
            Mathf.Clamp(Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y) * GetWingLaneAbs(), -halfHeight + 1.25f, halfHeight - 1.25f));
        SetIntent(passerIndex, IntentType.HoldWidth, wideReset, 2f);
    }

    private int FindCentralAttackReceiver(int teamId, int excludeA, int excludeB = -1)
    {
        var best = -1;
        var bestScore = float.MinValue;
        var towardX = teamId == 0 ? 1f : -1f;
        var endzoneBandX = (teamId == 0 ? halfWidth : -halfWidth) - (towardX * (GetEndzoneDepth() * 1.25f));
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == excludeA || i == excludeB || identities[i].teamId != teamId || IsGoalkeeper(i))
            {
                continue;
            }

            var lane = laneByIndex[i];
            var inCenterLane = lane == Lane.Center || lane == Lane.LeftCenter || lane == Lane.RightCenter;
            if (!inCenterLane)
            {
                continue;
            }

            var roleBonus = roleByIndex[i] == RoleGroup.Attacker ? 3f : 0f;
            var openness = FindNearestOpponentDistance(i);
            var corridorPenalty = Mathf.Abs(positions[i].y) * 0.4f;
            var inCorridor = Mathf.Abs(positions[i].y) < halfHeight * 0.20f;
            var endzoneBonus = ((positions[i].x - endzoneBandX) * towardX) > 0f ? 2f : 0f;
            var score = roleBonus + openness + endzoneBonus - corridorPenalty;
            if (!inCorridor)
            {
                score -= 1.1f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private int FindCutbackReceiver(int teamId, int carrierIndex, Vector2 carrierPos)
    {
        var best = -1;
        var bestScore = float.MinValue;
        var wingSide = Mathf.Sign(carrierPos.y == 0f ? GetAttackWingSign(teamId) : carrierPos.y);
        var towardX = teamId == 0 ? 1f : -1f;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId || IsGoalkeeper(i))
            {
                continue;
            }

            if (roleByIndex[i] != RoleGroup.Midfielder)
            {
                continue;
            }

            var lane = laneByIndex[i];
            var halfSpaceMatch = wingSide < 0f
                ? (lane == Lane.LeftCenter || lane == Lane.Center)
                : (lane == Lane.RightCenter || lane == Lane.Center);
            if (!halfSpaceMatch)
            {
                continue;
            }

            var behindBall = (positions[i].x - carrierPos.x) * towardX < 1f;
            if (!behindBall)
            {
                continue;
            }

            var openness = FindNearestOpponentDistance(i);
            if (openness < OpenThreshold * 0.9f)
            {
                continue;
            }

            var shootingLane = 1f - Mathf.Clamp01(Mathf.Abs(positions[i].y) / (halfHeight * 0.6f));
            var score = openness + (shootingLane * 2.2f) - (Vector2.Distance(positions[i], carrierPos) * 0.12f);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private void SetIntent(int athleteIndex, IntentType intent, Vector2 target, float baseDuration)
    {
        if (athleteIndex < 0 || athleteIndex >= TotalAthletes || IsGoalkeeper(athleteIndex))
        {
            return;
        }

        var jitter = DeterministicSignedRange(currentTickIndex, athleteIndex, (int)intent + 31) * 0.45f;
        intentType[athleteIndex] = intent;
        intentTarget[athleteIndex] = new Vector2(
            Mathf.Clamp(target.x, -halfWidth + 1.1f, halfWidth - 1.1f),
            Mathf.Clamp(target.y, -halfHeight + 1.1f, halfHeight - 1.1f));
        intentUntilTime[athleteIndex] = matchTimeSeconds + Mathf.Clamp(baseDuration + jitter, 1.5f, 2.5f);
    }

    private (int start, int end) GetTeamSpan(int teamId)
    {
        if (positions == null || positions.Length == 0 || teamId < 0 || teamId >= Teams)
        {
            return (0, 0);
        }

        var start = teamId * PlayersPerTeam;
        var end = Mathf.Min(start + PlayersPerTeam, positions.Length);
        start = Mathf.Clamp(start, 0, positions.Length);
        return (start, end);
    }

    private bool IsValidAthleteIndex(int idx)
    {
        return idx >= 0 && positions != null && idx < positions.Length;
    }

    private bool IsLaneSideMatch(int idx, float sideSign)
    {
        if (!IsValidAthleteIndex(idx) || Mathf.Approximately(sideSign, 0f))
        {
            return true;
        }

        if (sideSign < 0f)
        {
            var laneMatch = laneByIndex[idx] == Lane.Left || laneByIndex[idx] == Lane.LeftCenter;
            return laneMatch || positions[idx].y < 0f;
        }

        var rightLaneMatch = laneByIndex[idx] == Lane.Right || laneByIndex[idx] == Lane.RightCenter;
        return rightLaneMatch || positions[idx].y > 0f;
    }

    private int FindNearestByRoleAndSide(int teamId, RoleGroup role, float sideSign, int excludeA, int excludeB, Vector2 refPos)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        var (start, end) = GetTeamSpan(teamId);
        for (var idx = start; idx < end; idx++)
        {
            if (!IsValidAthleteIndex(idx) || idx == excludeA || idx == excludeB || roleByIndex[idx] != role || IsGoalkeeper(idx))
            {
                continue;
            }

            if (!IsLaneSideMatch(idx, sideSign))
            {
                continue;
            }

            var dist = (positions[idx] - refPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = idx;
            }
        }

        return best;
    }

    private void UpdateAthletes(float dt)
    {
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (returnToShapeUntilByAthlete != null && i < returnToShapeUntilByAthlete.Length && returnToShapeUntilByAthlete[i] > 0f)
            {
                returnToShapeUntilByAthlete[i] = Mathf.Max(0f, returnToShapeUntilByAthlete[i] - dt);
            }

            if (matchTimeSeconds < kickoffFreezeUntilTime)
            {
                var teamId = TeamOf(i);
                var localIndex = LocalIndexOf(i);
                var frozenSpot = GetKickoffSpot(teamId, localIndex);
                if (i == kickoffDuelIndexByTeam[teamId])
                {
                    frozenSpot.x += teamId == 0 ? 1.2f : -1.2f;
                }

                positions[i] = frozenSpot;
                velocities[i] = Vector2.zero;
                ClampAthlete(i);
                continue;
            }

            var desired = stunTimers[i] > 0f || matchFinished ? Vector2.zero : ComputeDesiredVelocity(i);
            var fatigue = GetFatigue01(i);
            var effectiveAccel = Mathf.Lerp(6f, 14f, profiles[i].accel) * (0.55f + (0.45f * fatigue));
            velocities[i] = Vector2.MoveTowards(velocities[i], desired, effectiveAccel * dt);
            positions[i] += velocities[i] * dt;
            ResolveAthleteBumperCollision(i, dt);
            ClampAthlete(i);

            var deltaY = Mathf.Abs(positions[i].y - prevPosByAthlete[i].y);
            if (matchTimeSeconds >= kickoffFreezeUntilTime && deltaY > 8f)
            {
                positions[i] = prevPosByAthlete[i];
                velocities[i] = Vector2.zero;
                if (!safetyGuardLogByAthlete[i])
                {
                    Debug.LogWarning($"[FS] SAFETY_REVERT_Y i={i} deltaY={deltaY:F2} prev={prevPosByAthlete[i]} revertedPos={positions[i]} tick={currentTickIndex}");
                    safetyGuardLogByAthlete[i] = true;
                }
            }

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
                    if (finishPlanType[i] == 1 && finishPlanUntil[i] >= matchTimeSeconds)
                    {
                        objective = finishPlanTarget[i];
                    }
                    else
                    {
                        var attackWingY = GetAttackWingSign(teamId) * GetLaneWideY();
                        var centralBlocked = IsCentralLaneBlocked(i);
                        objective = centralBlocked
                            ? new Vector2(GetOpponentGoalCenter(teamId).x * 0.65f, attackWingY)
                            : GetOpponentGoalCenter(teamId);
                    }
                }
                break;
            case AthleteState.ChaseFreeBall:
                objective = ballPos;
                break;
            case AthleteState.PressCarrier:
                objective = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
                break;
            case AthleteState.MarkLane:
                objective = ComputeMarkLaneObjective(i);
                break;
            case AthleteState.GoalkeeperHome:
                objective = ComputeGoalkeeperTarget(i);
                break;
        }

        if (IsActiveChaser(i))
        {
            objective = GetActiveChaseTarget(i);
        }

        var home = ComputeHomePosition(i);
        var homeDelta = home - positions[i];
        var fieldWidth = halfWidth * 2f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (homeDelta.magnitude > fieldWidth * 0.75f)
        {
            var teamId = identities[i].teamId;
            Debug.LogWarning($"[FS] Huge homeTarget jump i={i} team={teamId} pos={positions[i]} home={home} lane={laneByIndex[i]} role={roleByIndex[i]}");
        }
#endif
        if (homeDelta.magnitude > MaxHomeSnapPerTick)
        {
            home = positions[i] + (homeDelta.normalized * MaxHomeSnapPerTick);
        }

        if (i < intentUntilTime.Length && matchTimeSeconds < intentUntilTime[i] && !IsActiveChaser(i) && athleteStates[i] != AthleteState.BallCarrier)
        {
            objective = intentTarget[i];
        }

        if (ShouldApplyPatrolJitter(i))
        {
            objective = home + ComputePatrolJitter(i);
        }

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

        var phaseProgress = GetTeamProgress(teamId, ballPos);
        var home = FantasySportTactics.HomeTarget(roleByIndex[athleteIndex], laneByIndex[athleteIndex], teamId, teamPhase[teamId], phaseProgress, halfWidth, halfHeight, GetEndzoneDepth(), ballPos);
        var role = roleByIndex[athleteIndex];
        home.x = role switch
        {
            RoleGroup.Defender => defLineXByTeam[teamId],
            RoleGroup.Midfielder => midLineXByTeam[teamId],
            RoleGroup.Attacker => attLineXByTeam[teamId],
            RoleGroup.Sweeper => Mathf.Lerp(defLineXByTeam[teamId], midLineXByTeam[teamId], 0.35f),
            _ => home.x
        };

        home.y = GetLaneY(laneByIndex[athleteIndex], halfHeight, 0f, teamId);

        if (ballOwnerTeam >= 0 && ballOwnerTeam != teamId)
        {
            var roleThreat = roleByIndex[athleteIndex] == RoleGroup.Defender || roleByIndex[athleteIndex] == RoleGroup.Sweeper;
            if (roleThreat && threatMeter > 60)
            {
                home.y = Mathf.Lerp(home.y, Mathf.Clamp(ballPos.y, -halfHeight * 0.22f, halfHeight * 0.22f), 0.55f);
                home.x = Mathf.Lerp(home.x, defLineXByTeam[teamId], 0.25f);
            }

            var wingBall = Mathf.Abs(ballPos.y) > halfHeight * 0.24f;
            if (wingBall && threatMeter > 40f && roleThreat)
            {
                var trapSign = Mathf.Sign(ballPos.y);
                if (Mathf.Sign(home.y) == trapSign)
                {
                    home.y = Mathf.Lerp(home.y, ballPos.y, 0.4f);
                }
                else if (laneByIndex[athleteIndex] == Lane.Center || laneByIndex[athleteIndex] == Lane.LeftCenter || laneByIndex[athleteIndex] == Lane.RightCenter)
                {
                    home.y = Mathf.Lerp(home.y, ballPos.y * 0.35f, 0.5f);
                }
            }
        }

        if (athleteIndex < intentUntilTime.Length && matchTimeSeconds < intentUntilTime[athleteIndex])
        {
            var intentBlend = Mathf.InverseLerp(intentUntilTime[athleteIndex], intentUntilTime[athleteIndex] - 0.55f, matchTimeSeconds);
            home = Vector2.Lerp(home, intentTarget[athleteIndex], Mathf.Clamp01(intentBlend));
        }

        var lanePush = ComputeLaneOccupancyPush(athleteIndex);
        home.y += lanePush;

        home.x = Mathf.Clamp(home.x, -halfWidth + 1.1f, halfWidth - 1.1f);
        home.y = Mathf.Clamp(home.y, -halfHeight + 2f, halfHeight - 2f);
        return home;
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
        var lane = laneByIndex[athleteIndex];
        var allowCenterBias = (lane == Lane.Center || lane == Lane.LeftCenter || lane == Lane.RightCenter) && !ShouldApplyWidthAnchor(teamId, athleteIndex);
        var centerBias = (IsGoalkeeper(athleteIndex) || !allowCenterBias || IsBandAnchor(teamId, athleteIndex))
            ? Vector2.zero
            : (Vector2.up * (-athletePos.y) * CenterYBias * centerBiasScale);

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

        var widthSpring = Vector2.zero;
        if (!isCarrier && ShouldApplyWidthAnchor(teamId, athleteIndex))
        {
            var targetWingY = GetWidthAnchorTargetY(teamId, athleteIndex);
            widthSpring.y += (targetWingY - athletePos.y) * BandAnchorSpring;
        }

        var separationWeight = (!isEngagedRole && !IsGoalkeeper(athleteIndex)) ? 2.35f : 1.95f;
        var steering = (objective * 1.45f) + (homePull * (homeWeight + roamPullWeight)) + (separation * separationWeight) + (boundary * 1.45f) + centerBias + widthSpring;
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

        return roleByIndex[athleteIndex] == RoleGroup.Attacker && IsAttackerOpenNearGoal(athleteIndex);
    }

    private float GetRoleShapeWeight(RoleGroup role)
    {
        return role switch
        {
            RoleGroup.Sweeper => 1.5f,
            RoleGroup.Defender => 1.2f,
            RoleGroup.Midfielder => 0.9f,
            RoleGroup.Attacker => 0.7f,
            _ => 1f
        };
    }

    private float GetRoleRoamRadius(RoleGroup role)
    {
        return role switch
        {
            RoleGroup.Sweeper => 6.5f,
            RoleGroup.Defender => 10f,
            RoleGroup.Midfielder => 13f,
            RoleGroup.Attacker => 15f,
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

        if (roleByIndex[athleteIndex] == RoleGroup.Attacker && IsAttackerOpenNearGoal(athleteIndex))
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

        ComputeGoalRects(out var leftEndzone, out var rightEndzone);

        var enteredLeftEndzone = !leftEndzone.Contains(previousBallPos) && leftEndzone.Contains(ballPos);
        var enteredRightEndzone = !rightEndzone.Contains(previousBallPos) && rightEndzone.Contains(ballPos);

        if (enteredLeftEndzone)
        {
            scoreTeam1++;
            Debug.Log($"[FantasySport] GOAL team=1 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} mode=endzone-left");
            ResetAfterGoal();
            goalCooldownUntilTick = tickIndex + GoalCooldownTicks;
            previousBallPos = Vector2.zero;
            UpdateHud(force: true);
            UpdateScoreboardUI(force: true);
        }
        else if (enteredRightEndzone)
        {
            scoreTeam0++;
            Debug.Log($"[FantasySport] GOAL team=0 score={scoreTeam0}-{scoreTeam1} tick={tickIndex} mode=endzone-right");
            ResetAfterGoal();
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

            var isCarrier = ballOwnerIndex == i;
            possessionRings[i].enabled = isCarrier;
            possessionRings[i].transform.localPosition = isCarrier ? new Vector3(0f, 1.55f, 0f) : Vector3.zero;
            possessionRings[i].transform.localScale = isCarrier ? Vector3.one * 0.66f : Vector3.one * 1.1f;
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

    private void UpdateTacticsDebug()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!showTacticsDebug)
        {
            return;
        }

        var currentSecond = Mathf.FloorToInt(matchTimeSeconds);
        if (currentSecond != lastTacticsDebugSecond)
        {
            lastTacticsDebugSecond = currentSecond;
            var team = possessingTeam >= 0 ? possessingTeam : 0;
            var plan = activePlayPlans[team];
            Debug.Log($"[PLAN] t={matchTimeSeconds:F1} team={team} side={plan.side} planUntil={plan.untilTime:F1} nodes={plan.shortIdx}/{plan.wideIdx}/{plan.forwardIdx}/{plan.overlapIdx}/{plan.cutbackIdx} tri={triangleShortSupportByTeam[team]}/{triangleWideOutletByTeam[team]}/{triangleForwardRunByTeam[team]} lastPass={lastPasserIndex}->{lastReceiverIndex} passT={lastPassTime:F2}");
        }

        if (ballOwnerIndex >= 0)
        {
            var carrierPos = positions[ballOwnerIndex];
            for (var i = 0; i < topPassOptionIndices.Length; i++)
            {
                var candidate = topPassOptionIndices[i];
                if (candidate < 0)
                {
                    continue;
                }

                var color = i == 0 ? Color.green : (i == 1 ? Color.yellow : new Color(1f, 0.5f, 0f, 1f));
                Debug.DrawLine(carrierPos, positions[candidate], color, Time.deltaTime);
            }
        }
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
        var possession = BuildPossessionDescriptor();
        var congested = possessionCongested ? "Y" : "N";
        var activeCall = possessingTeam >= 0 ? activePlayPlans[possessingTeam].call.ToString() : "None";
        hudText.text = $"FantasySport  Team0 {scoreTeam0} : {scoreTeam1} Team1   Time: {minutes:00}:{seconds:00}\nThreat: {threatMeter:00}  Poss: {possession}  Plan: {activeCall}  noProg={noProgressTime:F1} congested={congested}";
    }



    private string BuildPossessionDescriptor()
    {
        if (ballOwnerIndex < 0)
        {
            return "Free";
        }

        var team = teamIdByIndex[ballOwnerIndex] == 0 ? "BLUE" : "ORANGE";
        var shirt = teamLocalIndexByIndex[ballOwnerIndex] + 1;
        var role = roleByIndex[ballOwnerIndex] switch
        {
            RoleGroup.Keeper => "GK",
            RoleGroup.Sweeper => "SWP",
            RoleGroup.Defender => "DEF",
            RoleGroup.Midfielder => "MID",
            RoleGroup.Attacker => "ATT",
            _ => "PLY"
        };
        var lane = laneByIndex[ballOwnerIndex] switch
        {
            Lane.Left => "L",
            Lane.LeftCenter => "LC",
            Lane.Center => "C",
            Lane.RightCenter => "RC",
            Lane.Right => "R",
            _ => "C"
        };
        return $"{team} #{shirt} ({role}-{lane})";
    }

    private void UpdateScoreboardUI(bool force)
    {
        var scoreboardObject = EnsureScoreboardText();
        if (scoreboardText == null)
        {
            scoreboardText = scoreboardObject != null ? scoreboardObject.GetComponent<Text>() : null;
            if (scoreboardText == null)
            {
                if (!scoreboardMissingLogged)
                {
                    scoreboardMissingLogged = true;
                    Debug.LogWarning("[FantasySport] BroadcastCanvas missing; scoreboard unavailable.");
                }
                return;
            }
        }

        if (scoreboardObject != null && !scoreboardObject.activeSelf)
        {
            scoreboardObject.SetActive(true);
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
        var possession = BuildPossessionDescriptor();
        scoreboardText.text = $"BLUE {scoreTeam0} — {scoreTeam1} ORANGE   {minutes:00}:{seconds:00}\nThreat: {threatMeter:00}  Poss: {possession}";
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
        var carrierPos = positions[carrierIndex];
        var carrierProgress = GetTeamProgress(teamId, carrierPos);
        var pressured = FindNearestOpponentDistance(carrierIndex) <= PressureRadius;
        var best = -1;
        var bestScore = float.MinValue;
        var second = -1;
        var secondScore = float.MinValue;
        var third = -1;
        var thirdScore = float.MinValue;
        var activeWingSign = GetAttackWingSign(teamId);
        var oppositeWingSign = -activeWingSign;
        var targetWingY = Mathf.Sign(carrierPos.y) * -GetWingLaneAbs();
        if (Mathf.Abs(targetWingY) < 0.001f)
        {
            targetWingY = -GetWingLaneAbs();
        }

        var centralCongested = CountOpponentsNear(carrierPos, 4.8f, teamId) >= 3;
        var wingThreshold = halfHeight * 0.28f;
        var finishBias = GetFinishBias(teamId, carrierProgress);
        var opponentEndzone = GetEndzoneRect(1 - teamId);
        var plan = activePlayPlans[teamId];
        var planActive = plan.untilTime > matchTimeSeconds;
        var isFinalThird = teamPhase[teamId] == TeamPhase.FinalThird;
        var shortNode = triangleShortSupportByTeam[teamId];
        var wideNode = triangleWideOutletByTeam[teamId];
        var forwardNode = triangleForwardRunByTeam[teamId];

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
            for (var j2 = 0; j2 < TotalAthletes; j2++)
            {
                if (identities[j2].teamId == teamId)
                {
                    continue;
                }

                nearestOpponent = Mathf.Min(nearestOpponent, Vector2.Distance(positions[i], positions[j2]));
            }

            var receiverPredicted = positions[i] + (velocities[i] * GetPassLeadTime(carrierIndex));
            var progress = (positions[i].x - carrierPos.x) * (teamId == 0 ? 1f : -1f);
            var progressGain = GetTeamProgress(teamId, receiverPredicted) - carrierProgress;
            var laneBonus = laneByIndex[i] == laneByIndex[carrierIndex] ? -0.8f : 0.55f;
            var awarenessBonus = profiles[carrierIndex].awareness * 0.8f;
            var stamina01 = GetStamina01(carrierIndex);
            var tiredDistancePenalty = passDist * (1.2f - stamina01) * 0.25f;
            var score = (progress * 1.25f) + (nearestOpponent * 0.9f) + laneBonus + awarenessBonus - (passDist * 0.25f) - tiredDistancePenalty;

            score += progressGain * ProgressWeight;
            if (progressGain < 0.01f && !pressured)
            {
                score -= 3f;
            }

            if (progressGain < -0.04f)
            {
                score -= 8f;
            }

            if (isFinalThird && progressGain < 0f && !pressured)
            {
                continue;
            }

            if (finishBias > 0f)
            {
                if (progressGain < 0f)
                {
                    score -= 999f;
                }

                if (progressGain < 0.03f)
                {
                    score -= 6f;
                }

                if (roleByIndex[i] == RoleGroup.Defender || roleByIndex[i] == RoleGroup.Sweeper)
                {
                    score -= 4f;
                }

                var isCutbackCandidate = Mathf.Abs(carrierPos.y) > wingThreshold && Mathf.Abs(receiverPredicted.y) < halfHeight * 0.22f && progressGain >= 0f && progressGain < 0.10f;
                if (isCutbackCandidate)
                {
                    score += 6f + (3f * finishBias);
                }

                var isCentralAttackerCorridor = roleByIndex[i] == RoleGroup.Attacker && Mathf.Abs(receiverPredicted.y) < halfHeight * 0.16f;
                if (isCentralAttackerCorridor)
                {
                    score += 7f + (3f * finishBias);
                }

                if (opponentEndzone.Contains(receiverPredicted))
                {
                    score += 8f + (4f * finishBias);
                }
            }

            if (i == lastPasserIndex && (matchTimeSeconds - lastPassTime) < 1.6f && teamId == lastPassTeam)
            {
                score -= 999f;
            }

            var centralRecycle = roleByIndex[carrierIndex] == RoleGroup.Midfielder
                && roleByIndex[i] == RoleGroup.Midfielder
                && (laneByIndex[carrierIndex] == Lane.Center || laneByIndex[carrierIndex] == Lane.LeftCenter || laneByIndex[carrierIndex] == Lane.RightCenter)
                && (laneByIndex[i] == Lane.Center || laneByIndex[i] == Lane.LeftCenter || laneByIndex[i] == Lane.RightCenter)
                && progressGain < 0.01f;
            if ((centralCongested || noProgressTime > 1f) && centralRecycle)
            {
                score -= 10f;
            }

            var wingSign = Mathf.Sign(positions[i].y);
            var isWingAnchor = i == idxWingL[teamId] || i == idxWingR[teamId];
            var width01 = Mathf.InverseLerp(GetLaneNarrowY() * 0.7f, GetLaneWideY(), Mathf.Abs(positions[i].y));
            var aheadBonus = progress > 0f ? 1f : -0.8f;
            if (preferWing || centralCongested || noProgressTime > 1f)
            {
                score += (width01 * 2.35f) + aheadBonus;
                if (isWingAnchor)
                {
                    score += (centralCongested || noProgressTime > 1f) ? 12f : 5f;
                }
            }

            if (preferWing)
            {
                var desiredSign = forceSwitch ? oppositeWingSign : activeWingSign;
                var wingMatch = Mathf.Abs(positions[i].y) > wingThreshold && wingSign == desiredSign;
                score += wingMatch ? 3.4f : -2.2f;
            }

            if (Mathf.Abs(receiverPredicted.y) > wingThreshold)
            {
                score += WingBonus;
            }

            if (isWingAnchor && Mathf.Abs(receiverPredicted.y) > wingThreshold)
            {
                score += WingOutletBonus;
            }

            if (centralCongested && forceSwitch)
            {
                score += wingSign == oppositeWingSign ? 2.6f : -1.1f;
            }

            if (forceSwitch)
            {
                score -= Mathf.Abs(receiverPredicted.y - targetWingY) * 0.35f;
            }

            if (nearestOpponent < OpenThreshold * 0.75f)
            {
                score -= 2f;
            }

            if (i == shortNode)
            {
                score += 4.4f;
            }

            if (i == wideNode)
            {
                score += (noProgressTime > 1.2f || forceSwitch || preferWing) ? 10.5f : 6.3f;
            }

            if (i == forwardNode)
            {
                score += (progressGain > 0f ? 9.4f : 4.2f);
            }

            if (possessionTime > 6f && i != wideNode && i != forwardNode && progressGain < 0.02f)
            {
                score -= 8f;
            }

            if (planActive)
            {
                var isPlanNode = i == plan.shortIdx || i == plan.wideIdx || i == plan.forwardIdx || i == plan.overlapIdx || i == plan.cutbackIdx;
                var playBias = plan.call switch
                {
                    PlayCall.BuildOut => 8f,
                    PlayCall.Overlap => 11f,
                    PlayCall.Switch => 14f,
                    PlayCall.FinalThirdCross => 13f,
                    PlayCall.FinalThirdCutback => 13f,
                    _ => 6f
                };

                if (i == plan.shortIdx)
                {
                    score += playBias * 0.75f;
                }

                if (i == plan.wideIdx)
                {
                    score += nearestOpponent > OpenThreshold * 0.9f ? playBias + 4f : playBias;
                }

                if (i == plan.forwardIdx)
                {
                    score += progressGain > 0.02f ? playBias + 3f : playBias * 0.65f;
                }

                if (i == plan.overlapIdx)
                {
                    score += (plan.call == PlayCall.Overlap ? 14f : 9f) + (nearestOpponent > OpenThreshold ? 2f : 0f);
                }

                if (i == plan.cutbackIdx)
                {
                    score += plan.call == PlayCall.FinalThirdCutback ? 15f : 8f;
                }

                if (!pressured && !isPlanNode)
                {
                    score -= playBias * 0.9f;
                }

                if (!pressured && (roleByIndex[i] == RoleGroup.Defender || roleByIndex[i] == RoleGroup.Sweeper) && progressGain < 0.02f)
                {
                    score -= 8f;
                }
            }

            if (score > bestScore)
            {
                third = second;
                thirdScore = secondScore;
                second = best;
                secondScore = bestScore;
                best = i;
                bestScore = score;
            }
            else if (score > secondScore)
            {
                third = second;
                thirdScore = secondScore;
                second = i;
                secondScore = score;
            }
            else if (score > thirdScore)
            {
                third = i;
                thirdScore = score;
            }
        }

        topPassOptionIndices[0] = best;
        topPassOptionIndices[1] = second;
        topPassOptionIndices[2] = third;
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

    private int CountTeammatesNear(Vector2 point, float radius, int teamId)
    {
        var count = 0;
        var radiusSq = radius * radius;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId != teamId)
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

    private float GetTeamProgress(int teamId, Vector2 position)
    {
        return FantasySportTactics.Progress01(position, teamId, halfWidth);
    }

    private void ResolveScrumBreaker(float dt)
    {
        if (ballOwnerIndex < 0 || ballOwnerTeam < 0)
        {
            scrumHoldTimeByTeam[0] = 0f;
            scrumHoldTimeByTeam[1] = 0f;
            return;
        }

        var teamId = ballOwnerTeam;
        var bodiesNear = CountBodiesNearBall(ScrumBreakRadius);
        if (bodiesNear >= ScrumBreakBodiesThreshold)
        {
            scrumHoldTimeByTeam[teamId] += dt;
            if (scrumHoldTimeByTeam[teamId] >= ScrumBreakHoldSeconds && !scrumBreakerArmedByTeam[teamId])
            {
                scrumBreakerArmedByTeam[teamId] = true;
                ExecuteScrumBreaker(teamId, ballOwnerIndex);
                scrumHoldTimeByTeam[teamId] = 0f;
                scrumBreakerArmedByTeam[teamId] = false;
            }
        }
        else
        {
            scrumHoldTimeByTeam[teamId] = 0f;
        }
    }

    private int CountBodiesNearBall(float radius)
    {
        var radiusSq = radius * radius;
        var count = 0;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if ((positions[i] - ballPos).sqrMagnitude <= radiusSq)
            {
                count++;
            }
        }

        return count;
    }

    private void ExecuteScrumBreaker(int teamId, int carrier)
    {
        if (!IsValidAthleteIndex(carrier) || passCooldowns[carrier] > 0f)
        {
            return;
        }

        possessionPlan = "ScrumBreak";
        if (IsGoalkeeper(carrier))
        {
            TryKeeperLongShot(carrier, teamId, positions[carrier], pressured: false, shotClockHot: true);
            passCooldowns[carrier] = PassCooldownSeconds;
            return;
        }

        var outlet = FindBestOpenTeammate(carrier, preferWing: true, forceSwitch: true);
        var target = outlet >= 0 ? positions[outlet] + (velocities[outlet] * GetPassLeadTime(carrier)) : GetFarWingDumpTarget(carrier);
        ThrowBall(carrier, target, outlet, false);
        passCooldowns[carrier] = PassCooldownSeconds;
        var side = target.y >= 0f ? 1 : -1;
        BuildPlayPlan(teamId, side, PlayCall.Switch, useFallback: true);
    }

    private float GetWingLaneAbs()
    {
        return Mathf.Clamp(halfHeight * 0.34f, 4f, halfHeight - 1f);
    }

    private Vector2 GetFarWingDumpTarget(int carrier)
    {
        var carrierPos = positions[carrier];
        var teamId = identities[carrier].teamId;
        var targetWingY = Mathf.Sign(carrierPos.y) * -GetWingLaneAbs();
        if (Mathf.Abs(targetWingY) < 0.001f)
        {
            targetWingY = -GetWingLaneAbs();
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - carrierPos).normalized;
        var targetX = Mathf.Clamp(carrierPos.x + towardGoal.x * 10f, -halfWidth + 2f, halfWidth - 2f);
        return new Vector2(targetX, Mathf.Clamp(targetWingY, -halfHeight + 1f, halfHeight - 1f));
    }

    private Vector2 GetCornerTargetForTeam(int teamId, float wingSign)
    {
        var padding = 1.4f;
        var cornerX = teamId == 0
            ? halfWidth - padding
            : -halfWidth + padding;
        var cornerY = Mathf.Clamp(wingSign * GetLaneWideY(teamId), -halfHeight + 1f, halfHeight - 1f);
        return new Vector2(cornerX, cornerY);
    }

    private Vector2 GetEndzoneBandTarget(int carrier, int teamId)
    {
        var goal = GetOpponentGoalCenter(teamId);
        var ySpread = GetGoalHeight() * 0.42f;
        var jitter = athleteRngs[carrier].Range(-ySpread, ySpread);
        return new Vector2(goal.x, Mathf.Clamp(goal.y + jitter, -halfHeight + 1f, halfHeight - 1f));
    }

    private bool TryThroughBallPass(int carrier, int teamId)
    {
        var runner = FindBestRunnerTarget(carrier, teamId);
        if (runner < 0)
        {
            return false;
        }

        var towardGoal = (GetOpponentGoalCenter(teamId) - positions[carrier]).normalized;
        var target = positions[runner] + (velocities[runner] * GetPassLeadTime(carrier)) + (towardGoal * 2.5f);
        ThrowBall(carrier, target, runner, false);
        return true;
    }

    private int FindBestRunnerTarget(int carrier, int teamId)
    {
        var carrierPos = positions[carrier];
        var carrierProgress = GetTeamProgress(teamId, carrierPos);
        var towardX = teamId == 0 ? 1f : -1f;
        var best = -1;
        var bestScore = float.MinValue;
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (i == carrier || identities[i].teamId != teamId || !IsRunner(i))
            {
                continue;
            }

            var teammatePos = positions[i];
            var ahead = (teammatePos.x - carrierPos.x) * towardX > 1f;
            if (!ahead)
            {
                continue;
            }

            var openEnough = FindNearestOpponentDistance(i) > OpenThreshold + 0.3f;
            if (!openEnough)
            {
                continue;
            }

            var projected = teammatePos + (velocities[i] * GetPassLeadTime(carrier));
            var gain = GetTeamProgress(teamId, projected) - carrierProgress;
            if (gain <= 0f)
            {
                continue;
            }

            var score = (gain * (ProgressWeight + 3f)) + (Mathf.Abs(teammatePos.y) * 0.06f);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private bool IsRunner(int athleteIndex)
    {
        if (IsGoalkeeper(athleteIndex))
        {
            return false;
        }

        var lane = laneByIndex[athleteIndex];
        var role = roleByIndex[athleteIndex];
        return lane == Lane.Left || lane == Lane.Right || (role == RoleGroup.Attacker && lane == Lane.Center) || (role == RoleGroup.Midfielder && lane == Lane.Center);
    }

    private Vector2 ComputeMarkLaneObjective(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        var isPrimaryPresser = athleteIndex == primaryChaserByTeam[teamId] || athleteIndex == secondaryChaserByTeam[teamId];
        if (isPrimaryPresser)
        {
            return ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
        }

        if (markUntil[athleteIndex] < matchTimeSeconds || !IsValidAthleteIndex(markTarget[athleteIndex]) || identities[markTarget[athleteIndex]].teamId == teamId)
        {
            markTarget[athleteIndex] = PickMarkTarget(athleteIndex, teamId);
            markUntil[athleteIndex] = matchTimeSeconds + DefensiveMarkTtl;
        }

        var target = markTarget[athleteIndex];
        if (!IsValidAthleteIndex(target))
        {
            return ComputeHomePosition(athleteIndex);
        }

        var opponentPos = positions[target];
        var goalSideOffset = TowardCenterSign(teamId) * 2.5f;
        var laneOffset = LaneToFloat(laneByIndex[athleteIndex]) * 0.2f;
        var markPos = new Vector2(opponentPos.x - goalSideOffset, opponentPos.y + laneOffset);
        var home = ComputeHomePosition(athleteIndex);
        return Vector2.Lerp(home, markPos, 0.68f);
    }


    private int PickMarkTarget(int athleteIndex, int teamId)
    {
        var bestLane = -1;
        var bestLaneScore = float.MinValue;
        var laneY = LaneToFloat(laneByIndex[athleteIndex]);
        for (var i = 0; i < TotalAthletes; i++)
        {
            if (identities[i].teamId == teamId || IsGoalkeeper(i))
            {
                continue;
            }

            var progress = GetTeamProgress(identities[i].teamId, positions[i]);
            var score = 5f - Mathf.Abs(positions[i].y - laneY) + (progress * 3f);
            if (score > bestLaneScore)
            {
                bestLaneScore = score;
                bestLane = i;
            }
        }

        return bestLane;
    }

    private RoleGroup ResolveRoleForTeamIndex(int teamIndex)
    {
        return teamIndex switch
        {
            0 => RoleGroup.Sweeper,
            1 => RoleGroup.Sweeper,
            2 => RoleGroup.Sweeper,
            3 => RoleGroup.Defender,
            4 => RoleGroup.Defender,
            5 => RoleGroup.Defender,
            6 => RoleGroup.Defender,
            7 => RoleGroup.Midfielder,
            8 => RoleGroup.Midfielder,
            9 => RoleGroup.Midfielder,
            10 => RoleGroup.Midfielder,
            _ => RoleGroup.Attacker
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
        return GetLaneY(lane, halfHeight, 0f, ballOwnerTeam);
    }

    private static int LaneToIndex(Lane lane)
    {
        return lane switch
        {
            Lane.Left => 0,
            Lane.LeftCenter => 1,
            Lane.Center => 2,
            Lane.RightCenter => 3,
            Lane.Right => 4,
            _ => 2
        };
    }

    private bool IsNonEngagedShapePlayer(int athleteIndex)
    {
        if (athleteIndex < 0 || athleteIndex >= TotalAthletes || IsGoalkeeper(athleteIndex))
        {
            return false;
        }

        var teamId = identities[athleteIndex].teamId;
        return athleteIndex != ballOwnerIndex
               && athleteIndex != primaryChaserByTeam[teamId]
               && athleteIndex != secondaryChaserByTeam[teamId]
               && athleteStates[athleteIndex] != AthleteState.PressCarrier
               && athleteStates[athleteIndex] != AthleteState.ChaseFreeBall;
    }

    private float ComputeLaneOccupancyPush(int athleteIndex)
    {
        if (!IsNonEngagedShapePlayer(athleteIndex))
        {
            return 0f;
        }

        var teamId = identities[athleteIndex].teamId;
        var laneIndex = LaneToIndex(laneByIndex[athleteIndex]);
        for (var i = 0; i < laneOccupancyScratch.Length; i++) laneOccupancyScratch[i] = 0;

        var (start, end) = GetTeamSpan(teamId);
        for (var idx = start; idx < end; idx++)
        {
            if (idx == athleteIndex || IsGoalkeeper(idx) || !IsNonEngagedShapePlayer(idx))
            {
                continue;
            }

            laneOccupancyScratch[LaneToIndex(laneByIndex[idx])]++;
        }

        var occupied = laneOccupancyScratch[laneIndex];
        if (occupied <= 4)
        {
            return 0f;
        }

        var nearestUnderfilledLane = laneIndex;
        var bestOffset = int.MaxValue;
        for (var lane = 0; lane < laneOccupancyScratch.Length; lane++)
        {
            if (laneOccupancyScratch[lane] >= 3)
            {
                continue;
            }

            var offset = Mathf.Abs(lane - laneIndex);
            if (offset < bestOffset)
            {
                bestOffset = offset;
                nearestUnderfilledLane = lane;
            }
        }

        if (nearestUnderfilledLane == laneIndex)
        {
            return 0f;
        }

        var toward = nearestUnderfilledLane < laneIndex ? -1f : 1f;
        return toward * Mathf.Min(1.85f, (occupied - 4) * 0.55f);
    }

    private void GetLaneFractions(int teamId, out float wingFrac, out float halfFrac)
    {
        var hasPossession = ballOwnerTeam == teamId;
        var phase = teamId >= 0 && teamId < teamPhase.Length ? teamPhase[teamId] : TeamPhase.Transition;
        halfFrac = 0.24f;

        if (hasPossession && (phase == TeamPhase.Advance || phase == TeamPhase.FinalThird))
        {
            wingFrac = 0.46f;
            return;
        }

        wingFrac = hasPossession ? 0.42f : 0.38f;
    }

    private float GetLaneY(Lane lane, float arenaHalfHeight, float bonus = 0f, int teamId = -1)
    {
        GetLaneFractions(teamId, out var wingFrac, out var halfFrac);
        var margin = 1.4f;
        var wide = (arenaHalfHeight * wingFrac) + bonus;
        var narrow = arenaHalfHeight * halfFrac;
        var wingMax = arenaHalfHeight - margin;
        wide = Mathf.Min(wide, wingMax);
        narrow = Mathf.Min(narrow, wingMax * 0.75f);

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

        return (athleteIndex % PlayersPerTeam) == 1;
    }

    private bool IsSweeperBack(int athleteIndex)
    {
        if (athleteIndex < 0 || athleteIndex >= roleByIndex.Length)
        {
            return false;
        }

        return roleByIndex[athleteIndex] == RoleGroup.Sweeper && !IsTrueKeeper(athleteIndex);
    }

    private int GetGoalkeeperIndex(int teamId)
    {
        var (start, end) = GetTeamSpan(teamId);
        for (var i = start; i < end; i++)
        {
            if (IsGoalkeeper(i))
            {
                return i;
            }
        }

        return (teamId * PlayersPerTeam) + GoalkeeperIndexPerTeam;
    }
    private Color GetTeamColor(int teamId) => teamId == 0 ? Team0Color : Team1Color;
    private float GetOwnBacklineX(int teamId) => teamId == 0 ? -halfWidth : halfWidth;
    private float TowardCenterSign(int teamId) => teamId == 0 ? 1f : -1f;
    private float GetOwnGoalX(int teamId) => GetOwnBacklineX(teamId) + (TowardCenterSign(teamId) * (rules.goalDepth * 0.5f));

    private Rect GetKeeperBox(int teamId)
    {
        var goalMouthHalfH = GetGoalMouthHalfHeight();
        ComputeGoalRects(out var leftRect, out _);
        var depth = Mathf.Max(6f, leftRect.width * 0.8f);
        var height = goalMouthHalfH * 2f * 0.7f;
        return teamId == 0
            ? new Rect(-halfWidth, -height * 0.5f, depth, height)
            : new Rect(halfWidth - depth, -height * 0.5f, depth, height);
    }

    private float GetGoalMouthHalfHeight() => Mathf.Clamp(halfHeight * 0.18f, 3.5f, 5.0f);
    private float GetGoalHeight() => GetGoalMouthHalfHeight() * 2f;
    private float GetEndzoneDepth() => Mathf.Clamp(halfWidth * 0.12f, 5.5f, 8.5f);
    private float GetEndzoneHalfHeight() => GetGoalMouthHalfHeight() * 1.5f;

    private float GetLaneWideY() => GetLaneWideY(ballOwnerTeam);
    private float GetLaneNarrowY() => GetLaneNarrowY(ballOwnerTeam);
    private float GetLaneWideY(int teamId)
    {
        GetLaneFractions(teamId, out var wingFrac, out _);
        return Mathf.Min(halfHeight * wingFrac, halfHeight - 1.4f);
    }

    private float GetLaneNarrowY(int teamId)
    {
        GetLaneFractions(teamId, out _, out var halfFrac);
        return Mathf.Min(halfHeight * halfFrac, (halfHeight - 1.4f) * 0.75f);
    }

    private Rect GetEndzoneRect(int teamId)
    {
        ComputeGoalRects(out var leftRect, out var rightRect);
        return teamId == 0 ? leftRect : rightRect;
    }

    private void ComputeGoalRects(out Rect leftRect, out Rect rightRect)
    {
        var goalMouthHalfH = Mathf.Clamp(halfHeight * 0.18f, 3.5f, 5.0f);
        var endzoneHalfH = goalMouthHalfH * 1.5f;
        var endzoneDepth = Mathf.Clamp(halfWidth * 0.12f, 5.5f, 8.5f);
        leftRect = new Rect(-halfWidth, -endzoneHalfH, endzoneDepth, endzoneHalfH * 2f);
        rightRect = new Rect(halfWidth - endzoneDepth, -endzoneHalfH, endzoneDepth, endzoneHalfH * 2f);
    }

    private Vector2 GetOwnGoalCenter(int teamId) => new Vector2(GetOwnBacklineX(teamId), 0f);
    private Vector2 GetOpponentGoalCenter(int teamId) => new Vector2(GetOwnBacklineX(1 - teamId), 0f);

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
            var allowedXLimit = teamId == 0 ? -0.6f : 0.6f;
            var canCrossCenter = ballOwnerTeam == teamId && ((teamId == 0 && ballPos.x > 0f) || (teamId == 1 && ballPos.x < 0f));
            var vel = velocities[athleteIndex];
            if (!canCrossCenter)
            {
                const float leash = 0.18f;
                if (teamId == 0 && p.x > allowedXLimit)
                {
                    p.x = Mathf.Lerp(p.x, allowedXLimit, leash);
                    if (vel.x > 0f)
                    {
                        vel.x = 0f;
                    }
                }
                else if (teamId == 1 && p.x < allowedXLimit)
                {
                    p.x = Mathf.Lerp(p.x, allowedXLimit, leash);
                    if (vel.x < 0f)
                    {
                        vel.x = 0f;
                    }
                }
            }

            velocities[athleteIndex] = vel;
        }

        var minX = -halfWidth + AthleteBoundsMargin;
        var maxX = halfWidth - AthleteBoundsMargin;
        var minY = -halfHeight + AthleteBoundsMargin;
        var maxY = halfHeight - AthleteBoundsMargin;

        var clampedX = false;
        var clampedY = false;
        if (p.x < minX || p.x > maxX)
        {
            p.x = Mathf.Clamp(p.x, minX, maxX);
            clampedX = true;
        }

        if (p.y < minY || p.y > maxY)
        {
            p.y = Mathf.Clamp(p.y, minY, maxY);
            clampedY = true;
        }

        var v = velocities[athleteIndex];
        if (clampedX)
        {
            v.x *= -0.25f;
        }

        if (clampedY)
        {
            v.y *= -0.25f;
        }

        if (Mathf.Abs(p.x) > halfWidth - AthleteEdgeRecoveryBand)
        {
            v.x += p.x > 0f ? -AthleteEdgePushStrength : AthleteEdgePushStrength;
        }

        if (Mathf.Abs(p.y) > halfHeight - AthleteEdgeRecoveryBand)
        {
            v.y += p.y > 0f ? -AthleteEdgePushStrength : AthleteEdgePushStrength;
        }

        velocities[athleteIndex] = v;
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
        scoreboardText = EnsureScoreboardText()?.GetComponent<Text>();
    }

    private GameObject EnsureScoreboardText()
    {
        var canvasObject = GameObject.Find("BroadcastCanvas");
        if (canvasObject == null)
        {
            return null;
        }

        var scoreboard = canvasObject.transform.Find("ScoreboardText")?.gameObject;
        if (scoreboard == null)
        {
            scoreboard = new GameObject("ScoreboardText", typeof(RectTransform), typeof(Text));
            scoreboard.transform.SetParent(canvasObject.transform, false);

            var rect = scoreboard.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(760f, 58f);

            var text = scoreboard.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        return scoreboard;
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

    private void RefreshGoalVisuals()
    {
        if (sceneGraph?.WorldObjectsRoot == null)
        {
            return;
        }

        var root = sceneGraph.WorldObjectsRoot.Find("FantasySportHazards");
        if (root == null)
        {
            return;
        }

        var existingLeft = root.Find("Endzone_Left");
        if (existingLeft != null)
        {
            Destroy(existingLeft.gameObject);
        }

        var existingRight = root.Find("Endzone_Right");
        if (existingRight != null)
        {
            Destroy(existingRight.gameObject);
        }

        ComputeGoalRects(out var leftRect, out var rightRect);
        BuildEndzoneVisual(root, 0, leftRect);
        BuildEndzoneVisual(root, 1, rightRect);
    }

    private void BuildEndzoneVisual(Transform parent, int teamId, Rect rect)
    {
        var zone = new GameObject(teamId == 0 ? "Endzone_Left" : "Endzone_Right");
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = rect.center;
        zone.transform.localScale = Vector3.one;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(zone.transform, false);
        fillGo.transform.localPosition = Vector3.zero;
        fillGo.transform.localScale = new Vector3(rect.width, rect.height, 1f);

        var fill = fillGo.AddComponent<SpriteRenderer>();
        fill.sprite = GetWhitePixelSprite();
        fill.color = teamId == 0
            ? new Color(0.36f, 0.42f, 0.95f, 0.12f)
            : new Color(0.58f, 0.33f, 0.92f, 0.12f);
        RenderOrder.Apply(fill, RenderOrder.WorldDeco - 1);

        BuildGoalRectOutline(zone.transform, rect);
        if (showDebugGoalRects)
        {
            BuildGoalRectCornerDots(zone.transform, rect);
        }
    }

    private void BuildGoalRectOutline(Transform parent, Rect rect)
    {
        const float thickness = 0.2f;
        CreateGoalRectStrip(parent, "Top", new Vector2(0f, (rect.height * 0.5f) - (thickness * 0.5f)), new Vector2(rect.width, thickness));
        CreateGoalRectStrip(parent, "Bottom", new Vector2(0f, (-rect.height * 0.5f) + (thickness * 0.5f)), new Vector2(rect.width, thickness));
        CreateGoalRectStrip(parent, "Left", new Vector2((-rect.width * 0.5f) + (thickness * 0.5f), 0f), new Vector2(thickness, rect.height));
        CreateGoalRectStrip(parent, "Right", new Vector2((rect.width * 0.5f) - (thickness * 0.5f), 0f), new Vector2(thickness, rect.height));
    }

    private void CreateGoalRectStrip(Transform parent, string name, Vector2 localPos, Vector2 scale)
    {
        var strip = new GameObject($"GoalRect_{name}").AddComponent<SpriteRenderer>();
        strip.transform.SetParent(parent, false);
        strip.transform.localPosition = localPos;
        strip.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        strip.sprite = GetWhitePixelSprite();
        strip.color = new Color(1f, 1f, 1f, 0.72f);
        RenderOrder.Apply(strip, RenderOrder.WorldDeco + 1);
    }

    private void BuildGoalRectCornerDots(Transform parent, Rect rect)
    {
        CreateGoalRectCornerDot(parent, new Vector2(-rect.width * 0.5f, -rect.height * 0.5f));
        CreateGoalRectCornerDot(parent, new Vector2(-rect.width * 0.5f, rect.height * 0.5f));
        CreateGoalRectCornerDot(parent, new Vector2(rect.width * 0.5f, -rect.height * 0.5f));
        CreateGoalRectCornerDot(parent, new Vector2(rect.width * 0.5f, rect.height * 0.5f));
    }

    private void CreateGoalRectCornerDot(Transform parent, Vector2 localPos)
    {
        var dot = new GameObject("GoalRectCornerDot").AddComponent<SpriteRenderer>();
        dot.transform.SetParent(parent, false);
        dot.transform.localPosition = localPos;
        dot.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
        dot.sprite = GetWhitePixelSprite();
        dot.color = new Color(1f, 0.95f, 0.25f, 0.95f);
        RenderOrder.Apply(dot, RenderOrder.WorldDeco + 2);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogGoalRectsForDebug()
    {
        if (goalRectsDebugLogged)
        {
            return;
        }

        goalRectsDebugLogged = true;
        ComputeGoalRects(out var leftRect, out var rightRect);
        Debug.Log($"[FantasySport] GoalRects L={leftRect} R={rightRect}");
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

        var hasIntent = athleteIndex < intentUntilTime.Length && matchTimeSeconds < intentUntilTime[athleteIndex];
        return !hasIntent && !IsImmediateTackler(athleteIndex) && !IsImmediateInterceptor(athleteIndex) && !IsGoalkeeperEngaged(athleteIndex);
    }

    private Vector2 ComputePatrolJitter(int athleteIndex)
    {
        var angle = (currentTickIndex * 0.02f) + (athleteIndex * 1.7f);
        var jitterRadius = Mathf.Lerp(0.6f, 1.0f, (athleteIndex % 5) / 4f);
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

    private float GetTopBandY() => halfHeight * 0.44f;
    private float GetBottomBandY() => -GetTopBandY();
    private bool IsBandAnchor(int teamId, int athleteIndex)
    {
        if (athleteIndex < 0 || athleteIndex >= TotalAthletes || IsGoalkeeper(athleteIndex))
        {
            return false;
        }

        return athleteIndex == idxWingL[teamId] || athleteIndex == idxWingR[teamId];
    }

    private bool ShouldApplyWidthAnchor(int teamId, int athleteIndex)
    {
        if (athleteIndex < 0 || athleteIndex >= TotalAthletes || IsGoalkeeper(athleteIndex) || athleteIndex == ballOwnerIndex)
        {
            return false;
        }

        if (!IsBandAnchor(teamId, athleteIndex))
        {
            return false;
        }

        return ballOwnerTeam == teamId;
    }

    private float GetWidthAnchorTargetY(int teamId, int athleteIndex)
    {
        if (athleteIndex == idxWingL[teamId])
        {
            return GetBottomBandY();
        }

        if (athleteIndex == idxWingR[teamId])
        {
            return GetTopBandY();
        }

        var lane = laneByIndex[athleteIndex];
        var wantsBottom = lane == Lane.Left || lane == Lane.LeftCenter;
        return wantsBottom ? GetBottomBandY() : GetTopBandY();
    }

    private float GetMoreOpenBandY(int teamId, int fromAthlete)
    {
        var topY = GetTopBandY();
        var bottomY = GetBottomBandY();
        var topPressure = CountOpponentsNear(new Vector2(positions[fromAthlete].x, topY), 4f, teamId);
        var bottomPressure = CountOpponentsNear(new Vector2(positions[fromAthlete].x, bottomY), 4f, teamId);
        return topPressure <= bottomPressure ? topY : bottomY;
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
        var initialOrtho = Mathf.Max(halfHeight + 2f, 10f);
        var arenaCameraPolicy = Object.FindAnyObjectByType<ArenaCameraPolicy>();
        if (arenaCameraPolicy != null && arenaCameraPolicy.targetCamera == cameraComponent)
        {
            arenaCameraPolicy.SetOrthoFromExternal(initialOrtho, "FantasySportRunner.EnsureMainCamera", syncZoomLevel: true);
        }
        else
        {
            cameraComponent.orthographicSize = initialOrtho;
        }

        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void EnsureArenaBoundsAndCameraFit()
    {
        if (sceneGraph?.WorldRoot == null)
        {
            return;
        }

        var boundsTransform = sceneGraph.WorldRoot.Find("ArenaBounds");
        if (boundsTransform == null)
        {
            var boundsObject = new GameObject("ArenaBounds");
            boundsObject.transform.SetParent(sceneGraph.WorldRoot, false);
            boundsObject.transform.localPosition = Vector3.zero;
            boundsTransform = boundsObject.transform;
        }

        var boundsCollider = boundsTransform.GetComponent<BoxCollider2D>();
        if (boundsCollider == null)
        {
            boundsCollider = boundsTransform.gameObject.AddComponent<BoxCollider2D>();
        }

        boundsCollider.isTrigger = true;
        boundsCollider.offset = Vector2.zero;
        boundsCollider.size = new Vector2(
            (halfWidth + ArenaBoundsMargin) * 2f,
            (halfHeight + ArenaBoundsMargin) * 2f);

        var policy = Object.FindAnyObjectByType<ArenaCameraPolicy>();
        if (policy != null)
        {
            policy.BindArenaBounds(boundsCollider, fitToBounds: true);
        }

        var followController = Object.FindAnyObjectByType<CameraFollowController>();
        if (followController != null)
        {
            followController.arenaCameraPolicy = policy;
        }
    }
}
