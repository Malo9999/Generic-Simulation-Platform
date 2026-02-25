using System.Collections.Generic;
using UnityEngine;

public static class SelectionHaloSpriteFactory
{
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

    public static Sprite GetBaseHaloSprite(int size = 64, int radius = 22, int ringThickness = 2, int outlineThickness = 1)
    {
        return GetHaloSprite(new HaloKey(size, radius, ringThickness, outlineThickness, includeShine: false));
    }

    public static Sprite GetShineHaloSprite(int size = 64, int radius = 22, int ringThickness = 2)
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
        var center = (key.Size - 1) * 0.5f;
        var baseRing = new Color(1f, 0.93f, 0.35f, 0.95f);
        var darkOutline = new Color(0.28f, 0.2f, 0.06f, 0.98f);
        var brightArc = new Color(1f, 1f, 0.86f, 1f);

        var innerRadius = key.Radius - (key.RingThickness * 0.5f);
        var outerRadius = key.Radius + (key.RingThickness * 0.5f);
        var outlineOuterRadius = outerRadius + key.OutlineThickness;

        for (var y = 0; y < key.Size; y++)
        {
            for (var x = 0; x < key.Size; x++)
            {
                texture.SetPixel(x, y, clear);

                var dx = x - center;
                var dy = y - center;
                var distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                if (!key.IncludeShine)
                {
                    if (distance >= innerRadius && distance <= outerRadius)
                    {
                        texture.SetPixel(x, y, baseRing);
                    }
                    else if (key.OutlineThickness > 0 && distance > outerRadius && distance <= outlineOuterRadius)
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
                if (isInArc && distance >= innerRadius && distance <= outerRadius + 0.5f)
                {
                    texture.SetPixel(x, y, brightArc);
                }
            }
        }

        if (key.IncludeShine)
        {
            DrawSparkle(texture, key.Size / 2 - 12, key.Size / 2 + 14, brightArc);
            DrawSparkle(texture, key.Size / 2 - 7, key.Size / 2 + 18, brightArc);
            DrawSparkle(texture, key.Size / 2 - 18, key.Size / 2 + 9, brightArc);
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, key.Size, key.Size),
            new Vector2(0.5f, 0.5f),
            key.Size,
            0,
            SpriteMeshType.FullRect);

        sprite.name = key.IncludeShine ? "SelectionHaloShineSprite" : "SelectionHaloBaseSprite";

        Cache[key] = sprite;
        return sprite;
    }

    private static void DrawSparkle(Texture2D texture, int x, int y, Color color)
    {
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
