using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 384;
    private const int DenseSamples = 320;
    private const int MinControlPointCount = 12;
    private const int MaxControlPointCount = 16;
    private const int MaxAttempts = 30;
    private const int QualityThreshold = 75;

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        return BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant, out _);
    }

    private MarbleRaceTrack BuildInternal(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant, out bool fallback)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var sourceSeed = rng != null ? rng.Seed : variant;

        MarbleRaceTrack bestTrack = null;
        var bestScore = int.MinValue;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var attemptSeed = StableMix(sourceSeed, variant, attempt, 0x2D2816FE);
            var attemptRng = new SeededRng(attemptSeed);
            var controlPoints = BuildPolarControlPoints(safeHalfW, safeHalfH, attemptRng);
            if (controlPoints == null || controlPoints.Count < 6)
            {
                continue;
            }

            var denseSpline = SampleClosedCentripetalCatmullRom(controlPoints, DenseSamples);
            if (denseSpline == null || denseSpline.Length < 32)
            {
                continue;
            }

            var equalized = ResampleArcLengthClosed(denseSpline, TargetSamples);
            if (equalized == null)
            {
                continue;
            }

            var smoothed = ApplyChaikin(equalized, 1);
            var center = ResampleArcLengthClosed(smoothed, TargetSamples);
            if (center == null)
            {
                continue;
            }

            ClampInside(center, safeHalfW - 3f, safeHalfH - 3f);

            if (!TryBuildTrack(center, minDim, out var candidate))
            {
                continue;
            }

            var quality = MarbleRaceTrackValidator.EvaluateQuality(candidate);
            if (quality.Score > bestScore)
            {
                bestScore = quality.Score;
                bestTrack = candidate;
            }

            if (quality.Score >= QualityThreshold)
            {
                fallback = false;
                Debug.Log($"[TrackGen] variant={variant} accepted attempt={attempt + 1}/{MaxAttempts} score={quality.Score}");
                return candidate;
            }
        }

        if (bestTrack != null)
        {
            fallback = bestScore < QualityThreshold;
            Debug.Log($"[TrackGen] variant={variant} bestOfN score={bestScore} attempts={MaxAttempts}");
            return bestTrack;
        }

        fallback = true;
        var backup = BuildFallbackRoundedRectangle(safeHalfW, safeHalfH, variant);
        Debug.Log($"[TrackGen] variant={variant} generated fallback rounded rectangle");
        return backup;
    }

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int seed, int variant, int fixedTemplateId, out bool fallbackUsed)
    {
        if (fixedTemplateId >= 0)
        {
            variant = fixedTemplateId;
        }

        return BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant, out fallbackUsed);
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight, int variant)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var rng = new SeededRng(StableMix(variant, 0x1234, 0x55AA, 0x3141));

        const int sampleCount = 512;
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

    private static List<Vector2> BuildPolarControlPoints(float arenaHalfWidth, float arenaHalfHeight, IRng rng)
    {
        var localRng = rng ?? new SeededRng(0x4F1BBCDC);
        var controlPointCount = localRng.Range(MinControlPointCount, MaxControlPointCount + 1);
        var minHalf = Mathf.Min(arenaHalfWidth, arenaHalfHeight);
        var margin = Mathf.Clamp(minHalf * 0.18f, 2.5f, 7.5f);
        var maxAllowedRadius = Mathf.Max(3f, minHalf - margin);
        var baseRadius = maxAllowedRadius * localRng.Range(0.74f, 0.86f);
        var angleStep = (Mathf.PI * 2f) / controlPointCount;

        var points = new List<Vector2>(controlPointCount);
        var minNeighborDistance = maxAllowedRadius * 0.3f;

        for (var i = 0; i < controlPointCount; i++)
        {
            var baseAngle = i * angleStep;
            var angleJitter = localRng.Range(-angleStep * 0.25f, angleStep * 0.25f);
            var angle = baseAngle + angleJitter;

            var radiusJitter = localRng.Range(-baseRadius * 0.25f, baseRadius * 0.25f);
            var radius = Mathf.Clamp(baseRadius + radiusJitter, baseRadius * 0.62f, maxAllowedRadius);

            var p = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            if (i > 0)
            {
                var prev = points[i - 1];
                var d = Vector2.Distance(prev, p);
                if (d < minNeighborDistance)
                {
                    var toPrev = (p - prev).normalized;
                    if (toPrev.sqrMagnitude < 1e-4f)
                    {
                        toPrev = new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)).normalized;
                    }

                    p = prev + (toPrev * minNeighborDistance);
                    var clampedRadius = Mathf.Clamp(p.magnitude, baseRadius * 0.62f, maxAllowedRadius);
                    p = p.normalized * clampedRadius;
                }
            }

            points.Add(p);
        }

        var first = points[0];
        var last = points[points.Count - 1];
        var endDist = Vector2.Distance(first, last);
        if (endDist < minNeighborDistance)
        {
            var dir = (last - first).normalized;
            if (dir.sqrMagnitude < 1e-4f)
            {
                dir = (last + first).normalized;
            }

            last = first + (dir * minNeighborDistance);
            var r = Mathf.Clamp(last.magnitude, baseRadius * 0.62f, maxAllowedRadius);
            points[points.Count - 1] = last.normalized * r;
        }

        return points;
    }

    private static Vector2[] SampleClosedCentripetalCatmullRom(List<Vector2> controlPoints, int sampleCount)
    {
        if (controlPoints == null || controlPoints.Count < 4 || sampleCount < 16)
        {
            return null;
        }

        var n = controlPoints.Count;
        var output = new Vector2[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var u = i / (float)sampleCount;
            var segmentF = u * n;
            var seg = Mathf.FloorToInt(segmentF) % n;
            var t = segmentF - Mathf.Floor(segmentF);

            var p0 = controlPoints[(seg - 1 + n) % n];
            var p1 = controlPoints[seg];
            var p2 = controlPoints[(seg + 1) % n];
            var p3 = controlPoints[(seg + 2) % n];

            output[i] = EvaluateCentripetalCatmullRom(p0, p1, p2, p3, t);
        }

        return output;
    }

    private static Vector2 EvaluateCentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        const float alpha = 0.5f;
        var t0 = 0f;
        var t1 = t0 + Mathf.Pow(Vector2.Distance(p0, p1), alpha);
        var t2 = t1 + Mathf.Pow(Vector2.Distance(p1, p2), alpha);
        var t3 = t2 + Mathf.Pow(Vector2.Distance(p2, p3), alpha);

        if (Mathf.Abs(t1 - t0) < 1e-4f || Mathf.Abs(t2 - t1) < 1e-4f || Mathf.Abs(t3 - t2) < 1e-4f)
        {
            return Vector2.Lerp(p1, p2, t);
        }

        var tt = Mathf.Lerp(t1, t2, t);

        var a1 = ((t1 - tt) / (t1 - t0) * p0) + ((tt - t0) / (t1 - t0) * p1);
        var a2 = ((t2 - tt) / (t2 - t1) * p1) + ((tt - t1) / (t2 - t1) * p2);
        var a3 = ((t3 - tt) / (t3 - t2) * p2) + ((tt - t2) / (t3 - t2) * p3);

        var b1 = ((t2 - tt) / (t2 - t0) * a1) + ((tt - t0) / (t2 - t0) * a2);
        var b2 = ((t3 - tt) / (t3 - t1) * a2) + ((tt - t1) / (t3 - t1) * a3);

        return ((t2 - tt) / (t2 - t1) * b1) + ((tt - t1) / (t2 - t1) * b2);
    }

    private static void ClampInside(Vector2[] pts, float halfW, float halfH)
    {
        for (var i = 0; i < pts.Length; i++)
        {
            var p = pts[i];
            pts[i] = new Vector2(Mathf.Clamp(p.x, -halfW, halfW), Mathf.Clamp(p.y, -halfH, halfH));
        }
    }

    private static Vector2[] ApplyChaikin(Vector2[] points, int passes)
    {
        if (points == null)
        {
            return null;
        }

        var outPts = new List<Vector2>(points);
        if (passes <= 0 || outPts.Count < 4)
        {
            return outPts.ToArray();
        }

        for (var pass = 0; pass < passes; pass++)
        {
            var src = new List<Vector2>(outPts);
            outPts.Clear();
            var n = src.Count;
            for (var i = 0; i < n; i++)
            {
                var p0 = src[i];
                var p1 = src[(i + 1) % n];
                outPts.Add(Vector2.Lerp(p0, p1, 0.25f));
                outPts.Add(Vector2.Lerp(p0, p1, 0.75f));
            }
        }

        return outPts.ToArray();
    }

    private static Vector2[] ResampleArcLengthClosed(Vector2[] points, int target)
    {
        if (points == null || points.Length < 3)
        {
            return null;
        }

        var n = points.Length;
        var lengths = new float[n + 1];
        var total = 0f;
        lengths[0] = 0f;
        for (var i = 0; i < n; i++)
        {
            total += Vector2.Distance(points[i], points[(i + 1) % n]);
            lengths[i + 1] = total;
        }

        if (total < 1e-3f)
        {
            return null;
        }

        var outPts = new Vector2[target];
        for (var i = 0; i < target; i++)
        {
            var d = (i / (float)target) * total;
            var seg = 0;
            while (seg < n - 1 && lengths[seg + 1] < d)
            {
                seg++;
            }

            var segLen = Mathf.Max(1e-6f, lengths[seg + 1] - lengths[seg]);
            var t = Mathf.Clamp01((d - lengths[seg]) / segLen);
            outPts[i] = Vector2.Lerp(points[seg], points[(seg + 1) % n], t);
        }

        return outPts;
    }

    private static bool TryBuildTrack(Vector2[] center, float minDim, out MarbleRaceTrack track)
    {
        track = null;
        if (center == null || center.Length < 128)
        {
            return false;
        }

        if (HasSelfIntersections(center, 12, 4))
        {
            return false;
        }

        track = BuildTrackData(center, minDim);
        RotateToBestStraight(track);
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
        var window = Mathf.Clamp(n / 48, 5, 9);
        var bestStart = 0;
        var bestScore = float.MaxValue;

        for (var i = 0; i < n; i++)
        {
            var turnAccum = 0f;
            var widthAccum = 0f;
            for (var j = -window; j <= window; j++)
            {
                var idxA = (i + j + n) % n;
                var idxB = (idxA + 1) % n;
                turnAccum += Vector2.Angle(track.Tangent[idxA], track.Tangent[idxB]);
                widthAccum += track.HalfWidth[idxA];
            }

            var score = turnAccum - (widthAccum * 0.015f);
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
