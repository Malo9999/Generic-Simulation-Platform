using System;
using System.Collections.Generic;
using UnityEngine;

public static class PrimitiveSpriteLibrary
{
    private enum PrimitiveKind
    {
        CircleFill,
        CircleOutline,
        CapsuleFill,
        CapsuleOutline,
        RoundedRectFill,
        RoundedRectOutline
    }

    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static Sprite CircleFill(int sizePx = 64) => GetOrCreate(PrimitiveKind.CircleFill, sizePx);
    public static Sprite CircleOutline(int sizePx = 64) => GetOrCreate(PrimitiveKind.CircleOutline, sizePx);
    public static Sprite CapsuleFill(int sizePx = 64) => GetOrCreate(PrimitiveKind.CapsuleFill, sizePx);
    public static Sprite CapsuleOutline(int sizePx = 64) => GetOrCreate(PrimitiveKind.CapsuleOutline, sizePx);
    public static Sprite RoundedRectFill(int sizePx = 64) => GetOrCreate(PrimitiveKind.RoundedRectFill, sizePx);
    public static Sprite RoundedRectOutline(int sizePx = 64) => GetOrCreate(PrimitiveKind.RoundedRectOutline, sizePx);

    public static void ClearCache()
    {
        foreach (var sprite in SpriteCache.Values)
        {
            if (sprite == null)
            {
                continue;
            }

            var texture = sprite.texture;
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            UnityEngine.Object.Destroy(sprite);
        }

        SpriteCache.Clear();
    }

