using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SavannaRiverRecipe : WorldRecipeBase<SavannaRiverSettingsSO>
{
    public override string RecipeId => "SavannaRiver";
    public override int Version => 9;

    private const int FastMinControlPoints = 24;
    private const int FastMaxControlPoints = 40;
    private const int SlowMinControlPoints = 32;
    private const int SlowMaxControlPoints = 64;
    private const int MaxPreviewScatter = 256;
    private const int MaxTributaries = 3;
    private const int MaxQualityModeTributaries = 1;
    private const int TributaryStartSearchAttempts = 24;
    private const int TributaryTraceMaxSteps = 160;
    private const int TributaryParallelAbortSteps = 14;
    private const float Epsilon = 1e-5f;

    private struct RiverPathData
    {
        public List<Vector2> main;
        public List<List<Vector2>> tributaries;
        public List<Vector2> oxbow;
        public List<int> cutoffRemovedIndices;
        public int mainControlStations;
        public int tributaryControlStations;
        public int cutoffApplied;
    }

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
        var riverData = BuildRiverSystem(settings, grid, mapRect, flowDir, seed, wetnessNoise, warpNoise, log);
        var splines = new List<WorldSpline>
        {
            new WorldSpline { id = "river_main", baseWidth = Mathf.Max(0.5f, settings.riverWidth), points = riverData.main }
        };

        for (var i = 0; i < riverData.tributaries.Count; i++)
        {
            var tribWidth = Mathf.Max(0.35f, settings.riverWidth * Mathf.Clamp(settings.TributaryWidthFactor, 0.2f, 0.9f));
            splines.Add(new WorldSpline { id = $"river_trib_{i}", baseWidth = tribWidth, points = riverData.tributaries[i] });
        }

        if (riverData.oxbow != null && riverData.oxbow.Count > 1)
        {
            var oxbowWidth = Mathf.Max(0.25f, settings.riverWidth * Mathf.Clamp(settings.OxbowWidthFactor, 0.2f, 0.8f));
            splines.Add(new WorldSpline { id = "river_oxbow", baseWidth = oxbowWidth, points = riverData.oxbow });
        }

        map.splines = new List<WorldSpline>();
        foreach (var spline in splines)
            map.splines.AddRange(SplineClipper.ClipToRectParts(spline, mapRect));
        pathTimer.Stop();

        var maskTimer = Stopwatch.StartNew();
        var height = new ScalarField("height", grid);
        var wetness = new ScalarField("wetness", grid);
        var water = new MaskField("water", grid, MaskEncoding.Boolean);
        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "river_corridor", "upland" } };
        var biomes = new MaskField("biomes", grid, MaskEncoding.Categorical) { categories = new[] { "upland", "valley", "floodplain", "water" } };

        var riverHalfBase = Mathf.Max(0.5f, settings.riverWidth * 0.5f);
        var floodRadiusBase = Mathf.Max(riverHalfBase + 0.75f, settings.floodplainWidth);
        var bankRadiusBase = Mathf.Max(riverHalfBase + 0.25f, settings.bankWidth + riverHalfBase);
        var valleyWidthBase = Mathf.Max(floodRadiusBase + grid.cellSize * 1.5f, settings.valleyWidth);
        var tribHalfBase = riverHalfBase * Mathf.Clamp(settings.TributaryWidthFactor, 0.2f, 0.9f);
        var tribFloodScale = Mathf.Lerp(0.58f, 0.8f, Mathf.Clamp01(settings.TributaryWidthFactor));
        var widthPhase = Mathf.Abs(Mathf.Sin(seed * 0.173f)) * Mathf.PI * 2f;
        var terraceCount = Mathf.Max(1, settings.TerraceCount);
        var richnessEnabled = settings.qualityMode != QualityMode.FastPreview;
        var terraceStrength = richnessEnabled ? Mathf.Clamp01(settings.TerraceStrength) * 0.75f : 0f;
        var bankRoughStrength = richnessEnabled ? Mathf.Clamp01(settings.BankRoughnessStrength) * 0.75f : 0f;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var distMain = DistanceToPolylineWithT(riverData.main, p, out var tAlongMain);

            var widthNoise = NoiseUtil.Sample2D(warpNoise, tAlongMain * 0.75f + 11f, seed * 0.0027f, seed + 101) * 2f - 1f;
            var widthWave = Mathf.Sin(tAlongMain * Mathf.PI * 1.6f + widthPhase) * 0.06f;
            var widthVariation = widthNoise * Mathf.Clamp01(settings.WidthVariationStrength) * 0.16f;
            var widthScale = Mathf.Clamp(1f + widthWave + widthVariation, 0.85f, 1.2f);

            var riverHalfMain = riverHalfBase * widthScale;
            var floodRadiusMain = floodRadiusBase * (1f + widthWave * 0.12f + widthVariation * 0.45f);
            var bankRadiusMain = bankRadiusBase * Mathf.Lerp(0.96f, 1.04f, 1f - tAlongMain);
            var valleyRadiusMain = valleyWidthBase * (1f + widthVariation * 0.3f);

            var distRiver = distMain;
            var tAlongRiver = tAlongMain;
            var riverHalf = riverHalfMain;
            var floodRadius = floodRadiusMain;
            var bankRadius = bankRadiusMain;
            var valleyRadius = valleyRadiusMain;
            var nearestTrib = float.MaxValue;

            for (var i = 0; i < riverData.tributaries.Count; i++)
            {
                var tribDist = DistanceToPolylineWithT(riverData.tributaries[i], p, out var tAlongTrib);
                if (tribDist < nearestTrib) nearestTrib = tribDist;
                if (tribDist < distRiver)
                {
                    distRiver = tribDist;
                    tAlongRiver = tAlongTrib;
                    riverHalf = tribHalfBase * Mathf.Lerp(0.86f, 1.05f, 1f - tAlongTrib);
                    floodRadius = floodRadiusBase * tribFloodScale * Mathf.Lerp(0.88f, 1.02f, 1f - tAlongTrib);
                    bankRadius = bankRadiusBase * tribFloodScale;
                    valleyRadius = valleyWidthBase * Mathf.Lerp(0.62f, 0.78f, 1f - tAlongTrib);
                }
            }

            var oxbowHint = 0f;
            if (riverData.oxbow != null && riverData.oxbow.Count > 1)
            {
                var oxbowDist = DistanceToPolylineWithT(riverData.oxbow, p, out _);
                var oxbowWidth = Mathf.Max(0.25f, settings.riverWidth * Mathf.Clamp(settings.OxbowWidthFactor, 0.2f, 0.8f));
                oxbowHint = Mathf.Clamp01(1f - oxbowDist / Mathf.Max(0.01f, oxbowWidth * 2f));
            }

            var slope = SlopeSample(grid, x, y, flowDir);
            var terrainNoise = (NoiseUtil.Sample2D(heightNoise, p.x * 0.21f, p.y * 0.21f, seed) * 2f - 1f) * settings.heightNoiseStrength * 0.22f;
            var floodNoise = (NoiseUtil.Sample2D(wetnessNoise, p.x * 0.07f, p.y * 0.07f, seed + 17) * 2f - 1f) * 0.17f;
            var valleyNoise = (NoiseUtil.Sample2D(warpNoise, p.x * 0.045f + 5f, p.y * 0.045f - 9f, seed + 203) * 2f - 1f) * 0.12f;
            var bankNoiseA = (NoiseUtil.Sample2D(warpNoise, p.x * 0.13f + 41f, p.y * 0.13f - 27f, seed + 223) * 2f - 1f);
            var bankNoiseB = (NoiseUtil.Sample2D(heightNoise, p.x * 0.29f - 13f, p.y * 0.29f + 7f, seed + 317) * 2f - 1f);

            var floodEdge = Mathf.Max(0.01f, floodRadius * (1f + floodNoise * 0.4f));
            var floodFactor = Mathf.Clamp01(1f - distRiver / floodEdge);
            var floodplainMask = Mathf.SmoothStep(0f, 1f, floodFactor + floodNoise * 0.2f);
            var bankFactor = Mathf.Clamp01(1f - distRiver / Mathf.Max(0.01f, bankRadius));
            var valleyDist = Mathf.Clamp01(distRiver / Mathf.Max(0.01f, valleyRadius));
            var valleyFactor = 1f - Mathf.SmoothStep(0f, 1f, valleyDist + valleyNoise * 0.15f);
            var isWater = distRiver <= riverHalf;

            var confluenceBoost = nearestTrib < float.MaxValue ? Mathf.Clamp01(1f - nearestTrib / Mathf.Max(0.01f, floodRadiusBase * 0.9f)) : 0f;
            floodplainMask = Mathf.Clamp01(floodplainMask + confluenceBoost * 0.14f);

            var bendStrength = Mathf.Abs(Mathf.Sin(tAlongRiver * Mathf.PI * 2f * Mathf.Max(0.5f, settings.MeanderFreq)));
            var roughEnvelope = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(distRiver / Mathf.Max(0.01f, bankRadius * 1.8f)));
            var bankRough = (bankNoiseA * 0.65f + bankNoiseB * 0.35f) * roughEnvelope * bankRoughStrength * Mathf.Lerp(0.75f, 1.2f, bendStrength);
            var terraceInput = Mathf.Clamp01(Mathf.Clamp01(distRiver / Mathf.Max(0.01f, valleyRadius)) + valleyNoise * 0.08f);
            var terraced = Mathf.Floor(terraceInput * terraceCount) / terraceCount;
            var terraceDelta = (terraced - terraceInput) * terraceStrength * valleyFactor;

            var h = slope + terrainNoise;
            h -= valleyFactor * settings.valleyDepth;
            h -= floodplainMask * settings.carveStrength;
            h += terraceDelta;
            h += bankRough;
            h -= isWater ? 0.07f : 0f;
            h -= oxbowHint * 0.02f;
            h = Mathf.Lerp(h, settings.waterLevel, 0.24f * floodplainMask);

            height[x, y] = h;
            wetness[x, y] = Mathf.Clamp01(floodplainMask * 0.82f + valleyFactor * 0.2f + oxbowHint * 0.18f + (0.5f - slope) * 0.12f + 0.08f + floodNoise * 0.06f);
            water[x, y] = (byte)(isWater ? 1 : 0);
            walkable[x, y] = (byte)(isWater ? 0 : 1);
            zones[x, y] = (byte)(bankFactor > 0.05f || valleyFactor > 0.1f ? 0 : 1);
            biomes[x, y] = isWater ? (byte)3 : (floodplainMask > 0.3f ? (byte)2 : (valleyFactor > 0.18f ? (byte)1 : (byte)0));
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

        var terrainFields = TerrainFieldBuilder.Build(
            grid,
            height,
            riverData.main,
            new TerrainFieldBuildParams
            {
                valleyRadius = valleyWidthBase,
                floodplainRadius = floodRadiusBase,
                ridgeRadius = Mathf.Max(valleyWidthBase * 2.4f, floodRadiusBase * 3f),
                rockSlopeWeight = 0.72f,
                rockDrynessWeight = 0.28f
            });
        terrainFields.WriteTo(map);
        maskTimer.Stop();

        var scatterTimer = Stopwatch.StartNew();
        var trees = new ScatterSet { id = "trees" };
        var rocks = new ScatterSet { id = "rocks" };
        var previewCap = settings.qualityMode == QualityMode.FastPreview ? MaxPreviewScatter : int.MaxValue;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var dist = DistanceToPolylineWithT(riverData.main, p, out _);
            for (var i = 0; i < riverData.tributaries.Count; i++)
                dist = Mathf.Min(dist, DistanceToPolylineWithT(riverData.tributaries[i], p, out _));

            if (water[x, y] > 0) continue;

            var valleyRange = Mathf.Max(0.01f, valleyWidthBase);
            var floodRange = Mathf.Max(0.01f, floodRadiusBase);
            var valleyPresence = Mathf.Clamp01(1f - dist / valleyRange);
            var floodPresence = Mathf.Clamp01(1f - dist / floodRange);
            var valleyEdge = Mathf.Clamp01((dist - floodRange * 0.72f) / Mathf.Max(0.01f, valleyRange - floodRange * 0.72f));

            if (trees.points.Count < previewCap)
            {
                var treeChance = settings.treeDensity * Mathf.Clamp01(0.2f + floodPresence * 0.85f + valleyPresence * 0.35f - valleyEdge * 0.25f);
                if (rng.NextFloat01() < treeChance)
                    trees.points.Add(new ScatterPoint { pos = p, scale = 0.8f + rng.NextFloat01() * 0.8f, typeId = 0, tags = new[] { "tree" } });
            }

            if (rocks.points.Count < previewCap)
            {
                var rockChance = settings.rockDensity * Mathf.Clamp01(valleyEdge * 0.85f + (1f - valleyPresence) * 0.45f);
                if (rng.NextFloat01() < rockChance)
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
        log.Log($"SavannaRiver path stats: mainControl={riverData.mainControlStations} tributaryControl={riverData.tributaryControlStations} tributaries={riverData.tributaries.Count} cutoff={riverData.cutoffApplied} splinePoints={riverData.main.Count}");
        log.Log($"Generated SavannaRiver ({settings.qualityMode}): trees={trees.points.Count} rocks={rocks.points.Count} splines={map.splines.Count}");
        return map;
    }

    private static RiverPathData BuildRiverSystem(SavannaRiverSettingsSO settings, WorldGridSpec grid, Rect mapRect, Vector2 flowDir, int seed, NoiseDescriptor meanderNoise, NoiseDescriptor warpNoise, IWorldGenLogger log)
    {
        var data = new RiverPathData
        {
            tributaries = new List<List<Vector2>>(),
            cutoffRemovedIndices = new List<int>()
        };

        data.main = BuildRiverCenterline(settings, grid, mapRect, flowDir, seed, meanderNoise, warpNoise, out data.mainControlStations, log);
        var richnessEnabled = settings.qualityMode != QualityMode.FastPreview;
        if (richnessEnabled)
            TryApplyCutoff(settings, seed, data.main, out data.oxbow, out data.cutoffRemovedIndices, out data.cutoffApplied);
        else
            TryApplyCutoff(settings, seed, null, out data.oxbow, out data.cutoffRemovedIndices, out data.cutoffApplied);

        var requestedTributaries = richnessEnabled
            ? Mathf.Clamp(settings.TributaryCount, 0, Mathf.Min(MaxTributaries, MaxQualityModeTributaries))
            : 0;
        var progressDir = DominantCardinal(flowDir);
        var lateralDir = new Vector2(-progressDir.y, progressDir.x);

        for (var i = 0; i < requestedTributaries; i++)
        {
            var joinT = Mathf.Lerp(0.18f, 0.86f, i / (float)Mathf.Max(1, requestedTributaries - 1));
            joinT += (Mathf.Sin((seed + 101 * (i + 1)) * 0.013f) * 0.5f + 0.5f - 0.5f) * 0.18f;
            joinT = Mathf.Clamp(joinT, 0.12f, 0.9f);
            var joinPoint = SamplePolyline(data.main, joinT, out _);
            var sideSign = (i % 2 == 0) ? 1f : -1f;
            var sideJitter = Mathf.Sin((seed + i * 47) * 0.031f) * 0.35f;
            var sideDir = (lateralDir * sideSign + progressDir * sideJitter).normalized;

            var trib = BuildTributaryPath(settings, grid, mapRect, data.main, joinPoint, sideDir, flowDir, seed + i * 97, meanderNoise, out var controls);
            data.tributaryControlStations += controls;
            if (trib != null && trib.Count > 1)
                data.tributaries.Add(trib);
        }

        return data;
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
            var curvatureNoise = (NoiseUtil.Sample2D(meanderNoise, t * 0.65f + 17f, seed * 0.0039f, seed + 151) * 2f - 1f) * Mathf.Clamp01(settings.CurvatureNoiseStrength);
            var localAmp = meanderAmp * (1f + curvatureNoise * 0.25f);
            var localPhase = phase + curvatureNoise * Mathf.PI * 0.22f;

            var sineOffset = Mathf.Sin(t * meanderFreq * Mathf.PI * 2f + localPhase) * localAmp;
            var lowFreqNoise = (NoiseUtil.Sample2D(meanderNoise, t * 1.1f + 3f, seed * 0.0043f, seed + 31) * 2f - 1f) * localAmp * 0.22f;
            var warpNoiseSample = (NoiseUtil.Sample2D(warpNoise, basePoint.x * warpFreq, basePoint.y * warpFreq, seed + 47) * 2f - 1f) * warpAmp;
            var taper = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI));

            var offset = (sineOffset + lowFreqNoise + warpNoiseSample) * taper;
            var p = basePoint + lateralDir * offset;
            controls.Add(ClampToRect(p, inset));
        }

        controls.Add(end);
        return Chaikin(controls, 2);
    }

    private static List<Vector2> BuildTributaryPath(SavannaRiverSettingsSO settings, WorldGridSpec grid, Rect mapRect, List<Vector2> mainRiver, Vector2 joinPoint, Vector2 sideDir, Vector2 flowDir, int seed, NoiseDescriptor meanderNoise, out int controlStations)
    {
        controlStations = 0;
        if (mainRiver == null || mainRiver.Count < 2)
            return null;

        if (!TryFindTributaryStart(settings, grid, mapRect, mainRiver, joinPoint, sideDir, flowDir, seed, out var start))
            return null;

        var step = Mathf.Max(grid.cellSize * 1.1f, settings.riverWidth * 0.55f);
        var riverJoinDistance = Mathf.Max(settings.riverWidth, step * 0.6f);
        var nearRiverBand = Mathf.Max(settings.riverWidth * 2f, step * 2f);
        var meanderFactor = Mathf.Clamp(settings.TributaryMeanderFactor, 0.15f, 0.85f);
        var sideNoiseScale = 0.18f * meanderFactor;
        var inset = InsetRect(mapRect, grid.cellSize);
        var points = new List<Vector2> { start };
        var consecutiveNearRiver = 0;
        var current = start;
        var currentHeight = TerrainHeightEstimate(mapRect, current, flowDir);

        for (var i = 0; i < TributaryTraceMaxSteps; i++)
        {
            var toJoin = joinPoint - current;
            var toJoinMag = toJoin.magnitude;
            if (toJoinMag < step * 0.8f)
                break;

            var direction = toJoin / Mathf.Max(Epsilon, toJoinMag);
            var perpendicular = new Vector2(-direction.y, direction.x);
            var noise = (NoiseUtil.Sample2D(meanderNoise, current.x * 0.05f + seed * 0.0017f, current.y * 0.05f + i * 0.033f, seed + 19) * 2f - 1f) * sideNoiseScale;
            direction = (direction + perpendicular * noise).normalized;
            var next = ClampToRect(current + direction * step, inset);

            var nextHeight = TerrainHeightEstimate(mapRect, next, flowDir);
            if (nextHeight >= currentHeight - Epsilon)
                next = PickLowerStep(current, direction, step, inset, mapRect, flowDir, currentHeight);

            if ((next - current).sqrMagnitude < grid.cellSize * grid.cellSize * 0.08f)
                return null;

            current = next;
            currentHeight = TerrainHeightEstimate(mapRect, current, flowDir);
            points.Add(current);

            var distToRiver = DistanceToPolylineWithT(mainRiver, current, out _);
            consecutiveNearRiver = distToRiver <= nearRiverBand ? consecutiveNearRiver + 1 : 0;
            if (consecutiveNearRiver > TributaryParallelAbortSteps)
                return null;

            if (distToRiver < riverJoinDistance)
            {
                var riverSnap = ClosestPointOnPolyline(mainRiver, current);
                points.Add(riverSnap);
                break;
            }
        }

        if (points.Count < 3)
            return null;

        var last = points[points.Count - 1];
        if (DistanceToPolylineWithT(mainRiver, last, out _) >= riverJoinDistance)
            return null;

        if (!IsTributaryValid(settings, mainRiver, points))
            return null;

        controlStations = points.Count;
        return Chaikin(points, 1);
    }

    private static bool IsTributaryValid(SavannaRiverSettingsSO settings, List<Vector2> mainRiver, List<Vector2> tributary)
    {
        if (mainRiver == null || tributary == null || mainRiver.Count < 2 || tributary.Count < 3)
            return false;

        if (Mathf.Clamp(settings.TributaryWidthFactor, 0.2f, 0.9f) >= 1f - Epsilon)
            return false;

        var mainLength = PolylineLength(mainRiver);
        var tribLength = PolylineLength(tributary);
        if (mainLength <= Epsilon || tribLength <= Epsilon)
            return false;

        if (tribLength >= mainLength * 0.82f)
            return false;

        var joinPoint = tributary[tributary.Count - 1];
        DistanceToPolylineWithT(mainRiver, joinPoint, out var joinT);
        var mainJoin = SamplePolyline(mainRiver, joinT, out var mainTangent);
        var tribTangent = (tributary[tributary.Count - 1] - tributary[tributary.Count - 2]).normalized;
        if (tribTangent.sqrMagnitude < Epsilon || mainTangent.sqrMagnitude < Epsilon)
            return false;

        var joinAngle = Vector2.Angle(tribTangent, mainTangent);
        if (joinAngle < 22f || joinAngle > 158f)
            return false;

        return Vector2.Distance(joinPoint, mainJoin) <= Mathf.Max(0.5f, settings.riverWidth * 0.65f);
    }

    private static bool TryFindTributaryStart(SavannaRiverSettingsSO settings, WorldGridSpec grid, Rect mapRect, List<Vector2> mainRiver, Vector2 joinPoint, Vector2 sideDir, Vector2 flowDir, int seed, out Vector2 start)
    {
        start = Vector2.zero;
        var minDistanceToRiver = settings.valleyWidth * 2f;
        var joinHeight = TerrainHeightEstimate(mapRect, joinPoint, flowDir);
        var outward = sideDir.sqrMagnitude > Epsilon ? sideDir.normalized : Vector2.right;
        var inset = InsetRect(mapRect, grid.cellSize);

        for (var attempt = 0; attempt < TributaryStartSearchAttempts; attempt++)
        {
            var radiusLerp = Mathf.Lerp(2f, 3.8f, attempt / (float)Mathf.Max(1, TributaryStartSearchAttempts - 1));
            var radialDist = settings.valleyWidth * radiusLerp;
            var jitter = (NoiseUtil.Sample2D(settings.HeightNoise, attempt * 0.17f + seed * 0.0011f, seed * 0.013f, seed + 33) * 2f - 1f) * 0.6f;
            var rotated = Rotate(outward, jitter * 55f);
            var candidate = ClampToRect(joinPoint + rotated * radialDist, inset);

            var distanceToRiver = DistanceToPolylineWithT(mainRiver, candidate, out _);
            if (distanceToRiver <= minDistanceToRiver)
                continue;

            var startHeight = TerrainHeightEstimate(mapRect, candidate, flowDir);
            if (startHeight <= joinHeight + Epsilon)
                continue;

            start = candidate;
            return true;
        }

        return false;
    }

    private static Vector2 PickLowerStep(Vector2 current, Vector2 preferredDir, float step, Rect bounds, Rect mapRect, Vector2 flowDir, float currentHeight)
    {
        var candidates = new[]
        {
            preferredDir,
            Rotate(preferredDir, 25f),
            Rotate(preferredDir, -25f),
            Rotate(preferredDir, 50f),
            Rotate(preferredDir, -50f),
            new Vector2(0f, -1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f)
        };

        var best = current;
        var bestHeight = currentHeight;
        for (var i = 0; i < candidates.Length; i++)
        {
            var dir = candidates[i].normalized;
            if (dir.sqrMagnitude < Epsilon) continue;

            var sample = ClampToRect(current + dir * step, bounds);
            var sampleHeight = TerrainHeightEstimate(mapRect, sample, flowDir);
            if (sampleHeight < bestHeight)
            {
                best = sample;
                bestHeight = sampleHeight;
            }
        }

        return best;
    }

    private static float TerrainHeightEstimate(Rect mapRect, Vector2 point, Vector2 flowDir)
    {
        var nx = Mathf.InverseLerp(mapRect.xMin, mapRect.xMax, point.x);
        var ny = Mathf.InverseLerp(mapRect.yMin, mapRect.yMax, point.y);
        var local = new Vector2(nx * 2f - 1f, ny * 2f - 1f);
        return Vector2.Dot(local, flowDir.normalized) * 0.5f + 0.5f;
    }

    private static Vector2 ClosestPointOnPolyline(List<Vector2> points, Vector2 p)
    {
        if (points == null || points.Count == 0) return p;
        if (points.Count == 1) return points[0];

        var best = points[0];
        var bestDist = float.MaxValue;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < Epsilon) continue;

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            var proj = a + ab * t;
            var d = (p - proj).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = proj;
            }
        }

        return best;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        var rad = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private static void TryApplyCutoff(SavannaRiverSettingsSO settings, int seed, List<Vector2> points, out List<Vector2> oxbow, out List<int> removedIndices, out int cutoffApplied)
    {
        oxbow = null;
        removedIndices = new List<int>();
        cutoffApplied = 0;
        if (points == null || points.Count < 18) return;
        if (Mathf.Clamp01(settings.CutoffChance) <= 0f) return;

        var rand = Mathf.Abs(Mathf.Sin(seed * 0.0193f));
        if (rand > settings.CutoffChance) return;

        var step = 3;
        var mainLength = PolylineLength(points);
        var minIndexGap = Mathf.Max(10, points.Count / 8);
        var maxCloseDist = Vector2.Distance(points[0], points[points.Count - 1]) * 0.16f;
        var bestScore = float.MinValue;
        var bestI = -1;
        var bestJ = -1;

        for (var i = step; i < points.Count - step; i += step)
        {
            for (var j = i + minIndexGap; j < points.Count - step; j += step)
            {
                var d = Vector2.Distance(points[i], points[j]);
                if (d > maxCloseDist) continue;
                var alongLength = PolylineLength(points, i, j);
                if (alongLength <= Epsilon) continue;

                var bendScore = Mathf.Min(LocalBendStrength(points, i), LocalBendStrength(points, j));
                if (bendScore < 0.18f) continue;

                var shortcutRatio = d / alongLength;
                if (shortcutRatio > 0.72f) continue;

                var localRatio = alongLength / Mathf.Max(Epsilon, mainLength);
                if (localRatio < 0.08f || localRatio > 0.34f) continue;

                if (WouldCutoffSelfIntersect(points, i, j)) continue;

                var alongGap = (j - i) / (float)points.Count;
                var score = alongGap - d / Mathf.Max(Epsilon, maxCloseDist) + bendScore * 0.45f + (1f - shortcutRatio) * 0.35f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestI = i;
                    bestJ = j;
                }
            }
        }

        if (bestI < 0 || bestJ <= bestI + 2 || bestScore < 0.2f) return;

        oxbow = new List<Vector2>();
        for (var k = bestI; k <= bestJ; k++)
            oxbow.Add(points[k]);

        var rebuilt = new List<Vector2>(points.Count - (bestJ - bestI) + 1);
        for (var k = 0; k <= bestI; k++) rebuilt.Add(points[k]);
        rebuilt.Add(Vector2.Lerp(points[bestI], points[bestJ], 0.5f));
        for (var k = bestJ; k < points.Count; k++) rebuilt.Add(points[k]);

        for (var k = bestI + 1; k < bestJ; k++) removedIndices.Add(k);

        points.Clear();
        points.AddRange(rebuilt);
        cutoffApplied = 1;
    }

    private static float PolylineLength(List<Vector2> points, int startIndex = 0, int endIndex = -1)
    {
        if (points == null || points.Count < 2)
            return 0f;

        var start = Mathf.Clamp(startIndex, 0, points.Count - 2);
        var end = endIndex < 0 ? points.Count - 1 : Mathf.Clamp(endIndex, start + 1, points.Count - 1);
        var length = 0f;
        for (var i = start + 1; i <= end; i++)
            length += Vector2.Distance(points[i - 1], points[i]);
        return length;
    }

    private static float LocalBendStrength(List<Vector2> points, int index)
    {
        if (points == null || index <= 0 || index >= points.Count - 1)
            return 0f;

        var a = (points[index] - points[index - 1]).normalized;
        var b = (points[index + 1] - points[index]).normalized;
        if (a.sqrMagnitude < Epsilon || b.sqrMagnitude < Epsilon)
            return 0f;

        return Mathf.Clamp01(Vector2.Angle(a, b) / 180f);
    }

    private static bool WouldCutoffSelfIntersect(List<Vector2> points, int i, int j)
    {
        var a = points[i];
        var b = points[j];
        for (var k = 1; k < points.Count; k++)
        {
            if (k >= i - 1 && k <= j + 1)
                continue;

            if (SegmentsIntersect(a, b, points[k - 1], points[k]))
                return true;
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var r = a2 - a1;
        var s = b2 - b1;
        var denom = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(denom) <= Epsilon)
            return false;

        var uNumer = (b1.x - a1.x) * r.y - (b1.y - a1.y) * r.x;
        var tNumer = (b1.x - a1.x) * s.y - (b1.y - a1.y) * s.x;
        var t = tNumer / denom;
        var u = uNumer / denom;
        return t > Epsilon && t < 1f - Epsilon && u > Epsilon && u < 1f - Epsilon;
    }

    private static Vector2 SamplePolyline(List<Vector2> points, float t, out Vector2 tangent)
    {
        tangent = Vector2.right;
        if (points == null || points.Count == 0) return Vector2.zero;
        if (points.Count == 1) return points[0];

        var ft = Mathf.Clamp01(t) * (points.Count - 1);
        var i = Mathf.Clamp(Mathf.FloorToInt(ft), 0, points.Count - 2);
        var lt = ft - i;
        tangent = (points[i + 1] - points[i]).normalized;
        if (tangent.sqrMagnitude < Epsilon) tangent = Vector2.right;
        return Vector2.Lerp(points[i], points[i + 1], lt);
    }

    private static Vector2 EdgePointByNormal(Rect rect, Vector2 anchor, Vector2 dir, int seed)
    {
        var direction = DominantCardinal(dir);
        var spread = Mathf.Lerp(0.12f, 0.88f, Mathf.Abs(Mathf.Sin(seed * 0.027f)));

        if (Mathf.Abs(direction.x) > 0.5f)
        {
            var x = direction.x > 0f ? rect.xMax : rect.xMin;
            return new Vector2(x, Mathf.Lerp(rect.yMin, rect.yMax, spread));
        }

        var y = direction.y > 0f ? rect.yMax : rect.yMin;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, spread), y);
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
