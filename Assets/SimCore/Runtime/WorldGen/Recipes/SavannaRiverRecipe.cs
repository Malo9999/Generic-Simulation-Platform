using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 4;

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

        var isFast = settings.qualityMode == QualityMode.FastPreview;
        var useAdvancedFeatures = settings.qualityMode == QualityMode.Bake;
        var workGrid = BuildWorkGrid(grid, isFast ? 64 : grid.width, isFast ? 64 : grid.height);

        var mapRect = new Rect(grid.originWorld.x, grid.originWorld.y, grid.width * grid.cellSize, grid.height * grid.cellSize);
        var flowDir = settings.gradientDir.sqrMagnitude < Epsilon ? Vector2.down : settings.gradientDir.normalized;

        var terrainTimer = Stopwatch.StartNew();
        var workTerrain = BuildTerrainField(settings, workGrid, seed, heightNoise, warpNoise, flowDir, mapRect);
        var terrain = workGrid.width == grid.width && workGrid.height == grid.height
            ? workTerrain
            : UpscaleField(workTerrain, workGrid, grid);
        terrainTimer.Stop();

        var pathTimer = Stopwatch.StartNew();
        var source = PickEdgeCell(workGrid, workTerrain, flowDir, true, settings.SourceEdgeBias, rng);
        var sink = PickEdgeCell(workGrid, workTerrain, flowDir, false, settings.SourceEdgeBias, rng);
        var mainPath = TraceFlowPath(workGrid, workTerrain, source, sink, settings.FlowInertia, settings.FlowNoiseStrength, seed, wetnessNoise);
        var mainPoints = Chaikin(PathToWorldPoints(workGrid, mainPath), 2);
        if (useAdvancedFeatures)
            TryApplyCutoff(mainPoints, settings.CutoffChance, settings.riverWidth, rng);

        var splines = new List<WorldSpline>
        {
            new WorldSpline { id = "river_main", baseWidth = settings.riverWidth, points = mainPoints }
        };

        if (useAdvancedFeatures)
            AddSideChannels(splines, mainPath, workTerrain, settings, workGrid, rng, seed, wetnessNoise);

        var clippedSplines = new List<WorldSpline>();
        foreach (var spline in splines)
            clippedSplines.AddRange(SplineClipper.ClipToRectParts(spline, mapRect));
        map.splines = clippedSplines;
        pathTimer.Stop();

        var maskTimer = Stopwatch.StartNew();
        var wetlandSeeds = useAdvancedFeatures ? BuildFeatureSeeds(settings.WetlandCount, mapRect, rng) : new List<Vector2>();
        var kopjeSeeds = useAdvancedFeatures ? BuildFeatureSeeds(settings.KopjeCount, mapRect, rng) : new List<Vector2>();

        var height = new ScalarField("height", grid);
        var wetness = new ScalarField("wetness", grid);
        var water = new MaskField("water", grid, MaskEncoding.Boolean);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var biomes = new MaskField("biomes", grid, MaskEncoding.Categorical) { categories = new[] { "savanna", "wetland", "rocky", "water" } };
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "river_corridor", "wetland", "north_plain", "south_plain", "kopje" } };

        var trees = new ScatterSet { id = "trees" };
        var rocks = new ScatterSet { id = "rocks" };

        var perp = new Vector2(-flowDir.y, flowDir.x);
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var nearest = FindNearestSplineInfo(map.splines, p);
            var along = nearest.along;
            var width = ChannelWidth(settings, along, seed, wetnessNoise, p, nearest.splineId == "river_main");

            var terrainH = terrain[x, y];
            var wetNoise = NoiseUtil.Sample2D(wetnessNoise, p.x, p.y, seed) * 2f - 1f;
            var floodExtent = width * (2.2f + (useAdvancedFeatures ? settings.WetlandNoiseStrength * 1.2f * wetNoise : 0f));
            var floodFactor = Mathf.Clamp01(1f - nearest.distance / Mathf.Max(0.01f, floodExtent));

            var wetlandBlob = useAdvancedFeatures ? FeatureBlobInfluence(p, wetlandSeeds, settings.floodplainWidth * 0.9f) : 0f;
            var wetnessValue = Mathf.Clamp01(floodFactor * 0.78f + wetlandBlob * 0.35f + Mathf.Clamp01(0.5f - terrainH) * 0.25f + (wetNoise * 0.5f + 0.5f) * 0.18f);
            wetness[x, y] = wetnessValue;

            var isWater = nearest.distance < width * 0.5f;
            var bankNoise = NoiseUtil.Sample2D(warpNoise, p.x * 0.06f, p.y * 0.06f, seed) * 2f - 1f;
            var isBank = nearest.distance < width * (0.8f + bankNoise * 0.25f);

            var isKopje = false;
            if (useAdvancedFeatures)
            {
                var kopjeNoise = NoiseUtil.Sample2D(heightNoise, p.x * 1.9f, p.y * 1.9f, seed + 41) * 2f - 1f;
                var kopjeBlob = FeatureBlobInfluence(p, kopjeSeeds, settings.floodplainWidth * 0.7f);
                isKopje = !isWater && wetnessValue < 0.4f && terrainH > settings.waterLevel + 0.14f && (kopjeBlob + kopjeNoise * settings.KopjeNoiseStrength) > 0.52f;
            }

            height[x, y] = terrainH - floodFactor * settings.carveStrength - (isWater ? 0.07f : 0f) + (isKopje ? 0.06f : 0f);
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);

            biomes[x, y] = isWater ? (byte)3 : isKopje ? (byte)2 : (isBank || wetnessValue > 0.58f) ? (byte)1 : (byte)0;

            if (isWater || isBank) zones[x, y] = 0;
            else if (wetnessValue > 0.62f || wetlandBlob > 0.58f) zones[x, y] = 1;
            else if (isKopje) zones[x, y] = 4;
            else
            {
                var side = Vector2.Dot(p - nearest.closestPoint, perp);
                zones[x, y] = (byte)(side >= 0f ? 2 : 3);
            }
        }

        height.Normalize01InPlace();
        wetness.Normalize01InPlace();
        ApplyContrastGamma(height, settings.HeightContrast, settings.HeightGamma);
        ApplyContrastGamma(wetness, settings.WetnessContrast, settings.WetnessGamma);

        map.scalars["height"] = height;
        map.scalars["wetness"] = wetness;
        map.masks["water"] = water;
        map.masks["walkable"] = walkable;
        map.masks["biomes"] = biomes;
        map.masks["zones"] = zones;
        maskTimer.Stop();

        var scatterTimer = Stopwatch.StartNew();
        var treeDensity = settings.treeDensity;
        var rockDensity = settings.rockDensity;
        if (isFast)
        {
            treeDensity *= 0.45f;
            rockDensity *= 0.35f;
        }

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            if (water[x, y] > 0) continue;
            var p = grid.CellCenterWorld(x, y);
            var wetnessValue = wetness[x, y];
            var terrainH = height[x, y];
            var nearWater = wetnessValue > 0.45f;
            var dryHighGround = wetnessValue < 0.35f && terrainH > settings.waterLevel;

            if (nearWater)
            {
                var treeWeight = Mathf.Clamp01(wetnessValue * 0.9f - terrainH * 0.15f);
                if (rng.NextFloat01() < treeDensity * treeWeight)
                    trees.points.Add(new ScatterPoint { pos = p, scale = 0.75f + rng.NextFloat01(), typeId = 0, tags = new[] { "tree" } });
            }

            if (dryHighGround)
            {
                var rockWeight = Mathf.Clamp01(terrainH * 0.7f - wetnessValue * 0.35f);
                if (rng.NextFloat01() < rockDensity * rockWeight)
                    rocks.points.Add(new ScatterPoint { pos = p, scale = 0.7f + rng.NextFloat01() * 1.2f, typeId = 0, tags = new[] { "rock" } });
            }
        }

        map.scatters["trees"] = trees;
        map.scatters["rocks"] = rocks;
        scatterTimer.Stop();

        map.zones["river_corridor"] = new ZoneDef { zoneId = 0, name = "river_corridor" };
        map.zones["wetland"] = new ZoneDef { zoneId = 1, name = "wetland" };
        map.zones["north_plain"] = new ZoneDef { zoneId = 2, name = "north_plain" };
        map.zones["south_plain"] = new ZoneDef { zoneId = 3, name = "south_plain" };
        map.zones["kopje"] = new ZoneDef { zoneId = 4, name = "kopje" };

        map.EnsureRequiredOutputs();
        log.Log($"SavannaRiver stages: terrain field={terrainTimer.ElapsedMilliseconds} ms | path generation={pathTimer.ElapsedMilliseconds} ms | masks={maskTimer.ElapsedMilliseconds} ms | scatter={scatterTimer.ElapsedMilliseconds} ms | preview texture=0 ms");
        log.Log($"Generated SavannaRiver ({settings.qualityMode}): path={mainPoints.Count} trees={trees.points.Count} rocks={rocks.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static WorldGridSpec BuildWorkGrid(WorldGridSpec source, int targetWidth, int targetHeight)
    {
        var scaleX = (float)source.width / Mathf.Max(1, targetWidth);
        var scaleY = (float)source.height / Mathf.Max(1, targetHeight);
        var scale = Mathf.Max(1f, Mathf.Max(scaleX, scaleY));
        var width = Mathf.Clamp(Mathf.RoundToInt(source.width / scale), 16, source.width);
        var height = Mathf.Clamp(Mathf.RoundToInt(source.height / scale), 16, source.height);
        return new WorldGridSpec
        {
            width = width,
            height = height,
            cellSize = source.cellSize * scale,
            originWorld = source.originWorld
        };
    }

    private static float[,] UpscaleField(float[,] sourceValues, WorldGridSpec sourceGrid, WorldGridSpec targetGrid)
    {
        var output = new float[targetGrid.width, targetGrid.height];
        for (var y = 0; y < targetGrid.height; y++)
        for (var x = 0; x < targetGrid.width; x++)
        {
            var u = targetGrid.width <= 1 ? 0f : (float)x / (targetGrid.width - 1);
            var v = targetGrid.height <= 1 ? 0f : (float)y / (targetGrid.height - 1);
            var sx = u * (sourceGrid.width - 1);
            var sy = v * (sourceGrid.height - 1);
            var x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, sourceGrid.width - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, sourceGrid.height - 1);
            var x1 = Mathf.Min(sourceGrid.width - 1, x0 + 1);
            var y1 = Mathf.Min(sourceGrid.height - 1, y0 + 1);
            var tx = sx - x0;
            var ty = sy - y0;
            var a = Mathf.Lerp(sourceValues[x0, y0], sourceValues[x1, y0], tx);
            var b = Mathf.Lerp(sourceValues[x0, y1], sourceValues[x1, y1], tx);
            output[x, y] = Mathf.Lerp(a, b, ty);
        }

        return output;
    }

    private static float[,] BuildTerrainField(SavannaRiverSettingsSO settings, WorldGridSpec grid, int seed, NoiseDescriptor heightNoise, NoiseDescriptor warpNoise, Vector2 flowDir, Rect mapRect)
    {
        var terrain = new float[grid.width, grid.height];
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var warpAmp = settings.RiverWarpAmplitude;
            var wx = 0f;
            var wy = 0f;
            if (warpAmp > 0.001f)
            {
                wx = (NoiseUtil.Sample2D(warpNoise, p.x * settings.RiverWarpFrequency, p.y * settings.RiverWarpFrequency, seed) * 2f - 1f) * warpAmp;
                wy = (NoiseUtil.Sample2D(warpNoise, (p.x + 77f) * settings.RiverWarpFrequency, (p.y - 33f) * settings.RiverWarpFrequency, seed) * 2f - 1f) * warpAmp;
            }

            var wp = p + new Vector2(wx, wy);
            var u = (wp.x - mapRect.xMin) / Mathf.Max(0.001f, mapRect.width);
            var v = (wp.y - mapRect.yMin) / Mathf.Max(0.001f, mapRect.height);
            var slopeSample = Vector2.Dot(new Vector2(u, v), flowDir) * 0.5f + 0.5f;

            var low = NoiseUtil.Sample2D(heightNoise, wp.x * 0.42f, wp.y * 0.42f, seed) * 2f - 1f;
            var med = NoiseUtil.Sample2D(heightNoise, wp.x * 1.6f + 31f, wp.y * 1.6f - 19f, seed + 13) * 2f - 1f;
            var noiseStrength = Mathf.Max(0f, settings.heightNoiseStrength);
            terrain[x, y] = slopeSample + (low * 0.42f + med * 0.25f) * noiseStrength;
        }

        NormalizeArray01(terrain, grid.width, grid.height);
        return terrain;
    }

    private static Vector2Int PickEdgeCell(WorldGridSpec grid, float[,] terrain, Vector2 flowDir, bool source, float edgeBias, WorldGenRng rng)
    {
        var target = source ? -flowDir : flowDir;
        var best = new List<(Vector2Int cell, float score)>();

        for (var i = 0; i < grid.width; i++)
        {
            best.Add((new Vector2Int(i, 0), ScoreEdge(i, 0)));
            best.Add((new Vector2Int(i, grid.height - 1), ScoreEdge(i, grid.height - 1)));
        }

        for (var j = 1; j < grid.height - 1; j++)
        {
            best.Add((new Vector2Int(0, j), ScoreEdge(0, j)));
            best.Add((new Vector2Int(grid.width - 1, j), ScoreEdge(grid.width - 1, j)));
        }

        best.Sort((a, b) => b.score.CompareTo(a.score));
        var pickIndex = Mathf.Clamp(Mathf.FloorToInt((1f - Mathf.Clamp01(edgeBias)) * (best.Count - 1) * rng.NextFloat01() * 0.35f), 0, best.Count - 1);
        return best[pickIndex].cell;

        float ScoreEdge(int x, int y)
        {
            var nx = grid.width <= 1 ? 0f : (float)x / (grid.width - 1);
            var ny = grid.height <= 1 ? 0f : (float)y / (grid.height - 1);
            var directional = Vector2.Dot(new Vector2(nx * 2f - 1f, ny * 2f - 1f), target);
            var elev = source ? terrain[x, y] : 1f - terrain[x, y];
            return directional * 0.7f + elev * 0.3f + rng.NextFloat01() * 0.2f;
        }
    }

    private static List<Vector2Int> TraceFlowPath(WorldGridSpec grid, float[,] terrain, Vector2Int source, Vector2Int sink, float inertia, float flowNoiseStrength, int seed, NoiseDescriptor wetnessNoise)
    {
        var path = new List<Vector2Int> { source };
        var visited = new HashSet<int> { source.y * grid.width + source.x };
        var current = source;
        var heading = (sink - source);
        var headingVec = new Vector2(heading.x, heading.y).normalized;
        var maxSteps = grid.width * grid.height;

        for (var i = 0; i < maxSteps; i++)
        {
            if (current == sink) break;

            var candidates = new List<(Vector2Int cell, float score)>();
            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                var nx = current.x + ox;
                var ny = current.y + oy;
                if (nx < 0 || ny < 0 || nx >= grid.width || ny >= grid.height) continue;

                var nCell = new Vector2Int(nx, ny);
                var delta = new Vector2(ox, oy).normalized;
                var downhill = terrain[current.x, current.y] - terrain[nx, ny];
                var towardSink = Vector2.Dot((sink - nCell), (sink - current));
                var continuity = Mathf.Clamp01((Vector2.Dot(delta, headingVec) + 1f) * 0.5f);
                var flowNoise = NoiseUtil.Sample2D(wetnessNoise, nx * 0.11f, ny * 0.11f, seed + i) * 2f - 1f;
                var score = downhill * 1.25f + continuity * Mathf.Clamp01(inertia) + towardSink * 0.004f + flowNoise * flowNoiseStrength;

                if (visited.Contains(ny * grid.width + nx)) score -= 0.45f;
                candidates.Add((nCell, score));
            }

            if (candidates.Count == 0) break;
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var next = candidates[0].cell;
            if (next == current) break;

            headingVec = ((Vector2)(next - current)).normalized;
            current = next;
            path.Add(current);
            visited.Add(current.y * grid.width + current.x);

            if ((current - sink).sqrMagnitude <= 2) break;
        }

        if (path[path.Count - 1] != sink) path.Add(sink);
        return path;
    }

    private static List<Vector2> PathToWorldPoints(WorldGridSpec grid, List<Vector2Int> path)
    {
        var points = new List<Vector2>(path.Count);
        for (var i = 0; i < path.Count; i++)
            points.Add(grid.CellCenterWorld(path[i].x, path[i].y));
        return points;
    }

    private static void TryApplyCutoff(List<Vector2> points, float cutoffChance, float width, WorldGenRng rng)
    {
        if (points == null || points.Count < 16 || rng.NextFloat01() > cutoffChance) return;

        var threshold = width * 2.8f;
        for (var i = 3; i < points.Count - 7; i++)
        for (var j = i + 6; j < points.Count - 2; j++)
        {
            if (Vector2.Distance(points[i], points[j]) > threshold) continue;
            var shortened = new List<Vector2>();
            shortened.AddRange(points.GetRange(0, i + 1));
            shortened.Add(Vector2.Lerp(points[i], points[j], 0.5f));
            shortened.AddRange(points.GetRange(j, points.Count - j));
            points.Clear();
            points.AddRange(Chaikin(shortened, 1));
            return;
        }
    }

    private static void AddSideChannels(List<WorldSpline> splines, List<Vector2Int> mainPath, float[,] terrain, SavannaRiverSettingsSO settings, WorldGridSpec grid, WorldGenRng rng, int seed, NoiseDescriptor wetnessNoise)
    {
        var count = Mathf.Clamp(settings.SideChannelCount, 0, 2);
        if (count <= 0 || mainPath.Count < 14) return;

        for (var i = 0; i < count; i++)
        {
            var startIndex = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(0.2f, 0.75f, rng.NextFloat01()) * (mainPath.Count - 1)), 2, mainPath.Count - 5);
            var start = mainPath[startIndex];
            var heading = ((Vector2)(mainPath[startIndex + 1] - mainPath[startIndex])).normalized;
            var lateral = rng.NextFloat01() > 0.5f ? new Vector2(-heading.y, heading.x) : new Vector2(heading.y, -heading.x);
            var current = start;
            var channel = new List<Vector2Int> { current };

            var steps = Mathf.Min(mainPath.Count, 36);
            for (var s = 0; s < steps; s++)
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

                    var n = new Vector2Int(nx, ny);
                    var downhill = terrain[current.x, current.y] - terrain[nx, ny];
                    var dir = new Vector2(ox, oy).normalized;
                    var lateralBias = Vector2.Dot(dir, lateral);
                    var nScore = downhill * 1.3f + lateralBias * 0.35f + (NoiseUtil.Sample2D(wetnessNoise, nx * 0.09f, ny * 0.09f, seed + i * 13) * 2f - 1f) * 0.16f;
                    if (nScore > bestScore)
                    {
                        bestScore = nScore;
                        best = n;
                    }
                }

                if (best == current) break;
                current = best;
                channel.Add(current);

                if (s > 8)
                {
                    for (var m = startIndex + 5; m < mainPath.Count; m++)
                    {
                        if ((mainPath[m] - current).sqrMagnitude > 4) continue;
                        channel.Add(mainPath[m]);
                        s = steps;
                        break;
                    }
                }
            }

            if (channel.Count < 6) continue;
            var points = Chaikin(PathToWorldPoints(grid, channel), 1);
            splines.Add(new WorldSpline
            {
                id = $"river_side_{i}",
                baseWidth = settings.riverWidth * Mathf.Clamp(settings.SideChannelWidthFactor, 0.25f, 0.8f),
                points = points
            });
        }
    }

    private static List<Vector2> BuildFeatureSeeds(int count, Rect mapRect, WorldGenRng rng)
    {
        var seeds = new List<Vector2>();
        for (var i = 0; i < Mathf.Max(0, count); i++)
        {
            seeds.Add(new Vector2(
                Mathf.Lerp(mapRect.xMin, mapRect.xMax, rng.NextFloat01()),
                Mathf.Lerp(mapRect.yMin, mapRect.yMax, rng.NextFloat01())));
        }

        return seeds;
    }

    private static float FeatureBlobInfluence(Vector2 p, List<Vector2> seeds, float radius)
    {
        if (seeds == null || seeds.Count == 0) return 0f;
        var inv = 1f / Mathf.Max(0.01f, radius);
        var influence = 0f;
        for (var i = 0; i < seeds.Count; i++)
        {
            var d = Vector2.Distance(p, seeds[i]) * inv;
            influence = Mathf.Max(influence, Mathf.Clamp01(1f - d));
        }

        return influence;
    }

    private static float ChannelWidth(SavannaRiverSettingsSO settings, float t, int seed, NoiseDescriptor wetnessNoise, Vector2 p, bool isMain)
    {
        var accumulation = Mathf.Lerp(0.65f, 1.45f, Mathf.Clamp01(t));
        var noise = NoiseUtil.Sample2D(wetnessNoise, p.x * 0.17f, p.y * 0.17f, seed + 99) * 2f - 1f;
        var variation = 1f + noise * Mathf.Clamp01(settings.WidthVariation) * 0.55f;
        var mainScale = isMain ? 1f : Mathf.Clamp(settings.SideChannelWidthFactor, 0.25f, 0.8f);
        return Mathf.Max(0.75f, settings.riverWidth * accumulation * variation * mainScale);
    }

    private static (float distance, float along, Vector2 closestPoint, string splineId) FindNearestSplineInfo(List<WorldSpline> splines, Vector2 p)
    {
        var minDistance = float.MaxValue;
        var bestAlong = 0f;
        var bestPoint = p;
        var bestId = "river_main";

        for (var s = 0; s < splines.Count; s++)
        {
            var spline = splines[s];
            if (spline?.points == null || spline.points.Count < 2) continue;

            var totalLen = 0f;
            for (var i = 1; i < spline.points.Count; i++) totalLen += Vector2.Distance(spline.points[i - 1], spline.points[i]);

            var traversed = 0f;
            for (var i = 1; i < spline.points.Count; i++)
            {
                var a = spline.points[i - 1];
                var b = spline.points[i];
                var ab = b - a;
                var lenSq = ab.sqrMagnitude;
                if (lenSq < Epsilon) continue;

                var segLen = Mathf.Sqrt(lenSq);
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                var proj = a + ab * t;
                var d = Vector2.Distance(p, proj);
                if (d < minDistance)
                {
                    minDistance = d;
                    bestAlong = (traversed + segLen * t) / Mathf.Max(Epsilon, totalLen);
                    bestPoint = proj;
                    bestId = spline.id;
                }

                traversed += segLen;
            }
        }

        return (minDistance, Mathf.Clamp01(bestAlong), bestPoint, bestId);
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
            v = Mathf.Pow(v, g);
            field[x, y] = Mathf.Clamp01(v);
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
