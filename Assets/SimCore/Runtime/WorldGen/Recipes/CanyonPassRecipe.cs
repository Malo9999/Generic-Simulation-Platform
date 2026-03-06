using System.Collections.Generic;
using UnityEngine;

public class CanyonPassRecipe : WorldRecipeBase<CanyonPassSettingsSO>
{
    public override string RecipeId => "CanyonPass";
    public override int Version => 3;

    private const float Epsilon = 1e-5f;

    protected override WorldMap GenerateTyped(CanyonPassSettingsSO settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log)
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
        var baseTerrain = BuildBaseTerrain(grid, settings, seed, heightNoise, warpNoise, mapRect);

        var start = new Vector2Int(Mathf.FloorToInt(grid.width * 0.1f + rng.NextFloat01() * grid.width * 0.25f), grid.height - 1);
        var sink = new Vector2Int(Mathf.FloorToInt(grid.width * 0.65f + rng.NextFloat01() * grid.width * 0.25f), 0);
        var mainPath = TracePassPath(grid, baseTerrain, start, sink, settings.FlowInertia, settings.PathNoiseStrength, seed, warpNoise);

        var passPoints = Chaikin(PathToWorldPoints(grid, mainPath), 2);
        map.splines.AddRange(SplineClipper.ClipToRectParts(new WorldSpline { id = "pass_main", baseWidth = settings.passWidth, points = passPoints }, mapRect));

        var sideGullies = BuildSideGullies(settings, grid, baseTerrain, mainPath, rng, seed, wetnessNoise);
        foreach (var gully in sideGullies)
            map.splines.AddRange(SplineClipper.ClipToRectParts(gully, mapRect));

        var chokeCenters = BuildFeatureCenters(Mathf.Clamp(settings.chokeCount, 1, 3), rng, 0.15f, 0.85f);
        var basinCenters = BuildFeatureCenters(Mathf.Clamp(settings.BasinCount, 0, 2), rng, 0.1f, 0.9f);

