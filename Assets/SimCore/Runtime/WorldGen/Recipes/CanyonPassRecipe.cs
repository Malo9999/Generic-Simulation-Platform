using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class CanyonPassRecipe : WorldRecipeBase<CanyonPassSettingsSO>
{
    public override string RecipeId => "CanyonPass";
    public override int Version => 4;

    private const int MaxControlPoints = 64;
    private const int MinControlPoints = 32;
    private const int MaxPathIterations = 512;
    private const int MaxPreviewScatter = 256;
    private const float Epsilon = 1e-5f;

    protected override WorldMap GenerateTyped(CanyonPassSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);

        noise.Register(settings.HeightNoise, seed);
        noise.Register(settings.WarpNoise, seed);
        var heightNoise = noise.Get(settings.HeightNoise.id);
        var warpNoise = noise.Get(settings.WarpNoise.id);

        var mapRect = new Rect(grid.originWorld.x, grid.originWorld.y, grid.width * grid.cellSize, grid.height * grid.cellSize);

        var pathTimer = Stopwatch.StartNew();
        var passPoints = BuildPassCenterline(settings, grid, mapRect, seed, warpNoise, log);
        var spline = new WorldSpline { id = "pass_main", baseWidth = Mathf.Max(0.75f, settings.passWidth), points = passPoints };
        map.splines = SplineClipper.ClipToRectParts(spline, mapRect);
        pathTimer.Stop();

        var maskTimer = Stopwatch.StartNew();
        var height = new ScalarField("height", grid);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "pass_floor", "wall", "upland" } };

        var floorHalf = Mathf.Max(0.6f, settings.passWidth * 0.5f);
        var wallOuter = Mathf.Max(floorHalf + grid.cellSize, floorHalf + settings.passWidth * 0.8f);

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolyline(passPoints, p);

            var slope = 0.35f + SlopeSample(grid, x, y) * 0.65f;
            var low = (NoiseUtil.Sample2D(heightNoise, p.x * 0.28f, p.y * 0.28f, seed) * 2f - 1f) * settings.noiseStrength;
            var wallNoise = (NoiseUtil.Sample2D(heightNoise, p.x * 1.1f + 13f, p.y * 1.1f - 7f, seed + 23) * 2f - 1f) * settings.wallRoughness * 0.2f;

            var floorCarve = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, floorHalf));
            var wallBand = Mathf.Clamp01(1f - Mathf.Abs(dist - floorHalf) / Mathf.Max(0.01f, wallOuter - floorHalf));

            var h = slope + low - floorCarve * settings.canyonDepth + wallBand * settings.wallSteepness * 0.12f + wallNoise;
            height[x, y] = h;

            var inFloor = dist <= floorHalf;
            var inWall = !inFloor && dist <= wallOuter * (1f + wallNoise);
            walkable[x, y] = (byte)(inFloor ? 1 : 0);
            zones[x, y] = (byte)(inFloor ? 0 : inWall ? 1 : 2);
        }

        height.Normalize01InPlace();

        map.scalars["height"] = height;
        map.masks["walkable"] = walkable;
        map.masks["zones"] = zones;

        map.zones["pass_floor"] = new ZoneDef { zoneId = 0, name = "pass_floor" };
        map.zones["wall"] = new ZoneDef { zoneId = 1, name = "wall" };
        map.zones["upland"] = new ZoneDef { zoneId = 2, name = "upland" };
        maskTimer.Stop();

        var scatterTimer = Stopwatch.StartNew();
        var boulders = new ScatterSet { id = "boulders" };
        var cap = settings.qualityMode == QualityMode.FastPreview ? MaxPreviewScatter : int.MaxValue;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            if (boulders.points.Count >= cap) break;
            if (zones[x, y] != 1) continue;

            var p = grid.CellCenterWorld(x, y);
            var chance = settings.boulderDensity * 0.7f;
            if (rng.NextFloat01() < chance)
                boulders.points.Add(new ScatterPoint { pos = p, scale = 0.9f + rng.NextFloat01() * 1.4f, typeId = 0, tags = new[] { "boulder" } });
        }

        map.scatters["boulders"] = boulders;
        scatterTimer.Stop();

        map.EnsureRequiredOutputs();
        log.Log($"CanyonPass stages: path={pathTimer.ElapsedMilliseconds} ms | masks={maskTimer.ElapsedMilliseconds} ms | scatter={scatterTimer.ElapsedMilliseconds} ms");
        log.Log($"Generated CanyonPass ({settings.qualityMode}): path={passPoints.Count} boulders={boulders.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static List<Vector2> BuildPassCenterline(CanyonPassSettingsSO settings, WorldGridSpec grid, Rect mapRect, int seed, NoiseDescriptor warpNoise, IWorldGenLogger log)
    {
        var start = new Vector2(mapRect.xMin, Mathf.Lerp(mapRect.yMin, mapRect.yMax, Mathf.Abs(Mathf.Sin(seed * 0.021f))));
        var end = new Vector2(mapRect.xMax, Mathf.Lerp(mapRect.yMin, mapRect.yMax, Mathf.Abs(Mathf.Cos(seed * 0.017f))));

        var estimated = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(grid.width, grid.height) * 0.4f), MinControlPoints, MaxControlPoints);
        var count = Mathf.Min(estimated, MaxPathIterations);
        if (count <= 2)
        {
            log.Warn("CanyonPass baseline path cap reached too early. Falling back to straight path.");
            return new List<Vector2> { start, end };
        }

        var controls = new List<Vector2>(count) { start };
        var warpAmp = Mathf.Max(0f, settings.WarpAmplitude);
        var warpFreq = Mathf.Max(0.0001f, settings.WarpFrequency);

        for (var i = 1; i < count - 1; i++)
        {
            var t = i / (float)(count - 1);
            var basePoint = Vector2.Lerp(start, end, t);
            var twist = (NoiseUtil.Sample2D(warpNoise, t * settings.PathNoiseStrength * 19f, seed * 0.009f, seed + 13) * 2f - 1f) * settings.TwistAmplitude;
            var warpY = (NoiseUtil.Sample2D(warpNoise, basePoint.x * warpFreq, basePoint.y * warpFreq, seed + 41) * 2f - 1f) * warpAmp;
            var p = new Vector2(basePoint.x, basePoint.y + twist + warpY * 0.4f);
            controls.Add(ClampToRect(p, mapRect));
        }

        controls.Add(end);
        return Chaikin(controls, 1);
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

    private static float SlopeSample(WorldGridSpec grid, int x, int y)
    {
        var nx = grid.width <= 1 ? 0f : x / (float)(grid.width - 1);
        var ny = grid.height <= 1 ? 0f : y / (float)(grid.height - 1);
        return nx * 0.45f + (1f - ny) * 0.55f;
    }

    private static Vector2 ClampToRect(Vector2 p, Rect rect)
    {
        return new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
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
