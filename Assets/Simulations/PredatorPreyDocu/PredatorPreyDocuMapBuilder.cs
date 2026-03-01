using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int SavannaBaseOrder = -200;
    private const int SavannaNoiseOrder = -199;
    private const int FloodplainOrder = -190;
    private const int RiverBankOrder = -181;
    private const int PermanentWaterOrder = -180;
    private const int SeasonalWaterOrder = -175;
    private const int GrassOrder = -165;
    private const int TreeOrder = -160;

    private const int TerrainPixelsPerUnit = 4;
    private const int WaterPixelsPerUnit = 6;

    private const float RiverMargin = 14f;
    private const float RiverWidthNorth = 10f;
    private const float RiverWidthSouth = 6f;
    private const float FloodplainExtra = 26f;
    private const float BankExtra = 2.5f;

    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();

    private Transform mapRoot;
    private SpriteRenderer seasonalWaterRenderer;

    // Optional debug-only paint markers for creek starts/endpoints.
    public bool DebugPaintDots = false;

    public IReadOnlyList<Vector2> WaterNodes => waterNodes;
    public IReadOnlyList<Vector2> ShadeNodes => shadeNodes;

    public void Build(Transform parent, ScenarioConfig config, float halfWidth, float halfHeight)
    {
        Clear();

        var cfg = config?.predatorPreyDocu ?? new PredatorPreyDocuConfig();
        cfg.Normalize();

        mapRoot = new GameObject("PredatorPreyDocuMap").transform;
        mapRoot.SetParent(parent, false);

        BuildObjectMap(config, cfg, halfWidth, halfHeight);
    }

    public void UpdateSeasonVisuals(float dryness01)
    {
        if (seasonalWaterRenderer == null)
        {
            return;
        }

        seasonalWaterRenderer.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(dryness01));
    }

    public void Clear()
    {
        if (mapRoot != null)
        {
            Object.Destroy(mapRoot.gameObject);
            mapRoot = null;
        }

        seasonalWaterRenderer = null;
        waterNodes.Clear();
        shadeNodes.Clear();
    }

    private void BuildObjectMap(ScenarioConfig config, PredatorPreyDocuConfig cfg, float providedHalfW, float providedHalfH)
    {
        var worldCfg = config?.world;
        var halfW = Mathf.Max(1f, worldCfg != null ? worldCfg.arenaWidth * 0.5f : providedHalfW);
        var halfH = Mathf.Max(1f, worldCfg != null ? worldCfg.arenaHeight * 0.5f : providedHalfH);
        var arenaW = halfW * 2f;
        var arenaH = halfH * 2f;

        var rngMap = RngService.Fork("SIM:PredatorPreyDocu:MAP");
        var rngWaterMain = RngService.Fork("SIM:PredatorPreyDocu:WATER:MAIN");
        var rngSeasonal = RngService.Fork("SIM:PredatorPreyDocu:WATER:SEASONAL");
        var rngScatter = RngService.Fork("SIM:PredatorPreyDocu:SCATTER");
        _ = RngService.Fork("SIM:PredatorPreyDocu:WATER");

        var riverModel = new RiverModel(rngWaterMain);

        BuildSavannaBase(arenaW, arenaH);
        BuildSavannaNoiseOverlay(arenaW, arenaH, halfW, halfH, riverModel, rngScatter);
        BuildRiverCorridorStrips(halfW, halfH, riverModel);
        var waterSummary = BuildWaterOverlays(halfW, halfH, cfg.map, riverModel, rngSeasonal);
        BuildGrass(halfW, halfH, rngScatter);
        BuildTrees(halfW, halfH, cfg, riverModel, rngMap);

        Debug.Log($"PredatorPreyDocuMap build summary arenaW={arenaW:F1} arenaH={arenaH:F1} waterTex={waterSummary.texW}x{waterSummary.texH} creeks={waterSummary.creekCount} riverMargin={RiverMargin:F1}");
    }

    private void BuildSavannaBase(float arenaW, float arenaH)
    {
        CreateSprite(
            mapRoot,
            "SavannaBase",
            PrimitiveSpriteLibrary.RoundedRectFill(64),
            Vector2.zero,
            new Vector2(arenaW + 2f, arenaH + 2f),
            new Color(0.79f, 0.69f, 0.42f, 1f),
            SavannaBaseOrder);
    }

    private void BuildSavannaNoiseOverlay(float arenaW, float arenaH, float halfW, float halfH, RiverModel riverModel, IRng rngScatter)
    {
        var texW = Mathf.Max(64, Mathf.RoundToInt(arenaW * TerrainPixelsPerUnit));
        var texH = Mathf.Max(64, Mathf.RoundToInt(arenaH * TerrainPixelsPerUnit));
        var texture = CreateTexture(texW, texH);
        var pixels = new Color32[texW * texH];

        var area = arenaW * arenaH;
        var speckleCount = Mathf.Clamp(Mathf.RoundToInt(area * 0.30f), 8000, 20000);

        for (var i = 0; i < speckleCount; i++)
        {
            var px = rngScatter.NextInt(0, texW);
            var py = rngScatter.NextInt(0, texH);

            var x = Mathf.Lerp(-halfW, halfW, px / Mathf.Max(1f, texW - 1f));
            var y = Mathf.Lerp(-halfH, halfH, py / Mathf.Max(1f, texH - 1f));
            var riverX = riverModel.EvaluateX(y, halfW, halfH);
            var corridorDistance = Mathf.Abs(x - riverX);
            var corridorBoost = Mathf.Clamp01(1f - (corridorDistance / Mathf.Max(8f, (FloodplainExtra * 1.3f))));

            if (rngScatter.Value() > (0.35f + (corridorBoost * 0.55f)))
            {
                continue;
            }

            var dotRadius = rngScatter.Value() < (0.55f + (0.25f * corridorBoost)) ? 1 : 2;
            var greenish = rngScatter.Value() < (0.52f + (0.20f * corridorBoost));
            var color = greenish
                ? new Color32((byte)rngScatter.NextInt(106, 132), (byte)rngScatter.NextInt(114, 148), (byte)rngScatter.NextInt(70, 98), (byte)rngScatter.NextInt(16, 42))
                : new Color32((byte)rngScatter.NextInt(132, 165), (byte)rngScatter.NextInt(112, 140), (byte)rngScatter.NextInt(67, 95), (byte)rngScatter.NextInt(14, 34));
            PaintDiscAlphaBlend(pixels, texW, texH, px, py, dotRadius, color);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), TerrainPixelsPerUnit);
        CreateSprite(mapRoot, "SavannaNoiseOverlay", sprite, Vector2.zero, Vector2.one, Color.white, SavannaNoiseOrder);
    }

    private void BuildRiverCorridorStrips(float halfW, float halfH, RiverModel riverModel)
    {
        var stripsRoot = new GameObject("RiverCorridorStrips").transform;
        stripsRoot.SetParent(mapRoot, false);

        var slices = 160;
        var arenaH = halfH * 2f;
        var dy = arenaH / slices;

        for (var i = 0; i < slices; i++)
        {
            var t = (i + 0.5f) / slices;
            var y = Mathf.Lerp(halfH, -halfH, t);
            var x = riverModel.EvaluateX(y, halfW, halfH);
            var riverWidth = riverModel.EvaluateWidth(y, halfH);
            var floodW = Mathf.Min((halfW * 2f) - 0.5f, riverWidth + FloodplainExtra);
            var bankW = Mathf.Min(floodW, riverWidth + (BankExtra * 2f));

            CreateSprite(
                stripsRoot,
                $"Floodplain_{i:000}",
                PrimitiveSpriteLibrary.RoundedRectFill(24),
                new Vector2(x, y),
                new Vector2(Mathf.Max(0.2f, floodW), dy + 0.3f),
                new Color(0.57f, 0.64f, 0.36f, 0.95f),
                FloodplainOrder);

            CreateSprite(
                stripsRoot,
                $"RiverBank_{i:000}",
                PrimitiveSpriteLibrary.RoundedRectFill(24),
                new Vector2(x, y),
                new Vector2(Mathf.Max(0.2f, bankW), dy + 0.3f),
                new Color(0.41f, 0.52f, 0.30f, 0.95f),
                RiverBankOrder);
        }
    }

    private (int texW, int texH, int creekCount) BuildWaterOverlays(float halfW, float halfH, Map mapCfg, RiverModel riverModel, IRng rngSeasonal)
    {
        var arenaW = halfW * 2f;
        var arenaH = halfH * 2f;
        var texW = Mathf.Max(128, Mathf.RoundToInt(arenaW * WaterPixelsPerUnit));
        var texH = Mathf.Max(128, Mathf.RoundToInt(arenaH * WaterPixelsPerUnit));

        var permanentTex = CreateTexture(texW, texH);
        var seasonalTex = CreateTexture(texW, texH);

        var permanentPixels = new Color32[texW * texH];
        var seasonalPixels = new Color32[texW * texH];

        PaintMainRiver(permanentPixels, texW, texH, halfW, halfH, riverModel);
        var creekCount = PaintSeasonalCreeksAndPonds(seasonalPixels, texW, texH, halfW, halfH, mapCfg, riverModel, rngSeasonal);

        permanentTex.SetPixels32(permanentPixels);
        permanentTex.Apply(false, false);
        seasonalTex.SetPixels32(seasonalPixels);
        seasonalTex.Apply(false, false);

        var permanentSprite = Sprite.Create(permanentTex, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), WaterPixelsPerUnit);
        var seasonalSprite = Sprite.Create(seasonalTex, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), WaterPixelsPerUnit);

        CreateSprite(mapRoot, "PermanentWaterOverlay", permanentSprite, Vector2.zero, Vector2.one, Color.white, PermanentWaterOrder);
        seasonalWaterRenderer = CreateSprite(mapRoot, "SeasonalWaterOverlay", seasonalSprite, Vector2.zero, Vector2.one, Color.white, SeasonalWaterOrder);

        return (texW, texH, creekCount);
    }

    private void PaintMainRiver(Color32[] pixels, int texW, int texH, float halfW, float halfH, RiverModel riverModel)
    {
        var riverColor = new Color32(50, 140, 220, 255);
        const int samples = 600;

        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var y = Mathf.Lerp(halfH, -halfH, t);
            var x = riverModel.EvaluateX(y, halfW, halfH);
            var riverWidth = riverModel.EvaluateWidth(y, halfH);
            var radiusPx = Mathf.Max(1, Mathf.RoundToInt((riverWidth * 0.5f) * WaterPixelsPerUnit));
            var (px, py) = WorldToPixel(x, y, halfW, halfH, texW, texH);

            PaintDisc(pixels, texW, texH, px, py, radiusPx, riverColor);
            waterNodes.Add(new Vector2(x, y));
        }
    }

    private int PaintSeasonalCreeksAndPonds(Color32[] pixels, int texW, int texH, float halfW, float halfH, Map mapCfg, RiverModel riverModel, IRng rngSeasonal)
    {
        var creekCount = Mathf.Clamp(mapCfg?.creekCount ?? 6, 0, 64);
        var baseCreekWidth = Mathf.Clamp(mapCfg?.creekWidth ?? 2f, 1.6f, 2.2f);
        var creekColor = new Color32(82, 168, 246, 255);

        for (var c = 0; c < creekCount; c++)
        {
            var absX = rngSeasonal.Range(halfW * 0.35f, halfW * 0.90f);
            var side = rngSeasonal.Value() < 0.5f ? -1f : 1f;
            var x = Mathf.Clamp(absX * side, -halfW, halfW);
            var y = rngSeasonal.Range(-halfH * 0.80f, halfH * 0.80f);

            var (prevPx, prevPy) = WorldToPixel(x, y, halfW, halfH, texW, texH);
            if (DebugPaintDots)
            {
                PaintDisc(pixels, texW, texH, prevPx, prevPy, 2, new Color32(255, 120, 120, 255));
            }

            var steps = rngSeasonal.NextInt(160, 241);
            var stepLen = rngSeasonal.Range(0.55f, 0.95f);
            var baseRadiusPx = Mathf.Max(1, Mathf.RoundToInt((baseCreekWidth * 0.5f) * WaterPixelsPerUnit));
            var creekRadiusPx = baseRadiusPx;
            var widthChangeInterval = rngSeasonal.NextInt(18, 25);

            for (var step = 0; step < steps; step++)
            {
                if ((step > 0) && (step % widthChangeInterval == 0))
                {
                    creekRadiusPx = Mathf.Max(1, baseRadiusPx + rngSeasonal.NextInt(-1, 2));
                    widthChangeInterval = rngSeasonal.NextInt(18, 25);
                }

                var riverX = riverModel.EvaluateX(y, halfW, halfH);
                var riverWidth = riverModel.EvaluateWidth(y, halfH);
                var floodW = riverWidth + FloodplainExtra;
                var distanceToCorridor = Mathf.Abs(x - riverX);
                if (distanceToCorridor <= (floodW * 0.5f + 1f))
                {
                    break;
                }

                var dirX = Mathf.Sign(riverX - x);
                var driftSouth = -0.02f;
                var wiggleX = rngSeasonal.Range(-0.20f, 0.20f);
                var wiggleY = rngSeasonal.Range(-0.16f, 0.16f);

                x += (dirX * stepLen) + wiggleX;
                y += driftSouth + wiggleY;

                x = Mathf.Clamp(x, -halfW, halfW);
                y = Mathf.Clamp(y, -halfH, halfH);

                var (px, py) = WorldToPixel(x, y, halfW, halfH, texW, texH);
                PaintStroke(pixels, texW, texH, prevPx, prevPy, px, py, creekRadiusPx, creekColor);
                prevPx = px;
                prevPy = py;
            }

            var pondCount = rngSeasonal.NextInt(1, 3);
            for (var p = 0; p < pondCount; p++)
            {
                var pondOffset = rngSeasonal.InsideUnitCircle() * 1.4f;
                var pondX = Mathf.Clamp(x + pondOffset.x, -halfW, halfW);
                var pondY = Mathf.Clamp(y + pondOffset.y, -halfH, halfH);
                var pondRadiusWorld = rngSeasonal.Range(3f, 8f);
                var pondRadiusPx = Mathf.Max(1, Mathf.RoundToInt(pondRadiusWorld * WaterPixelsPerUnit));
                var (pondPx, pondPy) = WorldToPixel(pondX, pondY, halfW, halfH, texW, texH);
                PaintDisc(pixels, texW, texH, pondPx, pondPy, pondRadiusPx, new Color32(76, 158, 245, 255));
                waterNodes.Add(new Vector2(pondX, pondY));
            }

            if (DebugPaintDots)
            {
                PaintDisc(pixels, texW, texH, prevPx, prevPy, 2, new Color32(255, 220, 80, 255));
            }
        }

        return creekCount;
    }

    private void BuildGrass(float halfW, float halfH, IRng rngScatter)
    {
        var count = Mathf.Clamp(Mathf.RoundToInt(halfW * halfH * 0.35f), 160, 700);

        for (var i = 0; i < count; i++)
        {
            var pos = new Vector2(rngScatter.Range(-halfW, halfW), rngScatter.Range(-halfH, halfH));
            var scale = rngScatter.Range(0.12f, 0.28f);
            var tint = rngScatter.Range(-0.05f, 0.05f);
            var color = new Color(0.33f + tint, 0.53f + (tint * 0.6f), 0.24f + (tint * 0.4f), rngScatter.Range(0.18f, 0.45f));
            CreateSprite(mapRoot, $"Grass_{i:0000}", PrimitiveSpriteLibrary.CircleFill(24), pos, new Vector2(scale, scale), color, GrassOrder);
        }
    }

    private void BuildTrees(float halfW, float halfH, PredatorPreyDocuConfig cfg, RiverModel riverModel, IRng rngMap)
    {
        var mapCfg = cfg.map ?? new Map();
        var visuals = cfg.visuals ?? new Visuals();
        var clusterCount = Mathf.Max(1, mapCfg.treeClusterCount);
        var treeScale = Mathf.Max(0.5f, visuals.treeScale);

        for (var c = 0; c < clusterCount; c++)
        {
            var y = rngMap.Range(-halfH * 0.92f, halfH * 0.92f);
            var riverX = riverModel.EvaluateX(y, halfW, halfH);
            var floodHalf = (riverModel.EvaluateWidth(y, halfH) + FloodplainExtra) * 0.5f;

            var side = rngMap.Value() < 0.5f ? -1f : 1f;
            var innerX = Mathf.Clamp(riverX + (side * (floodHalf + 1.8f)), -halfW * 0.95f, halfW * 0.95f);
            var outerX = side < 0f ? -halfW * 0.95f : halfW * 0.95f;
            var lo = Mathf.Min(innerX, outerX);
            var hi = Mathf.Max(innerX, outerX);
            var x = rngMap.Range(lo, hi);

            var center = new Vector2(Mathf.Clamp(x, -halfW, halfW), Mathf.Clamp(y, -halfH, halfH));
            var trees = rngMap.NextInt(3, 8);

            for (var i = 0; i < trees; i++)
            {
                var offset = rngMap.InsideUnitCircle() * rngMap.Range(0.2f, 2.2f);
                var pos = center + offset;
                pos.x = Mathf.Clamp(pos.x, -halfW + 0.2f, halfW - 0.2f);
                pos.y = Mathf.Clamp(pos.y, -halfH + 0.2f, halfH - 0.2f);
                shadeNodes.Add(pos);

                var canopy = rngMap.Range(0.45f, 1.0f) * treeScale;
                CreateSprite(mapRoot, $"TreeShadow_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos + new Vector2(0.12f, -0.09f), new Vector2(canopy * 1.2f, canopy * 0.8f), new Color(0f, 0f, 0f, 0.25f), TreeOrder);
                CreateSprite(mapRoot, $"TreeCanopy_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos, new Vector2(canopy, canopy), new Color(0.14f, 0.35f, 0.16f, 1f), TreeOrder + 1);
            }
        }
    }

    private static Texture2D CreateTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static (int px, int py) WorldToPixel(float x, float y, float halfW, float halfH, int texW, int texH)
    {
        var u = (x + halfW) / (2f * halfW);
        var v = (y + halfH) / (2f * halfH);
        var px = Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1);
        var py = Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1);
        return (px, py);
    }

    private static void PaintDisc(Color32[] pixels, int texW, int texH, int cx, int cy, int radius, Color32 color)
    {
        var r2 = radius * radius;
        for (var y = cy - radius; y <= cy + radius; y++)
        {
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if ((dx * dx) + (dy * dy) > r2)
                {
                    continue;
                }

                var clampedX = Mathf.Clamp(x, 0, texW - 1);
                var clampedY = Mathf.Clamp(y, 0, texH - 1);
                pixels[clampedX + (clampedY * texW)] = color;
            }
        }
    }

    private static void PaintStroke(Color32[] pixels, int texW, int texH, int x0, int y0, int x1, int y1, int radius, Color32 color)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
        var stepSize = Mathf.Max(1f, radius * 0.5f);
        var steps = Mathf.Max(1, Mathf.CeilToInt(dist / stepSize));

        for (var i = 0; i <= steps; i++)
        {
            var a = i / (float)steps;
            var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, a));
            var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, a));
            PaintDisc(pixels, texW, texH, x, y, radius, color);
        }
    }

    private static void PaintDiscAlphaBlend(Color32[] pixels, int texW, int texH, int cx, int cy, int radius, Color32 color)
    {
        var r2 = radius * radius;
        for (var y = cy - radius; y <= cy + radius; y++)
        {
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if ((dx * dx) + (dy * dy) > r2)
                {
                    continue;
                }

                var clampedX = Mathf.Clamp(x, 0, texW - 1);
                var clampedY = Mathf.Clamp(y, 0, texH - 1);
                var idx = clampedX + (clampedY * texW);
                pixels[idx] = Color32.Lerp(pixels[idx], color, color.a / 255f);
            }
        }
    }

    private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 scale, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = sortingOrder;
        sr.color = color;
        return sr;
    }

    private sealed class RiverModel
    {
        private readonly float f1;
        private readonly float f2;
        private readonly float f3;
        private readonly float p1;
        private readonly float p2;
        private readonly float p3;

        public RiverModel(IRng rngWaterMain)
        {
            f1 = rngWaterMain.Range(0.6f, 1.1f);
            f2 = rngWaterMain.Range(1.2f, 2.0f);
            f3 = rngWaterMain.Range(3.0f, 5.0f);
            p1 = rngWaterMain.Value();
            p2 = rngWaterMain.Value();
            p3 = rngWaterMain.Value();
        }

        public float EvaluateX(float y, float halfW, float halfH)
        {
            var t = Mathf.Clamp01((halfH - y) / (2f * Mathf.Max(0.001f, halfH)));
            var ampBase = Mathf.Lerp(10f, 5f, t);
            var ampSwirl = 9f * Mathf.SmoothStep(0.80f, 1f, t);
            var x = (ampBase * Mathf.Sin(2f * Mathf.PI * ((f1 * t) + p1)))
                    + ((ampBase * 0.6f) * Mathf.Sin(2f * Mathf.PI * ((f2 * t) + p2)))
                    + (ampSwirl * Mathf.Sin(2f * Mathf.PI * ((f3 * t * t) + p3)));
            return Mathf.Clamp(x, -halfW + RiverMargin, halfW - RiverMargin);
        }

        public float EvaluateWidth(float y, float halfH)
        {
            var t = Mathf.Clamp01((halfH - y) / (2f * Mathf.Max(0.001f, halfH)));
            return Mathf.Lerp(RiverWidthNorth, RiverWidthSouth, Mathf.SmoothStep(0.10f, 1f, t));
        }
    }
}
