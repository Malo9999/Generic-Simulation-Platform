using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int MinAcceptQualityScore = 70;

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int seed, int variant, int fixedTemplateId, out bool fallbackUsed)
    {
        fallbackUsed = false;
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var sourceSeed = rng != null ? rng.Seed : seed;
        var mixedSeed = StableMix(sourceSeed, variant, fixedTemplateId, 0x71A3);
        var trackRng = new SeededRng(mixedSeed);
        var tileGen = new TileTrackGenerator();
        var center = tileGen.BuildBestLoop(safeHalfW, safeHalfH, trackRng, variant);

        MarbleRaceTrack bestTrack = null;
        var bestScore = int.MinValue;
        if (center != null && center.Length >= 120)
        {
            var candidate = BuildTrackData(center, minDim);
            if (ValidateStrict(candidate.Center, candidate.Tangent, candidate.Normal, candidate.HalfWidth, safeHalfW, safeHalfH))
            {
                RotateToBestStraight(candidate);
                var quality = MarbleRaceTrackValidator.EvaluateQuality(candidate);
                bestScore = quality.Score;
                bestTrack = candidate;
                if (quality.Score < MinAcceptQualityScore)
                {
                    Debug.Log($"[TrackGen] tile loop generated with lower quality score={quality.Score}; accepting since constraints pass.");
                }
            }
        }

        if (bestTrack == null)
        {
            fallbackUsed = true;
            bestTrack = BuildFallbackRoundedRectangle(safeHalfW, safeHalfH, variant);
            bestScore = MarbleRaceTrackValidator.EvaluateQuality(bestTrack).Score;
        }

        Debug.Log($"[TrackGen] variant={variant} score={bestScore} fallback={(fallbackUsed ? 1 : 0)}");
        return bestTrack;
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight, int variant)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var fallbackSeed = StableMix(unchecked((int)0x51EDBEEF), variant, 0x1234, 0);
        var rng = new SeededRng(fallbackSeed);

        for (var retry = 0; retry < 6; retry++)
        {
            var tileGen = new TileTrackGenerator();
            var center = tileGen.BuildBestLoop(safeHalfW, safeHalfH, rng, variant + retry);
            if (center == null || center.Length < 120)
            {
                continue;
            }

            var baseHalfWidth = Mathf.Clamp(minDim * 0.035f, 0.9f, 2.2f);
            var requiredMargin = baseHalfWidth * 1.6f + (minDim * 0.09f);
            if (!FitToBounds(center, safeHalfW, safeHalfH, requiredMargin))
            {
                continue;
            }

            var candidate = BuildTrackData(center, minDim);
            RotateToBestStraight(candidate);
            return candidate;
        }

        var circle = new Vector2[320];
        var radius = Mathf.Min(safeHalfW, safeHalfH) * 0.62f;
        for (var i = 0; i < circle.Length; i++)
        {
            var a = i / (float)circle.Length * Mathf.PI * 2f;
            circle[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        var emergency = BuildTrackData(circle, minDim);
        RotateToBestStraight(emergency);
        return emergency;
    }

    private static int StableMix(int a, int b, int c, int d)
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) ^ a;
            h = (h * 31) ^ (b * unchecked((int)0x9E3779B9));
            h = (h * 31) ^ (c * unchecked((int)0x85EBCA6B));
            h = (h * 31) ^ (d * unchecked((int)0xC2B2AE35));
            h ^= h >> 16;
            return h;
        }
    }

    private static bool FitToBounds(Vector2[] points, float halfW, float halfH, float requiredMargin)
    {
        var min = points[0];
        var max = points[0];
        for (var i = 1; i < points.Length; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        var center = (min + max) * 0.5f;
        var ext = (max - min) * 0.5f;
        var usableX = halfW - requiredMargin;
        var usableY = halfH - requiredMargin;
        if (usableX <= 0.5f || usableY <= 0.5f)
        {
            return false;
        }

        var sx = usableX / Mathf.Max(0.001f, ext.x);
        var sy = usableY / Mathf.Max(0.001f, ext.y);
        var scale = Mathf.Min(sx, sy);
        if (scale < 0.45f)
        {
            return false;
        }

        for (var i = 0; i < points.Length; i++)
        {
            points[i] = (points[i] - center) * scale;
        }

        for (var i = 0; i < points.Length; i++)
        {
            if (Mathf.Abs(points[i].x) > usableX || Mathf.Abs(points[i].y) > usableY)
            {
                return false;
            }
        }

        return true;
    }

    private static MarbleRaceTrack BuildTrackData(Vector2[] center, float minDim)
    {
        var n = center.Length;
        var tangent = new Vector2[n];
        var normal = new Vector2[n];
        var curvature = new float[n];

        for (var i = 0; i < n; i++)
        {
            var prev = center[(i - 1 + n) % n];
            var next = center[(i + 1) % n];
            var t = (next - prev).normalized;
            if (i > 0 && Vector2.Dot(tangent[i - 1], t) < 0f)
            {
                t = -t;
            }

            tangent[i] = t;
            normal[i] = new Vector2(-t.y, t.x);
        }

        for (var i = 0; i < n; i++)
        {
            var prev = tangent[(i - 1 + n) % n];
            curvature[i] = Vector2.Angle(prev, tangent[i]) / 180f;
        }

        var baseHalfWidth = Mathf.Clamp(minDim * 0.035f, 0.9f, 2.2f);
        var halfWidth = BuildWidths(curvature, baseHalfWidth);
        var layer = new sbyte[n];
        return new MarbleRaceTrack(center, tangent, normal, halfWidth, curvature, layer);
    }

    private static float[] BuildWidths(float[] curvature, float baseHalfWidth)
    {
        var n = curvature.Length;
        var widths = new float[n];
        var minW = baseHalfWidth * 0.85f;
        var maxW = baseHalfWidth * 1.45f;

        for (var i = 0; i < n; i++)
        {
            var c = Mathf.Clamp01(curvature[i] * 6f);
            var widenOnStraights = Mathf.Clamp01((0.05f - curvature[i]) / 0.05f) * 0.22f;
            var narrowOnCorners = c * 0.24f;
            var w = baseHalfWidth * (1f + widenOnStraights - narrowOnCorners);
            widths[i] = Mathf.Clamp(w, minW, maxW);
        }

        return widths;
    }

    private static bool ValidateStrict(Vector2[] center, Vector2[] tangent, Vector2[] normal, float[] halfWidth, float halfW, float halfH)
    {
        if (center == null || tangent == null || normal == null || halfWidth == null)
        {
            return false;
        }

        var n = center.Length;
        if (n < 120 || tangent.Length != n || normal.Length != n || halfWidth.Length != n)
        {
            return false;
        }

        var minDim = Mathf.Min(halfW, halfH) * 2f;
        var maxHalfWidth = 0f;
        for (var i = 0; i < n; i++)
        {
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
        }

        var requiredMargin = maxHalfWidth + (minDim * 0.08f);
        for (var i = 0; i < n; i++)
        {
            if (Mathf.Abs(center[i].x) > halfW - requiredMargin || Mathf.Abs(center[i].y) > halfH - requiredMargin)
            {
                return false;
            }
        }

        var minStep = Mathf.Max(0.45f, minDim * 0.008f);
        var maxStep = Mathf.Min(1.5f, minDim * 0.04f);
        for (var i = 0; i < n; i++)
        {
            var prevI = (i - 1 + n) % n;
            if (Vector2.Dot(tangent[i], tangent[prevI]) < 0.22f)
            {
                return false;
            }

            var d = Vector2.Distance(center[i], center[prevI]);
            if (d < minStep || d > maxStep)
            {
                return false;
            }
        }

        if (HasSelfIntersections(center, 4, 10))
        {
            return false;
        }

        var left = new Vector2[n];
        var right = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            left[i] = center[i] + (normal[i] * halfWidth[i]);
            right[i] = center[i] - (normal[i] * halfWidth[i]);
        }

        if (HasSelfIntersections(left, 4, 10) || HasSelfIntersections(right, 4, 10))
        {
            return false;
        }

        return true;
    }

    private static bool HasSelfIntersections(Vector2[] points, int stride, int neighborIgnore)
    {
        var n = points.Length;
        for (var i = 0; i < n; i += stride)
        {
            var a1 = points[i];
            var a2 = points[(i + stride) % n];

            for (var j = i + stride; j < n; j += stride)
            {
                if (AreNeighborSegments(i, j, n, neighborIgnore, stride))
                {
                    continue;
                }

                var b1 = points[j];
                var b2 = points[(j + stride) % n];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreNeighborSegments(int i, int j, int n, int neighborIgnore, int stride)
    {
        var delta = Mathf.Abs(i - j);
        var wrapDelta = Mathf.Min(delta, n - delta);
        return wrapDelta <= (neighborIgnore * stride);
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        var numerT = Cross(q1 - p1, s);
        var numerU = Cross(q1 - p1, r);

        if (Mathf.Abs(denom) < 1e-6f)
        {
            return false;
        }

        var t = numerT / denom;
        var u = numerU / denom;
        return t > 0f && t < 1f && u > 0f && u < 1f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static void RotateToBestStraight(MarbleRaceTrack track)
    {
        var n = track.SampleCount;
        if (n <= 0)
        {
            return;
        }

        var window = Mathf.Clamp(n / 28, 6, 24);
        var bestStart = 0;
        var bestScore = float.MaxValue;

        for (var i = 0; i < n; i++)
        {
            var sum = 0f;
            var widthScore = 0f;
            for (var j = -window; j <= window; j++)
            {
                var idx = (i + j + n) % n;
                sum += track.Curvature[idx];
                widthScore += track.HalfWidth[idx];
            }

            var score = sum - (widthScore * 0.02f);
            if (score < bestScore)
            {
                bestScore = score;
                bestStart = i;
            }
        }

        if (bestStart == 0)
        {
            return;
        }

        Rotate(track.Center, bestStart);
        Rotate(track.Tangent, bestStart);
        Rotate(track.Normal, bestStart);
        Rotate(track.HalfWidth, bestStart);
        Rotate(track.Curvature, bestStart);
        Rotate(track.Layer, bestStart);
    }

    private static void Rotate<T>(T[] data, int start)
    {
        var n = data.Length;
        var copy = new T[n];
        for (var i = 0; i < n; i++)
        {
            copy[i] = data[(i + start) % n];
        }

        for (var i = 0; i < n; i++)
        {
            data[i] = copy[i];
        }
    }
}
