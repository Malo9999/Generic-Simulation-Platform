using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 512;
    private const int TemplateCount = 7;
    private const int MaxAttempts = 8;

    private enum SegmentKind
    {
        Straight,
        Arc,
        Chicane
    }

    private struct Segment
    {
        public SegmentKind Kind;
        public float A;
        public float B;
        public float C;
        public bool Left;

        public Segment(SegmentKind kind, float a, float b, float c, bool left)
        {
            Kind = kind;
            A = a;
            B = b;
            C = c;
            Left = left;
        }
    }

    private struct BuildResult
    {
        public MarbleRaceTrack Track;
        public int TemplateId;
        public int Attempt;
        public float MinWidth;
        public float MaxWidth;
        public bool UsedFallback;
    }

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var result = BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant);
        Debug.Log($"[MarbleRaceTrackGenerator] template={result.TemplateId} attempt={result.Attempt} widthMin={result.MinWidth:F2} widthMax={result.MaxWidth:F2} fallback={result.UsedFallback}");
        return result.Track;
    }

    private BuildResult BuildInternal(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var safeW = Mathf.Max(12f, arenaHalfWidth);
        var safeH = Mathf.Max(12f, arenaHalfHeight);
        var baseStep = Mathf.Clamp(Mathf.Min(safeW, safeH) * 0.04f, 0.45f, 1.2f);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var randomOffset = rng != null ? rng.NextInt(0, TemplateCount) : 0;
            var templateId = Mathf.Abs(variant + randomOffset + attempt) % TemplateCount;
            var t = attempt / (float)(MaxAttempts - 1);
            var step = baseStep * Mathf.Lerp(1f, 0.7f, t);
            var lengthMul = Mathf.Lerp(1f, 0.92f, t * 0.85f);
            var radiusMul = Mathf.Lerp(1f, 0.88f, t * 0.9f);

            var points = BuildFromTemplate(templateId, safeW, safeH, step, lengthMul, radiusMul);
            if (!ScaleToFit(points, safeW, safeH, 0.75f))
            {
                continue;
            }

            var center = ResampleArcLength(points, TargetSamples);
            var preview = BuildTrackData(center, safeW, safeH);
            if (!Validate(preview.Center, preview.Tangent, preview.HalfWidth, safeW, safeH))
            {
                continue;
            }

            GetMinMax(preview.HalfWidth, out var minWidth, out var maxWidth);
            return new BuildResult
            {
                Track = preview,
                TemplateId = templateId,
                Attempt = attempt,
                MinWidth = minWidth,
                MaxWidth = maxWidth,
                UsedFallback = false
            };
        }

        var fallback = BuildFallbackRoundedRectangle(safeW, safeH);
        GetMinMax(fallback.HalfWidth, out var fallbackMin, out var fallbackMax);
        return new BuildResult
        {
            Track = fallback,
            TemplateId = 1,
            Attempt = MaxAttempts,
            MinWidth = fallbackMin,
            MaxWidth = fallbackMax,
            UsedFallback = true
        };
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight)
    {
        var safeW = Mathf.Max(12f, arenaHalfWidth);
        var safeH = Mathf.Max(12f, arenaHalfHeight);
        var step = Mathf.Clamp(Mathf.Min(safeW, safeH) * 0.03f, 0.4f, 1f);
        var points = BuildFromTemplate(1, safeW, safeH, step, 0.88f, 0.86f);
        ScaleToFit(points, safeW, safeH, 1f);
        var center = ResampleArcLength(points, TargetSamples);
        return BuildTrackData(center, safeW, safeH);
    }

    private static List<Vector2> BuildFromTemplate(int templateId, float halfW, float halfH, float step, float lengthMul, float radiusMul)
    {
        var min = Mathf.Min(halfW, halfH);
        var longStraight = Mathf.Min(halfW * 1.2f, halfW + 14f) * lengthMul;
        var mediumStraight = Mathf.Min(halfW, halfW + 8f) * lengthMul;
        var shortStraight = Mathf.Clamp(min * 0.6f, 6f, 20f) * lengthMul;
        var overtakeStraight = Mathf.Clamp(min * 0.82f, 9f, 28f) * lengthMul;
        var sweepR = Mathf.Clamp(min * 0.62f, 6f, 24f) * radiusMul;
        var mediumR = Mathf.Clamp(min * 0.48f, 5f, 18f) * radiusMul;
        var hairpinR = Mathf.Clamp(min * 0.28f, 4f, 12f) * radiusMul;

        List<Segment> segments;
        switch (templateId)
        {
            case 0:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.1f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.7f, mediumR * 0.76f, 26f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR, 172f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 1.1f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.82f, 92f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.95f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR, 74f, 0f, false),
                    new Segment(SegmentKind.Straight, shortStraight * 0.8f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.76f, 65f, 0f, true)
                };
                break;
            case 1:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.15f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.84f, 88f, 0f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.66f, mediumR * 0.72f, 28f, false),
                    new Segment(SegmentKind.Straight, shortStraight * 0.92f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.08f, 176f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.84f, 96f, 0f, true)
                };
                break;
            case 2:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 1.04f, 72f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 1.04f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.64f, mediumR * 0.7f, 32f, true),
                    new Segment(SegmentKind.Straight, shortStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 0.95f, 168f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.72f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.76f, 84f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.8f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.8f, 60f, 0f, false)
                };
                break;
            case 3:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.2f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.72f, 80f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.92f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR, 170f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.82f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.62f, mediumR * 0.66f, 24f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.9f, 98f, 0f, true)
                };
                break;
            case 4:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.05f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.66f, mediumR * 0.72f, 30f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.1f, 178f, 0f, false),
                    new Segment(SegmentKind.Straight, shortStraight * 1.2f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.88f, 84f, 0f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.82f, 88f, 0f, true)
                };
                break;
            case 5:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.18f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.92f, 84f, 0f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight * 1.1f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.56f, mediumR * 0.6f, 22f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.75f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.02f, 166f, 0f, false),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.74f, 76f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.85f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.84f, 68f, 0f, true)
                };
                break;
            default:
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.08f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.8f, 86f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 1.02f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 0.98f, 172f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 1.18f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.58f, mediumR * 0.64f, 30f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.78f, 94f, 0f, true)
                };
                break;
        }

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
            switch (segment.Kind)
            {
                case SegmentKind.Straight:
                    {
                        var steps = Mathf.Max(2, Mathf.CeilToInt(segment.A / step));
                        var delta = dir * (segment.A / steps);
                        for (var s = 0; s < steps; s++)
                        {
                            pos += delta;
                            points.Add(pos);
                        }
                    }
                    break;
                case SegmentKind.Arc:
                    {
                        var sign = segment.Left ? 1f : -1f;
                        var radius = Mathf.Max(0.5f, segment.A);
                        var angleRad = Mathf.Deg2Rad * segment.B;
                        var arcLength = Mathf.Abs(angleRad) * radius;
                        var steps = Mathf.Max(6, Mathf.CeilToInt(arcLength / step));
                        var center = pos + new Vector2(-dir.y, dir.x) * sign * radius;
                        var rel = pos - center;
                        var delta = angleRad / steps;
                        for (var s = 0; s < steps; s++)
                        {
                            var cos = Mathf.Cos(delta);
                            var sin = Mathf.Sin(delta) * sign;
                            rel = new Vector2((rel.x * cos) - (rel.y * sin), (rel.x * sin) + (rel.y * cos));
                            pos = center + rel;
                            dir = new Vector2(-rel.y, rel.x).normalized * sign;
                            points.Add(pos);
                        }
                    }
                    break;
                case SegmentKind.Chicane:
                    {
                        var first = new Segment(SegmentKind.Arc, segment.A, segment.C, 0f, segment.Left);
                        var second = new Segment(SegmentKind.Arc, segment.B, segment.C, 0f, !segment.Left);
                        var chunk = BuildPolyline(new List<Segment> { first, second }, step);
                        for (var c = 1; c < chunk.Count; c++)
                        {
                            var local = chunk[c] - chunk[0];
                            var world = pos + (dir * local.x) + (new Vector2(-dir.y, dir.x) * local.y);
                            points.Add(world);
                        }

                        var prev = points[points.Count - 2];
                        pos = points[points.Count - 1];
                        dir = (pos - prev).normalized;
                    }
                    break;
            }
        }

        if ((points[points.Count - 1] - points[0]).sqrMagnitude > 0.01f)
        {
            points.Add(points[0]);
        }

        return points;
    }

    private static bool ScaleToFit(List<Vector2> points, float halfW, float halfH, float margin)
    {
        if (points == null || points.Count < 3)
        {
            return false;
        }

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < points.Count; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        var center = (min + max) * 0.5f;
        var extent = (max - min) * 0.5f;
        var usableX = halfW - margin;
        var usableY = halfH - margin;
        if (usableX <= 0.5f || usableY <= 0.5f)
        {
            return false;
        }

        var scale = Mathf.Min(usableX / Mathf.Max(1f, extent.x), usableY / Mathf.Max(1f, extent.y));
        if (scale < 0.35f)
        {
            return false;
        }

        for (var i = 0; i < points.Count; i++)
        {
            points[i] = (points[i] - center) * scale;
        }

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (Mathf.Abs(p.x) > halfW - 0.001f || Mathf.Abs(p.y) > halfH - 0.001f)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector2[] ResampleArcLength(List<Vector2> points, int samples)
    {
        var lengths = new float[points.Count];
        for (var i = 1; i < points.Count; i++)
        {
            lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        var total = lengths[lengths.Length - 1];
        var result = new Vector2[samples];
        var seg = 1;
        for (var i = 0; i < samples; i++)
        {
            var dist = (i / (float)samples) * total;
            while (seg < lengths.Length - 1 && lengths[seg] < dist)
            {
                seg++;
            }

            var prev = seg - 1;
            var span = Mathf.Max(0.0001f, lengths[seg] - lengths[prev]);
            var t = Mathf.Clamp01((dist - lengths[prev]) / span);
            result[i] = Vector2.Lerp(points[prev], points[seg], t);
        }

        return result;
    }

    private MarbleRaceTrack BuildTrackData(Vector2[] center, float arenaHalfWidth, float arenaHalfHeight)
    {
        var n = center.Length;
        var tangent = new Vector2[n];
        var normal = new Vector2[n];
        var curvature = new float[n];

        PopulateFrames(center, tangent, normal, curvature);

        var baseHalfWidth = Mathf.Clamp(Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.06f, 1.2f, 3.2f);
        var widths = BuildWidths(curvature, baseHalfWidth);
        var startIndex = FindBestStraightStart(curvature, widths, baseHalfWidth);
        if (startIndex > 0)
        {
            RotateArray(center, startIndex);
            RotateArray(tangent, startIndex);
            RotateArray(normal, startIndex);
            RotateArray(curvature, startIndex);
            RotateArray(widths, startIndex);
        }

        return new MarbleRaceTrack(center, tangent, normal, widths, curvature);
    }

    private static void PopulateFrames(Vector2[] center, Vector2[] tangent, Vector2[] normal, float[] curvature)
    {
        var n = center.Length;
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
    }

    private static float[] BuildWidths(float[] curvature, float baseHalfWidth)
    {
        var n = curvature.Length;
        var widths = new float[n];
        var overtakeAStart = Mathf.RoundToInt(n * 0.11f);
        var overtakeAEnd = Mathf.RoundToInt(n * 0.22f);
        var overtakeBStart = Mathf.RoundToInt(n * 0.55f);
        var overtakeBEnd = Mathf.RoundToInt(n * 0.69f);

        for (var i = 0; i < n; i++)
        {
            var width = baseHalfWidth;
            if (InRangeWrapped(i, overtakeAStart, overtakeAEnd, n) || InRangeWrapped(i, overtakeBStart, overtakeBEnd, n))
            {
                width = Mathf.Min(baseHalfWidth * 1.5f, baseHalfWidth * 1.6f);
            }

            var cornerTightening = Mathf.Lerp(1f, 0.85f, Mathf.Clamp01(curvature[i] * 3f));
            width *= cornerTightening;
            widths[i] = Mathf.Clamp(width, baseHalfWidth * 0.85f, baseHalfWidth * 1.6f);
        }

        return widths;
    }

    private static int FindBestStraightStart(float[] curvature, float[] widths, float baseHalfWidth)
    {
        var n = curvature.Length;
        var minWindow = Mathf.Min(40, Mathf.Max(12, n / 12));
        var bestStart = 0;
        var bestLength = 0;

        var runStart = -1;
        var runLength = 0;
        for (var i = 0; i < n * 2; i++)
        {
            var idx = i % n;
            var isStraight = curvature[idx] < 0.06f && widths[idx] >= baseHalfWidth * 1.2f;
            if (isStraight)
            {
                if (runStart < 0)
                {
                    runStart = i;
                    runLength = 0;
                }

                runLength++;
                if (runLength > bestLength)
                {
                    bestLength = runLength;
                    bestStart = runStart % n;
                }
            }
            else
            {
                runStart = -1;
                runLength = 0;
            }
        }

        return bestLength >= minWindow ? bestStart : 0;
    }

    private static bool Validate(Vector2[] center, Vector2[] tangent, float[] halfWidth, float halfW, float halfH)
    {
        if (center == null || tangent == null || halfWidth == null || center.Length < 16)
        {
            return false;
        }

        var n = center.Length;
        var maxHalfWidth = 0f;
        for (var i = 0; i < n; i++)
        {
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
        }

        var requiredMargin = maxHalfWidth + (Mathf.Min(halfW, halfH) * 0.08f);
        for (var i = 0; i < n; i++)
        {
            if (Mathf.Abs(center[i].x) > halfW - requiredMargin || Mathf.Abs(center[i].y) > halfH - requiredMargin)
            {
                return false;
            }
        }

        const float maxCurvature = 0.16f;
        var aboveCurvatureLimit = 0;
        var prevTangent = tangent[0];
        for (var i = 1; i < n; i++)
        {
            var dot = Vector2.Dot(prevTangent, tangent[i]);
            if (dot < 0.35f)
            {
                return false;
            }

            var localCurvature = Vector2.Angle(prevTangent, tangent[i]) / 180f;
            if (localCurvature > maxCurvature)
            {
                aboveCurvatureLimit++;
            }

            prevTangent = tangent[i];
        }

        if (aboveCurvatureLimit > Mathf.Max(8, n / 14))
        {
            return false;
        }

        if (HasSelfIntersection(center, 7))
        {
            return false;
        }

        var left = new Vector2[n];
        var right = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var normal = new Vector2(-tangent[i].y, tangent[i].x);
            left[i] = center[i] + normal * halfWidth[i];
            right[i] = center[i] - normal * halfWidth[i];
        }

        if (HasSelfIntersection(left, 9) || HasSelfIntersection(right, 9))
        {
            return false;
        }

        if (HasIntersectionsBetween(left, right, 9, n))
        {
            return false;
        }

        return true;
    }

    private static bool HasSelfIntersection(Vector2[] center, int stride)
    {
        for (var i = 0; i < center.Length; i += stride)
        {
            var a1 = center[i];
            var a2 = center[(i + stride) % center.Length];
            for (var j = i + (2 * stride); j < center.Length; j += stride)
            {
                if (Mathf.Abs(i - j) < stride * 2)
                {
                    continue;
                }

                var b1 = center[j];
                var b2 = center[(j + stride) % center.Length];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasIntersectionsBetween(Vector2[] a, Vector2[] b, int stride, int count)
    {
        for (var i = 0; i < count; i += stride)
        {
            var a1 = a[i];
            var a2 = a[(i + stride) % count];
            for (var j = 0; j < count; j += stride)
            {
                var cyclic = Mathf.Abs(i - j);
                if (cyclic < stride * 2 || cyclic > count - stride * 2)
                {
                    continue;
                }

                var b1 = b[j];
                var b2 = b[(j + stride) % count];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        if (Mathf.Abs(denom) < 0.0001f)
        {
            return false;
        }

        var t = Cross(q1 - p1, s) / denom;
        var u = Cross(q1 - p1, r) / denom;
        return t > 0f && t < 1f && u > 0f && u < 1f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static bool InRangeWrapped(int index, int start, int end, int count)
    {
        index = ((index % count) + count) % count;
        start = ((start % count) + count) % count;
        end = ((end % count) + count) % count;

        if (start <= end)
        {
            return index >= start && index <= end;
        }

        return index >= start || index <= end;
    }

    private static void RotateArray<T>(T[] array, int start)
    {
        if (array == null || array.Length <= 1 || start <= 0)
        {
            return;
        }

        var copy = new T[array.Length];
        for (var i = 0; i < array.Length; i++)
        {
            copy[i] = array[(i + start) % array.Length];
        }

        for (var i = 0; i < array.Length; i++)
        {
            array[i] = copy[i];
        }
    }

    private static void GetMinMax(float[] values, out float min, out float max)
    {
        if (values == null || values.Length == 0)
        {
            min = 0f;
            max = 0f;
            return;
        }

        min = float.MaxValue;
        max = float.MinValue;
        for (var i = 0; i < values.Length; i++)
        {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
        }
    }
}
