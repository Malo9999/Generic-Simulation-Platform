using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
    private const string SortingLayerDefault = "Default";
    private const int BackgroundOrder = -200;
    private const int BiomeGradientOrder = -195;
    private const int FloodplainOrder = -190;
    private const int RiverOrder = -180;
    private const int RiverBankOrder = -181;
    private const int RiverCapOrder = -179;
    private const int CreekOrder = -170;
    private const int GrassOrder = -165;
    private const int TreeOrder = -160;

    private readonly List<SpriteRenderer> creekRenderers = new();
    private readonly List<Vector2> waterNodes = new();
    private readonly List<Vector2> shadeNodes = new();

    private Transform mapRoot;
    private SpriteRenderer floodplainRenderer;

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
        BuildBiomeGradient(halfWidth, halfHeight);
        BuildRiverAndFloodplain(halfWidth, halfHeight, docu.map);
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

        if (floodplainRenderer != null)
        {
            floodplainRenderer.color = Color.Lerp(new Color(0.5f, 0.72f, 0.37f, 1f), new Color(0.56f, 0.66f, 0.35f, 1f), dry);
        }
    }

    public void Clear()
    {
        if (mapRoot != null)
        {
            Object.Destroy(mapRoot.gameObject);
            mapRoot = null;
        }

        creekRenderers.Clear();
        waterNodes.Clear();
        shadeNodes.Clear();
        floodplainRenderer = null;
    }

    private void BuildBackground(float halfWidth, float halfHeight)
    {
        var arenaWidth = halfWidth * 2f;
        var arenaHeight = halfHeight * 2f;
        var sr = CreateSprite("SavannaBackground", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.76f, 0.7f, 0.45f, 1f), Vector2.zero, new Vector2(arenaWidth, arenaHeight), 0f, BackgroundOrder);
        sr.drawMode = SpriteDrawMode.Sliced;
    }

    private void BuildBiomeGradient(float halfWidth, float halfHeight)
    {
        const int stripCount = 12;
        var gradientRoot = new GameObject("BiomeGradient").transform;
        gradientRoot.SetParent(mapRoot, false);

        var stripWidth = (halfWidth * 2f) / stripCount;
        var leftColor = new Color(0.84f, 0.76f, 0.43f, 1f);
        var rightColor = new Color(0.68f, 0.74f, 0.4f, 1f);

        for (var i = 0; i < stripCount; i++)
        {
            var t = stripCount <= 1 ? 0f : i / (float)(stripCount - 1);
            var x = -halfWidth + (stripWidth * (i + 0.5f));
            var strip = CreateSprite("BiomeStrip", PrimitiveSpriteLibrary.RoundedRectFill(), Color.Lerp(leftColor, rightColor, t), new Vector2(x, 0f), new Vector2(stripWidth + 0.02f, halfHeight * 2f), 0f, BiomeGradientOrder);
            strip.drawMode = SpriteDrawMode.Sliced;
            strip.transform.SetParent(gradientRoot, true);
        }
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

    private void BuildRiverAndFloodplain(float halfWidth, float halfHeight, Map mapConfig)
    {
        const float riverMarginTop = 18f;
        const float riverMarginBottom = 10f;

        var riverStartY = (-halfHeight) + riverMarginBottom;
        var riverEndY = halfHeight - riverMarginTop;
        if (riverEndY <= riverStartY)
        {
            riverEndY = riverStartY + Mathf.Max(1f, halfHeight * 0.5f);
        }

        var riverHeight = riverEndY - riverStartY;
        var riverCenterY = (riverStartY + riverEndY) * 0.5f;
        var floodWidth = Mathf.Max(mapConfig.floodplainWidth, mapConfig.riverWidth + 1f);
        floodplainRenderer = CreateSprite("Floodplain", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.5f, 0.72f, 0.37f, 1f), new Vector2(0f, riverCenterY), new Vector2(floodWidth, riverHeight), 0f, FloodplainOrder);
        floodplainRenderer.drawMode = SpriteDrawMode.Sliced;

        var riverBody = CreateSprite("RiverBody", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.14f, 0.47f, 0.92f, 1f), new Vector2(0f, riverCenterY), new Vector2(mapConfig.riverWidth, riverHeight), 0f, RiverOrder);
        riverBody.drawMode = SpriteDrawMode.Sliced;

        CreateSprite("RiverCapBottom", PrimitiveSpriteLibrary.CircleFill(), new Color(0.14f, 0.47f, 0.92f, 1f), new Vector2(0f, riverStartY), new Vector2(mapConfig.riverWidth, mapConfig.riverWidth), 0f, RiverCapOrder);
        CreateSprite("RiverCapTop", PrimitiveSpriteLibrary.CircleFill(), new Color(0.14f, 0.47f, 0.92f, 1f), new Vector2(0f, riverEndY), new Vector2(mapConfig.riverWidth, mapConfig.riverWidth), 0f, RiverCapOrder);

        var bankWidth = Mathf.Max(0.16f, mapConfig.riverWidth * 0.14f);
        var bankOffset = (mapConfig.riverWidth * 0.5f) + (bankWidth * 0.5f);
        CreateSprite("RiverBankLeft", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.33f, 0.49f, 0.24f, 1f), new Vector2(-bankOffset, riverCenterY), new Vector2(bankWidth, riverHeight), 0f, RiverBankOrder).drawMode = SpriteDrawMode.Sliced;
        CreateSprite("RiverBankRight", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.33f, 0.49f, 0.24f, 1f), new Vector2(bankOffset, riverCenterY), new Vector2(bankWidth, riverHeight), 0f, RiverBankOrder).drawMode = SpriteDrawMode.Sliced;

        const int sampleCount = 18;
        for (var i = 0; i <= sampleCount; i++)
        {
            var y = Mathf.Lerp(riverStartY, riverEndY, i / (float)sampleCount);
            waterNodes.Add(new Vector2(0f, y));
        }
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
