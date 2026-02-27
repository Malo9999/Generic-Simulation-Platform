using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 512;
    private const int TemplateCount = 9;
    private const int MaxAttempts = 12;

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
        public float Score;
        public int Attempts;
        public bool UsedFallback;
    }

    public MarbleRaceTrack Build(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var result = BuildInternal(arenaHalfWidth, arenaHalfHeight, rng, variant);
        Debug.Log($"[MarbleRaceTrackGenerator] bestTemplate={result.TemplateId} score={result.Score:F1} attempts={result.Attempts} fallback={(result.UsedFallback ? "yes" : "no")}");
        return result.Track;
    }

    private BuildResult BuildInternal(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var safeW = Mathf.Max(12f, arenaHalfWidth);
        var safeH = Mathf.Max(12f, arenaHalfHeight);
        var minArenaHalf = Mathf.Min(safeW, safeH);
        var baseStep = Mathf.Clamp(minArenaHalf * 0.04f, 0.45f, 1.2f);
        var fitMargin = minArenaHalf * 0.12f;

        MarbleRaceTrack bestTrack = null;
        var bestTemplateId = -1;
        var bestScore = float.MinValue;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var templateId = PositiveMod(variant + attempt, TemplateCount);
            var t = attempt / (float)Mathf.Max(1, MaxAttempts - 1);
            var step = baseStep * Mathf.Lerp(1.05f, 0.78f, t);

            var jitterA = NextJitter(rng, variant, attempt, 0.91f, 1.09f);
            var jitterB = NextJitter(rng, variant, attempt + 37, 0.92f, 1.08f);
            var lengthMul = jitterA;
            var radiusMul = jitterB;

            var points = BuildFromTemplate(templateId, safeW, safeH, step, lengthMul, radiusMul);
            if (!FitToBounds(points, safeW, safeH, fitMargin))
            {
                continue;
            }

            var center = ResampleArcLength(points, TargetSamples);
            var candidate = BuildTrackData(center, safeW, safeH);
            if (!ValidateStrict(candidate, safeW, safeH))
            {
                continue;
            }

            var score = ScoreTrack(candidate, safeW, safeH);
            if (score > bestScore)
            {
                bestScore = score;
                bestTrack = candidate;
                bestTemplateId = templateId;
            }
        }

        if (bestTrack != null)
        {
            return new BuildResult
            {
                Track = bestTrack,
                TemplateId = bestTemplateId,
                Score = bestScore,
                Attempts = MaxAttempts,
                UsedFallback = false
            };
        }

        var fallbackTemplate = PositiveMod(variant, 2) == 0 ? 1 : 4;
        var fallback = BuildFallbackTrack(safeW, safeH, fallbackTemplate, baseStep, fitMargin);
        return new BuildResult
        {
            Track = fallback,
            TemplateId = fallbackTemplate,
            Score = ScoreTrack(fallback, safeW, safeH),
            Attempts = MaxAttempts,
            UsedFallback = true
        };
    }

    public MarbleRaceTrack BuildFallbackRoundedRectangle(float arenaHalfWidth, float arenaHalfHeight)
    {
        var safeW = Mathf.Max(12f, arenaHalfWidth);
        var safeH = Mathf.Max(12f, arenaHalfHeight);
        var minArenaHalf = Mathf.Min(safeW, safeH);
        var step = Mathf.Clamp(minArenaHalf * 0.03f, 0.4f, 1f);
        var fitMargin = minArenaHalf * 0.12f;
        return BuildFallbackTrack(safeW, safeH, 1, step, fitMargin);
    }

    private MarbleRaceTrack BuildFallbackTrack(float safeW, float safeH, int templateId, float step, float fitMargin)
    {
        var points = BuildFromTemplate(templateId, safeW, safeH, step, 0.96f, 0.96f);
        if (!FitToBounds(points, safeW, safeH, fitMargin))
        {
            points = BuildFromTemplate(1, safeW, safeH, step, 0.94f, 0.94f);
            FitToBounds(points, safeW, safeH, fitMargin);
        }

        var center = ResampleArcLength(points, TargetSamples);
        return BuildTrackData(center, safeW, safeH);
    }

    private static List<Vector2> BuildFromTemplate(int templateId, float halfW, float halfH, float step, float lengthMul, float radiusMul)
    {
        var min = Mathf.Min(halfW, halfH);
        var longStraight = Mathf.Min(halfW * 1.25f, halfW + 16f) * lengthMul;
        var mediumStraight = Mathf.Min(halfW * 0.95f, halfW + 8f) * lengthMul;
        var shortStraight = Mathf.Clamp(min * 0.6f, 6f, 20f) * lengthMul;
        var overtakeStraight = Mathf.Clamp(min * 0.86f, 9f, 30f) * lengthMul;
        var megaStraight = Mathf.Clamp(min * 1.12f, 12f, 38f) * lengthMul;
        var sweepR = Mathf.Clamp(min * 0.62f, 6f, 24f) * radiusMul;
        var mediumR = Mathf.Clamp(min * 0.48f, 5f, 18f) * radiusMul;
        var hairpinR = Mathf.Clamp(min * 0.28f, 4f, 12f) * radiusMul;

        List<Segment> segments;
        switch (templateId)
        {
            case 0: // GP style
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
            case 1: // Rounded rectangle + chicane + hairpin
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.15f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.86f, 86f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 1.05f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.64f, mediumR * 0.7f, 28f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.92f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.04f, 176f, 0f, true),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.84f, 96f, 0f, true)
                };
                break;
            case 2: // Kidney + S bend
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
            case 3: // Double-straight + two sweepers
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, megaStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.92f, 105f, 0f, false),
                    new Segment(SegmentKind.Straight, longStraight * 0.96f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.82f, 98f, 0f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.7f, 72f, 0f, false),
                    new Segment(SegmentKind.Straight, shortStraight * 0.75f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.8f, 70f, 0f, true)
                };
                break;
            case 4: // Stadium
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, megaStraight * 1.05f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 1.1f, 178f, 0f, false),
                    new Segment(SegmentKind.Straight, megaStraight * 1.02f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 1.08f, 178f, 0f, false)
                };
                break;
            case 5: // Long back straight + tight hairpin + fast esses
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, megaStraight * 1.12f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.03f, 176f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.95f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.56f, mediumR * 0.6f, 22f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 1.04f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.72f, 78f, 0f, false),
                    new Segment(SegmentKind.Straight, shortStraight * 0.82f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.82f, 72f, 0f, true)
                };
                break;
            case 6: // Two chicanes separated by straights
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.64f, mediumR * 0.72f, 28f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.9f, 92f, 0f, false),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.85f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.6f, mediumR * 0.68f, 26f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.92f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.8f, 94f, 0f, true)
                };
                break;
            case 7: // Short technical section + mega straight
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, shortStraight * 0.72f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.72f, 66f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 0.68f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.66f, 72f, 0f, false),
                    new Segment(SegmentKind.Straight, megaStraight * 1.25f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 0.98f, 170f, 0f, false),
                    new Segment(SegmentKind.Straight, mediumStraight, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.86f, 102f, 0f, true)
                };
                break;
            default: // 8 Asymmetric GP
                segments = new List<Segment>
                {
                    new Segment(SegmentKind.Straight, longStraight * 1.08f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.86f, 88f, 0f, false),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.98f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, mediumR * 0.78f, 62f, 0f, true),
                    new Segment(SegmentKind.Straight, shortStraight * 1.08f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, hairpinR * 1.02f, 164f, 0f, false),
                    new Segment(SegmentKind.Straight, mediumStraight * 0.88f, 0f, 0f, true),
                    new Segment(SegmentKind.Chicane, mediumR * 0.58f, mediumR * 0.64f, 24f, true),
                    new Segment(SegmentKind.Straight, overtakeStraight * 0.9f, 0f, 0f, true),
                    new Segment(SegmentKind.Arc, sweepR * 0.8f, 90f, 0f, true)
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

    private static bool FitToBounds(List<Vector2> points, float halfW, float halfH, float margin)
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
        var startIndex = FindBestStraightStart(curvature);
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
        var overtakeAEnd = Mathf.RoundToInt(n * 0.23f);
        var overtakeBStart = Mathf.RoundToInt(n * 0.56f);
        var overtakeBEnd = Mathf.RoundToInt(n * 0.71f);

        for (var i = 0; i < n; i++)
        {
            var width = baseHalfWidth;
            if (InRangeWrapped(i, overtakeAStart, overtakeAEnd, n) || InRangeWrapped(i, overtakeBStart, overtakeBEnd, n))
            {
                var overtakeMul = Mathf.Lerp(1.35f, 1.55f, 0.5f + (0.5f * Mathf.Sin(i * 0.07f)));
                width = Mathf.Min(baseHalfWidth * overtakeMul, baseHalfWidth * 1.6f);
            }

            var cornerTightening = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(curvature[i] * 3f));
            width *= cornerTightening;
            widths[i] = Mathf.Clamp(width, baseHalfWidth * 0.82f, baseHalfWidth * 1.6f);
        }

        return widths;
    }

    private static int FindBestStraightStart(float[] curvature)
    {
        var n = curvature.Length;
        var bestStart = -1;
        var bestLength = 0;
        var runStart = -1;

        for (var i = 0; i < n * 2; i++)
        {
            var idx = i % n;
            if (curvature[idx] < 0.04f)
            {
                if (runStart < 0)
                {
                    runStart = i;
                }

                var runLength = i - runStart + 1;
                if (runLength > bestLength)
                {
                    bestLength = runLength;
                    bestStart = runStart;
                }
            }
            else
            {
                runStart = -1;
            }
        }

        if (bestLength < 24 || bestStart < 0)
        {
            return 0;
        }

        return (bestStart + (bestLength / 2)) % n;
    }

    private static bool ValidateStrict(MarbleRaceTrack track, float halfW, float halfH)
    {
        if (track == null || track.Center == null || track.Tangent == null || track.Normal == null || track.HalfWidth == null || track.Curvature == null || track.Center.Length < 16)
        {
            return false;
        }

        var n = track.Center.Length;
        var maxHalfWidth = 0f;
        var widthSum = 0f;
        for (var i = 0; i < n; i++)
        {
            maxHalfWidth = Mathf.Max(maxHalfWidth, track.HalfWidth[i]);
            widthSum += track.HalfWidth[i];
        }

        var requiredMargin = maxHalfWidth + (Mathf.Min(halfW, halfH) * 0.06f);
        for (var i = 0; i < n; i++)
        {
            var c = track.Center[i];
            if (Mathf.Abs(c.x) > halfW - requiredMargin || Mathf.Abs(c.y) > halfH - requiredMargin)
            {
                return false;
            }
        }

        for (var i = 1; i < n; i++)
        {
            if (Vector2.Dot(track.Tangent[i - 1], track.Tangent[i]) < 0.35f)
            {
                return false;
            }
        }

        if (Vector2.Dot(track.Tangent[n - 1], track.Tangent[0]) < 0.35f)
        {
            return false;
        }

        if (HasSelfIntersection(track.Center, 8))
        {
            return false;
        }

        var left = new Vector2[n];
        var right = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            left[i] = track.Center[i] + (track.Normal[i] * track.HalfWidth[i]);
            right[i] = track.Center[i] - (track.Normal[i] * track.HalfWidth[i]);
        }

        if (HasSelfIntersection(left, 10) || HasSelfIntersection(right, 10))
        {
            return false;
        }

        if (HasIntersectionsBetween(left, right, 10, n))
        {
            return false;
        }

        var avgWidth = widthSum / n;
        if (!HasMinimumSpacing(track.Center, avgWidth * 0.9f, 12))
        {
            return false;
        }

        return true;
    }

    private static float ScoreTrack(MarbleRaceTrack track, float halfW, float halfH)
    {
        if (track == null || track.Center == null || track.Center.Length < 8)
        {
            return float.MinValue;
        }

        var n = track.Center.Length;
        var center = track.Center;
        var curvature = track.Curvature;
        var widths = track.HalfWidth;

        var score = 0f;
        var totalArcLength = 0f;
        for (var i = 0; i < n; i++)
        {
            totalArcLength += Vector2.Distance(center[i], center[(i + 1) % n]);
        }

        score += totalArcLength * 0.1f;

        var straightCount = 0;
        var straightRunsOverMin = 0;
        var currentStraightRun = 0;
        var hairpinRun = 0;
        var foundHairpin = false;
        var signAlternations = 0;
        var previousSign = 0f;
        var foundOvertakeZone = false;
        var overtakeRun = 0;

        var widthMean = 0f;
        for (var i = 0; i < n; i++)
        {
            widthMean += widths[i];
        }

        widthMean /= n;

        for (var i = 0; i < n; i++)
        {
            var curv = curvature[i];
            if (curv < 0.04f)
            {
                straightCount++;
                currentStraightRun++;
                if (widths[i] > widthMean * 1.25f)
                {
                    overtakeRun++;
                }
                else
                {
                    overtakeRun = 0;
                }
            }
            else
            {
                if (currentStraightRun >= 60)
                {
                    straightRunsOverMin++;
                }

                currentStraightRun = 0;
                overtakeRun = 0;
            }

            if (overtakeRun >= 50)
            {
                foundOvertakeZone = true;
            }

            if (curv > 0.14f)
            {
                hairpinRun++;
                if (hairpinRun >= 12)
                {
                    foundHairpin = true;
                }
            }
            else
            {
                hairpinRun = 0;
            }

            var next = track.Tangent[(i + 1) % n];
            var cross = Cross(track.Tangent[i], next);
            var sign = Mathf.Abs(cross) < 0.0001f ? 0f : Mathf.Sign(cross);
            if (previousSign != 0f && sign != 0f && sign != previousSign)
            {
                signAlternations++;
            }

            if (sign != 0f)
            {
                previousSign = sign;
            }
        }

        if (currentStraightRun >= 60)
        {
            straightRunsOverMin++;
        }

        score += straightCount * 0.05f;
        if (straightRunsOverMin >= 2)
        {
            score += 20f;
        }

        if (foundHairpin)
        {
            score += 14f;
        }

        if (signAlternations > 0)
        {
            score += 12f;
        }

        if (foundOvertakeZone)
        {
            score += 16f;
        }

        var curvatureMean = 0f;
        var curvatureMax = 0f;
        for (var i = 0; i < n; i++)
        {
            curvatureMean += curvature[i];
            curvatureMax = Mathf.Max(curvatureMax, curvature[i]);
        }

        curvatureMean /= n;
        var curvatureVariance = 0f;
        for (var i = 0; i < n; i++)
        {
            var d = curvature[i] - curvatureMean;
            curvatureVariance += d * d;
        }

        curvatureVariance /= n;
        if (curvatureVariance < 0.00011f)
        {
            score -= 18f;
        }

        if (curvatureMax > 0.22f)
        {
            score -= (curvatureMax - 0.22f) * 220f;
        }

        var widthVariance = 0f;
        for (var i = 0; i < n; i++)
        {
            var d = widths[i] - widthMean;
            widthVariance += d * d;
        }

        widthVariance /= n;
        var widthStd = Mathf.Sqrt(widthVariance);
        var widthRatio = widthMean > 0.0001f ? widthStd / widthMean : 0f;
        if (widthRatio > 0.23f)
        {
            score -= (widthRatio - 0.23f) * 95f;
        }

        var minClearance = float.MaxValue;
        var marginFloor = Mathf.Min(halfW, halfH) * 0.08f;
        for (var i = 0; i < n; i++)
        {
            var p = center[i];
            var clearanceX = halfW - Mathf.Abs(p.x) - widths[i];
            var clearanceY = halfH - Mathf.Abs(p.y) - widths[i];
            minClearance = Mathf.Min(minClearance, Mathf.Min(clearanceX, clearanceY));
        }

        if (minClearance < marginFloor)
        {
            score -= (marginFloor - minClearance) * 30f;
        }

        return score;
    }

    private static bool HasMinimumSpacing(Vector2[] center, float minDistance, int neighborGap)
    {
        var n = center.Length;
        var minDistSq = minDistance * minDistance;
        for (var i = 0; i < n; i += 3)
        {
            for (var j = i + neighborGap; j < n; j += 3)
            {
                var cyclic = Mathf.Abs(i - j);
                if (cyclic < neighborGap || cyclic > n - neighborGap)
                {
                    continue;
                }

                if ((center[i] - center[j]).sqrMagnitude < minDistSq)
                {
                    return false;
                }
            }
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

    private static int PositiveMod(int value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        var mod = value % divisor;
        return mod < 0 ? mod + divisor : mod;
    }

    private static float NextJitter(IRng rng, int variant, int attempt, float min, float max)
    {
        if (rng != null)
        {
            return Mathf.Lerp(min, max, rng.NextFloat01());
        }

        var seed = Mathf.Abs((variant * 17) + (attempt * 31));
        var unit = 0.5f + (0.5f * Mathf.Sin(seed * 0.37f));
        return Mathf.Lerp(min, max, unit);
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
}
