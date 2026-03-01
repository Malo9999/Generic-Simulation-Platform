using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int SavannaOrder = -200;
    private const int PermanentWaterOrder = -180;
    private const int SeasonalWaterOrder = -175;
    private const int GrassOrder = -170;
    private const int TreeOrder = -160;
    private const int WaterPixelsPerUnit = 6;

    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();

    private Transform mapRoot;
    private SpriteRenderer seasonalWaterRenderer;

    public IReadOnlyList<Vector2> WaterNodes => waterNodes;
    public IReadOnlyList<Vector2> ShadeNodes => shadeNodes;

    public void Build(Transform parent, ScenarioConfig config, float halfWidth, float halfHeight)
    {
        Clear();

        var docu = config?.predatorPreyDocu ?? new PredatorPreyDocuConfig();
        docu.Normalize();

        mapRoot = new GameObject("PredatorPreyDocuMap").transform;
        mapRoot.SetParent(parent, false);

        BuildObjectMap(halfWidth, halfHeight, docu);
    }

    public void UpdateSeasonVisuals(float dryness01)
    {
        if (seasonalWaterRenderer == null)
        {
            return;
        }

        var c = seasonalWaterRenderer.color;
        c.a = 1f - Mathf.Clamp01(dryness01);
        seasonalWaterRenderer.color = c;
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

    private void BuildObjectMap(float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg)
    {
        var arenaW = halfWidth * 2f;
        var arenaH = halfHeight * 2f;
        var mapCfg = cfg.map ?? new Map();

        BuildSavannaBackground(arenaW, arenaH);
        BuildWaterOverlays(halfWidth, halfHeight, mapCfg);
        BuildGrass(halfWidth, halfHeight);
        BuildTrees(halfWidth, halfHeight, cfg);
    }

    private void BuildSavannaBackground(float arenaW, float arenaH)
    {
        CreateSprite(
            mapRoot,
            "SavannaBackground",
            PrimitiveSpriteLibrary.RoundedRectFill(64),
            Vector2.zero,
            new Vector2(arenaW + 2f, arenaH + 2f),
            new Color(0.79f, 0.69f, 0.42f, 1f),
            SavannaOrder);

        BuildSavannaNoiseOverlay(arenaW, arenaH);
    }

    private void BuildSavannaNoiseOverlay(float arenaW, float arenaH)
    {
        var texW = Mathf.Max(64, Mathf.RoundToInt(arenaW * WaterPixelsPerUnit));
        var texH = Mathf.Max(64, Mathf.RoundToInt(arenaH * WaterPixelsPerUnit));
        var overlayTex = CreateWaterTexture(texW, texH);
        var pixels = new Color32[texW * texH];
        var rng = RngService.Fork("SIM:PredatorPreyDocu:SAVANNA:OVERLAY");
        var floodplainBandPx = Mathf.Max(3f, texW * 0.10f);
        var centerX = texW * 0.5f;
        var speckleCount = Mathf.Clamp(Mathf.RoundToInt((texW * texH) * 0.07f), 5000, 12000);

        for (var i = 0; i < speckleCount; i++)
        {
            var x = rng.NextInt(0, texW);
            var y = rng.NextInt(0, texH);
            var offsetToCenter = Mathf.Abs(x - centerX);
            var floodplainBoost = Mathf.Clamp01(1f - (offsetToCenter / floodplainBandPx));
            var radius = rng.Value() < (0.35f + (floodplainBoost * 0.35f)) ? 2 : 1;
            var c = rng.Value() < (0.5f + (floodplainBoost * 0.2f))
                ? new Color32((byte)rng.NextInt(113, 132), (byte)rng.NextInt(122, 148), (byte)rng.NextInt(78, 100), (byte)rng.NextInt(18, 40))
                : new Color32((byte)rng.NextInt(140, 162), (byte)rng.NextInt(117, 139), (byte)rng.NextInt(69, 92), (byte)rng.NextInt(14, 34));
            PaintDiscAlphaBlend(pixels, texW, texH, x, y, radius, c);
        }

        overlayTex.SetPixels32(pixels);
        overlayTex.Apply(false, false);

        var overlaySprite = Sprite.Create(overlayTex, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), WaterPixelsPerUnit);
        CreateSprite(mapRoot, "SavannaNoiseOverlay", overlaySprite, Vector2.zero, Vector2.one, Color.white, SavannaOrder + 1);
    }

    private void BuildWaterOverlays(float halfWidth, float halfHeight, Map mapCfg)
    {
        var arenaW = halfWidth * 2f;
        var arenaH = halfHeight * 2f;
        var texW = Mathf.Max(64, Mathf.RoundToInt(arenaW * WaterPixelsPerUnit));
        var texH = Mathf.Max(64, Mathf.RoundToInt(arenaH * WaterPixelsPerUnit));

        var permanentTex = CreateWaterTexture(texW, texH);
        var seasonalTex = CreateWaterTexture(texW, texH);
        var permanentPixels = new Color32[texW * texH];
        var seasonalPixels = new Color32[texW * texH];

        var mainRng = RngService.Fork("SIM:PredatorPreyDocu:WATER:MAIN");
        var riverCurve = new RiverCurve(mainRng);

        PaintMainRiver(permanentPixels, texW, texH, halfWidth, halfHeight, mapCfg, riverCurve);
        PaintSeasonalCreeksAndPonds(seasonalPixels, texW, texH, halfWidth, halfHeight, mapCfg, riverCurve);

        permanentTex.SetPixels32(permanentPixels);
        permanentTex.Apply(false, false);
        seasonalTex.SetPixels32(seasonalPixels);
        seasonalTex.Apply(false, false);

        var permanentSprite = Sprite.Create(permanentTex, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), WaterPixelsPerUnit);
        var seasonalSprite = Sprite.Create(seasonalTex, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f), WaterPixelsPerUnit);

        CreateSprite(mapRoot, "PermanentWaterOverlay", permanentSprite, Vector2.zero, Vector2.one, Color.white, PermanentWaterOrder);
        seasonalWaterRenderer = CreateSprite(mapRoot, "SeasonalWaterOverlay", seasonalSprite, Vector2.zero, Vector2.one, Color.white, SeasonalWaterOrder);
    }

    private void PaintMainRiver(Color32[] pixels, int texW, int texH, float halfWidth, float halfHeight, Map mapCfg, RiverCurve riverCurve)
    {
        const int samples = 420;
        var margin = Mathf.Min(14f, Mathf.Max(1f, halfWidth - 0.05f));
        var northWidth = Mathf.Max(2f, mapCfg.riverWidth);
        var southWidth = Mathf.Max(1.5f, northWidth * 0.6f);
        var color = new Color32(36, 120, 233, 255);

        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var y = Mathf.Lerp(halfHeight, -halfHeight, t);
            var x = riverCurve.EvaluateX(t, halfWidth, margin);
            var w = Mathf.Lerp(northWidth, southWidth, Mathf.SmoothStep(0.10f, 1f, t));
            var rPx = Mathf.Max(1, Mathf.RoundToInt((w * 0.5f) * WaterPixelsPerUnit));

            var (px, py) = WorldToPixel(x, y, halfWidth, halfHeight, texW, texH);
            PaintDisc(pixels, texW, texH, px, py, rPx, color);

            if (i % 28 == 0)
            {
                waterNodes.Add(new Vector2(x, y));
            }
        }
    }

    private void PaintSeasonalCreeksAndPonds(Color32[] pixels, int texW, int texH, float halfWidth, float halfHeight, Map mapCfg, RiverCurve riverCurve)
    {
        var seasonalRng = RngService.Fork("SIM:PredatorPreyDocu:WATER:SEASONAL");
        var creekCount = Mathf.Clamp(mapCfg.creekCount, 0, 12);
        var creekWidth = Mathf.Clamp(mapCfg.creekWidth, 1.4f, 2.2f);
        var creekColor = new Color32(62, 145, 242, 255);

        for (var k = 0; k < creekCount; k++)
        {
            var side = seasonalRng.Value() < 0.5f ? -1f : 1f;
            var x = side * seasonalRng.Range(halfWidth * 0.35f, halfWidth * 0.90f);
            var y = seasonalRng.Range(-halfHeight * 0.80f, halfHeight * 0.80f);
            var steps = seasonalRng.NextInt(140, 221);
            var baseCreekRadiusPx = Mathf.Max(1, Mathf.RoundToInt((creekWidth * 0.5f) * WaterPixelsPerUnit));
            var creekRadiusPx = baseCreekRadiusPx;
            var variationStepInterval = seasonalRng.NextInt(10, 21);
            var (prevPx, prevPy) = WorldToPixel(x, y, halfWidth, halfHeight, texW, texH);

            for (var step = 0; step < steps; step++)
            {
                var t = Mathf.InverseLerp(halfHeight, -halfHeight, y);
                var riverX = riverCurve.EvaluateX(t, halfWidth, Mathf.Min(14f, Mathf.Max(1f, halfWidth - 0.05f)));

                var dirX = (riverX - x) * 0.11f;
                var dirY = ((-0.2f * y) / Mathf.Max(1f, halfHeight)) - 0.035f;
                var wiggleX = seasonalRng.Range(-0.35f, 0.35f);
                var wiggleY = seasonalRng.Range(-0.22f, 0.22f);

                x += dirX + wiggleX;
                y += dirY + wiggleY;

                x = Mathf.Clamp(x, -halfWidth, halfWidth);
                y = Mathf.Clamp(y, -halfHeight, halfHeight);

                var (px, py) = WorldToPixel(x, y, halfWidth, halfHeight, texW, texH);

                if ((step > 0) && (step % variationStepInterval == 0))
                {
                    creekRadiusPx = Mathf.Max(1, baseCreekRadiusPx + seasonalRng.NextInt(-1, 2));
                    variationStepInterval = seasonalRng.NextInt(10, 21);
                }

                PaintStroke(pixels, texW, texH, prevPx, prevPy, px, py, creekRadiusPx, creekColor);
                prevPx = px;
                prevPy = py;
            }

            var pondCount = seasonalRng.NextInt(1, 3);
            for (var p = 0; p < pondCount; p++)
            {
                var off = seasonalRng.InsideUnitCircle() * 1.2f;
                var pondX = Mathf.Clamp(x + off.x, -halfWidth, halfWidth);
                var pondY = Mathf.Clamp(y + off.y, -halfHeight, halfHeight);
                var pondRadius = seasonalRng.Range(3f, 8f);
                var pondRPx = Mathf.Max(1, Mathf.RoundToInt(pondRadius * WaterPixelsPerUnit));
                var (px, py) = WorldToPixel(pondX, pondY, halfWidth, halfHeight, texW, texH);
                PaintDisc(pixels, texW, texH, px, py, pondRPx, new Color32(76, 158, 245, 255));
            }
        }
    }

    private static Texture2D CreateWaterTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture;
    }

    private void BuildGrass(float halfWidth, float halfHeight)
    {
        var rng = RngService.Fork("SIM:PredatorPreyDocu:GRASS");
        var count = Mathf.Clamp(Mathf.RoundToInt(halfWidth * halfHeight * 0.5f), 180, 850);
        for (var i = 0; i < count; i++)
        {
            var pos = new Vector2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfHeight, halfHeight));
            var scale = rng.Range(0.12f, 0.32f);
            var tint = rng.Range(-0.06f, 0.06f);
            var c = new Color(0.31f + tint, 0.54f + (tint * 0.6f), 0.25f + (tint * 0.5f), rng.Range(0.22f, 0.55f));
            CreateSprite(mapRoot, $"Grass_{i:0000}", PrimitiveSpriteLibrary.CircleFill(32), pos, new Vector2(scale, scale), c, GrassOrder);
        }
    }

    private void BuildTrees(float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg)
    {
        var mapCfg = cfg.map ?? new Map();
        var visuals = cfg.visuals ?? new Visuals();
        var clusterCount = Mathf.Max(1, mapCfg.treeClusterCount);
        var treeScale = Mathf.Max(0.5f, visuals.treeScale);
        var rng = RngService.Fork("SIM:PredatorPreyDocu:TREES");

        for (var c = 0; c < clusterCount; c++)
        {
            var center = new Vector2(rng.Range(-halfWidth * 0.92f, halfWidth * 0.92f), rng.Range(-halfHeight * 0.92f, halfHeight * 0.92f));
            var trees = rng.NextInt(3, 8);
            for (var i = 0; i < trees; i++)
            {
                var offset = rng.InsideUnitCircle() * rng.Range(0.2f, 2.2f);
                var pos = center + offset;
                pos.x = Mathf.Clamp(pos.x, -halfWidth + 0.2f, halfWidth - 0.2f);
                pos.y = Mathf.Clamp(pos.y, -halfHeight + 0.2f, halfHeight - 0.2f);
                shadeNodes.Add(pos);

                var canopy = rng.Range(0.45f, 1.0f) * treeScale;
                CreateSprite(mapRoot, $"TreeShadow_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos + new Vector2(0.12f, -0.09f), new Vector2(canopy * 1.2f, canopy * 0.8f), new Color(0f, 0f, 0f, 0.25f), TreeOrder);
                CreateSprite(mapRoot, $"TreeCanopy_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos, new Vector2(canopy, canopy), new Color(0.14f, 0.35f, 0.16f, 1f), TreeOrder + 1);
            }
        }
    }

    private static (int px, int py) WorldToPixel(float x, float y, float halfWidth, float halfHeight, int texW, int texH)
    {
        var px = Mathf.Clamp(Mathf.RoundToInt(((x + halfWidth) / (2f * halfWidth)) * (texW - 1)), 0, texW - 1);
        var py = Mathf.Clamp(Mathf.RoundToInt(((y + halfHeight) / (2f * halfHeight)) * (texH - 1)), 0, texH - 1);
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
                pixels[(clampedY * texW) + clampedX] = color;
            }
        }
    }

    private static void PaintStroke(Color32[] pixels, int texW, int texH, int x0, int y0, int x1, int y1, int radius, Color32 color)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
        var steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(1f, radius * 0.5f)));

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
                var idx = (clampedY * texW) + clampedX;
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

    private sealed class RiverCurve
    {
        private readonly float f1;
        private readonly float f2;
        private readonly float f3;
        private readonly float p1;
        private readonly float p2;
        private readonly float p3;

        public RiverCurve(IRng rng)
        {
            f1 = rng.Range(0.6f, 1.1f);
            f2 = rng.Range(1.2f, 2.0f);
            f3 = rng.Range(3.0f, 5.0f);
            p1 = rng.Value();
            p2 = rng.Value();
            p3 = rng.Value();
        }

        public float EvaluateX(float t, float halfWidth, float margin)
        {
            t = Mathf.Clamp01(t);
            var ampBase = Mathf.Lerp(10f, 5f, t);
            var ampSwirl = 9f * Mathf.SmoothStep(0.75f, 1.0f, t);
            var x = (ampBase * Mathf.Sin(2f * Mathf.PI * ((f1 * t) + p1)))
                    + ((ampBase * 0.6f) * Mathf.Sin(2f * Mathf.PI * ((f2 * t) + p2)))
                    + (ampSwirl * Mathf.Sin(2f * Mathf.PI * ((f3 * t * t) + p3)));
            return Mathf.Clamp(x, -halfWidth + margin, halfWidth - margin);
        }
    }
}
