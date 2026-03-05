using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 1;

    protected override WorldMap GenerateTyped(SavannaRiverSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);
        var river = new WorldSpline { id = "river_main", baseWidth = settings.riverWidth };

        var pts = Mathf.Max(12, grid.width / 2);
        for (var i = 0; i < pts; i++)
        {
            var t = i / (float)(pts - 1);
            var x = Mathf.Lerp(0, grid.width - 1, t);
            var yCenter = grid.height * 0.5f + Mathf.Sin(t * Mathf.PI * 2f * settings.meanderFreq) * settings.meanderAmp * grid.height;
            river.points.Add(grid.CellCenterWorld(Mathf.RoundToInt(x), Mathf.Clamp(Mathf.RoundToInt(yCenter), 0, grid.height - 1)));
        }
        map.splines.Add(river);

        var height = new ScalarField("height", grid);
        var wetness = new ScalarField("wetness", grid);
        var water = new MaskField("water", grid, MaskEncoding.Boolean);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var biomes = new MaskField("biomes", grid, MaskEncoding.Categorical) { categories = new[] { "savanna", "wetland", "rocky", "water" } };
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "river", "north_plain", "south_plain" } };

        noise.Register(settings.HeightNoise, seed);
        noise.Register(settings.WetnessNoise, seed);
        noise.Register(settings.WarpNoise, seed);
        var heightNoise = noise.Get(settings.HeightNoise.id);
        var wetnessNoise = noise.Get(settings.WetnessNoise.id);
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dir = settings.gradientDir.normalized;
            var grad = ((x / (float)grid.width) * dir.x + (y / (float)grid.height) * dir.y) * 0.5f + 0.5f;
            var dist = MinDistanceToSpline(river, p);
            var riverFactor = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, settings.floodplainWidth));
            var h = grad + NoiseUtil.Sample2D(heightNoise, x, y, seed) * 0.2f - riverFactor * settings.carveStrength;
            height[x, y] = h;
            wetness[x, y] = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, settings.floodplainWidth) + NoiseUtil.Sample2D(wetnessNoise, x + 73, y + 19, seed) * 0.2f);

            var isWater = dist <= settings.riverWidth;
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);

            byte biome = 0;
            if (isWater) biome = 3;
            else if (wetness[x, y] > 0.65f) biome = 1;
            else if (h > settings.waterLevel + 0.3f) biome = 2;
            biomes[x, y] = biome;

            zones[x, y] = (byte)(isWater ? 0 : (y > grid.height / 2 ? 1 : 2));
        }

        height.Normalize01InPlace();
        wetness.Normalize01InPlace();

        map.scalars["height"] = height;
        map.scalars["wetness"] = wetness;
        map.masks["water"] = water;
        map.masks["walkable"] = walkable;
        map.masks["biomes"] = biomes;
        map.masks["zones"] = zones;

        var trees = new ScatterSet { id = "trees" };
        var rocks = new ScatterSet { id = "rocks" };
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var wet = wetness[x, y];
            var biome = biomes[x, y];
            var pos = grid.CellCenterWorld(x, y);
            if (biome != 3 && wet > 0.45f && rng.NextFloat01() < settings.treeDensity)
                trees.points.Add(new ScatterPoint { pos = pos, scale = 0.8f + rng.NextFloat01() * 0.8f, typeId = 0, tags = new[] { "tree" } });
            if (biome == 2 && rng.NextFloat01() < settings.rockDensity)
                rocks.points.Add(new ScatterPoint { pos = pos, scale = 0.7f + rng.NextFloat01() * 1.1f, typeId = 0, tags = new[] { "rock" } });
        }
        map.scatters["trees"] = trees;
        map.scatters["rocks"] = rocks;

        map.zones["river"] = new ZoneDef { zoneId = 0, name = "river" };
        map.zones["north_plain"] = new ZoneDef { zoneId = 1, name = "north_plain" };
        map.zones["south_plain"] = new ZoneDef { zoneId = 2, name = "south_plain" };
        map.EnsureRequiredOutputs();
        log.Log($"Generated SavannaRiver trees={trees.points.Count} rocks={rocks.points.Count}");
        return map;
    }

    private static float MinDistanceToSpline(WorldSpline spline, Vector2 p)
    {
        var minDist = float.MaxValue;
        for (var i = 1; i < spline.points.Count; i++)
        {
            var d = DistanceToSegment(p, spline.points[i - 1], spline.points[i]);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.sqrMagnitude;
        if (denom < 1e-6f) return Vector2.Distance(p, a);
        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        return Vector2.Distance(p, a + ab * t);
    }
}
