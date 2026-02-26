using System.Collections.Generic;
using UnityEngine;

public static class SelectionHaloSpriteFactory
{
    public const int DefaultSize = 64;
    public const int DefaultRadius = 22;
    public const float HaloPixelsPerUnit = 32f;

    private readonly struct HaloKey
    {
        public HaloKey(int size, int radius, int ringThickness, int outlineThickness, bool includeShine)
        {
            Size = size;
            Radius = radius;
            RingThickness = ringThickness;
            OutlineThickness = outlineThickness;
            IncludeShine = includeShine;
        }

        public int Size { get; }
        public int Radius { get; }
        public int RingThickness { get; }
        public int OutlineThickness { get; }
        public bool IncludeShine { get; }
    }

    private static readonly Dictionary<HaloKey, Sprite> Cache = new();

    public static Sprite GetBaseHaloSprite(int size = DefaultSize, int radius = DefaultRadius, int ringThickness = 2, int outlineThickness = 1)
    {
        return GetHaloSprite(new HaloKey(size, radius, ringThickness, outlineThickness, includeShine: false));
    }

    public static Sprite GetShineHaloSprite(int size = DefaultSize, int radius = DefaultRadius, int ringThickness = 2)
    {
        return GetHaloSprite(new HaloKey(size, radius, ringThickness, outlineThickness: 0, includeShine: true));
    }

    private static Sprite GetHaloSprite(HaloKey key)
    {
        if (Cache.TryGetValue(key, out var cachedSprite))
        {
            return cachedSprite;
        }

        var texture = new Texture2D(key.Size, key.Size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = key.IncludeShine ? "SelectionHaloShineTexture" : "SelectionHaloBaseTexture"
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var center = key.Size * 0.5f;
        var baseRing = new Color(1f, 0.84f, 0.2f, 1f);
        var darkOutline = new Color(0.15f, 0.1f, 0.03f, 1f);
        var brightArc = new Color(1f, 0.97f, 0.82f, 1f);

        var ringInnerRadius = key.Radius - 1f;
        var ringOuterRadius = key.Radius + 1f;
        var outlineInnerRadius = key.Radius + 1f;
        var outlineOuterRadius = key.Radius + 2f;

        for (var y = 0; y < key.Size; y++)
        {
            for (var x = 0; x < key.Size; x++)
            {
                texture.SetPixel(x, y, clear);

                var dx = (x + 0.5f) - center;
                var dy = (y + 0.5f) - center;
                var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                var isRingPixel = distance >= ringInnerRadius && distance <= ringOuterRadius;
                var isOutlinePixel = distance >= outlineInnerRadius && distance < outlineOuterRadius;

                if (!key.IncludeShine)
                {
                    if (isRingPixel)
                    {
                        texture.SetPixel(x, y, baseRing);
                    }
                    else if (key.OutlineThickness > 0 && isOutlinePixel)
                    {
                        texture.SetPixel(x, y, darkOutline);
                    }

                    continue;
                }

                var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f)
                {
                    angle += 360f;
                }

                var isInArc = angle >= 120f && angle <= 200f;
                if (isRingPixel && isInArc)
                {
                    texture.SetPixel(x, y, brightArc);
                }
            }
        }

        if (key.IncludeShine)
        {
            DrawSparkle(texture, center, -10, 12, brightArc);
            DrawSparkle(texture, center, -13, 9, brightArc);
            DrawSparkle(texture, center, -7, 15, brightArc);
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, key.Size, key.Size),
            new Vector2(0.5f, 0.5f),
            HaloPixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        sprite.name = key.IncludeShine ? "SelectionHaloShineSprite" : "SelectionHaloBaseSprite";

        Cache[key] = sprite;
        return sprite;
    }

    private static void DrawSparkle(Texture2D texture, float center, int offsetX, int offsetY, Color color)
    {
        var x = Mathf.RoundToInt(center + offsetX);
        var y = Mathf.RoundToInt(center + offsetY);

        SetIfInside(texture, x, y, color);
        SetIfInside(texture, x - 1, y, color);
        SetIfInside(texture, x + 1, y, color);
        SetIfInside(texture, x, y - 1, color);
        SetIfInside(texture, x, y + 1, color);
    }

    private static void SetIfInside(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
        {
            texture.SetPixel(x, y, color);
        }
    }
}
