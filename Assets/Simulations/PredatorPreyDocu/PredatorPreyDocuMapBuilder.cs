using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int BakedMapOrder = -200;
    private const int BakedPixelsPerUnit = 12;

    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();

    private Transform mapRoot;
    public IReadOnlyList<Vector2> WaterNodes => waterNodes;
    public IReadOnlyList<Vector2> ShadeNodes => shadeNodes;

    public void Build(Transform parent, ScenarioConfig config, float halfWidth, float halfHeight)
    {
        Clear();

        var docu = config?.predatorPreyDocu ?? new PredatorPreyDocuConfig();
        docu.Normalize();

        mapRoot = new GameObject("PredatorPreyDocuMap").transform;
        mapRoot.SetParent(parent, false);

        var mapRng = RngService.Fork("SIM:PredatorPreyDocu:MAP");
        BuildBakedMap(halfWidth, halfHeight, docu, mapRng);
    }

    public void UpdateSeasonVisuals(float dryness01)
    {
        _ = Mathf.Clamp01(dryness01);
    }

    public void Clear()
    {
        if (mapRoot != null)
        {
            Object.Destroy(mapRoot.gameObject);
            mapRoot = null;
        }

        waterNodes.Clear();
        shadeNodes.Clear();
    }

    private void BuildBakedMap(float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg, IRng rng)
    {
        var arenaW = halfWidth * 2f;
        var arenaH = halfHeight * 2f;
        var texW = Mathf.Max(32, Mathf.RoundToInt(arenaW * BakedPixelsPerUnit));
        var texH = Mathf.Max(32, Mathf.RoundToInt(arenaH * BakedPixelsPerUnit));

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[texW * texH];
        var dryColor = new Color(0.79f, 0.69f, 0.42f, 1f);
        var greenishColor = new Color(0.53f, 0.67f, 0.39f, 1f);
        var noiseSeed = rng.NextInt(0, int.MaxValue);

        FillSavannaBackground(pixels, texW, texH, dryColor, greenishColor, noiseSeed);

        const int segmentCount = 280;
        const float xMargin = 10f;
        var f1 = rng.Range(0.55f, 1.05f);
        var f2 = rng.Range(1.15f, 1.95f);
        var f3 = rng.Range(3.2f, 5.2f);
        var p1 = rng.Value();
        var p2 = rng.Value();
        var p3 = rng.Value();

        var riverPoints = new Vector2[segmentCount + 1];

        var mapConfig = cfg.map ?? new Map();
        var baseRiverWidth = Mathf.Max(0.8f, mapConfig.riverWidth);
        var floodExtra = Mathf.Max(0.8f, mapConfig.floodplainWidth);

        var floodColor = new Color32(128, 184, 94, 255);
        var bankColor = new Color32(77, 112, 56, 255);
        var riverColor = new Color32(36, 120, 235, 255);

        for (var i = 0; i <= segmentCount; i++)
        {
            var t = i / (float)segmentCount;
            var y = Mathf.Lerp(halfHeight, -halfHeight, t);
            var ampBase = Mathf.Lerp(6f, 3f, t);
            var ampDelta = 5f * Mathf.SmoothStep(0.75f, 1f, t);

            var x = (ampBase * Mathf.Sin(2f * Mathf.PI * ((f1 * t) + p1)))
                + ((ampBase * 0.6f) * Mathf.Sin(2f * Mathf.PI * ((f2 * t) + p2)))
                + (ampDelta * Mathf.Sin(2f * Mathf.PI * ((f3 * t * t) + p3)));
            x = Mathf.Clamp(x, -halfWidth + xMargin, halfWidth - xMargin);

            var pt = new Vector2(x, y);
            riverPoints[i] = pt;

            var riverW = Mathf.Lerp(baseRiverWidth * 1.10f, baseRiverWidth * 0.55f, Mathf.SmoothStep(0.10f, 1f, t));
            var floodW = riverW + floodExtra;
            var bankW = riverW + 2.5f;

            var (px, py) = WorldToPixel(pt, halfWidth, halfHeight, texW, texH);
            var floodRad = Mathf.Max(1, Mathf.RoundToInt((floodW * 0.5f) * BakedPixelsPerUnit));
            var bankRad = Mathf.Max(1, Mathf.RoundToInt((bankW * 0.5f) * BakedPixelsPerUnit));
            var riverRad = Mathf.Max(1, Mathf.RoundToInt((riverW * 0.5f) * BakedPixelsPerUnit));

            PaintDisc(pixels, texW, texH, px, py, floodRad, floodColor);
            PaintDisc(pixels, texW, texH, px, py, bankRad, bankColor);
            PaintDisc(pixels, texW, texH, px, py, riverRad, riverColor);

            if (i % 20 == 0)
            {
                waterNodes.Add(pt);
            }
        }

        if (waterNodes.Count == 0)
        {
            waterNodes.Add(new Vector2(riverPoints[segmentCount / 2].x, riverPoints[segmentCount / 2].y));
        }

        PaintCreeks(pixels, texW, texH, halfWidth, halfHeight, mapConfig, rng, riverPoints, riverColor);
        PaintGrassSpeckles(pixels, texW, texH, halfWidth, halfHeight, rng);
        PaintTrees(pixels, texW, texH, halfWidth, halfHeight, cfg, rng);

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        var sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, texW, texH),
            new Vector2(0.5f, 0.5f),
            BakedPixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        var go = new GameObject("BakedMap");
        go.transform.SetParent(mapRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = SortingLayerDefault;
        renderer.sortingOrder = BakedMapOrder;
        renderer.color = Color.white;
    }

    private void FillSavannaBackground(Color32[] pixels, int texW, int texH, Color dryColor, Color greenishColor, int seed)
    {
        for (var y = 0; y < texH; y++)
        {
            var tY = texH <= 1 ? 0f : y / (float)(texH - 1);
            for (var x = 0; x < texW; x++)
            {
                var tX = texW <= 1 ? 0f : x / (float)(texW - 1);
                var baseMix = Mathf.Clamp01((0.55f * tX) + (0.25f * (1f - tY)));
                var baseColor = Color.Lerp(dryColor, greenishColor, baseMix);
                var noise = (SmoothHashNoise01(x, y, seed) * 2f) - 1f;
                var nScale = 1f + (noise * 0.06f);
                baseColor *= nScale;
                baseColor.a = 1f;
                pixels[(y * texW) + x] = (Color32)baseColor;
            }
        }
    }

    private void PaintCreeks(
        Color32[] pixels,
        int texW,
        int texH,
        float halfWidth,
        float halfHeight,
        Map mapConfig,
        IRng rng,
        IReadOnlyList<Vector2> riverPoints,
        Color32 creekColor)
    {
        var creekCount = Mathf.Clamp(mapConfig.creekCount, 4, 8);
        var riverTargetX = 0f;
        var stepLen = 2.2f;
        var creekRadiusPx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.25f, mapConfig.creekWidth * 0.5f) * BakedPixelsPerUnit));

        for (var c = 0; c < creekCount; c++)
        {
            var side = c % 2 == 0 ? -1f : 1f;
            var current = new Vector2(
                side * rng.Range(halfWidth * 0.5f, halfWidth * 0.95f),
                rng.Range(-halfHeight * 0.95f, halfHeight * 0.95f));

            var steps = rng.NextInt(18, 45);
            for (var s = 0; s < steps; s++)
            {
                var nearest = FindNearestRiverPoint(current, riverPoints);
                riverTargetX = nearest.x;
                var towardRiver = new Vector2(riverTargetX - current.x, nearest.y - current.y).normalized;
                var wiggle = new Vector2(rng.Range(-0.55f, 0.55f), rng.Range(-0.35f, 0.35f));
                var dir = (towardRiver + wiggle).normalized;
                var next = current + (dir * stepLen);
                next.x = Mathf.Clamp(next.x, -halfWidth + 0.1f, halfWidth - 0.1f);
                next.y = Mathf.Clamp(next.y, -halfHeight + 0.1f, halfHeight - 0.1f);

                PaintStroke(pixels, texW, texH, halfWidth, halfHeight, current, next, creekRadiusPx, creekColor);
                current = next;

                if (Vector2.Distance(current, nearest) <= 1.8f)
                {
                    break;
                }
            }
        }
    }

    private void PaintGrassSpeckles(Color32[] pixels, int texW, int texH, float halfWidth, float halfHeight, IRng rng)
    {
        var grassCount = Mathf.Clamp(Mathf.RoundToInt(halfWidth * halfHeight * 0.9f), 1000, 4000);
        for (var i = 0; i < grassCount; i++)
        {
            var p = new Vector2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfHeight, halfHeight));
            var radius = rng.NextInt(1, 3);
            var tint = rng.Range(-14, 16);
            var color = new Color32((byte)Mathf.Clamp(83 + tint, 0, 255), (byte)Mathf.Clamp(132 + tint, 0, 255), (byte)Mathf.Clamp(62 + tint, 0, 255), 255);
            var (px, py) = WorldToPixel(p, halfWidth, halfHeight, texW, texH);
            PaintDisc(pixels, texW, texH, px, py, radius, color);
        }
    }

    private void PaintTrees(Color32[] pixels, int texW, int texH, float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg, IRng rng)
    {
        var clusterCount = Mathf.Max(1, cfg.map.treeClusterCount);
        var treeScale = Mathf.Max(0.5f, cfg.visuals.treeScale);

        for (var c = 0; c < clusterCount; c++)
        {
            var center = new Vector2(rng.Range(-halfWidth * 0.92f, halfWidth * 0.92f), rng.Range(-halfHeight * 0.92f, halfHeight * 0.92f));
            var treeCount = rng.NextInt(3, 9);
            for (var i = 0; i < treeCount; i++)
            {
                var pos = center + rng.InsideUnitCircle() * rng.Range(0.3f, 2.6f);
                shadeNodes.Add(pos);
                var radius = Mathf.RoundToInt(rng.Range(3f, 6f) * treeScale);
                var outlineRadius = Mathf.Max(radius + 1, radius + Mathf.RoundToInt(1f * treeScale));
                var (px, py) = WorldToPixel(pos, halfWidth, halfHeight, texW, texH);
                PaintDisc(pixels, texW, texH, px, py, outlineRadius, new Color32(18, 44, 20, 255));
                PaintDisc(pixels, texW, texH, px, py, radius, new Color32(36, 88, 38, 255));
            }
        }
    }

    private static Vector2 FindNearestRiverPoint(Vector2 p, IReadOnlyList<Vector2> riverPoints)
    {
        var nearest = riverPoints[0];
        var bestSq = (p - nearest).sqrMagnitude;
        for (var i = 1; i < riverPoints.Count; i++)
        {
            var sq = (p - riverPoints[i]).sqrMagnitude;
            if (sq >= bestSq)
            {
                continue;
            }

            bestSq = sq;
            nearest = riverPoints[i];
        }

        return nearest;
    }

    private static void PaintStroke(Color32[] pixels, int texW, int texH, float halfWidth, float halfHeight, Vector2 a, Vector2 b, int radiusPx, Color32 color)
    {
        var dist = Vector2.Distance(a, b);
        var samples = Mathf.Max(2, Mathf.CeilToInt(dist * BakedPixelsPerUnit * 0.6f));
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var p = Vector2.Lerp(a, b, t);
            var (px, py) = WorldToPixel(p, halfWidth, halfHeight, texW, texH);
            PaintDisc(pixels, texW, texH, px, py, radiusPx, color);
        }
    }

    private static (int px, int py) WorldToPixel(Vector2 world, float halfWidth, float halfHeight, int texW, int texH)
    {
        var u = (world.x + halfWidth) / (2f * halfWidth);
        var v = (world.y + halfHeight) / (2f * halfHeight);
        var px = Mathf.RoundToInt(u * (texW - 1));
        var py = Mathf.RoundToInt(v * (texH - 1));
        px = Mathf.Clamp(px, 0, texW - 1);
        py = Mathf.Clamp(py, 0, texH - 1);
        return (px, py);
    }

    private static void PaintDisc(Color32[] pixels, int texW, int texH, int cx, int cy, int radiusPx, Color32 color)
    {
        if (radiusPx <= 0)
        {
            return;
        }

        var minX = Mathf.Max(0, cx - radiusPx);
        var maxX = Mathf.Min(texW - 1, cx + radiusPx);
        var minY = Mathf.Max(0, cy - radiusPx);
        var maxY = Mathf.Min(texH - 1, cy + radiusPx);
        var r2 = radiusPx * radiusPx;

        for (var y = minY; y <= maxY; y++)
        {
            var dy = y - cy;
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - cx;
                if ((dx * dx) + (dy * dy) > r2)
                {
                    continue;
                }

                pixels[(y * texW) + x] = color;
            }
        }
    }

    private static float SmoothHashNoise01(int x, int y, int seed)
    {
        var center = HashNoise01(x, y, seed);
        var left = HashNoise01(x - 1, y, seed);
        var right = HashNoise01(x + 1, y, seed);
        var up = HashNoise01(x, y + 1, seed);
        var down = HashNoise01(x, y - 1, seed);
        return (center * 0.4f) + ((left + right + up + down) * 0.15f);
    }

    private static float HashNoise01(int x, int y, int seed)
    {
        var h = unchecked((x * 374761393) + (y * 668265263) + (seed * 1442695041));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }
}
