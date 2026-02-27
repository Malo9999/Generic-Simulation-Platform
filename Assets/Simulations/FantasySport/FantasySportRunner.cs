using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int AthleteCount = 10;
    private const int SpawnDebugCount = 5;
    private const float LaneSpacing = 6f;
    private const float SeparationRadius = 2.25f;
    private const float BoundaryAvoidanceDistance = 3f;
    private const float PressureRadius = 2f;
    private const float DenyRadius = 2.25f;
    private const float PassLeadTime = 0.28f;
    private const float PassSpeed = 15f;
    private const float ShootSpeed = 22f;
    private const float ShootRange = 14f;
    private const float AssignmentTtlSeconds = 0.75f;
    private const float SeekWeight = 1f;
    private const float SeparationWeight = 1.45f;
    private const float BoundaryWeight = 1.15f;

    private enum AthleteState
    {
        BallCarrier,
        ChaseFreeBall,
        SupportAttack,
        PressCarrier,
        MarkThreat,
        GuardGoal
    }

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private bool showDebugPlayerState;
    [SerializeField] private FantasySportRules rules = new FantasySportRules();
    [SerializeField] private float athleteIconScaleMultiplier = 1.3f;

    private Transform[] athletes;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] stunTimers;
    private float[] tackleCooldowns;
    private float[] laneByPlayer;
    private IRng[] athleteRngs;
    private AthleteState[] athleteStates;
    private int[] assignedTargetIndex;
    private float[] assignmentTtl;
    private TextMesh[] debugStateLabels;
    private int previousPossessionTeam = int.MinValue;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private SpriteRenderer[] possessionRings;

    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private SimulationSceneGraph sceneGraph;
    private int nextEntityId;

    private Transform ballTransform;
    private Vector2 ballPos;
    private Vector2 ballVel;
    private int ballOwnerIndex = -1;
    private int ballOwnerTeam = -1;

    private float elapsedMatchTime;
    private bool matchFinished;
    private int scoreTeam0;
    private int scoreTeam1;

    private Text hudText;
    private int lastHudSecond = -1;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "FantasySport");
        EnsureMainCamera();
        BuildAthletes(config);
        EnsureBall();
        FindHudText();
        ResetMatchState();
        ResetKickoff();
        UpdateHud(force: true);
        Debug.Log($"{nameof(FantasySportRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
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
            if (athletes[i] == null)
            {
                continue;
            }

            stunTimers[i] = Mathf.Max(0f, stunTimers[i] - dt);
            tackleCooldowns[i] = Mathf.Max(0f, tackleCooldowns[i] - dt);
        }

        UpdateAthletes(dt);
        ResolveTackleEvents();
        UpdateBall(dt);
        ResolvePickup();
        ResolveGoal(tickIndex);
        ApplyTransforms(dt);
        UpdateHud(force: false);
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

        athletes = null;
        identities = null;
        positions = null;
        velocities = null;
        stunTimers = null;
        tackleCooldowns = null;
        laneByPlayer = null;
        athleteRngs = null;
        athleteStates = null;
        assignedTargetIndex = null;
        assignmentTtl = null;
        debugStateLabels = null;
        previousPossessionTeam = int.MinValue;
        pipelineRenderers = null;
        visualKeys = null;
        possessionRings = null;
        hudText = null;
        lastHudSecond = -1;
        Debug.Log("FantasySportRunner Shutdown");
    }

    private void BuildAthletes(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64f) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64f) * 0.5f);

        athletes = new Transform[AthleteCount];
        identities = new EntityIdentity[AthleteCount];
        positions = new Vector2[AthleteCount];
        velocities = new Vector2[AthleteCount];
        stunTimers = new float[AthleteCount];
        tackleCooldowns = new float[AthleteCount];
        laneByPlayer = new float[AthleteCount];
        athleteRngs = new IRng[AthleteCount];
        athleteStates = new AthleteState[AthleteCount];
        assignedTargetIndex = new int[AthleteCount];
        assignmentTtl = new float[AthleteCount];
        debugStateLabels = new TextMesh[AthleteCount];
        pipelineRenderers = new GameObject[AthleteCount];
        visualKeys = new VisualKey[AthleteCount];
        possessionRings = new SpriteRenderer[AthleteCount];

        ResolveArtPipeline();

        for (var i = 0; i < AthleteCount; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{i}");
            laneByPlayer[i] = ResolveLaneForSpawnIndex(i);
            assignedTargetIndex[i] = -1;
            assignmentTtl[i] = 0f;
        }

        var rng = RngService.Fork("SIM:FantasySport:SPAWN");
        var teamSize = AthleteCount / 2;

        for (var i = 0; i < AthleteCount; i++)
        {
            var teamId = i < teamSize ? 0 : 1;
            var teamIndex = i % teamSize;
            var role = teamIndex <= 2 ? "offense" : "defense";

            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: teamId,
                role: role,
                variantCount: 3,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "FantasySport");

            var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, identity.teamId);

            var athlete = new GameObject($"Sim_{identity.entityId:0000}");
            athlete.transform.SetParent(groupRoot, false);

            var visualKey = VisualKeyBuilder.Create(
                simulationId: "FantasySport",
                entityType: "athlete",
                instanceId: identity.entityId,
                kind: string.IsNullOrWhiteSpace(identity.role) ? "athlete" : identity.role,
                state: "run",
                facingMode: FacingMode.Auto,
                groupId: identity.teamId);

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

            var possessionRing = new GameObject("PossessionRing");
            possessionRing.transform.SetParent(athlete.transform, false);
            possessionRing.transform.localPosition = Vector3.zero;
            possessionRing.transform.localScale = Vector3.one * 1.05f;

            var possessionRenderer = possessionRing.AddComponent<SpriteRenderer>();
            possessionRenderer.sprite = PrimitiveSpriteLibrary.CircleOutline();
            possessionRenderer.color = identity.teamId == 0
                ? new Color(0.25f, 0.86f, 1f, 0.95f)
                : new Color(1f, 0.74f, 0.22f, 0.95f);
            possessionRenderer.enabled = false;
            RenderOrder.Apply(possessionRenderer, RenderOrder.SelectionRing);
            possessionRings[i] = possessionRenderer;

            var debugLabelObject = new GameObject("DebugState");
            debugLabelObject.transform.SetParent(athlete.transform, false);
            debugLabelObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            var debugText = debugLabelObject.AddComponent<TextMesh>();
            debugText.characterSize = 0.1f;
            debugText.fontSize = 64;
            debugText.anchor = TextAnchor.MiddleCenter;
            debugText.alignment = TextAlignment.Center;
            debugText.color = new Color(1f, 1f, 1f, 0.9f);
            debugText.text = string.Empty;
            debugText.gameObject.SetActive(showDebugPlayerState);
            debugStateLabels[i] = debugText;

            positions[i] = Vector2.zero;
            velocities[i] = Vector2.zero;

            athlete.transform.localPosition = Vector3.zero;
            athlete.transform.localScale = Vector3.one * rng.Range(0.95f, 1.1f);
            athletes[i] = athlete.transform;
            identities[i] = identity;
            visualKeys[i] = visualKey;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(FantasySportRunner)} spawn[{i}] {identity}");
            }
        }

        ResetAthleteFormationAndVelocities();
    }

    private void ResolveArtPipeline()
    {
        artSelector = UnityEngine.Object.FindFirstObjectByType<ArtModeSelector>()
            ?? UnityEngine.Object.FindAnyObjectByType<ArtModeSelector>();

        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
        if (activePipeline != null)
        {
            Debug.Log($"{nameof(FantasySportRunner)} using art pipeline '{activePipeline.DisplayName}' ({activePipeline.Mode}).");
            return;
        }

        Debug.Log($"{nameof(FantasySportRunner)} no {nameof(ArtModeSelector)} / active pipeline found; using default athlete renderers.");
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

        var ballFillRenderer = ballObject.AddComponent<SpriteRenderer>();
        ballFillRenderer.sprite = PrimitiveSpriteLibrary.CircleFill();
        ballFillRenderer.color = new Color(0.97f, 0.97f, 0.94f, 1f);
        RenderOrder.Apply(ballFillRenderer, RenderOrder.EntityBody + 1);

        var outline = new GameObject("Outline");
        outline.transform.SetParent(ballObject.transform, false);
        outline.transform.localPosition = Vector3.zero;
        outline.transform.localScale = Vector3.one;
        var ballOutlineRenderer = outline.AddComponent<SpriteRenderer>();
        ballOutlineRenderer.sprite = PrimitiveSpriteLibrary.CircleOutline();
        ballOutlineRenderer.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
        RenderOrder.Apply(ballOutlineRenderer, RenderOrder.EntityBody + 2);

        var highlight = new GameObject("Highlight");
        highlight.transform.SetParent(ballObject.transform, false);
        highlight.transform.localPosition = new Vector3(-0.16f, 0.17f, 0f);
        highlight.transform.localScale = Vector3.one * 0.28f;
        var ballHighlightRenderer = highlight.AddComponent<SpriteRenderer>();
        ballHighlightRenderer.sprite = PrimitiveSpriteLibrary.CircleFill();
        ballHighlightRenderer.color = new Color(1f, 1f, 1f, 0.35f);
        RenderOrder.Apply(ballHighlightRenderer, RenderOrder.EntityBody + 3);

        ballTransform = ballObject.transform;
    }

    private void FindHudText()
    {
        var hudObject = GameObject.Find("HUDText");
        hudText = hudObject != null ? hudObject.GetComponent<Text>() : null;
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
        previousPossessionTeam = int.MinValue;
    }

    private void ResetKickoff()
    {
        if (athletes == null)
        {
            return;
        }

        ResetAthleteFormationAndVelocities();

        ballPos = Vector2.zero;
        ballVel = Vector2.zero;
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
    }

    private void ResetAthleteFormationAndVelocities()
    {
        var margin = 4f;
        var ySpacing = Mathf.Clamp(halfHeight * 0.35f, 3f, 10f);
        var teamSize = AthleteCount / 2;

        for (var i = 0; i < athletes.Length; i++)
        {
            var teamId = identities[i].teamId;
            var teamIndex = i % teamSize;
            var sign = teamId == 0 ? -1f : 1f;
            var offenseX = sign * (halfWidth * 0.25f);
            var defenseX = sign * (halfWidth * 0.55f);

            var isOffense = teamIndex <= 2;
            var x = isOffense ? offenseX : defenseX;
            var y = 0f;

            if (isOffense)
            {
                y = teamIndex switch
                {
                    0 => -ySpacing,
                    1 => 0f,
                    _ => ySpacing
                };
            }
            else
            {
                y = teamIndex == 3 ? -ySpacing * 0.5f : ySpacing * 0.5f;
            }

            x = Mathf.Clamp(x, -halfWidth + margin, halfWidth - margin);
            y = Mathf.Clamp(y, -halfHeight + margin, halfHeight - margin);

            positions[i] = new Vector2(x, y);

            var baseDirection = teamId == 0 ? Vector2.right : Vector2.left;
            var rng = RngService.Fork($"SIM:FantasySport:SPAWNVEL:{i}");
            var jitter = rng.Range(-0.35f, 0.35f);
            var moveDirection = Quaternion.Euler(0f, 0f, jitter * Mathf.Rad2Deg) * new Vector3(baseDirection.x, baseDirection.y, 0f);
            var speed = rng.Range(4f, 6f);
            velocities[i] = new Vector2(moveDirection.x, moveDirection.y).normalized * speed;

            stunTimers[i] = 0f;
            tackleCooldowns[i] = 0f;
            assignedTargetIndex[i] = -1;
            assignmentTtl[i] = 0f;
            athleteStates[i] = AthleteState.GuardGoal;
        }
    }

    private void UpdateAthletes(float dt)
    {
        RefreshAssignments(dt);
        ResolveBallCarrierDecision();

        for (var i = 0; i < athletes.Length; i++)
        {
            if (athletes[i] == null)
            {
                continue;
            }

            var desired = Vector2.zero;
            if (!matchFinished && stunTimers[i] <= 0f)
            {
                desired = ComputeDesiredVelocity(i);
            }

            velocities[i] = Vector2.MoveTowards(velocities[i], desired, rules.accel * dt);

            if (stunTimers[i] > 0f)
            {
                velocities[i] *= 0.9f;
            }

            positions[i] += velocities[i] * dt;
            ClampAthlete(i);
        }
    }

    private Vector2 ComputeDesiredVelocity(int athleteIndex)
    {
        var identity = identities[athleteIndex];
        var maxSpeed = identity.role == "defense" ? rules.athleteSpeedDefense : rules.athleteSpeedOffense;
        var athletePos = positions[athleteIndex];
        var target = ComputeHomePosition(athleteIndex);

        var ownGoal = GetOwnGoalCenter(identity.teamId);
        var enemyGoal = GetOpponentGoalCenter(identity.teamId);
        var assignedTarget = assignedTargetIndex[athleteIndex];
        var hasAssignedTarget = assignedTarget >= 0 && assignedTarget < AthleteCount;

        switch (athleteStates[athleteIndex])
        {
            case AthleteState.BallCarrier:
                target = enemyGoal;
                break;
            case AthleteState.ChaseFreeBall:
                target = ballPos;
                break;
            case AthleteState.SupportAttack:
                target = Vector2.Lerp(ComputeHomePosition(athleteIndex), ballPos, 0.22f);
                break;
            case AthleteState.PressCarrier:
                target = ballOwnerIndex >= 0 ? positions[ballOwnerIndex] : ballPos;
                break;
            case AthleteState.MarkThreat:
                if (hasAssignedTarget)
                {
                    var threatPos = positions[assignedTarget];
                    target = Vector2.Lerp(threatPos, ownGoal, 0.15f);
                }
                else
                {
                    target = Vector2.Lerp(ballPos, ownGoal, 0.55f);
                }

                break;
            case AthleteState.GuardGoal:
                target = Vector2.Lerp(ballPos, ownGoal, 0.65f);
                break;
        }

        if (athleteStates[athleteIndex] == AthleteState.PressCarrier)
        {
            maxSpeed *= 1.08f;
        }

        return BuildSteeringVelocity(athleteIndex, athletePos, target, maxSpeed);
    }

    private void RefreshAssignments(float dt)
    {
        var possessionChanged = previousPossessionTeam != ballOwnerTeam;
        previousPossessionTeam = ballOwnerTeam;

        for (var i = 0; i < AthleteCount; i++)
        {
            assignmentTtl[i] = Mathf.Max(0f, assignmentTtl[i] - dt);

            if (possessionChanged)
            {
                assignmentTtl[i] = 0f;
                assignedTargetIndex[i] = -1;
            }
        }

        var chaserTeam0 = FindClosestPlayerToPoint(0, ballPos);
        var chaserTeam1 = FindClosestPlayerToPoint(1, ballPos);

        var pressTeam0 = -1;
        var shadowTeam0 = -1;
        var pressTeam1 = -1;
        var shadowTeam1 = -1;

        if (ballOwnerIndex >= 0)
        {
            if (ballOwnerTeam == 0)
            {
                FindClosestAndSecondClosestToTarget(teamId: 1, targetIndex: ballOwnerIndex, out pressTeam1, out shadowTeam1);
            }
            else
            {
                FindClosestAndSecondClosestToTarget(teamId: 0, targetIndex: ballOwnerIndex, out pressTeam0, out shadowTeam0);
            }
        }

        for (var i = 0; i < AthleteCount; i++)
        {
            var teamId = identities[i].teamId;
            var role = identities[i].role;
            AthleteState nextState;

            if (i == ballOwnerIndex)
            {
                nextState = AthleteState.BallCarrier;
            }
            else if (ballOwnerIndex < 0)
            {
                var teamChaser = teamId == 0 ? chaserTeam0 : chaserTeam1;
                nextState = i == teamChaser ? AthleteState.ChaseFreeBall : (role == "offense" ? AthleteState.SupportAttack : AthleteState.GuardGoal);
            }
            else if (ballOwnerTeam == teamId)
            {
                nextState = role == "offense" ? AthleteState.SupportAttack : AthleteState.GuardGoal;
            }
            else
            {
                var press = teamId == 0 ? pressTeam0 : pressTeam1;
                var shadow = teamId == 0 ? shadowTeam0 : shadowTeam1;
                if (i == press)
                {
                    nextState = AthleteState.PressCarrier;
                }
                else if (i == shadow || role == "defense")
                {
                    nextState = AthleteState.MarkThreat;
                }
                else
                {
                    nextState = AthleteState.GuardGoal;
                }
            }

            athleteStates[i] = nextState;

            if (nextState == AthleteState.MarkThreat)
            {
                var currentTargetValid = assignedTargetIndex[i] >= 0
                    && assignedTargetIndex[i] < AthleteCount
                    && identities[assignedTargetIndex[i]].teamId != teamId;

                if (!currentTargetValid || assignmentTtl[i] <= 0f)
                {
                    assignedTargetIndex[i] = FindBestThreatToMark(i);
                    assignmentTtl[i] = AssignmentTtlSeconds;
                }
            }
            else if (nextState == AthleteState.PressCarrier)
            {
                assignedTargetIndex[i] = ballOwnerIndex;
                assignmentTtl[i] = AssignmentTtlSeconds;
            }
            else
            {
                assignedTargetIndex[i] = -1;
            }
        }
    }

    private void ResolveBallCarrierDecision()
    {
        if (ballOwnerIndex < 0 || matchFinished)
        {
            return;
        }

        var carrier = ballOwnerIndex;
        if (carrier < 0 || carrier >= AthleteCount || stunTimers[carrier] > 0f)
        {
            return;
        }

        var teamId = identities[carrier].teamId;
        var carrierPos = positions[carrier];
        var enemyGoal = GetOpponentGoalCenter(teamId);
        var nearestOpponentDist = FindNearestOpponentDistance(carrier);
        var pressured = nearestOpponentDist <= PressureRadius;

        var openTeammate = FindBestOpenTeammate(carrier);
        if (pressured && openTeammate >= 0)
        {
            var predicted = positions[openTeammate] + (velocities[openTeammate] * PassLeadTime);
            var dir = predicted - carrierPos;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = enemyGoal - carrierPos;
            }

            dir.Normalize();
            ballOwnerIndex = -1;
            ballOwnerTeam = -1;
            ballPos = carrierPos;
            ballVel = (dir * PassSpeed) + (velocities[carrier] * 0.2f);
            previousPossessionTeam = int.MinValue;
            return;
        }

        var toGoal = enemyGoal - carrierPos;
        var inShootRange = toGoal.magnitude <= ShootRange;
        var facingGoal = velocities[carrier].sqrMagnitude < 0.01f
            || Vector2.Dot(velocities[carrier].normalized, toGoal.normalized) > 0.35f;

        if (inShootRange && facingGoal)
        {
            var dir = toGoal.sqrMagnitude < 0.0001f ? Vector2.right : toGoal.normalized;
            ballOwnerIndex = -1;
            ballOwnerTeam = -1;
            ballPos = carrierPos;
            ballVel = (dir * ShootSpeed) + (velocities[carrier] * 0.15f);
            previousPossessionTeam = int.MinValue;
        }
    }

    private Vector2 ComputeHomePosition(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        var role = identities[athleteIndex].role;
        var attackDir = teamId == 0 ? 1f : -1f;
        var ownGoalX = teamId == 0 ? -halfWidth : halfWidth;
        var laneY = Mathf.Clamp(laneByPlayer[athleteIndex] * LaneSpacing, -halfHeight + 2f, halfHeight - 2f);

        float homeX;
        if (ballOwnerTeam == teamId)
        {
            homeX = attackDir * (halfWidth * (role == "offense" ? 0.22f : 0.1f));
        }
        else if (ballOwnerTeam >= 0)
        {
            homeX = Mathf.Lerp(ownGoalX, ballPos.x, role == "defense" ? 0.35f : 0.28f);
        }
        else
        {
            homeX = attackDir * (halfWidth * (role == "offense" ? 0.08f : 0.15f));
        }

        homeX = Mathf.Clamp(homeX, -halfWidth + 2f, halfWidth - 2f);
        return new Vector2(homeX, laneY);
    }

    private Vector2 BuildSteeringVelocity(int athleteIndex, Vector2 athletePos, Vector2 target, float maxSpeed)
    {
        var seek = target - athletePos;
        if (seek.sqrMagnitude > 0.0001f)
        {
            seek.Normalize();
        }

        var separation = Vector2.zero;
        for (var i = 0; i < AthleteCount; i++)
        {
            if (i == athleteIndex || identities[i].teamId != identities[athleteIndex].teamId)
            {
                continue;
            }

            var delta = athletePos - positions[i];
            var dist = delta.magnitude;
            if (dist < 0.001f || dist > SeparationRadius)
            {
                continue;
            }

            var closeness = 1f - (dist / SeparationRadius);
            separation += (delta / dist) * closeness;
        }

        var boundary = Vector2.zero;
        if (athletePos.x < -halfWidth + BoundaryAvoidanceDistance)
        {
            boundary.x += 1f - Mathf.InverseLerp(-halfWidth, -halfWidth + BoundaryAvoidanceDistance, athletePos.x);
        }
        else if (athletePos.x > halfWidth - BoundaryAvoidanceDistance)
        {
            boundary.x -= 1f - Mathf.InverseLerp(halfWidth, halfWidth - BoundaryAvoidanceDistance, athletePos.x);
        }

        if (athletePos.y < -halfHeight + BoundaryAvoidanceDistance)
        {
            boundary.y += 1f - Mathf.InverseLerp(-halfHeight, -halfHeight + BoundaryAvoidanceDistance, athletePos.y);
        }
        else if (athletePos.y > halfHeight - BoundaryAvoidanceDistance)
        {
            boundary.y -= 1f - Mathf.InverseLerp(halfHeight, halfHeight - BoundaryAvoidanceDistance, athletePos.y);
        }

        var steering = (seek * SeekWeight) + (separation * SeparationWeight) + (boundary * BoundaryWeight);
        if (steering.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return steering.normalized * maxSpeed;
    }

    private float ResolveLaneForSpawnIndex(int athleteIndex)
    {
        var teamSize = AthleteCount / 2;
        var teamIndex = athleteIndex % teamSize;

        if (teamIndex <= 2)
        {
            return teamIndex switch
            {
                0 => -1f,
                1 => 0f,
                _ => 1f
            };
        }

        return teamIndex == 3 ? -0.5f : 0.5f;
    }

    private int FindClosestPlayerToPoint(int teamId, Vector2 point)
    {
        var bestIndex = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < AthleteCount; i++)
        {
            if (identities[i].teamId != teamId || stunTimers[i] > 0f)
            {
                continue;
            }

            var dist = (positions[i] - point).sqrMagnitude;
            if (dist < bestDist || (Mathf.Approximately(dist, bestDist) && i < bestIndex))
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void FindClosestAndSecondClosestToTarget(int teamId, int targetIndex, out int closest, out int second)
    {
        closest = -1;
        second = -1;
        var best = float.MaxValue;
        var secondBest = float.MaxValue;
        var targetPos = positions[targetIndex];

        for (var i = 0; i < AthleteCount; i++)
        {
            if (identities[i].teamId != teamId || stunTimers[i] > 0f)
            {
                continue;
            }

            var dist = (positions[i] - targetPos).sqrMagnitude;
            if (dist < best || (Mathf.Approximately(dist, best) && i < closest))
            {
                secondBest = best;
                second = closest;
                best = dist;
                closest = i;
            }
            else if (dist < secondBest || (Mathf.Approximately(dist, secondBest) && i < second))
            {
                secondBest = dist;
                second = i;
            }
        }
    }

    private int FindBestThreatToMark(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        var ownGoal = GetOwnGoalCenter(teamId);
        var bestIndex = -1;
        var bestScore = float.MaxValue;

        for (var i = 0; i < AthleteCount; i++)
        {
            if (identities[i].teamId == teamId)
            {
                continue;
            }

            var distToMarker = Vector2.Distance(positions[athleteIndex], positions[i]);
            var distToGoal = Vector2.Distance(positions[i], ownGoal);
            var score = distToMarker + (distToGoal * 0.45f);
            if (score < bestScore || (Mathf.Approximately(score, bestScore) && i < bestIndex))
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float FindNearestOpponentDistance(int athleteIndex)
    {
        var teamId = identities[athleteIndex].teamId;
        var nearest = float.MaxValue;

        for (var i = 0; i < AthleteCount; i++)
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
        var carrierPos = positions[carrierIndex];
        var attackDir = teamId == 0 ? 1f : -1f;
        var bestIndex = -1;
        var bestScore = float.MinValue;

        for (var i = 0; i < AthleteCount; i++)
        {
            if (i == carrierIndex || identities[i].teamId != teamId)
            {
                continue;
            }

            var ahead = (positions[i].x - carrierPos.x) * attackDir;
            if (ahead <= 0f)
            {
                continue;
            }

            var nearestOpponent = float.MaxValue;
            for (var j = 0; j < AthleteCount; j++)
            {
                if (identities[j].teamId == teamId)
                {
                    continue;
                }

                nearestOpponent = Mathf.Min(nearestOpponent, Vector2.Distance(positions[i], positions[j]));
            }

            if (nearestOpponent <= DenyRadius)
            {
                continue;
            }

            var score = ahead - (Vector2.Distance(carrierPos, positions[i]) * 0.15f);
            if (score > bestScore || (Mathf.Approximately(score, bestScore) && i < bestIndex))
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void ResolveTackleEvents()
    {
        if (matchFinished || ballOwnerIndex < 0)
        {
            return;
        }

        var victimIndex = ballOwnerIndex;
        var victimTeam = ballOwnerTeam;
        var victimPos = positions[victimIndex];

        var bestTackler = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < athletes.Length; i++)
        {
            if (i == victimIndex || identities[i].teamId == victimTeam || tackleCooldowns[i] > 0f)
            {
                continue;
            }

            var dist = Vector2.Distance(positions[i], victimPos);
            if (dist <= rules.tackleRadius && dist < bestDistance)
            {
                bestDistance = dist;
                bestTackler = i;
            }
        }

        if (bestTackler < 0)
        {
            return;
        }

        var impulseDir = victimPos - positions[bestTackler];
        if (impulseDir.sqrMagnitude < 0.0001f)
        {
            impulseDir = identities[bestTackler].teamId == 0 ? Vector2.right : Vector2.left;
        }

        impulseDir.Normalize();

        stunTimers[victimIndex] = rules.stunSeconds;
        tackleCooldowns[bestTackler] = rules.tackleCooldownSeconds;

        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
        ballPos = victimPos;
        ballVel = (impulseDir * rules.tackleImpulse) + (velocities[victimIndex] * 0.25f);
    }

    private void UpdateBall(float dt)
    {
        if (ballOwnerIndex >= 0)
        {
            var ownerVelocity = velocities[ballOwnerIndex];
            var offset = ownerVelocity.sqrMagnitude > 0.0001f
                ? ownerVelocity.normalized * rules.carrierForwardOffset
                : Vector2.zero;
            ballPos = positions[ballOwnerIndex] + offset;
            ballVel = ownerVelocity;
        }
        else
        {
            if (!matchFinished)
            {
                ballPos += ballVel * dt;
                var damping = Mathf.Clamp01(1f - (rules.ballDamping * dt));
                ballVel *= damping;
                ClampBall();
            }
        }
    }

    private void ResolvePickup()
    {
        if (matchFinished || ballOwnerIndex >= 0)
        {
            return;
        }

        var closest = -1;
        var closestDist = rules.pickupRadius;

        for (var i = 0; i < athletes.Length; i++)
        {
            if (athletes[i] == null || stunTimers[i] > 0f)
            {
                continue;
            }

            var dist = Vector2.Distance(positions[i], ballPos);
            if (dist <= closestDist)
            {
                closestDist = dist;
                closest = i;
            }
        }

        if (closest >= 0)
        {
            ballOwnerIndex = closest;
            ballOwnerTeam = identities[closest].teamId;
            ballVel = velocities[closest];
        }
    }

    private void ResolveGoal(int tickIndex)
    {
        if (matchFinished)
        {
            return;
        }

        if (IsInLeftGoal(ballPos))
        {
            scoreTeam1 += 1;
            Debug.Log($"[FantasySport] GOAL team=1 score={scoreTeam0}-{scoreTeam1} tick={tickIndex}");
            ResetKickoff();
            UpdateHud(force: true);
            return;
        }

        if (IsInRightGoal(ballPos))
        {
            scoreTeam0 += 1;
            Debug.Log($"[FantasySport] GOAL team=0 score={scoreTeam0}-{scoreTeam1} tick={tickIndex}");
            ResetKickoff();
            UpdateHud(force: true);
        }
    }

    private void ApplyTransforms(float dt)
    {
        for (var i = 0; i < athletes.Length; i++)
        {
            if (athletes[i] == null)
            {
                continue;
            }

            athletes[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            FaceVelocity(athletes[i], velocities[i]);

            var pipelineRenderer = pipelineRenderers != null ? pipelineRenderers[i] : null;
            if (activePipeline != null && pipelineRenderer != null)
            {
                activePipeline.ApplyVisual(pipelineRenderer, visualKeys[i], velocities[i], dt);
            }
        }

        if (ballTransform != null)
        {
            ballTransform.localPosition = new Vector3(ballPos.x, ballPos.y, 0f);
        }

        var hasOwner = ballOwnerIndex >= 0;
        for (var i = 0; i < possessionRings.Length; i++)
        {
            var ring = possessionRings[i];
            if (ring == null)
            {
                continue;
            }

            ring.enabled = hasOwner && i == ballOwnerIndex;
        }

        for (var i = 0; i < debugStateLabels.Length; i++)
        {
            var label = debugStateLabels[i];
            if (label == null)
            {
                continue;
            }

            label.gameObject.SetActive(showDebugPlayerState);
            if (!showDebugPlayerState)
            {
                continue;
            }

            var assigned = assignedTargetIndex[i] >= 0 ? assignedTargetIndex[i].ToString() : "-";
            var roleTag = string.IsNullOrEmpty(identities[i].role) ? "?" : identities[i].role.Substring(0, 1);
            label.text = $"T{identities[i].teamId} {roleTag} {athleteStates[i]} #{assigned}";
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

        var possession = ballOwnerTeam >= 0 ? $"Team{ballOwnerTeam}" : "None";
        hudText.text = $"FantasySport  Team0 {scoreTeam0} : {scoreTeam1} Team1   Time: {minutes:00}:{seconds:00}   Possession: {possession}";
    }

    private Vector2 GetOwnGoalCenter(int teamId)
    {
        return teamId == 0 ? new Vector2(-halfWidth, 0f) : new Vector2(halfWidth, 0f);
    }

    private Vector2 GetOpponentGoalCenter(int teamId)
    {
        return teamId == 0 ? new Vector2(halfWidth, 0f) : new Vector2(-halfWidth, 0f);
    }

    private bool IsInLeftGoal(Vector2 point)
    {
        return point.x >= -halfWidth && point.x <= -halfWidth + rules.goalDepth && Mathf.Abs(point.y) <= rules.goalHeight * 0.5f;
    }

    private bool IsInRightGoal(Vector2 point)
    {
        return point.x <= halfWidth && point.x >= halfWidth - rules.goalDepth && Mathf.Abs(point.y) <= rules.goalHeight * 0.5f;
    }

    private void ClampAthlete(int athleteIndex)
    {
        if (positions[athleteIndex].x < -halfWidth || positions[athleteIndex].x > halfWidth)
        {
            positions[athleteIndex].x = Mathf.Clamp(positions[athleteIndex].x, -halfWidth, halfWidth);
            velocities[athleteIndex].x *= -0.35f;
        }

        if (positions[athleteIndex].y < -halfHeight || positions[athleteIndex].y > halfHeight)
        {
            positions[athleteIndex].y = Mathf.Clamp(positions[athleteIndex].y, -halfHeight, halfHeight);
            velocities[athleteIndex].y *= -0.35f;
        }
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
        if (velocity.sqrMagnitude > 0.0001f)
        {
            var angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            target.localRotation = Quaternion.Euler(0f, 0f, angle);
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
