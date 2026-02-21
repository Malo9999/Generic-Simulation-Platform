using System;
using System.Collections.Generic;
using UnityEngine;

public static class ArenaBuilder
{
    private const float PixelsPerUnit = 8f;
    private const int BackgroundSortingOrder = -100;
    private const int BorderSortingOrder = -10;

    private static Sprite whitePixelSprite;

    public static void Build(Transform simulationRoot, ScenarioConfig config)
    {
        if (simulationRoot == null || config == null)
        {
            return;
        }

        ClearExistingArenaRoots(simulationRoot);

        var world = config.world ?? new WorldConfig();
        var width = Mathf.Max(4f, world.arenaWidth);
        var height = Mathf.Max(4f, world.arenaHeight);
        var halfWidth = width * 0.5f;
        var halfHeight = height * 0.5f;

        var arenaRoot = new GameObject("ArenaRoot");
        arenaRoot.transform.SetParent(simulationRoot, false);
        arenaRoot.transform.localPosition = Vector3.zero;

        var obstacles = new List<ArenaLayout.ObstacleCircle>();

        BuildBackground(arenaRoot.transform, width, height);
        BuildBorders(arenaRoot.transform, halfWidth, halfHeight, world.walls);
        BuildObstacles(arenaRoot.transform, config.seed, world.obstacleDensity, halfWidth, halfHeight, obstacles);

        var layout = arenaRoot.AddComponent<ArenaLayout>();
        layout.SetData(halfWidth, halfHeight, obstacles);

        ArenaDecorBuilder.EnsureDecorRoot(arenaRoot.transform);
        ArenaDecorBuilder.BuildDecor(arenaRoot.transform, config, ResolveSimId(config));
    }

