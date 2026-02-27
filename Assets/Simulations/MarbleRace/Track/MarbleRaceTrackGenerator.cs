using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 512;
    private const int AttemptCount = 24;
    private const bool AllowCrossings = false;

    private struct Segment
    {
        public SegmentType Type;
        public float A;
        public float B;
        public float C;
        public float D;
        public bool Left;

        public Segment(SegmentType type, float a, float b, float c, float d, bool left)
        {
            Type = type;
            A = a;
            B = b;
            C = c;
            D = d;
            Left = left;
        }
    }

    private enum SegmentType
    {
        Straight,
        Arc,
        Chicane
    }

    private struct CandidateStats
    {
        public float Length;
        public int LongStraightRuns;
        public bool HasHairpin;
        public bool HasChicane;
        public float MinClearance;
    }

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;

        MarbleRaceTrack bestTrack = null;
        var bestScore = float.MinValue;
        var bestStats = new CandidateStats();

        for (var attempt = 0; attempt < AttemptCount; attempt++)
        {
            var attemptRng = ForkRng(rng, variant, attempt);
            var raw = ComposeCircuit(minDim, attemptRng);
            if (raw == null || raw.Count < 16)
            {
                continue;
            }

            ChaikinClosed(raw, 2);
            var center = ResampleArcLengthClosed(raw, TargetSamples);

            var baseHalfWidth = Mathf.Clamp(minDim * 0.035f, 0.9f, 2.2f);
            var requiredMargin = baseHalfWidth * 1.45f + (minDim * 0.08f);
            if (!FitToBounds(center, safeHalfW, safeHalfH, requiredMargin))
            {
                continue;
            }

            var candidate = BuildTrackData(center, minDim);
            if (!ValidateStrict(candidate.Center, candidate.Tangent, candidate.Normal, candidate.HalfWidth, safeHalfW, safeHalfH))
            {
                continue;
            }

            var score = ScoreTrack(candidate, safeHalfW, safeHalfH, out var stats);
            if (score > bestScore)
            {
                bestScore = score;
                bestTrack = candidate;
                bestStats = stats;
            }
        }

        if (bestTrack == null)
        {
            bestTrack = BuildFallbackRoundedRectangle(safeHalfW, safeHalfH);
            bestScore = ScoreTrack(bestTrack, safeHalfW, safeHalfH, out bestStats);
        }

        RotateToBestStraight(bestTrack);
        Debug.Log($"[TrackGen] seed={variant} bestScore={bestScore:F1} attempts={AttemptCount} length={bestStats.Length:F1} straights={bestStats.LongStraightRuns} hairpin={(bestStats.HasHairpin ? "yes" : "no")} chicane={(bestStats.HasChicane ? "yes" : "no")}");
        return bestTrack;
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight)
    {
        var safeHalfW = Mathf.Max(12f, arenaHalfWidth);
        var safeHalfH = Mathf.Max(12f, arenaHalfHeight);
        var minDim = Mathf.Min(safeHalfW, safeHalfH) * 2f;
        var baseHalfWidth = Mathf.Clamp(minDim * 0.035f, 0.9f, 2.2f);
        var requiredMargin = baseHalfWidth * 1.45f + (minDim * 0.08f);

        var points = BuildStadium(minDim);
        ChaikinClosed(points, 2);
        var center = ResampleArcLengthClosed(points, TargetSamples);
        FitToBounds(center, safeHalfW, safeHalfH, requiredMargin);

        var fallback = BuildTrackData(center, minDim);
        return fallback;
    }

    private static IRng ForkRng(IRng rng, int variant, int attempt)
    {
        var seed = rng != null ? rng.Seed : 1;
        unchecked
        {
            var mixed = seed;
            mixed = (mixed * 397) ^ variant;
            mixed = (mixed * 397) ^ (attempt * 7919);
            return new SeededRng(mixed);
        }
    }

    private static List<Vector2> ComposeCircuit(float minDim, IRng rng)
    {
        var step = Mathf.Lerp(0.6f, 1.2f, rng.NextFloat01());

        var mainStraight = rng.Range(0.62f * minDim, 0.85f * minDim);
        var sweeperRadius = rng.Range(0.30f * minDim, 0.55f * minDim);
        var sweeperDeg = rng.Range(35f, 110f);
        var backStraight = rng.Range(0.45f * minDim, 0.80f * minDim);

        var chicaneR1 = rng.Range(0.18f * minDim, 0.28f * minDim);
        var chicaneR2 = rng.Range(0.18f * minDim, 0.28f * minDim);
        var chicaneDegA = rng.Range(32f, 70f);
        var chicaneDegB = rng.Range(28f, 62f);
        var chicaneLeftFirst = rng.Sign() > 0;

        var shortStraight = rng.Range(0.35f * minDim, 0.52f * minDim);
        var hairpinRadius = rng.Range(0.12f * minDim, 0.20f * minDim);
        var hairpinDeg = rng.Range(160f, 220f);
        var finalStraight = rng.Range(0.40f * minDim, 0.72f * minDim);

        var sweeperLeft = rng.Sign() > 0;
        var hairpinLeft = sweeperLeft;

        var segments = new List<Segment>(8)
        {
            new Segment(SegmentType.Straight, mainStraight, 0f, 0f, 0f, true),
            new Segment(SegmentType.Arc, sweeperRadius, sweeperDeg, 0f, 0f, sweeperLeft),
            new Segment(SegmentType.Straight, backStraight, 0f, 0f, 0f, true),
            new Segment(SegmentType.Chicane, chicaneR1, chicaneR2, chicaneDegA, chicaneDegB, chicaneLeftFirst),
            new Segment(SegmentType.Straight, shortStraight, 0f, 0f, 0f, true),
            new Segment(SegmentType.Arc, hairpinRadius, hairpinDeg, 0f, 0f, hairpinLeft),
            new Segment(SegmentType.Straight, finalStraight, 0f, 0f, 0f, true)
        };

        var points = BuildPolyline(segments, step);
        if (points.Count < 4)
        {
            return points;
        }

        AppendSmoothClosure(points, Mathf.Clamp(minDim * 0.18f, 2.4f, 8f), step);
        return points;
    }

    private static List<Vector2> BuildStadium(float minDim)
    {
        var step = 0.8f;
        var straight = Mathf.Clamp(minDim * 0.75f, 12f, 48f);
        var radius = Mathf.Clamp(minDim * 0.2f, 4f, 18f);

        var segments = new List<Segment>(4)
        {
            new Segment(SegmentType.Straight, straight, 0f, 0f, 0f, true),
            new Segment(SegmentType.Arc, radius, 180f, 0f, 0f, true),
            new Segment(SegmentType.Straight, straight, 0f, 0f, 0f, true),
            new Segment(SegmentType.Arc, radius, 180f, 0f, 0f, true)
        };

        return BuildPolyline(segments, step);
    }

    private static List<Vector2> BuildPolyline(List<Segment> segments, float step)
    {
        var points = new List<Vector2>(1024) { Vector2.zero };
        var pos = Vector2.zero;
        var dir = Vector2.right;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            switch (segment.Type)
            {
                case SegmentType.Straight:
                    EmitStraight(points, ref pos, dir, Mathf.Max(1f, segment.A), step);
                    break;
                case SegmentType.Arc:
                    EmitArc(points, ref pos, ref dir, Mathf.Max(0.5f, segment.A), segment.B, segment.Left, step);
                    break;
                case SegmentType.Chicane:
                    EmitArc(points, ref pos, ref dir, Mathf.Max(0.5f, segment.A), segment.C, segment.Left, step);
                    EmitArc(points, ref pos, ref dir, Mathf.Max(0.5f, segment.B), segment.D, !segment.Left, step);
                    break;
            }
        }

        return points;
    }

    private static void EmitStraight(List<Vector2> points, ref Vector2 pos, Vector2 dir, float length, float step)
    {
        var steps = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.1f, step)));
        var delta = dir * (length / steps);
        for (var i = 0; i < steps; i++)
        {
            pos += delta;
            points.Add(pos);
        }
    }

    private static void EmitArc(List<Vector2> points, ref Vector2 pos, ref Vector2 dir, float radius, float degrees, bool left, float step)
    {
        var angleRad = Mathf.Abs(degrees) * Mathf.Deg2Rad;
        var arcLength = radius * angleRad;
        var steps = Mathf.Max(5, Mathf.CeilToInt(arcLength / Mathf.Max(0.1f, step)));
        var sign = left ? 1f : -1f;
        var normal = new Vector2(-dir.y, dir.x) * sign;
        var center = pos + (normal * radius);
        var rel = pos - center;
        var delta = angleRad / steps;

        for (var i = 0; i < steps; i++)
        {
            var cos = Mathf.Cos(delta);
            var sin = Mathf.Sin(delta) * sign;
            rel = new Vector2((rel.x * cos) - (rel.y * sin), (rel.x * sin) + (rel.y * cos));
            pos = center + rel;
            dir = (left ? new Vector2(-rel.y, rel.x) : new Vector2(rel.y, -rel.x)).normalized;
            points.Add(pos);
        }
    }

    private static void AppendSmoothClosure(List<Vector2> points, float tangentScale, float step)
    {
        var n = points.Count;
        if (n < 4)
        {
            return;
        }

        var p0 = points[n - 1];
        var p1 = points[0];
        var d0 = (points[n - 1] - points[n - 2]).normalized;
        var d1 = (points[1] - points[0]).normalized;

        var dist = Vector2.Distance(p0, p1);
        var samples = Mathf.Max(6, Mathf.CeilToInt(dist / Mathf.Max(0.1f, step)));
        var m0 = d0 * tangentScale;
        var m1 = d1 * tangentScale;

        for (var i = 1; i <= samples; i++)
        {
            var t = i / (float)samples;
            var tt = t * t;
            var ttt = tt * t;
            var h00 = (2f * ttt) - (3f * tt) + 1f;
            var h10 = ttt - (2f * tt) + t;
            var h01 = (-2f * ttt) + (3f * tt);
            var h11 = ttt - tt;
            var p = (h00 * p0) + (h10 * m0) + (h01 * p1) + (h11 * m1);
            points.Add(p);
        }
    }

    private static void ChaikinClosed(List<Vector2> points, int passes)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            var next = new List<Vector2>(points.Count * 2);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var q = Vector2.Lerp(a, b, 0.25f);
                var r = Vector2.Lerp(a, b, 0.75f);
                next.Add(q);
                next.Add(r);
            }

            points.Clear();
            points.AddRange(next);
        }
    }

    private static Vector2[] ResampleArcLengthClosed(List<Vector2> points, int target)
    {
        var n = points.Count;
        var cumulative = new float[n + 1];
        cumulative[0] = 0f;

        for (var i = 0; i < n; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % n];
            cumulative[i + 1] = cumulative[i] + Vector2.Distance(a, b);
        }

        var total = Mathf.Max(1f, cumulative[n]);
        var output = new Vector2[target];
        for (var i = 0; i < target; i++)
        {
            var d = (i / (float)target) * total;
            var seg = 0;
            while (seg < n - 1 && cumulative[seg + 1] < d)
            {
                seg++;
            }

            var segLen = Mathf.Max(0.0001f, cumulative[seg + 1] - cumulative[seg]);
            var t = Mathf.Clamp01((d - cumulative[seg]) / segLen);
            var pA = points[seg];
            var pB = points[(seg + 1) % n];
            output[i] = Vector2.Lerp(pA, pB, t);
        }

        return output;
    }

    private static bool FitToBounds(Vector2[] points, float halfW, float halfH, float requiredMargin)
    {
        if (points == null || points.Length < 4)
        {
            return false;
        }

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < points.Length; i++)
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
        if (n < 64 || tangent.Length != n || normal.Length != n || halfWidth.Length != n)
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

        var minStep = Mathf.Max(0.6f, minDim * 0.015f);
        var maxStep = Mathf.Min(1.2f, minDim * 0.03f);
        for (var i = 0; i < n; i++)
        {
            var prevI = (i - 1 + n) % n;
            if (Vector2.Dot(tangent[i], tangent[prevI]) < 0.40f)
            {
                return false;
            }

            var d = Vector2.Distance(center[i], center[prevI]);
            if (d < minStep * 0.35f || d > maxStep * 2.2f)
            {
                return false;
            }
        }

        if (!AllowCrossings && HasSelfIntersections(center, 6, 8))
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

        if (HasSelfIntersections(left, 6, 8) || HasSelfIntersections(right, 6, 8))
        {
            return false;
        }

        var separationThreshold = Mathf.Max(halfWidth[0] * 4f, minDim * 0.08f);
        var threshSq = separationThreshold * separationThreshold;
        for (var i = 0; i < n; i += 3)
        {
            for (var j = i + 20; j < n; j += 3)
            {
                var wrapDelta = Mathf.Min(Mathf.Abs(i - j), n - Mathf.Abs(i - j));
                if (wrapDelta < 18)
                {
                    continue;
                }

                var distSq = (center[i] - center[j]).sqrMagnitude;
                if (distSq < threshSq)
                {
                    return false;
                }
            }
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
        var a = i / Mathf.Max(1, stride);
        var b = j / Mathf.Max(1, stride);
        var segCount = Mathf.Max(1, n / Mathf.Max(1, stride));
        var d = Mathf.Abs(a - b);
        d = Mathf.Min(d, segCount - d);
        return d <= neighborIgnore;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        if (Mathf.Abs(denom) < 1e-4f)
        {
            return false;
        }

        var t = Cross(q1 - p1, s) / denom;
        var u = Cross(q1 - p1, r) / denom;
        return t > 0.001f && t < 0.999f && u > 0.001f && u < 0.999f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static float ScoreTrack(MarbleRaceTrack track, float halfW, float halfH, out CandidateStats stats)
    {
        var n = track.SampleCount;
        var length = 0f;
        var longestStraight = 0;
        var straightRun = 0;
        var longStraightRuns = 0;
        var curvatureMean = 0f;

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            length += Vector2.Distance(track.Center[i], track.Center[next]);
            curvatureMean += track.Curvature[i];

            if (track.Curvature[i] < 0.035f)
            {
                straightRun++;
                longestStraight = Mathf.Max(longestStraight, straightRun);
            }
            else
            {
                if (straightRun >= 24)
                {
                    longStraightRuns++;
                }

                straightRun = 0;
            }
        }

        if (straightRun >= 24)
        {
            longStraightRuns++;
        }

        curvatureMean /= Mathf.Max(1, n);
        var curvatureVar = 0f;
        for (var i = 0; i < n; i++)
        {
            var d = track.Curvature[i] - curvatureMean;
            curvatureVar += d * d;
        }

        curvatureVar /= Mathf.Max(1, n);

        var hasHairpin = false;
        var hasChicane = false;
        for (var i = 1; i < n - 1; i++)
        {
            if (track.Curvature[i] > 0.095f)
            {
                hasHairpin = true;
            }

            var crossA = Cross(track.Tangent[i - 1], track.Tangent[i]);
            var crossB = Cross(track.Tangent[i], track.Tangent[i + 1]);
            if (Mathf.Abs(crossA) > 0.02f && Mathf.Abs(crossB) > 0.02f && Mathf.Sign(crossA) != Mathf.Sign(crossB))
            {
                hasChicane = true;
            }
        }

        var minClearance = float.MaxValue;
        var extX = 0f;
        var extY = 0f;
        for (var i = 0; i < n; i++)
        {
            extX = Mathf.Max(extX, Mathf.Abs(track.Center[i].x));
            extY = Mathf.Max(extY, Mathf.Abs(track.Center[i].y));
            minClearance = Mathf.Min(minClearance, Mathf.Min(halfW - Mathf.Abs(track.Center[i].x), halfH - Mathf.Abs(track.Center[i].y)));
        }

        var aspect = Mathf.Min(extX / Mathf.Max(1f, extY), extY / Mathf.Max(1f, extX));
        var targetLength = (Mathf.Min(halfW, halfH) * 2f) * 4.6f;
        var lengthScore = 40f - Mathf.Abs(length - targetLength) * 0.35f;

        var score = lengthScore;
        score += longStraightRuns * 14f;
        score += hasHairpin ? 18f : -22f;
        score += hasChicane ? 14f : -20f;
        score += Mathf.Clamp01(curvatureVar * 300f) * 16f;
        score -= Mathf.Clamp01((0.35f - aspect) / 0.35f) * 24f;
        score -= Mathf.Clamp01((2.5f - minClearance) / 2.5f) * 20f;

        stats = new CandidateStats
        {
            Length = length,
            LongStraightRuns = longStraightRuns,
            HasHairpin = hasHairpin,
            HasChicane = hasChicane,
            MinClearance = minClearance
        };

        return score;
    }

    private static void RotateToBestStraight(MarbleRaceTrack track)
    {
        var n = track.SampleCount;
        var bestStart = 0;
        var bestLen = 0;

        for (var i = 0; i < n; i++)
        {
            if (track.Curvature[i] >= 0.035f)
            {
                continue;
            }

            var len = 0;
            while (len < n && track.Curvature[(i + len) % n] < 0.035f)
            {
                len++;
            }

            if (len > bestLen)
            {
                bestLen = len;
                bestStart = (i + (len / 2)) % n;
            }

            i += Mathf.Max(0, len - 1);
        }

        if (bestLen <= 0 || bestStart == 0)
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
