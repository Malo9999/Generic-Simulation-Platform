using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private readonly struct RegionField
    {
        public RegionField(RegionSpec region, float x0, float x1, float y0, float y1)
        {
            this.region = region;
            this.x0 = Mathf.Min(x0, x1);
            this.x1 = Mathf.Max(x0, x1);
            this.y0 = Mathf.Min(y0, y1);
            this.y1 = Mathf.Max(y0, y1);
            cx = (this.x0 + this.x1) * 0.5f;
            cy = (this.y0 + this.y1) * 0.5f;
            ex = Mathf.Max(4f, (this.x1 - this.x0) * 0.5f);
            ey = Mathf.Max(4f, (this.y1 - this.y0) * 0.5f);
        }

        public readonly RegionSpec region;
        public readonly float x0;
        public readonly float x1;
        public readonly float y0;
        public readonly float y1;
        public readonly float cx;
        public readonly float cy;
        public readonly float ex;
        public readonly float ey;
    }

    private const string SortingLayerDefault = "Default";
    private const int TerrainBaseOrder = -200;
    private const int RegionOverlayOrder = -198;
    private const int FloodplainOrder = -190;
    private const int PointBarsOrder = -184;
    private const int CutBanksOrder = -183;
    private const int BankOrder = -181;
    private const int PermanentWaterOrder = -180;
    private const int SeasonalWaterOrder = -175;
    private const int KopjesOverlayOrder = -170;
    private const int DebugOverlayOrder = -10;

    private Transform mapRoot;
    private SpriteRenderer terrainSR;
    private SpriteRenderer regionSR;
    private SpriteRenderer kopjesSR;
    private SpriteRenderer floodplainSR;
    private SpriteRenderer pointBarsSR;
    private SpriteRenderer cutBanksSR;
    private SpriteRenderer bankSR;
    private SpriteRenderer permanentWaterSR;
    private SpriteRenderer seasonalWaterSR;
    private int mainRiverRasterSamples;
    private int grumetiRasterSamples;
    private bool[] permanentWaterMask;
    private int permanentWaterMaskWidth;
    private int permanentWaterMaskHeight;
    private readonly List<Vector2> mainRiverWaterNodes = new();

    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();
    private readonly List<CrossingNodeData> crossingNodes = new();
    private readonly List<KopjesNodeData> kopjesNodes = new();

    public readonly List<CrossingNodeData> crossingsMain = new();
    public readonly List<CrossingNodeData> crossingsGrumeti = new();
    public readonly List<PoolNodeData> pools = new();
    public readonly List<KopjesNodeData> kopjes = new();
    public readonly List<WetlandNodeData> wetlands = new();

    public IReadOnlyList<Vector2> WaterNodes => waterNodes;
    public IReadOnlyList<Vector2> ShadeNodes => shadeNodes;
    public IReadOnlyList<CrossingNodeData> CrossingNodes => crossingNodes;
    public IReadOnlyList<KopjesNodeData> KopjesNodes => kopjesNodes;

    public void Build(Transform worldObjectsRoot, ScenarioConfig config, SerengetiMapSpec spec, float halfW, float halfH)
    {
        Clear();

        if (worldObjectsRoot == null)
        {
            throw new InvalidOperationException("World objects root is required for map build.");
        }

        var existing = worldObjectsRoot.Find("PredatorPreyDocuMap");
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        mapRoot = new GameObject("PredatorPreyDocuMap").transform;
        mapRoot.SetParent(worldObjectsRoot, false);

        ValidateSpecConformance(spec, halfW, halfH);
        BuildWater(spec, halfW, halfH);
        BuildTerrain(spec, halfW, halfH);
        BuildRegions(spec, halfW, halfH);
        BuildKopjes(spec, halfW, halfH);
        BuildDebugOverlays(spec, config, halfW, halfH);

        var mainRiverControlPoints = spec.water.mainRiver.centerline.Count;
        var grumetiControlPoints = spec.water.grumeti.centerline.Count;
        var overlaysEnabled = config?.predatorPreyDocu != null && config.predatorPreyDocu.debugShowMapOverlays;
        Debug.Log($"[SerengetiConformance] mapId={spec.mapId} mainRiver(controlPts={mainRiverControlPoints} raster={mainRiverRasterSamples}) grumeti(controlPts={grumetiControlPoints} raster={grumetiRasterSamples}) regions={spec.regions.Count} pools={spec.water.pools.Count} kopjes={spec.landmarks.kopjes.Count} wetlands={spec.landmarks.wetlands.Count} debugOverlays={(overlaysEnabled ? "ON" : "OFF")}");
    }

    public void UpdateSeasonVisuals(float seasonalPresence01)
    {
        var floodAlpha = Mathf.Clamp01(seasonalPresence01);
        if (floodplainSR != null)
        {
            floodplainSR.color = new Color(1f, 1f, 1f, floodAlpha);
        }

        if (seasonalWaterSR != null)
        {
            seasonalWaterSR.color = new Color(1f, 1f, 1f, Mathf.Clamp01(seasonalPresence01));
        }
    }

    public void Clear()
    {
        if (mapRoot != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mapRoot.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mapRoot.gameObject);
            }
        }

        mapRoot = null;
        terrainSR = null;
        regionSR = null;
        kopjesSR = null;
        floodplainSR = null;
        pointBarsSR = null;
        cutBanksSR = null;
        bankSR = null;
        permanentWaterSR = null;
        seasonalWaterSR = null;
        mainRiverRasterSamples = 0;
        grumetiRasterSamples = 0;
        permanentWaterMask = null;
        permanentWaterMaskWidth = 0;
        permanentWaterMaskHeight = 0;
        mainRiverWaterNodes.Clear();

        waterNodes.Clear();
        shadeNodes.Clear();
        crossingNodes.Clear();
        kopjesNodes.Clear();
        crossingsMain.Clear();
        crossingsGrumeti.Clear();
        pools.Clear();
        kopjes.Clear();
        wetlands.Clear();
    }

    private static void ValidateSpecConformance(SerengetiMapSpec spec, float halfW, float halfH)
    {
        foreach (var region in spec.regions)
        {
            WarnIfNormalizedOutsideRange(region.shape.xMin, $"regions.{region.id}.shape.xMin");
            WarnIfNormalizedOutsideRange(region.shape.xMax, $"regions.{region.id}.shape.xMax");
            WarnIfNormalizedOutsideRange(region.shape.yMin, $"regions.{region.id}.shape.yMin");
            WarnIfNormalizedOutsideRange(region.shape.yMax, $"regions.{region.id}.shape.yMax");
            WarnIfWorldOutsideBounds(ToWorld(region.shape.xMin, region.shape.yMin, halfW, halfH), halfW, halfH, $"regions.{region.id}.shape.min");
            WarnIfWorldOutsideBounds(ToWorld(region.shape.xMax, region.shape.yMax, halfW, halfH), halfW, halfH, $"regions.{region.id}.shape.max");
        }
    }

    private void BuildTerrain(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 3f, 1_800_000);
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        var c0 = new Vector3(173f, 152f, 96f);
        var dryToneA = c0 + new Vector3(6f, 3f, 1f);
        var dryToneB = c0 - new Vector3(7f, 5f, 3f);
        var moistGrass = new Vector3(154f, 156f, 102f);
        var seedSalt = Mathf.Abs(StableHash.Hash32(spec.mapId ?? "serengeti_v1"));
        var moistureFalloffWorld = 180f;
        var moistureCellSize = moistureFalloffWorld * 0.75f;
        var moistureSources = BuildMoistureSources();
        var moistureGrid = BuildPointGrid(moistureSources, halfW, halfH, moistureCellSize, out var cellsX, out var cellsY);
        Debug.Log($"[SerengetiTerrain] mapId={spec.mapId} terrainTex={w}x{h}@{ppu:0.###} waterNodes={moistureSources.Count} moistureFalloff={moistureFalloffWorld:0.#} grid={cellsX}x{cellsY}");

        for (var y = 0; y < h; y++)
        {
            var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, h - 1f));
            for (var x = 0; x < w; x++)
            {
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, w - 1f));
                var low = Noise2D(wx * 0.0009f + 37f, wy * 0.0009f + 53f, seedSalt) * 0.6f;
                var mid = Noise2D(wx * 0.0024f + 11f, wy * 0.0024f + 23f, seedSalt) * 0.3f;
                var high = Noise2D(wx * 0.0058f + 71f, wy * 0.0058f + 89f, seedSalt) * 0.1f;
                var macroPatch = Noise2D(wx * 0.0003f + 5f, wy * 0.0003f + 13f, seedSalt);
                var fbm = low + mid + high;
                var brightness = 1f + fbm * 0.065f;
                var patchBlend = Mathf.Clamp01((macroPatch + 1f) * 0.5f);
                var macroDryColor = Vector3.Lerp(dryToneA, dryToneB, patchBlend);
                var dryColor = macroDryColor * brightness;
                var nearestWater = ApproxNearestPointDistance(wx, wy, moistureSources, moistureGrid, halfW, halfH, moistureCellSize, cellsX, cellsY);
                var moisture = Mathf.Exp(-nearestWater / Mathf.Max(1f, moistureFalloffWorld));
                var moistureStrength = Mathf.Clamp01(Mathf.Pow(moisture, 0.65f) * 0.45f);
                var wetColor = moistGrass * (0.985f + fbm * 0.015f);
                var finalColor = Vector3.Lerp(dryColor, wetColor, moistureStrength);
                var r = Mathf.Clamp(Mathf.RoundToInt(finalColor.x), 0, 255);
                var g = Mathf.Clamp(Mathf.RoundToInt(finalColor.y), 0, 255);
                var b = Mathf.Clamp(Mathf.RoundToInt(finalColor.z), 0, 255);
                px[x + y * w] = new Color32((byte)r, (byte)g, (byte)b, 255);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        terrainSR = CreateSprite("TerrainBaseTexture", tex, ppu, TerrainBaseOrder);
        Debug.Log($"[SerengetiConformance] TerrainTex={w}x{h}@{ppu:0.###}PPU");
    }

    private void BuildRegions(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var ppu = PickBoundedPpu(halfW * 2f, halfH * 2f, 2f, 3f, 1_800_000);
        var w = Mathf.Max(256, Mathf.RoundToInt(halfW * 2f * ppu));
        var h = Mathf.Max(256, Mathf.RoundToInt(halfH * 2f * ppu));
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        var seedSalt = Mathf.Abs(StableHash.Hash32(spec.mapId ?? "serengeti_v1"));
        var warpAmpWorld = 78f;
        var warpFreq = 0.0023f;
        var microWarpAmpWorld = 14f;
        var microWarpFreq = 0.01f;
        var bonusPadding = 45f;
        var bonusFalloff = 220f;
        var bonusScale = 0.46f;
        var weightPower = 1.9f;
        var alphaMax = 0f;
        var regions = new List<RegionField>();
        foreach (var region in spec.regions)
        {
            regions.Add(new RegionField(
                region,
                Mathf.Lerp(-halfW, halfW, region.shape.xMin),
                Mathf.Lerp(-halfW, halfW, region.shape.xMax),
                Mathf.Lerp(-halfH, halfH, region.shape.yMin),
                Mathf.Lerp(-halfH, halfH, region.shape.yMax)));
        }

        var weights = new float[regions.Count];
        for (var y = 0; y < h; y++)
        {
            var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, h - 1f));
            for (var x = 0; x < w; x++)
            {
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, w - 1f));

                var warpX = Noise2D(wx * warpFreq + 11f, wy * warpFreq + 17f, seedSalt) * warpAmpWorld +
                            Noise2D(wx * microWarpFreq + 101f, wy * microWarpFreq + 131f, seedSalt) * microWarpAmpWorld;
                var warpY = Noise2D(wx * warpFreq + 23f, wy * warpFreq + 29f, seedSalt) * warpAmpWorld +
                            Noise2D(wx * microWarpFreq + 149f, wy * microWarpFreq + 173f, seedSalt) * microWarpAmpWorld;
                var warpedX = wx + warpX;
                var warpedY = wy + warpY;

                var sumWeights = 0f;
                var bestInfluence = float.NegativeInfinity;
                var secondInfluence = float.NegativeInfinity;
                var nearest = 0;
                var nearestD2 = float.PositiveInfinity;
                var alphaWeighted = 0f;
                var rWeighted = 0f;
                var gWeighted = 0f;
                var bWeighted = 0f;

                for (var i = 0; i < regions.Count; i++)
                {
                    var entry = regions[i];
                    var influence = ComputeRegionInfluence(entry, warpedX, warpedY, seedSalt, x, y, bonusPadding, bonusFalloff, bonusScale);
                    if (influence > bestInfluence)
                    {
                        secondInfluence = bestInfluence;
                        bestInfluence = influence;
                    }
                    else if (influence > secondInfluence)
                    {
                        secondInfluence = influence;
                    }

                    var dxC = warpedX - entry.cx;
                    var dyC = warpedY - entry.cy;
                    var d2 = dxC * dxC + dyC * dyC;
                    if (d2 < nearestD2)
                    {
                        nearestD2 = d2;
                        nearest = i;
                    }

                    var weight = influence > 0f ? Mathf.Pow(influence, weightPower) : 0f;
                    weights[i] = weight;
                    sumWeights += weight;
                }

                if (sumWeights < 0.0001f && regions.Count > 0)
                {
                    for (var i = 0; i < regions.Count; i++) weights[i] = 0f;
                    weights[nearest] = 1f;
                    sumWeights = 1f;
                }

                for (var i = 0; i < regions.Count; i++)
                {
                    var entry = regions[i];
                    var weight = weights[i];
                    if (weight <= 0f) continue;
                    var tint = RegionTintWithTraits(entry.region);
                    rWeighted += tint.r * weight;
                    gWeighted += tint.g * weight;
                    bWeighted += tint.b * weight;
                    alphaWeighted += RegionBaseAlpha(entry.region) * weight;
                }

                var inv = sumWeights > 0f ? 1f / sumWeights : 0f;
                var boundaryFactor = 0.8f + 0.2f * Mathf.SmoothStep(0f, 0.35f, bestInfluence - secondInfluence);
                var alpha = alphaWeighted * inv * boundaryFactor;
                alpha *= 1f + (Hash01(x, y, seedSalt ^ 433) - 0.5f) * 0.06f;
                alpha = Mathf.Clamp(alpha, 20f, 75f);
                alphaMax = Mathf.Max(alphaMax, alpha);

                px[x + y * w] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(rWeighted * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(gWeighted * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(bWeighted * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        regionSR = CreateSprite("RegionOverlayTexture", tex, ppu, RegionOverlayOrder);
        Debug.Log($"[SerengetiConformance] Regions: alphaMax={alphaMax:0.##} warpAmp={warpAmpWorld:0.#} warpFreq={warpFreq:0.####} blendP={weightPower:0.##} PPU={ppu:0.###} tex={w}x{h}");
    }

    private void BuildWater(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 4f, 3_500_000);
        var floodPx = new Color32[w * h];
        var bankPx = new Color32[w * h];
        var permPx = new Color32[w * h];
        var seasonPx = new Color32[w * h];
        var pointBarPx = new Color32[w * h];
        var cutBankPx = new Color32[w * h];

        mainRiverRasterSamples = PaintMainRiverScanline(spec.water.mainRiver, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx, out var sampledMain);
        var mainWaterMask = new bool[permPx.Length];
        for (var i = 0; i < permPx.Length; i++)
        {
            mainWaterMask[i] = permPx[i].a > 0;
        }

        grumetiRasterSamples = PaintGrumetiPolyline(spec.water.grumeti, spec.water.mainRiver, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx, mainWaterMask, spec.mapId, out var sampledGrumeti);
        BuildRiverGeomorphology(sampledMain, sampledGrumeti, spec.water.mainRiver, spec.water.grumeti, halfW, halfH, w, h, ppu, pointBarPx, cutBankPx);
        BuildOxbowLakes(sampledMain, spec.water.mainRiver, halfW, halfH, w, h, ppu, permPx, bankPx, spec.mapId);

        foreach (var pool in spec.water.pools)
        {
            WarnIfNormalizedOutsideRange(pool.x, "water.pools.x");
            WarnIfNormalizedOutsideRange(pool.y, "water.pools.y");
            var wp = ToWorld(pool.x, pool.y, halfW, halfH);
            WarnIfWorldOutsideBounds(wp, halfW, halfH, "water.pools");
            pools.Add(new PoolNodeData { id = pool.id, worldPos = wp, worldRadius = pool.radius, permanent = pool.permanent });
            waterNodes.Add(wp);
            shadeNodes.Add(wp);
            var center = WorldToPixel(wp.x, wp.y, halfW, halfH, w, h);
            var waterRadius = Mathf.Max(1, Mathf.RoundToInt(pool.radius * ppu * 0.5f));
            PaintDisc(permPx, w, h, center, waterRadius, new Color32(44, 126, 218, 220));
            PaintRing(bankPx, w, h, center, waterRadius + Mathf.Max(1, Mathf.RoundToInt(2.5f * ppu)), Mathf.Max(1, Mathf.RoundToInt(2f * ppu)), new Color32(66, 103, 58, 95));
        }

        foreach (var wetland in spec.landmarks.wetlands)
        {
            WarnIfNormalizedOutsideRange(wetland.x, "landmarks.wetlands.x");
            WarnIfNormalizedOutsideRange(wetland.y, "landmarks.wetlands.y");
            var wp = ToWorld(wetland.x, wetland.y, halfW, halfH);
            WarnIfWorldOutsideBounds(wp, halfW, halfH, "landmarks.wetlands");
            wetlands.Add(new WetlandNodeData { id = wetland.id, worldPos = wp, worldRadius = wetland.radius });
            PaintIrregularWetland(seasonPx, w, h, WorldToPixel(wp.x, wp.y, halfW, halfH, w, h), wetland.radius, ppu, wetland.id);
        }

        floodplainSR = CreateSprite("FloodplainOverlayTexture", ToTex(w, h, floodPx), ppu, FloodplainOrder);
        pointBarsSR = CreateSprite("PointBarsOverlay", ToTex(w, h, pointBarPx), ppu, PointBarsOrder);
        cutBanksSR = CreateSprite("CutBanksOverlay", ToTex(w, h, cutBankPx), ppu, CutBanksOrder);
        bankSR = CreateSprite("BankOverlayTexture", ToTex(w, h, bankPx), ppu, BankOrder);
        permanentWaterSR = CreateSprite("PermanentWaterOverlay", ToTex(w, h, permPx), ppu, PermanentWaterOrder);
        seasonalWaterSR = CreateSprite("SeasonalWaterOverlay", ToTex(w, h, seasonPx), ppu, SeasonalWaterOrder);
        permanentWaterMask = new bool[permPx.Length];
        for (var i = 0; i < permPx.Length; i++)
        {
            permanentWaterMask[i] = permPx[i].a > 0;
        }

        permanentWaterMaskWidth = w;
        permanentWaterMaskHeight = h;
        Debug.Log($"[SerengetiWaterStyle] bankPermanent=true floodSeasonal=true bankExtra={spec.water.mainRiver.bankExtra:0.##} floodExtra={spec.water.mainRiver.floodplainExtra:0.##}");
        Debug.Log($"[SerengetiConformance] WaterTex={w}x{h}@{ppu:0.###}PPU");
    }

    private int PaintMainRiverScanline(RiverSpec river, float halfW, float halfH, int texW, int texH, float ppu, Color32[] seasonalPx, Color32[] bankPx, Color32[] waterPx, out List<Vector2> sampledOut)
    {
        var worldPoints = new List<Vector2>();
        foreach (var p in river.centerline)
        {
            WarnIfNormalizedOutsideRange(p.x, $"water.{river.id}.centerline.x");
            WarnIfNormalizedOutsideRange(p.y, $"water.{river.id}.centerline.y");
            var world = ToWorld(p.x, p.y, halfW, halfH);
            WarnIfWorldOutsideBounds(world, halfW, halfH, $"water.{river.id}.centerline");
            worldPoints.Add(world);
        }

        var sampled = SampleCatmullRom(worldPoints, 60);
        sampled = UpsamplePolyline(sampled, 3);
        var lookup = BuildSplineYLookup(sampled);
        if (sampled.Count < 2 || !lookup.Valid)
        {
            sampledOut = sampled;
            return 0;
        }

        var floodCol = new Color32(108, 145, 92, 120);
        var bankCol = new Color32(73, 108, 61, 170);
        var waterCol = new Color32(35, 120, 220, 255);

        for (var py = 0; py < texH; py++)
        {
            var yWorld = Mathf.Lerp(-halfH, halfH, py / Mathf.Max(1f, texH - 1f));
            if (!lookup.TryEvalX(yWorld, out var xWorld, out var progress01))
            {
                continue;
            }

            var width = Mathf.Lerp(river.widthNorth, river.widthSouth, progress01);
            FillHorizontalSpan(seasonalPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt((width * 0.5f + river.floodplainExtra * 0.5f) * ppu), floodCol);
            FillHorizontalSpan(bankPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt((width * 0.5f + river.bankExtra) * ppu), bankCol);
            FillHorizontalSpan(waterPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt(width * 0.5f * ppu), waterCol);
        }

        for (var i = 0; i < sampled.Count; i += 40)
        {
            waterNodes.Add(sampled[i]);
            mainRiverWaterNodes.Add(sampled[i]);
        }

        var dest = crossingsMain;
        foreach (var c in river.crossings)
        {
            WarnIfNormalizedOutsideRange(c.x, $"water.{river.id}.crossings.x");
            WarnIfNormalizedOutsideRange(c.y, $"water.{river.id}.crossings.y");
            var pos = ToWorld(c.x, c.y, halfW, halfH);
            WarnIfWorldOutsideBounds(pos, halfW, halfH, $"water.{river.id}.crossings");
            var node = new CrossingNodeData { worldPos = pos, worldRadius = c.radius, crocRisk = c.crocRisk };
            crossingNodes.Add(node);
            dest.Add(node);
        }

        sampledOut = sampled;
        return sampled.Count;
    }

    private int PaintGrumetiPolyline(RiverSpec river, RiverSpec mainRiver, float halfW, float halfH, int texW, int texH, float ppu, Color32[] seasonalPx, Color32[] bankPx, Color32[] waterPx, bool[] mainWaterMask, string mapId, out List<Vector2> sampledOut)
    {
        var worldPoints = new List<Vector2>();
        foreach (var p in river.centerline)
        {
            WarnIfNormalizedOutsideRange(p.x, $"water.{river.id}.centerline.x");
            WarnIfNormalizedOutsideRange(p.y, $"water.{river.id}.centerline.y");
            var world = ToWorld(p.x, p.y, halfW, halfH);
            WarnIfWorldOutsideBounds(world, halfW, halfH, $"water.{river.id}.centerline");
            worldPoints.Add(world);
        }

        var sampled = SampleCatmullRom(worldPoints, 60);
        sampled = EnsureMinSampleCount(sampled, 400);
        if (sampled.Count < 2)
        {
            sampledOut = sampled;
            return 0;
        }

        sampled = ApplyMicroMeanderToPolyline(sampled, mapId, river.id, out var meanderAmp, out var meanderFreq);

        var mainWidthRef = (mainRiver.widthNorth + mainRiver.widthSouth) * 0.5f;
        var ratio = Mathf.Clamp(river.width / Mathf.Max(0.001f, mainWidthRef), 0.25f, 0.75f);
        var bankExtra = Mathf.Max(2f, mainRiver.bankExtra * (0.75f * ratio + 0.25f));
        var floodExtra = Mathf.Max(18f, mainRiver.floodplainExtra * (0.55f * ratio + 0.10f));
        var floodCol = new Color32(108, 145, 92, (byte)Mathf.Clamp(Mathf.RoundToInt(120f * 0.75f), 0, 255));
        var bankCol = new Color32(73, 108, 61, (byte)Mathf.Clamp(Mathf.RoundToInt(170f * 0.85f), 0, 255));
        var waterCol = new Color32(35, 120, 220, 255);
        Debug.Log($"[SerengetiGrumetiStyle] width={river.width:0.##} ratio={ratio:0.###} bankExtra={bankExtra:0.##} floodExtra={floodExtra:0.##} bankA={bankCol.a} floodA={floodCol.a}");
        Debug.Log($"[SerengetiGrumetiDetail] microMeander amp={meanderAmp:0.##} freq={meanderFreq:0.##}");

        var reversalEps = 0.0001f;
        var xReversals = CountSignReversals(sampled, true, reversalEps);
        var yReversals = CountSignReversals(sampled, false, reversalEps);

        var xAxisAllowed = xReversals == 0;
        var yAxisAllowed = yReversals == 0;
        var mode = "Stamp";
        var xLookup = RiverXLookup.Invalid;
        var yLookup = RiverYLookup.Invalid;

        if (xAxisAllowed || yAxisAllowed)
        {
            var min = sampled[0];
            var max = sampled[0];
            for (var i = 1; i < sampled.Count; i++)
            {
                min = Vector2.Min(min, sampled[i]);
                max = Vector2.Max(max, sampled[i]);
            }

            var dx = Mathf.Abs(max.x - min.x);
            var dy = Mathf.Abs(max.y - min.y);
            if (xAxisAllowed && (!yAxisAllowed || dx >= dy))
            {
                xLookup = BuildSplineXLookup(sampled);
                if (xLookup.Valid)
                {
                    mode = "XScan";
                }
            }
            else if (yAxisAllowed)
            {
                yLookup = BuildSplineYLookup(sampled);
                if (yLookup.Valid)
                {
                    mode = "YScan";
                }
            }
        }

        var paintedSteps = 0;
        var confluenceHit = false;

        if (mode == "XScan")
        {
            for (var px = 0; px < texW; px++)
            {
                var xWorld = Mathf.Lerp(-halfW, halfW, px / Mathf.Max(1f, texW - 1f));
                if (!xLookup.TryEvalY(xWorld, out var yWorld, out var progress01)) continue;
                var width = river.width * (1f + Mathf.Sin(progress01 * Mathf.PI * 5f) * 0.1f);
                var waterHalfPx = Mathf.Max(1, Mathf.RoundToInt(width * 0.5f * ppu));
                var bankHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + bankExtra) * ppu));
                var floodHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + floodExtra * 0.5f) * ppu));
                var centerY = WorldToPixel(0f, yWorld, halfW, halfH, texW, texH).py;
                FillVerticalSpan(seasonalPx, texW, texH, px, centerY, floodHalfPx, floodCol);
                FillVerticalSpan(bankPx, texW, texH, px, centerY, bankHalfPx, bankCol);
                FillVerticalSpan(waterPx, texW, texH, px, centerY, waterHalfPx, waterCol);
                paintedSteps++;
            }
        }
        else if (mode == "YScan")
        {
            for (var py = 0; py < texH; py++)
            {
                var yWorld = Mathf.Lerp(-halfH, halfH, py / Mathf.Max(1f, texH - 1f));
                if (!yLookup.TryEvalX(yWorld, out var xWorld, out var progress01)) continue;
                var width = river.width * (1f + Mathf.Sin(progress01 * Mathf.PI * 5f) * 0.1f);
                var waterHalfPx = Mathf.Max(1, Mathf.RoundToInt(width * 0.5f * ppu));
                var bankHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + bankExtra) * ppu));
                var floodHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + floodExtra * 0.5f) * ppu));
                var centerX = WorldToPixelX(xWorld, halfW, texW);
                FillHorizontalSpan(seasonalPx, texW, texH, py, centerX, floodHalfPx, floodCol);
                FillHorizontalSpan(bankPx, texW, texH, py, centerX, bankHalfPx, bankCol);
                FillHorizontalSpan(waterPx, texW, texH, py, centerX, waterHalfPx, waterCol);
                paintedSteps++;
            }
        }
        else
        {
            var joinPoint = sampled[0];
            var hasJoinPoint = false;
            var stepWorld = Mathf.Max(0.5f, river.width * 0.5f * 0.6f);
            for (var i = 1; i < sampled.Count; i++)
            {
                var a = sampled[i - 1];
                var b = sampled[i];
                var segLen = Vector2.Distance(a, b);
                var subSteps = Mathf.Max(1, Mathf.CeilToInt(segLen / stepWorld));
                for (var s = 0; s <= subSteps; s++)
                {
                    var t = s / Mathf.Max(1f, subSteps);
                    var point = Vector2.Lerp(a, b, t);
                    var center = WorldToPixel(point.x, point.y, halfW, halfH, texW, texH);
                    var waterIndex = center.px + center.py * texW;
                    if (mainWaterMask[waterIndex])
                    {
                        confluenceHit = true;
                        if (hasJoinPoint)
                        {
                            var joinPixel = WorldToPixel(joinPoint.x, joinPoint.y, halfW, halfH, texW, texH);
                            var joinWidth = river.width * 0.7f;
                            var joinWaterHalfPx = Mathf.Max(1, Mathf.RoundToInt(joinWidth * 0.5f * ppu));
                            var joinBankHalfPx = Mathf.Max(1, Mathf.RoundToInt((joinWidth * 0.5f + bankExtra) * ppu));
                            var joinFloodHalfPx = Mathf.Max(1, Mathf.RoundToInt((joinWidth * 0.5f + floodExtra * 0.5f) * ppu));
                            PaintDisc(seasonalPx, texW, texH, joinPixel, joinFloodHalfPx, floodCol);
                            PaintDisc(bankPx, texW, texH, joinPixel, joinBankHalfPx, bankCol);
                            PaintDisc(waterPx, texW, texH, joinPixel, joinWaterHalfPx, waterCol);
                        }

                        goto StampDone;
                    }

                    var progress01 = (i - 1 + t) / Mathf.Max(1f, sampled.Count - 1f);
                    var width = river.width * (1f + Mathf.Sin(progress01 * Mathf.PI * 5f) * 0.1f);
                    var waterHalfPx = Mathf.Max(1, Mathf.RoundToInt(width * 0.5f * ppu));
                    var bankHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + bankExtra) * ppu));
                    var floodHalfPx = Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + floodExtra * 0.5f) * ppu));
                    PaintDisc(seasonalPx, texW, texH, center, floodHalfPx, floodCol);
                    PaintDisc(bankPx, texW, texH, center, bankHalfPx, bankCol);
                    PaintDisc(waterPx, texW, texH, center, waterHalfPx, waterCol);
                    joinPoint = point;
                    hasJoinPoint = true;
                    paintedSteps++;
                }
            }

