using UnityEngine;

public class CanyonPassRecipe : WorldRecipeBase<CanyonPassSettingsSO>
{
    public override string RecipeId => "CanyonPass";
    public override int Version => 1;

    protected override WorldMap GenerateTyped(CanyonPassSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);
        noise.Register(settings.HeightNoise, seed);
        noise.Register(settings.WetnessNoise, seed);
        noise.Register(settings.WarpNoise, seed);
        var heightNoise = noise.Get(settings.HeightNoise.id);

        var path = new WorldSpline { id = "path_main", baseWidth = settings.passWidth };
        var pts = Mathf.Max(10, grid.height / 2);
        for (var i = 0; i < pts; i++)
        {
            var t = i / (float)(pts - 1);
            var y = Mathf.Lerp(0, grid.height - 1, t);
            var x = grid.width * 0.5f + Mathf.Sin(t * Mathf.PI * (settings.chokeCount + 1)) * settings.passTwist * grid.width;
            path.points.Add(grid.CellCenterWorld(Mathf.Clamp(Mathf.RoundToInt(x), 0, grid.width - 1), Mathf.RoundToInt(y)));
        }
        map.splines.Add(path);

        var height = new ScalarField("height", grid);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "north_basin", "pass", "south_basin", "cliffs" } };
        var boulders = new ScatterSet { id = "boulders" };

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var d = MinDistanceToSpline(path, p);
            var edge = Mathf.Clamp01(d / Mathf.Max(0.01f, settings.passWidth));
            var h = Mathf.Pow(edge, settings.wallSteepness) * settings.canyonDepth + NoiseUtil.Sample2D(heightNoise, x, y, seed) * 0.08f;
            height[x, y] = h;

            var inPass = d <= settings.passWidth;
            walkable[x, y] = (byte)(inPass ? 1 : 0);

            byte zone;
            if (!inPass && d < settings.passWidth * 1.8f) zone = 3;
            else if (inPass) zone = 1;
            else zone = (byte)(y > grid.height / 2 ? 0 : 2);
            zones[x, y] = zone;

            if (zone == 3 && rng.NextFloat01() < settings.boulderDensity)
            {
                boulders.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 1.4f, typeId = 0, tags = new[] { "boulder" } });
            }
        }

        height.Normalize01InPlace();
        map.scalars["height"] = height;
        map.masks["walkable"] = walkable;
        map.masks["zones"] = zones;
        map.scatters["boulders"] = boulders;

        map.zones["north_basin"] = new ZoneDef { zoneId = 0, name = "north_basin" };
        map.zones["pass"] = new ZoneDef { zoneId = 1, name = "pass" };
        map.zones["south_basin"] = new ZoneDef { zoneId = 2, name = "south_basin" };
        map.zones["cliffs"] = new ZoneDef { zoneId = 3, name = "cliffs" };

        map.EnsureRequiredOutputs();
        log.Log($"Generated CanyonPass boulders={boulders.points.Count}");
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
