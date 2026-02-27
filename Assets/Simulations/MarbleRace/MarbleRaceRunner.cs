using System.Text;
using UnityEngine;

public class MarbleRaceRunner : MonoBehaviour, ITickableSimulationRunner
{
    public enum RacePhase
    {
        Ready,
        Racing,
        Finished,
        Cooldown
    }

    private const int MarbleCount = 12;
    private const int LapsToWin = 3;
    private const int TrackBuildAttempts = 6;

    [SerializeField] private float maxSpeed = 12.5f;
    [SerializeField] private float steeringAcceleration = 13f;
    [SerializeField] private float friction = 0.22f;

    private Transform[] marbles;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] readyPositions;
    private Vector2[] velocities;
    private float[] progress;
    private int[] lapCount;
    private int[] closestTrackIndex;
    private float[] laneOffset;
    private float[] desiredTopSpeed;
    private float[] stuckTimer;
    private int[] lastClosestIndex;

    private SimulationSceneGraph sceneGraph;
    private MarbleRaceTrack track;
    private MarbleRaceTrackGenerator trackGenerator;
    private MarbleRaceTrackRenderer trackRenderer;

    private float arenaHalfWidth;
    private float arenaHalfHeight;
    private RacePhase raceState;
    private int winnerIndex;
    private float elapsedTime;
    private float finishTime;
    private int nextEntityId;

    private readonly int[] rankingBuffer = new int[MarbleCount];

    public RacePhase CurrentPhase => raceState;
    public bool IsReady => raceState == RacePhase.Ready;
    public bool IsRacing => raceState == RacePhase.Racing;
    public bool IsFinished => raceState == RacePhase.Finished || raceState == RacePhase.Cooldown;
    public int WinnerEntityId => winnerIndex >= 0 && identities != null && winnerIndex < identities.Length ? identities[winnerIndex].entityId : -1;
    public float WinnerFinishTime => finishTime;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();

        CleanupLegacyGraphChildren();

        var simulationRootGo = GameObject.Find("SimulationRoot");
        if (simulationRootGo == null)
        {
            Debug.LogError("[MarbleRace] Missing SimulationRoot. Cannot initialize MarbleRaceRunner.");
            return;
        }

        sceneGraph = SimulationSceneGraph.Ensure(simulationRootGo.transform);
        if (sceneGraph == null)
        {
            Debug.LogError("[MarbleRace] Failed to resolve SimulationSceneGraph at SimulationRoot.");
            return;
        }

        CleanupLegacyTrackObjects();

        var arenaRoot = sceneGraph.WorldRoot != null ? sceneGraph.WorldRoot.Find("ArenaRoot") : null;
        if (arenaRoot == null && sceneGraph.WorldRoot != null)
        {
            var arenaRootGo = new GameObject("ArenaRoot");
            arenaRootGo.transform.SetParent(sceneGraph.WorldRoot, false);
            arenaRoot = arenaRootGo.transform;
        }

        var arenaWidth = Mathf.Max(1f, config != null && config.world != null ? config.world.arenaWidth : 64f);
        var arenaHeight = Mathf.Max(1f, config != null && config.world != null ? config.world.arenaHeight : 64f);
        arenaHalfWidth = arenaWidth * 0.5f;
        arenaHalfHeight = arenaHeight * 0.5f;

        trackGenerator ??= new MarbleRaceTrackGenerator();
        trackRenderer ??= new MarbleRaceTrackRenderer();

        var seed = config != null ? config.seed : 0;
        var fallbackUsed = false;
        track = BuildTrack(seed, arenaWidth, arenaHeight, out fallbackUsed);

        if (track == null || track.SampleCount <= 0)
        {
            track = trackGenerator.BuildFallbackRoundedRectangle(arenaHalfWidth, arenaHalfHeight);
            fallbackUsed = true;
        }

        var minHalfWidth = float.MaxValue;
        var maxHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            minHalfWidth = Mathf.Min(minHalfWidth, track.HalfWidth[i]);
            maxHalfWidth = Mathf.Max(maxHalfWidth, track.HalfWidth[i]);
        }

        Debug.Log($"[MarbleRace] Track built: samples={track.SampleCount} minHalfWidth={minHalfWidth:F2} maxHalfWidth={maxHalfWidth:F2} fallback={fallbackUsed}");

        trackRenderer.Apply(arenaRoot, track);
        BuildMarbles(seed);

        raceState = RacePhase.Ready;
        winnerIndex = -1;
        elapsedTime = 0f;
        finishTime = 0f;
    }

    public void Tick(int tickIndex, float dt)
    {
        if (marbles == null || track == null || track.SampleCount <= 0)
        {
            return;
        }

        if (raceState == RacePhase.Ready)
        {
            for (var i = 0; i < MarbleCount; i++)
            {
                positions[i] = readyPositions[i];
                velocities[i] = Vector2.zero;
                lapCount[i] = 0;
                closestTrackIndex[i] = 0;
                lastClosestIndex[i] = 0;
                progress[i] = i * -0.001f;
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            }

            return;
        }

        elapsedTime += dt;

        if (raceState == RacePhase.Finished)
        {
            if (elapsedTime >= finishTime + 2.5f)
            {
                raceState = RacePhase.Cooldown;
            }

            for (var i = 0; i < MarbleCount; i++)
            {
                velocities[i] = Vector2.MoveTowards(velocities[i], Vector2.zero, dt * 4f);
                positions[i] += velocities[i] * dt;
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            }

            return;
        }

        if (raceState == RacePhase.Cooldown)
        {
            for (var i = 0; i < MarbleCount; i++)
            {
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            }

            return;
        }

        for (var i = 0; i < MarbleCount; i++)
        {
            UpdateClosestIndex(i, 20);
            SimulateMarble(i, dt);
            ApplyCorridor(i);
            ClampArena(i);
            UpdateClosestIndex(i, 22);
            CheckRescue(i, dt);
            UpdateLapAndProgress(i);
            lastClosestIndex[i] = closestTrackIndex[i];
            marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
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
        readyPositions = null;
        velocities = null;
        progress = null;
        lapCount = null;
        closestTrackIndex = null;
        laneOffset = null;
        desiredTopSpeed = null;
        stuckTimer = null;
        lastClosestIndex = null;

        track = null;
        raceState = RacePhase.Ready;
        winnerIndex = -1;
        elapsedTime = 0f;
        finishTime = 0f;
    }

    public void StartRace()
    {
        if (raceState != RacePhase.Ready || track == null)
        {
            return;
        }

        raceState = RacePhase.Racing;
        for (var i = 0; i < MarbleCount; i++)
        {
            var forward = track.Tangent[0];
            var push = Mathf.Lerp(3.1f, 4.3f, i / (float)(MarbleCount - 1));
            velocities[i] = forward * push;
        }
    }

    public void FillLeaderboard(StringBuilder sb, int maxEntries, bool final)
    {
        if (sb == null)
        {
            return;
        }

        sb.Clear();
        if (marbles == null)
        {
            sb.Append("No marbles.");
            return;
        }

        var count = FillRankingBuffer(rankingBuffer);
        var top = Mathf.Clamp(maxEntries <= 0 ? count : maxEntries, 1, count);

        for (var rank = 0; rank < top; rank++)
        {
            var i = rankingBuffer[rank];
            if (rank > 0)
            {
                sb.Append('\n');
            }

            sb.Append('#').Append(rank + 1)
                .Append(" M").Append(identities[i].entityId)
                .Append(" L").Append(lapCount[i]);

            if (!final)
            {
                sb.Append(" T").Append(closestTrackIndex[i]);
            }
        }
    }

    public string GetWinnerLine()
    {
        if (winnerIndex < 0 || identities == null)
        {
            return string.Empty;
        }

        return $"WINNER: M{identities[winnerIndex].entityId} time={finishTime:F2}s laps={lapCount[winnerIndex]}";
    }

    private void BuildMarbles(int seed)
    {
        var rng = new SeededRng(seed ^ 0x5162AB);

        marbles = new Transform[MarbleCount];
        identities = new EntityIdentity[MarbleCount];
        positions = new Vector2[MarbleCount];
        readyPositions = new Vector2[MarbleCount];
        velocities = new Vector2[MarbleCount];
        progress = new float[MarbleCount];
        lapCount = new int[MarbleCount];
        closestTrackIndex = new int[MarbleCount];
        laneOffset = new float[MarbleCount];
        desiredTopSpeed = new float[MarbleCount];
        stuckTimer = new float[MarbleCount];
        lastClosestIndex = new int[MarbleCount];

        var entitiesRoot = sceneGraph != null ? sceneGraph.EntitiesRoot : transform;
        for (var i = 0; i < MarbleCount; i++)
        {
            var identity = IdentityService.Create(nextEntityId++, i % 3, "marble", 6, seed, "MarbleRace");
            var groupRoot = SceneGraphUtil.EnsureEntityGroup(entitiesRoot, identity.teamId);

            var marble = new GameObject($"Sim_{identity.entityId:0000}");
            marble.transform.SetParent(groupRoot, false);

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(marble.transform, false);
            EntityIconFactory.BuildMarble(iconRoot.transform, identity);

            var spriteRenderer = marble.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 24;
            }

            desiredTopSpeed[i] = rng.Range(7.6f, 10.5f);
            laneOffset[i] = rng.Range(-0.35f, 0.35f);

            identities[i] = identity;
            marbles[i] = marble.transform;
        }

        PlaceStartGrid();
    }

    private void PlaceStartGrid()
    {
        var startPos = track.Center[0];
        var forward = track.Tangent[0];
        var side = track.Normal[0];

        const int cols = 4;
        const int rows = 3;
        var laneSpacing = Mathf.Min(track.HalfWidth[0] * 0.55f, 1.35f);
        var rowSpacing = Mathf.Max(0.9f, track.HalfWidth[0] * 0.7f);

        var index = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (index >= MarbleCount)
                {
                    break;
                }

                var lane = (c - (cols - 1) * 0.5f) * laneSpacing;
                var behind = (r + 1) * rowSpacing;
                var spawn = startPos + (side * lane) - (forward * behind);

                positions[index] = spawn;
                readyPositions[index] = spawn;
                velocities[index] = Vector2.zero;
                lapCount[index] = 0;
                closestTrackIndex[index] = 0;
                progress[index] = index * -0.001f;
                lastClosestIndex[index] = 0;
                marbles[index].localPosition = new Vector3(spawn.x, spawn.y, 0f);
                index++;
            }
        }
    }

    private void SimulateMarble(int i, float dt)
    {
        var idx = closestTrackIndex[i];
        var lookAhead = 8 + Mathf.RoundToInt(Mathf.Clamp(desiredTopSpeed[i] - 8f, 0f, 3f) * 2f);
        var targetIdx = track.Wrap(idx + lookAhead);

        var passBias = ComputeOvertakeBias(i, idx, targetIdx);
        var maxLane = track.HalfWidth[targetIdx] * 0.88f;
        var desiredLane = Mathf.Clamp((laneOffset[i] + passBias) * track.HalfWidth[targetIdx] * 0.55f, -maxLane, maxLane);
        var currentLane = Vector2.Dot(positions[i] - track.Center[targetIdx], track.Normal[targetIdx]);
        var laneError = desiredLane - currentLane;

        var targetPoint = track.Center[targetIdx] + (track.Normal[targetIdx] * desiredLane);
        var desiredDirection = (targetPoint - positions[i]).normalized;

        var cornerPenalty = Mathf.Clamp01(track.Curvature[targetIdx] * 3f);
        var desiredSpeed = desiredTopSpeed[i] * Mathf.Lerp(1f, 0.72f, cornerPenalty);
        desiredSpeed = Mathf.Clamp(desiredSpeed + ComputeDraftBoost(i), 4.5f, maxSpeed);

        var desiredVelocity = desiredDirection * desiredSpeed;
        desiredVelocity += track.Normal[targetIdx] * (laneError * 1.4f);

        velocities[i] = Vector2.MoveTowards(velocities[i], desiredVelocity, steeringAcceleration * dt);
        velocities[i] *= 1f - Mathf.Clamp01(friction * dt);
        velocities[i] = Vector2.ClampMagnitude(velocities[i], maxSpeed);

        positions[i] += velocities[i] * dt;
    }

    private float ComputeOvertakeBias(int i, int idx, int targetIdx)
    {
        var nearestAhead = -1;
        var nearestDelta = int.MaxValue;
        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            var delta = track.ForwardDelta(idx, closestTrackIndex[j]);
            if (delta > 0 && delta < 26 && delta < nearestDelta)
            {
                nearestAhead = j;
                nearestDelta = delta;
            }
        }

        if (nearestAhead < 0)
        {
            return 0f;
        }

        var meOffset = Vector2.Dot(positions[i] - track.Center[targetIdx], track.Normal[targetIdx]);
        var aheadOffset = Vector2.Dot(positions[nearestAhead] - track.Center[targetIdx], track.Normal[targetIdx]);

        if (Mathf.Abs(aheadOffset) < track.HalfWidth[targetIdx] * 0.25f)
        {
            return meOffset >= 0f ? -0.9f : 0.9f;
        }

        return aheadOffset > 0f ? -0.8f : 0.8f;
    }

    private float ComputeDraftBoost(int i)
    {
        var idx = closestTrackIndex[i];
        for (var j = 0; j < MarbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            var delta = track.ForwardDelta(idx, closestTrackIndex[j]);
            if (delta <= 0 || delta > 16)
            {
                continue;
            }

            var distance = Vector2.Distance(positions[i], positions[j]);
            if (distance < 0.001f || distance > 5f)
            {
                continue;
            }

            return Mathf.Lerp(0.1f, 1f, 1f - Mathf.InverseLerp(0.8f, 5f, distance));
        }

        return 0f;
    }

    private void ApplyCorridor(int i)
    {
        var idx = closestTrackIndex[i];
        var center = track.Center[idx];
        var normal = track.Normal[idx];
        var halfWidth = track.HalfWidth[idx];

        var lateral = Vector2.Dot(positions[i] - center, normal);
        if (Mathf.Abs(lateral) <= halfWidth)
        {
            return;
        }

        var clamped = Mathf.Clamp(lateral, -halfWidth, halfWidth);
        var push = clamped - lateral;
        positions[i] += normal * push;

        var lateralVelocity = Vector2.Dot(velocities[i], normal);
        velocities[i] -= normal * lateralVelocity * 1.8f;
    }

    private void ClampArena(int i)
    {
        var pos = positions[i];
        var vel = velocities[i];

        if (Mathf.Abs(pos.x) > arenaHalfWidth)
        {
            pos.x = Mathf.Clamp(pos.x, -arenaHalfWidth, arenaHalfWidth);
            vel.x *= -0.3f;
        }

        if (Mathf.Abs(pos.y) > arenaHalfHeight)
        {
            pos.y = Mathf.Clamp(pos.y, -arenaHalfHeight, arenaHalfHeight);
            vel.y *= -0.3f;
        }

        positions[i] = pos;
        velocities[i] = vel;
    }

    private void CheckRescue(int i, float dt)
    {
        if (velocities[i].magnitude > 0.45f)
        {
            stuckTimer[i] = 0f;
            return;
        }

        stuckTimer[i] += dt;
        if (stuckTimer[i] < 1f)
        {
            return;
        }

        var idx = closestTrackIndex[i];
        var snap = track.Center[idx] + (track.Normal[idx] * Mathf.Clamp(laneOffset[i], -0.6f, 0.6f) * track.HalfWidth[idx] * 0.55f);
        positions[i] = snap;
        velocities[i] = track.Tangent[idx] * 2.8f;
        stuckTimer[i] = 0f;
    }

    private void UpdateLapAndProgress(int i)
    {
        var idx = closestTrackIndex[i];
        var prev = lastClosestIndex[i];

        if (prev > track.SampleCount - 8 && idx < 8)
        {
            lapCount[i]++;
            if (winnerIndex < 0 && lapCount[i] >= LapsToWin)
            {
                winnerIndex = i;
                finishTime = elapsedTime;
                raceState = RacePhase.Finished;
            }
        }

        var lateral = Mathf.Abs(Vector2.Dot(positions[i] - track.Center[idx], track.Normal[idx]));
        progress[i] = (lapCount[i] * track.SampleCount) + idx - (lateral * 0.05f);
    }

    private void UpdateClosestIndex(int i, int window)
    {
        var start = closestTrackIndex[i];
        var bestIndex = start;
        var bestSq = float.MaxValue;

        for (var offset = -window; offset <= window; offset++)
        {
            var idx = track.Wrap(start + offset);
            var sq = (positions[i] - track.Center[idx]).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestIndex = idx;
            }
        }

        closestTrackIndex[i] = bestIndex;
    }

    private int FillRankingBuffer(int[] indices)
    {
        for (var i = 0; i < MarbleCount; i++)
        {
            indices[i] = i;
        }

        System.Array.Sort(indices, (a, b) => progress[b].CompareTo(progress[a]));
        return MarbleCount;
    }

    private void CleanupLegacyTrackObjects()
    {
        if (sceneGraph != null && sceneGraph.DebugRoot != null)
        {
            DestroyIfFound(sceneGraph.DebugRoot, "TrackDebug");
        }

        var arenaRoot = sceneGraph != null && sceneGraph.WorldRoot != null ? sceneGraph.WorldRoot.Find("ArenaRoot") : null;
        if (arenaRoot != null)
        {
            DestroyIfFound(arenaRoot, "TrackDebug");
        }

        DestroyByNameRecursive(transform.root, "TrackSurfaceStamps");
        DestroyByNameRecursive(transform.root, "SanityStamp");
    }

    private void CleanupLegacyGraphChildren()
    {
        var legacyRoots = new[] { "WorldRoot", "RunnerRoot", "EntitiesRoot", "DebugRoot" };
        for (var i = 0; i < legacyRoots.Length; i++)
        {
            var legacy = transform.Find(legacyRoots[i]);
            if (legacy != null)
            {
                Destroy(legacy.gameObject);
            }
        }
    }

    private MarbleRaceTrack BuildTrack(int seed, float arenaWidth, float arenaHeight, out bool fallbackUsed)
    {
        fallbackUsed = false;
        var halfW = arenaWidth * 0.5f;
        var halfH = arenaHeight * 0.5f;
        var bounds = new Rect(-halfW, -halfH, arenaWidth, arenaHeight);

        var templateStart = Mathf.Abs(seed) % 3;
        for (var attempt = 0; attempt < TrackBuildAttempts; attempt++)
        {
            var attemptSeed = unchecked(seed ^ (int)0x9E3779B9 ^ (attempt * 7919));
            var rng = new SeededRng(attemptSeed);
            var template = (templateStart + attempt + rng.NextInt(0, 3)) % 3;
            var built = trackGenerator.Build(halfW, halfH, rng, template);
            if (built == null || built.SampleCount <= 0)
            {
                continue;
            }

            var widthJitterScale = Mathf.Lerp(1f, 0.75f, attempt / (float)Mathf.Max(1, TrackBuildAttempts - 1));
            var processed = PostProcessTrack(built, arenaWidth, arenaHeight, widthJitterScale);
            if (ValidateTrack(processed, bounds))
            {
                return processed;
            }
        }

        fallbackUsed = true;
        var fallback = trackGenerator.BuildFallbackRoundedRectangle(halfW, halfH);
        return PostProcessTrack(fallback, arenaWidth, arenaHeight, 0.8f);
    }

    private static MarbleRaceTrack PostProcessTrack(MarbleRaceTrack source, float arenaWidth, float arenaHeight, float widthJitterScale)
    {
        if (source == null || source.SampleCount <= 0)
        {
            return source;
        }

        var n = source.SampleCount;
        var widths = new float[n];
        var minArena = Mathf.Min(arenaWidth, arenaHeight);
        var baseHalfWidth = Mathf.Clamp(minArena * 0.04f, 1.25f, 4f);
        var minWidth = baseHalfWidth * 0.75f;
        var maxWidth = baseHalfWidth * 1.75f;

        for (var i = 0; i < n; i++)
        {
            var scaled = Mathf.Lerp(baseHalfWidth, source.HalfWidth[i], Mathf.Clamp01(widthJitterScale));
            widths[i] = Mathf.Clamp(scaled, minWidth, maxWidth);
        }

        var startIndex = FindStartOnStraight(source.Curvature);
        return RotateTrack(source, widths, startIndex);
    }

    private static int FindStartOnStraight(float[] curvature)
    {
        if (curvature == null || curvature.Length == 0)
        {
            return 0;
        }

        var n = curvature.Length;
        var window = Mathf.Clamp(n / 32, 3, 12);
        var bestIndex = 0;
        var bestScore = float.MaxValue;

        for (var i = 0; i < n; i++)
        {
            var score = 0f;
            for (var j = -window; j <= window; j++)
            {
                var idx = (i + j + n) % n;
                score += curvature[idx];
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static MarbleRaceTrack RotateTrack(MarbleRaceTrack source, float[] widths, int startIndex)
    {
        var n = source.SampleCount;
        if (n <= 0 || startIndex <= 0)
        {
            return new MarbleRaceTrack(source.Center, source.Tangent, source.Normal, widths, source.Curvature);
        }

        var center = new Vector2[n];
        var tangent = new Vector2[n];
        var normal = new Vector2[n];
        var curvature = new float[n];
        var rotatedWidths = new float[n];

        for (var i = 0; i < n; i++)
        {
            var src = (i + startIndex) % n;
            center[i] = source.Center[src];
            tangent[i] = source.Tangent[src];
            normal[i] = source.Normal[src];
            curvature[i] = source.Curvature[src];
            rotatedWidths[i] = widths[src];
        }

        return new MarbleRaceTrack(center, tangent, normal, rotatedWidths, curvature);
    }

    private static bool ValidateTrack(MarbleRaceTrack candidate, Rect boundsRect)
    {
        if (candidate == null || candidate.SampleCount < 16)
        {
            return false;
        }

        var center = candidate.Center;
        var n = candidate.SampleCount;
        var margin = Mathf.Min(boundsRect.width, boundsRect.height) * 0.1f;
        var innerBounds = new Rect(
            boundsRect.xMin + margin,
            boundsRect.yMin + margin,
            Mathf.Max(0.01f, boundsRect.width - (margin * 2f)),
            Mathf.Max(0.01f, boundsRect.height - (margin * 2f)));

        for (var i = 0; i < n; i++)
        {
            if (!innerBounds.Contains(center[i]))
            {
                return false;
            }

            var next = center[(i + 1) % n];
            if ((next - center[i]).sqrMagnitude <= 0.0004f)
            {
                return false;
            }

            if (candidate.HalfWidth[i] <= 0f)
            {
                return false;
            }

            var boundarySpan = Vector2.Dot((candidate.Normal[i] * candidate.HalfWidth[i]) * 2f, candidate.Normal[i]);
            if (boundarySpan <= 0.01f)
            {
                return false;
            }
        }

        var prevTangent = candidate.Tangent[n - 1];
        for (var i = 0; i < n; i++)
        {
            if (Vector2.Dot(candidate.Tangent[i], prevTangent) < 0.2f)
            {
                return false;
            }

            prevTangent = candidate.Tangent[i];
        }

        return !HasCoarseIntersection(center, 8);
    }

    private static bool HasCoarseIntersection(Vector2[] center, int stride)
    {
        var n = center.Length;
        for (var i = 0; i < n; i += stride)
        {
            var a1 = center[i];
            var a2 = center[(i + stride) % n];

            for (var j = i + (2 * stride); j < n; j += stride)
            {
                if (Mathf.Abs(i - j) <= stride)
                {
                    continue;
                }

                var b1 = center[j];
                var b2 = center[(j + stride) % n];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        if (Mathf.Abs(denom) < 0.0001f)
        {
            return false;
        }

        var t = Cross(q1 - p1, s) / denom;
        var u = Cross(q1 - p1, r) / denom;
        return t > 0f && t < 1f && u > 0f && u < 1f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static void DestroyIfFound(Transform parent, string childName)
    {
        if (parent == null)
        {
            return;
        }

        var child = parent.Find(childName);
        if (child != null)
        {
            Object.Destroy(child.gameObject);
        }
    }

    private static void DestroyByNameRecursive(Transform root, string name)
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
                Object.Destroy(child.gameObject);
                continue;
            }

            DestroyByNameRecursive(child, name);
        }
    }
}
