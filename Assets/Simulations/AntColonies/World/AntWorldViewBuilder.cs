using System;
using UnityEngine;

public static class AntWorldViewBuilder
{
    private const int TileOrder = -50;
    private const int DecorOrder = -20;
    private const int EntityOrder = -5;

    private static Sprite squareSprite;
    private static Sprite circleSprite;

    public static void BuildOrRefresh(Transform parent, ScenarioConfig config, AntWorldState state)
    {
        if (parent == null || state == null)
        {
            return;
        }

        var existing = parent.Find("AntWorldView");
        if (existing != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(existing.gameObject);
            else UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        var root = new GameObject("AntWorldView");
        root.transform.SetParent(parent, false);

        BuildTiles(root.transform, config, state);
        BuildDecor(root.transform, state);
        BuildObstacles(root.transform, state);
        BuildFood(root.transform, state);
        BuildNests(root.transform, state);
    }

    private static void BuildTiles(Transform root, ScenarioConfig config, AntWorldState state)
    {
        var tiles = new GameObject("Tiles");
        tiles.transform.SetParent(root, false);
        var worldWidth = Mathf.Max(1f, config?.world?.arenaWidth ?? 64);
        var worldHeight = Mathf.Max(1f, config?.world?.arenaHeight ?? 64);
        var startX = -worldWidth * 0.5f + (worldWidth / AntWorldState.TileSize) * 0.5f;
        var startY = -worldHeight * 0.5f + (worldHeight / AntWorldState.TileSize) * 0.5f;
        var sx = worldWidth / AntWorldState.TileSize;
        var sy = worldHeight / AntWorldState.TileSize;

        for (var y = 0; y < AntWorldState.TileSize; y++)
        {
            for (var x = 0; x < AntWorldState.TileSize; x++)
            {
                var idx = (y * AntWorldState.TileSize) + x;
                var tile = new GameObject($"T_{x:00}_{y:00}");
                tile.transform.SetParent(tiles.transform, false);
                tile.transform.localPosition = new Vector3(startX + (x * sx), startY + (y * sy), 0f);
                tile.transform.localScale = new Vector3(sx, sy, 1f);

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sortingOrder = TileOrder;
                sr.sprite = TrySprite(state.tileSpriteIds[idx], out var sprite) ? sprite : GetSquareSprite();
                sr.color = sr.sprite == squareSprite ? new Color(0.25f, 0.4f, 0.2f, 1f) : Color.white;
            }
        }
    }

    private static void BuildDecor(Transform root, AntWorldState state)
    {
        var decorRoot = new GameObject("Decor");
        decorRoot.transform.SetParent(root, false);
        for (var i = 0; i < state.decor.Count; i++)
        {
            var entry = state.decor[i];
            var go = new GameObject($"Decor_{i:000}");
            go.transform.SetParent(decorRoot.transform, false);
            go.transform.localPosition = new Vector3(entry.position.x, entry.position.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, entry.rotation);
            go.transform.localScale = Vector3.one * entry.scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = DecorOrder;
            sr.sprite = TrySprite(entry.spriteId, out var sprite) ? sprite : GetCircleSprite();
            sr.color = sr.sprite == circleSprite ? new Color(0.3f, 0.6f, 0.3f, entry.alpha) : new Color(1f, 1f, 1f, entry.alpha);
        }
    }

    private static void BuildNests(Transform root, AntWorldState state)
    {
        var nests = new GameObject("Nests");
        nests.transform.SetParent(root, false);
        for (var i = 0; i < state.nests.Count; i++)
        {
            var n = state.nests[i];
            var go = new GameObject($"Nest_{n.teamId}_{n.speciesId}");
            go.transform.SetParent(nests.transform, false);
            go.transform.localPosition = new Vector3(n.position.x, n.position.y, 0f);
            go.transform.localScale = Vector3.one * 1.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            var nestId = "prop:ant:prop_nest_entrance_medium:default:na:na:00";
            sr.sprite = TrySprite(nestId, out var sprite) ? sprite : GetCircleSprite();
            sr.color = sr.sprite == circleSprite ? n.teamColor : Color.white;
            sr.sortingOrder = EntityOrder;
        }
    }

    private static void BuildFood(Transform root, AntWorldState state)
    {
        var foodRoot = new GameObject("Food");
        foodRoot.transform.SetParent(root, false);
        for (var i = 0; i < state.foodPiles.Count; i++)
        {
            var f = state.foodPiles[i];
            var go = new GameObject($"Food_{f.id:00}");
            go.transform.SetParent(foodRoot.transform, false);
            go.transform.localPosition = new Vector3(f.position.x, f.position.y, 0f);
            go.transform.localScale = Vector3.one * 1.2f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = TrySprite("prop:ant:prop_food_large:default:na:na:00", out var sprite)
                ? sprite
                : GetSquareSprite();
            sr.color = sr.sprite == squareSprite ? new Color(0.9f, 0.78f, 0.2f, 1f) : Color.white;
            sr.sortingOrder = EntityOrder + 1;
        }
    }

    private static void BuildObstacles(Transform root, AntWorldState state)
    {
        var obstacleRoot = new GameObject("Obstacles");
        obstacleRoot.transform.SetParent(root, false);
        for (var i = 0; i < state.obstacles.Count; i++)
        {
            var o = state.obstacles[i];
            var go = new GameObject($"Rock_{i:00}");
            go.transform.SetParent(obstacleRoot.transform, false);
            go.transform.localPosition = new Vector3(o.position.x, o.position.y, 0f);
            go.transform.localScale = Vector3.one * (o.radius * 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.38f, 0.38f, 0.4f, 1f);
            sr.sortingOrder = DecorOrder + 1;
        }
    }

    private static bool TrySprite(string id, out Sprite sprite)
    {
        sprite = null;
        return !string.IsNullOrWhiteSpace(id) && ContentPackService.TryGetSprite(id, out sprite);
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tx = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tx.SetPixel(0, 0, Color.white);
        tx.Apply(false, false);
        squareSprite = Sprite.Create(tx, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 32;
        var tx = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var c = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                tx.SetPixel(x, y, d <= c ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }

        tx.Apply(false, false);
        circleSprite = Sprite.Create(tx, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }
}
