using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int TerrainBaseOrder = -200;
    private const int RegionOverlayOrder = -198;
    private const int FloodplainOrder = -190;
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
    private SpriteRenderer bankSR;
    private SpriteRenderer permanentWaterSR;
    private SpriteRenderer seasonalWaterSR;
    private int mainRiverRasterSamples;
    private int grumetiRasterSamples;

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
        BuildTerrain(halfW, halfH);
        BuildRegions(spec, halfW, halfH);
        BuildWater(spec, halfW, halfH);
        BuildKopjes(spec, halfW, halfH);
        BuildDebugOverlays(spec, config, halfW, halfH);

        var mainRiverControlPoints = spec.water.mainRiver.centerline.Count;
        var grumetiControlPoints = spec.water.grumeti.centerline.Count;
        Debug.Log($"[SerengetiConformance] mapId={spec.mapId} arena={spec.arena.width}x{spec.arena.height} regions={spec.regions.Count} mainRiverControlPoints={mainRiverControlPoints} mainRiverRasterSamples={mainRiverRasterSamples} grumetiControlPoints={grumetiControlPoints} grumetiRasterSamples={grumetiRasterSamples} pools={spec.water.pools.Count} kopjes={spec.landmarks.kopjes.Count} wetlands={spec.landmarks.wetlands.Count}");
    }

    public void UpdateSeasonVisuals(float seasonalPresence01)
    {
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
        bankSR = null;
        permanentWaterSR = null;
        seasonalWaterSR = null;
        mainRiverRasterSamples = 0;
        grumetiRasterSamples = 0;

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

    private void BuildTerrain(float halfW, float halfH)
    {
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 3f, 1_800_000);
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        var c0 = new Color32(173, 152, 96, 255);
        for (var i = 0; i < px.Length; i++) px[i] = c0;
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
        var featherWidthWorld = Mathf.Clamp(18f, 12f, 24f);
        var regions = new List<(RegionSpec region, float x0, float x1, float y0, float y1)>();
        foreach (var region in spec.regions)
        {
            regions.Add((
                region,
                Mathf.Lerp(-halfW, halfW, region.shape.xMin),
                Mathf.Lerp(-halfW, halfW, region.shape.xMax),
                Mathf.Lerp(-halfH, halfH, region.shape.yMin),
                Mathf.Lerp(-halfH, halfH, region.shape.yMax)));
        }

        for (var y = 0; y < h; y++)
        {
            var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, h - 1f));
            for (var x = 0; x < w; x++)
            {
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, w - 1f));
                var chosen = default(RegionSpec);
                var dist = -1f;
                foreach (var entry in regions)
                {
                    var region = entry.region;
                    var rx0 = entry.x0;
                    var rx1 = entry.x1;
                    var ry0 = entry.y0;
                    var ry1 = entry.y1;
                    if (wx < rx0 || wx > rx1 || wy < ry0 || wy > ry1) continue;
                    var edge = Mathf.Min(wx - rx0, rx1 - wx, wy - ry0, ry1 - wy);
                    if (edge > dist)
                    {
                        dist = edge;
                        chosen = region;
                    }
                }

                if (chosen == null) continue;
                var edgeFeather = Mathf.SmoothStep(0f, featherWidthWorld, dist);
                var alpha = RegionBaseAlpha(chosen.biome) * edgeFeather;
                var noise = Hash01(x, y, 17) * 0.12f - 0.06f;
                alpha *= 1f + noise;
                var tint = RegionTint(chosen.biome);
                px[x + y * w] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        regionSR = CreateSprite("RegionOverlayTexture", tex, ppu, RegionOverlayOrder);
        Debug.Log($"[SerengetiConformance] RegionTex={w}x{h}@{ppu:0.###}PPU");
    }

    private void BuildWater(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 4f, 3_500_000);
        var floodPx = new Color32[w * h];
        var bankPx = new Color32[w * h];
        var permPx = new Color32[w * h];
        var seasonPx = new Color32[w * h];

        mainRiverRasterSamples = PaintMainRiverScanline(spec.water.mainRiver, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx);
        grumetiRasterSamples = PaintGrumetiPolyline(spec.water.grumeti, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx);

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
        bankSR = CreateSprite("BankOverlayTexture", ToTex(w, h, bankPx), ppu, BankOrder);
        permanentWaterSR = CreateSprite("PermanentWaterOverlay", ToTex(w, h, permPx), ppu, PermanentWaterOrder);
        seasonalWaterSR = CreateSprite("SeasonalWaterOverlay", ToTex(w, h, seasonPx), ppu, SeasonalWaterOrder);
        Debug.Log($"[SerengetiConformance] WaterTex={w}x{h}@{ppu:0.###}PPU");
    }

    private int PaintMainRiverScanline(RiverSpec river, float halfW, float halfH, int texW, int texH, float ppu, Color32[] floodPx, Color32[] bankPx, Color32[] waterPx)
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
            FillHorizontalSpan(floodPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt((width * 0.5f + river.floodplainExtra * 0.5f) * ppu), floodCol);
            FillHorizontalSpan(bankPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt((width * 0.5f + river.bankExtra) * ppu), bankCol);
            FillHorizontalSpan(waterPx, texW, texH, py, WorldToPixelX(xWorld, halfW, texW), Mathf.RoundToInt(width * 0.5f * ppu), waterCol);
        }

        for (var i = 0; i < sampled.Count; i += 40)
        {
            waterNodes.Add(sampled[i]);
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

        return sampled.Count;
    }

    private int PaintGrumetiPolyline(RiverSpec river, float halfW, float halfH, int texW, int texH, float ppu, Color32[] floodPx, Color32[] bankPx, Color32[] waterPx)
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
            return 0;
        }

        var floodRadiusPx = Mathf.Max(1, Mathf.RoundToInt((river.width * 0.5f + river.floodplainExtra * 0.35f) * ppu));
        var bankRadiusPx = Mathf.Max(1, Mathf.RoundToInt((river.width * 0.5f + river.bankExtra * 0.6f) * ppu));
        var waterRadiusPx = Mathf.Max(1, Mathf.RoundToInt(river.width * 0.5f * ppu));
        var floodCol = new Color32(103, 136, 89, 68);
        var bankCol = new Color32(68, 100, 58, 104);
        var waterCol = new Color32(35, 120, 220, 255);

        for (var i = 1; i < sampled.Count; i++)
        {
            var a = sampled[i - 1];
            var b = sampled[i];
            var aPx = WorldToPixel(a.x, a.y, halfW, halfH, texW, texH);
            var bPx = WorldToPixel(b.x, b.y, halfW, halfH, texW, texH);
            var pixLen = Mathf.Max(1f, Vector2.Distance(new Vector2(aPx.px, aPx.py), new Vector2(bPx.px, bPx.py)));
            var steps = Mathf.Max(1, Mathf.CeilToInt(pixLen / Mathf.Max(1f, waterRadiusPx * 0.5f)));
            for (var s = 0; s <= steps; s++)
            {
                var t = s / (float)steps;
                var p = Vector2.Lerp(a, b, t);
                var c = WorldToPixel(p.x, p.y, halfW, halfH, texW, texH);
                FillHorizontalSpan(floodPx, texW, texH, c.py, c.px, floodRadiusPx, floodCol);
                FillHorizontalSpan(bankPx, texW, texH, c.py, c.px, bankRadiusPx, bankCol);
                FillHorizontalSpan(waterPx, texW, texH, c.py, c.px, waterRadiusPx, waterCol);
            }
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

        return sampled.Count;
    }

    private void BuildKopjes(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var ppu = PickBoundedPpu(halfW * 2f, halfH * 2f, 2f, 3f, 1_800_000);
        var w = Mathf.Max(256, Mathf.RoundToInt(halfW * 2f * ppu));
        var h = Mathf.Max(256, Mathf.RoundToInt(halfH * 2f * ppu));
        var px = new Color32[w * h];
        foreach (var node in spec.landmarks.kopjes)
        {
            WarnIfNormalizedOutsideRange(node.x, "landmarks.kopjes.x");
            WarnIfNormalizedOutsideRange(node.y, "landmarks.kopjes.y");
            var world = ToWorld(node.x, node.y, halfW, halfH);
            WarnIfWorldOutsideBounds(world, halfW, halfH, "landmarks.kopjes");
            var data = new KopjesNodeData(node.id, world, node.radius, node.cover);
            kopjes.Add(data);
            kopjesNodes.Add(data);
            shadeNodes.Add(world);
            var seed = Mathf.Abs(node.id?.GetHashCode() ?? 29);
            var speckles = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(200f, 600f, Mathf.Clamp01(node.radius / 48f))), 200, 600);
            for (var i = 0; i < speckles; i++)
            {
                var ang = Hash01(seed, i, 83) * Mathf.PI * 2f;
                var mag = Mathf.Sqrt(Hash01(seed, i, 107)) * node.radius;
                var p = WorldToPixel(world.x + Mathf.Cos(ang) * mag, world.y + Mathf.Sin(ang) * mag, halfW, halfH, w, h);
                var alpha = Mathf.RoundToInt(Mathf.Lerp(90f, 150f, Hash01(seed, i, 149)));
                px[p.px + p.py * w] = Blend(px[p.px + p.py * w], new Color32(63, 62, 58, (byte)alpha));
            }

            var blobCount = Mathf.Clamp(Mathf.RoundToInt(node.radius * 0.06f), 3, 8);
            for (var i = 0; i < blobCount; i++)
            {
                var ang = Hash01(seed, i, 211) * Mathf.PI * 2f;
                var mag = Mathf.Sqrt(Hash01(seed, i, 239)) * node.radius * 0.85f;
                var p = WorldToPixel(world.x + Mathf.Cos(ang) * mag, world.y + Mathf.Sin(ang) * mag, halfW, halfH, w, h);
                var rad = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1.2f, 2.8f, Hash01(seed, i, 271)) * ppu));
                PaintDisc(px, w, h, p, rad, new Color32(70, 69, 65, (byte)Mathf.RoundToInt(Mathf.Lerp(100f, 140f, Hash01(seed, i, 307)))));
            }
        }

        kopjesSR = CreateSprite("KopjesOverlayTexture", ToTex(w, h, px), ppu, KopjesOverlayOrder);
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
            var x0 = Mathf.Lerp(-halfW, halfW, r.shape.xMin);
            var x1 = Mathf.Lerp(-halfW, halfW, r.shape.xMax);
            var y0 = Mathf.Lerp(-halfH, halfH, r.shape.yMin);
            var y1 = Mathf.Lerp(-halfH, halfH, r.shape.yMax);
            var color = new Color(0.2f, 1f, 0.2f, 0.88f);
            CreateRectOutline(debugRoot, x0, x1, y0, y1, color, 0.7f);
            CreateDebugLabel(debugRoot, new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f), r.id, color);
        }

        foreach (var c in crossingsMain)
        {
            var color = new Color(1f, 0.2f, 0.2f, 0.9f);
            CreateCircleOutline(debugRoot, c.worldPos, c.worldRadius, color);
            CreateDebugLabel(debugRoot, c.worldPos + new Vector2(c.worldRadius + 1.4f, c.worldRadius + 0.8f), "MaraCrossing", color);
        }

        foreach (var c in crossingsGrumeti)
        {
            var color = new Color(1f, 0.62f, 0.17f, 0.9f);
            CreateCircleOutline(debugRoot, c.worldPos, c.worldRadius, color);
            CreateDebugLabel(debugRoot, c.worldPos + new Vector2(c.worldRadius + 1.4f, c.worldRadius + 0.8f), "GrumetiCrossing", color);
        }

        foreach (var pool in pools)
        {
            var color = new Color(0.3f, 0.92f, 1f, 0.82f);
            CreateCircleOutline(debugRoot, pool.worldPos, pool.worldRadius, color);
            CreateDebugLabel(debugRoot, pool.worldPos + new Vector2(pool.worldRadius + 1.4f, pool.worldRadius + 0.8f), "Pool", color);
        }

        foreach (var node in kopjes)
        {
            var color = new Color(0.66f, 0.66f, 0.66f, 0.82f);
            CreateCircleOutline(debugRoot, node.position, node.radius, color);
            CreateDebugLabel(debugRoot, node.position + new Vector2(node.radius + 1.4f, node.radius + 0.8f), "Kopjes", color);
        }

        Debug.Log($"[SerengetiDebug] regions={spec.regions.Count} crossingsMain={crossingsMain.Count} crossingsGrumeti={crossingsGrumeti.Count} pools={pools.Count} kopjes={kopjes.Count} wetlands={wetlands.Count}");
    }

    private static void CreateRectOutline(Transform parent, float x0, float x1, float y0, float y1, Color color, float thickness)
    {
        CreateLine(parent, new Vector2(x0, y0), new Vector2(x1, y0), color, thickness);
        CreateLine(parent, new Vector2(x1, y0), new Vector2(x1, y1), color, thickness);
        CreateLine(parent, new Vector2(x1, y1), new Vector2(x0, y1), color, thickness);
        CreateLine(parent, new Vector2(x0, y1), new Vector2(x0, y0), color, thickness);
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

    private static void CreateLine(Transform parent, Vector2 a, Vector2 b, Color color, float thickness)
    {
        var d = b - a;
        var sr = CreateDebugSprite(parent, PrimitiveSpriteLibrary.RoundedRectFill(64), a + d * 0.5f, new Vector2(d.magnitude, thickness));
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
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

    private static void PaintIrregularWetland(Color32[] px, int w, int h, (int px, int py) c, float worldRadius, float ppu, string id)
    {
        var seed = Mathf.Abs(id?.GetHashCode() ?? 43);
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
        if (string.Equals(biome, "riverine", StringComparison.OrdinalIgnoreCase)) return new Color32(132, 150, 106, 255);
        if (string.Equals(biome, "woodland", StringComparison.OrdinalIgnoreCase)) return new Color32(122, 130, 98, 255);
        if (string.Equals(biome, "kopjes", StringComparison.OrdinalIgnoreCase)) return new Color32(136, 132, 108, 255);
        return new Color32(148, 135, 100, 255);
    }

    private static float RegionBaseAlpha(string biome)
    {
        if (string.Equals(biome, "riverine", StringComparison.OrdinalIgnoreCase)) return 42f;
        if (string.Equals(biome, "woodland", StringComparison.OrdinalIgnoreCase)) return 36f;
        if (string.Equals(biome, "kopjes", StringComparison.OrdinalIgnoreCase)) return 30f;
        return 28f;
    }

    private static float Hash01(int x, int y, int seed)
    {
        var h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h & 0xFFFF) / 65535f;
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
