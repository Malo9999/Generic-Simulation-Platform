using UnityEngine;
using UnityEngine.UI;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int AthleteCount = 10;
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private FantasySportRules rules = new FantasySportRules();

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

        ResolveArtPipeline();

        for (var i = 0; i < AthleteCount; i++)
        {
            athleteRngs[i] = RngService.Fork($"SIM:FantasySport:ATHLETE:{i}");
            supportLaneOffsets[i] = athleteRngs[i].Range(-halfHeight * 0.55f, halfHeight * 0.55f);
        }

        var rng = RngService.Fork("SIM:FantasySport:SPAWN");

        for (var i = 0; i < AthleteCount; i++)
        {
            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: i < AthleteCount / 2 ? 0 : 1,
                role: i < AthleteCount / 2 ? "offense" : "defense",
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

        var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObject.name = "FantasySportBall";
        ballObject.transform.SetParent(sceneGraph.EntitiesRoot, false);
        ballObject.transform.localScale = Vector3.one * 0.65f;

        var collider = ballObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

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

        var laneStep = (halfHeight * 1.8f) / Mathf.Max(1, (AthleteCount / 2) - 1);
        var teamCounts = new int[2];

        for (var i = 0; i < athletes.Length; i++)
        {
            var teamId = identities[i].teamId;
            var teamSlot = teamCounts[teamId]++;
            var laneY = -halfHeight * 0.9f + (laneStep * teamSlot);
            var xBase = teamId == 0 ? -halfWidth * 0.45f : halfWidth * 0.45f;
            var xOffset = identities[i].role == "offense" ? halfWidth * 0.12f : -halfWidth * 0.12f;
            if (teamId == 1)
            {
                xOffset *= -1f;
            }

            positions[i] = new Vector2(xBase + xOffset, laneY);
            velocities[i] = Vector2.zero;
            stunTimers[i] = 0f;
            tackleCooldowns[i] = 0f;
        }

        ballPos = Vector2.zero;
        ballVel = Vector2.zero;
        ballOwnerIndex = -1;
        ballOwnerTeam = -1;
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
