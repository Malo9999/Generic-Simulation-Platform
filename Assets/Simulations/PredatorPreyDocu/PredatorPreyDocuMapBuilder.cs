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

        var texWidth = Mathf.Max(8, Mathf.RoundToInt(arenaWidth * BackgroundPixelsPerUnit));
        var texHeight = Mathf.Max(8, Mathf.RoundToInt(arenaHeight * BackgroundPixelsPerUnit));

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

        var sr = CreateSprite("SavannaBackground", sprite, Color.white, Vector2.zero, Vector2.one, 0f, BackgroundOrder);
        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = BackgroundOrder;
        sr.color = Color.white;

        var overlay = CreateSprite("SavannaBackgroundOverlay", PrimitiveSpriteLibrary.Square(), new Color(0.95f, 0.97f, 0.9f, 0.09f), Vector2.zero, new Vector2(arenaWidth, arenaHeight), 0f, BiomeOverlayOrder);
        overlay.sortingLayerName = SortingLayerDefault;
        overlay.sortingOrder = BiomeOverlayOrder;
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
        var arenaHeight = halfHeight * 2f;
        var riverHeight = arenaHeight + 1f;
        var riverCenterY = 0f;
        var floodWidth = Mathf.Max(mapConfig.floodplainWidth, mapConfig.riverWidth + 1f);
        var floodplainHeight = arenaHeight + 1f;
        floodplainRenderer = CreateSprite("Floodplain", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.5f, 0.72f, 0.37f, 1f), new Vector2(0f, riverCenterY), new Vector2(floodWidth, floodplainHeight), 0f, FloodplainOrder);
        floodplainRenderer.drawMode = SpriteDrawMode.Sliced;

        var riverBody = CreateSprite("RiverBody", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.14f, 0.47f, 0.92f, 1f), new Vector2(0f, riverCenterY), new Vector2(mapConfig.riverWidth, riverHeight), 0f, RiverOrder);
        riverBody.drawMode = SpriteDrawMode.Sliced;

        var bankWidth = Mathf.Max(0.16f, mapConfig.riverWidth * 0.14f);
        var bankOffset = (mapConfig.riverWidth * 0.5f) + (bankWidth * 0.5f);
        CreateSprite("RiverBankLeft", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.33f, 0.49f, 0.24f, 1f), new Vector2(-bankOffset, riverCenterY), new Vector2(bankWidth, riverHeight), 0f, RiverBankOrder).drawMode = SpriteDrawMode.Sliced;
        CreateSprite("RiverBankRight", PrimitiveSpriteLibrary.RoundedRectFill(), new Color(0.33f, 0.49f, 0.24f, 1f), new Vector2(bankOffset, riverCenterY), new Vector2(bankWidth, riverHeight), 0f, RiverBankOrder).drawMode = SpriteDrawMode.Sliced;

        const int sampleCount = 18;
        var riverStartY = -(riverHeight * 0.5f);
        var riverEndY = riverHeight * 0.5f;
        for (var i = 0; i <= sampleCount; i++)
        {
            var y = Mathf.Lerp(riverStartY, riverEndY, i / (float)sampleCount);
            waterNodes.Add(new Vector2(0f, y));
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
