using System.Text;
using UnityEngine;

public class MarbleRaceRunner : MonoBehaviour, ITickableSimulationRunner
{
    public enum RacePhase
    {
        Ready,
        Racing,
        Finished,
        Cooldown,
    }

    private const int MarbleCount = 12;
    private const int SpawnDebugCount = 5;
    private const int TrackSamples = 512;
    private const int LapCrossWindow = 12;
    private const int LapArmDistance = 32;

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private bool logLapEvents = false;
    [SerializeField] private bool showDebugTrack = true;
    [SerializeField] private float gravityStrength = 8f;
    [SerializeField] private float rollingFriction = 0.24f;
    [SerializeField] private float maxSpeed = 13f;

    private Transform[] marbles;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] targetSpeeds;
    private float[] steeringAccels;
    private int[] lapCount;
    private float[] progressScore;
    private float[] desiredLaneOffset;
    private int[] closestTrackIndex;
    private int[] previousLapLogged;
    private int[] lastClosestIndex;
    private bool[] lapArmed;
    private int[] startIndex;

    private float[] baseSpeedTrait;
    private float[] aggressionTrait;
    private float[] laneBiasTrait;
    private float[] avoidanceTrait;
    private float[] draftingTrait;
    private float[] corneringTrait;

    private Vector2[] controlPoints;
    private Vector2[] trackCenter;
    private Vector2[] trackTangent;
    private Vector2[] trackNormal;
    private float[] trackHalfWidth;
    private float[] trackCurvature;
    private float[] trackElevation;
    private float[] trackSlopeAccel;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private SimulationSceneGraph sceneGraph;

    private int lapsToWin;
    private RacePhase raceState;
    private float finishTime;
    private float elapsedTime;
    private int winnerIndex;
    private readonly int[] rankingBuffer = new int[MarbleCount];
    private readonly StringBuilder winnerLineBuilder = new(64);

    public RacePhase CurrentPhase => raceState;

    private GameObject trackDebugRoot;
    private LineRenderer leftBoundaryRenderer;
    private LineRenderer rightBoundaryRenderer;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "MarbleRace");
        EnsureMainCamera();
        BuildMarbles(config);
        Debug.Log($"{nameof(MarbleRaceRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void StartRace()
    {
        if (raceState != RacePhase.Ready)
        {
            return;
        }

        raceState = RacePhase.Racing;
    }

    public void FillLeaderboard(StringBuilder sb, int maxEntries, bool final)
    {
        if (sb == null)
        {
            return;
        }

        sb.Clear();
        if (marbles == null || identities == null || marbles.Length == 0)
        {
            sb.Append("No marbles.");
            return;
        }

        var count = FillOrderedIndicesByProgress(rankingBuffer);
        var top = Mathf.Clamp(maxEntries <= 0 ? count : maxEntries, 1, count);

        for (var rank = 0; rank < top; rank++)
        {
            var idx = rankingBuffer[rank];
            if (rank > 0)
            {
                sb.Append('\n');
            }

            sb.Append('#').Append(rank + 1)
                .Append(" M").Append(identities[idx].entityId)
                .Append(" L").Append(lapCount[idx]);

            if (!final)
            {
                sb.Append(" T").Append(closestTrackIndex[idx]);
            }
        }
    }

    public string GetWinnerLine()
    {
        if (winnerIndex < 0 || identities == null || winnerIndex >= identities.Length)
        {
            return string.Empty;
        }

        winnerLineBuilder.Clear();
        winnerLineBuilder.Append("WINNER: M")
            .Append(identities[winnerIndex].entityId)
            .Append(" time=")
            .Append(finishTime.ToString("F2"))
            .Append("s laps=")
            .Append(lapCount[winnerIndex]);

        return winnerLineBuilder.ToString();
    }

    public void Tick(int tickIndex, float dt)
    {
        if (marbles == null || trackCenter == null || trackCenter.Length == 0)
        {
            return;
        }

        elapsedTime += dt;

        for (var i = 0; i < marbles.Length; i++)
        {
            var marble = marbles[i];
            if (!marble)
            {
                continue;
            }

            UpdateClosestTrackIndex(i, 14);

            if (raceState == RacePhase.Racing)
            {
                UpdateDrivingBehavior(i, dt);
            }
            else
            {
                velocities[i] = Vector2.MoveTowards(velocities[i], Vector2.zero, steeringAccels[i] * dt * 1.5f);
            }

            positions[i] += velocities[i] * dt;
            ApplyCorridorBounds(i);
            ApplySoftArenaBounds(i, dt);
            UpdateClosestTrackIndex(i, 16);

            if (raceState == RacePhase.Racing)
            {
                UpdateLapProgress(i);
            }

            UpdateProgress(i);
            lastClosestIndex[i] = closestTrackIndex[i];
            marble.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);

            var pipelineRenderer = pipelineRenderers != null ? pipelineRenderers[i] : null;
            if (activePipeline != null && pipelineRenderer != null)
            {
                activePipeline.ApplyVisual(pipelineRenderer, visualKeys[i], velocities[i], dt);
            }
        }

        if (raceState == RacePhase.Finished && elapsedTime >= finishTime + 3f)
        {
            raceState = RacePhase.Cooldown;
        }
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

        if (trackDebugRoot != null)
        {
            Destroy(trackDebugRoot);
        }

        marbles = null;
        identities = null;
        positions = null;
        velocities = null;
        targetSpeeds = null;
        steeringAccels = null;
        lapCount = null;
        progressScore = null;
        desiredLaneOffset = null;
        closestTrackIndex = null;
        previousLapLogged = null;
        lastClosestIndex = null;
        lapArmed = null;
        startIndex = null;

        baseSpeedTrait = null;
        aggressionTrait = null;
        laneBiasTrait = null;
        avoidanceTrait = null;
        draftingTrait = null;
        corneringTrait = null;

        controlPoints = null;
        trackCenter = null;
        trackTangent = null;
        trackNormal = null;
        trackHalfWidth = null;
        trackCurvature = null;
        trackElevation = null;
        trackSlopeAccel = null;

        pipelineRenderers = null;
        visualKeys = null;

        raceState = RacePhase.Ready;
        finishTime = 0f;
        elapsedTime = 0f;
        winnerIndex = -1;
        leftBoundaryRenderer = null;
        rightBoundaryRenderer = null;
        trackDebugRoot = null;

        Debug.Log("MarbleRaceRunner Shutdown");
    }

    private void BuildMarbles(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64f) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64f) * 0.5f);
        lapsToWin = 3;
        raceState = RacePhase.Ready;
        winnerIndex = -1;
        elapsedTime = 0f;

        marbles = new Transform[MarbleCount];
        identities = new EntityIdentity[MarbleCount];
        positions = new Vector2[MarbleCount];
        velocities = new Vector2[MarbleCount];
        targetSpeeds = new float[MarbleCount];
        steeringAccels = new float[MarbleCount];
        lapCount = new int[MarbleCount];
        progressScore = new float[MarbleCount];
        desiredLaneOffset = new float[MarbleCount];
        closestTrackIndex = new int[MarbleCount];
        previousLapLogged = new int[MarbleCount];
        lastClosestIndex = new int[MarbleCount];
        lapArmed = new bool[MarbleCount];
        startIndex = new int[MarbleCount];

        baseSpeedTrait = new float[MarbleCount];
        aggressionTrait = new float[MarbleCount];
        laneBiasTrait = new float[MarbleCount];
        avoidanceTrait = new float[MarbleCount];
        draftingTrait = new float[MarbleCount];
        corneringTrait = new float[MarbleCount];

        pipelineRenderers = new GameObject[MarbleCount];
        visualKeys = new VisualKey[MarbleCount];

        ResolveArtPipeline();
        var rng = RngService.Fork("SIM:MarbleRace:SPAWN");
        BuildTrack(halfWidth, halfHeight, rng);
        BuildDebugTrack();

        var startPoint = trackCenter[0];
        var startDirection = trackTangent[0];
        var startPerpendicular = trackNormal[0];

        for (var i = 0; i < MarbleCount; i++)
        {
            previousLapLogged[i] = -1;

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
            var laneSpace = Mathf.Min(trackHalfWidth[0] * 0.45f, 1.8f);
            var lateralOffset = (column - 1.5f) * laneSpace;
            var longitudinalOffset = -row * 1.2f;
            var spawnJitter = new Vector2(rng.Range(-0.12f, 0.12f), rng.Range(-0.12f, 0.12f));

            var startPosition = startPoint + startPerpendicular * lateralOffset + startDirection * longitudinalOffset + spawnJitter;
            startPosition.x = Mathf.Clamp(startPosition.x, -halfWidth + 0.5f, halfWidth - 0.5f);
            startPosition.y = Mathf.Clamp(startPosition.y, -halfHeight + 0.5f, halfHeight - 0.5f);

            baseSpeedTrait[i] = rng.Range(6.5f, 9.5f);
            aggressionTrait[i] = rng.Range(0f, 1f);
            laneBiasTrait[i] = rng.Range(-1f, 1f);
            avoidanceTrait[i] = rng.Range(0.7f, 1.35f);
            draftingTrait[i] = rng.Range(0.6f, 1.3f);
            corneringTrait[i] = rng.Range(0f, 1f);

            positions[i] = startPosition;
            velocities[i] = Vector2.zero;
            targetSpeeds[i] = baseSpeedTrait[i];
            steeringAccels[i] = rng.Range(10f, 15f);
            closestTrackIndex[i] = FindClosestTrackIndex(startPosition);
            lastClosestIndex[i] = closestTrackIndex[i];
            startIndex[i] = closestTrackIndex[i];
            lapArmed[i] = false;
            lapCount[i] = 0;
            desiredLaneOffset[i] = lateralOffset;

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

    private void BuildTrack(float arenaHalfWidth, float arenaHalfHeight, IRng rng)
    {
        controlPoints = BuildGrandPrixControlPoints(arenaHalfWidth, arenaHalfHeight, rng);
        var smooth = ApplyChaikinLoop(controlPoints, 3);
        trackCenter = ResampleLoopedCatmull(smooth, TrackSamples);

        var baseHalfWidth = Mathf.Clamp(Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.14f, 2.2f, 5.5f);
        EnsureSafeStartLine(baseHalfWidth * 0.8f);

        trackTangent = new Vector2[TrackSamples];
        trackNormal = new Vector2[TrackSamples];
        trackHalfWidth = new float[TrackSamples];
        trackCurvature = new float[TrackSamples];
        trackElevation = new float[TrackSamples];
        trackSlopeAccel = new float[TrackSamples];

        var overtakeAStart = Mathf.RoundToInt(TrackSamples * 0.10f);
        var overtakeAEnd = Mathf.RoundToInt(TrackSamples * 0.20f);
        var overtakeBStart = Mathf.RoundToInt(TrackSamples * 0.58f);
        var overtakeBEnd = Mathf.RoundToInt(TrackSamples * 0.73f);

        var p1 = rng.Range(0f, Mathf.PI * 2f);
        var p2 = rng.Range(0f, Mathf.PI * 2f);
        var p3 = rng.Range(0f, Mathf.PI * 2f);

        for (var i = 0; i < TrackSamples; i++)
        {
            var prev = trackCenter[(i - 1 + TrackSamples) % TrackSamples];
            var curr = trackCenter[i];
            var next = trackCenter[(i + 1) % TrackSamples];
            var tangent = (next - prev).normalized;
            if (tangent.sqrMagnitude < 0.00001f)
            {
                tangent = Vector2.right;
            }

            trackTangent[i] = tangent;
            trackNormal[i] = new Vector2(-tangent.y, tangent.x);

            var a = (curr - prev).normalized;
            var b = (next - curr).normalized;
            var corner = 1f - Mathf.Clamp01(Vector2.Dot(a, b));
            trackCurvature[i] = corner;

            var straightness = 1f - corner;
            var width = baseHalfWidth * (0.9f + straightness * 0.4f);
            if (i >= overtakeAStart && i <= overtakeAEnd)
            {
                width *= 1.5f;
            }

            if (i >= overtakeBStart && i <= overtakeBEnd)
            {
                width *= 1.42f;
            }

            if (corner > 0.42f)
            {
                width *= 0.9f;
            }

            trackHalfWidth[i] = Mathf.Clamp(width, baseHalfWidth * 0.75f, baseHalfWidth * 1.65f);

            var t = (float)i / TrackSamples;
            trackElevation[i] = Mathf.Sin(t * Mathf.PI * 2f * 1.1f + p1) * 0.42f
                              + Mathf.Sin(t * Mathf.PI * 2f * 2.3f + p2) * 0.26f
                              + Mathf.Sin(t * Mathf.PI * 2f * 3.7f + p3) * 0.18f;
        }

        for (var i = 0; i < TrackSamples; i++)
        {
            var prev = trackElevation[(i - 1 + TrackSamples) % TrackSamples];
            var next = trackElevation[(i + 1) % TrackSamples];
            var slope = (next - prev) * 0.5f;
            trackSlopeAccel[i] = -slope * gravityStrength;
        }
    }

    private Vector2[] BuildGrandPrixControlPoints(float arenaHalfWidth, float arenaHalfHeight, IRng rng)
    {
        var maxX = Mathf.Max(6f, arenaHalfWidth - 4f);
        var maxY = Mathf.Max(6f, arenaHalfHeight - 4f);

        var points = new[]
        {
            new Vector2(-0.78f, -0.12f),
            new Vector2(-0.58f, -0.08f),
            new Vector2(-0.24f, -0.06f),
            new Vector2(0.24f, -0.04f),
            new Vector2(0.70f, -0.02f),
            new Vector2(0.84f, 0.30f),   // hairpin approach
            new Vector2(0.66f, 0.72f),
            new Vector2(0.16f, 0.80f),
            new Vector2(-0.18f, 0.64f),  // chicane / S entry
            new Vector2(-0.38f, 0.82f),
            new Vector2(-0.62f, 0.66f),
            new Vector2(-0.80f, 0.24f),
            new Vector2(-0.86f, -0.18f),
            new Vector2(-0.66f, -0.54f),
            new Vector2(-0.18f, -0.76f),
            new Vector2(0.42f, -0.68f),
        };

        var result = new Vector2[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var p = new Vector2(points[i].x * maxX, points[i].y * maxY);
            var jitter = new Vector2(rng.Range(-0.9f, 0.9f), rng.Range(-0.8f, 0.8f));
            if (i % 4 == 0)
            {
                jitter *= 0.45f;
            }

            result[i] = p + jitter;
        }

        EnforceMinControlPointSeparation(result, 2.8f);
        return result;
    }

    private static void EnforceMinControlPointSeparation(Vector2[] points, float minSeparation)
    {
        var minSq = minSeparation * minSeparation;
        for (var iter = 0; iter < 3; iter++)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var j = (i + 1) % points.Length;
                var delta = points[j] - points[i];
                var sq = delta.sqrMagnitude;
                if (sq < 0.0001f || sq >= minSq)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(sq);
                var push = (minSeparation - dist) * 0.5f;
                var dir = delta / dist;
                points[i] -= dir * push;
                points[j] += dir * push;
            }
        }
    }

    private void EnsureSafeStartLine(float threshold)
    {
        if (trackCenter == null || trackCenter.Length < 4)
        {
            return;
        }

        var seamDistance = Vector2.Distance(trackCenter[0], trackCenter[TrackSamples - 1]);
        if (seamDistance >= threshold)
        {
            return;
        }

        var bestIndex = 0;
        var bestScore = float.MinValue;
        for (var i = 0; i < TrackSamples; i++)
        {
            var prev = trackCenter[(i - 1 + TrackSamples) % TrackSamples];
            var curr = trackCenter[i];
            var next = trackCenter[(i + 1) % TrackSamples];
            var corner = 1f - Mathf.Clamp01(Vector2.Dot((curr - prev).normalized, (next - curr).normalized));
            var separation = Vector2.Distance(curr, trackCenter[(i - 1 + TrackSamples) % TrackSamples]);
            var score = separation - corner * 3f;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex == 0)
        {
            return;
        }

        var rotated = new Vector2[TrackSamples];
        for (var i = 0; i < TrackSamples; i++)
        {
            rotated[i] = trackCenter[(bestIndex + i) % TrackSamples];
        }

        trackCenter = rotated;
    }

    private static Vector2[] ApplyChaikinLoop(Vector2[] input, int iterations)
    {
        var current = input;
        for (var iter = 0; iter < iterations; iter++)
        {
            var next = new Vector2[current.Length * 2];
            for (var i = 0; i < current.Length; i++)
            {
                var a = current[i];
                var b = current[(i + 1) % current.Length];
                next[i * 2] = a * 0.75f + b * 0.25f;
                next[i * 2 + 1] = a * 0.25f + b * 0.75f;
            }

            current = next;
        }

        return current;
    }

    private static Vector2[] ResampleLoopedCatmull(Vector2[] controls, int sampleCount)
    {
        var result = new Vector2[sampleCount];
        var n = controls.Length;

        for (var i = 0; i < sampleCount; i++)
        {
            var u = (float)i / sampleCount * n;
            var seg = Mathf.FloorToInt(u) % n;
            var t = u - Mathf.Floor(u);

            var p0 = controls[(seg - 1 + n) % n];
            var p1 = controls[seg];
            var p2 = controls[(seg + 1) % n];
            var p3 = controls[(seg + 2) % n];

            result[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        return result;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private int FindClosestTrackIndex(Vector2 position)
    {
        var bestIndex = 0;
        var bestDistSq = float.MaxValue;
        for (var i = 0; i < TrackSamples; i++)
        {
            var distSq = (position - trackCenter[i]).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void UpdateClosestTrackIndex(int marbleIndex, int window)
    {
        var current = closestTrackIndex[marbleIndex];
        var bestIndex = current;
        var bestDistSq = (positions[marbleIndex] - trackCenter[current]).sqrMagnitude;

        for (var o = -window; o <= window; o++)
        {
            var idx = (current + o + TrackSamples) % TrackSamples;
            var distSq = (positions[marbleIndex] - trackCenter[idx]).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = idx;
            }
        }

        closestTrackIndex[marbleIndex] = bestIndex;
    }

    private void UpdateDrivingBehavior(int i, float dt)
    {
        var idx = closestTrackIndex[i];
        var lookAhead = 8 + Mathf.RoundToInt(Mathf.Clamp(baseSpeedTrait[i] - 6f, 0f, 4f) * 2.5f);
        var targetIdx = (idx + lookAhead) % TrackSamples;

        var desiredLane = laneBiasTrait[i] * trackHalfWidth[targetIdx] * 0.35f;

        var ahead = FindNearestAhead(i, 34);
        if (ahead >= 0)
        {
            var aheadDelta = ForwardDelta(idx, closestTrackIndex[ahead]);
            if (aheadDelta > 0 && aheadDelta < 24)
            {
                var mySpeed = velocities[i].magnitude;
                var aheadSpeed = velocities[ahead].magnitude;
                if (mySpeed > aheadSpeed * 0.94f)
                {
                    var side = ChoosePassSide(i, ahead, targetIdx);
                    var passAmount = Mathf.Lerp(0.35f, 0.95f, aggressionTrait[i]);
                    desiredLane += side * trackHalfWidth[targetIdx] * passAmount;
                }
            }
        }

        desiredLane = Mathf.Clamp(desiredLane, -trackHalfWidth[targetIdx] * 0.95f, trackHalfWidth[targetIdx] * 0.95f);
        desiredLaneOffset[i] = Mathf.MoveTowards(desiredLaneOffset[i], desiredLane, dt * 3f);

        var targetPoint = trackCenter[targetIdx] + trackNormal[targetIdx] * desiredLaneOffset[i];
        var desiredDir = (targetPoint - positions[i]).normalized;

        var draftBonus = ComputeDraftBonus(i);
        var cornerPenalty = trackCurvature[targetIdx] * Mathf.Lerp(0.66f, 0.28f, corneringTrait[i]);
        var desiredSpeed = baseSpeedTrait[i] * (1f - cornerPenalty) + draftBonus + aggressionTrait[i] * 0.15f;
        desiredSpeed = Mathf.Clamp(desiredSpeed, 4f, 11.4f);

        targetSpeeds[i] = desiredSpeed;
        var desiredVel = desiredDir * desiredSpeed;
        var repel = ComputeAvoidanceRepulsion(i);
        desiredVel += repel;

        velocities[i] = Vector2.MoveTowards(velocities[i], desiredVel, steeringAccels[i] * dt);
        velocities[i] += trackTangent[idx] * trackSlopeAccel[idx] * dt;
        velocities[i] *= Mathf.Clamp01(1f - rollingFriction * dt);
        velocities[i] = Vector2.ClampMagnitude(velocities[i], maxSpeed);
    }

    private int FindNearestAhead(int marbleIndex, int forwardWindow)
    {
        var myIdx = closestTrackIndex[marbleIndex];
        var best = -1;
        var bestDelta = int.MaxValue;

        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == marbleIndex)
            {
                continue;
            }

            var delta = ForwardDelta(myIdx, closestTrackIndex[j]);
            if (delta <= 0 || delta > forwardWindow)
            {
                continue;
            }

            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = j;
            }
        }

        return best;
    }

    private int ChoosePassSide(int me, int ahead, int targetIdx)
    {
        var leftOccupancy = 0;
        var rightOccupancy = 0;

        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == me)
            {
                continue;
            }

            var delta = Mathf.Abs(ForwardDelta(targetIdx, closestTrackIndex[j]));
            if (delta > 10 && delta < TrackSamples - 10)
            {
                continue;
            }

            var offset = Vector2.Dot(positions[j] - trackCenter[targetIdx], trackNormal[targetIdx]);
            if (offset >= 0f)
            {
                leftOccupancy++;
            }
            else
            {
                rightOccupancy++;
            }
        }

        var bias = laneBiasTrait[me] + aggressionTrait[me] * 0.6f;
        var defaultSide = bias >= 0f ? 1 : -1;
        var lessOccupiedSide = leftOccupancy <= rightOccupancy ? 1 : -1;

        if (Mathf.Abs(bias) > 0.2f)
        {
            return defaultSide;
        }

        if (Mathf.Abs(leftOccupancy - rightOccupancy) >= 1)
        {
            return lessOccupiedSide;
        }

        var aheadOffset = Vector2.Dot(positions[ahead] - trackCenter[targetIdx], trackNormal[targetIdx]);
        return aheadOffset >= 0f ? -1 : 1;
    }

    private Vector2 ComputeAvoidanceRepulsion(int i)
    {
        var repulse = Vector2.zero;

        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            var delta = positions[i] - positions[j];
            var dist = delta.magnitude;
            if (dist < 0.001f || dist > 1.5f)
            {
                continue;
            }

            var push = (1.5f - dist) / 1.5f;
            var lateralSign = Mathf.Sign(Vector2.Dot(delta, trackNormal[closestTrackIndex[i]]));
            var lateralPush = trackNormal[closestTrackIndex[i]] * lateralSign * push * avoidanceTrait[i] * 2.4f;
            repulse += lateralPush + delta.normalized * push * 0.35f;
        }

        return repulse;
    }

    private float ComputeDraftBonus(int i)
    {
        var idx = closestTrackIndex[i];
        var forward = trackTangent[idx];
        var bonus = 0f;

        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            var toOther = positions[j] - positions[i];
            var dist = toOther.magnitude;
            if (dist < 0.2f || dist > 3f)
            {
                continue;
            }

            var alignment = Vector2.Dot(forward, toOther.normalized);
            if (alignment < 0.8f)
            {
                continue;
            }

            var delta = ForwardDelta(idx, closestTrackIndex[j]);
            if (delta <= 0 || delta > 18)
            {
                continue;
            }

            var local = (3f - dist) / 3f;
            bonus = Mathf.Max(bonus, local * draftingTrait[i] * 0.8f);
        }

        return Mathf.Clamp(bonus, 0f, 1.1f);
    }

    private void ApplyCorridorBounds(int marbleIndex)
    {
        var idx = closestTrackIndex[marbleIndex];
        var center = trackCenter[idx];
        var normal = trackNormal[idx];
        var tangent = trackTangent[idx];
        var half = trackHalfWidth[idx];

        var delta = positions[marbleIndex] - center;
        var lateral = Vector2.Dot(delta, normal);
        var absLat = Mathf.Abs(lateral);

        if (absLat <= half)
        {
            return;
        }

        var side = Mathf.Sign(lateral);
        var penetration = absLat - half;
        positions[marbleIndex] -= normal * side * penetration;

        var vel = velocities[marbleIndex];
        var lateralVel = Vector2.Dot(vel, normal);
        var tangentialVel = Vector2.Dot(vel, tangent);

        lateralVel = -lateralVel * 0.35f;
        tangentialVel *= 0.92f;
        velocities[marbleIndex] = tangent * tangentialVel + normal * lateralVel;
    }

    private void ApplySoftArenaBounds(int index, float dt)
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

    private void UpdateLapProgress(int index)
    {
        var prev = lastClosestIndex[index];
        var curr = closestTrackIndex[index];
        var speed = velocities[index].magnitude;

        if (!lapArmed[index])
        {
            if (speed > 0.4f && ForwardDelta(startIndex[index], curr) > LapArmDistance)
            {
                lapArmed[index] = true;
            }

            return;
        }

        var velocityDir = speed > 0.001f ? velocities[index] / speed : Vector2.zero;
        var forwardDot = Vector2.Dot(velocityDir, trackTangent[curr]);

        if (prev > TrackSamples - LapCrossWindow
            && curr < LapCrossWindow
            && forwardDot > 0.25f
            && speed > 0.5f)
        {
            lapCount[index]++;
            lapArmed[index] = false;

            if (logLapEvents && previousLapLogged[index] != lapCount[index])
            {
                previousLapLogged[index] = lapCount[index];
                Debug.Log($"{nameof(MarbleRaceRunner)} lap complete marble={identities[index].entityId} lap={lapCount[index]}");
            }

            if (lapCount[index] >= lapsToWin)
            {
                raceState = RacePhase.Finished;
                winnerIndex = index;
                finishTime = elapsedTime;
            }
        }
    }

    private void UpdateProgress(int index)
    {
        var idx = closestTrackIndex[index];
        var lateral = Vector2.Dot(positions[index] - trackCenter[idx], trackNormal[idx]);
        var lateralPenalty = Mathf.Abs(lateral) / Mathf.Max(0.001f, trackHalfWidth[idx]) * 0.2f;
        progressScore[index] = lapCount[index] * TrackSamples + idx - lateralPenalty;
    }

    private int FillOrderedIndicesByProgress(int[] ordering)
    {
        if (ordering == null || marbles == null)
        {
            return 0;
        }

        var count = marbles.Length;
        for (var i = 0; i < count; i++)
        {
            ordering[i] = i;
        }

        for (var i = 1; i < count; i++)
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

        return count;
    }

    private void BuildDebugTrack()
    {
        if (sceneGraph == null || sceneGraph.DebugRoot == null || trackCenter == null)
        {
            return;
        }

        if (trackDebugRoot != null)
        {
            Destroy(trackDebugRoot);
        }

        trackDebugRoot = new GameObject("TrackDebug");
        trackDebugRoot.transform.SetParent(sceneGraph.DebugRoot, false);
        trackDebugRoot.SetActive(showDebugTrack);

        leftBoundaryRenderer = CreateBoundaryRenderer(trackDebugRoot.transform, "LeftBoundary", new Color(0.1f, 1f, 0.45f, 0.95f));
        rightBoundaryRenderer = CreateBoundaryRenderer(trackDebugRoot.transform, "RightBoundary", new Color(1f, 0.55f, 0.1f, 0.95f));

        var left = new Vector3[TrackSamples];
        var right = new Vector3[TrackSamples];
        for (var i = 0; i < TrackSamples; i++)
        {
            var c = trackCenter[i];
            var n = trackNormal[i];
            var w = trackHalfWidth[i];
            left[i] = new Vector3(c.x + n.x * w, c.y + n.y * w, 0f);
            right[i] = new Vector3(c.x - n.x * w, c.y - n.y * w, 0f);
        }

        leftBoundaryRenderer.positionCount = left.Length;
        leftBoundaryRenderer.SetPositions(left);
        rightBoundaryRenderer.positionCount = right.Length;
        rightBoundaryRenderer.SetPositions(right);
    }

    private static LineRenderer CreateBoundaryRenderer(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.widthMultiplier = 0.12f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        return lr;
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

    private static int ForwardDelta(int from, int to)
    {
        var delta = to - from;
        if (delta < 0)
        {
            delta += TrackSamples;
        }

        return delta;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (trackCenter == null || trackCenter.Length == 0)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (var i = 0; i < trackCenter.Length; i += 8)
        {
            var current = new Vector3(trackCenter[i].x, trackCenter[i].y, 0f);
            var next = new Vector3(trackCenter[(i + 8) % trackCenter.Length].x, trackCenter[(i + 8) % trackCenter.Length].y, 0f);
            Gizmos.DrawLine(current, next);
        }

        Gizmos.color = Color.cyan;
        for (var i = 0; i < trackCenter.Length; i += 16)
        {
            var c = trackCenter[i];
            var n = trackNormal[i];
            var w = trackHalfWidth[i];
            var l = new Vector3(c.x + n.x * w, c.y + n.y * w, 0f);
            var r = new Vector3(c.x - n.x * w, c.y - n.y * w, 0f);
            Gizmos.DrawLine(l, r);
        }
    }
#endif
}
