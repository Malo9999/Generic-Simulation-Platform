using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 5;

    private const int MaxControlPoints = 64;
    private const int MinControlPoints = 32;
    private const int MaxPathIterations = 512;
    private const int MaxPreviewScatter = 256;
    private const float Epsilon = 1e-5f;

    protected override WorldMap GenerateTyped(SavannaRiverSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);

        noise.Register(settings.HeightNoise, seed);
        noise.Register(settings.WetnessNoise, seed);
        noise.Register(settings.WarpNoise, seed);
        var heightNoise = noise.Get(settings.HeightNoise.id);
        var wetnessNoise = noise.Get(settings.WetnessNoise.id);
        var warpNoise = noise.Get(settings.WarpNoise.id);

        var mapRect = new Rect(grid.originWorld.x, grid.originWorld.y, grid.width * grid.cellSize, grid.height * grid.cellSize);
        var flowDir = settings.gradientDir.sqrMagnitude > Epsilon ? settings.gradientDir.normalized : Vector2.down;

        var pathTimer = Stopwatch.StartNew();
        var riverPoints = BuildRiverCenterline(settings, grid, mapRect, flowDir, seed, wetnessNoise, warpNoise, log);
        var spline = new WorldSpline { id = "river_main", baseWidth = Mathf.Max(0.5f, settings.riverWidth), points = riverPoints };
        map.splines = SplineClipper.ClipToRectParts(spline, mapRect);
        pathTimer.Stop();

        var maskTimer = Stopwatch.StartNew();
        var height = new ScalarField("height", grid);
        var wetness = new ScalarField("wetness", grid);
        var water = new MaskField("water", grid, MaskEncoding.Boolean);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "river_corridor", "upland" } };
        var biomes = new MaskField("biomes", grid, MaskEncoding.Categorical) { categories = new[] { "upland", "floodplain", "water" } };

        var riverHalf = Mathf.Max(0.5f, settings.riverWidth * 0.5f);
        var floodRadius = Mathf.Max(riverHalf + 0.5f, settings.floodplainWidth);
        var bankRadius = Mathf.Max(riverHalf + 0.25f, settings.bankWidth + riverHalf);

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolyline(riverPoints, p);

            var slope = SlopeSample(grid, x, y, flowDir);
            var terrainNoise = (NoiseUtil.Sample2D(heightNoise, p.x * 0.35f, p.y * 0.35f, seed) * 2f - 1f) * settings.heightNoiseStrength * 0.28f;
            var floodNoise = (NoiseUtil.Sample2D(wetnessNoise, p.x * 0.11f, p.y * 0.11f, seed + 17) * 2f - 1f) * 0.2f;

            var floodFactor = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, floodRadius * (1f + floodNoise)));
            var bankFactor = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, bankRadius));
            var isWater = dist <= riverHalf;

            var h = slope + terrainNoise - floodFactor * settings.carveStrength - (isWater ? 0.05f : 0f);
            h = Mathf.Lerp(h, settings.waterLevel, 0.2f * floodFactor);
            height[x, y] = h;
            wetness[x, y] = Mathf.Clamp01(floodFactor * 0.75f + (0.5f - slope) * 0.25f + 0.1f);
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);
            zones[x, y] = (byte)(bankFactor > 0.05f ? 0 : 1);

            biomes[x, y] = isWater ? (byte)2 : (floodFactor > 0.22f ? (byte)1 : (byte)0);
        }

        height.Normalize01InPlace();
        wetness.Normalize01InPlace();

        map.scalars["height"] = height;
        map.scalars["wetness"] = wetness;
        map.masks["water"] = water;
        map.masks["walkable"] = walkable;
        map.masks["zones"] = zones;
        map.masks["biomes"] = biomes;

        map.zones["river_corridor"] = new ZoneDef { zoneId = 0, name = "river_corridor" };
        map.zones["upland"] = new ZoneDef { zoneId = 1, name = "upland" };
        maskTimer.Stop();

        var scatterTimer = Stopwatch.StartNew();
        var trees = new ScatterSet { id = "trees" };
        var rocks = new ScatterSet { id = "rocks" };
        var previewCap = settings.qualityMode == QualityMode.FastPreview ? MaxPreviewScatter : int.MaxValue;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolyline(riverPoints, p);
            if (water[x, y] > 0) continue;

            if (dist <= floodRadius && trees.points.Count < previewCap)
            {
                var chance = settings.treeDensity * Mathf.Clamp01(1f - dist / floodRadius);
                if (rng.NextFloat01() < chance)
                    trees.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 0.8f, typeId = 0, tags = new[] { "tree" } });
            }

            if (dist >= bankRadius && rocks.points.Count < previewCap)
            {
                var chance = settings.rockDensity * Mathf.Clamp01((dist - bankRadius) / Mathf.Max(0.01f, floodRadius));
                if (rng.NextFloat01() < chance)
                    rocks.points.Add(new ScatterPoint { pos = p, scale = 0.7f + rng.NextFloat01(), typeId = 0, tags = new[] { "rock" } });
            }
        }

        map.scatters["trees"] = trees;
        map.scatters["rocks"] = rocks;
        scatterTimer.Stop();

        map.EnsureRequiredOutputs();
        log.Log($"SavannaRiver stages: path={pathTimer.ElapsedMilliseconds} ms | masks={maskTimer.ElapsedMilliseconds} ms | scatter={scatterTimer.ElapsedMilliseconds} ms");
        log.Log($"Generated SavannaRiver ({settings.qualityMode}): path={riverPoints.Count} trees={trees.points.Count} rocks={rocks.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static List<Vector2> BuildRiverCenterline(SavannaRiverSettingsSO settings, WorldGridSpec grid, Rect mapRect, Vector2 flowDir, int seed, NoiseDescriptor meanderNoise, NoiseDescriptor warpNoise, IWorldGenLogger log)
    {
        var progressDir = DominantCardinal(flowDir);
        var lateralDir = new Vector2(-progressDir.y, progressDir.x);

        var start = EdgePoint(mapRect, progressDir, true, seed + 5);
        var end = EdgePoint(mapRect, progressDir, false, seed + 11);

        var estimated = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(grid.width, grid.height) * 0.4f), MinControlPoints, MaxControlPoints);
        var count = Mathf.Min(estimated, MaxPathIterations);
        if (count <= 2)
        {
            log.Warn("SavannaRiver baseline path cap reached too early. Falling back to straight path.");
            return new List<Vector2> { start, end };
        }

        var controls = new List<Vector2>(count);
        controls.Add(start);
        var warpAmp = Mathf.Max(0f, settings.RiverWarpAmplitude);
        var warpFreq = Mathf.Max(0.0001f, settings.RiverWarpFrequency);

        for (var i = 1; i < count - 1; i++)
        {
            var t = i / (float)(count - 1);
            var basePoint = Vector2.Lerp(start, end, t);
            var meander = (NoiseUtil.Sample2D(meanderNoise, t * settings.MeanderFreq * 17f, seed * 0.013f, seed + 31) * 2f - 1f) * settings.MeanderAmp;
            var wx = (NoiseUtil.Sample2D(warpNoise, basePoint.x * warpFreq, basePoint.y * warpFreq, seed + 47) * 2f - 1f) * warpAmp;
            var wy = (NoiseUtil.Sample2D(warpNoise, (basePoint.x + 97f) * warpFreq, (basePoint.y - 53f) * warpFreq, seed + 79) * 2f - 1f) * warpAmp;

            var p = basePoint + lateralDir * meander + new Vector2(wx, wy) * 0.35f;
            controls.Add(ClampToRect(p, mapRect));
        }

        controls.Add(end);
        return Chaikin(controls, 1);
    }

    private static Vector2 DominantCardinal(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private static Vector2 EdgePoint(Rect rect, Vector2 direction, bool isStart, int seed)
    {
        var u = Mathf.Abs(Mathf.Sin(seed * 0.017f));
        if (Mathf.Abs(direction.x) > 0.5f)
        {
            var x = (isStart ? direction.x > 0f : direction.x < 0f) ? rect.xMin : rect.xMax;
            return new Vector2(x, Mathf.Lerp(rect.yMin, rect.yMax, u));
        }

        var y = (isStart ? direction.y > 0f : direction.y < 0f) ? rect.yMin : rect.yMax;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), y);
    }

    private static Vector2 ClampToRect(Vector2 p, Rect rect)
    {
        return new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
    }

    private static float DistanceToPolyline(List<Vector2> points, Vector2 p)
    {
        if (points == null || points.Count < 2) return float.MaxValue;
        var best = float.MaxValue;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < Epsilon) continue;

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            var proj = a + ab * t;
            var d = Vector2.Distance(p, proj);
            if (d < best) best = d;
        }

        return best;
    }

    private static float SlopeSample(WorldGridSpec grid, int x, int y, Vector2 flowDir)
    {
        var nx = grid.width <= 1 ? 0f : x / (float)(grid.width - 1);
        var ny = grid.height <= 1 ? 0f : y / (float)(grid.height - 1);
        var local = new Vector2(nx * 2f - 1f, ny * 2f - 1f);
        return Vector2.Dot(local, flowDir) * 0.5f + 0.5f;
    }

    private static List<Vector2> Chaikin(List<Vector2> points, int iterations)
    {
        if (points == null || points.Count < 3 || iterations <= 0) return points;
        var current = new List<Vector2>(points);

        for (var iter = 0; iter < iterations; iter++)
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
}
