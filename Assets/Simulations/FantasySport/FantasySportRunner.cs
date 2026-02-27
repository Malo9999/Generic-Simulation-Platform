using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int AthleteCount = 10;
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private FantasySportRules rules = new FantasySportRules();
    [SerializeField] private float athleteIconScaleMultiplier = 1.3f;

    private Transform[] athletes;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] stunTimers;
    private float[] tackleCooldowns;
    private float[] supportLaneOffsets;
    private IRng[] athleteRngs;

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
        supportLaneOffsets = null;
        athleteRngs = null;
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
        supportLaneOffsets = new float[AthleteCount];
        athleteRngs = new IRng[AthleteCount];
        pipelineRenderers = new GameObject[AthleteCount];
        visualKeys = new VisualKey[AthleteCount];
        possessionRings = new SpriteRenderer[AthleteCount];

        ResolveArtPipeline();

        for (var i = 0; i < AthleteCount; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{i}");
            supportLaneOffsets[i] = athleteRngs[i].Range(-halfHeight * 0.55f, halfHeight * 0.55f);
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
        }
    }

    private void UpdateAthletes(float dt)
    {
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
        var target = athletePos;

        var ownGoal = GetOwnGoalCenter(identity.teamId);
        var enemyGoal = GetOpponentGoalCenter(identity.teamId);

        if (athleteIndex == ballOwnerIndex)
        {
            target = enemyGoal;
        }
        else if (identity.role == "offense")
        {
            if (ballOwnerIndex < 0)
            {
                target = ballPos;
            }
            else if (ballOwnerTeam == identity.teamId)
            {
                var laneTarget = new Vector2(
                    Mathf.Lerp(ballPos.x, enemyGoal.x, 0.45f),
                    Mathf.Clamp(supportLaneOffsets[athleteIndex], -halfHeight * 0.8f, halfHeight * 0.8f));
                target = Vector2.Lerp(laneTarget, ballPos, 0.35f);
            }
            else
            {
                target = ballPos;
            }
        }
        else
        {
            if (ballOwnerIndex >= 0 && ballOwnerTeam != identity.teamId)
            {
                target = positions[ballOwnerIndex];
            }
            else
            {
                target = Vector2.Lerp(ballPos, ownGoal, 0.5f);
            }
        }

        var toTarget = target - athletePos;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return toTarget.normalized * maxSpeed;
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
