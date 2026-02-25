using System.Collections.Generic;
using UnityEngine;

public static class DebugShapeSpriteFactory
{
    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static Sprite GetCircleSprite(int size = 16)
    {
        return GetOrCreateSprite($"circle_{size}", size, BuildCircleTexture);
    }

    public static Sprite GetSquareSprite(int size = 16)
    {
        return GetOrCreateSprite($"square_{size}", size, BuildSquareTexture);
    }

    public static Sprite GetDiamondSprite(int size = 16)
    {
        return GetOrCreateSprite($"diamond_{size}", size, BuildDiamondTexture);
    }

    public static Sprite GetArrowSprite(int size = 16)
    {
        return GetOrCreateSprite($"arrow_{size}", size, BuildArrowTexture);
    }

    private static Sprite GetOrCreateSprite(string cacheKey, int size, System.Func<int, Texture2D> textureBuilder)
    {
        size = Mathf.Max(4, size);
        if (SpriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var texture = textureBuilder(size);
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect);
        sprite.name = cacheKey;
        SpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private static Texture2D CreateTexture(int size, string name)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[size * size];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }

        texture.SetPixels32(pixels);
        return texture;
    }

    private static Texture2D BuildCircleTexture(int size)
    {
        var texture = CreateTexture(size, $"DebugCircle_{size}");
        var fill = new Color32(255, 255, 255, 255);
        var outline = new Color32(0, 0, 0, 255);
        var center = (size - 1) * 0.5f;
        var radius = size * 0.36f;
        var outlineStart = Mathf.Max(0f, radius - 1f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (distance <= radius)
                {
                    texture.SetPixel(x, y, distance >= outlineStart ? outline : fill);
                }
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildSquareTexture(int size)
    {
        var texture = CreateTexture(size, $"DebugSquare_{size}");
        var fill = new Color32(255, 255, 255, 255);
        var outline = new Color32(0, 0, 0, 255);

        for (var y = 1; y < size - 1; y++)
        {
            for (var x = 1; x < size - 1; x++)
            {
                var isOutline = x == 1 || x == size - 2 || y == 1 || y == size - 2;
                texture.SetPixel(x, y, isOutline ? outline : fill);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildDiamondTexture(int size)
    {
        var texture = CreateTexture(size, $"DebugDiamond_{size}");
        var fill = new Color32(255, 255, 255, 255);
        var outline = new Color32(0, 0, 0, 255);
        var center = (size - 1) / 2;
        var radius = center - 1;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                if (distance <= radius)
                {
                    texture.SetPixel(x, y, distance >= radius - 1 ? outline : fill);
                }
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildArrowTexture(int size)
    {
        var texture = CreateTexture(size, $"DebugArrow_{size}");
        var fill = new Color32(255, 255, 255, 255);
        var outline = new Color32(0, 0, 0, 255);

        var centerY = size / 2;
        var shaftStart = 2;
        var shaftEnd = Mathf.Max(shaftStart + 1, size - 6);
        var headStart = shaftEnd - 1;

        for (var x = shaftStart; x <= shaftEnd; x++)
        {
            for (var y = centerY - 1; y <= centerY + 1; y++)
            {
                texture.SetPixel(x, y, fill);
            }
        }

        for (var x = headStart; x < size - 2; x++)
        {
            var spread = (x - headStart) + 1;
            for (var y = centerY - spread; y <= centerY + spread; y++)
            {
                if (y > 0 && y < size - 1)
                {
                    texture.SetPixel(x, y, fill);
                }
            }
        }

        for (var y = 1; y < size - 1; y++)
        {
            for (var x = 1; x < size - 1; x++)
            {
                if (texture.GetPixel(x, y).a <= 0f)
                {
                    continue;
                }

                var isEdge = texture.GetPixel(x + 1, y).a <= 0f ||
                             texture.GetPixel(x - 1, y).a <= 0f ||
                             texture.GetPixel(x, y + 1).a <= 0f ||
                             texture.GetPixel(x, y - 1).a <= 0f;
                if (isEdge)
                {
                    texture.SetPixel(x, y, outline);
                }
            }
        }

        texture.Apply(false, false);
        return texture;
    }
}
