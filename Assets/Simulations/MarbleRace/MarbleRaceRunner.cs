using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public class MarbleRaceRunner : MonoBehaviour, ITickableSimulationRunner
{
    public enum RacePhase
    {
        Ready,
        Racing,
        Finished,
        Cooldown
    }

    private const float Z_UNDER = 0f;
    private const float Z_OVER = 0.02f;
    private const float MarbleRadius = 0.55f;
    private const float WallRestitution = 0.35f;
    private const float WallFriction = 0.20f;
    private static readonly string[] LegacyDecorTrackObjectNames =
    {
        "StartFinishTile",
        "TrackLane",
        "TrackInnerBorder",
        "TrackOuterBorder",
        "StartFinishLine"
    };

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
    private SortingGroup[] marbleSortingGroups;
    private SpriteRenderer[] marbleSpriteRenderers;

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

    private int marbleCount = 12;
    private int lapsToWin = 3;
    private int trackTemplate = -1;
    private int trackVariant = 0;
    private int simulationSeed;
    private float simulationArenaWidth;
    private float simulationArenaHeight;
    private int[] rankingBuffer;

    public RacePhase CurrentPhase => raceState;
    public bool IsReady => raceState == RacePhase.Ready;
    public bool IsRacing => raceState == RacePhase.Racing;
    public bool IsFinished => raceState == RacePhase.Finished || raceState == RacePhase.Cooldown;
    public int LiveMarbleCount => marbles?.Length ?? 0;
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
        }

        var arenaWidth = Mathf.Max(1f, config != null && config.world != null ? config.world.arenaWidth : 64f);
        var arenaHeight = Mathf.Max(1f, config != null && config.world != null ? config.world.arenaHeight : 64f);
        arenaHalfWidth = arenaWidth * 0.5f;
        arenaHalfHeight = arenaHeight * 0.5f;

        trackGenerator ??= new MarbleRaceTrackGenerator();
        trackRenderer ??= new MarbleRaceTrackRenderer();

        var seed = config != null ? config.seed : 0;
        var seedChanged = seed != simulationSeed;
        simulationSeed = seed;
        marbleCount = Mathf.Max(2, config?.marbleRace?.marbleCount ?? 12);
        lapsToWin = Mathf.Max(1, config?.marbleRace?.laps ?? 3);
        trackTemplate = ResolveTrackTemplate(config?.marbleRace?.trackPreset);
        if (seedChanged)
        {
            trackVariant = 0;
        }
        simulationArenaWidth = arenaWidth;
        simulationArenaHeight = arenaHeight;
        var fallbackUsed = false;
        track = BuildTrack(seed, arenaWidth, arenaHeight, trackVariant, out fallbackUsed);

        if (track == null || track.SampleCount <= 0)
        {
            track = trackGenerator.BuildFallbackRoundedRectangle(arenaHalfWidth, arenaHalfHeight, trackVariant);
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

        trackRenderer.Apply(sceneGraph.DecorRoot, track);
        LogTrackValidation();
        BuildMarbles(seed);
        EnsureRankingBuffer(GetSafeCount());

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
            for (var i = 0; i < marbleCount; i++)
            {
                positions[i] = readyPositions[i];
                velocities[i] = Vector2.zero;
                lapCount[i] = 0;
                closestTrackIndex[i] = 0;
                lastClosestIndex[i] = 0;
                progress[i] = i * -0.001f;
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, GetMarbleZ(i));
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

            for (var i = 0; i < marbleCount; i++)
            {
                velocities[i] = Vector2.MoveTowards(velocities[i], Vector2.zero, dt * 4f);
                positions[i] += velocities[i] * dt;
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, GetMarbleZ(i));
            }

            return;
        }

        if (raceState == RacePhase.Cooldown)
        {
            for (var i = 0; i < marbleCount; i++)
            {
                marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, GetMarbleZ(i));
            }

            return;
        }

        for (var i = 0; i < marbleCount; i++)
        {
            UpdateClosestIndex(i, 20);
            SimulateMarble(i, dt);
            ApplyCorridor(i);
            ClampArena(i);
            UpdateClosestIndex(i, 22);
            SetMarbleRenderState(i, closestTrackIndex[i]);
            CheckRescue(i, dt);
            UpdateLapAndProgress(i);
            lastClosestIndex[i] = closestTrackIndex[i];
            marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, GetMarbleZ(i));
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
        CleanupLegacyTrackObjects();

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
        marbleSortingGroups = null;
        marbleSpriteRenderers = null;

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
        for (var i = 0; i < marbleCount; i++)
        {
            var forward = track.Tangent[0];
            var push = Mathf.Lerp(3.1f, 4.3f, i / (float)(marbleCount - 1));
            velocities[i] = forward * push;
        }
    }

    public void ForceNewTrack()
    {
        if (sceneGraph == null)
        {
            return;
        }

        simulationSeed = GenerateNextTrackSeed(simulationSeed);
        trackVariant++;
        Debug.Log($"[MarbleRace] ForceNewTrack seed={simulationSeed} trackVariant={trackVariant}");
        RebuildTrackAndResetToReady();
    }

    public void FillLeaderboard(StringBuilder sb, int maxEntries, bool final)
    {
        if (sb == null)
        {
            return;
        }

        sb.Clear();
        var count = GetSafeCount();
        if (count == 0)
        {
            sb.Append("No marbles.");
            return;
        }

        var buf = EnsureRankingBuffer(count);
        var rankedCount = FillRankingBuffer(buf, count);
        if (rankedCount <= 0)
        {
            sb.Append("No marbles.");
            return;
        }

        var top = Mathf.Clamp(maxEntries <= 0 ? rankedCount : maxEntries, 1, rankedCount);

        for (var rank = 0; rank < top; rank++)
        {
            var i = buf[rank];
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

        marbles = new Transform[marbleCount];
        identities = new EntityIdentity[marbleCount];
        positions = new Vector2[marbleCount];
        readyPositions = new Vector2[marbleCount];
        velocities = new Vector2[marbleCount];
        progress = new float[marbleCount];
        lapCount = new int[marbleCount];
        closestTrackIndex = new int[marbleCount];
        laneOffset = new float[marbleCount];
        desiredTopSpeed = new float[marbleCount];
        stuckTimer = new float[marbleCount];
        lastClosestIndex = new int[marbleCount];
        marbleSortingGroups = new SortingGroup[marbleCount];
        marbleSpriteRenderers = new SpriteRenderer[marbleCount];

        var entitiesRoot = sceneGraph != null ? sceneGraph.EntitiesRoot : transform;
        for (var i = 0; i < marbleCount; i++)
        {
            var identity = IdentityService.Create(nextEntityId++, i % 3, "marble", 6, seed, "MarbleRace");
            var groupRoot = SceneGraphUtil.EnsureEntityGroup(entitiesRoot, identity.teamId);

            var marble = new GameObject($"Sim_{identity.entityId:0000}");
            marble.transform.SetParent(groupRoot, false);

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(marble.transform, false);
            EntityIconFactory.BuildMarble(iconRoot.transform, identity);

            var sortingGroup = marble.GetComponentInChildren<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = marble.AddComponent<SortingGroup>();
            }

            var spriteRenderer = marble.GetComponentInChildren<SpriteRenderer>();
            marbleSortingGroups[i] = sortingGroup;
            marbleSpriteRenderers[i] = spriteRenderer;
            SetMarbleRenderState(i, 0);

            desiredTopSpeed[i] = rng.Range(7.6f, 10.5f);
            laneOffset[i] = rng.Range(-0.35f, 0.35f);

            identities[i] = identity;
            marbles[i] = marble.transform;
        }

        PlaceStartGrid();
        EnsureRankingBuffer(GetSafeCount());
    }

    private int GetSafeCount()
    {
        var n = marbles?.Length ?? 0;
        if (n <= 0)
        {
            return 0;
        }

        if (identities == null)
        {
            return 0;
        }

        n = Mathf.Min(n, identities.Length);
        if (positions != null)
        {
            n = Mathf.Min(n, positions.Length);
        }
        else
        {
            return 0;
        }

        if (progress != null)
        {
            n = Mathf.Min(n, progress.Length);
        }
        else
        {
            return 0;
        }

        if (lapCount != null)
        {
            n = Mathf.Min(n, lapCount.Length);
        }
        else
        {
            return 0;
        }

        if (closestTrackIndex != null)
        {
            n = Mathf.Min(n, closestTrackIndex.Length);
        }
        else
        {
            return 0;
        }

        if (lastClosestIndex != null)
        {
            n = Mathf.Min(n, lastClosestIndex.Length);
        }
        else
        {
            return 0;
        }

        return Mathf.Max(0, n);
    }

    private int[] EnsureRankingBuffer(int count)
    {
        if (count <= 0)
        {
            return System.Array.Empty<int>();
        }

        if (rankingBuffer == null || rankingBuffer.Length != count)
        {
            rankingBuffer = new int[count];
        }

        return rankingBuffer;
    }

    private void PlaceStartGrid()
    {
        var startPos = track.Center[0];
        var forward = track.Tangent[0];
        var side = track.Normal[0];

        var maxLane = Mathf.Max(0f, track.HalfWidth[0] - MarbleRadius - 0.25f);
        var cols = Mathf.Min(4, marbleCount);
        var minLaneSpacing = MarbleRadius * 2.1f;
        while (cols > 1)
        {
            var laneSpacing = (2f * maxLane) / (cols - 1);
            if (laneSpacing >= minLaneSpacing)
            {
                break;
            }

            cols--;
        }

        var rows = Mathf.CeilToInt(marbleCount / (float)Mathf.Max(1, cols));
        var backSpacing = MarbleRadius * 2.2f;

        var index = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (index >= marbleCount)
                {
                    break;
                }

                var lane = cols == 1 ? 0f : Mathf.Lerp(-maxLane, maxLane, (c + 0.5f) / cols);
                var behind = (r * backSpacing) + (MarbleRadius * 1.5f);
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
        for (var j = 0; j < marbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            if (IsDifferentElevationConflict(i, j))
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
        for (var j = 0; j < marbleCount; j++)
        {
            if (j == i)
            {
                continue;
            }

            if (IsDifferentElevationConflict(i, j))
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

    private void SetMarbleRenderState(int marbleIndex, int trackIndex)
    {
        var layer = track != null ? track.GetLayer(trackIndex) : (sbyte)0;
        var sortingOrder = layer == 1 ? 30 : 8;
        if (marbleSortingGroups != null && marbleIndex >= 0 && marbleIndex < marbleSortingGroups.Length && marbleSortingGroups[marbleIndex] != null)
        {
            marbleSortingGroups[marbleIndex].sortingOrder = sortingOrder;
        }
        else if (marbleSpriteRenderers != null && marbleIndex >= 0 && marbleIndex < marbleSpriteRenderers.Length && marbleSpriteRenderers[marbleIndex] != null)
        {
            marbleSpriteRenderers[marbleIndex].sortingOrder = sortingOrder;
        }
    }

    private float GetMarbleZ(int marbleIndex)
    {
        if (track == null || closestTrackIndex == null || marbleIndex < 0 || marbleIndex >= closestTrackIndex.Length)
        {
            return Z_UNDER;
        }

        return track.GetLayer(closestTrackIndex[marbleIndex]) == 1 ? Z_OVER : Z_UNDER;
    }

    private bool IsDifferentElevationConflict(int a, int b)
    {
        if (track == null)
        {
            return false;
        }

        var layerA = track.GetLayer(closestTrackIndex[a]);
        var layerB = track.GetLayer(closestTrackIndex[b]);
        return layerA != layerB;
    }

    private void ApplyCorridor(int i)
    {
        var idx = closestTrackIndex[i];
        var center = track.Center[idx];
        var normal = track.Normal[idx];
        var tangent = track.Tangent[idx];
        var halfWidth = track.HalfWidth[idx];

        var lateral = Vector2.Dot(positions[i] - center, normal);
        if (Mathf.Abs(lateral) > halfWidth * 2f)
        {
            var tangentSpeed = Mathf.Abs(Vector2.Dot(velocities[i], tangent));
            positions[i] = center;
            velocities[i] = tangent * Mathf.Max(2.6f, tangentSpeed);
            return;
        }

        var limit = Mathf.Max(0.25f, halfWidth - MarbleRadius);
        if (Mathf.Abs(lateral) <= limit)
        {
            return;
        }

        var contactNormal = lateral > limit ? normal : -normal;
        var penetration = Mathf.Abs(lateral) - limit;
        positions[i] -= contactNormal * penetration;

        var velocity = velocities[i];
        var vn = Vector2.Dot(velocity, contactNormal);
        var vt = Vector2.Dot(velocity, tangent);

        vn = -vn * WallRestitution;
        vt *= 1f - WallFriction;
        velocities[i] = (contactNormal * vn) + (tangent * vt);
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
            if (winnerIndex < 0 && lapCount[i] >= lapsToWin)
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

    private int FillRankingBuffer(int[] indices, int count)
    {
        if (indices == null || count <= 0)
        {
            return 0;
        }

        count = Mathf.Min(count, indices.Length);
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
        }

        System.Array.Sort(indices, 0, count, Comparer<int>.Create((a, b) =>
        {
            var pa = (progress != null && a >= 0 && a < progress.Length) ? progress[a] : float.NegativeInfinity;
            var pb = (progress != null && b >= 0 && b < progress.Length) ? progress[b] : float.NegativeInfinity;
            return pb.CompareTo(pa);
        }));

        return count;
    }

    private void CleanupLegacyTrackObjects()
    {
        if (sceneGraph != null)
        {
            CleanupDecorRootTrackObjects(sceneGraph.DecorRoot);

            var arenaRoot = sceneGraph.WorldRoot != null ? sceneGraph.WorldRoot.Find("ArenaRoot") : null;
            DestroyChildrenByNames(arenaRoot, new[] { "TrackRoot" });

            if (sceneGraph.DebugRoot != null)
            {
                DestroyChildrenByNames(sceneGraph.DebugRoot, new[] { "TrackDebug" });
            }
        }

        DestroyByNameRecursive(transform.root, "TrackSurfaceStamps");
        DestroyByNameRecursive(transform.root, "SanityStamp");
    }

    private static void CleanupDecorRootTrackObjects(Transform decorRoot)
    {
        if (decorRoot == null)
        {
            return;
        }

        for (var i = decorRoot.childCount - 1; i >= 0; i--)
        {
            var child = decorRoot.GetChild(i);
            var childName = child.name;

            if (childName == "StartFinishTile")
            {
                Destroy(child.gameObject);
                continue;
            }

            if (childName == "TrackRoot")
            {
                continue;
            }

            for (var n = 0; n < LegacyDecorTrackObjectNames.Length; n++)
            {
                if (childName == LegacyDecorTrackObjectNames[n])
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
    }

    private static void DestroyChildrenByNames(Transform parent, string[] names)
    {
        if (parent == null || names == null || names.Length == 0)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            for (var n = 0; n < names.Length; n++)
            {
                if (child.name == names[n])
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
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

    private MarbleRaceTrack BuildTrack(int seed, float arenaWidth, float arenaHeight, int variant, out bool fallbackUsed)
    {
        fallbackUsed = false;
        var halfW = arenaWidth * 0.5f;
        var halfH = arenaHeight * 0.5f;
        var bounds = new Rect(-halfW, -halfH, arenaWidth, arenaHeight);
        var trackSeed = unchecked(seed ^ (variant * (int)0x9E3779B9) ^ 0x7F4A7C15);
        Debug.Log($"[MarbleRace] BuildTrack seed={seed} variant={variant} trackSeed={trackSeed}");

        var rng = new SeededRng(trackSeed);
        var built = trackGenerator.Build(halfW, halfH, rng, seed, variant, trackTemplate, out var generatorFallbackUsed);
        var widthJitterScale = generatorFallbackUsed ? 0.8f : 1f;
        var processed = PostProcessTrack(built, arenaWidth, arenaHeight, widthJitterScale);
        if (processed != null && processed.SampleCount > 0 && ValidateTrack(processed, bounds))
        {
            fallbackUsed = generatorFallbackUsed;
            return processed;
        }

        fallbackUsed = true;
        var fallback = trackGenerator.BuildFallbackRoundedRectangle(halfW, halfH, variant);
        return PostProcessTrack(fallback, arenaWidth, arenaHeight, 0.8f);
    }

    private void RebuildTrackAndResetToReady()
    {
        if (trackGenerator == null)
        {
            trackGenerator = new MarbleRaceTrackGenerator();
        }

        if (trackRenderer == null)
        {
            trackRenderer = new MarbleRaceTrackRenderer();
        }

        var width = Mathf.Max(1f, simulationArenaWidth);
        var height = Mathf.Max(1f, simulationArenaHeight);
        track = BuildTrack(simulationSeed, width, height, trackVariant, out _);
        if (track == null || track.SampleCount <= 0)
        {
            track = trackGenerator.BuildFallbackRoundedRectangle(width * 0.5f, height * 0.5f, trackVariant);
        }

        trackRenderer.Apply(sceneGraph.DecorRoot, track);
        LogTrackValidation();

        if (marbles == null || marbles.Length != marbleCount)
        {
            BuildMarbles(simulationSeed);
        }
        else
        {
            PlaceStartGrid();
        }

        raceState = RacePhase.Ready;
        winnerIndex = -1;
        elapsedTime = 0f;
        finishTime = 0f;
    }

    private static int GenerateNextTrackSeed(int current)
    {
        unchecked
        {
            var next = current + 1;
            if (next == current)
            {
                next ^= (int)0x9E3779B9;
            }

            return next;
        }
    }

    private static int ResolveTrackTemplate(string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return -1;
        }

        switch (preset.Trim().ToLowerInvariant())
        {
            case "template0":
            case "oval":
                return 0;
            case "template1":
            case "s":
                return 1;
            case "template2":
            case "twist":
                return 2;
            case "template3":
                return 3;
            case "template4":
                return 4;
            case "template5":
                return 5;
            case "template6":
                return 6;
            case "template7":
                return 7;
            case "template8":
                return 8;
            default:
                return -1;
        }
    }

    private void LogTrackValidation()
    {
        var validation = MarbleRaceTrackValidator.Validate(track, marbleCount, trackRenderer != null ? trackRenderer.TrackRoot : null);
        var status = validation.ValidityPassed ? "VALID" : "INVALID";
        Debug.Log($"[MarbleRace] TrackValidator validity={status} quality={validation.QualityScore} overall={(validation.Passed ? "PASS" : "FAIL")} reasons={string.Join(" | ", validation.Reasons)}");
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

        var startIndex = FindStartOnStraight(source.Curvature, source.Layer);
        return RotateTrack(source, widths, startIndex);
    }

    private static int FindStartOnStraight(float[] curvature, sbyte[] layer)
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
                if (layer != null && layer.Length == n && layer[idx] == 1)
                {
                    score += 1.5f;
                }
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
            return new MarbleRaceTrack(source.Center, source.Tangent, source.Normal, widths, source.Curvature, source.Layer);
        }

        var center = new Vector2[n];
        var tangent = new Vector2[n];
        var normal = new Vector2[n];
        var curvature = new float[n];
        var rotatedWidths = new float[n];
        var rotatedLayer = new sbyte[n];

        for (var i = 0; i < n; i++)
        {
            var src = (i + startIndex) % n;
            center[i] = source.Center[src];
            tangent[i] = source.Tangent[src];
            normal[i] = source.Normal[src];
            curvature[i] = source.Curvature[src];
            rotatedWidths[i] = widths[src];
            rotatedLayer[i] = source.GetLayer(src);
        }

        return new MarbleRaceTrack(center, tangent, normal, rotatedWidths, curvature, rotatedLayer);
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
