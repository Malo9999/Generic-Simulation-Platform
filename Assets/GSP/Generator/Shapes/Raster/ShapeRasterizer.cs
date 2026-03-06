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

    public static Color32[] RasterizeTriangleAgent(int size, Color tint, float tipRadiusPx, float baseWidthPx, float baseOffsetPx, int outlinePx)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var tip = new Vector2(center.x, center.y - tipRadiusPx);
        var left = new Vector2(center.x - baseWidthPx, center.y + baseOffsetPx);
        var right = new Vector2(center.x + baseWidthPx, center.y + baseOffsetPx);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            if (PointInTriangle(new Vector2(x, y), tip, left, right))
            {
                pixels[(y * size) + x] = tint;
            }
        }

        if (outlinePx > 0)
        {
            DrawSegment(pixels, size, tip, left, outlinePx, tint);
            DrawSegment(pixels, size, left, right, outlinePx, tint);
            DrawSegment(pixels, size, right, tip, outlinePx, tint);
        }

        return pixels;
    }

    public static Color32[] RasterizeDiamondAgent(int size, Color tint, float diamondRadiusPx, int outlinePx)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = Mathf.Abs(x - center.x);
            var dy = Mathf.Abs(y - center.y);
            var manhattan = dx + dy;
            if (manhattan <= diamondRadiusPx)
            {
                if (outlinePx <= 0 || manhattan >= diamondRadiusPx - outlinePx)
                {
                    pixels[(y * size) + x] = tint;
                }
                else
                {
                    pixels[(y * size) + x] = tint;
                }
            }
        }

        return pixels;
    }

    public static Color32[] RasterizeLineSegment(int size, Color tint, float lengthPx, float thicknessPx, bool roundedCaps)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var halfLength = Mathf.Max(1f, lengthPx * 0.5f);
        var halfThickness = Mathf.Max(1f, thicknessPx * 0.5f);
        var a = new Vector2(center.x - halfLength, center.y);
        var b = new Vector2(center.x + halfLength, center.y);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var p = new Vector2(x, y);
            bool filled;
            if (roundedCaps)
            {
                filled = DistanceToSegment(p, a, b) <= halfThickness;
            }
            else
            {
                filled = x >= a.x && x <= b.x && Mathf.Abs(y - center.y) <= halfThickness;
            }

            if (filled)
            {
                pixels[(y * size) + x] = tint;
            }
        }

        return pixels;
    }


    public static Color32[] RasterizeFilament(
        int size,
        Color tint,
        float lengthPx,
        float thicknessStartPx,
        float thicknessEndPx,
        float bend,
        float waveAmpPx,
        float waveFreq,
        int seed,
        bool useRimGradient,
        int rimWidthPx,
        float innerMul,
        float outerMul)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var halfLength = Mathf.Clamp(lengthPx * 0.5f, 1f, (size - 2) * 0.5f);
        var margin = Mathf.Max(1f, center.x - halfLength);
        var p0 = new Vector2(margin, center.y);
        var p2 = new Vector2(size - 1 - margin, center.y);
        var p1 = new Vector2(center.x, center.y + (bend * (size * 0.25f)));
        var sampleSteps = 64;
        var tau = Mathf.PI * 2f;
        var seedOffset = RasterNoiseUtil.Hash01(seed, 113, 977);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var pixel = new Vector2(x, y);
            var nearestT = 0f;
            var nearestDistSq = float.MaxValue;

            for (var i = 0; i <= sampleSteps; i++)
            {
                var t = i / (float)sampleSteps;
                var point = QuadraticBezier(p0, p1, p2, t);
                var distSq = (pixel - point).sqrMagnitude;
                if (distSq >= nearestDistSq)
                {
                    continue;
                }

                nearestDistSq = distSq;
                nearestT = t;
            }

            var thick = Mathf.Lerp(thicknessStartPx, thicknessEndPx, nearestT);
            if (waveAmpPx > 0f && waveFreq > 0f)
            {
                thick += Mathf.Sin(((nearestT * waveFreq) + seedOffset) * tau) * waveAmpPx;
            }

            var halfThickness = Mathf.Max(0.5f, thick * 0.5f);
            var dist = Mathf.Sqrt(nearestDistSq);
            if (dist > halfThickness)
            {
                continue;
            }

            var brightness = innerMul;
            if (useRimGradient)
            {
                var distToEdgePx = halfThickness - dist;
                brightness = ApplyRimGradient(distToEdgePx, rimWidthPx, innerMul, outerMul);
            }

            pixels[(y * size) + x] = MultiplyColor(tint, brightness, 1f);
        }

        return pixels;
    }

    public static Color32[] RasterizeOrganic(
        int size,
        Color tint,
        OrganicBlobMode mode,
        int seed,
        int lobeCount,
        float radius,
        float jitter,
        int baseRadiusPx,
        float noiseAmplitudePx,
        float noiseFrequency,
        int noiseOctaves,
        float noiseLacunarity,
        float noiseGain,
        int rimSoftnessPx,
        float symmetryBreak,
        bool useRimGradient,
        int rimWidthPx,
        float innerMul,
        float outerMul)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        if (mode == OrganicBlobMode.AmoebaNoise)
        {
            RasterizeAmoebaNoise(pixels, size, tint, center, seed, baseRadiusPx, noiseAmplitudePx, noiseFrequency, noiseOctaves, noiseLacunarity, noiseGain, rimSoftnessPx, symmetryBreak, useRimGradient, rimWidthPx, innerMul, outerMul);
            return pixels;
        }

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
            const float threshold = 2.1f;

            if (f < threshold)
            {
                continue;
            }

            var distToEdgePx = float.MaxValue;
            for (var i = 0; i < lobeCount; i++)
            {
                var d = radii[i] - Vector2.Distance(new Vector2(x, y), centers[i]);
                if (d < distToEdgePx)
                {
                    distToEdgePx = d;
                }
            }

            var brightness = useRimGradient ? ApplyRimGradient(distToEdgePx, rimWidthPx, innerMul, outerMul) : innerMul;
            pixels[(y * size) + x] = MultiplyColor(tint, brightness, tint.a);
        }

        return pixels;
    }

    private static void RasterizeAmoebaNoise(Color32[] pixels, int size, Color tint, Vector2 center, int seed, int baseRadiusPx, float noiseAmplitudePx, float noiseFrequency, int noiseOctaves, float noiseLacunarity, float noiseGain, int rimSoftnessPx, float symmetryBreak, bool useRimGradient, int rimWidthPx, float innerMul, float outerMul)
    {
        var seedY = seed * 0.071f;
        var secondSeedY = (seed + 7919) * 0.113f;

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = x - center.x;
            var dy = y - center.y;
            var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
            var angle = Mathf.Atan2(dy, dx);
            if (angle < 0f)
            {
                angle += Mathf.PI * 2f;
            }

            var primary = Fbm01(angle * noiseFrequency, seedY, seed, noiseOctaves, noiseLacunarity, noiseGain);
            var secondary = Fbm01((angle + 1.234f) * (noiseFrequency * 1.37f), secondSeedY, seed + 17, noiseOctaves, noiseLacunarity, noiseGain);
            var n = Mathf.Lerp(primary, secondary, symmetryBreak);
            var deformedRadius = baseRadiusPx + ((n * 2f - 1f) * noiseAmplitudePx);
            var sdf = deformedRadius - dist;

            if (sdf >= 0f)
            {
                var brightness = useRimGradient ? ApplyRimGradient(Mathf.Abs(sdf), rimWidthPx, innerMul, outerMul) : innerMul;
                pixels[(y * size) + x] = MultiplyColor(tint, brightness, tint.a);
                continue;
            }

            if (rimSoftnessPx <= 0 || sdf <= -rimSoftnessPx)
            {
                continue;
            }

            var a = Mathf.Clamp01(1f + (sdf / rimSoftnessPx));
            if (a <= 0f)
            {
                continue;
            }

            var c = tint;
            c.a *= a;
            pixels[(y * size) + x] = c;
        }
    }

    private static float ApplyRimGradient(float distanceToEdgePx, int rimWidthPx, float innerMul, float outerMul)
    {
        if (rimWidthPx <= 0)
        {
            return innerMul;
        }

        if (distanceToEdgePx < rimWidthPx)
        {
            var t = Mathf.Clamp01(distanceToEdgePx / rimWidthPx);
            return Mathf.Lerp(outerMul, innerMul, t);
        }

        return innerMul;
    }

    private static Color32 MultiplyColor(Color tint, float brightness, float alpha)
    {
        var c = tint;
        var mul = Mathf.Max(0f, brightness);
        c.r *= mul;
        c.g *= mul;
        c.b *= mul;
        c.a = Mathf.Clamp01(alpha);
        return c;
    }

    private static float Fbm01(float x, float y, int seed, int octaves, float lacunarity, float gain)
    {
        var amplitude = 1f;
        var frequency = 1f;
        var weight = 0f;
        var value = 0f;

        for (var i = 0; i < octaves; i++)
        {
            value += RasterNoiseUtil.ValueNoise01(x * frequency, y * frequency, seed + (i * 101)) * amplitude;
            weight += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        if (weight <= 0f)
        {
            return 0.5f;
        }

        return value / weight;
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


    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        var u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
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

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return ((p1.x - p3.x) * (p2.y - p3.y)) - ((p2.x - p3.x) * (p1.y - p3.y));
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
