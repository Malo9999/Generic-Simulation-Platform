using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 512;

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

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var safeW = Mathf.Max(12f, arenaHalfWidth);
        var safeH = Mathf.Max(12f, arenaHalfHeight);
        var templateId = Mathf.Abs(variant + (rng != null ? rng.NextInt(0, 3) : 0)) % 3;

        var points = BuildFromTemplate(templateId, safeW, safeH);
        var center = ResampleArcLength(points, TargetSamples);
        if (!Validate(center))
        {
            return BuildFallbackRoundedRectangle(safeW, safeH);
        }

        return BuildTrackData(center, safeW, safeH);
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight)
    {
        var points = BuildFromTemplate(1, arenaHalfWidth, arenaHalfHeight);
        var center = ResampleArcLength(points, TargetSamples);
        return BuildTrackData(center, arenaHalfWidth, arenaHalfHeight);
    }

    private static List<Vector2> BuildFromTemplate(int templateId, float halfW, float halfH)
    {
        var min = Mathf.Min(halfW, halfH);
        var longStraight = Mathf.Min(halfW * 1.2f, halfW + 14f);
        var mediumStraight = Mathf.Min(halfW, halfW + 8f);
        var shortStraight = Mathf.Clamp(min * 0.6f, 6f, 20f);
        var sweepR = Mathf.Clamp(min * 0.62f, 6f, 24f);
        var hairpinR = Mathf.Clamp(min * 0.28f, 4f, 12f);

        List<Segment> segments;
        switch (templateId)
        {
            case 0:
                // Template 1: GP style.
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR, 75f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, sweepR * 0.52f, sweepR * 0.48f, 24f, true),
                    new Segment(SegmentKind.Arc, hairpinR, 170f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.86f, 110f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.75f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.86f, 20f, 0f, true)
                };
                break;
            case 2:
                // Template 3: kidney + S bend.
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, mediumStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.9f, 95f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, sweepR * 0.46f, sweepR * 0.42f, 34f, false),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.7f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.56f, 160f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.65f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.84f, 95f, 0f, true)
                };
                break;
            default:
                // Template 2: rounded rectangle + chicane + hairpin.
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.8f, 90f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, sweepR * 0.44f, sweepR * 0.5f, 26f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.05f, 175f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.55f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.82f, 95f, 0f, true)
                };
                break;
        }

        var polyline = BuildPolyline(segments, Mathf.Clamp(min * 0.04f, 0.45f, 1.2f));
        NormalizeAndClamp(polyline, halfW - 2.5f, halfH - 2.5f);
        return polyline;
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

    private static void NormalizeAndClamp(List<Vector2> points, float halfW, float halfH)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < points.Count; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        var center = (min + max) * 0.5f;
        var extent = (max - min) * 0.5f;
        var scale = Mathf.Min(halfW / Mathf.Max(1f, extent.x), halfH / Mathf.Max(1f, extent.y));

        for (var i = 0; i < points.Count; i++)
        {
            var p = (points[i] - center) * scale;
            p.x = Mathf.Clamp(p.x, -halfW, halfW);
            p.y = Mathf.Clamp(p.y, -halfH, halfH);
            points[i] = p;
        }
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
            curvature[i] = Vector2.Angle(tangent[(i - 1 + n) % n], t) / 180f;
        }

        var baseHalfWidth = Mathf.Clamp(Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.10f, 1.8f, 4.8f);
        var widths = new float[n];
        var overtakeA = (start: Mathf.RoundToInt(n * 0.13f), end: Mathf.RoundToInt(n * 0.23f));
        var overtakeB = (start: Mathf.RoundToInt(n * 0.58f), end: Mathf.RoundToInt(n * 0.72f));

        for (var i = 0; i < n; i++)
        {
            var width = baseHalfWidth;
            if (InRangeWrapped(i, overtakeA.start, overtakeA.end, n) || InRangeWrapped(i, overtakeB.start, overtakeB.end, n))
            {
                width *= 1.35f;
            }

            var cornerTightening = Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(curvature[i] * 3f));
            width *= cornerTightening;
            widths[i] = Mathf.Clamp(width, baseHalfWidth * 0.72f, baseHalfWidth * 1.7f);
        }

        return new MarbleRaceTrack(center, tangent, normal, widths, curvature);
    }

    private static bool Validate(Vector2[] center)
    {
        if (center == null || center.Length < 16)
        {
            return false;
        }

        var n = center.Length;
        var prevTangent = (center[1] - center[0]).normalized;
        for (var i = 1; i < n; i++)
        {
            var tangent = (center[(i + 1) % n] - center[(i - 1 + n) % n]).normalized;
            if (Vector2.Dot(tangent, prevTangent) < 0.2f)
            {
                return false;
            }

            prevTangent = tangent;
        }

        return !HasSelfIntersection(center, 12);
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
}
