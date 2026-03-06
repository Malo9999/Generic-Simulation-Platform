using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 7;

    private const int FastMinControlPoints = 24;
    private const int FastMaxControlPoints = 40;
    private const int SlowMinControlPoints = 32;
    private const int SlowMaxControlPoints = 64;
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
        int controlStationCount;
        var riverPoints = BuildRiverCenterline(settings, grid, mapRect, flowDir, seed, wetnessNoise, warpNoise, out controlStationCount, log);
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

        var riverHalfBase = Mathf.Max(0.5f, settings.riverWidth * 0.5f);
        var floodRadiusBase = Mathf.Max(riverHalfBase + 0.75f, settings.floodplainWidth);
        var bankRadiusBase = Mathf.Max(riverHalfBase + 0.25f, settings.bankWidth + riverHalfBase);
        var widthPhase = Mathf.Abs(Mathf.Sin(seed * 0.173f)) * Mathf.PI * 2f;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolylineWithT(riverPoints, p, out var tAlong);

            var widthNoise = NoiseUtil.Sample2D(warpNoise, tAlong * 0.75f + 11f, seed * 0.0027f, seed + 101) * 2f - 1f;
            var widthWave = Mathf.Sin(tAlong * Mathf.PI * 1.6f + widthPhase) * 0.08f;
            var widthScale = Mathf.Clamp(1f + widthWave + widthNoise * 0.06f, 0.88f, 1.16f);

            var riverHalf = riverHalfBase * widthScale;
            var floodRadius = floodRadiusBase * (1f + widthWave * 0.15f + widthNoise * 0.04f);
            var bankRadius = bankRadiusBase * Mathf.Lerp(0.96f, 1.04f, 1f - tAlong);

            var slope = SlopeSample(grid, x, y, flowDir);
            var terrainNoise = (NoiseUtil.Sample2D(heightNoise, p.x * 0.21f, p.y * 0.21f, seed) * 2f - 1f) * settings.heightNoiseStrength * 0.25f;
            var floodNoise = (NoiseUtil.Sample2D(wetnessNoise, p.x * 0.07f, p.y * 0.07f, seed + 17) * 2f - 1f) * 0.17f;

            var floodEdge = Mathf.Max(0.01f, floodRadius * (1f + floodNoise * 0.4f));
            var floodFactor = Mathf.Clamp01(1f - dist / floodEdge);
            var bankFactor = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, bankRadius));
            var isWater = dist <= riverHalf;

            var h = slope + terrainNoise - floodFactor * settings.carveStrength - (isWater ? 0.06f : 0f);
            h = Mathf.Lerp(h, settings.waterLevel, 0.22f * floodFactor);
            height[x, y] = h;
            wetness[x, y] = Mathf.Clamp01(floodFactor * 0.8f + (0.5f - slope) * 0.2f + 0.1f + floodNoise * 0.08f);
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);
            zones[x, y] = (byte)(bankFactor > 0.05f ? 0 : 1);
            biomes[x, y] = isWater ? (byte)2 : (floodFactor > 0.2f ? (byte)1 : (byte)0);
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
            var dist = DistanceToPolylineWithT(riverPoints, p, out _);
            if (water[x, y] > 0) continue;

            if (dist <= floodRadiusBase && trees.points.Count < previewCap)
            {
                var chance = settings.treeDensity * Mathf.Clamp01(1f - dist / floodRadiusBase);
                if (rng.NextFloat01() < chance)
                    trees.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 0.8f, typeId = 0, tags = new[] { "tree" } });
            }

            if (dist >= bankRadiusBase && rocks.points.Count < previewCap)
            {
                var chance = settings.rockDensity * Mathf.Clamp01((dist - bankRadiusBase) / Mathf.Max(0.01f, floodRadiusBase));
                if (rng.NextFloat01() < chance)
                    rocks.points.Add(new ScatterPoint { pos = p, scale = 0.7f + rng.NextFloat01(), typeId = 0, tags = new[] { "rock" } });
            }
        }

        map.scatters["trees"] = trees;
        map.scatters["rocks"] = rocks;
        scatterTimer.Stop();

        var textureTimer = Stopwatch.StartNew();
        textureTimer.Stop();

        map.EnsureRequiredOutputs();
        log.Log($"SavannaRiver stages: path={pathTimer.ElapsedMilliseconds} ms | masks={maskTimer.ElapsedMilliseconds} ms | scatter={scatterTimer.ElapsedMilliseconds} ms | texture={textureTimer.ElapsedMilliseconds} ms");
        log.Log($"SavannaRiver path stats: controlStations={controlStationCount} splinePoints={riverPoints.Count}");
        log.Log($"Generated SavannaRiver ({settings.qualityMode}): trees={trees.points.Count} rocks={rocks.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static List<Vector2> BuildRiverCenterline(SavannaRiverSettingsSO settings, WorldGridSpec grid, Rect mapRect, Vector2 flowDir, int seed, NoiseDescriptor meanderNoise, NoiseDescriptor warpNoise, out int controlStationCount, IWorldGenLogger log)
    {
        var progressDir = DominantCardinal(flowDir);
        var lateralDir = new Vector2(-progressDir.y, progressDir.x);

        var start = EdgePoint(mapRect, progressDir, true, seed + 5);
        var end = EdgePoint(mapRect, progressDir, false, seed + 11);

        var maxDim = Mathf.Max(grid.width, grid.height);
        var estimated = Mathf.RoundToInt(maxDim * 0.22f);
        var count = settings.qualityMode == QualityMode.FastPreview
            ? Mathf.Clamp(estimated, FastMinControlPoints, FastMaxControlPoints)
            : Mathf.Clamp(estimated, SlowMinControlPoints, SlowMaxControlPoints);

        if (count <= 2)
        {
            controlStationCount = 2;
            log.Warn("SavannaRiver path control station count too low. Falling back to straight path.");
            return new List<Vector2> { start, end };
        }

        controlStationCount = count;
        var controls = new List<Vector2>(count) { start };

        var meanderFreq = Mathf.Max(0.2f, settings.MeanderFreq);
        var trendLen = Vector2.Distance(start, end);
        var ampLimit = Mathf.Max(grid.cellSize * 2f, trendLen * 0.28f);
        var meanderAmp = Mathf.Clamp(settings.MeanderAmp * trendLen * 0.18f, grid.cellSize * 2f, ampLimit);
        var warpAmp = Mathf.Max(0f, settings.RiverWarpAmplitude) * meanderAmp * 0.35f;
        var warpFreq = Mathf.Max(0.0001f, settings.RiverWarpFrequency);
        var phase = Mathf.Abs(Mathf.Sin(seed * 0.0217f)) * Mathf.PI * 2f;
        var inset = InsetRect(mapRect, grid.cellSize * 1.5f);

        for (var i = 1; i < count - 1; i++)
        {
            var t = i / (float)(count - 1);
            var basePoint = Vector2.Lerp(start, end, t);

            var sineOffset = Mathf.Sin(t * meanderFreq * Mathf.PI * 2f + phase) * meanderAmp;
            var lowFreqNoise = (NoiseUtil.Sample2D(meanderNoise, t * 1.1f + 3f, seed * 0.0043f, seed + 31) * 2f - 1f) * meanderAmp * 0.22f;
            var warpNoiseSample = (NoiseUtil.Sample2D(warpNoise, basePoint.x * warpFreq, basePoint.y * warpFreq, seed + 47) * 2f - 1f) * warpAmp;
            var taper = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI));

            var offset = (sineOffset + lowFreqNoise + warpNoiseSample) * taper;
            var p = basePoint + lateralDir * offset;
            controls.Add(ClampToRect(p, inset));
        }

        controls.Add(end);
        return Chaikin(controls, 2);
    }

    private static Vector2 DominantCardinal(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private static Vector2 EdgePoint(Rect rect, Vector2 direction, bool isStart, int seed)
    {
        var u = Mathf.Lerp(0.2f, 0.8f, Mathf.Abs(Mathf.Sin(seed * 0.017f)));
        if (Mathf.Abs(direction.x) > 0.5f)
        {
            var x = (isStart ? direction.x > 0f : direction.x < 0f) ? rect.xMin : rect.xMax;
            return new Vector2(x, Mathf.Lerp(rect.yMin, rect.yMax, u));
        }

        var y = (isStart ? direction.y > 0f : direction.y < 0f) ? rect.yMin : rect.yMax;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), y);
    }

    private static Rect InsetRect(Rect rect, float inset)
    {
        var minX = rect.xMin + inset;
        var maxX = rect.xMax - inset;
        var minY = rect.yMin + inset;
        var maxY = rect.yMax - inset;
        if (maxX <= minX || maxY <= minY) return rect;
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static Vector2 ClampToRect(Vector2 p, Rect rect)
    {
        return new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
    }

    private static float DistanceToPolylineWithT(List<Vector2> points, Vector2 p, out float tAlong)
    {
        tAlong = 0f;
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
            if (d < best)
            {
                best = d;
                tAlong = ((i - 1) + t) / Mathf.Max(1f, points.Count - 1f);
            }
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
