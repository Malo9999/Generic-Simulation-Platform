using UnityEngine;
using UnityEngine.UI;

public class MarbleRaceRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int MarbleCount = 12;
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;

    private Transform[] marbles;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] targetSpeeds;
    private float[] steeringAccels;
    private int[] nextCheckpointIndex;
    private int[] lapCount;
    private float[] progressScore;
    private float[] previousCheckpointDistance;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private SimulationSceneGraph sceneGraph;

    private Vector2[] checkpoints;
    private float checkpointRadius;
    private int lapsToWin;
    private bool raceFinished;
    private int winnerIndex;
    private int lastHudOrLogSecond = -1;
    private Text hudText;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "MarbleRace");
        EnsureMainCamera();
        BuildMarbles(config);
        Debug.Log($"{nameof(MarbleRaceRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (marbles == null || checkpoints == null || checkpoints.Length == 0)
        {
            return;
        }

        for (var i = 0; i < marbles.Length; i++)
        {
            var marble = marbles[i];
            if (!marble)
            {
                continue;
            }

            var checkpointIndex = nextCheckpointIndex[i];
            var checkpoint = checkpoints[checkpointIndex];
            var nextCheckpoint = checkpoints[(checkpointIndex + 1) % checkpoints.Length];

            var toNext = nextCheckpoint - checkpoint;
            var toNextMag = toNext.magnitude;
            var toNextDir = toNextMag > 0.0001f ? toNext / toNextMag : Vector2.zero;

            var lookAheadDistance = checkpointRadius * 0.5f;
            var targetPoint = checkpoint + toNextDir * lookAheadDistance;
            var desiredDir = (targetPoint - positions[i]).normalized;
            var desiredVel = desiredDir * targetSpeeds[i];

            if (raceFinished)
            {
                velocities[i] = Vector2.MoveTowards(velocities[i], Vector2.zero, steeringAccels[i] * dt * 1.5f);
            }
            else
            {
                velocities[i] = Vector2.MoveTowards(velocities[i], desiredVel, steeringAccels[i] * dt);
            }

            positions[i] += velocities[i] * dt;
            ApplySoftBounds(i, dt);

            var updatedToCheckpoint = checkpoints[nextCheckpointIndex[i]] - positions[i];
            var updatedDistance = updatedToCheckpoint.magnitude;

            if (!raceFinished)
            {
                var isApproaching = updatedDistance <= previousCheckpointDistance[i] + 0.01f;
                var forwardGuard = Vector2.Dot(velocities[i], updatedToCheckpoint) > -0.1f;
                if (updatedDistance <= checkpointRadius && isApproaching && forwardGuard)
                {
                    AdvanceCheckpoint(i, tickIndex, dt);
                }

                previousCheckpointDistance[i] = (checkpoints[nextCheckpointIndex[i]] - positions[i]).magnitude;
            }

            UpdateProgress(i);

            marble.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);

            var pipelineRenderer = pipelineRenderers != null ? pipelineRenderers[i] : null;
            if (activePipeline != null && pipelineRenderer != null)
            {
                activePipeline.ApplyVisual(pipelineRenderer, visualKeys[i], velocities[i], dt);
            }
        }

        UpdateHudOrLogs(tickIndex, dt);
    }

    public void Shutdown()
    {
        if (marbles != null)
        {
            for (var i = 0; i < marbles.Length; i++)
            {
                if (marbles[i] != null)
                {
                    Destroy(marbles[i].gameObject);
                }
            }
        }

        marbles = null;
        identities = null;
        positions = null;
        velocities = null;
        targetSpeeds = null;
        steeringAccels = null;
        nextCheckpointIndex = null;
        lapCount = null;
        progressScore = null;
        previousCheckpointDistance = null;
        checkpoints = null;
        pipelineRenderers = null;
        visualKeys = null;
        raceFinished = false;
        winnerIndex = -1;
        lastHudOrLogSecond = -1;
        hudText = null;

        Debug.Log("MarbleRaceRunner Shutdown");
    }

    private void BuildMarbles(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);
        lapsToWin = 3;
        checkpointRadius = Mathf.Clamp(Mathf.Min(halfWidth, halfHeight) * 0.12f, 1f, 2.5f);

        marbles = new Transform[MarbleCount];
        identities = new EntityIdentity[MarbleCount];
        positions = new Vector2[MarbleCount];
        velocities = new Vector2[MarbleCount];
        targetSpeeds = new float[MarbleCount];
        steeringAccels = new float[MarbleCount];
        nextCheckpointIndex = new int[MarbleCount];
        lapCount = new int[MarbleCount];
        progressScore = new float[MarbleCount];
        previousCheckpointDistance = new float[MarbleCount];
        pipelineRenderers = new GameObject[MarbleCount];
        visualKeys = new VisualKey[MarbleCount];

        ResolveArtPipeline();
        ResolveHud();

        var rng = RngService.Fork("SIM:MarbleRace:SPAWN");
        checkpoints = BuildTrack(halfWidth, halfHeight, rng);

        var startPoint = checkpoints[0];
        var startDirection = (checkpoints[1] - checkpoints[0]).normalized;
        var startPerpendicular = new Vector2(-startDirection.y, startDirection.x);

        for (var i = 0; i < MarbleCount; i++)
        {
            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: i % 2,
                role: "marble",
                variantCount: 4,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "MarbleRace");

            var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, identity.teamId);

            var marble = new GameObject($"Sim_{identity.entityId:0000}");
            marble.transform.SetParent(groupRoot, false);

            var visualKey = VisualKeyBuilder.Create(
                simulationId: "MarbleRace",
                entityType: "marble",
                instanceId: identity.entityId,
                kind: string.IsNullOrWhiteSpace(identity.role) ? "marble" : identity.role,
                state: "idle",
                facingMode: FacingMode.Auto,
                groupId: identity.teamId);

            var visualParent = marble.transform;
            if (activePipeline != null)
            {
                pipelineRenderers[i] = activePipeline.CreateRenderer(visualKey, marble.transform);
                if (pipelineRenderers[i] != null)
                {
                    visualParent = pipelineRenderers[i].transform;
                }
            }

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(visualParent, false);
            EntityIconFactory.BuildMarble(iconRoot.transform, identity);

            var row = i / 4;
            var column = i % 4;
            var lateralOffset = (column - 1.5f) * checkpointRadius * 0.65f;
            var longitudinalOffset = -row * checkpointRadius * 0.8f;
            var spawnJitter = new Vector2(rng.Range(-0.15f, 0.15f), rng.Range(-0.15f, 0.15f));

            var startPosition = startPoint + startPerpendicular * lateralOffset + startDirection * longitudinalOffset + spawnJitter;
            startPosition.x = Mathf.Clamp(startPosition.x, -halfWidth + 0.5f, halfWidth - 0.5f);
            startPosition.y = Mathf.Clamp(startPosition.y, -halfHeight + 0.5f, halfHeight - 0.5f);

            var initialSpeed = rng.Range(4f, 6f);
            var initialVelocity = startDirection * initialSpeed;

            positions[i] = startPosition;
            velocities[i] = initialVelocity;
            targetSpeeds[i] = rng.Range(6f, 9.5f);
            steeringAccels[i] = rng.Range(10f, 16f);
            nextCheckpointIndex[i] = 1 % checkpoints.Length;
            lapCount[i] = 0;
            previousCheckpointDistance[i] = (checkpoints[nextCheckpointIndex[i]] - positions[i]).magnitude;

            marble.transform.localPosition = new Vector3(startPosition.x, startPosition.y, 0f);
            marble.transform.localScale = Vector3.one;
            marbles[i] = marble.transform;
            identities[i] = identity;
            visualKeys[i] = visualKey;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(MarbleRaceRunner)} spawn[{i}] {identity}");
            }
        }
    }

    private Vector2[] BuildTrack(float arenaHalfWidth, float arenaHalfHeight, IRng rng)
    {
        var checkpointCount = rng.Range(8, 17);
        var points = new Vector2[checkpointCount];

        var radiusX = arenaHalfWidth * 0.72f;
        var radiusY = arenaHalfHeight * 0.72f;

        for (var i = 0; i < checkpointCount; i++)
        {
            var t = (float)i / checkpointCount;
            var angle = t * Mathf.PI * 2f;

            var warpX = 1f + Mathf.Sin(angle * 2f) * 0.06f;
            var warpY = 1f + Mathf.Cos(angle * 3f) * 0.06f;
            var jitterX = rng.Range(-arenaHalfWidth * 0.04f, arenaHalfWidth * 0.04f);
            var jitterY = rng.Range(-arenaHalfHeight * 0.04f, arenaHalfHeight * 0.04f);

            var x = Mathf.Cos(angle) * radiusX * warpX + jitterX;
            var y = Mathf.Sin(angle) * radiusY * warpY + jitterY;
            x = Mathf.Clamp(x, -arenaHalfWidth + checkpointRadius, arenaHalfWidth - checkpointRadius);
            y = Mathf.Clamp(y, -arenaHalfHeight + checkpointRadius, arenaHalfHeight - checkpointRadius);

            points[i] = new Vector2(x, y);
        }

        return points;
    }

    private void ApplySoftBounds(int index, float dt)
    {
        var outX = Mathf.Abs(positions[index].x) - halfWidth;
        var outY = Mathf.Abs(positions[index].y) - halfHeight;

        if (outX > 0f)
        {
            var direction = positions[index].x > 0f ? -1f : 1f;
            velocities[index].x += direction * Mathf.Min(outX * 8f, 12f) * dt;
            positions[index].x = Mathf.Clamp(positions[index].x, -halfWidth, halfWidth);
        }

        if (outY > 0f)
        {
            var direction = positions[index].y > 0f ? -1f : 1f;
            velocities[index].y += direction * Mathf.Min(outY * 8f, 12f) * dt;
            positions[index].y = Mathf.Clamp(positions[index].y, -halfHeight, halfHeight);
        }
    }

    private void AdvanceCheckpoint(int index, int tickIndex, float dt)
    {
        nextCheckpointIndex[index]++;
        if (nextCheckpointIndex[index] >= checkpoints.Length)
        {
            nextCheckpointIndex[index] = 0;
            lapCount[index]++;
            Debug.Log($"{nameof(MarbleRaceRunner)} lap complete marble={identities[index].entityId} lap={lapCount[index]}");

            if (!raceFinished && lapCount[index] >= lapsToWin)
            {
                raceFinished = true;
                winnerIndex = index;
                var raceTime = tickIndex * dt;
                Debug.Log($"{nameof(MarbleRaceRunner)} winner marble={identities[index].entityId} time={raceTime:F2}s laps={lapCount[index]}");
            }
        }
    }

    private void UpdateProgress(int index)
    {
        var cpIndex = nextCheckpointIndex[index];
        var distToCheckpoint = (checkpoints[cpIndex] - positions[index]).magnitude;
        var cpFraction = cpIndex - Mathf.Clamp01(distToCheckpoint / Mathf.Max(0.001f, checkpointRadius));
        progressScore[index] = lapCount[index] * checkpoints.Length + cpFraction;
    }

    private int[] GetOrderedIndicesByProgress()
    {
        var ordering = new int[marbles.Length];
        for (var i = 0; i < ordering.Length; i++)
        {
            ordering[i] = i;
        }

        for (var i = 1; i < ordering.Length; i++)
        {
            var current = ordering[i];
            var currentScore = progressScore[current];
            var currentEntityId = identities[current].entityId;
            var j = i - 1;

            while (j >= 0)
            {
                var compare = ordering[j];
                var compareScore = progressScore[compare];
                var compareEntityId = identities[compare].entityId;
                var shouldSwap = currentScore > compareScore
                    || (Mathf.Approximately(currentScore, compareScore) && currentEntityId < compareEntityId);

                if (!shouldSwap)
                {
                    break;
                }

                ordering[j + 1] = ordering[j];
                j--;
            }

            ordering[j + 1] = current;
        }

        return ordering;
    }

    private void UpdateHudOrLogs(int tickIndex, float dt)
    {
        var elapsedSeconds = Mathf.FloorToInt(tickIndex * dt);
        if (elapsedSeconds == lastHudOrLogSecond)
        {
            return;
        }

        lastHudOrLogSecond = elapsedSeconds;

        var ranking = GetOrderedIndicesByProgress();
        var topCount = Mathf.Min(3, ranking.Length);
        var status = "";

        for (var rank = 0; rank < topCount; rank++)
        {
            var idx = ranking[rank];
            var entry = $"#{rank + 1} M{identities[idx].entityId} L{lapCount[idx]} CP{nextCheckpointIndex[idx]}";
            status += rank == 0 ? entry : $" | {entry}";
        }

        if (raceFinished && winnerIndex >= 0)
        {
            status += $" | WINNER: M{identities[winnerIndex].entityId}";
        }

        if (hudText != null)
        {
            hudText.text = status;
        }
        else
        {
            Debug.Log($"{nameof(MarbleRaceRunner)} leaderboard {status}");
        }
    }

    private void ResolveArtPipeline()
    {
        artSelector = UnityEngine.Object.FindFirstObjectByType<ArtModeSelector>()
            ?? UnityEngine.Object.FindAnyObjectByType<ArtModeSelector>();

        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
        if (activePipeline != null)
        {
            Debug.Log($"{nameof(MarbleRaceRunner)} using art pipeline '{activePipeline.DisplayName}' ({activePipeline.Mode}).");
            return;
        }

        Debug.Log($"{nameof(MarbleRaceRunner)} no {nameof(ArtModeSelector)} / active pipeline found; using default marble renderers.");
    }

    private void ResolveHud()
    {
        var hudObject = GameObject.Find("HUDText");
        if (hudObject == null)
        {
            hudText = null;
            return;
        }

        hudText = hudObject.GetComponent<Text>();
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (var i = 0; i < checkpoints.Length; i++)
        {
            var current = new Vector3(checkpoints[i].x, checkpoints[i].y, 0f);
            var next = new Vector3(checkpoints[(i + 1) % checkpoints.Length].x, checkpoints[(i + 1) % checkpoints.Length].y, 0f);
            Gizmos.DrawWireSphere(current, checkpointRadius);
            Gizmos.DrawLine(current, next);
        }
    }
#endif
}
