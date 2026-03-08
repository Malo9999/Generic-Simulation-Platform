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
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.Metaball, 1337, 4, 18f, 8f, 18, 4f, 2.5f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f);
                size = 96;
                break;
            case ShapeId.OrganicAmoeba:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaNoise, 999, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f);
                size = 96;
                break;
            case ShapeId.OrganicAmoebaCrawler:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaPseudopod, 6121, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f, 15f, 3, 4, 16f, 28f, 2.4f, 5.4f, 0.86f, 0.34f, 0.15f, 0.42f, 0.3f, 0.55f, 1, false);
                size = 96;
                break;
            case ShapeId.OrganicAmoebaStar:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaPseudopod, 6203, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f, 14f, 5, 7, 11f, 20f, 2.2f, 5.8f, 0.78f, 0.26f, 0.22f, 0.45f, 0.2f, 0.5f, 1, false);
                size = 96;
                break;
            case ShapeId.OrganicAmoebaBranch:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaPseudopod, 6331, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f, 14.5f, 3, 5, 13f, 22f, 2.3f, 5.3f, 0.82f, 0.31f, 0.52f, 0.52f, 0.28f, 0.55f, 1, false);
                size = 96;
                break;
            case ShapeId.OrganicAmoebaWideArms:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaPseudopod, 6419, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f, 17f, 4, 6, 9f, 18f, 2.8f, 6.4f, 0.7f, 0.2f, 0.2f, 0.4f, 0.22f, 0.45f, 1, false);
                size = 96;
                break;
            case ShapeId.OrganicAmoebaHunter:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaPseudopod, 6581, 4, 18f, 8f, 18, 5f, 3f, 3, 2f, 0.5f, 0, 0.35f, true, 6, 1f, 0.78f, 13.5f, 3, 4, 15f, 30f, 2f, 4.8f, 0.9f, 0.38f, 0.12f, 0.4f, 0.34f, 0.5f, 1, false);
                size = 96;
                break;
            case ShapeId.StrokeScribble:
                pixels = ShapeRasterizer.RasterizeStroke(96, tint, 90210, 14, 2f, 4.5f);
                size = 96;
                break;
            case ShapeId.TriangleAgent:
                pixels = ShapeRasterizer.RasterizeTriangleAgent(64, tint, 20f, 18f, 6f, 0);
                size = 64;
                break;
            case ShapeId.DiamondAgent:
                pixels = ShapeRasterizer.RasterizeDiamondAgent(64, tint, 18f, 0);
                size = 64;
                break;
            case ShapeId.LineSegment:
                pixels = ShapeRasterizer.RasterizeLineSegment(64, tint, 44f, 2f, true);
                size = 64;
                break;
            case ShapeId.NoiseBlob:
                pixels = ShapeRasterizer.RasterizeOrganic(96, tint, OrganicBlobMode.AmoebaNoise, 2024, 4, 18f, 0f, 22, 2f, 1.2f, 2, 2f, 0.5f, 0, 0.2f, true, 6, 1f, 0.78f);
                size = 96;
                break;
            case ShapeId.PulseRing:
                pixels = ShapeRasterizer.RasterizeRingPing(128, tint, 28f, 2f, true, 10f, 0.18f);
                size = 128;
                break;
            default:
                return null;
        }

        var texture = ShapeSpriteFactory.CreateTexture(size, pixels);
        return ShapeSpriteFactory.CreateSprite(texture, ppu);
    }
}
