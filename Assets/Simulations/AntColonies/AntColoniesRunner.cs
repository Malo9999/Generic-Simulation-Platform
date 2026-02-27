using System;
using System.Collections.Generic;
using UnityEngine;

public class AntColoniesRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;

    private readonly List<AntAgentState> ants = new();
    private readonly List<AntAgentView> antViews = new();
    private readonly Dictionary<int, int> antIdToIndex = new();

    private readonly List<SpriteRenderer> foodRenderers = new();

    private int nextAntId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private AntWorldState worldState;
    private AntWorldRecipe recipe;
    private bool showHealthBars;
    private AntColoniesConfig antConfig;
    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private SimulationSceneGraph sceneGraph;
    private Transform antWorldViewRoot;

    private static Sprite fallbackAntSprite;
    private static Sprite squareSprite;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "AntColonies");
        EnsureMainCamera();

        antConfig = config?.antColonies ?? new AntColoniesConfig();
        antConfig.Normalize();

        recipe = antConfig.worldRecipe ?? new AntWorldRecipe();
        recipe.Normalize();
        showHealthBars = config?.presentation?.showHealthBars ?? false;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        worldState = AntWorldGenerator.Generate(config);
        AntWorldViewBuilder.BuildOrRefresh(sceneGraph.WorldObjectsRoot, config, worldState);
        antWorldViewRoot = sceneGraph.WorldObjectsRoot.Find("AntWorldView");
        CacheWorldRenderers();
        ResolveArtPipeline();

        nextAntId = 0;
        RebuildAntIndex();
        SpawnInitialAnts();

        Debug.Log($"{nameof(AntColoniesRunner)} Initialize seed={config?.seed ?? 0}, scenario={config?.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (worldState == null)
        {
            return;
        }

        SpawnAnts(tickIndex);
        RespawnFood(tickIndex);

        var pairDamage = new Dictionary<int, float>();

        for (var i = 0; i < ants.Count; i++)
        {
            var ant = ants[i];
            if (!ant.isAlive)
            {
                continue;
            }

            ant.ageTicks++;
            ant.hp -= recipe.ageDrainPerTick;

            if (ant.state == AntBehaviorState.Wander)
            {
                UpdateWander(ant, tickIndex, dt);
                TryPickupFood(ant);
            }
            else if (ant.state == AntBehaviorState.ReturnHome)
            {
                UpdateReturnHome(ant, dt);
            }
            else
            {
                UpdateFightMotion(ant, dt);
            }

            TryEnterNestFight(ant);

            if (ant.state == AntBehaviorState.Fight)
            {
                ant.fightTicksRemaining--;
                ant.hp -= recipe.antDpsPerTick;

                if (ant.fightTargetAntId >= 0)
                {
                    if (!pairDamage.ContainsKey(ant.fightTargetAntId)) pairDamage[ant.fightTargetAntId] = 0f;
                    pairDamage[ant.fightTargetAntId] += recipe.antDpsPerTick;
                }
                else if (ant.fightTargetNestId >= 0 && ant.fightTargetNestId < worldState.nests.Count)
                {
                    var nest = worldState.nests[ant.fightTargetNestId];
                    nest.hp = Mathf.Max(0, nest.hp - Mathf.RoundToInt(recipe.nestDpsPerTick));
                    worldState.nests[ant.fightTargetNestId] = nest;
                }

                if (ant.fightTicksRemaining <= 0)
                {
                    ant.state = ant.carrying ? AntBehaviorState.ReturnHome : AntBehaviorState.Wander;
                    ant.fightTargetAntId = -1;
                    ant.fightTargetNestId = -1;
                }
            }

            ClampAndApply(ant, i);
        }

        ResolveAntCollisionsForFight();

        foreach (var kv in pairDamage)
        {
            if (antIdToIndex.TryGetValue(kv.Key, out var idx))
            {
                ants[idx].hp -= kv.Value;
            }
        }

        for (var i = ants.Count - 1; i >= 0; i--)
        {
            if (ants[i].hp <= 0f || !ants[i].isAlive)
            {
                KillAnt(i);
            }
            else
            {
                ApplyVisual(i, tickIndex);
            }
        }

        SyncFoodView();
    }

    public void Shutdown()
    {
        for (var i = 0; i < antViews.Count; i++)
        {
            if (antViews[i]?.root != null)
            {
                Destroy(antViews[i].root.gameObject);
            }
        }

        if (antWorldViewRoot != null)
        {
            Destroy(antWorldViewRoot.gameObject);
            antWorldViewRoot = null;
        }

        ants.Clear();
        antViews.Clear();
        antIdToIndex.Clear();
        foodRenderers.Clear();
        worldState = null;
        recipe = null;
        artSelector = null;
        activePipeline = null;
        antConfig = null;
    }


    private void SpawnInitialAnts()
    {
        if (worldState == null || worldState.nests == null || worldState.nests.Count == 0)
        {
            return;
        }

        var nestCount = Mathf.Clamp(antConfig?.nestCount ?? worldState.nests.Count, 1, worldState.nests.Count);
        var antsPerNest = Mathf.Max(0, antConfig?.antsPerNest ?? 12);
        var maxAntsTotal = Mathf.Max(1, antConfig?.maxAntsTotal ?? 50);
        var remainingBudget = maxAntsTotal - ants.Count;

        for (var nestIndex = 0; nestIndex < nestCount && remainingBudget > 0; nestIndex++)
        {
            var spawnCount = Mathf.Min(antsPerNest, remainingBudget);
            for (var i = 0; i < spawnCount; i++)
            {
                SpawnAntAtNest(nestIndex);
            }

            remainingBudget -= spawnCount;
        }

        RebuildAntIndex();
    }

    private void SpawnAnts(int tickIndex)
    {
        if (tickIndex % recipe.spawnEveryNTicks != 0)
        {
            return;
        }

        var maxAntsTotal = Mathf.Max(1, antConfig?.maxAntsTotal ?? recipe.maxAntsGlobal);

        if (ants.Count >= maxAntsTotal)
        {
            return;
        }

        var antsPerNest = new int[worldState.nests.Count];
        for (var i = 0; i < ants.Count; i++)
        {
            if (ants[i].isAlive && ants[i].homeNestId >= 0 && ants[i].homeNestId < antsPerNest.Length)
            {
                antsPerNest[ants[i].homeNestId]++;
            }
        }

        for (var nestIndex = 0; nestIndex < worldState.nests.Count; nestIndex++)
        {
            if (ants.Count >= maxAntsTotal || antsPerNest[nestIndex] >= recipe.maxAntsPerNest)
            {
                continue;
            }

            SpawnAntAtNest(nestIndex);
            antsPerNest[nestIndex]++;
        }

        RebuildAntIndex();
    }

    private void SpawnAntAtNest(int nestIndex)
    {
        var nest = worldState.nests[nestIndex];
        var antId = nextAntId++;

        var spawnRng = RngService.Fork($"ANTS:SPAWN:{antId}");
        var spawnSeed = spawnRng.Seed;
        var angle = spawnRng.Range(0f, Mathf.PI * 2f);
        var radius = spawnRng.Range(0f, recipe.spawnOffsetRadius);
        var pos = nest.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        var identity = IdentityService.Create(
            entityId: antId,
            teamId: nest.teamId,
            role: "worker",
            variantCount: 8,
            scenarioSeed: spawnSeed,
            simIdOrSalt: "AntColonies:A3");

        var ant = new AntAgentState
        {
            id = antId,
            speciesId = nestIndex,
            teamId = nest.teamId,
            homeNestId = nestIndex,
            identity = identity,
            position = pos,
            velocity = Vector2.right * recipe.walkSpeed,
            maxHp = recipe.antMaxHp,
            hp = recipe.antMaxHp,
            ageTicks = 0,
            carrying = false,
            carriedAmount = 0,
            state = AntBehaviorState.Wander,
            fightTicksRemaining = 0,
            fightTargetAntId = -1,
            fightTargetNestId = -1,
            wanderHeading = angle,
            nextTurnTick = 0,
            isAlive = true
        };

        ants.Add(ant);
        antViews.Add(CreateAntView(ant));

        if (logSpawnIdentity && ant.id < SpawnDebugCount)
        {
            Debug.Log($"{nameof(AntColoniesRunner)} spawn[{ant.id}] {identity}");
        }
    }

    private void RespawnFood(int tickIndex)
    {
        for (var i = 0; i < worldState.foodPiles.Count; i++)
        {
            var food = worldState.foodPiles[i];
            if (food.remaining > 0)
            {
                continue;
            }

            if (food.respawnAtTick < 0)
            {
                food.respawnAtTick = tickIndex + recipe.foodRespawnDelayTicks;
            }
            else if (tickIndex >= food.respawnAtTick)
            {
                food.position = PickRespawnFoodPosition(food.id, tickIndex);
                food.remaining = recipe.foodAmount;
                food.respawnAtTick = -1;
            }

            worldState.foodPiles[i] = food;
        }
    }

    private Vector2 PickRespawnFoodPosition(int foodId, int tickIndex)
    {
        var rng = RngService.Fork($"ANTS:FOOD_RESPAWN:{foodId}:{tickIndex}");
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var edge = rng.Range(0, 4);
            Vector2 p;
            switch (edge)
            {
                case 0: p = new Vector2(rng.Range(-halfWidth + recipe.foodEdgeMargin, halfWidth - recipe.foodEdgeMargin), halfHeight - recipe.foodEdgeMargin); break;
                case 1: p = new Vector2(rng.Range(-halfWidth + recipe.foodEdgeMargin, halfWidth - recipe.foodEdgeMargin), -halfHeight + recipe.foodEdgeMargin); break;
                case 2: p = new Vector2(-halfWidth + recipe.foodEdgeMargin, rng.Range(-halfHeight + recipe.foodEdgeMargin, halfHeight - recipe.foodEdgeMargin)); break;
                default: p = new Vector2(halfWidth - recipe.foodEdgeMargin, rng.Range(-halfHeight + recipe.foodEdgeMargin, halfHeight - recipe.foodEdgeMargin)); break;
            }

            var tooCloseToNest = false;
            for (var i = 0; i < worldState.nests.Count; i++)
            {
                if ((worldState.nests[i].position - p).sqrMagnitude < 16f)
                {
                    tooCloseToNest = true;
                    break;
                }
            }

            if (!tooCloseToNest)
            {
                return p;
            }
        }

        return Vector2.zero;
    }

    private void UpdateWander(AntAgentState ant, int tickIndex, float dt)
    {
        if (tickIndex >= ant.nextTurnTick)
        {
            var turnNoise = Hash01(ant.id, tickIndex, 11) * 2f - 1f;
            ant.wanderHeading += turnNoise * recipe.wanderTurnRadians;
            var interval = Mathf.RoundToInt(Mathf.Lerp(recipe.wanderTurnIntervalMinTicks, recipe.wanderTurnIntervalMaxTicks, Hash01(ant.id, tickIndex, 17)));
            ant.nextTurnTick = tickIndex + interval;
        }

        var dir = new Vector2(Mathf.Cos(ant.wanderHeading), Mathf.Sin(ant.wanderHeading));
        dir += ComputeRepulsion(ant.position);
        if (dir.sqrMagnitude > 0.0001f)
        {
            dir.Normalize();
        }

        ant.velocity = dir * recipe.walkSpeed;
        ant.position += ant.velocity * dt;
    }

    private void UpdateReturnHome(AntAgentState ant, float dt)
    {
        var nestPos = worldState.nests[ant.homeNestId].position;
        var toNest = nestPos - ant.position;
        var distance = toNest.magnitude;
        var dir = distance > 0.001f ? toNest / distance : Vector2.zero;
        ant.velocity = dir * recipe.runSpeed;
        ant.position += ant.velocity * dt;

        if (distance <= recipe.depositRadius)
        {
            var nest = worldState.nests[ant.homeNestId];
            nest.foodStored += ant.carriedAmount;
            worldState.nests[ant.homeNestId] = nest;

            ant.carrying = false;
            ant.carriedAmount = 0;
            ant.state = AntBehaviorState.Wander;
        }
    }

    private void UpdateFightMotion(AntAgentState ant, float dt)
    {
        ant.velocity *= 0.8f;
        ant.position += ant.velocity * dt;
    }

    private Vector2 ComputeRepulsion(Vector2 position)
    {
        var push = Vector2.zero;
        for (var i = 0; i < worldState.obstacles.Count; i++)
        {
            var obstacle = worldState.obstacles[i];
            var toAnt = position - obstacle.position;
            var dist = toAnt.magnitude;
            var radius = obstacle.radius + 0.8f;
            if (dist < radius && dist > 0.001f)
            {
                push += (toAnt / dist) * ((radius - dist) / radius);
            }
        }

        for (var i = 0; i < worldState.nests.Count; i++)
        {
            var nest = worldState.nests[i];
            var toAnt = position - nest.position;
            var dist = toAnt.magnitude;
            if (dist < 1.5f && dist > 0.001f)
            {
                push += (toAnt / dist) * 0.4f;
            }
        }

        return push;
    }

    private void TryPickupFood(AntAgentState ant)
    {
        if (ant.carrying)
        {
            return;
        }

        var bestIndex = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < worldState.foodPiles.Count; i++)
        {
            var food = worldState.foodPiles[i];
            if (food.remaining <= 0)
            {
                continue;
            }

            var dist = Vector2.Distance(ant.position, food.position);
            if (dist <= recipe.foodSenseRadius)
            {
                if (dist < bestDist || (Mathf.Abs(dist - bestDist) < 0.0001f && food.id < worldState.foodPiles[bestIndex].id))
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
        }

        if (bestIndex < 0)
        {
            return;
        }

        var selected = worldState.foodPiles[bestIndex];
        if (bestDist <= recipe.pickupRadius && selected.remaining > 0)
        {
            selected.remaining = Mathf.Max(0, selected.remaining - 1);
            if (selected.remaining <= 0)
            {
                selected.respawnAtTick = -1;
            }

            worldState.foodPiles[bestIndex] = selected;

            ant.carrying = true;
            ant.carriedAmount = 1;
            ant.state = AntBehaviorState.ReturnHome;
        }
    }

    private void TryEnterNestFight(AntAgentState ant)
    {
        if (ant.state == AntBehaviorState.Fight)
        {
            return;
        }

        for (var i = 0; i < worldState.nests.Count; i++)
        {
            var nest = worldState.nests[i];
            if (nest.teamId == ant.teamId || nest.hp <= 0)
            {
                continue;
            }

            if (Vector2.Distance(ant.position, nest.position) <= recipe.enemyNestAggroRadius)
            {
                ant.state = AntBehaviorState.Fight;
                ant.fightTicksRemaining = recipe.fightDurationTicks;
                ant.fightTargetAntId = -1;
                ant.fightTargetNestId = i;
                return;
            }
        }
    }

    private void ResolveAntCollisionsForFight()
    {
        var sqrCollision = recipe.antCollisionRadius * recipe.antCollisionRadius;
        for (var i = 0; i < ants.Count; i++)
        {
            if (!ants[i].isAlive)
            {
                continue;
            }

            for (var j = i + 1; j < ants.Count; j++)
            {
                if (!ants[j].isAlive || ants[i].teamId == ants[j].teamId)
                {
                    continue;
                }

                if ((ants[i].position - ants[j].position).sqrMagnitude <= sqrCollision)
                {
                    EnterFightAgainstAnt(ants[i], ants[j].id);
                    EnterFightAgainstAnt(ants[j], ants[i].id);
                }
            }
        }
    }

    private void EnterFightAgainstAnt(AntAgentState ant, int targetId)
    {
        ant.state = AntBehaviorState.Fight;
        ant.fightTicksRemaining = recipe.fightDurationTicks;
        ant.fightTargetAntId = targetId;
        ant.fightTargetNestId = -1;
    }

    private void ClampAndApply(AntAgentState ant, int index)
    {
        ant.position.x = Mathf.Clamp(ant.position.x, -halfWidth, halfWidth);
        ant.position.y = Mathf.Clamp(ant.position.y, -halfHeight, halfHeight);
        ants[index] = ant;
    }

    private void KillAnt(int index)
    {
        ants[index].isAlive = false;

        if (antViews[index]?.root != null)
        {
            Destroy(antViews[index].root.gameObject);
        }

        ants.RemoveAt(index);
        antViews.RemoveAt(index);
        RebuildAntIndex();
    }

    private AntAgentView CreateAntView(AntAgentState ant)
    {
        var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, ant.teamId);
        var root = new GameObject($"Sim_{ant.id:0000}");
        root.transform.SetParent(groupRoot, false);

        GameObject pipelineRenderer = null;
        SpriteRenderer baseRenderer = null;

        var visualKey = VisualKeyBuilder.Create(
            simulationId: "AntColonies",
            entityType: "ant",
            instanceId: ant.id,
            kind: string.IsNullOrWhiteSpace(ant.identity.role) ? $"species-{ant.speciesId}" : ant.identity.role,
            state: "idle",
            facingMode: FacingMode.Auto,
            groupId: ant.teamId);

        if (activePipeline != null)
        {
            pipelineRenderer = activePipeline.CreateRenderer(visualKey, root.transform);
            if (pipelineRenderer != null)
            {
                baseRenderer = pipelineRenderer.GetComponent<SpriteRenderer>() ?? pipelineRenderer.GetComponentInChildren<SpriteRenderer>();
                if (baseRenderer != null)
                {
                    RenderOrder.Apply(baseRenderer, RenderOrder.EntityBody);
                }
            }
        }

        if (baseRenderer == null)
        {
            var baseObj = new GameObject("Base");
            baseObj.transform.SetParent(root.transform, false);
            baseRenderer = baseObj.AddComponent<SpriteRenderer>();
            RenderOrder.Apply(baseRenderer, RenderOrder.EntityBody);
        }

        var maskObj = new GameObject("Mask");
        maskObj.transform.SetParent(root.transform, false);
        var maskRenderer = maskObj.AddComponent<SpriteRenderer>();
        RenderOrder.Apply(maskRenderer, RenderOrder.EntityBody + 1);

        SpriteRenderer hpBgRenderer = null;
        SpriteRenderer hpFillRenderer = null;
        if (showHealthBars)
        {
            var hpBgObj = new GameObject("HpBg");
            hpBgObj.transform.SetParent(root.transform, false);
            hpBgObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            hpBgObj.transform.localScale = new Vector3(0.5f, 0.08f, 1f);
            hpBgRenderer = hpBgObj.AddComponent<SpriteRenderer>();
            hpBgRenderer.sprite = GetSquareSprite();
            hpBgRenderer.color = new Color(0f, 0f, 0f, 0.8f);
            RenderOrder.Apply(hpBgRenderer, RenderOrder.EntityFx);

            var hpFillObj = new GameObject("HpFill");
            hpFillObj.transform.SetParent(root.transform, false);
            hpFillObj.transform.localPosition = new Vector3(-0.25f, 0.75f, 0f);
            hpFillObj.transform.localScale = new Vector3(0.5f, 0.06f, 1f);
            hpFillRenderer = hpFillObj.AddComponent<SpriteRenderer>();
            hpFillRenderer.sprite = GetSquareSprite();
            hpFillRenderer.color = new Color(0.2f, 0.95f, 0.2f, 1f);
            RenderOrder.Apply(hpFillRenderer, RenderOrder.EntityFx + 1);
        }

        return new AntAgentView
        {
            antId = ant.id,
            root = root.transform,
            pipelineRenderer = pipelineRenderer,
            baseRenderer = baseRenderer,
            maskRenderer = maskRenderer,
            hpBgRenderer = hpBgRenderer,
            hpFillRenderer = hpFillRenderer,
            visualKey = visualKey,
            lastPos = root.transform.position,
            hasLastPos = false
        };
    }

    private void ApplyVisual(int index, int tickIndex)
    {
        var ant = ants[index];
        var view = antViews[index];

        var state = ant.state == AntBehaviorState.Fight
            ? "fight"
            : (ant.velocity.magnitude >= recipe.runSpeed * 0.8f ? "run" : (ant.velocity.magnitude >= recipe.walkSpeed * 0.4f ? "walk" : "idle"));
        view.visualKey.state = state;

        var species = worldState.nests[Mathf.Clamp(ant.speciesId, 0, worldState.nests.Count - 1)].speciesId;
        var frame = ResolveFrameFromContract(state, tickIndex);

        var baseId = $"agent:ant:{species}:worker:adult:{state}:{frame:00}";
        var maskId = baseId + "_mask";

        view.baseRenderer.sprite = ContentPackService.TryGetSprite(baseId, out var baseSprite) ? baseSprite : GetFallbackAntSprite();
        view.baseRenderer.color = ContentPackService.Current == null ? GetTeamColor(ant.teamId) : Color.white;

        view.maskRenderer.sprite = ContentPackService.TryGetSprite(maskId, out var maskSprite) ? maskSprite : null;
        view.maskRenderer.color = new Color(GetTeamColor(ant.teamId).r, GetTeamColor(ant.teamId).g, GetTeamColor(ant.teamId).b, 0.85f);

        view.root.localPosition = new Vector3(ant.position.x, ant.position.y, 0f);
        view.root.localRotation = Quaternion.identity;

        var pos = (Vector2)view.root.position;
        var vel = view.hasLastPos
            ? (pos - view.lastPos) / Mathf.Max(0.0001f, Time.deltaTime)
            : Vector2.zero;
        view.lastPos = pos;
        view.hasLastPos = true;

        var bodyRotation = Quaternion.identity;
        if (ant.velocity.sqrMagnitude > 0.0001f)
        {
            var angle = Mathf.Atan2(ant.velocity.y, ant.velocity.x) * Mathf.Rad2Deg;
            var snapped = Mathf.Round(angle / 45f) * 45f;
            bodyRotation = Quaternion.Euler(0f, 0f, snapped);
        }

        view.baseRenderer.transform.localRotation = bodyRotation;
        view.maskRenderer.transform.localRotation = bodyRotation;

        if (activePipeline != null && view.pipelineRenderer != null)
        {
            activePipeline.ApplyVisual(view.pipelineRenderer, view.visualKey, vel, Time.deltaTime);
        }

        if (!showHealthBars || view.hpFillRenderer == null)
        {
            return;
        }

        var hpPct = Mathf.Clamp01(ant.hp / Mathf.Max(0.01f, ant.maxHp));
        view.hpFillRenderer.transform.localScale = new Vector3(0.5f * hpPct, 0.06f, 1f);
        view.hpFillRenderer.transform.localPosition = new Vector3(-0.25f + 0.25f * hpPct, 0.75f, 0f);
    }

    private void ResolveArtPipeline()
    {
        artSelector = UnityEngine.Object.FindFirstObjectByType<ArtModeSelector>()
            ?? UnityEngine.Object.FindAnyObjectByType<ArtModeSelector>();

        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
        if (activePipeline != null)
        {
            Debug.Log($"{nameof(AntColoniesRunner)} using art pipeline '{activePipeline.DisplayName}' ({activePipeline.Mode}).");
            return;
        }

        Debug.Log($"{nameof(AntColoniesRunner)} no {nameof(ArtModeSelector)} / active pipeline found; using default ant renderers.");
    }

    private int ResolveFrameFromContract(string state, int tickIndex)
    {
        var fps = ContentPackService.GetClipFpsOrDefault("ant", "worker", "adult", state, 8);
        var ticksPerFrame = Mathf.Max(1, Mathf.RoundToInt(60f / Mathf.Max(1, fps)));
        var t = (tickIndex / ticksPerFrame);

        return state switch
        {
            "idle" => t % 2,
            "walk" => 2 + (t % 3),
            "run" => 5 + (t % 4),
            "fight" => 9,
            _ => 0
        };
    }

    private void CacheWorldRenderers()
    {
        foodRenderers.Clear();
        var foodRoot = antWorldViewRoot != null ? antWorldViewRoot.Find("Food") : null;
        if (foodRoot == null)
        {
            return;
        }

        for (var i = 0; i < worldState.foodPiles.Count; i++)
        {
            var child = foodRoot.Find($"Food_{worldState.foodPiles[i].id:00}");
            foodRenderers.Add(child != null ? child.GetComponent<SpriteRenderer>() : null);
        }
    }

    private void SyncFoodView()
    {
        for (var i = 0; i < worldState.foodPiles.Count && i < foodRenderers.Count; i++)
        {
            var renderer = foodRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            var pile = worldState.foodPiles[i];
            renderer.enabled = pile.remaining > 0;
            renderer.transform.localPosition = new Vector3(pile.position.x, pile.position.y, 0f);
        }
    }

    private Color GetTeamColor(int teamId)
    {
        for (var i = 0; i < worldState.nests.Count; i++)
        {
            if (worldState.nests[i].teamId == teamId)
            {
                return worldState.nests[i].teamColor;
            }
        }

        return Color.white;
    }

    private void RebuildAntIndex()
    {
        antIdToIndex.Clear();
        for (var i = 0; i < ants.Count; i++)
        {
            antIdToIndex[ants[i].id] = i;
        }
    }

    private static float Hash01(int a, int b, int salt)
    {
        unchecked
        {
            uint n = (uint)(a * 374761393) ^ (uint)(b * 668265263) ^ unchecked((uint)salt * 2246822519u);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0x00FFFFFF) / 16777215f;
        }
    }

    private static Sprite GetFallbackAntSprite()
    {
        if (fallbackAntSprite != null)
        {
            return fallbackAntSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);

        fallbackAntSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return fallbackAntSprite;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        var tx = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tx.SetPixel(0, 0, Color.white);
        tx.Apply(false, false);
        squareSprite = Sprite.Create(tx, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
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