    private static Sprite GetOrCreate(PrimitiveKind kind, int sizePx)
    {
        sizePx = Mathf.Max(8, sizePx);
        var key = $"{kind}_{sizePx}";
        if (SpriteCache.TryGetValue(key, out var sprite) && sprite != null)
        {
            return sprite;
        }

        var pixels = new Color32[sizePx * sizePx];
        Clear(pixels, new Color32(255, 255, 255, 0));

        switch (kind)
        {
            case PrimitiveKind.CircleFill:
                DrawCircleFilled(pixels, sizePx, new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f), sizePx * 0.45f, new Color32(255, 255, 255, 255));
                break;
            case PrimitiveKind.CircleOutline:
                DrawCircleOutline(pixels, sizePx, new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f), sizePx * 0.45f, Mathf.Max(1f, sizePx * 0.08f), new Color32(24, 24, 24, 255));
                break;
            case PrimitiveKind.CapsuleFill:
                DrawCapsuleFilled(pixels, sizePx, new Rect(sizePx * 0.12f, sizePx * 0.34f, sizePx * 0.76f, sizePx * 0.32f), new Color32(255, 255, 255, 255));
                break;
            case PrimitiveKind.CapsuleOutline:
                DrawCapsuleOutline(pixels, sizePx, new Rect(sizePx * 0.12f, sizePx * 0.34f, sizePx * 0.76f, sizePx * 0.32f), Mathf.Max(1f, sizePx * 0.08f), new Color32(24, 24, 24, 255));
                break;
            case PrimitiveKind.RoundedRectFill:
                DrawRoundedRectFilled(pixels, sizePx, new Rect(sizePx * 0.1f, sizePx * 0.24f, sizePx * 0.8f, sizePx * 0.52f), sizePx * 0.14f, new Color32(255, 255, 255, 255));
                break;
            case PrimitiveKind.RoundedRectOutline:
                DrawRoundedRectOutline(pixels, sizePx, new Rect(sizePx * 0.1f, sizePx * 0.24f, sizePx * 0.8f, sizePx * 0.52f), sizePx * 0.14f, Mathf.Max(1f, sizePx * 0.08f), new Color32(24, 24, 24, 255));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        sprite = Sprite.Create(texture, new Rect(0f, 0f, sizePx, sizePx), new Vector2(0.5f, 0.5f), sizePx);
        SpriteCache[key] = sprite;
        return sprite;
    }

    private static void DrawCircleFilled(Color32[] pixels, int sizePx, Vector2 center, float radius, Color32 color)
    {
        var radiusSq = radius * radius;
        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var dx = x - center.x;
                var dy = y - center.y;
                if ((dx * dx) + (dy * dy) <= radiusSq)
                {
                    pixels[(y * sizePx) + x] = color;
                }
            }
        }
    }

    private static void DrawCircleOutline(Color32[] pixels, int sizePx, Vector2 center, float radius, float thickness, Color32 color)
    {
        var outer = radius;
        var inner = Mathf.Max(0f, radius - thickness);
        var outerSq = outer * outer;
        var innerSq = inner * inner;
        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var dx = x - center.x;
                var dy = y - center.y;
                var d = (dx * dx) + (dy * dy);
                if (d <= outerSq && d >= innerSq)
                {
                    pixels[(y * sizePx) + x] = color;
                }
            }
        }
    }

    private static void DrawCapsuleFilled(Color32[] pixels, int sizePx, Rect rect, Color32 color)
    {
        var radius = rect.height * 0.5f;
        var leftCenter = new Vector2(rect.xMin + radius, rect.center.y);
        var rightCenter = new Vector2(rect.xMax - radius, rect.center.y);

        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var point = new Vector2(x, y);
                var inMiddle = x >= leftCenter.x && x <= rightCenter.x && y >= rect.yMin && y <= rect.yMax;
                var inLeft = (point - leftCenter).sqrMagnitude <= radius * radius;
                var inRight = (point - rightCenter).sqrMagnitude <= radius * radius;

                if (inMiddle || inLeft || inRight)
                {
                    pixels[(y * sizePx) + x] = color;
                }
            }
        }
    }

    private static void DrawCapsuleOutline(Color32[] pixels, int sizePx, Rect rect, float thickness, Color32 color)
    {
        var fill = new Color32[sizePx * sizePx];
        var inner = new Color32[sizePx * sizePx];
        DrawCapsuleFilled(fill, sizePx, rect, color);
        var inset = new Rect(rect.xMin + thickness, rect.yMin + thickness, Mathf.Max(1f, rect.width - (2f * thickness)), Mathf.Max(1f, rect.height - (2f * thickness)));
        DrawCapsuleFilled(inner, sizePx, inset, color);
        for (var i = 0; i < pixels.Length; i++)
        {
            if (fill[i].a > 0 && inner[i].a == 0)
            {
                pixels[i] = color;
            }
        }
    }

    private static void DrawRoundedRectFilled(Color32[] pixels, int sizePx, Rect rect, float radius, Color32 color)
    {
        var radiusSq = radius * radius;
        var left = rect.xMin;
        var right = rect.xMax;
        var bottom = rect.yMin;
        var top = rect.yMax;

        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var insideCore = x >= left + radius && x <= right - radius && y >= bottom && y <= top;
                insideCore |= y >= bottom + radius && y <= top - radius && x >= left && x <= right;

                if (insideCore)
                {
                    pixels[(y * sizePx) + x] = color;
                    continue;
                }

                var corner = GetNearestCorner(left, right, bottom, top, radius, x, y);
                if (corner.HasValue)
                {
                    var d = new Vector2(x, y) - corner.Value;
                    if (d.sqrMagnitude <= radiusSq)
                    {
                        pixels[(y * sizePx) + x] = color;
                    }
                }
            }
        }
    }

    private static void DrawRoundedRectOutline(Color32[] pixels, int sizePx, Rect rect, float radius, float thickness, Color32 color)
    {
        var fill = new Color32[sizePx * sizePx];
        var inner = new Color32[sizePx * sizePx];
        DrawRoundedRectFilled(fill, sizePx, rect, radius, color);

        var insetRect = new Rect(rect.xMin + thickness, rect.yMin + thickness, Mathf.Max(1f, rect.width - (2f * thickness)), Mathf.Max(1f, rect.height - (2f * thickness)));
        var insetRadius = Mathf.Max(0f, radius - thickness);
        DrawRoundedRectFilled(inner, sizePx, insetRect, insetRadius, color);

        for (var i = 0; i < pixels.Length; i++)
        {
            if (fill[i].a > 0 && inner[i].a == 0)
            {
                pixels[i] = color;
            }
        }
    }

    private static Vector2? GetNearestCorner(float left, float right, float bottom, float top, float radius, float x, float y)
    {
        var cx = x < left + radius ? left + radius : (x > right - radius ? right - radius : float.NaN);
        var cy = y < bottom + radius ? bottom + radius : (y > top - radius ? top - radius : float.NaN);

        if (float.IsNaN(cx) || float.IsNaN(cy))
        {
            return null;
        }

        return new Vector2(cx, cy);
    }

    private static void Clear(Color32[] pixels, Color32 color)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
    }
}
