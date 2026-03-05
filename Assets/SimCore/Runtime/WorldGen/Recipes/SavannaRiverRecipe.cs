using System.Collections.Generic;
using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 3;

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

        var mapRect = new Rect(
            grid.originWorld.x,
            grid.originWorld.y,
            grid.width * grid.cellSize,
            grid.height * grid.cellSize);

        var mainRiver = BuildMainRiverSpline(settings, grid, rng, seed, warpNoise, mapRect);
        ApplyCutoffs(mainRiver, settings, rng);

        var splines = new List<WorldSpline> { mainRiver };
        BuildSideChannels(splines, mainRiver, settings, rng, seed, warpNoise, mapRect);

        var clippedSplines = new List<WorldSpline>();
        foreach (var spline in splines)
            clippedSplines.AddRange(SplineClipper.ClipToRectParts(spline, mapRect));
        map.splines = clippedSplines;

        var height = new ScalarField("height", grid);
        var wetness = new ScalarField("wetness", grid);
        var water = new MaskField("water", grid, MaskEncoding.Boolean);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var biomes = new MaskField("biomes", grid, MaskEncoding.Categorical) { categories = new[] { "savanna", "wetland", "rocky", "water" } };
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "river", "north_plain", "south_plain" } };

        var riverHalfWidth = Mathf.Max(0.01f, settings.riverWidth * 0.5f);
        var bankLimit = riverHalfWidth + Mathf.Max(0.01f, settings.bankWidth);
        var floodplainWidth = Mathf.Max(bankLimit + 0.01f, settings.floodplainWidth);
        var gradientDir = settings.gradientDir.sqrMagnitude < 0.0001f ? Vector2.down : settings.gradientDir.normalized;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var nearest = FindNearestRiverInfo(map.splines, settings, p);

            var heightN = NoiseUtil.Sample2D(heightNoise, p.x, p.y, seed);
            var wetnessN = NoiseUtil.Sample2D(wetnessNoise, p.x, p.y, seed);
            var normalizedWetNoise = wetnessN * 2f - 1f;

            var gradCoord = new Vector2(
                (p.x - mapRect.xMin) / Mathf.Max(0.001f, mapRect.width),
                (p.y - mapRect.yMin) / Mathf.Max(0.001f, mapRect.height));
            var slope = Vector2.Dot(gradCoord, gradientDir) * 0.5f + 0.5f;

            var bankWidth = bankLimit * (0.85f + settings.BankNoiseStrength * normalizedWetNoise);
            var floodWidth = floodplainWidth * (0.8f + settings.FloodplainNoiseStrength * normalizedWetNoise);
            var carvedFloodplain = Mathf.Clamp01(1f - nearest.distance / Mathf.Max(0.01f, floodWidth));

            var h = slope + heightN * 0.4f - carvedFloodplain * settings.carveStrength;
            height[x, y] = h;
            wetness[x, y] = Mathf.Clamp01(carvedFloodplain * (0.5f + 0.5f * wetnessN));

            var isWater = nearest.distance < nearest.halfWidth;
            var isBank = nearest.distance < bankWidth;
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);

            byte biome = 0;
            if (isWater) biome = 3;
            else if (isBank || wetness[x, y] > 0.55f) biome = 1;
            else if (h > settings.waterLevel + 0.26f) biome = 2;
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
            if (water[x, y] > 0) continue;

            var p = grid.CellCenterWorld(x, y);
            var nearest = FindNearestRiverInfo(map.splines, settings, p);
            var dist = nearest.distance;
            var wet = wetness[x, y];
            var elev = height[x, y];

            var treeDistanceWeight = 1f - Mathf.SmoothStep(settings.riverWidth, floodplainWidth, dist);
            var rockDistanceWeight = Mathf.SmoothStep(settings.riverWidth + settings.bankWidth, floodplainWidth * 1.3f, dist);
            var treeWeight = Mathf.Clamp01(treeDistanceWeight * wet);
            var rockWeight = Mathf.Clamp01(rockDistanceWeight * elev);

            if (rng.NextFloat01() < settings.treeDensity * treeWeight)
                trees.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 0.8f, typeId = 0, tags = new[] { "tree" } });
            if (rng.NextFloat01() < settings.rockDensity * rockWeight)
                rocks.points.Add(new ScatterPoint { pos = p, scale = 0.7f + rng.NextFloat01() * 1.1f, typeId = 0, tags = new[] { "rock" } });
        }

        map.scatters["trees"] = trees;
        map.scatters["rocks"] = rocks;

        map.zones["river"] = new ZoneDef { zoneId = 0, name = "river" };
        map.zones["north_plain"] = new ZoneDef { zoneId = 1, name = "north_plain" };
        map.zones["south_plain"] = new ZoneDef { zoneId = 2, name = "south_plain" };
        map.EnsureRequiredOutputs();
        log.Log($"Generated SavannaRiver trees={trees.points.Count} rocks={rocks.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static WorldSpline BuildMainRiverSpline(SavannaRiverSettingsSO settings, WorldGridSpec grid, WorldGenRng rng, int seed, NoiseDescriptor warpNoise, Rect mapRect)
    {
        var dir = settings.gradientDir.sqrMagnitude < 0.0001f ? Vector2.down : settings.gradientDir.normalized;
        var start = EdgePointForDirection(mapRect, -dir, rng.NextFloat01());
        var exit = EdgePointForDirection(mapRect, dir, rng.NextFloat01());

        var points = new List<Vector2> { start };
        var p = start;
        var heading = (exit - start).normalized;
        var step = Mathf.Max(0.5f, grid.cellSize);
        var maxSteps = (grid.width + grid.height) * 4;
        var turnStrength = 0.75f + settings.meanderAmp * 2.2f;
        var turnFrequency = Mathf.Max(0.002f, settings.meanderFreq * 0.04f);

        for (var i = 0; i < maxSteps; i++)
        {
            var turnNoise = NoiseUtil.Sample2D(warpNoise, p.x * turnFrequency, p.y * turnFrequency, seed) * 2f - 1f;
            var turn = turnNoise * turnStrength;
            var turned = Rotate(heading, turn);
            heading = Vector2.Lerp(heading, turned, 0.6f).normalized;
            p += heading * step;
            points.Add(p);

            if (Vector2.Distance(p, exit) < step * 1.5f || (mapRect.Contains(p) && Vector2.Dot((exit - p).normalized, heading) > 0.96f && Vector2.Distance(p, exit) < mapRect.width * 0.08f))
                break;
        }

        points.Add(exit);
        points = Chaikin(points, 2);
        ApplyDomainWarp(points, settings, warpNoise, seed);

        return new WorldSpline { id = "river_main", baseWidth = settings.riverWidth, points = points };
    }

    private static void BuildSideChannels(List<WorldSpline> splines, WorldSpline main, SavannaRiverSettingsSO settings, WorldGenRng rng, int seed, NoiseDescriptor warpNoise, Rect mapRect)
    {
        if (main.points.Count < 8 || settings.SideChannelCount <= 0) return;

        for (var i = 0; i < settings.SideChannelCount; i++)
        {
            var t0 = Mathf.Lerp(0.2f, 0.8f, rng.NextFloat01());
            var t1 = Mathf.Clamp01(t0 + 0.08f + rng.NextFloat01() * 0.25f);
            var branchStart = PointOnPolyline(main.points, t0, out var tangentStart);
            var rejoin = PointOnPolyline(main.points, t1, out _);

            var outward = Rotate(tangentStart.normalized, (rng.NextFloat01() - 0.5f) * 1.2f);
            var branchMid = Vector2.Lerp(branchStart, rejoin, 0.5f) + new Vector2(-outward.y, outward.x) * settings.riverWidth * (0.8f + rng.NextFloat01() * 1.2f);

            var channel = new WorldSpline
            {
                id = $"river_side_{i}",
                baseWidth = settings.riverWidth * Mathf.Clamp(settings.SideChannelWidthFactor, 0.2f, 1f),
                points = new List<Vector2> { branchStart, branchMid, rejoin }
            };

            channel.points = Chaikin(channel.points, 1);
            ApplyDomainWarp(channel.points, settings, warpNoise, seed + i + 17);
            splines.Add(channel);
        }
    }

    private static void ApplyCutoffs(WorldSpline main, SavannaRiverSettingsSO settings, WorldGenRng rng)
    {
        var cutoffs = Mathf.Max(0, settings.CutoffCount);
        if (cutoffs == 0 || main.points.Count < 12) return;

        var cutoffDistance = settings.riverWidth * 3f;
        var applied = 0;

        for (var i = 2; i < main.points.Count - 8 && applied < cutoffs; i++)
        {
            for (var j = i + 5; j < main.points.Count - 2 && applied < cutoffs; j++)
            {
                if (Vector2.Distance(main.points[i], main.points[j]) > cutoffDistance) continue;
                if (rng.NextFloat01() > settings.CutoffChance) continue;

                var shortcut = new List<Vector2>();
                shortcut.AddRange(main.points.GetRange(0, i + 1));
                shortcut.Add(Vector2.Lerp(main.points[i], main.points[j], 0.5f));
                shortcut.AddRange(main.points.GetRange(j, main.points.Count - j));
                main.points = Chaikin(shortcut, 1);
                applied++;
            }
        }
    }

    private static void ApplyDomainWarp(List<Vector2> points, SavannaRiverSettingsSO settings, NoiseDescriptor warpNoise, int seed)
    {
        if (settings.RiverWarpAmplitude <= 0f || points == null) return;

        var freq = Mathf.Max(0.001f, settings.RiverWarpFrequency);
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var wx = NoiseUtil.Sample2D(warpNoise, p.x * freq, p.y * freq, seed) * 2f - 1f;
            var wy = NoiseUtil.Sample2D(warpNoise, (p.x + 53f) * freq, (p.y + 91f) * freq, seed) * 2f - 1f;
            points[i] = p + new Vector2(wx, wy) * settings.RiverWarpAmplitude;
        }
    }

    private static Vector2 EdgePointForDirection(Rect rect, Vector2 direction, float edgeT)
    {
        var d = direction.normalized;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
        {
            var x = d.x >= 0f ? rect.xMax : rect.xMin;
            return new Vector2(x, Mathf.Lerp(rect.yMin, rect.yMax, edgeT));
        }

        var y = d.y >= 0f ? rect.yMax : rect.yMin;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, edgeT), y);
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

    private static Vector2 PointOnPolyline(List<Vector2> points, float t, out Vector2 tangent)
    {
        tangent = Vector2.right;
        if (points == null || points.Count == 0) return Vector2.zero;
        if (points.Count == 1) return points[0];

        var lengths = new float[points.Count - 1];
        var total = 0f;
        for (var i = 1; i < points.Count; i++)
        {
            lengths[i - 1] = Vector2.Distance(points[i - 1], points[i]);
            total += lengths[i - 1];
        }

        var target = Mathf.Clamp01(t) * total;
        var acc = 0f;
        for (var i = 0; i < lengths.Length; i++)
        {
            var seg = lengths[i];
            if (target <= acc + seg)
            {
                var local = seg <= 1e-5f ? 0f : (target - acc) / seg;
                tangent = (points[i + 1] - points[i]).normalized;
                return Vector2.Lerp(points[i], points[i + 1], local);
            }
            acc += seg;
        }

        tangent = (points[points.Count - 1] - points[points.Count - 2]).normalized;
        return points[points.Count - 1];
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var cs = Mathf.Cos(radians);
        var sn = Mathf.Sin(radians);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    private static (float distance, float halfWidth) FindNearestRiverInfo(List<WorldSpline> splines, SavannaRiverSettingsSO settings, Vector2 p)
    {
        var minDistance = float.MaxValue;
        var halfWidth = Mathf.Max(0.01f, settings.riverWidth * 0.5f);

        for (var s = 0; s < splines.Count; s++)
        {
            var spline = splines[s];
            if (spline?.points == null || spline.points.Count < 2) continue;

            for (var i = 1; i < spline.points.Count; i++)
            {
                var d = DistanceToSegment(p, spline.points[i - 1], spline.points[i]);
                if (d >= minDistance) continue;
                minDistance = d;
                halfWidth = Mathf.Max(0.01f, spline.baseWidth * 0.5f);
            }
        }

        return (minDistance, halfWidth);
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
