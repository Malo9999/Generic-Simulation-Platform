using System.Collections.Generic;
using UnityEngine;

public static class RuntimeShapeCache
{
    private static readonly Dictionary<string, Sprite> Cache = new();
    private static ShapeLibrary library;

    public static Sprite Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (Cache.TryGetValue(id, out var cached) && cached != null)
        {
            return cached;
        }

        if (library == null)
        {
            library = Resources.Load<ShapeLibrary>("ShapeLibrary");
        }

        if (library != null && library.TryGet(id, out var fromLibrary))
        {
            Cache[id] = fromLibrary;
            return fromLibrary;
        }

        var generated = GenerateFallback(id);
        if (generated != null)
        {
            Cache[id] = generated;
        }

        return generated;
    }

    public static void Clear()
    {
        Cache.Clear();
    }

    private static Sprite GenerateFallback(string id)
    {
        Color32[] pixels;
        var tint = Color.white;
        var size = 64;
        var ppu = 16;

        switch (id)
        {
            case ShapeId.DotCore:
                pixels = ShapeRasterizer.RasterizeDotCore(64, tint, 10f, 2f, 0.9f, 0.25f);
                size = 64;
                break;
            case ShapeId.DotGlow:
                pixels = ShapeRasterizer.RasterizeGlowDot(128, tint, 14f, 44f, 2.6f, 0.35f);
                size = 128;
                break;
            case ShapeId.DotGlowSmall:
                pixels = ShapeRasterizer.RasterizeGlowDot(64, tint, 6f, 22f, 2.2f, 0.22f);
                size = 64;
                break;
            case ShapeId.RingPing:
                pixels = ShapeRasterizer.RasterizeRingPing(128, tint, 26f, 3f, true, 12f, 0.15f);
                size = 128;
                break;
            case ShapeId.OrganicMetaball:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.Metaball, 1337, 4, 18f, 8f);
                size = 96;
                break;
            case ShapeId.OrganicAmoeba:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.Amoeba, 7331, 5, 17f, 10f);
                size = 96;
                break;
            case ShapeId.StrokeScribble:
                pixels = ShapeRasterizer.RasterizeStroke(96, tint, 90210, 14, 2f, 4.5f);
                size = 96;
                break;
            default:
                return null;
        }

        var texture = ShapeSpriteFactory.CreateTexture(size, pixels);
        return ShapeSpriteFactory.CreateSprite(texture, ppu);
    }
}
