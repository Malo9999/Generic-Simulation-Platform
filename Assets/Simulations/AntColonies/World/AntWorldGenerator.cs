using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AntWorldGenerator
{
    private static readonly string[] SpeciesOrder = { "FireAnt", "CarpenterAnt", "PharaohAnt", "ArmyAnt", "WeaverAnt" };
    private static readonly Color[] TeamColors =
    {
        new(0.86f, 0.24f, 0.19f),
        new(0.56f, 0.36f, 0.19f),
        new(0.88f, 0.76f, 0.24f),
        new(0.36f, 0.62f, 0.22f),
        new(0.24f, 0.58f, 0.82f)
    };

    public static AntWorldState Generate(ScenarioConfig config)
    {
        var recipe = config?.antColonies?.worldRecipe ?? new AntWorldRecipe();
        recipe.Normalize();

        var halfWidth = Mathf.Max(8f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        var halfHeight = Mathf.Max(8f, (config?.world?.arenaHeight ?? 64) * 0.5f);
        var rng = RngService.Fork("ANTS:WORLDv1");

        var state = new AntWorldState();
        var propIds = GetSpriteIdsByPrefix("prop:");
        var tileIds = GetSpriteIdsByPrefix("tile:");
        propIds.Sort(StringComparer.Ordinal);
        tileIds.Sort(StringComparer.Ordinal);

        PlaceNests(state, recipe, halfWidth, halfHeight, rng);
        PlaceFood(state, recipe, halfWidth, halfHeight, rng);
        PlaceObstacles(state, recipe, halfWidth, halfHeight, rng);
        PlaceDecor(state, recipe, halfWidth, halfHeight, rng, propIds);
        BuildTiles(state, recipe, rng, tileIds, halfWidth, halfHeight);

        return state;
    }

    private static void PlaceNests(AntWorldState state, AntWorldRecipe recipe, float halfWidth, float halfHeight, IRng rng)
    {
        var speciesOrder = ResolveSpeciesOrder();
        var minDistance = recipe.nestMinDistance;
        for (var team = 0; team < 5; team++)
        {
            var placed = false;
            for (var relax = 0; relax < 5 && !placed; relax++)
            {
                var requiredDistance = minDistance * (1f - (relax * 0.12f));
                for (var attempt = 0; attempt < 200; attempt++)
                {
                    var p = new Vector2(
                        rng.Range(-halfWidth + recipe.nestBorderMargin, halfWidth - recipe.nestBorderMargin),
                        rng.Range(-halfHeight + recipe.nestBorderMargin, halfHeight - recipe.nestBorderMargin));

                    var ok = true;
                    for (var i = 0; i < state.nests.Count; i++)
                    {
                        if ((state.nests[i].position - p).sqrMagnitude < requiredDistance * requiredDistance)
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (!ok)
                    {
                        continue;
                    }

                    state.nests.Add(new AntWorldState.NestEntry
                    {
                        speciesId = speciesOrder[team % speciesOrder.Count],
                        teamId = team,
                        position = p,
                        hp = recipe.nestHp,
                        teamColor = TeamColors[team],
                        foodStored = 0
                    });
                    placed = true;
                    break;
                }
            }
        }
    }

    private static void PlaceFood(AntWorldState state, AntWorldRecipe recipe, float halfWidth, float halfHeight, IRng rng)
    {
        for (var i = 0; i < recipe.foodCount; i++)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var edge = rng.Range(0, 4);
                Vector2 p;
                switch (edge)
                {
                    case 0:
                        p = new Vector2(rng.Range(-halfWidth + recipe.foodEdgeMargin, halfWidth - recipe.foodEdgeMargin), halfHeight - recipe.foodEdgeMargin);
                        break;
                    case 1:
                        p = new Vector2(rng.Range(-halfWidth + recipe.foodEdgeMargin, halfWidth - recipe.foodEdgeMargin), -halfHeight + recipe.foodEdgeMargin);
                        break;
                    case 2:
                        p = new Vector2(-halfWidth + recipe.foodEdgeMargin, rng.Range(-halfHeight + recipe.foodEdgeMargin, halfHeight - recipe.foodEdgeMargin));
                        break;
                    default:
                        p = new Vector2(halfWidth - recipe.foodEdgeMargin, rng.Range(-halfHeight + recipe.foodEdgeMargin, halfHeight - recipe.foodEdgeMargin));
                        break;
                }

                if (state.nests.Any(n => (n.position - p).sqrMagnitude < 36f))
                {
                    continue;
                }

                state.foodPiles.Add(new AntWorldState.FoodPileEntry
                {
                    id = i,
                    position = p,
                    remaining = recipe.foodAmount,
                    respawnAtTick = -1
                });
                break;
            }
        }
    }

    private static void PlaceObstacles(AntWorldState state, AntWorldRecipe recipe, float halfWidth, float halfHeight, IRng rng)
    {
        var count = rng.Range(recipe.obstacleCountMin, recipe.obstacleCountMax + 1);
        for (var i = 0; i < count; i++)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var radius = rng.Range(recipe.obstacleRadiusRange.x, recipe.obstacleRadiusRange.y);
                var p = new Vector2(
                    rng.Range(-halfWidth + radius + 1f, halfWidth - radius - 1f),
                    rng.Range(-halfHeight + radius + 1f, halfHeight - radius - 1f));

                if (state.nests.Any(n => (n.position - p).sqrMagnitude < Mathf.Pow(radius + 3f, 2f)) ||
                    state.foodPiles.Any(f => (f.position - p).sqrMagnitude < Mathf.Pow(radius + 2f, 2f)) ||
                    state.obstacles.Any(o => (o.position - p).sqrMagnitude < Mathf.Pow(radius + o.radius + 1f, 2f)))
                {
                    continue;
                }

                state.obstacles.Add(new AntWorldState.ObstacleEntry { position = p, radius = radius });
                break;
            }
        }
    }

    private static void PlaceDecor(AntWorldState state, AntWorldRecipe recipe, float halfWidth, float halfHeight, IRng rng, List<string> propIds)
    {
        var count = Mathf.Clamp(recipe.decorTargetCount, recipe.decorCountMin, recipe.decorCountMax);
        count = Mathf.Clamp(count, 80, 200);

        var grass = FilterByAny(propIds, "grass", "tuft");
        var stones = FilterByAny(propIds, "stone", "rock", "pebble");
        var plants = FilterByAny(propIds, "plant", "tree", "flower", "leaf");
        grass.Sort(StringComparer.Ordinal);
        stones.Sort(StringComparer.Ordinal);
        plants.Sort(StringComparer.Ordinal);
        var fallbackPool = propIds.Count > 0 ? propIds : new List<string>();

        var occupied = new List<Vector2>();
        var attempts = 0;
        while (state.decor.Count < count && attempts < recipe.decorMaxAttempts)
        {
            attempts++;
            var p = new Vector2(
                rng.Range(-halfWidth + recipe.decorBorderMargin, halfWidth - recipe.decorBorderMargin),
                rng.Range(-halfHeight + recipe.decorBorderMargin, halfHeight - recipe.decorBorderMargin));

            if (p.sqrMagnitude < recipe.decorClearCenterRadius * recipe.decorClearCenterRadius)
            {
                continue;
            }

            var tooClose = false;
            for (var i = 0; i < occupied.Count; i++)
            {
                if ((occupied[i] - p).sqrMagnitude < recipe.decorMinSpacing * recipe.decorMinSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose || state.nests.Any(n => (n.position - p).sqrMagnitude < 4f) || state.foodPiles.Any(f => (f.position - p).sqrMagnitude < 2f))
            {
                continue;
            }

            var roll = rng.Value();
            var pool = roll < 0.45f ? grass : (roll < 0.75f ? stones : plants);
            if (pool.Count == 0)
            {
                pool = fallbackPool;
            }

            var id = pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : string.Empty;
            state.decor.Add(new AntWorldState.DecorEntry
            {
                position = p,
                spriteId = id,
                rotation = rng.Range(0f, 360f),
                scale = rng.Range(0.7f, 1.35f),
                alpha = rng.Range(0.45f, 0.9f)
            });
            occupied.Add(p);
        }
    }

    private static void BuildTiles(AntWorldState state, AntWorldRecipe recipe, IRng rng, List<string> tileIds, float halfWidth, float halfHeight)
    {
        var grassIds = FilterByAny(tileIds, "grass", "surface");
        var dirtIds = FilterByAny(tileIds, "dirt", "ground", "underground");
        var pathIds = FilterByAny(tileIds, "path", "tunnel");

        grassIds.Sort(StringComparer.Ordinal);
        dirtIds.Sort(StringComparer.Ordinal);
        pathIds.Sort(StringComparer.Ordinal);

        if (grassIds.Count == 0) grassIds = tileIds;
        if (dirtIds.Count == 0) dirtIds = grassIds;
        if (pathIds.Count == 0) pathIds = dirtIds;

        var dirtPatchCenters = new List<Vector2>();
        for (var i = 0; i < recipe.dirtPatches; i++)
        {
            dirtPatchCenters.Add(new Vector2(rng.Range(0, AntWorldState.TileSize), rng.Range(0, AntWorldState.TileSize)));
        }

        for (var y = 0; y < AntWorldState.TileSize; y++)
        {
            for (var x = 0; x < AntWorldState.TileSize; x++)
            {
                var idx = (y * AntWorldState.TileSize) + x;
                var noise = Hash01(x, y, rng.Seed);
                var isGrass = noise < recipe.baseGrassRatio;
                for (var p = 0; p < dirtPatchCenters.Count; p++)
                {
                    if ((dirtPatchCenters[p] - new Vector2(x, y)).sqrMagnitude < 16f)
                    {
                        isGrass = false;
                        break;
                    }
                }

                var pool = isGrass ? grassIds : dirtIds;
                state.tileSpriteIds[idx] = pool.Count > 0 ? pool[(x + (y * 13)) % pool.Count] : string.Empty;
            }
        }

        var centerTile = new Vector2((AntWorldState.TileSize - 1) * 0.5f, (AntWorldState.TileSize - 1) * 0.5f);
        for (var i = 0; i < state.nests.Count; i++)
        {
            var local = new Vector2(state.nests[i].position.x / (halfWidth * 2f) + 0.5f, state.nests[i].position.y / (halfHeight * 2f) + 0.5f);
            var start = new Vector2(local.x * (AntWorldState.TileSize - 1), local.y * (AntWorldState.TileSize - 1));
            DrawLine(state, start, centerTile, pathIds, recipe.pathStrength, rng.Seed + (i * 7919));
        }
    }

    private static void DrawLine(AntWorldState state, Vector2 from, Vector2 to, List<string> pathIds, float pathStrength, int salt)
    {
        var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 2f));
        for (var i = 0; i <= steps; i++)
        {
            if (pathIds.Count == 0 || Hash01(Mathf.RoundToInt(from.x) + i, Mathf.RoundToInt(from.y) - i, salt) > pathStrength)
            {
                continue;
            }

            var t = i / (float)steps;
            var p = Vector2.Lerp(from, to, t);
            var x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, AntWorldState.TileSize - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, AntWorldState.TileSize - 1);
            var idx = (y * AntWorldState.TileSize) + x;
            state.pathMask[idx] = true;
            state.tileSpriteIds[idx] = pathIds[(x + y) % pathIds.Count];
        }
    }

    private static float Hash01(int x, int y, int seed)
    {
        var n = unchecked((uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)seed);
        n = (n ^ (n >> 13)) * 1274126177u;
        return (n & 0x00FFFFFF) / 16777215f;
    }

    private static List<string> GetSpriteIdsByPrefix(string prefix)
    {
        var pack = ContentPackService.Current;
        if (pack == null)
        {
            return new List<string>();
        }

        return pack.GetAllSpriteIds().Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }

    private static List<string> FilterByAny(List<string> source, params string[] terms)
    {
        if (source == null || source.Count == 0)
        {
            return new List<string>();
        }

        var filtered = source.Where(id => terms.Any(term => id.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        return filtered;
    }

    private static List<string> ResolveSpeciesOrder()
    {
        var pack = ContentPackService.Current;
        var packSpecies = pack?.Selections?
            .FirstOrDefault(selection => string.Equals(selection.entityId, "ant", StringComparison.OrdinalIgnoreCase))
            .speciesIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        return packSpecies != null && packSpecies.Count > 0 ? packSpecies : SpeciesOrder.ToList();
    }
}
