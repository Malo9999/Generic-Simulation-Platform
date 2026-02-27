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
    private const int LapCrossWindow = 12;
    private const int LapArmDistance = 32;

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private bool logLapEvents = false;
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
    private float[] stuckTimer;
    private float[] desiredLaneOffset;
    private int[] closestTrackIndex;
    private int[] previousLapLogged;
    private int[] lastClosestIndex;
    private bool[] lapArmed;

    private float[] baseSpeedTrait;
    private float[] aggressionTrait;
    private float[] laneBiasTrait;
    private float[] avoidanceTrait;
    private float[] draftingTrait;
    private float[] corneringTrait;

    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private SimulationSceneGraph sceneGraph;

    private MarbleRaceTrack currentTrack;
    private MarbleRaceTrackRenderer trackRenderer;
    private MarbleRaceTrackGenerator trackGenerator;

    private int lapsToWin;
    private RacePhase raceState;
    private float finishTime;
    private float elapsedTime;
    private int winnerIndex;
    private readonly int[] rankingBuffer = new int[MarbleCount];
    private readonly StringBuilder winnerLineBuilder = new(64);

    public RacePhase CurrentPhase => raceState;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "MarbleRace");
        CleanupLegacyTrackObjects();
        EnsureMainCamera();
        BuildRace(config);
        Debug.Log($"{nameof(MarbleRaceRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void StartRace()
    {
        if (raceState == RacePhase.Ready)
        {
            raceState = RacePhase.Racing;
        }
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
        if (marbles == null || currentTrack == null || currentTrack.SampleCount == 0)
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
                var idx = closestTrackIndex[i];
                var lateral = Vector2.Dot(positions[i] - currentTrack.Center[idx], currentTrack.Normal[idx]);
                var speed = velocities[i].magnitude;
                var half = currentTrack.HalfWidth[idx];
                if (Mathf.Abs(lateral) > half * 1.25f && speed < 0.5f)
                {
                    RescueMarble(i);
                }
                else
                {
                    stuckTimer[i] = speed < 0.25f ? stuckTimer[i] + dt : 0f;
                    if (stuckTimer[i] > 1.2f)
                    {
                        RescueMarble(i);
                    }
                }

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

        trackRenderer?.Clear();

        marbles = null;
        identities = null;
        positions = null;
        velocities = null;
        targetSpeeds = null;
        steeringAccels = null;
        lapCount = null;
        progressScore = null;
        stuckTimer = null;
        desiredLaneOffset = null;
        closestTrackIndex = null;
        previousLapLogged = null;
        lastClosestIndex = null;
        lapArmed = null;
        baseSpeedTrait = null;
        aggressionTrait = null;
        laneBiasTrait = null;
        avoidanceTrait = null;
        draftingTrait = null;
        corneringTrait = null;
        pipelineRenderers = null;
        visualKeys = null;
        currentTrack = null;

        raceState = RacePhase.Ready;
        finishTime = 0f;
        elapsedTime = 0f;
        winnerIndex = -1;
    }

    private void BuildRace(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);
        lapsToWin = 3;

        trackGenerator ??= new MarbleRaceTrackGenerator();
        trackRenderer ??= new MarbleRaceTrackRenderer();

        var rng = new SeededRng(config?.seed ?? 0);
        var variant = Mathf.Abs(config?.seed ?? 0) % 3;

        currentTrack = trackGenerator.Build(halfWidth, halfHeight, rng, variant);
        if (currentTrack == null || currentTrack.SampleCount == 0)
        {
            currentTrack = trackGenerator.BuildFallbackRoundedRectangle(halfWidth, halfHeight);
        }

        trackRenderer.Apply(sceneGraph, currentTrack);

        marbles = new Transform[MarbleCount];
        identities = new EntityIdentity[MarbleCount];
        positions = new Vector2[MarbleCount];
        velocities = new Vector2[MarbleCount];
        targetSpeeds = new float[MarbleCount];
        steeringAccels = new float[MarbleCount];
        lapCount = new int[MarbleCount];
        progressScore = new float[MarbleCount];
        stuckTimer = new float[MarbleCount];
        desiredLaneOffset = new float[MarbleCount];
        closestTrackIndex = new int[MarbleCount];
        previousLapLogged = new int[MarbleCount];
        lastClosestIndex = new int[MarbleCount];
        lapArmed = new bool[MarbleCount];

        baseSpeedTrait = new float[MarbleCount];
        aggressionTrait = new float[MarbleCount];
        laneBiasTrait = new float[MarbleCount];
        avoidanceTrait = new float[MarbleCount];
        draftingTrait = new float[MarbleCount];
        corneringTrait = new float[MarbleCount];

        pipelineRenderers = new GameObject[MarbleCount];
        visualKeys = new VisualKey[MarbleCount];
        ResolveArtPipeline();

        for (var i = 0; i < MarbleCount; i++)
        {
            var identity = IdentityService.Create(nextEntityId++, i % 3, "marble", 6, config?.seed ?? 0, "MarbleRace");
            var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, identity.teamId);

            var marble = new GameObject($"Sim_{identity.entityId:0000}");
            marble.transform.SetParent(groupRoot, false);

            var visualKey = VisualKeyBuilder.Create("MarbleRace", "marble", identity.entityId, "marble", "roll", FacingMode.Auto, identity.teamId);
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

            var sr = marble.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 24;
            }

            var idx = currentTrack.Wrap(i * (currentTrack.SampleCount / MarbleCount));
            var laneJitter = rng.Range(-0.55f, 0.55f);
            positions[i] = currentTrack.Center[idx] + currentTrack.Normal[idx] * (currentTrack.HalfWidth[idx] * laneJitter);
            velocities[i] = currentTrack.Tangent[idx] * rng.Range(2f, 4f);

            baseSpeedTrait[i] = rng.Range(7.2f, 10.5f);
            aggressionTrait[i] = rng.Range(0f, 1f);
            laneBiasTrait[i] = rng.Range(-1f, 1f);
            avoidanceTrait[i] = rng.Range(0.2f, 1f);
            draftingTrait[i] = rng.Range(0.1f, 1f);
            corneringTrait[i] = rng.Range(0.2f, 1f);
            targetSpeeds[i] = baseSpeedTrait[i];
            steeringAccels[i] = rng.Range(9f, 14f);

            closestTrackIndex[i] = idx;
            lastClosestIndex[i] = idx;
            previousLapLogged[i] = -1;
            lapArmed[i] = true;
            marble.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);

            marbles[i] = marble.transform;
            identities[i] = identity;
            visualKeys[i] = visualKey;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(MarbleRaceRunner)} spawn[{i}] {identity}");
            }
        }

        raceState = RacePhase.Ready;
        winnerIndex = -1;
        finishTime = 0f;
        elapsedTime = 0f;
    }

    private void CleanupLegacyTrackObjects()
    {
        if (sceneGraph?.DebugRoot != null)
        {
            DestroyIfFound(sceneGraph.DebugRoot, "TrackDebug");
            var debugChildCount = sceneGraph.DebugRoot.childCount;
            for (var i = debugChildCount - 1; i >= 0; i--)
            {
                var child = sceneGraph.DebugRoot.GetChild(i);
                if (child.name.Contains("Boundary"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        DestroyAllByName(transform.root, "TrackSurfaceStamps");
    }

    private static void DestroyIfFound(Transform parent, string name)
    {
        var found = parent.Find(name);
        if (found != null)
        {
            Destroy(found.gameObject);
        }
    }

    private static void DestroyAllByName(Transform root, string name)
    {
        if (root == null)
        {
            return;
        }

        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name == name)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyAllByName(child, name);
            }
        }
    }

    private int FindClosestTrackIndex(Vector2 position)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < currentTrack.SampleCount; i++)
        {
            var d = (position - currentTrack.Center[i]).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private void UpdateClosestTrackIndex(int marbleIndex, int window)
    {
        var current = closestTrackIndex[marbleIndex];
        var bestIndex = current;
        var bestDistSq = (positions[marbleIndex] - currentTrack.Center[current]).sqrMagnitude;
        for (var o = -window; o <= window; o++)
        {
            var idx = currentTrack.Wrap(current + o);
            var distSq = (positions[marbleIndex] - currentTrack.Center[idx]).sqrMagnitude;
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
        var lookAhead = 7 + Mathf.RoundToInt(Mathf.Clamp(baseSpeedTrait[i] - 6f, 0f, 4f) * 2f);
        var targetIdx = currentTrack.Wrap(idx + lookAhead);

        var desiredLane = laneBiasTrait[i] * currentTrack.HalfWidth[targetIdx] * 0.33f;
        var ahead = FindNearestAhead(i, 34);
        if (ahead >= 0)
        {
            var aheadDelta = currentTrack.ForwardDelta(idx, closestTrackIndex[ahead]);
            if (aheadDelta > 0 && aheadDelta < 22)
            {
                var side = ChoosePassSide(i, ahead, targetIdx);
                desiredLane += side * currentTrack.HalfWidth[targetIdx] * Mathf.Lerp(0.30f, 0.92f, aggressionTrait[i]);
            }
        }

        desiredLane = Mathf.Clamp(desiredLane, -currentTrack.HalfWidth[targetIdx] * 0.95f, currentTrack.HalfWidth[targetIdx] * 0.95f);
        desiredLaneOffset[i] = Mathf.MoveTowards(desiredLaneOffset[i], desiredLane, dt * 3f);

        var targetPoint = currentTrack.Center[targetIdx] + currentTrack.Normal[targetIdx] * desiredLaneOffset[i];
        var desiredDir = (targetPoint - positions[i]).normalized;
        var cornerPenalty = currentTrack.Curvature[targetIdx] * Mathf.Lerp(0.65f, 0.28f, corneringTrait[i]);
        var desiredSpeed = baseSpeedTrait[i] * (1f - cornerPenalty) + ComputeDraftBonus(i) + aggressionTrait[i] * 0.18f;
        targetSpeeds[i] = Mathf.Clamp(desiredSpeed, 4f, 11.4f);

        var desiredVel = desiredDir * targetSpeeds[i] + ComputeAvoidanceRepulsion(i);
        if (gravityStrength > 0f)
        {
            desiredVel += currentTrack.Tangent[targetIdx] * (gravityStrength * 0.03f);
        }

        velocities[i] = Vector2.MoveTowards(velocities[i], desiredVel, steeringAccels[i] * dt);
        velocities[i] *= 1f - Mathf.Clamp01(rollingFriction * dt);
        velocities[i] = Vector2.ClampMagnitude(velocities[i], maxSpeed);
    }

    private int FindNearestAhead(int i, int maxDelta)
    {
        var idx = closestTrackIndex[i];
        var best = -1;
        var bestDelta = int.MaxValue;
        for (var j = 0; j < marbles.Length; j++)
        {
            if (j == i || marbles[j] == null)
            {
                continue;
            }

            var delta = currentTrack.ForwardDelta(idx, closestTrackIndex[j]);
            if (delta > 0 && delta < maxDelta && delta < bestDelta)
            {
                bestDelta = delta;
                best = j;
            }
        }

        return best;
    }

    private float ChoosePassSide(int me, int ahead, int targetIdx)
    {
        var myOffset = Vector2.Dot(positions[me] - currentTrack.Center[targetIdx], currentTrack.Normal[targetIdx]);
        var aheadOffset = Vector2.Dot(positions[ahead] - currentTrack.Center[targetIdx], currentTrack.Normal[targetIdx]);
        if (Mathf.Abs(aheadOffset) < currentTrack.HalfWidth[targetIdx] * 0.3f)
        {
            return myOffset >= 0f ? -1f : 1f;
        }

        return aheadOffset > 0f ? -1f : 1f;
    }

    private float ComputeDraftBonus(int i)
    {
        var ahead = FindNearestAhead(i, 18);
        if (ahead < 0)
        {
            return 0f;
        }

        var dist = Vector2.Distance(positions[i], positions[ahead]);
        var t = Mathf.InverseLerp(12f, 2.5f, dist);
        return t * 1.2f * draftingTrait[i];
    }

    private Vector2 ComputeAvoidanceRepulsion(int i)
    {
        var repel = Vector2.zero;
        for (var j = 0; j < marbles.Length; j++)
        {
            if (j == i || marbles[j] == null)
            {
                continue;
            }

            var delta = positions[i] - positions[j];
            var sq = delta.sqrMagnitude;
            if (sq < 0.0001f || sq > 7f)
            {
                continue;
            }

            repel += delta.normalized * ((7f - sq) / 7f);
        }

        return repel * avoidanceTrait[i] * 1.8f;
    }

    private void ApplyCorridorBounds(int marbleIndex)
    {
        var idx = closestTrackIndex[marbleIndex];
        var center = currentTrack.Center[idx];
        var normal = currentTrack.Normal[idx];
        var half = currentTrack.HalfWidth[idx];
        var lateral = Vector2.Dot(positions[marbleIndex] - center, normal);

        if (Mathf.Abs(lateral) <= half)
        {
            return;
        }

        var clamped = Mathf.Clamp(lateral, -half, half);
        var correction = (clamped - lateral);
        positions[marbleIndex] += normal * correction;

        var nVel = Vector2.Dot(velocities[marbleIndex], normal);
        velocities[marbleIndex] -= normal * nVel * 1.75f;
    }

    private void ApplySoftArenaBounds(int marbleIndex, float dt)
    {
        var pos = positions[marbleIndex];
        var vel = velocities[marbleIndex];

        if (Mathf.Abs(pos.x) > halfWidth)
        {
            pos.x = Mathf.Clamp(pos.x, -halfWidth, halfWidth);
            vel.x *= -0.45f;
        }

        if (Mathf.Abs(pos.y) > halfHeight)
        {
            pos.y = Mathf.Clamp(pos.y, -halfHeight, halfHeight);
            vel.y *= -0.45f;
        }

        vel *= 1f - Mathf.Clamp01(dt * 0.1f);
        positions[marbleIndex] = pos;
        velocities[marbleIndex] = vel;
    }

    private void RescueMarble(int marbleIndex)
    {
        var idx = closestTrackIndex[marbleIndex];
        var laneClamp = Mathf.Clamp(desiredLaneOffset[marbleIndex], -currentTrack.HalfWidth[idx] * 0.8f, currentTrack.HalfWidth[idx] * 0.8f);
        positions[marbleIndex] = currentTrack.Center[idx] + currentTrack.Normal[idx] * laneClamp;
        velocities[marbleIndex] = currentTrack.Tangent[idx] * Mathf.Max(2f, targetSpeeds[marbleIndex] * 0.4f);
        stuckTimer[marbleIndex] = 0f;
    }

    private void UpdateLapProgress(int index)
    {
        var prev = lastClosestIndex[index];
        var curr = closestTrackIndex[index];
        if (!lapArmed[index] && curr > LapArmDistance)
        {
            lapArmed[index] = true;
        }

        if (!lapArmed[index])
        {
            return;
        }

        if (prev > currentTrack.SampleCount - LapCrossWindow && curr < LapCrossWindow)
        {
            lapCount[index]++;
            lapArmed[index] = false;
            if (logLapEvents && lapCount[index] != previousLapLogged[index])
            {
                previousLapLogged[index] = lapCount[index];
                Debug.Log($"{nameof(MarbleRaceRunner)} lap marble={identities[index].entityId} lap={lapCount[index]}");
            }

            if (winnerIndex < 0 && lapCount[index] >= lapsToWin)
            {
                winnerIndex = index;
                finishTime = elapsedTime;
                raceState = RacePhase.Finished;
            }
        }
    }

    private void UpdateProgress(int index)
    {
        var idx = closestTrackIndex[index];
        var lateral = Vector2.Dot(positions[index] - currentTrack.Center[idx], currentTrack.Normal[idx]);
        var lateralPenalty = Mathf.Abs(lateral) * 0.05f;
        progressScore[index] = lapCount[index] * currentTrack.SampleCount + idx - lateralPenalty;
    }

    private int FillOrderedIndicesByProgress(int[] indices)
    {
        for (var i = 0; i < marbles.Length; i++)
        {
            indices[i] = i;
        }

        System.Array.Sort(indices, (a, b) => progressScore[b].CompareTo(progressScore[a]));
        return marbles.Length;
    }

    private void ResolveArtPipeline()
    {
        artSelector = Object.FindFirstObjectByType<ArtModeSelector>() ?? Object.FindAnyObjectByType<ArtModeSelector>();
        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
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
