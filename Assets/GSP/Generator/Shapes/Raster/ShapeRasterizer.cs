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
        float outerMul,
        float coreRadiusPx = 17f,
        int armCountMin = 3,
        int armCountMax = 6,
        float armLengthMinPx = 10f,
        float armLengthMaxPx = 24f,
        float armWidthMinPx = 2.5f,
        float armWidthMaxPx = 7f,
        float armTaper = 0.8f,
        float armCurvature = 0.3f,
        float branchChance = 0.2f,
        float branchLengthMul = 0.45f,
        float bodyIrregularity = 0.25f,
        float edgeJitterPx = 0.6f,
        int fillMarginPx = 1,
        bool allowInteriorHoles = false)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        if (mode == OrganicBlobMode.AmoebaNoise)
        {
            RasterizeAmoebaNoise(pixels, size, tint, center, seed, baseRadiusPx, noiseAmplitudePx, noiseFrequency, noiseOctaves, noiseLacunarity, noiseGain, rimSoftnessPx, symmetryBreak, useRimGradient, rimWidthPx, innerMul, outerMul);
            return pixels;
        }

        if (mode == OrganicBlobMode.AmoebaPseudopod)
        {
            RasterizeAmoebaPseudopod(pixels, size, tint, center, seed, useRimGradient, rimWidthPx, innerMul, outerMul, coreRadiusPx, armCountMin, armCountMax, armLengthMinPx, armLengthMaxPx, armWidthMinPx, armWidthMaxPx, armTaper, armCurvature, branchChance, branchLengthMul, bodyIrregularity, edgeJitterPx, fillMarginPx, allowInteriorHoles);
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

    private static void RasterizeAmoebaPseudopod(Color32[] pixels, int size, Color tint, Vector2 center, int seed, bool useRimGradient, int rimWidthPx, float innerMul, float outerMul, float coreRadiusPx, int armCountMin, int armCountMax, float armLengthMinPx, float armLengthMaxPx, float armWidthMinPx, float armWidthMaxPx, float armTaper, float armCurvature, float branchChance, float branchLengthMul, float bodyIrregularity, float edgeJitterPx, int fillMarginPx, bool allowInteriorHoles)
    {
        var mask = new bool[size * size];
        var rng = new System.Random(seed);
        var bodyCount = 1 + (rng.NextDouble() < (0.35 + (bodyIrregularity * 0.35)) ? 1 : 0);

        for (var i = 0; i < bodyCount; i++)
        {
            var jitter = bodyIrregularity * coreRadiusPx * 0.45f;
            var bodyCenter = center + new Vector2(
                Mathf.Lerp(-jitter, jitter, (float)rng.NextDouble()),
                Mathf.Lerp(-jitter, jitter, (float)rng.NextDouble()));
            var bodyRadius = coreRadiusPx * Mathf.Lerp(0.8f, 1.18f, (float)rng.NextDouble());
            StampCircle(mask, size, bodyCenter, bodyRadius);
        }

        var minArms = Mathf.Max(1, armCountMin);
        var maxArms = Mathf.Max(minArms, armCountMax);
        var armCount = rng.Next(minArms, maxArms + 1);
        var dominantArm = rng.Next(0, armCount);
        var baseAngle = (float)rng.NextDouble() * Mathf.PI * 2f;

        for (var i = 0; i < armCount; i++)
        {
            var angleStep = (Mathf.PI * 2f) / armCount;
            var angle = baseAngle + (i * angleStep) + Mathf.Lerp(-0.3f, 0.3f, (float)rng.NextDouble());
            var armLength = Mathf.Lerp(armLengthMinPx, armLengthMaxPx, (float)rng.NextDouble());
            if (i == dominantArm)
            {
                armLength *= Mathf.Lerp(1.15f, 1.4f, (float)rng.NextDouble());
            }

            var armWidth = Mathf.Lerp(armWidthMinPx, armWidthMaxPx, (float)rng.NextDouble());
            StampTaperedArm(mask, size, center, angle, armLength, armWidth, armTaper, armCurvature, rng);

            if ((float)rng.NextDouble() <= branchChance)
            {
                var branchLen = armLength * Mathf.Clamp(branchLengthMul * Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble()), 0.2f, 0.95f);
                var branchWidth = Mathf.Max(1f, armWidth * Mathf.Lerp(0.5f, 0.75f, (float)rng.NextDouble()));
                var branchOffset = armLength * Mathf.Lerp(0.35f, 0.68f, (float)rng.NextDouble());
                var branchOrigin = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * branchOffset;
                var branchDir = angle + Mathf.Lerp(0.45f, 1.05f, (float)rng.NextDouble()) * (rng.Next(0, 2) == 0 ? -1f : 1f);
                StampTaperedArm(mask, size, branchOrigin, branchDir, branchLen, branchWidth, armTaper, armCurvature * 0.8f, rng);
            }
        }

        if (!allowInteriorHoles)
        {
            FillInteriorHoles(mask, size);
        }

        if (fillMarginPx > 0)
        {
            DilateMask(mask, size, fillMarginPx);
        }

        if (edgeJitterPx > 0.01f)
        {
            ApplyEdgeJitter(mask, size, seed, edgeJitterPx);
            if (!allowInteriorHoles)
            {
                FillInteriorHoles(mask, size);
            }
        }

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var idx = (y * size) + x;
            if (!mask[idx])
            {
                continue;
            }

            var edgeDistance = DistanceToMaskEdge(mask, size, x, y, rimWidthPx + 2);
            var brightness = useRimGradient ? ApplyRimGradient(edgeDistance, rimWidthPx, innerMul, outerMul) : innerMul;
            pixels[idx] = MultiplyColor(tint, brightness, tint.a);
        }
    }

    private static void StampTaperedArm(bool[] mask, int size, Vector2 start, float angle, float lengthPx, float widthPx, float taper, float curvature, System.Random rng)
    {
        var step = Mathf.Max(1f, widthPx * 0.45f);
        var steps = Mathf.Max(3, Mathf.CeilToInt(lengthPx / step));
        var normal = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var curve = Mathf.Sin(t * Mathf.PI) * curvature * lengthPx * Mathf.Lerp(0.2f, 0.85f, (float)rng.NextDouble());
            var point = start + (forward * (t * lengthPx)) + (normal * curve);
            var radius = Mathf.Lerp(widthPx, Mathf.Max(0.8f, widthPx * (1f - taper)), t);
            StampCircle(mask, size, point, radius);
        }
    }

    private static void StampCircle(bool[] mask, int size, Vector2 center, float radius)
    {
        var minX = Mathf.Clamp(Mathf.FloorToInt(center.x - radius), 0, size - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(center.x + radius), 0, size - 1);
        var minY = Mathf.Clamp(Mathf.FloorToInt(center.y - radius), 0, size - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(center.y + radius), 0, size - 1);
        var r2 = radius * radius;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = x - center.x;
            var dy = y - center.y;
            if ((dx * dx) + (dy * dy) <= r2)
            {
                mask[(y * size) + x] = true;
            }
        }
    }

    private static void FillInteriorHoles(bool[] mask, int size)
    {
        var visited = new bool[mask.Length];
        var queue = new System.Collections.Generic.Queue<int>();

        for (var x = 0; x < size; x++)
        {
            EnqueueOutside(mask, visited, queue, size, x, 0);
            EnqueueOutside(mask, visited, queue, size, x, size - 1);
        }

        for (var y = 0; y < size; y++)
        {
            EnqueueOutside(mask, visited, queue, size, 0, y);
            EnqueueOutside(mask, visited, queue, size, size - 1, y);
        }

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            var x = idx % size;
            var y = idx / size;
            TryVisit(mask, visited, queue, size, x - 1, y);
            TryVisit(mask, visited, queue, size, x + 1, y);
            TryVisit(mask, visited, queue, size, x, y - 1);
            TryVisit(mask, visited, queue, size, x, y + 1);
        }

        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i] && !visited[i])
            {
                mask[i] = true;
            }
        }
    }

    private static void EnqueueOutside(bool[] mask, bool[] visited, System.Collections.Generic.Queue<int> queue, int size, int x, int y)
    {
        var idx = (y * size) + x;
        if (mask[idx] || visited[idx])
        {
            return;
        }

        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static void TryVisit(bool[] mask, bool[] visited, System.Collections.Generic.Queue<int> queue, int size, int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return;
        }

        var idx = (y * size) + x;
        if (mask[idx] || visited[idx])
        {
            return;
        }

        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static void DilateMask(bool[] mask, int size, int passes)
    {
        var temp = new bool[mask.Length];
        for (var pass = 0; pass < passes; pass++)
        {
            System.Array.Copy(mask, temp, mask.Length);
            for (var y = 1; y < size - 1; y++)
            for (var x = 1; x < size - 1; x++)
            {
                var idx = (y * size) + x;
                if (temp[idx])
                {
                    continue;
                }

                if (temp[idx - 1] || temp[idx + 1] || temp[idx - size] || temp[idx + size])
                {
                    mask[idx] = true;
                }
            }
        }
    }

    private static void ApplyEdgeJitter(bool[] mask, int size, int seed, float edgeJitterPx)
    {
        var jitterCells = Mathf.Max(0, Mathf.RoundToInt(edgeJitterPx));
        if (jitterCells <= 0)
        {
            return;
        }

        var source = new bool[mask.Length];
        System.Array.Copy(mask, source, mask.Length);
        for (var y = 1; y < size - 1; y++)
        for (var x = 1; x < size - 1; x++)
        {
            var idx = (y * size) + x;
            if (!source[idx])
            {
                continue;
            }

            var isEdge = !source[idx - 1] || !source[idx + 1] || !source[idx - size] || !source[idx + size];
            if (!isEdge)
            {
                continue;
            }

            var n = RasterNoiseUtil.Hash01(seed, x + (y * 31), 179 + seed);
            if (n > 0.78f)
            {
                for (var oy = -jitterCells; oy <= jitterCells; oy++)
                for (var ox = -jitterCells; ox <= jitterCells; ox++)
                {
                    var nx = x + ox;
                    var ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                    {
                        continue;
                    }

                    if ((ox * ox) + (oy * oy) <= jitterCells * jitterCells)
                    {
                        mask[(ny * size) + nx] = true;
                    }
                }
            }
        }
    }

    private static float DistanceToMaskEdge(bool[] mask, int size, int x, int y, int searchRadius)
    {
        if (searchRadius <= 0)
        {
            return 0f;
        }

        var best = (float)searchRadius;
        for (var oy = -searchRadius; oy <= searchRadius; oy++)
        for (var ox = -searchRadius; ox <= searchRadius; ox++)
        {
            var nx = x + ox;
            var ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= size || ny >= size)
            {
                continue;
            }

            if (mask[(ny * size) + nx])
            {
                continue;
            }

            var dist = Mathf.Sqrt((ox * ox) + (oy * oy));
            if (dist < best)
            {
                best = dist;
            }
        }

        return best;
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
