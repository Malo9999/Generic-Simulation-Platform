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
        int vfSeed = 4703,
        float vfBodyRadiusPx = 16f,
        int vfTipCountMin = 4,
        int vfTipCountMax = 6,
        int vfStepCountMin = 12,
        int vfStepCountMax = 26,
        float vfStepLengthPx = 1.5f,
        float headingPersistence = 0.78f,
        float outwardBias = 0.85f,
        float noiseTurnStrength = 0.5f,
        float branchChance = 0.2f,
        float branchStepFraction = 0.52f,
        float thicknessStartPx = 5.4f,
        float thicknessEndPx = 1.6f,
        float coreBlendBoost = 2f,
        float edgeJitterPx = 0.45f,
        bool fillInteriorHoles = true,
        bool keepLargestComponent = true,
        bool discardSmallIslands = true)
    {
        var pixels = NewPixels(size);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        if (mode == OrganicBlobMode.AmoebaNoise)
        {
            RasterizeAmoebaNoise(pixels, size, tint, center, seed, baseRadiusPx, noiseAmplitudePx, noiseFrequency, noiseOctaves, noiseLacunarity, noiseGain, rimSoftnessPx, symmetryBreak, useRimGradient, rimWidthPx, innerMul, outerMul);
            return pixels;
        }

        if (mode == OrganicBlobMode.AmoebaSolidGrowth)
        {
            RasterizeAmoebaSolidGrowth(pixels, size, tint, center, seed, useRimGradient, rimWidthPx, innerMul, outerMul, vfSeed, vfBodyRadiusPx, vfTipCountMin, vfTipCountMax, vfStepCountMin, vfStepCountMax, vfStepLengthPx, headingPersistence, outwardBias, noiseTurnStrength, branchChance, branchStepFraction, thicknessStartPx, thicknessEndPx, coreBlendBoost, edgeJitterPx, fillInteriorHoles, keepLargestComponent, discardSmallIslands);
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

    private sealed class GrowthTip
    {
        public Vector2 position;
        public Vector2 direction;
        public int remainingSteps;
        public int initialSteps;
        public float thicknessStart;
        public float thicknessEnd;
        public bool canBranch = true;
    }

    private static void RasterizeAmoebaSolidGrowth(Color32[] pixels, int size, Color tint, Vector2 center, int fallbackSeed, bool useRimGradient, int rimWidthPx, float innerMul, float outerMul, int vfSeed, float vfBodyRadiusPx, int vfTipCountMin, int vfTipCountMax, int vfStepCountMin, int vfStepCountMax, float vfStepLengthPx, float headingPersistence, float outwardBias, float noiseTurnStrength, float branchChance, float branchStepFraction, float thicknessStartPx, float thicknessEndPx, float coreBlendBoost, float edgeJitterPx, bool fillInteriorHoles, bool keepLargestComponent, bool discardSmallIslands)
    {
        var growthSeed = vfSeed != 0 ? vfSeed : fallbackSeed;
        var rng = new System.Random(growthSeed);
        var deposit = new float[size * size];

        var bodyRadius = Mathf.Max(2f, vfBodyRadiusPx);
        StampFloatCircle(deposit, size, center, bodyRadius + coreBlendBoost, 1f);
        var auxBodies = 1 + (rng.NextDouble() < 0.55 ? 1 : 0);
        for (var i = 0; i < auxBodies; i++)
        {
            var a = (float)rng.NextDouble() * Mathf.PI * 2f;
            var r = bodyRadius * Mathf.Lerp(0.1f, 0.35f, (float)rng.NextDouble());
            var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            StampFloatCircle(deposit, size, center + offset, bodyRadius * Mathf.Lerp(0.62f, 0.95f, (float)rng.NextDouble()) + (coreBlendBoost * 0.6f), 0.95f);
        }

        var tipCount = rng.Next(Mathf.Max(1, vfTipCountMin), Mathf.Max(vfTipCountMin, vfTipCountMax) + 1);
        var tips = new System.Collections.Generic.List<GrowthTip>(tipCount * 3);
        var dominantTip = rng.Next(0, tipCount);
        var baseAngle = (float)rng.NextDouble() * Mathf.PI * 2f;

        for (var i = 0; i < tipCount; i++)
        {
            var spread = (Mathf.PI * 2f) / tipCount;
            var tipAngle = baseAngle + (i * spread) + Mathf.Lerp(-0.35f, 0.35f, (float)rng.NextDouble());
            var dir = new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle)).normalized;
            var steps = rng.Next(Mathf.Max(3, vfStepCountMin), Mathf.Max(vfStepCountMin, vfStepCountMax) + 1);
            if (i == dominantTip)
            {
                steps = Mathf.RoundToInt(steps * Mathf.Lerp(1.15f, 1.45f, (float)rng.NextDouble()));
            }

            tips.Add(new GrowthTip
            {
                position = center + (dir * bodyRadius * Mathf.Lerp(0.35f, 0.75f, (float)rng.NextDouble())),
                direction = dir,
                remainingSteps = steps,
                initialSteps = steps,
                thicknessStart = thicknessStartPx * Mathf.Lerp(0.88f, 1.12f, (float)rng.NextDouble()),
                thicknessEnd = thicknessEndPx * Mathf.Lerp(0.88f, 1.12f, (float)rng.NextDouble())
            });
        }

        var maxTipInstances = 64;
        var branchAngleRad = 30f * Mathf.Deg2Rad;
        for (var i = 0; i < tips.Count; i++)
        {
            var tip = tips[i];
            while (tip.remainingSteps > 0)
            {
                var progress = 1f - (tip.remainingSteps / Mathf.Max(1f, tip.initialSteps));
                var outward = (tip.position - center).normalized;
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = tip.direction;
                }

                var noiseAngle = SampleFieldAngle(tip.position, growthSeed);
                var noiseDir = new Vector2(Mathf.Cos(noiseAngle), Mathf.Sin(noiseAngle));
                var heading = (tip.direction * headingPersistence) + (outward * outwardBias) + (noiseDir * noiseTurnStrength);
                if (heading.sqrMagnitude < 0.001f)
                {
                    heading = tip.direction;
                }

                tip.direction = heading.normalized;
                tip.position += tip.direction * vfStepLengthPx;

                tip.position.x = Mathf.Clamp(tip.position.x, 1f, size - 2f);
                tip.position.y = Mathf.Clamp(tip.position.y, 1f, size - 2f);

                var thickness = Mathf.Lerp(tip.thicknessStart, tip.thicknessEnd, progress);
                thickness = Mathf.Max(0.8f, thickness);
                StampFloatCircle(deposit, size, tip.position, thickness, 1f);

                if (tip.canBranch && tips.Count < maxTipInstances && tip.remainingSteps > 6 && progress > 0.2f && (float)rng.NextDouble() < branchChance)
                {
                    var sign = rng.Next(0, 2) == 0 ? -1f : 1f;
                    var angle = branchAngleRad * Mathf.Lerp(0.65f, 1.25f, (float)rng.NextDouble()) * sign;
                    var branchDir = Rotate(tip.direction, angle).normalized;
                    var branchSteps = Mathf.Max(4, Mathf.RoundToInt(tip.remainingSteps * branchStepFraction * Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble())));
                    tips.Add(new GrowthTip
                    {
                        position = tip.position,
                        direction = branchDir,
                        remainingSteps = branchSteps,
                        initialSteps = branchSteps,
                        thicknessStart = Mathf.Max(1f, thickness * Mathf.Lerp(0.7f, 0.9f, (float)rng.NextDouble())),
                        thicknessEnd = Mathf.Max(0.8f, tip.thicknessEnd * Mathf.Lerp(0.7f, 0.95f, (float)rng.NextDouble())),
                        canBranch = false
                    });

                    tip.canBranch = false;
                }

                tip.remainingSteps--;
            }
        }

        var mask = new bool[deposit.Length];
        for (var idx = 0; idx < deposit.Length; idx++)
        {
            mask[idx] = deposit[idx] > 0.05f;
        }

        if (fillInteriorHoles)
        {
            FillInteriorHoles(mask, size);
        }

        if (keepLargestComponent)
        {
            KeepLargestConnectedComponent(mask, size);
        }

        if (discardSmallIslands)
        {
            RemoveSmallIslands(mask, size, Mathf.Max(8, size / 48));
        }

        if (edgeJitterPx > 0.01f)
        {
            ApplyEdgeJitter(mask, size, growthSeed, edgeJitterPx);
            if (fillInteriorHoles)
            {
                FillInteriorHoles(mask, size);
            }

            if (keepLargestComponent)
            {
                KeepLargestConnectedComponent(mask, size);
            }

            if (discardSmallIslands)
            {
                RemoveSmallIslands(mask, size, Mathf.Max(8, size / 48));
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

    private static float SampleFieldAngle(Vector2 point, int seed)
    {
        var nx = (point.x * 0.075f) + (seed * 0.0131f);
        var ny = (point.y * 0.075f) + (seed * 0.0173f);
        var n = Fbm01(nx, ny, seed + 137, 3, 2f, 0.5f);
        return n * Mathf.PI * 2f;
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var s = Mathf.Sin(angle);
        var c = Mathf.Cos(angle);
        return new Vector2((v.x * c) - (v.y * s), (v.x * s) + (v.y * c));
    }

    private static void StampFloatCircle(float[] mask, int size, Vector2 center, float radius, float add)
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
            if ((dx * dx) + (dy * dy) > r2)
            {
                continue;
            }

            mask[(y * size) + x] += add;
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

    private static void KeepLargestConnectedComponent(bool[] mask, int size)
    {
        var visited = new bool[mask.Length];
        var queue = new System.Collections.Generic.Queue<int>();
        var largestStart = -1;
        var largestCount = 0;

        for (var idx = 0; idx < mask.Length; idx++)
        {
            if (!mask[idx] || visited[idx])
            {
                continue;
            }

            visited[idx] = true;
            queue.Clear();
            queue.Enqueue(idx);
            var count = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                count++;
                var x = current % size;
                var y = current / size;

                TryVisitFilled(mask, visited, queue, size, x - 1, y);
                TryVisitFilled(mask, visited, queue, size, x + 1, y);
                TryVisitFilled(mask, visited, queue, size, x, y - 1);
                TryVisitFilled(mask, visited, queue, size, x, y + 1);
            }

            if (count > largestCount)
            {
                largestCount = count;
                largestStart = idx;
            }
        }

        if (largestStart < 0)
        {
            return;
        }

        var keep = new bool[mask.Length];
        queue.Clear();
        queue.Enqueue(largestStart);
        keep[largestStart] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var x = current % size;
            var y = current / size;

            TryKeep(mask, keep, queue, size, x - 1, y);
            TryKeep(mask, keep, queue, size, x + 1, y);
            TryKeep(mask, keep, queue, size, x, y - 1);
            TryKeep(mask, keep, queue, size, x, y + 1);
        }

        for (var i = 0; i < mask.Length; i++)
        {
            mask[i] = keep[i];
        }
    }


    private static void RemoveSmallIslands(bool[] mask, int size, int minPixels)
    {
        if (minPixels <= 1)
        {
            return;
        }

        var visited = new bool[mask.Length];
        var queue = new System.Collections.Generic.Queue<int>();
        var component = new System.Collections.Generic.List<int>();

        for (var idx = 0; idx < mask.Length; idx++)
        {
            if (!mask[idx] || visited[idx])
            {
                continue;
            }

            visited[idx] = true;
            queue.Clear();
            component.Clear();
            queue.Enqueue(idx);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                var x = current % size;
                var y = current / size;

                TryVisitFilled(mask, visited, queue, size, x - 1, y);
                TryVisitFilled(mask, visited, queue, size, x + 1, y);
                TryVisitFilled(mask, visited, queue, size, x, y - 1);
                TryVisitFilled(mask, visited, queue, size, x, y + 1);
            }

            if (component.Count >= minPixels)
            {
                continue;
            }

            for (var i = 0; i < component.Count; i++)
            {
                mask[component[i]] = false;
            }
        }
    }

    private static System.Collections.Generic.List<int> ExtractOuterContour(bool[] mask, int size)
    {
        var contour = new System.Collections.Generic.List<int>();
        for (var y = 1; y < size - 1; y++)
        for (var x = 1; x < size - 1; x++)
        {
            var idx = (y * size) + x;
            if (!mask[idx])
            {
                continue;
            }

            if (!mask[idx - 1] || !mask[idx + 1] || !mask[idx - size] || !mask[idx + size])
            {
                contour.Add(idx);
            }
        }

        return contour;
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

    private static void TryVisitFilled(bool[] mask, bool[] visited, System.Collections.Generic.Queue<int> queue, int size, int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return;
        }

        var idx = (y * size) + x;
        if (!mask[idx] || visited[idx])
        {
            return;
        }

        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static void TryKeep(bool[] mask, bool[] keep, System.Collections.Generic.Queue<int> queue, int size, int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return;
        }

        var idx = (y * size) + x;
        if (!mask[idx] || keep[idx])
        {
            return;
        }

        keep[idx] = true;
        queue.Enqueue(idx);
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
        var contour = ExtractOuterContour(source, size);
        for (var i = 0; i < contour.Count; i++)
        {
            var idx = contour[i];
            var x = idx % size;
            var y = idx / size;
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
