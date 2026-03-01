using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const float BackgroundPixelsPerUnit = 8f;
    private const int BackgroundOrder = -200;
    private const int FloodplainOrder = -190;
    private const int BiomeOverlayOrder = -198;
    private const int RiverOrder = -180;
    private const int RiverBankOrder = -181;
    private const int CreekOrder = -170;
    private const int GrassOrder = -165;
    private const int TreeOrder = -160;

    private readonly List<SpriteRenderer> creekRenderers = new();
    private readonly List<SpriteRenderer> floodplainRenderers = new();
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

        BuildBackground(halfWidth, halfHeight);
        BuildRiverRibbon(mapRoot, config, halfWidth, halfHeight, RngService.Fork("SIM:PredatorPreyDocu:RIVER"));
        BuildCreeks(docu.map, halfWidth, halfHeight);
        BuildGrassDots(docu.map, halfWidth, halfHeight);
        BuildTrees(docu, halfWidth, halfHeight);
    }

    public void UpdateSeasonVisuals(float dryness01)
    {
        var dry = Mathf.Clamp01(dryness01);
        var creekAlpha = Mathf.Lerp(1f, 0.05f, dry);

        for (var i = 0; i < creekRenderers.Count; i++)
        {
            if (creekRenderers[i] == null)
            {
                continue;
            }

            var color = creekRenderers[i].color;
            color.a = creekAlpha;
            creekRenderers[i].color = color;
        }

        var floodplainColor = Color.Lerp(new Color(0.5f, 0.72f, 0.37f, 1f), new Color(0.56f, 0.66f, 0.35f, 1f), dry);
        for (var i = 0; i < floodplainRenderers.Count; i++)
        {
            if (floodplainRenderers[i] == null)
            {
                continue;
            }

            floodplainRenderers[i].color = floodplainColor;
        }
    }

    public void Clear()
    {
        if (mapRoot != null)
        {
            Object.Destroy(mapRoot.gameObject);
            mapRoot = null;
        }

        floodplainRenderers.Clear();
        creekRenderers.Clear();
        waterNodes.Clear();
        shadeNodes.Clear();
    }

    private void BuildBackground(float halfWidth, float halfHeight)
    {
        var arenaWidth = halfWidth * 2f;
        var arenaHeight = halfHeight * 2f;

        var texWidth = Mathf.Max(32, Mathf.RoundToInt(arenaWidth * BackgroundPixelsPerUnit));
        var texHeight = Mathf.Max(32, Mathf.RoundToInt(arenaHeight * BackgroundPixelsPerUnit));

        var texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        var seedRng = RngService.Fork("SIM:PredatorPreyDocu:MAP");
        var noiseSeed = seedRng.NextInt(0, int.MaxValue);
        var dryColor = new Color(0.79f, 0.69f, 0.42f, 1f);
        var greenColor = new Color(0.53f, 0.67f, 0.39f, 1f);
        var pixels = new Color32[texWidth * texHeight];

        for (var y = 0; y < texHeight; y++)
        {
            var tY = texHeight <= 1 ? 0f : y / (float)(texHeight - 1);

            for (var x = 0; x < texWidth; x++)
            {
                var tX = texWidth <= 1 ? 0f : x / (float)(texWidth - 1);
                var noise = SmoothHashNoise01(x, y, noiseSeed);
                var blend = Mathf.Clamp01((0.55f * tX) + (0.25f * tY) + (0.20f * noise));
                var color = Color.Lerp(dryColor, greenColor, blend);
                pixels[(y * texWidth) + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texWidth, texHeight),
            new Vector2(0.5f, 0.5f),
            BackgroundPixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        var backgroundScale = new Vector2((arenaWidth + 2f) / Mathf.Max(0.001f, arenaWidth), (arenaHeight + 2f) / Mathf.Max(0.001f, arenaHeight));
        var sr = CreateSprite("SavannaBackground", sprite, Color.white, Vector2.zero, backgroundScale, 0f, BackgroundOrder);
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = BackgroundOrder;
        sr.color = Color.white;
        sr.transform.localPosition = Vector3.zero;

        var overlay = CreateSprite("SavannaBackgroundOverlay", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.95f, 0.97f, 0.9f, 0.09f), Vector2.zero, new Vector2(arenaWidth + 2f, arenaHeight + 2f), 0f, BiomeOverlayOrder);
        overlay.sortingLayerName = SortingLayerDefault;
        overlay.sortingOrder = BiomeOverlayOrder;
        overlay.transform.localPosition = Vector3.zero;
    }

    private void BuildGrassDots(Map mapConfig, float halfWidth, float halfHeight)
    {
        var rng = RngService.Fork("SIM:PredatorPreyDocu:GRASS");
        var grassRoot = new GameObject("GrassDots").transform;
        grassRoot.SetParent(mapRoot, false);

        var area = halfWidth * halfHeight * 4f;
        var targetCount = Mathf.Clamp(Mathf.RoundToInt(area * 0.32f), 800, 2000);
        var floodWidth = Mathf.Max(1f, mapConfig.floodplainWidth * 1.2f);

        for (var i = 0; i < targetCount; i++)
        {
            var position = new Vector2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfHeight, halfHeight));
            var nearFloodplain01 = 1f - Mathf.Clamp01(Mathf.Abs(position.x) / floodWidth);
            var spawnChance = Mathf.Lerp(0.25f, 1f, nearFloodplain01);
            if (rng.Value() > spawnChance)
            {
                continue;
            }

            var scale = rng.Range(0.06f, 0.14f);
            var baseColor = new Color(0.41f, 0.62f, 0.3f, 1f);
            var variance = rng.Range(-0.045f, 0.045f);
            var color = new Color(
                Mathf.Clamp01(baseColor.r + variance * 0.7f),
                Mathf.Clamp01(baseColor.g + variance),
                Mathf.Clamp01(baseColor.b + variance * 0.55f),
                0.95f);

            var dot = CreateSprite("GrassDot", PrimitiveSpriteLibrary.CircleFill(), color, position, new Vector2(scale, scale), 0f, GrassOrder);
            dot.transform.SetParent(grassRoot, true);
        }
    }

    private void BuildRiverRibbon(Transform parent, ScenarioConfig cfg, float halfWidth, float halfHeight, IRng rng)
    {
        if (parent == null)
        {
            return;
        }

        var mapConfig = cfg?.predatorPreyDocu?.map ?? new Map();
        var riverRoot = new GameObject("RiverRibbon").transform;
        riverRoot.SetParent(parent, false);

        const int segmentCount = 56;
        const float xMargin = 10f;
        const float bankWidth = 1.25f;

        var widthNorth = mapConfig.riverWidth * 1.10f;
        var widthMid = mapConfig.riverWidth;
        var widthSouth = mapConfig.riverWidth * 0.55f;

        var y0 = halfHeight;
        var yN = -halfHeight;

        var points = new Vector2[segmentCount + 1];
        var f1 = rng.Range(0.6f, 1.1f);
        var f2 = rng.Range(1.2f, 2.0f);
        var f3 = rng.Range(3.0f, 5.0f);
        var phase1 = rng.Value();
        var phase2 = rng.Value();
        var phase3 = rng.Value();

        for (var i = 0; i <= segmentCount; i++)
        {
            var t = i / (float)segmentCount;
            var y = Mathf.Lerp(y0, yN, t);
            y = Mathf.Clamp(y, -halfHeight, halfHeight);
            var smoothT = Mathf.SmoothStep(0f, 1f, t);
            var ampBase = Mathf.Lerp(6f, 3f, smoothT);
            var ampDelta = 5f * Mathf.SmoothStep(0.8f, 1f, t);

            var x = (ampBase * Mathf.Sin(2f * Mathf.PI * ((f1 * t) + phase1)))
                + ((ampBase * 0.6f) * Mathf.Sin(2f * Mathf.PI * ((f2 * t) + phase2)))
                + (ampDelta * Mathf.Sin(2f * Mathf.PI * ((f3 * t * t) + phase3)));

            x = Mathf.Clamp(x, -halfWidth + xMargin, halfWidth - xMargin);
            points[i] = new Vector2(x, y);
        }

        var floodExtra = Mathf.Max(1f, mapConfig.floodplainWidth);
        var floodplainColor = new Color(0.5f, 0.72f, 0.37f, 1f);
        var bankColor = new Color(0.33f, 0.49f, 0.24f, 1f);
        var riverColor = new Color(0.14f, 0.47f, 0.92f, 1f);

        for (var i = 0; i < segmentCount; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var segment = b - a;
            var len = segment.magnitude;
            if (len <= 0.001f)
            {
                continue;
            }

            var mid = (a + b) * 0.5f;
            var angleRad = Mathf.Atan2(segment.y, segment.x);
            var angle = angleRad * Mathf.Rad2Deg;
            var tMid = (i + 0.5f) / segmentCount;
            var taper = Mathf.SmoothStep(0.1f, 1f, tMid);
            var wMid = Mathf.Lerp(widthNorth, widthSouth, taper);
            wMid += 0.25f * widthMid * Mathf.Sin(Mathf.PI * tMid) * 0.35f;

            var segLen = len;
            const float overlap = 0.35f;
            if (i > 0 && i < segmentCount - 1)
            {
                segLen += overlap;
            }

            var floodplainWidth = wMid + floodExtra;
            var bankWidthMid = wMid + (bankWidth * 2f);
            var floodMid = AdjustMidpointToVerticalBounds(mid, angleRad, segLen, floodplainWidth, halfHeight);
            var bankMid = AdjustMidpointToVerticalBounds(mid, angleRad, segLen, bankWidthMid, halfHeight);
            var riverMid = AdjustMidpointToVerticalBounds(mid, angleRad, segLen, wMid, halfHeight);

            var floodplain = CreateSprite($"FloodplainSeg_{i}", PrimitiveSpriteLibrary.RoundedRectFill(64), floodplainColor, floodMid, new Vector2(floodplainWidth, segLen), angle, FloodplainOrder);
            floodplain.transform.SetParent(riverRoot, true);
            floodplainRenderers.Add(floodplain);

            var bank = CreateSprite($"RiverBankSeg_{i}", PrimitiveSpriteLibrary.RoundedRectFill(64), bankColor, bankMid, new Vector2(bankWidthMid, segLen), angle, RiverBankOrder);
            bank.transform.SetParent(riverRoot, true);

            var river = CreateSprite($"RiverSeg_{i}", PrimitiveSpriteLibrary.RoundedRectFill(64), riverColor, riverMid, new Vector2(wMid, segLen), angle, RiverOrder);
            river.transform.SetParent(riverRoot, true);

            if (i % rng.NextInt(4, 7) == 0)
            {
                waterNodes.Add(riverMid);
            }
        }

        var northCapRadius = Mathf.Max(0.1f, widthNorth * 0.9f);
        var northCap = CreateSprite("RiverCapNorth", PrimitiveSpriteLibrary.CircleFill(), riverColor, new Vector2(points[0].x, halfHeight), new Vector2(northCapRadius, northCapRadius), 0f, RiverOrder + 1);
        northCap.transform.SetParent(riverRoot, true);

        var southCapRadius = Mathf.Max(0.1f, widthSouth * 0.9f);
        var southCap = CreateSprite("RiverCapSouth", PrimitiveSpriteLibrary.CircleFill(), riverColor, new Vector2(points[segmentCount].x, -halfHeight), new Vector2(southCapRadius, southCapRadius), 0f, RiverOrder + 1);
        southCap.transform.SetParent(riverRoot, true);

        if (waterNodes.Count == 0)
        {
            waterNodes.Add(points[segmentCount / 2]);
        }
    }

    private static Vector2 AdjustMidpointToVerticalBounds(Vector2 midpoint, float angleRad, float segmentLength, float width, float halfHeight)
    {
        var yExtent = (0.5f * segmentLength * Mathf.Abs(Mathf.Sin(angleRad)))
            + (0.5f * width * Mathf.Abs(Mathf.Cos(angleRad)));

        var adjusted = midpoint;
        var maxY = halfHeight;
        var minY = -halfHeight;

        if (adjusted.y + yExtent > maxY)
        {
            adjusted.y -= (adjusted.y + yExtent) - maxY;
        }

        if (adjusted.y - yExtent < minY)
        {
            adjusted.y += minY - (adjusted.y - yExtent);
        }

        return adjusted;
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

    private void BuildCreeks(Map mapConfig, float halfWidth, float halfHeight)
    {
        var rng = RngService.Fork("SIM:PredatorPreyDocu:MAP");

        for (var c = 0; c < mapConfig.creekCount; c++)
        {
            var side = c % 2 == 0 ? -1f : 1f;
            var startX = side * rng.Range(halfWidth * 0.4f, halfWidth * 0.9f);
            var startY = rng.Range(-halfHeight * 0.95f, halfHeight * 0.95f);
            var current = new Vector2(startX, startY);

            var segCount = rng.NextInt(6, 13);
            for (var s = 0; s < segCount; s++)
            {
                var toRiver = new Vector2(-Mathf.Sign(current.x), rng.Range(-0.18f, 0.18f));
                var step = new Vector2(toRiver.x, toRiver.y).normalized * rng.Range(4f, 8f);
                step.y += rng.Range(-2f, 2f);

                var next = current + step;
                next.x = Mathf.MoveTowards(next.x, 0f, rng.Range(0.8f, 2.8f));
                next.x = Mathf.Clamp(next.x, -halfWidth + 1f, halfWidth - 1f);
                next.y = Mathf.Clamp(next.y, -halfHeight + 1f, halfHeight - 1f);

                var delta = next - current;
                if (delta.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                var mid = (current + next) * 0.5f;
                var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                var creek = CreateSprite("CreekSeg", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.24f, 0.6f, 0.9f, 1f), mid, new Vector2(delta.magnitude + 0.1f, mapConfig.creekWidth), angle, CreekOrder);
                creekRenderers.Add(creek);

                current = next;
            }

            waterNodes.Add(current);
        }
    }

    private void BuildTrees(PredatorPreyDocuConfig docu, float halfWidth, float halfHeight)
    {
        var rng = RngService.Fork("SIM:PredatorPreyDocu:MAP");
        var treeScale = docu.visuals.treeScale;

        for (var c = 0; c < docu.map.treeClusterCount; c++)
        {
            var center = new Vector2(rng.Range(-halfWidth * 0.94f, halfWidth * 0.94f), rng.Range(-halfHeight * 0.94f, halfHeight * 0.94f));
            var treeCount = rng.NextInt(3, 9);

            for (var i = 0; i < treeCount; i++)
            {
                var jitter = rng.InsideUnitCircle() * rng.Range(0.2f, 2.4f);
                var pos = center + jitter;
                shadeNodes.Add(pos);

                var radius = rng.Range(0.55f, 1.1f) * treeScale;
                CreateSprite("TreeOutline", PrimitiveSpriteLibrary.CircleOutline(), new Color(0.06f, 0.17f, 0.07f, 1f), pos, new Vector2(radius * 1.08f, radius * 1.08f), 0f, TreeOrder);
                CreateSprite("Tree", PrimitiveSpriteLibrary.CircleFill(), new Color(0.14f, 0.36f, 0.15f, 1f), pos, new Vector2(radius, radius), 0f, TreeOrder);
            }
        }
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, Color color, Vector2 localPosition, Vector2 localScale, float localRotationDeg, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(mapRoot, false);
        go.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, localRotationDeg);
        go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = sortingOrder;
        sr.enabled = true;
        return sr;
    }
}
