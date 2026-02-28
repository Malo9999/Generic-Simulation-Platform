using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        return BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant, out _);
    }

    private MarbleRaceTrack BuildInternal(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant, out bool fallback)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;

        var snapped = new SnappedTrackGenerator();
        var center = snapped.BuildCenterline(safeHalfW, safeHalfH, rng, variant);

        fallback = false;
        int segmentsCount;
        int diagCount;
        snapped.TryGetLoopStats(safeHalfW, safeHalfH, rng, variant, out segmentsCount, out diagCount);

        MarbleRaceTrack track;
        if (center != null && center.Length >= 120 && !HasSelfIntersections(center, 4, 10))
        {
            track = BuildTrackData(center, minDim);
            RotateToBestStraight(track);
        }
        else
        {
            fallback = true;
            track = BuildFallbackRoundedRectangle(safeHalfW, safeHalfH, variant);
            segmentsCount = 0;
            diagCount = 0;
        }

        Debug.Log($"[TrackGenSnapped] variant={variant} fallback={(fallback ? 1 : 0)} segments={segmentsCount} diagonals={diagCount}");
        return track;
    }

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int seed, int variant, int fixedTemplateId, out bool fallbackUsed)
    {
        return BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant, out fallbackUsed);
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight, int variant)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var rng = new SeededRng(StableMix(variant, 0x1234, 0x55AA, 0x3141));

        var sampleCount = 512;
        var center = new Vector2[sampleCount];
        var rx = safeHalfW * rng.Range(0.5f, 0.72f);
        var ry = safeHalfH * rng.Range(0.5f, 0.72f);
        var lobeX = rng.Range(0.05f, 0.13f);
        var lobeY = rng.Range(0.04f, 0.11f);
        var phase = rng.Range(0f, Mathf.PI * 2f);

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleCount * Mathf.PI * 2f;
            var x = Mathf.Cos(t) * rx * (1f + Mathf.Sin((2f * t) + phase) * lobeX);
            var y = Mathf.Sin(t) * ry * (1f + Mathf.Cos((3f * t) - phase) * lobeY);
            center[i] = new Vector2(x, y);
        }

        var track = BuildTrackData(center, minDim);
        RotateToBestStraight(track);
        return track;
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
        var halfWidth = BuildWidths(curvature, tangent, baseHalfWidth);
        return new MarbleRaceTrack(center, tangent, normal, halfWidth, curvature, new sbyte[n]);
    }

    private static float[] BuildWidths(float[] curvature, Vector2[] tangent, float baseHalfWidth)
    {
        var n = curvature.Length;
        var widths = new float[n];
        var minW = baseHalfWidth * 0.82f;
        var maxW = baseHalfWidth * 1.6f;

        for (var i = 0; i < n; i++)
        {
            var c = Mathf.Clamp01(curvature[i] * 6f);
            var straightBoost = Mathf.Clamp01((0.04f - curvature[i]) / 0.04f) * 0.2f;
            var turnPenalty = c * 0.24f;

            var run = 0;
            for (var k = -6; k <= 6; k++)
            {
                var idx = (i + k + n) % n;
                if (curvature[idx] < 0.03f)
                {
                    run++;
                }
            }

            var overtakeBoost = run >= 10 ? 0.18f : 0f;
            var wobble = Mathf.Abs(Mathf.Sin((tangent[i].x + tangent[i].y) * 3.2f)) * 0.04f;
            var w = baseHalfWidth * (1f + straightBoost + overtakeBoost + wobble - turnPenalty);
            widths[i] = Mathf.Clamp(w, minW, maxW);
        }

        return widths;
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

    private static void RotateToBestStraight(MarbleRaceTrack track)
    {
        var n = track.SampleCount;
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
