using UnityEngine;

public class PredatorPreyDocuRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int HerdGoalRetargetTicks = 1200;

    private SimulationSceneGraph sceneGraph;
    private ScenarioConfig activeConfig;
    private PredatorPreyDocuMapBuilder map;
    private float halfWidth;
    private float halfHeight;
    private int preyCountTotal;
    private int lionCountTotal;

    private Vector2[] preyPos;
    private Vector2[] preyVel;
    private int[] preyHerdId;
    private Vector2[] preyOffset;
    private Transform[] preyTf;

    private Vector2[] lionPos;
    private Vector2[] lionVel;
    private int[] lionPrideId;
    private bool[] lionMale;
    private bool[] lionYoung;
    private bool[] lionLeader;
    private bool[] lionRoaming;
    private Transform[] lionTf;

    private Vector2[] herdCenter;
    private Vector2[] herdGoal;
    private int[] herdGoalTick;

    private Vector2[] prideCenter;
    private int[] prideShadeIndex;

    private string lastSeasonName;
    private bool loggedArenaBackgroundOverride;

    [SerializeField] private PredatorPreyDocuMapRecipe mapRecipe;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();

        activeConfig = config ?? new ScenarioConfig();
        activeConfig.NormalizeAliases();

        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "PredatorPreyDocu");
        ForceArenaRootBackgroundBehindEverything();
        halfWidth = Mathf.Max(1f, activeConfig.world.arenaWidth * 0.5f);
        halfHeight = Mathf.Max(1f, activeConfig.world.arenaHeight * 0.5f);

        EnsureMainCamera();

        map = new PredatorPreyDocuMapBuilder();
        var mapParent = sceneGraph.WorldObjectsRoot != null ? sceneGraph.WorldObjectsRoot : sceneGraph.ArenaRoot;
        if (mapParent != null && !mapParent.gameObject.activeSelf)
        {
            mapParent.gameObject.SetActive(true);
        }

        map.Build(mapParent != null ? mapParent : transform, activeConfig, halfWidth, halfHeight, mapRecipe);
        SpawnEntities();

        lastSeasonName = null;
        Debug.Log($"{nameof(PredatorPreyDocuRunner)} Initialize seed={activeConfig.seed}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (activeConfig == null || preyTf == null || lionTf == null)
        {
            return;
        }

        var docu = activeConfig.predatorPreyDocu;
        var season = docu.season;
        var wet = Mathf.Max(1, season.wetTicks);
        var dry = Mathf.Max(1, season.dryTicks);
        var cycle = wet + dry;
        var phase = cycle > 0 ? tickIndex % cycle : 0;
        var seasonName = phase < wet ? "Wet" : "Dry";

        var dryness01 = 0f;
        if (phase >= wet)
        {
            var t = (phase - wet) / (float)Mathf.Max(1, dry);
            dryness01 = Mathf.SmoothStep(0f, 1f, t);
        }

        if (!string.Equals(lastSeasonName, seasonName))
        {
            EventBusService.Global.Publish("season.change", new { season = seasonName });
            lastSeasonName = seasonName;
        }

        map.UpdateSeasonVisuals(dryness01);
        UpdateHerdCenters(tickIndex, dt, dryness01);
        UpdatePrey(tickIndex, dt, docu);
        UpdateLions(tickIndex, dt, docu);
    }

    public void Shutdown()
    {
        DestroyTransforms(preyTf);
        DestroyTransforms(lionTf);

        preyTf = null;
        lionTf = null;

        preyPos = null;
        preyVel = null;
        preyHerdId = null;
        preyOffset = null;

        lionPos = null;
        lionVel = null;
        lionPrideId = null;
        lionMale = null;
        lionYoung = null;
        lionLeader = null;
        lionRoaming = null;

        herdCenter = null;
        herdGoal = null;
        herdGoalTick = null;

        prideCenter = null;
        prideShadeIndex = null;

        preyCountTotal = 0;
        lionCountTotal = 0;

        if (map != null)
        {
            map.Clear();
            map = null;
        }

        activeConfig = null;
        lastSeasonName = null;
        Debug.Log("PredatorPreyDocuRunner Shutdown");
    }

    public void RebuildMapPreview()
    {
        if (map == null || activeConfig == null)
        {
            return;
        }

        var mapParent = sceneGraph != null && sceneGraph.WorldObjectsRoot != null ? sceneGraph.WorldObjectsRoot : transform;
        map.Build(mapParent, activeConfig, halfWidth, halfHeight, mapRecipe);
    }

    private void ForceArenaRootBackgroundBehindEverything()
    {
        if (sceneGraph == null)
        {
            return;
        }

        var root = sceneGraph.ArenaRoot;
        if (root == null)
        {
            return;
        }

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        var forcedAny = false;
        for (var i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (sr == null)
            {
                continue;
            }

            if (sr.gameObject.name != "Background")
            {
                continue;
            }

            sr.sortingLayerName = "Default";
            sr.sortingOrder = -1000;
            sr.color = Color.white;
            forcedAny = true;
        }

        if (forcedAny && !loggedArenaBackgroundOverride)
        {
            Debug.Log("[PredatorPreyDocu] Forced Arena Background behind (sortingOrder=-1000).");
            loggedArenaBackgroundOverride = true;
        }
    }

    private void SpawnEntities()
    {
        var docu = activeConfig.predatorPreyDocu;
        var pop = docu.pop;
        var movement = docu.movement;
        var visuals = docu.visuals;

        var preyRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, 0);
        var lionRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, 1);

        var rngPop = RngService.Fork("SIM:PredatorPreyDocu:POP");

        var herdCount = pop.herdCount;
        herdCenter = new Vector2[herdCount];
        herdGoal = new Vector2[herdCount];
        herdGoalTick = new int[herdCount];

        for (var h = 0; h < herdCount; h++)
        {
            var side = h % 2 == 0 ? -1f : 1f;
            var x = side * rngPop.Range(docu.map.floodplainWidth * 0.8f, halfWidth * 0.92f);
            var y = rngPop.Range(-halfHeight * 0.85f, halfHeight * 0.85f);
            herdCenter[h] = new Vector2(x, y);
            herdGoal[h] = herdCenter[h];
            herdGoalTick[h] = 0;
        }

        preyCountTotal = pop.herdCount * pop.preyPerHerd;
        preyTf = new Transform[preyCountTotal];
        preyPos = new Vector2[preyCountTotal];
        preyVel = new Vector2[preyCountTotal];
        preyHerdId = new int[preyCountTotal];
        preyOffset = new Vector2[preyCountTotal];

        for (var i = 0; i < preyCountTotal; i++)
        {
            var herdId = i % herdCount;
            preyHerdId[i] = herdId;
            preyOffset[i] = rngPop.InsideUnitCircle() * movement.herdRadius;
            preyPos[i] = herdCenter[herdId] + preyOffset[i];

            var root = new GameObject($"Prey_{i:0000}");
            root.transform.SetParent(preyRoot, false);
            root.transform.localPosition = new Vector3(preyPos[i].x, preyPos[i].y, 0f);
            var accent = PredatorPreyDocuVisualFactory.HerdAccentColor(herdId);
            PredatorPreyDocuVisualFactory.BuildPrey(root.transform, accent, visuals.preyScale, visuals.showPackAccent);
            preyTf[i] = root.transform;
        }

        var prideCount = pop.prideCount;
        prideCenter = new Vector2[prideCount];
        prideShadeIndex = new int[prideCount];

        for (var p = 0; p < prideCount; p++)
        {
            if (map.ShadeNodes.Count > 0)
            {
                var idx = (p * Mathf.Max(1, map.ShadeNodes.Count / Mathf.Max(1, prideCount)) + rngPop.NextInt(0, map.ShadeNodes.Count)) % map.ShadeNodes.Count;
                prideCenter[p] = map.ShadeNodes[idx];
                prideShadeIndex[p] = idx;
            }
            else
            {
                prideCenter[p] = new Vector2(rngPop.Range(-halfWidth * 0.8f, halfWidth * 0.8f), rngPop.Range(-halfHeight * 0.8f, halfHeight * 0.8f));
                prideShadeIndex[p] = -1;
            }
        }

        lionCountTotal = (pop.prideCount * pop.lionsPerPride) + (pop.roamingCoalitions * pop.coalitionSize);
        lionTf = new Transform[lionCountTotal];
        lionPos = new Vector2[lionCountTotal];
        lionVel = new Vector2[lionCountTotal];
        lionPrideId = new int[lionCountTotal];
        lionMale = new bool[lionCountTotal];
        lionYoung = new bool[lionCountTotal];
        lionLeader = new bool[lionCountTotal];
        lionRoaming = new bool[lionCountTotal];

        var lionIndex = 0;
        for (var p = 0; p < pop.prideCount; p++)
        {
            for (var i = 0; i < pop.lionsPerPride; i++)
            {
                SpawnLion(lionRoot, rngPop, visuals, lionIndex, p, false, coalitionSlot: i);
                lionIndex++;
            }
        }

        for (var c = 0; c < pop.roamingCoalitions; c++)
        {
            for (var i = 0; i < pop.coalitionSize; i++)
            {
                SpawnLion(lionRoot, rngPop, visuals, lionIndex, c % Mathf.Max(1, pop.prideCount), true, coalitionSlot: i);
                lionIndex++;
            }
        }
    }

    private void SpawnLion(Transform lionRoot, IRng rngPop, Visuals visuals, int lionIndex, int prideId, bool roaming, int coalitionSlot)
    {
        var isLeader = !roaming && coalitionSlot == 0;
        var isMale = roaming || isLeader || (coalitionSlot == 1 && (prideId % 3 == 0));
        var isYoung = !isMale && !isLeader && ((lionIndex + prideId) % 4 == 0);

        lionPrideId[lionIndex] = prideId;
        lionLeader[lionIndex] = isLeader;
        lionMale[lionIndex] = isMale;
        lionYoung[lionIndex] = isYoung;
        lionRoaming[lionIndex] = roaming;

        Vector2 spawn;
        if (roaming)
        {
            var edgeX = rngPop.Value() < 0.5f ? -halfWidth * 0.92f : halfWidth * 0.92f;
            spawn = new Vector2(edgeX, rngPop.Range(-halfHeight * 0.9f, halfHeight * 0.9f));
        }
        else
        {
            spawn = prideCenter[prideId] + rngPop.InsideUnitCircle() * 4f;
        }

        lionPos[lionIndex] = spawn;

        var root = new GameObject($"Lion_{lionIndex:0000}");
        root.transform.SetParent(lionRoot, false);
        root.transform.localPosition = new Vector3(spawn.x, spawn.y, 0f);
        var accent = PredatorPreyDocuVisualFactory.PrideAccentColor(prideId);
        PredatorPreyDocuVisualFactory.BuildLion(root.transform, accent, visuals.lionScale, isMale, isYoung, isLeader, visuals.showPackAccent, visuals.showMaleRing, roaming);
        lionTf[lionIndex] = root.transform;
    }

    private void UpdateHerdCenters(int tickIndex, float dt, float dryness01)
    {
        var docu = activeConfig.predatorPreyDocu;
        var mapCfg = docu.map;
        var move = docu.movement;

        for (var h = 0; h < herdCenter.Length; h++)
        {
            var distToGoal = Vector2.Distance(herdCenter[h], herdGoal[h]);
            if (tickIndex >= herdGoalTick[h] || distToGoal < 2f)
            {
                var wetGoal = PickWetHerdGoal(h, mapCfg.floodplainWidth);
                var waterGoal = PickWaterGoal(herdCenter[h]);
                herdGoal[h] = Vector2.Lerp(wetGoal, waterGoal, dryness01);
                herdGoalTick[h] = tickIndex + HerdGoalRetargetTicks + (h * 13);
            }

            var toGoal = herdGoal[h] - herdCenter[h];
            var wander = SineNoise2D(tickIndex * 0.002f + h * 5.31f, 0.13f + h * 0.17f) * move.herdWander;
            var desired = toGoal + wander;
            if (desired.sqrMagnitude > 0.0001f)
            {
                desired = desired.normalized * move.herdCruiseSpeed;
            }

            herdCenter[h] += desired * dt;
            herdCenter[h] = ClampToBounds(herdCenter[h], move.edgeMargin);
        }
    }

    private void UpdatePrey(int tickIndex, float dt, PredatorPreyDocuConfig docu)
    {
        var move = docu.movement;

        for (var i = 0; i < preyCountTotal; i++)
        {
            var herdId = preyHerdId[i];
            var target = herdCenter[herdId] + preyOffset[i];

            var jitter = SineNoise2D(tickIndex * 0.013f + i * 0.53f, i * 0.11f) * move.preyJitter;
            target += jitter;

            var toTarget = target - preyPos[i];
            var desiredVel = toTarget * 1.75f;
            var perp = new Vector2(-toTarget.y, toTarget.x).normalized;
            desiredVel += perp * (Mathf.Sin((tickIndex * 0.03f) + i * 0.37f) * move.herdWander * 0.35f);

            var speedLimit = move.herdCruiseSpeed * Mathf.Lerp(0.7f, 1.1f, Frac(Mathf.Sin(i * 17.37f) * 43758.5453f));
            preyVel[i] = Vector2.Lerp(preyVel[i], desiredVel, 0.24f);
            if (preyVel[i].magnitude > speedLimit)
            {
                preyVel[i] = preyVel[i].normalized * speedLimit;
            }

            preyPos[i] += preyVel[i] * dt;
            ApplyBoundsBounce(ref preyPos[i], ref preyVel[i], move.edgeMargin);

            if (preyTf[i] != null)
            {
                preyTf[i].localPosition = new Vector3(preyPos[i].x, preyPos[i].y, 0f);
            }
        }
    }

    private void UpdateLions(int tickIndex, float dt, PredatorPreyDocuConfig docu)
    {
        var move = docu.movement;
        var floodBand = docu.map.floodplainWidth;

        for (var i = 0; i < lionCountTotal; i++)
        {
            var target = lionPos[i];
            if (!lionRoaming[i])
            {
                var prideId = lionPrideId[i];
                var anchor = prideCenter[prideId];
                if (prideShadeIndex[prideId] >= 0 && prideShadeIndex[prideId] < map.ShadeNodes.Count)
                {
                    var shade = map.ShadeNodes[prideShadeIndex[prideId]];
                    anchor = Vector2.Lerp(anchor, shade, 0.5f + 0.2f * Mathf.Sin((tickIndex * 0.0015f) + i));
                }

                var patrolRadius = 2.5f + (i % 3);
                var patrol = new Vector2(
                    Mathf.Cos((tickIndex * 0.004f) + i * 0.3f),
                    Mathf.Sin((tickIndex * 0.0045f) + i * 0.27f)) * patrolRadius;

                target = anchor + patrol;
            }
            else
            {
                var edgeSign = lionPos[i].x < 0f ? -1f : 1f;
                var desiredX = edgeSign * Mathf.Lerp(floodBand + 10f, halfWidth - 2f, 0.75f);
                var wanderY = Mathf.Sin((tickIndex * 0.0035f) + i * 0.91f) * (halfHeight * 0.65f);
                target = new Vector2(desiredX, wanderY);
            }

            var noise = SineNoise2D(tickIndex * 0.007f + i, i * 0.39f) * move.lionWander;
            var desired = (target - lionPos[i]) + noise;
            var desiredVel = desired.sqrMagnitude > 0.0001f ? desired.normalized * move.pridePatrolSpeed : Vector2.zero;
            lionVel[i] = Vector2.Lerp(lionVel[i], desiredVel, 0.18f);
            lionPos[i] += lionVel[i] * dt;
            ApplyBoundsBounce(ref lionPos[i], ref lionVel[i], move.edgeMargin);

            if (lionTf[i] != null)
            {
                lionTf[i].localPosition = new Vector3(lionPos[i].x, lionPos[i].y, 0f);
            }
        }
    }

    private Vector2 PickWetHerdGoal(int herdId, float floodplainWidth)
    {
        var xSign = herdCenter[herdId].x < 0f ? -1f : 1f;
        var phase = Mathf.Sin((herdId + 1) * 12.123f + herdGoalTick[herdId] * 0.013f);
        var x = xSign * Mathf.Lerp(floodplainWidth * 0.75f, halfWidth * 0.86f, Frac(phase * 231.37f));
        var yNoise = Mathf.Sin((herdId * 1.7f) + herdGoalTick[herdId] * 0.01f);
        var y = yNoise * halfHeight * 0.82f;
        return new Vector2(x, y);
    }

    private Vector2 PickWaterGoal(Vector2 from)
    {
        var nearest = new Vector2(0f, from.y);
        var bestSq = (nearest - from).sqrMagnitude;
        for (var i = 0; i < map.WaterNodes.Count; i++)
        {
            var cand = map.WaterNodes[i];
            var sq = (cand - from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                nearest = cand;
            }
        }

        return nearest;
    }

    private Vector2 ClampToBounds(Vector2 point, float edgeMargin)
    {
        var x = Mathf.Clamp(point.x, -halfWidth + edgeMargin, halfWidth - edgeMargin);
        var y = Mathf.Clamp(point.y, -halfHeight + edgeMargin, halfHeight - edgeMargin);
        return new Vector2(x, y);
    }

    private void ApplyBoundsBounce(ref Vector2 position, ref Vector2 velocity, float edgeMargin)
    {
        var minX = -halfWidth + edgeMargin;
        var maxX = halfWidth - edgeMargin;
        var minY = -halfHeight + edgeMargin;
        var maxY = halfHeight - edgeMargin;

        if (position.x < minX)
        {
            position.x = minX;
            velocity.x = Mathf.Abs(velocity.x);
        }
        else if (position.x > maxX)
        {
            position.x = maxX;
            velocity.x = -Mathf.Abs(velocity.x);
        }

        if (position.y < minY)
        {
            position.y = minY;
            velocity.y = Mathf.Abs(velocity.y);
        }
        else if (position.y > maxY)
        {
            position.y = maxY;
            velocity.y = -Mathf.Abs(velocity.y);
        }
    }

    private static Vector2 SineNoise2D(float x, float y)
    {
        var nx = Mathf.Sin((x * 3.1f) + (y * 1.7f));
        var ny = Mathf.Sin((x * 1.3f) - (y * 2.2f) + 1.234f);
        return new Vector2(nx, ny);
    }

    private static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }

    private static void DestroyTransforms(Transform[] transforms)
    {
        if (transforms == null)
        {
            return;
        }

        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                Object.Destroy(transforms[i].gameObject);
            }
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

        var cam = cameraObject.AddComponent<Camera>();
        cam.orthographic = true;
        var initialOrtho = Mathf.Max(halfHeight + 6f, 10f);
        var arenaCameraPolicy = Object.FindAnyObjectByType<ArenaCameraPolicy>();
        if (arenaCameraPolicy != null && arenaCameraPolicy.targetCamera == cam)
        {
            arenaCameraPolicy.SetOrthoFromExternal(initialOrtho, "PredatorPreyDocuRunner.EnsureMainCamera", syncZoomLevel: true);
        }
        else
        {
            cam.orthographicSize = initialOrtho;
        }

        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }
}
