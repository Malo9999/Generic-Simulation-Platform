using UnityEngine;

public static class ShapeRasterizer
{
    public static Color32[] RasterizeDotCore(int size, Color tint, float radius, float outlinePx, float outlineAlpha, float outlineColorMultiplier)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var fill = (Color32)tint;
        var outline = (Color32)(tint * Mathf.Clamp01(outlineColorMultiplier));
        outline.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(outlineAlpha));

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var idx = (y * size) + x;
            var fillA = SdfUtil.HardCircleAlpha(x, y, center, radius);
            if (fillA > 0f)
            {
                pixels[idx] = fill;
                continue;
            }

            var outA = SdfUtil.OutlineAlpha(x, y, center, radius, outlinePx);
            if (outA > 0f)
            {
                pixels[idx] = outline;
            }
        }

        return pixels;
    }

    public static Color32[] RasterizeGlowDot(int size, Color tint, float innerRadius, float outerRadius, float exponent, float alphaMultiplier)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var a = SdfUtil.SoftCircleAlpha(x, y, center, innerRadius, outerRadius, exponent) * alphaMultiplier;
            if (a <= 0f)
            {
                continue;
            }

            var c = tint;
            c.a = Mathf.Clamp01(a);
            pixels[(y * size) + x] = c;
        }

        return pixels;
    }

    public static Color32[] RasterizeRingPing(int size, Color tint, float radius, float thickness, bool includeGlow, float glowWidth, float glowAlpha)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var ring = SdfUtil.RingAlpha(x, y, center, radius, thickness);
            var glow = 0f;
            if (includeGlow)
            {
                glow = SdfUtil.SoftCircleAlpha(x, y, center, radius, radius + glowWidth, 1.4f) * glowAlpha;
            }

            var a = Mathf.Clamp01(Mathf.Max(ring, glow));
            if (a <= 0f)
            {
                continue;
            }

            var c = tint;
            c.a = a;
            pixels[(y * size) + x] = c;
        }

        return pixels;
    }

    public static Color32[] RasterizeOrganic(int size, Color tint, OrganicBlobMode mode, int seed, int lobeCount, float radius, float jitter)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var centers = new Vector2[lobeCount];
        var radii = new float[lobeCount];

        for (var i = 0; i < lobeCount; i++)
        {
            var angle = (Mathf.PI * 2f * i) / lobeCount;
            var rJitter = Mathf.Lerp(-jitter, jitter, RasterNoiseUtil.Hash01(i, seed, seed + 17));
            centers[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rJitter;
            radii[i] = radius * Mathf.Lerp(0.8f, 1.2f, RasterNoiseUtil.Hash01(seed, i, seed + 31));
        }

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var f = MetaballUtil.SampleField(x, y, centers, radii);
            var threshold = mode == OrganicBlobMode.Metaball ? 2.1f : 1.8f;
            if (mode == OrganicBlobMode.Amoeba)
            {
                var wobble = RasterNoiseUtil.ValueNoise01(x * 0.09f, y * 0.09f, seed) - 0.5f;
                threshold += wobble * 0.4f;
            }

            if (f >= threshold)
            {
                pixels[(y * size) + x] = tint;
            }
        }

        return pixels;
    }

    public static Color32[] RasterizeStroke(int size, Color tint, int seed, int steps, float widthPx, float stridePx)
    {
        var pixels = NewPixels(size);
        var p = new Vector2(size * 0.25f, size * 0.5f);
        var angle = 0f;

        for (var i = 0; i < steps; i++)
        {
            var n = RasterNoiseUtil.Hash01(i, seed, 17 + seed);
            angle += Mathf.Lerp(-0.9f, 0.9f, n);
            var next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * stridePx;
            DrawSegment(pixels, size, p, next, widthPx, tint);
            p = next;
        }

        return pixels;
    }

    private static void DrawSegment(Color32[] pixels, int size, Vector2 a, Vector2 b, float width, Color color)
    {
        var minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width), 0, size - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width), 0, size - 1);
        var minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width), 0, size - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width), 0, size - 1);

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var d = DistanceToSegment(new Vector2(x, y), a, b);
            if (d <= width)
            {
                pixels[(y * size) + x] = color;
            }
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ap = p - a;
        var ab = b - a;
        var t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + (ab * t));
    }

    private static Color32[] NewPixels(int size)
    {
        var px = new Color32[size * size];
        var clear = new Color32(0, 0, 0, 0);
        for (var i = 0; i < px.Length; i++)
        {
            px[i] = clear;
        }

        return px;
    }
}