        var height = new ScalarField("height", grid);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "pass_floor", "choke", "basin", "wall_left", "wall_right", "upland" } };
        var boulders = new ScatterSet { id = "boulders" };

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var along = PolylineProjectionT(passPoints, p, out var distanceToPass, out var closestPoint, out var tangent);

            var width = PassWidth(settings, along, p, seed, warpNoise, chokeCenters, basinCenters);
            var floorHalf = width * 0.5f;
            var basinBoost = FeatureBand(along, basinCenters, 0.12f);
            var chokeBand = FeatureBand(along, chokeCenters, 0.08f);

            var asym = NoiseUtil.Sample2D(warpNoise, p.x * 0.05f, p.y * 0.05f, seed) * 2f - 1f;
            var rough = NoiseUtil.Sample2D(heightNoise, p.x * 1.7f + 12f, p.y * 1.7f - 6f, seed + 33) * 2f - 1f;
            var erosion = NoiseUtil.Sample2D(wetnessNoise, p.x * 0.11f, p.y * 0.11f, seed + 71) * 2f - 1f;

            var sideSign = Mathf.Sign(Vector2.Dot(p - closestPoint, new Vector2(-tangent.y, tangent.x)));
            if (Mathf.Approximately(sideSign, 0f)) sideSign = 1f;

            var wallDistance = Mathf.Max(0f, distanceToPass - floorHalf);
            var wallFactor = Mathf.Clamp01(wallDistance / Mathf.Max(grid.cellSize, width));
            var asymWall = sideSign > 0f ? asym * settings.asymmetryStrength : -asym * settings.asymmetryStrength;
            var wallNoise = rough * settings.wallRoughness + erosion * settings.erosionStrength;

            var floorCarve = Mathf.Clamp01(1f - distanceToPass / Mathf.Max(0.01f, floorHalf + basinBoost * floorHalf * 0.4f));
            var h = baseTerrain[x, y]
                    - floorCarve * settings.canyonDepth
                    + wallFactor * settings.wallSteepness * (0.6f + wallNoise + asymWall)
                    + chokeBand * 0.1f;
            height[x, y] = h;

            var inPass = distanceToPass <= floorHalf;
            walkable[x, y] = (byte)(inPass ? 1 : 0);

            byte zone;
            if (inPass && chokeBand > 0.4f) zone = 1;
            else if (inPass && basinBoost > 0.45f) zone = 2;
            else if (inPass) zone = 0;
            else if (distanceToPass <= width * (1.1f + settings.wallRoughness * 0.2f)) zone = (byte)(sideSign > 0f ? 3 : 4);
            else zone = 5;
            zones[x, y] = zone;

            var slopeProxy = Mathf.Abs(wallFactor * (settings.wallSteepness + wallNoise));
            var basinEdge = Mathf.Clamp01(1f - Mathf.Abs(distanceToPass - floorHalf) / Mathf.Max(grid.cellSize * 2f, width * 0.2f));
            var boulderWeight = Mathf.Clamp01(slopeProxy * 0.6f + basinEdge * basinBoost * 0.55f);
            if (!inPass && rng.NextFloat01() < settings.boulderDensity * boulderWeight)
                boulders.points.Add(new ScatterPoint { pos = p, scale = 0.9f + rng.NextFloat01() * 1.4f, typeId = 0, tags = new[] { "boulder" } });
        }

        height.Normalize01InPlace();
        ApplyContrastGamma(height, settings.HeightContrast, settings.HeightGamma);
        map.scalars["height"] = height;
        map.masks["walkable"] = walkable;
        map.masks["zones"] = zones;
        map.scatters["boulders"] = boulders;

        map.zones["pass_floor"] = new ZoneDef { zoneId = 0, name = "pass_floor" };
        map.zones["choke"] = new ZoneDef { zoneId = 1, name = "choke" };
        map.zones["basin"] = new ZoneDef { zoneId = 2, name = "basin" };
        map.zones["wall_left"] = new ZoneDef { zoneId = 3, name = "wall_left" };
        map.zones["wall_right"] = new ZoneDef { zoneId = 4, name = "wall_right" };
        map.zones["upland"] = new ZoneDef { zoneId = 5, name = "upland" };

        map.EnsureRequiredOutputs();
        log.Log($"Generated CanyonPass terrain-first: boulders={boulders.points.Count} splines={map.splines.Count} chokes={chokeCenters.Count} basins={basinCenters.Count}");
        return map;
    }

    private static float[,] BuildBaseTerrain(WorldGridSpec grid, CanyonPassSettingsSO settings, int seed, NoiseDescriptor heightNoise, NoiseDescriptor warpNoise, Rect mapRect)
    {
        var terrain = new float[grid.width, grid.height];
        var slopeDir = new Vector2(0.2f, -1f).normalized;
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var wx = (NoiseUtil.Sample2D(warpNoise, p.x * settings.WarpFrequency, p.y * settings.WarpFrequency, seed) * 2f - 1f) * settings.WarpAmplitude;
            var wy = (NoiseUtil.Sample2D(warpNoise, (p.x + 91f) * settings.WarpFrequency, (p.y + 43f) * settings.WarpFrequency, seed) * 2f - 1f) * settings.WarpAmplitude;
            var wp = p + new Vector2(wx, wy);

            var u = (wp.x - mapRect.xMin) / Mathf.Max(0.001f, mapRect.width);
            var v = (wp.y - mapRect.yMin) / Mathf.Max(0.001f, mapRect.height);
            var slope = Vector2.Dot(new Vector2(u, v), slopeDir) * 0.5f + 0.5f;
            var low = NoiseUtil.Sample2D(heightNoise, wp.x * 0.4f, wp.y * 0.4f, seed) * 2f - 1f;
            var breakup = NoiseUtil.Sample2D(heightNoise, wp.x * 1.5f + 21f, wp.y * 1.5f - 13f, seed + 17) * 2f - 1f;
            terrain[x, y] = slope + low * settings.noiseStrength + breakup * (settings.noiseStrength * 0.75f);
        }

        NormalizeArray01(terrain, grid.width, grid.height);
        return terrain;
    }

    private static List<Vector2Int> TracePassPath(WorldGridSpec grid, float[,] terrain, Vector2Int start, Vector2Int sink, float inertia, float pathNoiseStrength, int seed, NoiseDescriptor warpNoise)
    {
        start.x = Mathf.Clamp(start.x, 0, grid.width - 1);
        sink.x = Mathf.Clamp(sink.x, 0, grid.width - 1);
        var current = start;
        var path = new List<Vector2Int> { current };
        var heading = ((Vector2)(sink - start)).normalized;
        var maxSteps = grid.width * grid.height;

        for (var i = 0; i < maxSteps; i++)
        {
            if (current == sink) break;
            Vector2Int best = current;
            var bestScore = float.MinValue;

            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                var nx = current.x + ox;
                var ny = current.y + oy;
                if (nx < 0 || ny < 0 || nx >= grid.width || ny >= grid.height) continue;

                var candidate = new Vector2Int(nx, ny);
                var dir = new Vector2(ox, oy).normalized;
                var continuity = Mathf.Clamp01((Vector2.Dot(dir, heading) + 1f) * 0.5f);
                var towardsSink = -Vector2.Distance(candidate, sink);
                var terrainCost = -terrain[nx, ny] * 1.1f;
                var wander = (NoiseUtil.Sample2D(warpNoise, nx * 0.1f, ny * 0.1f, seed + i) * 2f - 1f) * pathNoiseStrength;
                var score = terrainCost + continuity * inertia + towardsSink * 0.02f + wander;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == current) break;
            heading = ((Vector2)(best - current)).normalized;
            current = best;
            path.Add(current);
            if ((current - sink).sqrMagnitude <= 2) break;
        }

        if (path[path.Count - 1] != sink) path.Add(sink);
        return path;
    }

    private static List<WorldSpline> BuildSideGullies(CanyonPassSettingsSO settings, WorldGridSpec grid, float[,] terrain, List<Vector2Int> mainPath, WorldGenRng rng, int seed, NoiseDescriptor wetnessNoise)
    {
        var gullies = new List<WorldSpline>();
        var count = Mathf.Clamp(settings.SideGullyCount, 0, 2);
        if (count == 0 || mainPath.Count < 10) return gullies;

        for (var i = 0; i < count; i++)
        {
            var start = mainPath[Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(0.2f, 0.8f, rng.NextFloat01()) * (mainPath.Count - 1)), 2, mainPath.Count - 3)];
            var current = start;
            var points = new List<Vector2Int> { current };
            var last = Vector2Int.zero;

            for (var s = 0; s < 24; s++)
            {
                Vector2Int best = current;
                var bestScore = float.MinValue;
                for (var oy = -1; oy <= 1; oy++)
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    var nx = current.x + ox;
                    var ny = current.y + oy;
                    if (nx < 0 || ny < 0 || nx >= grid.width || ny >= grid.height) continue;
                    var c = new Vector2Int(nx, ny);
                    var grad = terrain[current.x, current.y] - terrain[nx, ny];
                    var distPenalty = Vector2.Distance(c, start) * 0.02f;
                    var noise = (NoiseUtil.Sample2D(wetnessNoise, nx * 0.13f, ny * 0.13f, seed + i * 9) * 2f - 1f) * 0.18f;
                    var backtrack = c == last ? -0.2f : 0f;
                    var score = grad * 1.25f - distPenalty + noise + backtrack;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }

                if (best == current) break;
                last = current;
                current = best;
                points.Add(current);
                if (current.x <= 1 || current.x >= grid.width - 2 || current.y <= 1 || current.y >= grid.height - 2) break;
            }

            if (points.Count < 4) continue;
            gullies.Add(new WorldSpline
            {
                id = $"gully_{i}",
                baseWidth = Mathf.Max(2f, settings.passWidth * 0.35f),
                points = Chaikin(PathToWorldPoints(grid, points), 1)
            });
        }

        return gullies;
    }

    private static List<float> BuildFeatureCenters(int count, WorldGenRng rng, float min, float max)
    {
        var centers = new List<float>();
        for (var i = 0; i < count; i++)
            centers.Add(Mathf.Lerp(min, max, rng.NextFloat01()));
        return centers;
    }

    private static float FeatureBand(float t, List<float> centers, float radius)
    {
        var f = 0f;
        for (var i = 0; i < centers.Count; i++)
        {
            var d = Mathf.Abs(t - centers[i]);
            f = Mathf.Max(f, Mathf.Clamp01(1f - d / Mathf.Max(0.001f, radius)));
        }

        return f;
    }

    private static float PassWidth(CanyonPassSettingsSO settings, float t, Vector2 p, int seed, NoiseDescriptor warpNoise, List<float> chokeCenters, List<float> basinCenters)
    {
        var width = Mathf.Max(2f, settings.passWidth);
        var noise = NoiseUtil.Sample2D(warpNoise, p.x * 0.07f, p.y * 0.07f, seed + 11) * 2f - 1f;
        width *= 1f + noise * settings.WidthVariation * 0.5f;

        width *= 1f - FeatureBand(t, chokeCenters, 0.08f) * 0.42f;
        width *= 1f + FeatureBand(t, basinCenters, 0.13f) * 0.58f;
        return Mathf.Max(2f, width);
    }

    private static List<Vector2> PathToWorldPoints(WorldGridSpec grid, List<Vector2Int> cells)
    {
        var points = new List<Vector2>(cells.Count);
        for (var i = 0; i < cells.Count; i++) points.Add(grid.CellCenterWorld(cells[i].x, cells[i].y));
        return points;
    }

    private static float PolylineProjectionT(List<Vector2> points, Vector2 p, out float minDistance, out Vector2 closestPoint, out Vector2 tangent)
    {
        minDistance = float.MaxValue;
        closestPoint = p;
        tangent = Vector2.down;
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
            if (len <= Epsilon) continue;

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            var proj = a + ab * t;
            var d = Vector2.Distance(p, proj);
            if (d < minDistance)
            {
                minDistance = d;
                bestAlong = (traversed + len * t) / Mathf.Max(Epsilon, totalLength);
                closestPoint = proj;
                tangent = ab.normalized;
            }

            traversed += len;
        }

        return Mathf.Clamp01(bestAlong);
    }

    private static void NormalizeArray01(float[,] values, int width, int height)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var v = values[x, y];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        var range = Mathf.Max(Epsilon, max - min);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            values[x, y] = Mathf.Clamp01((values[x, y] - min) / range);
    }

    private static void ApplyContrastGamma(ScalarField field, float contrast, float gamma)
    {
        var c = Mathf.Max(0f, contrast);
        var g = Mathf.Max(0.01f, gamma);
        for (var y = 0; y < field.grid.height; y++)
        for (var x = 0; x < field.grid.width; x++)
        {
            var v = field[x, y];
            v = Mathf.Clamp01((v - 0.5f) * c + 0.5f);
            field[x, y] = Mathf.Pow(v, g);
        }
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
}
