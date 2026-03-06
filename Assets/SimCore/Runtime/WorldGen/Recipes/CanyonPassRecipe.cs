using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class CanyonPassRecipe : WorldRecipeBase<CanyonPassSettingsSO>
{
    public override string RecipeId => "CanyonPass";
    public override int Version => 5;

    private const int FastMinControlPoints = 22;
    private const int FastMaxControlPoints = 36;
    private const int SlowMinControlPoints = 30;
    private const int SlowMaxControlPoints = 56;
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
        int controlStationCount;
        var passPoints = BuildPassCenterline(settings, grid, mapRect, seed, warpNoise, out controlStationCount, log);
        var spline = new WorldSpline { id = "pass_main", baseWidth = Mathf.Max(0.75f, settings.passWidth), points = passPoints };
        map.splines = SplineClipper.ClipToRectParts(spline, mapRect);
        pathTimer.Stop();

        var maskTimer = Stopwatch.StartNew();
        var height = new ScalarField("height", grid);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "pass_floor", "wall", "upland" } };

        var floorHalfBase = Mathf.Max(0.6f, settings.passWidth * 0.5f);
        var wallOuterBase = Mathf.Max(floorHalfBase + grid.cellSize, floorHalfBase + settings.passWidth * 0.8f);
        var widthPhase = Mathf.Abs(Mathf.Sin(seed * 0.119f)) * Mathf.PI * 2f;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolylineWithT(passPoints, p, out var tAlong);

            var widthNoise = NoiseUtil.Sample2D(warpNoise, tAlong * 1.4f + 9f, seed * 0.0023f, seed + 73) * 2f - 1f;
            var widthScale = Mathf.Clamp(1f + Mathf.Sin(tAlong * Mathf.PI * 2f + widthPhase) * 0.09f + widthNoise * 0.07f, 0.82f, 1.18f);
            var floorHalf = floorHalfBase * widthScale;
            var wallOuter = wallOuterBase * Mathf.Lerp(0.95f, 1.08f, tAlong);

            var slope = 0.35f + SlopeSample(grid, x, y) * 0.65f;
            var low = (NoiseUtil.Sample2D(heightNoise, p.x * 0.22f, p.y * 0.22f, seed) * 2f - 1f) * settings.noiseStrength;
            var wallNoise = (NoiseUtil.Sample2D(heightNoise, p.x * 0.9f + 13f, p.y * 0.9f - 7f, seed + 23) * 2f - 1f) * settings.wallRoughness * 0.2f;

            var floorCarve = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, floorHalf));
            var wallBand = Mathf.Clamp01(1f - Mathf.Abs(dist - floorHalf) / Mathf.Max(0.01f, wallOuter - floorHalf));

            var h = slope + low - floorCarve * settings.canyonDepth + wallBand * settings.wallSteepness * 0.12f + wallNoise;
            height[x, y] = h;

            var inFloor = dist <= floorHalf;
            var inWall = !inFloor && dist <= wallOuter * (1f + wallNoise * 0.5f);
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

        var textureTimer = Stopwatch.StartNew();
        textureTimer.Stop();

        map.EnsureRequiredOutputs();
        log.Log($"CanyonPass stages: path={pathTimer.ElapsedMilliseconds} ms | masks={maskTimer.ElapsedMilliseconds} ms | scatter={scatterTimer.ElapsedMilliseconds} ms | texture={textureTimer.ElapsedMilliseconds} ms");
        log.Log($"CanyonPass path stats: controlStations={controlStationCount} splinePoints={passPoints.Count}");
        log.Log($"Generated CanyonPass ({settings.qualityMode}): boulders={boulders.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static List<Vector2> BuildPassCenterline(CanyonPassSettingsSO settings, WorldGridSpec grid, Rect mapRect, int seed, NoiseDescriptor warpNoise, out int controlStationCount, IWorldGenLogger log)
    {
        var flowDir = settings.gradientDir.sqrMagnitude > Epsilon ? settings.gradientDir.normalized : Vector2.right;
        var progressDir = DominantCardinal(flowDir);
        var lateralDir = new Vector2(-progressDir.y, progressDir.x);

        var start = EdgePoint(mapRect, progressDir, true, seed + 3);
        var end = EdgePoint(mapRect, progressDir, false, seed + 19);

        var maxDim = Mathf.Max(grid.width, grid.height);
        var estimated = Mathf.RoundToInt(maxDim * 0.2f);
        var count = settings.qualityMode == QualityMode.FastPreview
            ? Mathf.Clamp(estimated, FastMinControlPoints, FastMaxControlPoints)
            : Mathf.Clamp(estimated, SlowMinControlPoints, SlowMaxControlPoints);

        if (count <= 2)
        {
            controlStationCount = 2;
            log.Warn("CanyonPass path control station count too low. Falling back to straight path.");
            return new List<Vector2> { start, end };
        }

        controlStationCount = count;
        var controls = new List<Vector2>(count) { start };

        var freq = Mathf.Max(0.01f, settings.MeanderFrequency);
        var amp = Mathf.Max(grid.cellSize, settings.TwistAmplitude * grid.cellSize * 0.75f);
        var warpAmp = Mathf.Max(0f, settings.WarpAmplitude) * grid.cellSize * 0.35f;
        var warpFreq = Mathf.Max(0.0001f, settings.WarpFrequency);
        var phase = Mathf.Abs(Mathf.Sin(seed * 0.0241f)) * Mathf.PI * 2f;
        var inset = InsetRect(mapRect, grid.cellSize * 1.25f);

        for (var i = 1; i < count - 1; i++)
        {
            var t = i / (float)(count - 1);
            var basePoint = Vector2.Lerp(start, end, t);
            var sinOffset = Mathf.Sin(t * freq * Mathf.PI * 2f + phase) * amp;
            var lowNoise = (NoiseUtil.Sample2D(warpNoise, t * 1.7f + 5f, seed * 0.006f, seed + 13) * 2f - 1f) * amp * 0.2f;
            var warp = (NoiseUtil.Sample2D(warpNoise, basePoint.x * warpFreq, basePoint.y * warpFreq, seed + 41) * 2f - 1f) * warpAmp;
            var taper = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.9f);

            var p = basePoint + lateralDir * ((sinOffset + lowNoise + warp) * taper);
            controls.Add(ClampToRect(p, inset));
        }

        controls.Add(end);
        return Chaikin(controls, 2);
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

    private static float SlopeSample(WorldGridSpec grid, int x, int y)
    {
        var nx = grid.width <= 1 ? 0f : x / (float)(grid.width - 1);
        var ny = grid.height <= 1 ? 0f : y / (float)(grid.height - 1);
        return nx * 0.45f + (1f - ny) * 0.55f;
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
