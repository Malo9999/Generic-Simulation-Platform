using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int SampleCount = 512;

    private enum SegmentKind
    {
        Straight,
        Arc,
        Chicane
    }

    private readonly struct Segment
    {
        public readonly SegmentKind Kind;
        public readonly float A;
        public readonly float B;
        public readonly float C;
        public readonly bool Left;

        public Segment(SegmentKind kind, float a, float b = 0f, float c = 0f, bool left = true)
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
        var segments = BuildTemplate(arenaHalfWidth, arenaHalfHeight, variant, rng);
        var step = Mathf.Clamp(Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.03f, 0.6f, 1.2f);
        var raw = BuildPolyline(segments, step);
        ClampInside(raw, arenaHalfWidth - 3f, arenaHalfHeight - 3f);

        var smooth = ApplyChaikin(raw, 2);
        var center = ResampleEvenly(smooth, SampleCount);
        if (!TryBuildTrack(center, arenaHalfWidth, arenaHalfHeight, out var track))
        {
            return BuildFallbackRoundedRectangle(arenaHalfWidth, arenaHalfHeight);
        }

        return track;
    }

    private static List<Segment> BuildTemplate(float halfW, float halfH, int variant, IRng rng)
    {
        var longStraight = Mathf.Min(halfW * 1.35f, halfW + 12f);
        var backStraight = Mathf.Min(halfW * 1.15f, halfW + 8f);
        var shortStraight = Mathf.Min(halfW * 0.6f, halfW * 0.8f);
        var sweeperRadius = Mathf.Min(halfW, halfH) * 0.55f;
        var hairpinRadius = Mathf.Min(halfW, halfH) * 0.28f;
        var chicaneRLeft = Mathf.Min(halfW, halfH) * 0.35f;
        var chicaneRRight = Mathf.Min(halfW, halfH) * 0.30f;

        var v = Mathf.Abs(variant + (rng?.NextInt(0, 3) ?? 0)) % 3;
        return v switch
        {
            1 => new List<Segment>
            {
                new(SegmentKind.Straight, longStraight),
                new(SegmentKind.Arc, sweeperRadius, 70f, left: true),
                new(SegmentKind.Straight, backStraight),
                new(SegmentKind.Chicane, chicaneRLeft, chicaneRRight, 28f),
                new(SegmentKind.Arc, hairpinRadius, 155f, left: true),
                new(SegmentKind.Straight, shortStraight),
                new(SegmentKind.Arc, sweeperRadius * 0.85f, 115f, left: true),
                new(SegmentKind.Straight, shortStraight * 0.8f),
                new(SegmentKind.Arc, sweeperRadius * 0.9f, 20f, left: true)
            },
            2 => new List<Segment>
            {
                new(SegmentKind.Straight, longStraight * 0.9f),
                new(SegmentKind.Arc, sweeperRadius * 0.95f, 55f, left: true),
                new(SegmentKind.Straight, backStraight * 1.05f),
                new(SegmentKind.Chicane, chicaneRLeft * 0.9f, chicaneRRight * 1.05f, 24f),
                new(SegmentKind.Straight, shortStraight * 0.6f),
                new(SegmentKind.Arc, hairpinRadius * 0.9f, 165f, left: true),
                new(SegmentKind.Straight, shortStraight),
                new(SegmentKind.Arc, sweeperRadius, 98f, left: true),
                new(SegmentKind.Straight, shortStraight * 0.55f),
                new(SegmentKind.Arc, sweeperRadius * 0.85f, 42f, left: true)
            },
            _ => new List<Segment>
            {
                new(SegmentKind.Straight, longStraight),
                new(SegmentKind.Arc, sweeperRadius, 62f, left: true),
                new(SegmentKind.Straight, backStraight),
                new(SegmentKind.Chicane, chicaneRLeft, chicaneRRight, 26f),
                new(SegmentKind.Straight, shortStraight * 0.5f),
                new(SegmentKind.Arc, hairpinRadius, 170f, left: true),
                new(SegmentKind.Straight, shortStraight),
                new(SegmentKind.Arc, sweeperRadius * 0.9f, 102f, left: true),
                new(SegmentKind.Straight, shortStraight * 0.55f),
                new(SegmentKind.Arc, sweeperRadius * 0.82f, 30f, left: true)
            }
        };
    }

    private static List<Vector2> BuildPolyline(List<Segment> segments, float step)
    {
        var points = new List<Vector2>(512) { Vector2.zero };
        var pos = Vector2.zero;
        var heading = Vector2.right;

        foreach (var seg in segments)
        {
            switch (seg.Kind)
            {
                case SegmentKind.Straight:
                    {
                        var count = Mathf.Max(2, Mathf.CeilToInt(seg.A / step));
                        for (var i = 1; i <= count; i++)
                        {
                            var p = pos + heading * (seg.A * (i / (float)count));
                            points.Add(p);
                        }

                        pos += heading * seg.A;
                        break;
                    }
                case SegmentKind.Arc:
                    {
                        AppendArc(points, ref pos, ref heading, seg.A, seg.B, seg.Left, step);
                        break;
                    }
                case SegmentKind.Chicane:
                    {
                        AppendArc(points, ref pos, ref heading, seg.A, seg.C, true, step);
                        AppendArc(points, ref pos, ref heading, seg.B, seg.C, false, step);
                        break;
                    }
            }
        }

        return points;
    }

    private static void AppendArc(List<Vector2> pts, ref Vector2 pos, ref Vector2 heading, float radius, float deg, bool left, float step)
    {
        var angleRad = deg * Mathf.Deg2Rad;
        var arcLength = Mathf.Abs(angleRad * radius);
        var count = Mathf.Max(4, Mathf.CeilToInt(arcLength / step));
        var sign = left ? 1f : -1f;
        var right = new Vector2(heading.y, -heading.x);
        var center = pos - right * sign * radius;

        var start = pos - center;
        for (var i = 1; i <= count; i++)
        {
            var t = i / (float)count;
            var a = sign * angleRad * t;
            var c = Mathf.Cos(a);
            var s = Mathf.Sin(a);
            var rotated = new Vector2(start.x * c - start.y * s, start.x * s + start.y * c);
            var p = center + rotated;
            pts.Add(p);
        }

        pos = pts[pts.Count - 1];
        var hA = sign * angleRad;
        var hc = Mathf.Cos(hA);
        var hs = Mathf.Sin(hA);
        heading = new Vector2(heading.x * hc - heading.y * hs, heading.x * hs + heading.y * hc).normalized;
    }

    private static void ClampInside(List<Vector2> points, float maxX, float maxY)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < points.Count; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        var center = (min + max) * 0.5f;
        var ext = (max - min) * 0.5f;
        var sx = ext.x > maxX ? maxX / Mathf.Max(ext.x, 0.001f) : 1f;
        var sy = ext.y > maxY ? maxY / Mathf.Max(ext.y, 0.001f) : 1f;
        var s = Mathf.Min(sx, sy);

        for (var i = 0; i < points.Count; i++)
        {
            points[i] = (points[i] - center) * s;
        }
    }

    private static Vector2[] ApplyChaikin(List<Vector2> input, int iterations)
    {
        var current = new List<Vector2>(input);
        for (var n = 0; n < iterations; n++)
        {
            var next = new List<Vector2>(current.Count * 2);
            for (var i = 0; i < current.Count - 1; i++)
            {
                var a = current[i];
                var b = current[i + 1];
                next.Add(a * 0.75f + b * 0.25f);
                next.Add(a * 0.25f + b * 0.75f);
            }

            next.Add(current[current.Count - 1] * 0.75f + current[0] * 0.25f);
            next.Add(current[current.Count - 1] * 0.25f + current[0] * 0.75f);
            current = next;
        }

        return current.ToArray();
    }

    private static Vector2[] ResampleEvenly(Vector2[] points, int samples)
    {
        var lengths = new float[points.Length + 1];
        var total = 0f;
        for (var i = 1; i <= points.Length; i++)
        {
            total += Vector2.Distance(points[i - 1], points[i % points.Length]);
            lengths[i] = total;
        }

        var result = new Vector2[samples];
        var step = total / samples;
        var seg = 1;
        for (var i = 0; i < samples; i++)
        {
            var d = i * step;
            while (seg < lengths.Length - 1 && lengths[seg] < d)
            {
                seg++;
            }

            var l0 = lengths[seg - 1];
            var l1 = lengths[seg];
            var t = Mathf.InverseLerp(l0, l1, d);
            var a = points[(seg - 1) % points.Length];
            var b = points[seg % points.Length];
            result[i] = Vector2.LerpUnclamped(a, b, t);
        }

        return result;
    }

    private static bool TryBuildTrack(Vector2[] center, float arenaHalfWidth, float arenaHalfHeight, out MarbleRaceTrack track)
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
            if (i > 0 && Vector2.Dot(t, tangent[i - 1]) < 0f)
            {
                t = -t;
            }

            tangent[i] = t;
            var norm = new Vector2(-t.y, t.x);
            if (i > 0 && Vector2.Dot(norm, normal[i - 1]) < 0f)
            {
                norm = -norm;
            }

            normal[i] = norm;
            curvature[i] = Vector2.Angle(tangent[(i - 1 + n) % n], t) / 180f;
            if (i > 0 && Vector2.Dot(tangent[i], tangent[i - 1]) < 0.2f)
            {
                track = null;
                return false;
            }
        }

        if (HasSelfIntersection(center, 16))
        {
            track = null;
            return false;
        }

        var baseHalf = Mathf.Clamp(Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.10f, 1.8f, 4.8f);
        var halfWidth = new float[n];
        var zones = new[]
        {
            (start: Mathf.RoundToInt(n * 0.12f), end: Mathf.RoundToInt(n * 0.23f)),
            (start: Mathf.RoundToInt(n * 0.56f), end: Mathf.RoundToInt(n * 0.70f))
        };

        for (var i = 0; i < n; i++)
        {
            var width = baseHalf;
            if (InZone(i, zones[0], n) || InZone(i, zones[1], n))
            {
                width *= 1.35f;
            }

            width *= Mathf.Lerp(1.0f, 0.78f, Mathf.Clamp01(curvature[i] * 3f));
            halfWidth[i] = Mathf.Clamp(width, baseHalf * 0.72f, baseHalf * 1.6f);
        }

        track = new MarbleRaceTrack(center, tangent, normal, halfWidth, curvature, zones);
        return true;
    }

    private static bool InZone(int i, (int start, int end) zone, int n)
    {
        i = ((i % n) + n) % n;
        var s = ((zone.start % n) + n) % n;
        var e = ((zone.end % n) + n) % n;
        return s <= e ? i >= s && i <= e : i >= s || i <= e;
    }

    private static bool HasSelfIntersection(Vector2[] center, int step)
    {
        for (var i = 0; i < center.Length; i += step)
        {
            var a0 = center[i];
            var a1 = center[(i + step) % center.Length];
            for (var j = i + step * 2; j < center.Length; j += step)
            {
                if (Mathf.Abs(i - j) < step * 2 || Mathf.Abs(i - j) > center.Length - step * 2)
                {
                    continue;
                }

                var b0 = center[j];
                var b1 = center[(j + step) % center.Length];
                if (SegmentsIntersect(a0, a1, b0, b1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        static float Cross(Vector2 a, Vector2 b) => (a.x * b.y) - (a.y * b.x);
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        if (Mathf.Abs(denom) < 0.0001f)
        {
            return false;
        }

        var u = Cross(q1 - p1, r) / denom;
        var t = Cross(q1 - p1, s) / denom;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight)
    {
        var w = arenaHalfWidth * 0.72f;
        var h = arenaHalfHeight * 0.62f;
        var r = Mathf.Min(w, h) * 0.25f;

        var poly = new List<Vector2>();
        AppendRoundedRect(poly, w, h, r, 18);
        var center = ResampleEvenly(poly.ToArray(), SampleCount);
        TryBuildTrack(center, arenaHalfWidth, arenaHalfHeight, out var track);
        return track;
    }

    private static void AppendRoundedRect(List<Vector2> points, float w, float h, float r, int arcSteps)
    {
        var corners = new[]
        {
            new Vector2(w - r, h - r),
            new Vector2(-w + r, h - r),
            new Vector2(-w + r, -h + r),
            new Vector2(w - r, -h + r)
        };

        for (var c = 0; c < 4; c++)
        {
            var start = 90f * c;
            for (var i = 0; i < arcSteps; i++)
            {
                var a = (start + (i / (float)(arcSteps - 1)) * 90f) * Mathf.Deg2Rad;
                points.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }
    }
}
