using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class PredatorPreyDocuRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int HerdGoalRetargetTicks = 1200;

    private SimulationSceneGraph sceneGraph;
    private ScenarioConfig activeConfig;
    private PredatorPreyDocuMapBuilder map;
    private SerengetiMapSpec loadedMapSpec;
    private float halfWidth;
    private float halfHeight;
    private int preyCountTotal;
    private int lionCountTotal;

    private Vector2[] preyPos;
    private Vector2[] preyVel;
    private int[] preyHerdId;
    private Vector2[] preyOffset;
    private Transform[] preyTf;
    private string[] herdSpeciesKey;

    private Vector2[] lionPos;
    private Vector2[] lionVel;
    private int[] lionPrideId;
    private bool[] lionMale;
    private bool[] lionYoung;
    private bool[] lionLeader;
    private bool[] lionRoaming;
    private Transform[] lionTf;
    private string[] prideSpeciesKey;

    private Vector2[] herdCenter;
    private Vector2[] herdGoal;
    private int[] herdGoalTick;

    private Vector2[] prideCenter;
    private int[] prideShadeIndex;

    private string lastSeasonName;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();

        activeConfig = config ?? new ScenarioConfig();
        activeConfig.NormalizeAliases();

        var mapId = activeConfig.predatorPreyDocu?.mapId ?? "serengeti_v1";
        loadedMapSpec = SerengetiMapSpecLoader.LoadOrThrow(mapId);
        if (loadedMapSpec.arena != null &&
            (loadedMapSpec.arena.width != activeConfig.world.arenaWidth || loadedMapSpec.arena.height != activeConfig.world.arenaHeight))
        {
            Debug.LogWarning($"[PredatorPreyDocu] Map '{loadedMapSpec.mapId}' arena={loadedMapSpec.arena.width}x{loadedMapSpec.arena.height} differs from configured world {activeConfig.world.arenaWidth}x{activeConfig.world.arenaHeight}. Preset should match arena size before ArenaBuilder.Build.");
        }

        Debug.Log($"[PredatorPreyDocu] Loaded map '{loadedMapSpec.mapId}' arena={loadedMapSpec.arena.width}x{loadedMapSpec.arena.height} regions={loadedMapSpec.regions.Count} speciesLegend={loadedMapSpec.legend.species.Count}");
        Debug.Log($"[PredatorPreyDocu] MapRender: {loadedMapSpec.mapId} regions={loadedMapSpec.regions.Count} pools={loadedMapSpec.water.pools.Count} kopjes={loadedMapSpec.landmarks.kopjes.Count}");

        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "PredatorPreyDocu");
        halfWidth = Mathf.Max(1f, activeConfig.world.arenaWidth * 0.5f);
        halfHeight = Mathf.Max(1f, activeConfig.world.arenaHeight * 0.5f);

        EnsureMainCamera();

        map = new PredatorPreyDocuMapBuilder();
        var mapParent = sceneGraph.WorldObjectsRoot != null ? sceneGraph.WorldObjectsRoot : sceneGraph.ArenaRoot;
        if (mapParent != null && !mapParent.gameObject.activeSelf)
        {
            mapParent.gameObject.SetActive(true);
        }

        map.Build(mapParent != null ? mapParent : transform, activeConfig, loadedMapSpec, halfWidth, halfHeight);
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
        var ticksPerMonth = docu.ticksPerMonth > 0 ? docu.ticksPerMonth : Mathf.Max(300, loadedMapSpec?.calendar?.ticksPerMonth ?? 3600);
        var startMonth = Mathf.Clamp(docu.startMonth, 1, 12);
        var month = ((startMonth - 1) + (tickIndex / Mathf.Max(1, ticksPerMonth))) % 12 + 1;
        var seasonalPresence01 = 0f;
        var presence = loadedMapSpec?.water?.seasonal?.presenceByMonth;
        if (presence != null && presence.TryGetValue(month.ToString(), out var monthPresence))
        {
            seasonalPresence01 = Mathf.Clamp01(monthPresence);
        }

        var seasonName = seasonalPresence01 >= 0.5f ? "Wet" : "Dry";
        if (!string.Equals(lastSeasonName, seasonName))
        {
            EventBusService.Global.Publish("season.change", new { season = seasonName, month });
            lastSeasonName = seasonName;
        }

        map.UpdateSeasonVisuals(seasonalPresence01);
        UpdateHerdCenters(tickIndex, dt, 1f - seasonalPresence01);
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
        herdSpeciesKey = null;

        lionPos = null;
        lionVel = null;
        lionPrideId = null;
        lionMale = null;
        lionYoung = null;
        lionLeader = null;
        lionRoaming = null;
        prideSpeciesKey = null;

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
        loadedMapSpec = null;
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
        loadedMapSpec = SerengetiMapSpecLoader.LoadOrThrow(activeConfig.predatorPreyDocu?.mapId ?? "serengeti_v1");
        map.Build(mapParent, activeConfig, loadedMapSpec, halfWidth, halfHeight);
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
        var spawnSeed = activeConfig.seed ^ StableHash.Hash32(loadedMapSpec?.mapId ?? "serengeti_v1") ^ unchecked((int)0x6D2B79F5u);
        var speciesRng = new SeededRng(spawnSeed);

        var herbivoreWeights = BuildHerbivoreSpawnWeights(loadedMapSpec);
        var predatorWeights = BuildPredatorSpawnWeights(loadedMapSpec);
        var useLegendSpecies = herbivoreWeights.Count > 0 && predatorWeights.Count > 0;

        var herdCount = pop.herdCount;
        herdSpeciesKey = new string[Mathf.Max(0, herdCount)];
        for (var h = 0; h < herdSpeciesKey.Length; h++)
        {
            herdSpeciesKey[h] = useLegendSpecies ? PickWeightedSpecies(speciesRng, herbivoreWeights, "wildebeest") : "generic_prey";
        }
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

            var speciesKey = herdSpeciesKey != null && herdId >= 0 && herdId < herdSpeciesKey.Length ? herdSpeciesKey[herdId] : "generic_prey";

            var root = new GameObject($"Prey_{speciesKey}_{i:0000}");
            root.transform.SetParent(preyRoot, false);
            root.transform.localPosition = new Vector3(preyPos[i].x, preyPos[i].y, 0f);
            var accent = PredatorPreyDocuVisualFactory.HerdAccentColor(herdId);
            if (useLegendSpecies && TryGetLegendSpecies(speciesKey, out var entry))
            {
                var isMale = ((i + herdId) % 2) == 0;
                var isChild = ((i + herdId) % 7) == 0;
                PredatorPreyDocuVisualFactory.BuildLegendAnimal(root.transform, entry, loadedMapSpec.legend.sexAgeRules, visuals.preyScale, isMale, isChild, visuals.showPackAccent, accent);
            }
            else
            {
                PredatorPreyDocuVisualFactory.BuildPrey(root.transform, accent, visuals.preyScale, visuals.showPackAccent);
            }

            AttachSpeciesLabel(root.transform, speciesKey, visuals.preyScale);
            preyTf[i] = root.transform;
        }

        var prideCount = pop.prideCount;
        prideSpeciesKey = new string[Mathf.Max(0, prideCount)];
        for (var p = 0; p < prideSpeciesKey.Length; p++)
        {
            prideSpeciesKey[p] = useLegendSpecies ? PickWeightedSpecies(speciesRng, predatorWeights, "lion") : "lion";
        }
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
                SpawnLion(lionRoot, rngPop, visuals, lionIndex, p, false, coalitionSlot: i, useLegendSpecies);
                lionIndex++;
            }
        }

        for (var c = 0; c < pop.roamingCoalitions; c++)
        {
            for (var i = 0; i < pop.coalitionSize; i++)
            {
                SpawnLion(lionRoot, rngPop, visuals, lionIndex, c % Mathf.Max(1, pop.prideCount), true, coalitionSlot: i, useLegendSpecies);
                lionIndex++;
            }
        }

        Debug.Log($"[SerengetiSpawn] mapId={loadedMapSpec?.mapId} herds={herdCount} herdSpecies={SummarizeSpecies(herdSpeciesKey)} predators={lionCountTotal} predatorSpecies={SummarizeSpecies(prideSpeciesKey)} useLegend={useLegendSpecies}");
    }

    private void SpawnLion(Transform lionRoot, IRng rngPop, Visuals visuals, int lionIndex, int prideId, bool roaming, int coalitionSlot, bool useLegendSpecies)
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

        var speciesKey = prideSpeciesKey != null && prideId >= 0 && prideId < prideSpeciesKey.Length ? prideSpeciesKey[prideId] : "lion";
        var root = new GameObject($"Predator_{speciesKey}_{lionIndex:0000}");
        root.transform.SetParent(lionRoot, false);
        root.transform.localPosition = new Vector3(spawn.x, spawn.y, 0f);
        var accent = PredatorPreyDocuVisualFactory.PrideAccentColor(prideId);
        if (useLegendSpecies && TryGetLegendSpecies(speciesKey, out var entry))
        {
            PredatorPreyDocuVisualFactory.BuildLegendAnimal(root.transform, entry, loadedMapSpec.legend.sexAgeRules, visuals.lionScale, isMale, isYoung, visuals.showPackAccent, accent);
        }
        else
        {
            PredatorPreyDocuVisualFactory.BuildLion(root.transform, accent, visuals.lionScale, isMale, isYoung, isLeader, visuals.showPackAccent, visuals.showMaleRing, roaming);
        }

        AttachSpeciesLabel(root.transform, speciesKey, visuals.lionScale);
        lionTf[lionIndex] = root.transform;
    }

    private void AttachSpeciesLabel(Transform parent, string speciesKey, float scale)
    {
        if (activeConfig?.predatorPreyDocu == null || !activeConfig.predatorPreyDocu.debugShowMapOverlays || parent == null || string.IsNullOrWhiteSpace(speciesKey))
        {
            return;
        }

        var label = new GameObject("SpeciesLabel");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, Mathf.Max(1f, scale * 1.9f), 0f);

        var text = label.AddComponent<TextMesh>();
        text.text = speciesKey;
        text.fontSize = 24;
        text.characterSize = Mathf.Clamp(0.08f * scale, 0.06f, 0.14f);
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.92f, 0.95f, 0.98f, 0.92f);
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
                UnityEngine.Object.Destroy(transforms[i].gameObject);
            }
        }
    }

    private bool TryGetLegendSpecies(string key, out LegendSpeciesEntry entry)
    {
        entry = null;
        var species = loadedMapSpec?.legend?.species;
        return !string.IsNullOrWhiteSpace(key) && species != null && species.TryGetValue(key, out entry) && entry != null;
    }

    private static List<WeightedSpecies> BuildHerbivoreSpawnWeights(SerengetiMapSpec spec)
    {
        var result = new List<WeightedSpecies>();
        if (spec?.spawnHints?.entries == null) return result;
        foreach (var pair in spec.spawnHints.entries.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Value == null || pair.Value.Type != JTokenType.Object) continue;
            if (!TryGetInt(pair.Value, "baseHerds", out var baseHerds) || !TryGetInt(pair.Value, "herdSize", out var herdSize)) continue;
            var weight = Mathf.Max(1f, baseHerds * Mathf.Max(1, herdSize));
            result.Add(new WeightedSpecies(pair.Key, weight));
        }

        return result;
    }

    private static List<WeightedSpecies> BuildPredatorSpawnWeights(SerengetiMapSpec spec)
    {
        var result = new List<WeightedSpecies>();
        if (spec?.spawnHints?.entries == null) return result;
        foreach (var pair in spec.spawnHints.entries.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Value == null || pair.Value.Type != JTokenType.Object) continue;

            var weight = 0f;
            if (TryGetInt(pair.Value, "prides", out var prides))
            {
                var prideSize = TryGetInt(pair.Value, "prideSize", out var value) ? value : 1;
                weight = Mathf.Max(weight, prides * Mathf.Max(1, prideSize));
            }

            if (TryGetInt(pair.Value, "clans", out var clans))
            {
                var clanSize = TryGetInt(pair.Value, "clanSize", out var value) ? value : 1;
                weight = Mathf.Max(weight, clans * Mathf.Max(1, clanSize));
            }

            if (TryGetInt(pair.Value, "solitary", out var solitary))
            {
                weight = Mathf.Max(weight, solitary);
            }

            if (TryGetInt(pair.Value, "rarePacksCount", out var rarePacksCount) || TryGetArrayCount(pair.Value, "rarePacks", out rarePacksCount))
            {
                var packSize = TryGetInt(pair.Value, "packSize", out var value) ? value : 1;
                weight = Mathf.Max(weight, rarePacksCount * Mathf.Max(1, packSize));
            }

            if (weight > 0f)
            {
                result.Add(new WeightedSpecies(pair.Key, weight));
            }
        }

        return result;
    }

    private static string PickWeightedSpecies(IRng rng, IReadOnlyList<WeightedSpecies> weighted, string fallback)
    {
        if (rng == null || weighted == null || weighted.Count == 0)
        {
            return fallback;
        }

        var total = 0f;
        for (var i = 0; i < weighted.Count; i++) total += Mathf.Max(0f, weighted[i].weight);
        if (total <= 0f) return fallback;

        var pick = rng.Range(0f, total);
        var run = 0f;
        for (var i = 0; i < weighted.Count; i++)
        {
            run += Mathf.Max(0f, weighted[i].weight);
            if (pick <= run) return weighted[i].key;
        }

        return weighted[weighted.Count - 1].key;
    }

    private static string SummarizeSpecies(IEnumerable<string> keys)
    {
        if (keys == null) return "none";
        var summary = keys.Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}:{g.Count()}");
        var text = string.Join(",", summary);
        return string.IsNullOrEmpty(text) ? "none" : text;
    }

    private static bool TryGetInt(JToken token, string key, out int value)
    {
        value = 0;
        if (token == null || token.Type != JTokenType.Object || string.IsNullOrWhiteSpace(key)) return false;
        if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var child) || child == null) return false;
        if (child.Type == JTokenType.Integer) { value = child.Value<int>(); return true; }
        if (child.Type == JTokenType.Boolean) { value = child.Value<bool>() ? 1 : 0; return true; }
        if (child.Type == JTokenType.String && int.TryParse(child.Value<string>(), out value)) return true;
        if ((child.Type == JTokenType.Float || child.Type == JTokenType.Integer) && float.TryParse(child.ToString(), out var f)) { value = Mathf.RoundToInt(f); return true; }
        return false;
    }

    private static bool TryGetFloat(JToken token, string key, out float value)
    {
        value = 0f;
        if (token == null || token.Type != JTokenType.Object || string.IsNullOrWhiteSpace(key)) return false;
        if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var child) || child == null) return false;
        if (child.Type == JTokenType.Float || child.Type == JTokenType.Integer) { value = child.Value<float>(); return true; }
        if (child.Type == JTokenType.String && float.TryParse(child.Value<string>(), out value)) return true;
        return false;
    }

    private static bool TryGetBool(JToken token, string key, out bool value)
    {
        value = false;
        if (token == null || token.Type != JTokenType.Object || string.IsNullOrWhiteSpace(key)) return false;
        if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var child) || child == null) return false;
        if (child.Type == JTokenType.Boolean) { value = child.Value<bool>(); return true; }
        if (child.Type == JTokenType.Integer) { value = child.Value<int>() != 0; return true; }
        if (child.Type == JTokenType.String && bool.TryParse(child.Value<string>(), out value)) return true;
        return false;
    }

    private static bool TryGetString(JToken token, string key, out string value)
    {
        value = null;
        if (token == null || token.Type != JTokenType.Object || string.IsNullOrWhiteSpace(key)) return false;
        if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var child) || child == null) return false;
        if (child.Type == JTokenType.String)
        {
            value = child.Value<string>();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryGetArrayCount(JToken token, string key, out int count)
    {
        count = 0;
        if (token == null || token.Type != JTokenType.Object || string.IsNullOrWhiteSpace(key)) return false;
        if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var child) || child == null || child.Type != JTokenType.Array) return false;
        count = child.Count();
        return true;
    }

    private readonly struct WeightedSpecies
    {
        public readonly string key;
        public readonly float weight;

        public WeightedSpecies(string key, float weight)
        {
            this.key = key;
            this.weight = weight;
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
        var arenaCameraPolicy = UnityEngine.Object.FindAnyObjectByType<ArenaCameraPolicy>();
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
