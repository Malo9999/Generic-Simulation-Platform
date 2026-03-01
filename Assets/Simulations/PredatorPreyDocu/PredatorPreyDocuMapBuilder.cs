using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int SavannaOrder = -200;
    private const int FloodplainOrder = -190;
    private const int BankOrder = -181;
    private const int RiverOrder = -180;
    private const int CreekOrder = -176;
    private const int GrassOrder = -170;
    private const int TreeOrder = -160;

    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();
    private readonly List<SpriteRenderer> creekRenderers = new();

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
        BuildObjectMap(halfWidth, halfHeight, docu, mapRng);
    }

    public void UpdateSeasonVisuals(float dryness01)
    {
        var dry = Mathf.Clamp01(dryness01);
        var alpha = Mathf.Lerp(0.95f, 0.22f, dry);
        for (var i = 0; i < creekRenderers.Count; i++)
        {
            var sr = creekRenderers[i];
            if (sr == null)
            {
                continue;
            }

            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
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
        creekRenderers.Clear();
    }

    private void BuildObjectMap(float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg, IRng rng)
    {
        var arenaW = halfWidth * 2f;
        var arenaH = halfHeight * 2f;
        var mapCfg = cfg.map ?? new Map();

        BuildSavannaBackground(arenaW, arenaH);
        BuildBiomeGradientBands(halfWidth, halfHeight, rng);

        BuildRiverRibbon(halfWidth, halfHeight, mapCfg, rng, out var riverPoints, out var widths);
        BuildCreeks(halfWidth, halfHeight, mapCfg, riverPoints, rng);
        BuildGrass(halfWidth, halfHeight, rng);
        BuildTrees(halfWidth, halfHeight, cfg, rng);

        if (waterNodes.Count == 0 && riverPoints.Count > 0)
        {
            waterNodes.Add(riverPoints[riverPoints.Count / 2]);
        }
    }

    private void BuildSavannaBackground(float arenaW, float arenaH)
    {
        var sr = CreateSprite(
            mapRoot,
            "SavannaBackground",
            PrimitiveSpriteLibrary.RoundedRectFill(64),
            Vector2.zero,
            new Vector2(arenaW + 2f, arenaH + 2f),
            0f,
            new Color(0.79f, 0.69f, 0.42f, 1f),
            SavannaOrder);

        sr.sortingLayerName = SortingLayerDefault;
    }

    private void BuildBiomeGradientBands(float halfWidth, float halfHeight, IRng rng)
    {
        const int strips = 20;
        var stripH = (halfHeight * 2f) / strips;
        for (var i = 0; i < strips; i++)
        {
            var t = i / (float)(strips - 1);
            var y = Mathf.Lerp(halfHeight - (stripH * 0.5f), -halfHeight + (stripH * 0.5f), t);
            var baseColor = Color.Lerp(new Color(0.82f, 0.73f, 0.47f, 0.30f), new Color(0.50f, 0.66f, 0.38f, 0.34f), 1f - t);
            var jitter = rng.Range(-0.04f, 0.04f);
            baseColor.r = Mathf.Clamp01(baseColor.r + jitter);
            baseColor.g = Mathf.Clamp01(baseColor.g + jitter * 0.6f);

            CreateSprite(
                mapRoot,
                $"BiomeStrip_{i:00}",
                PrimitiveSpriteLibrary.RoundedRectFill(64),
                new Vector2(0f, y),
                new Vector2((halfWidth * 2f) + 1.6f, stripH + 0.2f),
                0f,
                baseColor,
                SavannaOrder + 1);
        }
    }

    private void BuildRiverRibbon(
        float halfWidth,
        float halfHeight,
        Map mapCfg,
        IRng rng,
        out List<Vector2> points,
        out List<float> widths)
    {
        const int segmentCount = 160;
        const float xMargin = 3f;
        const float overlap = 0.75f;

        var baseRiverWidth = Mathf.Max(0.8f, mapCfg.riverWidth);
        var floodplainExtra = Mathf.Max(0.8f, mapCfg.floodplainWidth);
        var bankExtra = Mathf.Max(0.8f, Mathf.Min(mapCfg.floodplainWidth * 0.25f, 5.5f));

        var f1 = rng.Range(0.6f, 1.05f);
        var f2 = rng.Range(1.2f, 1.9f);
        var p1 = rng.Value();
        var p2 = rng.Value();
        var southSwirlPhase = rng.Value();

        points = new List<Vector2>(segmentCount + 1);
        widths = new List<float>(segmentCount + 1);

        for (var i = 0; i <= segmentCount; i++)
        {
            var t = i / (float)segmentCount;
            var y = Mathf.Lerp(halfHeight, -halfHeight, t);

            var amp = Mathf.Lerp(Mathf.Min(halfWidth * 0.32f, 8f), Mathf.Min(halfWidth * 0.22f, 4f), t);
            var meander = (amp * Mathf.Sin((2f * Mathf.PI * f1 * t) + (p1 * Mathf.PI * 2f)))
                          + ((amp * 0.62f) * Mathf.Sin((2f * Mathf.PI * f2 * t) + (p2 * Mathf.PI * 2f)));
            var swirlT = Mathf.SmoothStep(0.70f, 1f, t);
            var swirl = swirlT * Mathf.Min(halfWidth * 0.2f, 2.5f) * Mathf.Sin((6f * Mathf.PI * t * t) + (southSwirlPhase * Mathf.PI * 2f));

            var x = Mathf.Clamp(meander + swirl, -halfWidth + xMargin, halfWidth - xMargin);
            var riverW = Mathf.Lerp(baseRiverWidth * 1.10f, baseRiverWidth * 0.55f, Mathf.SmoothStep(0.10f, 1f, t));

            points.Add(new Vector2(x, y));
            widths.Add(riverW);
        }

        for (var i = 0; i < segmentCount; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var dir = b - a;
            var len = Mathf.Max(0.001f, dir.magnitude);
            var angleRad = Mathf.Atan2(dir.y, dir.x);
            var segLen = len + ((i > 0 && i < segmentCount - 1) ? overlap : 0f);
            var angleDeg = angleRad * Mathf.Rad2Deg;
            var mid = (a + b) * 0.5f;
            var w = (widths[i] + widths[i + 1]) * 0.5f;

            var floodWidth = w + floodplainExtra;
            var bankWidth = w + bankExtra;

            var floodMid = ClampSegmentCenterToArena(mid, halfWidth, halfHeight, segLen, floodWidth, angleRad);
            CreateSprite(mapRoot, $"Flood_{i:000}", PrimitiveSpriteLibrary.RoundedRectFill(64), floodMid, new Vector2(segLen, floodWidth), angleDeg, new Color(0.50f, 0.72f, 0.40f, 0.95f), FloodplainOrder);

            var bankMid = ClampSegmentCenterToArena(mid, halfWidth, halfHeight, segLen, bankWidth, angleRad);
            CreateSprite(mapRoot, $"Bank_{i:000}", PrimitiveSpriteLibrary.RoundedRectFill(64), bankMid, new Vector2(segLen, bankWidth), angleDeg, new Color(0.30f, 0.44f, 0.22f, 1f), BankOrder);

            var waterMid = ClampSegmentCenterToArena(mid, halfWidth, halfHeight, segLen, w, angleRad);
            CreateSprite(mapRoot, $"Water_{i:000}", PrimitiveSpriteLibrary.RoundedRectFill(64), waterMid, new Vector2(segLen, w), angleDeg, new Color(0.14f, 0.47f, 0.92f, 1f), RiverOrder);

            if (i % 14 == 0)
            {
                waterNodes.Add(waterMid);
            }
        }

        AddRiverCaps(halfWidth, halfHeight, points[0], points[points.Count - 1], widths[0], widths[widths.Count - 1]);
    }

    private void AddRiverCaps(float halfWidth, float halfHeight, Vector2 northPoint, Vector2 southPoint, float northWidth, float southWidth)
    {
        AddCap("RiverCap_North", northPoint, northWidth, halfWidth, halfHeight, true);
        AddCap("RiverCap_South", southPoint, southWidth, halfWidth, halfHeight, false);
    }

    private void AddCap(string name, Vector2 edgePoint, float width, float halfWidth, float halfHeight, bool north)
    {
        var radiusWorld = width * 0.5f;
        var x = Mathf.Clamp(edgePoint.x, -halfWidth + radiusWorld, halfWidth - radiusWorld);
        var y = north ? halfHeight - radiusWorld : -halfHeight + radiusWorld;

        CreateSprite(mapRoot, name, PrimitiveSpriteLibrary.CircleFill(64), new Vector2(x, y), new Vector2(width, width), 0f, new Color(0.14f, 0.47f, 0.92f, 1f), RiverOrder);
    }

    private void BuildCreeks(float halfWidth, float halfHeight, Map mapCfg, IReadOnlyList<Vector2> riverPoints, IRng rng)
    {
        var creekCount = Mathf.Clamp(mapCfg.creekCount, 0, 12);
        if (creekCount <= 0 || riverPoints.Count == 0)
        {
            return;
        }

        var creekWidth = Mathf.Max(0.25f, mapCfg.creekWidth);
        var creekColor = new Color(0.14f, 0.47f, 0.92f, 0.95f);

        for (var c = 0; c < creekCount; c++)
        {
            var side = (c & 1) == 0 ? -1f : 1f;
            var current = new Vector2(side * rng.Range(halfWidth * 0.55f, halfWidth * 0.95f), rng.Range(-halfHeight * 0.92f, halfHeight * 0.92f));
            var steps = rng.NextInt(12, 28);

            for (var s = 0; s < steps; s++)
            {
                var nearest = FindNearestPoint(current, riverPoints);
                var towardRiver = (nearest - current).normalized;
                var wiggle = new Vector2(rng.Range(-0.45f, 0.45f), rng.Range(-0.30f, 0.30f));
                var dir = (towardRiver + wiggle).normalized;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = towardRiver;
                }

                var stepLen = rng.Range(1.6f, 2.6f);
                var next = current + (dir * stepLen);
                next.x = Mathf.Clamp(next.x, -halfWidth + 0.15f, halfWidth - 0.15f);
                next.y = Mathf.Clamp(next.y, -halfHeight + 0.15f, halfHeight - 0.15f);

                var segment = CreateSegment($"Creek_{c:00}_{s:00}", current, next, creekWidth, creekColor, CreekOrder, halfWidth, halfHeight);
                creekRenderers.Add(segment);

                current = next;
                if ((current - nearest).sqrMagnitude <= 2.5f * 2.5f)
                {
                    break;
                }
            }
        }
    }

    private void BuildGrass(float halfWidth, float halfHeight, IRng rng)
    {
        var count = Mathf.Clamp(Mathf.RoundToInt(halfWidth * halfHeight * 0.5f), 180, 850);
        for (var i = 0; i < count; i++)
        {
            var pos = new Vector2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfHeight, halfHeight));
            var scale = rng.Range(0.12f, 0.32f);
            var tint = rng.Range(-0.06f, 0.06f);
            var c = new Color(0.31f + tint, 0.54f + (tint * 0.6f), 0.25f + (tint * 0.5f), rng.Range(0.22f, 0.55f));
            CreateSprite(mapRoot, $"Grass_{i:0000}", PrimitiveSpriteLibrary.CircleFill(32), pos, new Vector2(scale, scale), 0f, c, GrassOrder);
        }
    }

    private void BuildTrees(float halfWidth, float halfHeight, PredatorPreyDocuConfig cfg, IRng rng)
    {
        var mapCfg = cfg.map ?? new Map();
        var visuals = cfg.visuals ?? new Visuals();
        var clusterCount = Mathf.Max(1, mapCfg.treeClusterCount);
        var treeScale = Mathf.Max(0.5f, visuals.treeScale);

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
                CreateSprite(mapRoot, $"TreeShadow_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos + new Vector2(0.12f, -0.09f), new Vector2(canopy * 1.2f, canopy * 0.8f), 0f, new Color(0f, 0f, 0f, 0.25f), TreeOrder);
                CreateSprite(mapRoot, $"TreeCanopy_{c:000}_{i:00}", PrimitiveSpriteLibrary.CircleFill(32), pos, new Vector2(canopy, canopy), 0f, new Color(0.14f, 0.35f, 0.16f, 1f), TreeOrder + 1);
            }
        }
    }

    private SpriteRenderer CreateSegment(string name, Vector2 a, Vector2 b, float width, Color color, int sortingOrder, float halfWidth, float halfHeight)
    {
        var dir = b - a;
        var length = Mathf.Max(0.001f, dir.magnitude);
        var angleRad = Mathf.Atan2(dir.y, dir.x);
        var mid = (a + b) * 0.5f;
        var clampedMid = ClampSegmentCenterToArena(mid, halfWidth, halfHeight, length, width, angleRad);
        return CreateSprite(mapRoot, name, PrimitiveSpriteLibrary.RoundedRectFill(64), clampedMid, new Vector2(length, width), angleRad * Mathf.Rad2Deg, color, sortingOrder);
    }

    private static SpriteRenderer CreateSprite(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 position,
        Vector2 scale,
        float angleDeg,
        Color color,
        int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = sortingOrder;
        sr.color = color;
        return sr;
    }

    private static Vector2 ClampSegmentCenterToArena(Vector2 c, float halfW, float halfH, float length, float width, float angleRad)
    {
        var ca = Mathf.Abs(Mathf.Cos(angleRad));
        var sa = Mathf.Abs(Mathf.Sin(angleRad));
        var ex = (0.5f * length * ca) + (0.5f * width * sa);
        var ey = (0.5f * length * sa) + (0.5f * width * ca);

        var minX = -halfW + ex;
        var maxX = halfW - ex;
        var minY = -halfH + ey;
        var maxY = halfH - ey;

        if (minX > maxX)
        {
            minX = 0f;
            maxX = 0f;
        }

        if (minY > maxY)
        {
            minY = 0f;
            maxY = 0f;
        }

        c.x = Mathf.Clamp(c.x, minX, maxX);
        c.y = Mathf.Clamp(c.y, minY, maxY);
        return c;
    }

    private static Vector2 FindNearestPoint(Vector2 p, IReadOnlyList<Vector2> points)
    {
        var nearest = points[0];
        var bestSq = (p - nearest).sqrMagnitude;

        for (var i = 1; i < points.Count; i++)
        {
            var sq = (p - points[i]).sqrMagnitude;
            if (sq >= bestSq)
            {
                continue;
            }

            nearest = points[i];
            bestSq = sq;
        }

        return nearest;
    }
}
