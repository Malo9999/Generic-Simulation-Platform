using System.Collections.Generic;
using UnityEngine;

public class CanyonPassRecipe : WorldRecipeBase<CanyonPassSettingsSO>
{
    public override string RecipeId => "CanyonPass";
    public override int Version => 2;

    protected override WorldMap GenerateTyped(CanyonPassSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);

        noise.Register(settings.HeightNoise, seed);
        noise.Register(settings.WetnessNoise, seed);
        noise.Register(settings.WarpNoise, seed);
        var heightNoise = noise.Get(settings.HeightNoise.id);
        var warpNoise = noise.Get(settings.WarpNoise.id);

        var mapRect = new Rect(grid.originWorld.x, grid.originWorld.y, grid.width * grid.cellSize, grid.height * grid.cellSize);
        var path = BuildPassSpline(settings, grid, seed, warpNoise, mapRect);
        map.splines.AddRange(SplineClipper.ClipToRectParts(path, mapRect));

        var chokeCenters = BuildChokes(settings, rng);

        var height = new ScalarField("height", grid);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "north_basin", "pass", "south_basin", "cliffs" } };
        var boulders = new ScatterSet { id = "boulders" };

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var along = PolylineProjectionT(path.points, p, out var distanceToPass);
            var corridorWidth = CorridorWidth(settings, along, chokeCenters);

            var asymmetryNoise = NoiseUtil.Sample2D(warpNoise, p.x * 0.02f, p.y * 0.02f, seed) * 2f - 1f;
            var leftBias = asymmetryNoise * settings.asymmetryStrength;
            var baseShape = Mathf.Exp(-distanceToPass * Mathf.Max(0.1f, settings.wallSteepness) / Mathf.Max(0.01f, corridorWidth));
            var n = NoiseUtil.Sample2D(heightNoise, p.x, p.y, seed) * 2f - 1f;
            var h = settings.canyonDepth * (1f - baseShape) + n * settings.noiseStrength + leftBias;
            height[x, y] = h;

            var erosionNoise = NoiseUtil.Sample2D(warpNoise, p.x * 0.08f, p.y * 0.08f, seed) * 2f - 1f;
            var walkableHalfWidth = corridorWidth * 0.5f + erosionNoise * grid.cellSize * 0.8f;
            var inPass = distanceToPass < walkableHalfWidth;
            walkable[x, y] = (byte)(inPass ? 1 : 0);

            byte zone;
            if (!inPass && distanceToPass < corridorWidth * 0.95f) zone = 3;
            else if (inPass) zone = 1;
            else zone = (byte)(y > grid.height / 2 ? 0 : 2);
            zones[x, y] = zone;

            var edgeBand = 1f - Mathf.Abs(distanceToPass - walkableHalfWidth) / Mathf.Max(0.01f, grid.cellSize * 2.5f);
            var wallWeight = Mathf.Clamp01(edgeBand) * Mathf.Clamp01((n + 1f) * 0.5f);
            if (!inPass && rng.NextFloat01() < settings.boulderDensity * wallWeight)
                boulders.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 1.4f, typeId = 0, tags = new[] { "boulder" } });
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
        log.Log($"Generated CanyonPass boulders={boulders.points.Count} chokes={chokeCenters.Count}");
        return map;
    }

    private static WorldSpline BuildPassSpline(CanyonPassSettingsSO settings, WorldGridSpec grid, int seed, NoiseDescriptor warpNoise, Rect mapRect)
    {
        var start = new Vector2(Mathf.Lerp(mapRect.xMin, mapRect.xMax, 0.15f), mapRect.yMax);
        var exit = new Vector2(Mathf.Lerp(mapRect.xMin, mapRect.xMax, 0.85f), mapRect.yMin);
        var heading = (exit - start).normalized;
        var step = Mathf.Max(0.5f, grid.cellSize);
        var maxSteps = (grid.width + grid.height) * 4;

        var points = new List<Vector2> { start };
        var p = start;
        for (var i = 0; i < maxSteps; i++)
        {
            var turnNoise = NoiseUtil.Sample2D(warpNoise, p.x * settings.headingNoiseFrequency, p.y * settings.headingNoiseFrequency, seed) * 2f - 1f;
            var turned = Rotate(heading, turnNoise * settings.headingTurnStrength + settings.passTwist * 0.25f);
            heading = Vector2.Lerp(heading, turned, 0.6f).normalized;
            p += heading * step;
            points.Add(p);

            if (Vector2.Distance(p, exit) < step * 1.5f) break;
        }

        points.Add(exit);
        points = Chaikin(points, 2);
        return new WorldSpline { id = "path_main", baseWidth = settings.passWidth, points = points };
    }

    private static List<float> BuildChokes(CanyonPassSettingsSO settings, WorldGenRng rng)
    {
        var chokes = new List<float>();
        for (var i = 0; i < Mathf.Max(0, settings.chokeCount); i++)
            chokes.Add(Mathf.Lerp(0.1f, 0.9f, rng.NextFloat01()));
        return chokes;
    }

    private static float CorridorWidth(CanyonPassSettingsSO settings, float t, List<float> chokeCenters)
    {
        var width = Mathf.Max(2f, settings.passWidth);
        foreach (var center in chokeCenters)
        {
            var d = Mathf.Abs(t - center);
            var influence = Mathf.Clamp01(1f - d / 0.14f);
            width *= 1f - influence * 0.45f;
        }

        return Mathf.Max(2f, width);
    }

    private static float PolylineProjectionT(List<Vector2> points, Vector2 p, out float minDistance)
    {
        minDistance = float.MaxValue;
        var bestAlong = 0f;
        var totalLength = 0f;
        for (var i = 1; i < points.Count; i++) totalLength += Vector2.Distance(points[i - 1], points[i]);

        var traversed = 0f;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var ab = b - a;
            var len = ab.magnitude;
            if (len <= 1e-5f) continue;

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            var proj = a + ab * t;
            var d = Vector2.Distance(p, proj);
            if (d < minDistance)
            {
                minDistance = d;
                bestAlong = (traversed + len * t) / Mathf.Max(1e-5f, totalLength);
            }

            traversed += len;
        }

        return Mathf.Clamp01(bestAlong);
    }

    private static List<Vector2> Chaikin(List<Vector2> points, int iterations)
    {
        if (points == null || points.Count < 3) return points;
        var current = new List<Vector2>(points);

        for (var it = 0; it < iterations; it++)
        {
            var next = new List<Vector2> { current[0] };
            for (var i = 0; i < current.Count - 1; i++)
            {
                var a = current[i];
                var b = current[i + 1];
                next.Add(Vector2.Lerp(a, b, 0.25f));
                next.Add(Vector2.Lerp(a, b, 0.75f));
            }
            next.Add(current[current.Count - 1]);
            current = next;
        }

        return current;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var cs = Mathf.Cos(radians);
        var sn = Mathf.Sin(radians);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}