    private static void ClearExistingArenaRoots(Transform simulationRoot)
    {
        for (var i = simulationRoot.childCount - 1; i >= 0; i--)
        {
            var child = simulationRoot.GetChild(i);
            if (!string.Equals(child.name, "ArenaRoot", StringComparison.Ordinal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static string ResolveSimId(ScenarioConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.activeSimulation))
        {
            return config.activeSimulation;
        }

        if (!string.IsNullOrWhiteSpace(config.simulationId))
        {
            return config.simulationId;
        }

        return config.scenarioName;
    }

    private static void BuildBackground(Transform parent, float width, float height)
    {
        var background = new GameObject("Background");
        background.transform.SetParent(parent, false);
        background.transform.localPosition = Vector3.zero;

        var renderer = background.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = BackgroundSortingOrder;

        var texWidth = Mathf.Max(8, Mathf.RoundToInt(width * PixelsPerUnit));
        var texHeight = Mathf.Max(8, Mathf.RoundToInt(height * PixelsPerUnit));
        var texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[texWidth * texHeight];
        var c0 = new Color32(30, 35, 42, 255);
        var c1 = new Color32(34, 39, 47, 255);
        for (var y = 0; y < texHeight; y++)
        {
            for (var x = 0; x < texWidth; x++)
            {
                var idx = (y * texWidth) + x;
                var checker = ((x + y) & 1) == 0;
                var noise = ((x * 17) + (y * 31)) % 11 == 0;
                pixels[idx] = noise ? c1 : (checker ? c0 : c1);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texWidth, texHeight), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }

    private static void BuildBorders(Transform parent, float halfWidth, float halfHeight, bool wallsEnabled)
    {
        var borders = new GameObject("Borders");
        borders.transform.SetParent(parent, false);
        borders.transform.localPosition = Vector3.zero;

        var borderColor = wallsEnabled ? new Color(0.8f, 0.86f, 0.95f, 0.95f) : new Color(0.8f, 0.86f, 0.95f, 0.28f);
        var thickness = wallsEnabled ? 0.5f : 0.38f;

        CreateBorderSegment(borders.transform, "Top", new Vector3(0f, halfHeight, 0f), new Vector2((halfWidth * 2f) + thickness, thickness), borderColor);
        CreateBorderSegment(borders.transform, "Bottom", new Vector3(0f, -halfHeight, 0f), new Vector2((halfWidth * 2f) + thickness, thickness), borderColor);
        CreateBorderSegment(borders.transform, "Left", new Vector3(-halfWidth, 0f, 0f), new Vector2(thickness, (halfHeight * 2f) + thickness), borderColor);
        CreateBorderSegment(borders.transform, "Right", new Vector3(halfWidth, 0f, 0f), new Vector2(thickness, (halfHeight * 2f) + thickness), borderColor);
    }

    private static void BuildObstacles(
        Transform parent,
        int seed,
        float density,
        float halfWidth,
        float halfHeight,
        List<ArenaLayout.ObstacleCircle> obstacleCircles)
    {
        var obstaclesRoot = new GameObject("Obstacles");
        obstaclesRoot.transform.SetParent(parent, false);
        obstaclesRoot.transform.localPosition = Vector3.zero;

        var localSeed = seed ^ unchecked((int)0xA8F1D2C3);
        var rng = new SeededRng(localSeed);

        var width = halfWidth * 2f;
        var height = halfHeight * 2f;
        var obstacleCount = Mathf.Clamp(Mathf.RoundToInt(width * height * Mathf.Max(0f, density) * 0.05f), 0, 200);
        var margin = Mathf.Min(2f, Mathf.Min(halfWidth * 0.5f, halfHeight * 0.5f));
        var clearRadius = 6f;

        for (var i = 0; i < obstacleCount; i++)
        {
            Vector2 position;
            var foundPosition = false;

            for (var attempt = 0; attempt < 30; attempt++)
            {
                position = new Vector2(
                    rng.Range(-halfWidth + margin, halfWidth - margin),
                    rng.Range(-halfHeight + margin, halfHeight - margin));

                if (position.sqrMagnitude <= clearRadius * clearRadius)
                {
                    continue;
                }

                foundPosition = true;
                CreateObstacle(obstaclesRoot.transform, rng, i, position, obstacleCircles);
                break;
            }

            if (!foundPosition)
            {
                break;
            }
        }
    }

    private static void CreateObstacle(Transform parent, SeededRng rng, int index, Vector2 position, List<ArenaLayout.ObstacleCircle> obstacleCircles)
    {
        var obstacle = new GameObject($"Obstacle_{index:000}");
        obstacle.transform.SetParent(parent, false);
        obstacle.transform.localPosition = new Vector3(position.x, position.y, 0f);

        var shapeRoll = rng.Range(0, 3);
        var fillRenderer = obstacle.AddComponent<SpriteRenderer>();
        var outline = new GameObject("Outline");
        outline.transform.SetParent(obstacle.transform, false);
        outline.transform.localPosition = Vector3.zero;
        var outlineRenderer = outline.AddComponent<SpriteRenderer>();

        switch (shapeRoll)
        {
            case 0:
                fillRenderer.sprite = PrimitiveSpriteLibrary.CircleFill();
                outlineRenderer.sprite = PrimitiveSpriteLibrary.CircleOutline();
                break;
            case 1:
                fillRenderer.sprite = PrimitiveSpriteLibrary.RoundedRectFill();
                outlineRenderer.sprite = PrimitiveSpriteLibrary.RoundedRectOutline();
                break;
            default:
                fillRenderer.sprite = PrimitiveSpriteLibrary.CapsuleFill();
                outlineRenderer.sprite = PrimitiveSpriteLibrary.CapsuleOutline();
                break;
        }

        var baseScale = rng.Range(0.8f, 1.8f);
        var stretch = shapeRoll == 2 ? rng.Range(1.2f, 1.8f) : rng.Range(0.9f, 1.25f);
        obstacle.transform.localScale = new Vector3(baseScale * stretch, baseScale, 1f);
        obstacle.transform.localRotation = Quaternion.Euler(0f, 0f, rng.Range(0f, 360f));

        fillRenderer.sortingOrder = rng.Range(-5, 0);
        outlineRenderer.sortingOrder = fillRenderer.sortingOrder + 1;

        var tint = new Color(
            rng.Range(0.28f, 0.44f),
            rng.Range(0.38f, 0.56f),
            rng.Range(0.24f, 0.42f),
            0.95f);
        fillRenderer.color = tint;
        outlineRenderer.color = new Color(tint.r * 0.45f, tint.g * 0.45f, tint.b * 0.45f, 0.95f);

        var approxRadius = Mathf.Max(obstacle.transform.localScale.x, obstacle.transform.localScale.y) * 0.6f;
        obstacleCircles.Add(new ArenaLayout.ObstacleCircle(position, approxRadius));
    }

    private static void CreateBorderSegment(Transform parent, string name, Vector3 localPos, Vector2 scale, Color color)
    {
        var segment = new GameObject(name);
        segment.transform.SetParent(parent, false);
        segment.transform.localPosition = localPos;
        segment.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var renderer = segment.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhitePixelSprite();
        renderer.color = color;
        renderer.sortingOrder = BorderSortingOrder;
    }

    private static Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null)
        {
            return whitePixelSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);

        whitePixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whitePixelSprite;
    }
}

public sealed class ArenaLayout : MonoBehaviour
{
    [Serializable]
    public struct ObstacleCircle
    {
        public Vector2 position;
        public float radius;

        public ObstacleCircle(Vector2 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    [SerializeField] private float halfWidth;
    [SerializeField] private float halfHeight;
    [SerializeField] private List<ObstacleCircle> obstacles = new();

    public float HalfWidth => halfWidth;
    public float HalfHeight => halfHeight;
    public IReadOnlyList<ObstacleCircle> Obstacles => obstacles;

    public void SetData(float widthHalfExtent, float heightHalfExtent, List<ObstacleCircle> obstacleData)
    {
        halfWidth = widthHalfExtent;
        halfHeight = heightHalfExtent;
        obstacles = obstacleData ?? new List<ObstacleCircle>();
    }
}
