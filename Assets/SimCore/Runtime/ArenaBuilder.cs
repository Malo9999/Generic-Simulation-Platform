using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ArenaBuilder
{
    private const float PixelsPerUnit = 8f;
    private const int BackgroundSortingOrder = -100;
    private const int GroundTileSortingOrder = -90;
    private const int PackDecorOrderMin = -20;
    private const int PackDecorOrderMax = -15;
    private const int BorderSortingOrder = -10;
    private const int ObstacleFillSortingOrder = -5;
    private const int ObstacleOutlineSortingOrder = -4;
    private const float ObstacleOverlapPadding = 0.4f;

    private static Sprite whitePixelSprite;

    public static void Build(Transform simulationRoot, ScenarioConfig config)
    {
        if (simulationRoot == null || config == null)
        {
            return;
        }

        var sceneGraph = SimulationSceneGraph.Ensure(simulationRoot);
        ClearExistingArenaRoots(simulationRoot, sceneGraph.ArenaRootParent);

        var world = config.world ?? new WorldConfig();
        var width = Mathf.Max(4f, world.arenaWidth);
        var height = Mathf.Max(4f, world.arenaHeight);
        var halfWidth = width * 0.5f;
        var halfHeight = height * 0.5f;

        var arenaRoot = new GameObject("ArenaRoot");
        arenaRoot.transform.SetParent(sceneGraph.ArenaRootParent, false);
        arenaRoot.transform.localPosition = Vector3.zero;

        var obstacles = new List<ArenaLayout.ObstacleCircle>();

        var pack = ContentPackService.Current;
        var didBuildPackTiles = pack != null && BuildPackGroundTiles(arenaRoot.transform, config, pack, width, height);
        if (!didBuildPackTiles)
        {
            BuildBackground(arenaRoot.transform, width, height);
        }

        BuildBorders(arenaRoot.transform, halfWidth, halfHeight, world.walls);
        var simId = ResolveSimId(config);
        if (ShouldBuildGenericObstacles(simId) && world.obstacleDensity > 0f)
        {
            BuildObstacles(arenaRoot.transform, world.obstacleDensity, halfWidth, halfHeight, obstacles);
        }

        var layout = arenaRoot.AddComponent<ArenaLayout>();
        layout.SetData(halfWidth, halfHeight, obstacles);

        var didBuildPackDecor = pack != null && BuildPackDecorProps(sceneGraph.DecorRoot, pack, halfWidth, halfHeight);
        if (!didBuildPackDecor)
        {
            ArenaDecorBuilder.BuildDecor(sceneGraph.DecorRoot, arenaRoot.transform, config, simId);
        }
    }

    private static bool ShouldBuildGenericObstacles(string simId)
    {
        if (string.IsNullOrWhiteSpace(simId))
        {
            return false;
        }

        return string.Equals(simId, "AntColonies", StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearExistingArenaRoots(Transform simulationRoot, Transform arenaRootParent)
    {
        ClearExistingArenaRootsInParent(simulationRoot);
        if (arenaRootParent != null)
        {
            ClearExistingArenaRootsInParent(arenaRootParent);
        }
    }

    private static void ClearExistingArenaRootsInParent(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
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

    private static bool BuildPackGroundTiles(Transform parent, ScenarioConfig config, ContentPack pack, float width, float height)
    {
        var tileIds = pack.GetAllSpriteIds().Where(id => id.StartsWith("tile:", StringComparison.Ordinal)).ToList();
        if (tileIds.Count == 0)
        {
            return false;
        }

        var tileNamespace = ResolveMostCommonNamespace(tileIds, "tile");
        if (string.IsNullOrWhiteSpace(tileNamespace))
        {
            return false;
        }

        var surfacePrefix = $"tile:{tileNamespace}:surface:";
        var surfaceIds = tileIds.Where(id => id.StartsWith(surfacePrefix, StringComparison.Ordinal)).ToList();
        if (surfaceIds.Count == 0)
        {
            return false;
        }

        var preferredIds = surfaceIds.Where(id => id.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("grass", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        var candidates = preferredIds.Count > 0 ? preferredIds : surfaceIds;
        var sprites = candidates.Select(id =>
        {
            pack.TryGetSprite(id, out var sprite);
            return sprite;
        }).Where(sprite => sprite != null).ToList();
        if (sprites.Count == 0)
        {
            return false;
        }

        var root = new GameObject("GroundTiles");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        var gridW = Mathf.Max(1, Mathf.RoundToInt(width));
        var gridH = Mathf.Max(1, Mathf.RoundToInt(height));
        var rng = RngService.Fork("WORLD:GROUND_TILES");
        var startX = -width * 0.5f + 0.5f;
        var startY = -height * 0.5f + 0.5f;

        for (var y = 0; y < gridH; y++)
        {
            for (var x = 0; x < gridW; x++)
            {
                var tile = new GameObject($"Tile_{x:000}_{y:000}");
                tile.transform.SetParent(root.transform, false);
                tile.transform.localPosition = new Vector3(startX + x, startY + y, 0f);

                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = GroundTileSortingOrder;
                renderer.sprite = sprites[rng.Range(0, sprites.Count)];
                renderer.color = Color.white;
            }
        }

        return true;
    }

    private static bool BuildPackDecorProps(Transform decorRoot, ContentPack pack, float halfWidth, float halfHeight)
    {
        var propIds = pack.GetAllSpriteIds().Where(id => id.StartsWith("prop:", StringComparison.Ordinal)).ToList();
        if (propIds.Count == 0)
        {
            return false;
        }

        var propNamespace = ResolveMostCommonNamespace(propIds, "prop");
        if (string.IsNullOrWhiteSpace(propNamespace))
        {
            return false;
        }

        var prefix = $"prop:{propNamespace}:";
        var sprites = propIds.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).Select(id =>
        {
            pack.TryGetSprite(id, out var sprite);
            return sprite;
        }).Where(sprite => sprite != null).ToList();

        if (sprites.Count == 0)
        {
            return false;
        }

        var resolvedDecorRoot = ArenaDecorBuilder.EnsureDecorRoot(decorRoot);
        ArenaDecorBuilder.ClearChildren(resolvedDecorRoot);

        var minHalf = Mathf.Min(halfWidth, halfHeight);
        var margin = Mathf.Clamp(minHalf * 0.07f, 1.5f, 2.5f);
        var clearRadius = Mathf.Clamp(minHalf * 0.2f, 6f, 8f);
        var budget = Mathf.Clamp(Mathf.RoundToInt((halfWidth * halfHeight) * 0.12f), 80, 200);

        var rng = RngService.Fork("DECOR:PACK_PROPS");

        for (var i = 0; i < budget; i++)
        {
            var position = new Vector2(
                rng.Range(-halfWidth + margin, halfWidth - margin),
                rng.Range(-halfHeight + margin, halfHeight - margin));

            if (position.sqrMagnitude < clearRadius * clearRadius)
            {
                continue;
            }

            var prop = new GameObject($"Prop_{i:000}");
            prop.transform.SetParent(resolvedDecorRoot, false);
            prop.transform.localPosition = new Vector3(position.x, position.y, 0f);
            prop.transform.localRotation = Quaternion.identity;

            var renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[rng.Range(0, sprites.Count)];
            renderer.color = new Color(1f, 1f, 1f, rng.Range(0.25f, 0.8f));
            renderer.sortingOrder = rng.Range(PackDecorOrderMin, PackDecorOrderMax + 1);

            var scale = rng.Range(0.8f, 1.3f);
            prop.transform.localScale = new Vector3(scale, scale, 1f);
        }

        return true;
    }

    private static string ResolveMostCommonNamespace(List<string> ids, string category)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string first = null;

        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            var parts = id.Split(':');
            if (parts.Length < 3 || !string.Equals(parts[0], category, StringComparison.Ordinal))
            {
                continue;
            }

            var ns = parts[1];
            if (string.IsNullOrWhiteSpace(ns))
            {
                continue;
            }

            if (first == null)
            {
                first = ns;
            }

            counts[ns] = counts.TryGetValue(ns, out var count) ? count + 1 : 1;
        }

        if (counts.Count == 0)
        {
            return string.Empty;
        }

        var best = first;
        var bestCount = -1;
        foreach (var kvp in counts)
        {
            if (kvp.Value > bestCount)
            {
                best = kvp.Key;
                bestCount = kvp.Value;
            }
        }

        return best ?? string.Empty;
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
        float density,
        float halfWidth,
        float halfHeight,
        List<ArenaLayout.ObstacleCircle> obstacleCircles)
    {
        var obstaclesRoot = new GameObject("Obstacles");
        obstaclesRoot.transform.SetParent(parent, false);
        obstaclesRoot.transform.localPosition = Vector3.zero;

        var rng = RngService.Fork("WORLD:OBSTACLES");

        var width = halfWidth * 2f;
        var height = halfHeight * 2f;
        var obstacleCount = Mathf.Clamp(Mathf.RoundToInt(width * height * Mathf.Max(0f, density) * 0.05f), 0, 200);
        var margin = Mathf.Min(2f, Mathf.Min(halfWidth * 0.5f, halfHeight * 0.5f));
        var clearRadius = 6f;

        for (var i = 0; i < obstacleCount; i++)
        {
            Vector2 position;
            var foundPosition = false;

            for (var attempt = 0; attempt < 40; attempt++)
            {
                var shapeRoll = rng.Range(0, 3);
                var baseScale = rng.Range(0.8f, 1.8f);
                var stretch = shapeRoll == 2 ? rng.Range(1.2f, 1.8f) : rng.Range(0.9f, 1.25f);
                var approxRadius = Mathf.Max(baseScale * stretch, baseScale) * 0.6f;

                position = new Vector2(
                    rng.Range(-halfWidth + margin, halfWidth - margin),
                    rng.Range(-halfHeight + margin, halfHeight - margin));

                if (position.sqrMagnitude <= clearRadius * clearRadius)
                {
                    continue;
                }

                if (IntersectsExistingObstacle(position, approxRadius, obstacleCircles))
                {
                    continue;
                }

                foundPosition = true;
                CreateObstacle(obstaclesRoot.transform, rng, i, position, shapeRoll, baseScale, stretch, approxRadius, obstacleCircles);
                break;
            }

            if (!foundPosition)
            {
                break;
            }
        }
    }

    private static bool IntersectsExistingObstacle(Vector2 candidatePosition, float candidateRadius, List<ArenaLayout.ObstacleCircle> obstacleCircles)
    {
        for (var i = 0; i < obstacleCircles.Count; i++)
        {
            var existing = obstacleCircles[i];
            var allowedDistance = candidateRadius + existing.radius + ObstacleOverlapPadding;
            if ((candidatePosition - existing.position).sqrMagnitude < allowedDistance * allowedDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static void CreateObstacle(
        Transform parent,
        IRng rng,
        int index,
        Vector2 position,
        int shapeRoll,
        float baseScale,
        float stretch,
        float approxRadius,
        List<ArenaLayout.ObstacleCircle> obstacleCircles)
    {
        var obstacle = new GameObject($"Obstacle_{index:000}");
        obstacle.transform.SetParent(parent, false);
        obstacle.transform.localPosition = new Vector3(position.x, position.y, 0f);

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

        obstacle.transform.localScale = new Vector3(baseScale * stretch, baseScale, 1f);
        obstacle.transform.localRotation = Quaternion.Euler(0f, 0f, rng.Range(0f, 360f));

        fillRenderer.sortingOrder = ObstacleFillSortingOrder;
        outlineRenderer.sortingOrder = ObstacleOutlineSortingOrder;

        var tint = new Color(
            rng.Range(0.28f, 0.44f),
            rng.Range(0.38f, 0.56f),
            rng.Range(0.24f, 0.42f),
            0.95f);
        fillRenderer.color = tint;
        outlineRenderer.color = new Color(tint.r * 0.45f, tint.g * 0.45f, tint.b * 0.45f, 0.95f);

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