StampDone:;
        }

        var startCap = sampled[0];
        var endCap = sampled[sampled.Count - 1];
        var addEndCap = mode != "Stamp" || !confluenceHit;
        var endCaps = addEndCap ? new[] { startCap, endCap } : new[] { startCap };
        for (var i = 0; i < endCaps.Length; i++)
        {
            var c = WorldToPixel(endCaps[i].x, endCaps[i].y, halfW, halfH, texW, texH);
            var waterHalfPx = Mathf.Max(1, Mathf.RoundToInt(river.width * 0.5f * ppu));
            var bankHalfPx = Mathf.Max(1, Mathf.RoundToInt((river.width * 0.5f + bankExtra) * ppu));
            var floodHalfPx = Mathf.Max(1, Mathf.RoundToInt((river.width * 0.5f + floodExtra * 0.5f) * ppu));
            PaintDisc(seasonalPx, texW, texH, c, floodHalfPx, floodCol);
            PaintDisc(bankPx, texW, texH, c, bankHalfPx, bankCol);
            PaintDisc(waterPx, texW, texH, c, waterHalfPx, waterCol);
        }

        for (var i = 0; i < sampled.Count; i += 30)
        {
            waterNodes.Add(sampled[i]);
        }

        foreach (var c in river.crossings)
        {
            WarnIfNormalizedOutsideRange(c.x, $"water.{river.id}.crossings.x");
            WarnIfNormalizedOutsideRange(c.y, $"water.{river.id}.crossings.y");
            var pos = ToWorld(c.x, c.y, halfW, halfH);
            WarnIfWorldOutsideBounds(pos, halfW, halfH, $"water.{river.id}.crossings");
            var node = new CrossingNodeData { worldPos = pos, worldRadius = c.radius, crocRisk = c.crocRisk };
            crossingNodes.Add(node);
            crossingsGrumeti.Add(node);
        }

        Debug.Log($"[SerengetiConfluence] hit={(confluenceHit ? "true" : "false")} mode={mode} paintedSteps={paintedSteps}");
        Debug.Log($"[SerengetiWater] grumeti mode={mode} xRev={xReversals} yRev={yReversals} samples={sampled.Count}");

        sampledOut = sampled;
        return sampled.Count;
    }

    private void BuildRiverGeomorphology(List<Vector2> sampledMain, List<Vector2> sampledGrumeti, RiverSpec mainRiver, RiverSpec grumetiRiver, float halfW, float halfH, int texW, int texH, float ppu, Color32[] pointBarPx, Color32[] cutBankPx)
    {
        var bars = 0;
        var cutBanks = 0;
        var maxCurvMain = 0f;
        var maxCurvGrumeti = 0f;
        var threshMain = 0f;
        var threshGrumeti = 0f;
        var strideMain = 0;
        var strideGrumeti = 0;
        PaintGeomorphForPolyline(sampledMain, mainRiver.widthNorth, mainRiver.widthSouth, mainRiver.bankExtra, 4, 26f, 22f, 311, pointBarPx, cutBankPx, ref bars, ref cutBanks, ref maxCurvMain, out strideMain, out threshMain, halfW, halfH, texW, texH, ppu);
        PaintGeomorphForPolyline(sampledGrumeti, grumetiRiver.width, grumetiRiver.width, Mathf.Max(2f, mainRiver.bankExtra * 0.55f), 5, 18f, 16f, 719, pointBarPx, cutBankPx, ref bars, ref cutBanks, ref maxCurvGrumeti, out strideGrumeti, out threshGrumeti, halfW, halfH, texW, texH, ppu);
        Debug.Log($"[SerengetiGeomorph] mainSamples={sampledMain.Count} grumetiSamples={sampledGrumeti.Count} maxCurvMain={maxCurvMain:0.####} maxCurvGrumeti={maxCurvGrumeti:0.####} threshMain={threshMain:0.####} threshGrumeti={threshGrumeti:0.####} stride={strideMain}/{strideGrumeti} bars={bars} cutbanks={cutBanks}");
    }

    private void PaintGeomorphForPolyline(List<Vector2> sampled, float widthStart, float widthEnd, float bankExtra, int step, float pointBarMaxRadius, float cutBankMaxRadius, int seedSalt, Color32[] pointBarPx, Color32[] cutBankPx, ref int bars, ref int cutBanks, ref float maxCurvature, out int strideUsed, out float curvatureThresholdUsed, float halfW, float halfH, int texW, int texH, float ppu)
    {
        strideUsed = 0;
        curvatureThresholdUsed = 0.0025f;
        if (sampled == null || sampled.Count < 3)
        {
            return;
        }

        var stride = Mathf.Clamp(Mathf.RoundToInt(sampled.Count * 0.01f), 3, 10);
        strideUsed = stride;
        if (sampled.Count <= stride * 2)
        {
            return;
        }

        var maxCurvLocal = 0f;
        for (var i = stride; i < sampled.Count - stride; i += Mathf.Max(3, step))
        {
            var prev = (sampled[i] - sampled[i - stride]).normalized;
            var next = (sampled[i + stride] - sampled[i]).normalized;
            if (prev.sqrMagnitude < 1e-5f || next.sqrMagnitude < 1e-5f)
            {
                continue;
            }

            var curvature = 1f - Mathf.Clamp(Vector2.Dot(prev, next), -1f, 1f);
            maxCurvLocal = Mathf.Max(maxCurvLocal, curvature);
        }

        var curvThresh = Mathf.Max(0.0025f, maxCurvLocal * 0.35f);
        curvatureThresholdUsed = curvThresh;
        maxCurvature = Mathf.Max(maxCurvature, maxCurvLocal);

        for (var i = stride; i < sampled.Count - stride; i += Mathf.Max(3, step))
        {
            var prev = (sampled[i] - sampled[i - stride]).normalized;
            var next = (sampled[i + stride] - sampled[i]).normalized;
            if (prev.sqrMagnitude < 1e-5f || next.sqrMagnitude < 1e-5f)
            {
                continue;
            }

            var curvature = 1f - Mathf.Clamp(Vector2.Dot(prev, next), -1f, 1f);
            maxCurvature = Mathf.Max(maxCurvature, curvature);
            if (curvature <= curvThresh)
            {
                continue;
            }

            var turnSign = Mathf.Sign(prev.x * next.y - prev.y * next.x);
            if (Mathf.Abs(turnSign) < 0.01f)
            {
                continue;
            }

            var tangent = (prev + next).normalized;
            if (tangent.sqrMagnitude < 1e-5f)
            {
                tangent = prev;
            }

            var normal = new Vector2(-tangent.y, tangent.x);
            var insideSide = -turnSign * normal;
            var outsideSide = turnSign * normal;
            var progress = i / Mathf.Max(1f, sampled.Count - 1f);
            var riverWidth = Mathf.Lerp(widthStart, widthEnd, progress);
            var jitter = (Hash01(seedSalt, i, 991) - 0.5f) * 4f;
            var insideOffset = riverWidth * 0.45f + bankExtra * 0.6f + jitter;
            var outsideOffset = riverWidth * 0.50f + bankExtra * 0.7f - jitter;
            var strength = Mathf.Clamp01(curvature * 3f);
            var pointBarRadiusWorld = Mathf.Lerp(12f, pointBarMaxRadius, strength);
            var cutBankRadiusWorld = Mathf.Lerp(10f, cutBankMaxRadius, strength);

            var insideCenter = sampled[i] + insideSide * insideOffset;
            var outsideCenter = sampled[i] + outsideSide * outsideOffset;
            PaintDisc(pointBarPx, texW, texH, WorldToPixel(insideCenter.x, insideCenter.y, halfW, halfH, texW, texH), Mathf.Max(1, Mathf.RoundToInt(pointBarRadiusWorld * ppu * 0.5f)), new Color32(199, 170, 114, (byte)Mathf.RoundToInt(Mathf.Lerp(92f, 140f, strength))));
            PaintDisc(cutBankPx, texW, texH, WorldToPixel(outsideCenter.x, outsideCenter.y, halfW, halfH, texW, texH), Mathf.Max(1, Mathf.RoundToInt(cutBankRadiusWorld * ppu * 0.5f)), new Color32(82, 93, 64, (byte)Mathf.RoundToInt(Mathf.Lerp(112f, 170f, strength))));
            bars++;
            cutBanks++;
        }
    }

    private void BuildOxbowLakes(List<Vector2> sampledMain, RiverSpec mainRiver, float halfW, float halfH, int texW, int texH, float ppu, Color32[] permPx, Color32[] bankPx, string mapId)
    {
        var peaks = new List<(int index, float curvature)>();
        var maxCurv = 0f;
        var peakThresh = 0.004f;
        var bestCurv = 0f;
        var bestIndex = -1;
        var stride = Mathf.Clamp(Mathf.RoundToInt(sampledMain.Count * 0.01f), 3, 10);
        if (sampledMain.Count <= stride * 2)
        {
            Debug.Log($"[SerengetiOxbow] maxCurv={maxCurv:0.####} peakThresh={peakThresh:0.####} stride={stride} count=0 centers=()");
            return;
        }

        for (var i = stride; i < sampledMain.Count - stride; i++)
        {
            var prev = (sampledMain[i] - sampledMain[i - stride]).normalized;
            var next = (sampledMain[i + stride] - sampledMain[i]).normalized;
            if (prev.sqrMagnitude < 1e-5f || next.sqrMagnitude < 1e-5f) continue;
            var curvature = 1f - Mathf.Clamp(Vector2.Dot(prev, next), -1f, 1f);
            maxCurv = Mathf.Max(maxCurv, curvature);
            if (curvature > bestCurv)
            {
                bestCurv = curvature;
                bestIndex = i;
            }
        }

        peakThresh = Mathf.Max(0.004f, maxCurv * 0.55f);

        for (var i = stride; i < sampledMain.Count - stride; i++)
        {
            var prev = (sampledMain[i] - sampledMain[i - stride]).normalized;
            var next = (sampledMain[i + stride] - sampledMain[i]).normalized;
            if (prev.sqrMagnitude < 1e-5f || next.sqrMagnitude < 1e-5f) continue;
            var curvature = 1f - Mathf.Clamp(Vector2.Dot(prev, next), -1f, 1f);
            if (curvature >= peakThresh)
            {
                peaks.Add((i, curvature));
            }
        }

        if (peaks.Count == 0 && bestCurv > 0f && bestIndex >= 0)
        {
            peaks.Add((bestIndex, bestCurv));
        }

        peaks.Sort((a, b) => b.curvature.CompareTo(a.curvature));
        var selectedCenters = new List<Vector2>(2);
        var seed = Mathf.Abs(StableHash.Hash32($"{mapId}:oxbow"));
        foreach (var peak in peaks)
        {
            if (selectedCenters.Count >= 2)
            {
                break;
            }

            TryPlaceOxbowPeak(peak, sampledMain, stride, mainRiver, halfW, halfH, texW, texH, ppu, permPx, bankPx, seed, selectedCenters, enforceSpacing: true);
        }

        if (peaks.Count > 0 && selectedCenters.Count == 0)
        {
            TryPlaceOxbowPeak(peaks[0], sampledMain, stride, mainRiver, halfW, halfH, texW, texH, ppu, permPx, bankPx, seed, selectedCenters, enforceSpacing: false);
        }

        if (selectedCenters.Count == 0)
        {
            Debug.Log($"[SerengetiOxbow] maxCurv={maxCurv:0.####} peakThresh={peakThresh:0.####} stride={stride} count=0 centers=()");
            return;
        }

        var centersText = string.Empty;
        for (var i = 0; i < selectedCenters.Count; i++)
        {
            if (i > 0) centersText += ";";
            centersText += $"{selectedCenters[i].x:0.#},{selectedCenters[i].y:0.#}";
        }

        Debug.Log($"[SerengetiOxbow] maxCurv={maxCurv:0.####} peakThresh={peakThresh:0.####} stride={stride} count={selectedCenters.Count} centers=({centersText})");
    }

    private bool TryPlaceOxbowPeak((int index, float curvature) peak, List<Vector2> sampledMain, int stride, RiverSpec mainRiver, float halfW, float halfH, int texW, int texH, float ppu, Color32[] permPx, Color32[] bankPx, int seed, List<Vector2> selectedCenters, bool enforceSpacing)
    {
        var i = peak.index;
        var prev = (sampledMain[i] - sampledMain[i - stride]).normalized;
        var next = (sampledMain[i + stride] - sampledMain[i]).normalized;
        var turnSign = Mathf.Sign(prev.x * next.y - prev.y * next.x);
        if (Mathf.Abs(turnSign) < 0.01f) return false;
        var tangent = (prev + next).normalized;
        if (tangent.sqrMagnitude < 1e-5f) tangent = prev;
        var normal = new Vector2(-tangent.y, tangent.x);
        var insideSide = -turnSign * normal;
        var center = sampledMain[i] + insideSide * ((mainRiver.widthNorth + mainRiver.widthSouth) * 0.5f * 1.2f + 40f);
        if (center.x < -halfW || center.x > halfW || center.y < -halfH || center.y > halfH)
        {
            return false;
        }

        if (enforceSpacing)
        {
            for (var c = 0; c < selectedCenters.Count; c++)
            {
                if (Vector2.Distance(center, selectedCenters[c]) < 120f)
                {
                    return false;
                }
            }

            if (IsNearExistingWaterNode(center, sampledMain[i], 70f))
            {
                return false;
            }
        }

        var strength = Mathf.Clamp01(peak.curvature * 3f);
        var rWorld = Mathf.Lerp(22f, 40f, strength) + (Hash01(seed, i, 147) - 0.5f) * 2f;
        if (WouldOverlapPermanentWaterHeavily(center, rWorld, halfW, halfH, texW, texH, ppu, permPx))
        {
            return false;
        }

        var cpx = WorldToPixel(center.x, center.y, halfW, halfH, texW, texH);
        var waterRadius = Mathf.Max(1, Mathf.RoundToInt(rWorld * ppu * 0.5f));
        PaintDisc(permPx, texW, texH, cpx, waterRadius, new Color32(44, 126, 218, 220));
        PaintRing(bankPx, texW, texH, cpx, waterRadius + Mathf.Max(1, Mathf.RoundToInt(2.5f * ppu)), Mathf.Max(1, Mathf.RoundToInt(2f * ppu)), new Color32(66, 103, 58, 95));
        waterNodes.Add(center);
        selectedCenters.Add(center);
        return true;
    }

    private bool IsNearExistingWaterNode(Vector2 candidate, Vector2 localRiverPoint, float distance)
    {
        var minD2 = distance * distance;
        for (var i = 0; i < waterNodes.Count; i++)
        {
            var node = waterNodes[i];
            if (Vector2.Distance(node, localRiverPoint) < 40f)
            {
                continue;
            }

            if ((node - candidate).sqrMagnitude <= minD2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool WouldOverlapPermanentWaterHeavily(Vector2 center, float radiusWorld, float halfW, float halfH, int texW, int texH, float ppu, Color32[] permPx)
    {
        var c = WorldToPixel(center.x, center.y, halfW, halfH, texW, texH);
        var r = Mathf.Max(1, Mathf.RoundToInt(radiusWorld * ppu * 0.5f));
        var total = 0;
        var overlap = 0;
        var r2 = r * r;
        for (var y = c.py - r; y <= c.py + r; y++)
        {
            if (y < 0 || y >= texH) continue;
            for (var x = c.px - r; x <= c.px + r; x++)
            {
                if (x < 0 || x >= texW) continue;
                var dx = x - c.px;
                var dy = y - c.py;
                if (dx * dx + dy * dy > r2) continue;
                total++;
                if (permPx[x + y * texW].a > 10) overlap++;
            }
        }

        if (total <= 0)
        {
            return true;
        }

        return overlap / (float)total > 0.45f;
    }

    private static List<Vector2> ApplyMicroMeanderToPolyline(List<Vector2> sampled, string mapId, string riverId, out float amp, out float freq)
    {
        var seed = Mathf.Abs(StableHash.Hash32($"{mapId}:{riverId}:micro_meander"));
        amp = Mathf.Lerp(6f, 14f, Hash01(seed, 17, 131));
        freq = Mathf.Lerp(6f, 10f, Hash01(seed, 29, 163));
        var phase = Hash01(seed, 41, 197) * Mathf.PI * 2f;
        var meandered = new List<Vector2>(sampled.Count);
        for (var i = 0; i < sampled.Count; i++)
        {
            var prev = sampled[Mathf.Max(0, i - 1)];
            var next = sampled[Mathf.Min(sampled.Count - 1, i + 1)];
            var tangent = (next - prev).normalized;
            if (tangent.sqrMagnitude < 1e-5f)
            {
                meandered.Add(sampled[i]);
                continue;
            }

            var normal = new Vector2(-tangent.y, tangent.x);
            var progress = i / Mathf.Max(1f, sampled.Count - 1f);
            var taper = Mathf.Sin(progress * Mathf.PI);
            var offset = Mathf.Sin(progress * freq * Mathf.PI * 2f + phase) * amp * taper;
            meandered.Add(sampled[i] + normal * offset);
        }

        return meandered;
    }

    private static int CountSignReversals(IReadOnlyList<Vector2> points, bool axisX, float eps)
    {
        var reversals = 0;
        var previousSign = 0;
        for (var i = 1; i < points.Count; i++)
        {
            var delta = axisX ? points[i].x - points[i - 1].x : points[i].y - points[i - 1].y;
            if (Mathf.Abs(delta) < eps)
            {
                continue;
            }

            var sign = delta > 0f ? 1 : -1;
            if (previousSign != 0 && sign != previousSign)
            {
                reversals++;
            }

            previousSign = sign;
        }

        return reversals;
    }

    private void BuildKopjes(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var ppu = PickBoundedPpu(halfW * 2f, halfH * 2f, 2f, 3f, 1_800_000);
        var w = Mathf.Max(256, Mathf.RoundToInt(halfW * 2f * ppu));
        var h = Mathf.Max(256, Mathf.RoundToInt(halfH * 2f * ppu));
        var px = new Color32[w * h];
        var localWaterMask = BuildLocalWaterMask(halfW, halfH, w, h);
        var allNodes = new List<KopjesNodeData>(spec.landmarks.kopjes.Count + 20);
        foreach (var node in spec.landmarks.kopjes)
        {
            WarnIfNormalizedOutsideRange(node.x, "landmarks.kopjes.x");
            WarnIfNormalizedOutsideRange(node.y, "landmarks.kopjes.y");
            var world = ToWorld(node.x, node.y, halfW, halfH);
            WarnIfWorldOutsideBounds(world, halfW, halfH, "landmarks.kopjes");
            allNodes.Add(new KopjesNodeData(node.id, world, node.radius, node.cover));
        }

        var extraEast = GenerateExtraEastKopjes(spec, halfW, halfH, 10, 35f);
        allNodes.AddRange(extraEast);
        var extraRiverbank = GenerateRiverbankKopjes(halfW, halfH, 6, 10, 35f);
        allNodes.AddRange(extraRiverbank);

        foreach (var data in allNodes)
        {
            kopjes.Add(data);
            kopjesNodes.Add(data);
            shadeNodes.Add(data.position);
            var seed = Mathf.Abs(StableHash.Hash32(data.id ?? "kopjes"));
            var speckles = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(200f, 600f, Mathf.Clamp01(data.radius / 48f))), 200, 600);
            for (var i = 0; i < speckles; i++)
            {
                var ang = Hash01(seed, i, 83) * Mathf.PI * 2f;
                var mag = Mathf.Sqrt(Hash01(seed, i, 107)) * data.radius;
                var p = WorldToPixel(data.position.x + Mathf.Cos(ang) * mag, data.position.y + Mathf.Sin(ang) * mag, halfW, halfH, w, h);
                if (localWaterMask[p.px + p.py * w])
                {
                    continue;
                }

                var alpha = Mathf.RoundToInt(Mathf.Lerp(90f, 150f, Hash01(seed, i, 149)));
                px[p.px + p.py * w] = Blend(px[p.px + p.py * w], new Color32(63, 62, 58, (byte)alpha));
            }

            var blobCount = Mathf.Clamp(Mathf.RoundToInt(data.radius * 0.06f), 3, 8);
            for (var i = 0; i < blobCount; i++)
            {
                var ang = Hash01(seed, i, 211) * Mathf.PI * 2f;
                var mag = Mathf.Sqrt(Hash01(seed, i, 239)) * data.radius * 0.85f;
                var p = WorldToPixel(data.position.x + Mathf.Cos(ang) * mag, data.position.y + Mathf.Sin(ang) * mag, halfW, halfH, w, h);
                var rad = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1.2f, 2.8f, Hash01(seed, i, 271)) * ppu));
                PaintDiscMasked(px, localWaterMask, w, h, p, rad, new Color32(70, 69, 65, (byte)Mathf.RoundToInt(Mathf.Lerp(100f, 140f, Hash01(seed, i, 307)))));
            }
        }

        Debug.Log($"[SerengetiKopjes] base={spec.landmarks.kopjes.Count} extraEast={extraEast.Count} extraRiverbank={extraRiverbank.Count}");
        kopjesSR = CreateSprite("KopjesOverlayTexture", ToTex(w, h, px), ppu, KopjesOverlayOrder);
    }

    private List<KopjesNodeData> GenerateExtraEastKopjes(SerengetiMapSpec spec, float halfW, float halfH, int targetCount, float minWaterDistance)
    {
        var result = new List<KopjesNodeData>(targetCount);
        RegionSpec eastRegion = null;
        foreach (var region in spec.regions)
        {
            if (string.Equals(region.id, "east_kopjes", StringComparison.OrdinalIgnoreCase))
            {
                eastRegion = region;
                break;
            }
        }

        if (eastRegion == null)
        {
            return result;
        }

        var xMin = Mathf.Lerp(-halfW, halfW, eastRegion.shape.xMin);
        var xMax = Mathf.Lerp(-halfW, halfW, eastRegion.shape.xMax);
        var yMin = Mathf.Lerp(-halfH, halfH, eastRegion.shape.yMin);
        var yMax = Mathf.Lerp(-halfH, halfH, eastRegion.shape.yMax);
        var seed = Mathf.Abs(StableHash.Hash32($"{spec.mapId}:east_kopjes_extra"));
        for (var i = 0; i < targetCount * 8 && result.Count < targetCount; i++)
        {
            var nx = Hash01(seed, i, 37);
            var ny = Hash01(seed, i, 53);
            var world = new Vector2(Mathf.Lerp(xMin, xMax, nx), Mathf.Lerp(yMin, yMax, ny));
            if (IsNearPermanentWater(world, minWaterDistance, halfW, halfH))
            {
                continue;
            }

            var radius = Mathf.Lerp(18f, 34f, Hash01(seed, i, 71));
            var cover = Mathf.Lerp(0.75f, 0.90f, Hash01(seed, i, 89));
            result.Add(new KopjesNodeData($"east_kopjes_extra_{result.Count + 1}", world, radius, cover));
        }

        return result;
    }

    private List<KopjesNodeData> GenerateRiverbankKopjes(float halfW, float halfH, int stepEvery, int maxCount, float minWaterDistance)
    {
        var result = new List<KopjesNodeData>(maxCount);
        if (mainRiverWaterNodes.Count < 3)
        {
            return result;
        }

        for (var i = stepEvery; i < mainRiverWaterNodes.Count - 1 && result.Count < maxCount; i += stepEvery)
        {
            var prev = mainRiverWaterNodes[i - 1];
            var next = mainRiverWaterNodes[i + 1];
            var tangent = (next - prev).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            var normal = new Vector2(-tangent.y, tangent.x);
            var seed = Mathf.Abs(StableHash.Hash32($"riverbank_kopjes_{i}"));
            var side = Hash01(seed, i, 113) < 0.5f ? -1f : 1f;
            var offset = Mathf.Lerp(25f, 55f, Hash01(seed, i, 149));
            var candidate = mainRiverWaterNodes[i] + normal * (offset * side);
            if (candidate.x < -halfW || candidate.x > halfW || candidate.y < -halfH || candidate.y > halfH)
            {
                continue;
            }

            if (IsNearPermanentWater(candidate, minWaterDistance, halfW, halfH))
            {
                continue;
            }

            var radius = Mathf.Lerp(14f, 24f, Hash01(seed, i, 173));
            var cover = Mathf.Lerp(0.65f, 0.85f, Hash01(seed, i, 191));
            result.Add(new KopjesNodeData($"riverbank_kopjes_{result.Count + 1}", candidate, radius, cover));
        }

        return result;
    }

    private bool[] BuildLocalWaterMask(float halfW, float halfH, int targetW, int targetH)
    {
        var mask = new bool[targetW * targetH];
        if (permanentWaterMask == null || permanentWaterMaskWidth <= 0 || permanentWaterMaskHeight <= 0)
        {
            return mask;
        }

        for (var y = 0; y < targetH; y++)
        {
            var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, targetH - 1f));
            for (var x = 0; x < targetW; x++)
            {
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, targetW - 1f));
                var waterPixel = WorldToPixel(wx, wy, halfW, halfH, permanentWaterMaskWidth, permanentWaterMaskHeight);
                mask[x + y * targetW] = permanentWaterMask[waterPixel.px + waterPixel.py * permanentWaterMaskWidth];
            }
        }

        return mask;
    }

    private bool IsNearPermanentWater(Vector2 world, float minDistance, float halfW, float halfH)
    {
        if (permanentWaterMask == null || permanentWaterMaskWidth <= 0 || permanentWaterMaskHeight <= 0)
        {
            return false;
        }

        var center = WorldToPixel(world.x, world.y, halfW, halfH, permanentWaterMaskWidth, permanentWaterMaskHeight);
        var worldPerPixelX = (2f * halfW) / Mathf.Max(1f, permanentWaterMaskWidth - 1f);
        var worldPerPixelY = (2f * halfH) / Mathf.Max(1f, permanentWaterMaskHeight - 1f);
        var radiusPx = Mathf.CeilToInt(minDistance / Mathf.Max(0.001f, Mathf.Min(worldPerPixelX, worldPerPixelY)));
        var radiusSq = minDistance * minDistance;
        for (var y = center.py - radiusPx; y <= center.py + radiusPx; y++)
        {
            if (y < 0 || y >= permanentWaterMaskHeight) continue;
            for (var x = center.px - radiusPx; x <= center.px + radiusPx; x++)
            {
                if (x < 0 || x >= permanentWaterMaskWidth) continue;
                if (!permanentWaterMask[x + y * permanentWaterMaskWidth]) continue;
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, permanentWaterMaskWidth - 1f));
                var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, permanentWaterMaskHeight - 1f));
                var dx = wx - world.x;
                var dy = wy - world.y;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void BuildDebugOverlays(SerengetiMapSpec spec, ScenarioConfig config, float halfW, float halfH)
    {
        var existing = mapRoot.Find("DebugOverlays");
        if (existing != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(existing.gameObject);
            else UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        if (config?.predatorPreyDocu == null || !config.predatorPreyDocu.debugShowMapOverlays)
        {
            return;
        }

        var debugRoot = new GameObject("DebugOverlays").transform;
        debugRoot.SetParent(mapRoot, false);
        foreach (var r in spec.regions)
        {
            var xMinW = Mathf.Lerp(-halfW, halfW, r.shape.xMin);
            var xMaxW = Mathf.Lerp(-halfW, halfW, r.shape.xMax);
            var yMinW = Mathf.Lerp(-halfH, halfH, r.shape.yMin);
            var yMaxW = Mathf.Lerp(-halfH, halfH, r.shape.yMax);
            var color = new Color(0f, 1f, 0f, 0.9f);
            CreateRectOutline(debugRoot, xMinW, xMaxW, yMinW, yMaxW, color, 2f);
            CreateDebugLabel(debugRoot, new Vector2((xMinW + xMaxW) * 0.5f, (yMinW + yMaxW) * 0.5f), r.id, color);
            var center = new Vector2((xMinW + xMaxW) * 0.5f, (yMinW + yMaxW) * 0.5f);
            CreateRectStrip(debugRoot, center, new Vector2(4f, 0.8f), color);
            CreateRectStrip(debugRoot, center, new Vector2(0.8f, 4f), color);
        }

        DrawRegionBoundaryHints(debugRoot, spec, halfW, halfH);

        foreach (var c in crossingsMain)
        {
            var color = new Color(1f, 0f, 0f, 0.9f);
            CreateCircleOutline(debugRoot, c.worldPos, c.worldRadius, color);
            CreateDebugLabel(debugRoot, c.worldPos + new Vector2(c.worldRadius + 1.4f, c.worldRadius + 0.8f), "MaraCrossing", color);
        }

        foreach (var c in crossingsGrumeti)
        {
            var color = new Color(1f, 0.55f, 0f, 0.9f);
            CreateCircleOutline(debugRoot, c.worldPos, c.worldRadius, color);
            CreateDebugLabel(debugRoot, c.worldPos + new Vector2(c.worldRadius + 1.4f, c.worldRadius + 0.8f), "GrumetiCrossing", color);
        }

        foreach (var pool in pools)
        {
            var color = new Color(0f, 1f, 1f, 0.85f);
            CreateCircleOutline(debugRoot, pool.worldPos, pool.worldRadius, color);
            CreateDebugLabel(debugRoot, pool.worldPos + new Vector2(pool.worldRadius + 1.4f, pool.worldRadius + 0.8f), "Pool", color);
        }

        foreach (var node in kopjes)
        {
            var color = new Color(0.62f, 0.62f, 0.62f, 0.85f);
            CreateCircleOutline(debugRoot, node.position, node.radius, color);
            CreateDebugLabel(debugRoot, node.position + new Vector2(node.radius + 1.4f, node.radius + 0.8f), "Kopjes", color);
        }

        Debug.Log($"[SerengetiDebug] RegionOutlines drawn: {spec.regions.Count} (no global lines)");
        Debug.Log($"[SerengetiDebug] overlays=ON regions={spec.regions.Count} mainCross={crossingsMain.Count} grumetiCross={crossingsGrumeti.Count} pools={pools.Count} kopjes={kopjes.Count} wetlands={wetlands.Count}");
    }

    private static void CreateRectOutline(Transform parent, float x0, float x1, float y0, float y1, Color color, float thickness)
    {
        var xMid = (x0 + x1) * 0.5f;
        var yMid = (y0 + y1) * 0.5f;
        var width = Mathf.Max(thickness, x1 - x0);
        var height = Mathf.Max(thickness, y1 - y0);

        CreateRectStrip(parent, new Vector2(xMid, y1), new Vector2(width, thickness), color);
        CreateRectStrip(parent, new Vector2(xMid, y0), new Vector2(width, thickness), color);
        CreateRectStrip(parent, new Vector2(x0, yMid), new Vector2(thickness, height), color);
        CreateRectStrip(parent, new Vector2(x1, yMid), new Vector2(thickness, height), color);
    }

    private static void CreateCircleOutline(Transform parent, Vector2 center, float radius, Color color)
    {
        var sr = CreateDebugSprite(parent, PrimitiveSpriteLibrary.CircleOutline(64), center, new Vector2(radius * 2f, radius * 2f));
        sr.color = color;
    }

    private static void CreateDebugLabel(Transform parent, Vector2 pos, string text, Color color)
    {
        var go = new GameObject($"Label_{text}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 24;
        tm.characterSize = 0.22f;
        tm.anchor = TextAnchor.MiddleLeft;
        tm.alignment = TextAlignment.Left;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = SortingLayerDefault;
            mr.sortingOrder = DebugOverlayOrder;
        }
    }

    private static void CreateRectStrip(Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var sr = CreateDebugSprite(parent, PrimitiveSpriteLibrary.RoundedRectFill(64), center, size);
        sr.color = color;
    }

    private static SpriteRenderer CreateDebugSprite(Transform parent, Sprite sprite, Vector2 pos, Vector2 scale)
    {
        var go = new GameObject("Debug");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = DebugOverlayOrder;
        return sr;
    }

    private SpriteRenderer CreateSprite(string name, Texture2D texture, float ppu, int sorting)
    {
        var go = new GameObject(name);
        go.transform.SetParent(mapRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu);
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = sorting;
        return sr;
    }

    private static Texture2D ToTex(int w, int h, Color32[] px)
    {
        var tex = NewTex(w, h);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

    private static float PickBoundedPpu(float arenaW, float arenaH, float minPpu, float maxPpu, int maxPixels)
    {
        var choices = new[] { maxPpu, (maxPpu + minPpu) * 0.5f, minPpu };
        foreach (var ppu in choices)
        {
            var w = Mathf.RoundToInt(arenaW * ppu);
            var h = Mathf.RoundToInt(arenaH * ppu);
            if ((long)w * h <= maxPixels)
            {
                return ppu;
            }
        }

        return minPpu;
    }

    private static (int w, int h, float ppu) PickTextureSize(float arenaW, float arenaH, float preferredPPU, int maxPixels)
    {
        var ppu = preferredPPU;
        var w = Mathf.RoundToInt(arenaW * ppu);
        var h = Mathf.RoundToInt(arenaH * ppu);
        while ((long)w * h > maxPixels && ppu > 1f)
        {
            ppu *= 0.8f;
            w = Mathf.RoundToInt(arenaW * ppu);
            h = Mathf.RoundToInt(arenaH * ppu);
        }

        return (Mathf.Max(256, w), Mathf.Max(256, h), ppu);
    }

    private static Vector2 ToWorld(float nx, float ny, float halfW, float halfH) => new(Mathf.Lerp(-halfW, halfW, nx), Mathf.Lerp(-halfH, halfH, ny));

    private static (int px, int py) WorldToPixel(float x, float y, float halfW, float halfH, int texW, int texH)
    {
        var u = (x + halfW) / (2f * halfW);
        var v = (y + halfH) / (2f * halfH);
        return (Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1), Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1));
    }

    private static int WorldToPixelX(float x, float halfW, int texW)
    {
        var u = (x + halfW) / (2f * halfW);
        return Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1);
    }

    private static void FillHorizontalSpan(Color32[] px, int w, int h, int y, int centerX, int halfWidthPx, Color32 col)
    {
        if (y < 0 || y >= h)
        {
            return;
        }

        var x0 = Mathf.Clamp(centerX - Mathf.Max(1, halfWidthPx), 0, w - 1);
        var x1 = Mathf.Clamp(centerX + Mathf.Max(1, halfWidthPx), 0, w - 1);
        for (var x = x0; x <= x1; x++)
        {
            px[x + y * w] = Blend(px[x + y * w], col);
        }
    }

    private static void FillVerticalSpan(Color32[] px, int w, int h, int x, int centerY, int halfHeightPx, Color32 col)
    {
        if (x < 0 || x >= w)
        {
            return;
        }

        var y0 = Mathf.Clamp(centerY - Mathf.Max(1, halfHeightPx), 0, h - 1);
        var y1 = Mathf.Clamp(centerY + Mathf.Max(1, halfHeightPx), 0, h - 1);
        for (var y = y0; y <= y1; y++)
        {
            px[x + y * w] = Blend(px[x + y * w], col);
        }
    }

    private static void PaintDisc(Color32[] px, int w, int h, (int px, int py) c, int r, Color32 col)
    {
        var r2 = r * r;
        for (var y = c.py - r; y <= c.py + r; y++)
        {
            if (y < 0 || y >= h) continue;
            for (var x = c.px - r; x <= c.px + r; x++)
            {
                if (x < 0 || x >= w) continue;
                var dx = x - c.px;
                var dy = y - c.py;
                if (dx * dx + dy * dy > r2) continue;
                px[x + y * w] = Blend(px[x + y * w], col);
            }
        }
    }

    private static void PaintDiscMasked(Color32[] px, bool[] mask, int w, int h, (int px, int py) c, int r, Color32 col)
    {
        var r2 = r * r;
        for (var y = c.py - r; y <= c.py + r; y++)
        {
            if (y < 0 || y >= h) continue;
            for (var x = c.px - r; x <= c.px + r; x++)
            {
                if (x < 0 || x >= w) continue;
                var dx = x - c.px;
                var dy = y - c.py;
                if (dx * dx + dy * dy > r2) continue;
                var index = x + y * w;
                if (mask != null && mask[index]) continue;
                px[index] = Blend(px[index], col);
            }
        }
    }

    private static void PaintIrregularWetland(Color32[] px, int w, int h, (int px, int py) c, float worldRadius, float ppu, string id)
    {
        var seed = Mathf.Abs(StableHash.Hash32(id ?? "wetland"));
        var puddles = Mathf.Clamp(Mathf.RoundToInt(worldRadius * 0.12f), 6, 14);
        for (var i = 0; i < puddles; i++)
        {
            var ang = Hash01(seed, i, 401) * Mathf.PI * 2f;
            var mag = Mathf.Sqrt(Hash01(seed, i, 433)) * worldRadius * 0.75f;
            var center = (c.px + Mathf.RoundToInt(Mathf.Cos(ang) * mag * ppu), c.py + Mathf.RoundToInt(Mathf.Sin(ang) * mag * ppu));
            var rad = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(3f, 9f, Hash01(seed, i, 461)) * ppu));
            var alpha = Mathf.RoundToInt(Mathf.Lerp(60f, 120f, Hash01(seed, i, 487)));
            PaintDisc(px, w, h, center, rad, new Color32(80, 152, 198, (byte)alpha));
        }
    }


    private static void PaintRing(Color32[] px, int w, int h, (int px, int py) c, int r, int th, Color32 col)
    {
        var inner = Mathf.Max(0, r - th);
        var o2 = r * r;
        var i2 = inner * inner;
        for (var y = c.py - r; y <= c.py + r; y++)
        {
            if (y < 0 || y >= h) continue;
            for (var x = c.px - r; x <= c.px + r; x++)
            {
                if (x < 0 || x >= w) continue;
                var dx = x - c.px;
                var dy = y - c.py;
                var d2 = dx * dx + dy * dy;
                if (d2 > o2 || d2 < i2) continue;
                px[x + y * w] = Blend(px[x + y * w], col);
            }
        }
    }

    private static Color32 Blend(Color32 dst, Color32 src)
    {
        var a = src.a / 255f;
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(src.r * a + dst.r * (1f - a)), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(src.g * a + dst.g * (1f - a)), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(src.b * a + dst.b * (1f - a)), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt((a + (dst.a / 255f) * (1f - a)) * 255f), 0, 255));
    }

    private static Color32 RegionTint(string biome)
    {
        if (string.Equals(biome, "riverine", StringComparison.OrdinalIgnoreCase)) return new Color32(138, 142, 101, 255);
        if (string.Equals(biome, "woodland", StringComparison.OrdinalIgnoreCase)) return new Color32(122, 133, 96, 255);
        if (string.Equals(biome, "plains", StringComparison.OrdinalIgnoreCase)) return new Color32(166, 149, 106, 255);
        if (string.Equals(biome, "kopjes", StringComparison.OrdinalIgnoreCase)) return new Color32(132, 130, 115, 255);
        return new Color32(145, 137, 106, 255);
    }

    private static Color32 RegionTintWithTraits(RegionSpec region)
    {
        var tint = RegionTint(region.biome);
        var greenness = Mathf.Clamp(region.baseGreenness, -1f, 1f);
        var cover = Mathf.Clamp01(region.cover);
        var r = tint.r - greenness * 7f - cover * 3f;
        var g = tint.g + greenness * 12f + cover * 4f;
        var b = tint.b - greenness * 4f - cover * 2f;
        var darken = 1f - cover * 0.04f;
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(r * darken), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(g * darken), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(b * darken), 0, 255),
            255);
    }

    private static float RegionBaseAlpha(RegionSpec region)
    {
        var baseBiomeAlpha = 20f;
        if (string.Equals(region.biome, "riverine", StringComparison.OrdinalIgnoreCase)) baseBiomeAlpha = 27f;
        else if (string.Equals(region.biome, "woodland", StringComparison.OrdinalIgnoreCase)) baseBiomeAlpha = 23f;
        else if (string.Equals(region.biome, "kopjes", StringComparison.OrdinalIgnoreCase)) baseBiomeAlpha = 21f;
        else if (string.Equals(region.biome, "plains", StringComparison.OrdinalIgnoreCase)) baseBiomeAlpha = 18f;

        var cover = Mathf.Clamp01(region.cover);
        var greenness = Mathf.Clamp(region.baseGreenness, -1f, 1f);
        var alpha = baseBiomeAlpha + cover * 18f + greenness * 6f;
        return Mathf.Clamp(alpha, 14f, 70f);
    }

    private List<Vector2> BuildMoistureSources()
    {
        var sources = new List<Vector2>(waterNodes.Count + pools.Count + wetlands.Count);
        sources.AddRange(waterNodes);
        for (var i = 0; i < pools.Count; i++)
        {
            sources.Add(pools[i].worldPos);
        }

        for (var i = 0; i < wetlands.Count; i++)
        {
            sources.Add(wetlands[i].worldPos);
        }

        return sources;
    }

    private static Dictionary<int, List<int>> BuildPointGrid(List<Vector2> points, float halfW, float halfH, float cellSizeWorld, out int cellsX, out int cellsY)
    {
        cellsX = Mathf.Max(1, Mathf.CeilToInt((halfW * 2f) / Mathf.Max(1f, cellSizeWorld)));
        cellsY = Mathf.Max(1, Mathf.CeilToInt((halfH * 2f) / Mathf.Max(1f, cellSizeWorld)));
        var grid = new Dictionary<int, List<int>>();
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var cx = Mathf.Clamp(Mathf.FloorToInt((p.x + halfW) / cellSizeWorld), 0, cellsX - 1);
            var cy = Mathf.Clamp(Mathf.FloorToInt((p.y + halfH) / cellSizeWorld), 0, cellsY - 1);
            var key = cx + cy * cellsX;
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<int>();
                grid[key] = list;
            }

            list.Add(i);
        }

        return grid;
    }

    private static float ApproxNearestPointDistance(float wx, float wy, List<Vector2> points, Dictionary<int, List<int>> grid, float halfW, float halfH, float cellSizeWorld, int cellsX, int cellsY)
    {
        if (points.Count == 0)
        {
            return halfW + halfH;
        }

        var centerX = Mathf.Clamp(Mathf.FloorToInt((wx + halfW) / cellSizeWorld), 0, cellsX - 1);
        var centerY = Mathf.Clamp(Mathf.FloorToInt((wy + halfH) / cellSizeWorld), 0, cellsY - 1);
        var bestD2 = float.PositiveInfinity;

        for (var ring = 0; ring <= 2; ring++)
        {
            var foundAny = false;
            for (var oy = -ring; oy <= ring; oy++)
            {
                var cy = centerY + oy;
                if (cy < 0 || cy >= cellsY) continue;
                for (var ox = -ring; ox <= ring; ox++)
                {
                    var cx = centerX + ox;
                    if (cx < 0 || cx >= cellsX) continue;
                    var key = cx + cy * cellsX;
                    if (!grid.TryGetValue(key, out var list)) continue;
                    foundAny = true;
                    for (var i = 0; i < list.Count; i++)
                    {
                        var p = points[list[i]];
                        var dx = p.x - wx;
                        var dy = p.y - wy;
                        var d2 = dx * dx + dy * dy;
                        if (d2 < bestD2) bestD2 = d2;
                    }
                }
            }

            if (foundAny && bestD2 < float.PositiveInfinity)
            {
                return Mathf.Sqrt(bestD2);
            }
        }

        return Mathf.Sqrt((halfW * 2f) * (halfW * 2f) + (halfH * 2f) * (halfH * 2f));
    }

    private static float Noise2D(float x, float y, int seedSalt)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        var tx = x - x0;
        var ty = y - y0;
        var u = tx * tx * (3f - 2f * tx);
        var v = ty * ty * (3f - 2f * ty);

        var v00 = Hash01(x0, y0, seedSalt);
        var v10 = Hash01(x1, y0, seedSalt);
        var v01 = Hash01(x0, y1, seedSalt);
        var v11 = Hash01(x1, y1, seedSalt);
        var ix0 = Mathf.Lerp(v00, v10, u);
        var ix1 = Mathf.Lerp(v01, v11, u);
        return Mathf.Lerp(ix0, ix1, v) * 2f - 1f;
    }

    private static float Hash01(int x, int y, int seed)
    {
        var h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h & 0xFFFF) / 65535f;
    }

    private static float ComputeRegionInfluence(RegionField field, float x, float y, int seedSalt, int px, int py, float bonusPadding, float bonusFalloff, float bonusScale)
    {
        var dx = (x - field.cx) / field.ex;
        var dy = (y - field.cy) / field.ey;
        var d = Mathf.Sqrt(dx * dx + dy * dy);
        var baseInfluence = 1f - d;
        var signedDist = SignedDistanceToRect(field.x0, field.x1, field.y0, field.y1, x, y);
        var bonus = Mathf.Clamp01((signedDist + bonusPadding) / Mathf.Max(1f, bonusFalloff)) * bonusScale;
        var tinyNoise = (Noise2D(x * 0.016f + 7f, y * 0.016f + 13f, seedSalt ^ 719) + (Hash01(px, py, seedSalt ^ 887) - 0.5f)) * 0.02f;
        return baseInfluence + bonus + tinyNoise;
    }

    private static float SignedDistanceToRect(float x0, float x1, float y0, float y1, float x, float y)
    {
        var insideX = x >= x0 && x <= x1;
        var insideY = y >= y0 && y <= y1;
        if (insideX && insideY)
        {
            return Mathf.Min(x - x0, x1 - x, y - y0, y1 - y);
        }

        var dx = x < x0 ? x0 - x : (x > x1 ? x - x1 : 0f);
        var dy = y < y0 ? y0 - y : (y > y1 ? y - y1 : 0f);
        return -Mathf.Sqrt(dx * dx + dy * dy);
    }

    private void DrawRegionBoundaryHints(Transform parent, SerengetiMapSpec spec, float halfW, float halfH)
    {
        var seedSalt = Mathf.Abs(StableHash.Hash32(spec.mapId ?? "serengeti_v1"));
        var fields = new List<RegionField>();
        foreach (var region in spec.regions)
        {
            fields.Add(new RegionField(
                region,
                Mathf.Lerp(-halfW, halfW, region.shape.xMin),
                Mathf.Lerp(-halfW, halfW, region.shape.xMax),
                Mathf.Lerp(-halfH, halfH, region.shape.yMin),
                Mathf.Lerp(-halfH, halfH, region.shape.yMax)));
        }

        const float spacing = 72f;
        const float warpAmpWorld = 78f;
        const float warpFreq = 0.0023f;
        const float microWarpAmpWorld = 14f;
        const float microWarpFreq = 0.01f;
        for (var y = -halfH; y <= halfH; y += spacing)
        {
            for (var x = -halfW; x <= halfW; x += spacing)
            {
                var wx = x;
                var wy = y;
                var warpX = Noise2D(wx * warpFreq + 11f, wy * warpFreq + 17f, seedSalt) * warpAmpWorld +
                            Noise2D(wx * microWarpFreq + 101f, wy * microWarpFreq + 131f, seedSalt) * microWarpAmpWorld;
                var warpY = Noise2D(wx * warpFreq + 23f, wy * warpFreq + 29f, seedSalt) * warpAmpWorld +
                            Noise2D(wx * microWarpFreq + 149f, wy * microWarpFreq + 173f, seedSalt) * microWarpAmpWorld;
                var tx = wx + warpX;
                var ty = wy + warpY;

                var best = float.NegativeInfinity;
                var second = float.NegativeInfinity;
                var bestIndex = -1;
                var secondIndex = -1;
                for (var i = 0; i < fields.Count; i++)
                {
                    var influence = ComputeRegionInfluence(fields[i], tx, ty, seedSalt, Mathf.RoundToInt(x), Mathf.RoundToInt(y), 45f, 220f, 0.46f);
                    if (influence > best)
                    {
                        second = best;
                        secondIndex = bestIndex;
                        best = influence;
                        bestIndex = i;
                    }
                    else if (influence > second)
                    {
                        second = influence;
                        secondIndex = i;
                    }
                }

                var delta = best - second;
                if (delta < 0.12f && bestIndex >= 0)
                {
                    var bestColor = RegionDebugColor(fields[bestIndex].region.biome);
                    var secondColor = secondIndex >= 0 ? RegionDebugColor(fields[secondIndex].region.biome) : bestColor;
                    var dotColor = Color.Lerp(bestColor, secondColor, 0.5f);
                    dotColor.a = 0.85f;
                    CreateRectStrip(parent, new Vector2(wx, wy), new Vector2(2.5f, 2.5f), dotColor);
                }
                else if (delta < 0.2f && bestIndex >= 0)
                {
                    var color = RegionDebugColor(fields[bestIndex].region.biome);
                    color.a = 0.62f;
                    CreateRectStrip(parent, new Vector2(wx, wy), new Vector2(1.8f, 1.8f), color);
                }
            }
        }
    }

    private static Color RegionDebugColor(string biome)
    {
        var tint = RegionTint(biome);
        return new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, 0.8f);
    }

    private static List<Vector2> SampleCatmullRom(List<Vector2> points, int samplesPerSegment)
    {
        var result = new List<Vector2>();
        if (points == null || points.Count < 2) return result;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];
            for (var s = 0; s < samplesPerSegment; s++)
            {
                var t = s / (float)samplesPerSegment;
                result.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }

        result.Add(points[points.Count - 1]);
        return result;
    }



    private static List<Vector2> UpsamplePolyline(List<Vector2> points, int factor)
    {
        if (points == null || points.Count < 2 || factor <= 1)
        {
            return points ?? new List<Vector2>();
        }

        var result = new List<Vector2>((points.Count - 1) * factor + 1);
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            for (var s = 0; s < factor; s++)
            {
                var t = s / (float)factor;
                result.Add(Vector2.Lerp(a, b, t));
            }
        }

        result.Add(points[points.Count - 1]);
        return result;
    }

    private static List<Vector2> EnsureMinSampleCount(List<Vector2> points, int minSamples)
    {
        if (points == null || points.Count < 2 || points.Count >= minSamples)
        {
            return points ?? new List<Vector2>();
        }

        var factor = Mathf.CeilToInt((minSamples - 1f) / Mathf.Max(1f, points.Count - 1f));
        return UpsamplePolyline(points, Mathf.Max(2, factor));
    }

    private static RiverYLookup BuildSplineYLookup(List<Vector2> smooth)
    {
        if (smooth == null || smooth.Count < 2)
        {
            return RiverYLookup.Invalid;
        }

        var totalLength = 0f;
        for (var i = 1; i < smooth.Count; i++) totalLength += Vector2.Distance(smooth[i - 1], smooth[i]);

        var samples = new List<RiverSample>(smooth.Count);
        var run = 0f;
        samples.Add(new RiverSample(smooth[0].y, smooth[0].x, 0f));
        for (var i = 1; i < smooth.Count; i++)
        {
            run += Vector2.Distance(smooth[i - 1], smooth[i]);
            var progress = totalLength <= 0f ? 0f : run / totalLength;
            samples.Add(new RiverSample(smooth[i].y, smooth[i].x, progress));
        }

        samples.Sort((a, b) => a.y.CompareTo(b.y));
        return new RiverYLookup(samples);
    }

    private static RiverXLookup BuildSplineXLookup(List<Vector2> smooth)
    {
        if (smooth == null || smooth.Count < 2)
        {
            return RiverXLookup.Invalid;
        }

        var totalLength = 0f;
        for (var i = 1; i < smooth.Count; i++) totalLength += Vector2.Distance(smooth[i - 1], smooth[i]);

        var samples = new List<RiverSample>(smooth.Count);
        var run = 0f;
        samples.Add(new RiverSample(smooth[0].x, smooth[0].y, 0f));
        for (var i = 1; i < smooth.Count; i++)
        {
            run += Vector2.Distance(smooth[i - 1], smooth[i]);
            var progress = totalLength <= 0f ? 0f : run / totalLength;
            samples.Add(new RiverSample(smooth[i].x, smooth[i].y, progress));
        }

        samples.Sort((a, b) => a.y.CompareTo(b.y));
        return new RiverXLookup(samples);
    }

    private static void WarnIfNormalizedOutsideRange(float value, string path)
    {
        if (value < 0f || value > 1f)
        {
            Debug.LogWarning($"[SerengetiConformance] Coordinate outside [0..1] at {path}: {value:0.###}");
        }
    }

    private static void WarnIfWorldOutsideBounds(Vector2 world, float halfW, float halfH, string path)
    {
        if (Mathf.Abs(world.x) > halfW || Mathf.Abs(world.y) > halfH)
        {
            Debug.LogWarning($"[SerengetiConformance] World coordinate outside arena at {path}: ({world.x:0.##}, {world.y:0.##}) bounds=({halfW:0.##}, {halfH:0.##})");
        }
    }

    private readonly struct RiverSample
    {
        public readonly float y;
        public readonly float x;
        public readonly float progress;

        public RiverSample(float y, float x, float progress)
        {
            this.y = y;
            this.x = x;
            this.progress = progress;
        }
    }

    private readonly struct RiverYLookup
    {
        private readonly List<RiverSample> samples;

        public static RiverYLookup Invalid => new(null);

        public bool Valid => samples != null && samples.Count > 1;

        public RiverYLookup(List<RiverSample> samples)
        {
            this.samples = samples;
        }

        public bool TryEvalX(float yWorld, out float xWorld, out float progress)
        {
            xWorld = 0f;
            progress = 0f;
            if (!Valid || yWorld < samples[0].y || yWorld > samples[samples.Count - 1].y)
            {
                return false;
            }

            var lo = 0;
            var hi = samples.Count - 1;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) >> 1;
                if (samples[mid].y <= yWorld)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            var a = samples[lo];
            var b = samples[hi];
            var span = Mathf.Max(0.0001f, b.y - a.y);
            var t = Mathf.Clamp01((yWorld - a.y) / span);
            xWorld = Mathf.Lerp(a.x, b.x, t);
            progress = Mathf.Lerp(a.progress, b.progress, t);
            return true;
        }
    }

    private readonly struct RiverXLookup
    {
        private readonly List<RiverSample> samples;

        public static RiverXLookup Invalid => new(null);

        public bool Valid => samples != null && samples.Count > 1;

        public RiverXLookup(List<RiverSample> samples)
        {
            this.samples = samples;
        }

        public bool TryEvalY(float xWorld, out float yWorld, out float progress)
        {
            yWorld = 0f;
            progress = 0f;
            if (!Valid || xWorld < samples[0].y || xWorld > samples[samples.Count - 1].y)
            {
                return false;
            }

            var lo = 0;
            var hi = samples.Count - 1;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) >> 1;
                if (samples[mid].y <= xWorld)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            var a = samples[lo];
            var b = samples[hi];
            var span = Mathf.Max(0.0001f, b.y - a.y);
            var t = Mathf.Clamp01((xWorld - a.y) / span);
            yWorld = Mathf.Lerp(a.x, b.x, t);
            progress = Mathf.Lerp(a.progress, b.progress, t);
            return true;
        }
    }

    public struct CrossingNodeData
    {
        public Vector2 worldPos;
        public float worldRadius;
        public float crocRisk;
        public Vector2 position => worldPos;
        public float radius => worldRadius;
    }

    public struct PoolNodeData
    {
        public string id;
        public Vector2 worldPos;
        public float worldRadius;
        public bool permanent;
    }

    public struct WetlandNodeData
    {
        public string id;
        public Vector2 worldPos;
        public float worldRadius;
    }

    public struct KopjesNodeData
    {
        public string id;
        public Vector2 position;
        public float radius;
        public float cover;

        public KopjesNodeData(string id, Vector2 position, float radius, float cover)
        {
            this.id = id;
            this.position = position;
            this.radius = radius;
            this.cover = cover;
        }
    }
}
