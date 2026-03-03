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
    private const int DebugOverlayOrder = -150;

    private Transform mapRoot;
    private SpriteRenderer terrainSR;
    private SpriteRenderer regionSR;
    private SpriteRenderer kopjesSR;
    private SpriteRenderer floodplainSR;
    private SpriteRenderer bankSR;
    private SpriteRenderer permanentWaterSR;
    private SpriteRenderer seasonalWaterSR;

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

        Debug.Log($"[SerengetiConformance] mapId={spec.mapId} arena={spec.arena.width}x{spec.arena.height} regions={spec.regions.Count} mainRiverSamples={spec.water.mainRiver.centerline.Count} grumetiSamples={spec.water.grumeti.centerline.Count} pools={spec.water.pools.Count} kopjes={spec.landmarks.kopjes.Count} wetlands={spec.landmarks.wetlands.Count}");
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
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 3f, 1_800_000);
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        var feather = Mathf.Clamp((halfW * 2f) * 0.01f, 8f, 20f);

        for (var y = 0; y < h; y++)
        {
            var wy = Mathf.Lerp(-halfH, halfH, y / Mathf.Max(1f, h - 1f));
            for (var x = 0; x < w; x++)
            {
                var wx = Mathf.Lerp(-halfW, halfW, x / Mathf.Max(1f, w - 1f));
                var chosen = default(RegionSpec);
                var dist = -1f;
                foreach (var region in spec.regions)
                {
                    var rx0 = Mathf.Lerp(-halfW, halfW, region.shape.xMin);
                    var rx1 = Mathf.Lerp(-halfW, halfW, region.shape.xMax);
                    var ry0 = Mathf.Lerp(-halfH, halfH, region.shape.yMin);
                    var ry1 = Mathf.Lerp(-halfH, halfH, region.shape.yMax);
                    if (wx < rx0 || wx > rx1 || wy < ry0 || wy > ry1) continue;
                    var edge = Mathf.Min(wx - rx0, rx1 - wx, wy - ry0, ry1 - wy);
                    if (edge > dist)
                    {
                        dist = edge;
                        chosen = region;
                    }
                }

                if (chosen == null) continue;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / feather));
                var alpha = Mathf.Lerp(25f, 55f, t);
                var noise = Hash01(x, y, 17) * 0.16f - 0.08f;
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

        PaintRiver(spec.water.mainRiver, true, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx);
        PaintRiver(spec.water.grumeti, false, halfW, halfH, w, h, ppu, floodPx, bankPx, permPx);

        foreach (var pool in spec.water.pools)
        {
            WarnIfNormalizedOutsideRange(pool.x, "water.pools.x");
            WarnIfNormalizedOutsideRange(pool.y, "water.pools.y");
            var wp = ToWorld(pool.x, pool.y, halfW, halfH);
            WarnIfWorldOutsideBounds(wp, halfW, halfH, "water.pools");
            pools.Add(new PoolNodeData { id = pool.id, worldPos = wp, worldRadius = pool.radius, permanent = pool.permanent });
            waterNodes.Add(wp);
            shadeNodes.Add(wp);
            PaintDisc(permPx, w, h, WorldToPixel(wp.x, wp.y, halfW, halfH, w, h), Mathf.Max(1, Mathf.RoundToInt(pool.radius * ppu * 0.5f)), new Color32(44, 126, 218, 235));
        }

        foreach (var wetland in spec.landmarks.wetlands)
        {
            WarnIfNormalizedOutsideRange(wetland.x, "landmarks.wetlands.x");
            WarnIfNormalizedOutsideRange(wetland.y, "landmarks.wetlands.y");
            var wp = ToWorld(wetland.x, wetland.y, halfW, halfH);
            WarnIfWorldOutsideBounds(wp, halfW, halfH, "landmarks.wetlands");
            wetlands.Add(new WetlandNodeData { id = wetland.id, worldPos = wp, worldRadius = wetland.radius });
            PaintDisc(seasonPx, w, h, WorldToPixel(wp.x, wp.y, halfW, halfH, w, h), Mathf.Max(1, Mathf.RoundToInt(wetland.radius * ppu * 0.45f)), new Color32(85, 150, 220, 95));
        }

        floodplainSR = CreateSprite("FloodplainOverlayTexture", ToTex(w, h, floodPx), ppu, FloodplainOrder);
        bankSR = CreateSprite("BankOverlayTexture", ToTex(w, h, bankPx), ppu, BankOrder);
        permanentWaterSR = CreateSprite("PermanentWaterOverlay", ToTex(w, h, permPx), ppu, PermanentWaterOrder);
        seasonalWaterSR = CreateSprite("SeasonalWaterOverlay", ToTex(w, h, seasonPx), ppu, SeasonalWaterOrder);
        Debug.Log($"[SerengetiConformance] WaterTex={w}x{h}@{ppu:0.###}PPU");
    }

    private void PaintRiver(RiverSpec river, bool tapered, float halfW, float halfH, int texW, int texH, float ppu, Color32[] floodPx, Color32[] bankPx, Color32[] waterPx)
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

        var spline = BuildSplineYLookup(worldPoints, 2000);
        if (!spline.Valid)
        {
            return;
        }

        for (var py = 0; py < texH; py++)
        {
            var yWorld = Mathf.Lerp(-halfH, halfH, py / Mathf.Max(1f, texH - 1f));
            if (!spline.TryEvalX(yWorld, out var xWorld, out var progress01))
            {
                continue;
            }

            var width = tapered ? Mathf.Lerp(river.widthNorth, river.widthSouth, progress01) : river.width;
            var center = WorldToPixel(xWorld, yWorld, halfW, halfH, texW, texH);
            PaintScanline(floodPx, texW, texH, center.py, center.px, Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + river.floodplainExtra * 0.5f) * ppu)), new Color32(108, 145, 92, 120));
            PaintScanline(bankPx, texW, texH, center.py, center.px, Mathf.Max(1, Mathf.RoundToInt((width * 0.5f + river.bankExtra) * ppu)), new Color32(73, 108, 61, 170));
            PaintScanline(waterPx, texW, texH, center.py, center.px, Mathf.Max(1, Mathf.RoundToInt(width * 0.5f * ppu)), new Color32(35, 120, 220, 255));
            if ((py % 16) == 0)
            {
                waterNodes.Add(new Vector2(xWorld, yWorld));
            }
        }

        var dest = tapered ? crossingsMain : crossingsGrumeti;
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
    }

    private void BuildKopjes(SerengetiMapSpec spec, float halfW, float halfH)
    {
        var (w, h, ppu) = PickTextureSize(halfW * 2f, halfH * 2f, 3f, 1_800_000);
        var px = new Color32[w * h];
        var rng = RngService.Fork("SERENGETI:KOPJES");
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
            var clusterCount = Mathf.Clamp(Mathf.RoundToInt(node.radius * 0.25f), 8, 24);
            for (var i = 0; i < clusterCount; i++)
            {
                var off = rng.InsideUnitCircle() * node.radius;
                var p = WorldToPixel(world.x + off.x, world.y + off.y, halfW, halfH, w, h);
                var rad = Mathf.Max(1, Mathf.RoundToInt(rng.Range(0.5f, 2f) * ppu));
                PaintDisc(px, w, h, p, rad, new Color32(65, 60, 55, (byte)rng.NextInt(80, 141)));
            }

        }

        kopjesSR = CreateSprite("KopjesOverlayTexture", ToTex(w, h, px), ppu, KopjesOverlayOrder);
    }

    private void BuildDebugOverlays(SerengetiMapSpec spec, ScenarioConfig config, float halfW, float halfH)
    {
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
            CreateLine(debugRoot, new Vector2(x0, y0), new Vector2(x1, y0));
            CreateLine(debugRoot, new Vector2(x1, y0), new Vector2(x1, y1));
            CreateLine(debugRoot, new Vector2(x1, y1), new Vector2(x0, y1));
            CreateLine(debugRoot, new Vector2(x0, y1), new Vector2(x0, y0));
        }

        foreach (var c in crossingNodes)
        {
            var sr = CreateDebugSprite(debugRoot, PrimitiveSpriteLibrary.CircleOutline(48), c.worldPos, Vector2.one * (c.worldRadius * 0.02f));
            sr.color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    private static void CreateLine(Transform parent, Vector2 a, Vector2 b)
    {
        var d = b - a;
        var sr = CreateDebugSprite(parent, PrimitiveSpriteLibrary.CircleFill(8), a + d * 0.5f, new Vector2(d.magnitude, 0.25f));
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        sr.color = new Color(1f, 1f, 1f, 0.7f);
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

    private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

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


    private static void PaintScanline(Color32[] px, int w, int h, int y, int cx, int halfW, Color32 col)
    {
        if (y < 0 || y >= h)
        {
            return;
        }

        var x0 = Mathf.Clamp(cx - halfW, 0, w - 1);
        var x1 = Mathf.Clamp(cx + halfW, 0, w - 1);
        var row = y * w;
        for (var x = x0; x <= x1; x++)
        {
            px[row + x] = Blend(px[row + x], col);
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
        if (string.Equals(biome, "wetland", StringComparison.OrdinalIgnoreCase)) return new Color32(95, 132, 98, 255);
        if (string.Equals(biome, "riverine", StringComparison.OrdinalIgnoreCase)) return new Color32(112, 147, 101, 255);
        if (string.Equals(biome, "kopjes", StringComparison.OrdinalIgnoreCase)) return new Color32(132, 124, 103, 255);
        if (string.Equals(biome, "woodland", StringComparison.OrdinalIgnoreCase)) return new Color32(108, 128, 92, 255);
        return new Color32(140, 132, 100, 255);
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

    private static RiverYLookup BuildSplineYLookup(List<Vector2> worldPoints, int targetSamples)
    {
        if (worldPoints == null || worldPoints.Count < 2)
        {
            return RiverYLookup.Invalid;
        }

        var samplesPerSegment = Mathf.Max(8, Mathf.CeilToInt(targetSamples / (float)Mathf.Max(1, worldPoints.Count - 1)));
        var smooth = SampleCatmullRom(worldPoints, samplesPerSegment);
        if (smooth.Count < 2)
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
