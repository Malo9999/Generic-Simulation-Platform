using System.Collections.Generic;
using UnityEngine;

public sealed class PredatorPreyDocuMapBuilder
{
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
        BuildRiverAndFloodplain(halfWidth, halfHeight, docu.map);
        BuildCreeks(docu.map, halfWidth, halfHeight);
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
            floodplainRenderer.color = Color.Lerp(new Color(0.56f, 0.67f, 0.39f, 0.75f), new Color(0.61f, 0.57f, 0.31f, 0.65f), dry);
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
        var sr = CreateSprite("SavannaBg", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.74f, 0.67f, 0.41f, 1f), Vector2.zero, new Vector2(halfWidth * 2f, halfHeight * 2f), 0f, -200);
        sr.drawMode = SpriteDrawMode.Sliced;
    }

    private void BuildRiverAndFloodplain(float halfWidth, float halfHeight, Map mapConfig)
    {
        var floodWidth = Mathf.Max(mapConfig.floodplainWidth, mapConfig.riverWidth + 1f);
        floodplainRenderer = CreateSprite("Floodplain", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.56f, 0.67f, 0.39f, 0.75f), Vector2.zero, new Vector2(floodWidth, halfHeight * 2.08f), 0f, -190);
        floodplainRenderer.drawMode = SpriteDrawMode.Sliced;

        var river = CreateSprite("River", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.24f, 0.53f, 0.78f, 0.93f), Vector2.zero, new Vector2(mapConfig.riverWidth, halfHeight * 2.1f), 0f, -180);
        river.drawMode = SpriteDrawMode.Sliced;

        const int sampleCount = 18;
        for (var i = 0; i <= sampleCount; i++)
        {
            var y = Mathf.Lerp(-halfHeight, halfHeight, i / (float)sampleCount);
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
                var creek = CreateSprite("CreekSeg", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.33f, 0.64f, 0.9f, 1f), mid, new Vector2(delta.magnitude + 0.1f, mapConfig.creekWidth), angle, -170);
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
            var treeCount = rng.NextInt(3, 8);

            for (var i = 0; i < treeCount; i++)
            {
                var jitter = rng.InsideUnitCircle() * rng.Range(0.2f, 2.4f);
                var pos = center + jitter;
                shadeNodes.Add(pos);

                var radius = rng.Range(0.55f, 1.1f) * treeScale;
                CreateSprite("Tree", PrimitiveSpriteLibrary.CircleFill(), new Color(0.16f, 0.38f, 0.17f, 0.92f), pos, new Vector2(radius, radius), 0f, -160);
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
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
