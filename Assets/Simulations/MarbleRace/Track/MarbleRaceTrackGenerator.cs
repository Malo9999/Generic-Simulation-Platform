using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackGenerator
{
    private const int TargetSamples = 512;
    private const int TemplateCount = 10;

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
        var v = Mathf.Abs(variant) % TemplateCount;

        fallback = true;
        MarbleRaceTrack track = null;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var attemptSeed = StableMix(sourceSeed, variant, v, attempt);
            var attemptRng = new SeededRng(attemptSeed);
            var raw = BuildPolyline(safeHalfW, safeHalfH, attemptRng, v);
            if (raw == null || raw.Count < 12)
            {
                continue;
            }

            CloseLoopByDrift(raw);
            ClampInside(raw, safeHalfW - 3f, safeHalfH - 3f);
            var smooth = ApplyChaikin(raw, 1);
            var center = ResampleArcLengthClosed(smooth, TargetSamples);
            if (center == null)
            {
                continue;
            }

            if (TryBuildTrack(center, minDim, out track))
            {
                fallback = false;
                break;
            }
        }

        if (fallback)
        {
            Debug.LogWarning($"[TrackGen] FALLBACK variant={variant} template={v}");
            track = BuildFallbackRoundedRectangle(safeHalfW, safeHalfH, variant);
        }
        else
        {
            Debug.Log($"[TrackGen] OK variant={variant} template={v}");
        }

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

    private static List<Vector2> BuildPolyline(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int template)
    {
        var points = new List<Vector2>(256) { Vector2.zero };
        var pos = Vector2.zero;
        var heading = Vector2.right;
        var localRng = rng ?? new SeededRng(template);
        var radiusScale = Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.115f * localRng.Range(0.92f, 1.08f);
        var straightScale = Mathf.Min(arenaHalfWidth, arenaHalfHeight) * 0.34f * localRng.Range(0.9f, 1.1f);

        switch (template)
        {
            case 0:
                DoStraight(ref pos, heading, straightScale * 0.9f, points);
                DoArc(ref pos, ref heading, radiusScale, 45f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 35f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 40f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 55f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 42f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.6f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 60f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale, 38f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 1.15f, 48f, true, points);
                break;
            case 1:
                DoStraight(ref pos, heading, straightScale * 0.7f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 50f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.75f, 32f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.75f, 36f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale, 70f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 40f, false, points);
                DoStraight(ref pos, heading, straightScale * 0.3f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 44f, true, points);
                DoArc(ref pos, ref heading, radiusScale * 1.2f, 66f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale, 28f, false, points);
                DoArc(ref pos, ref heading, radiusScale, 34f, true, points);
                break;
            case 2:
                DoStraight(ref pos, heading, straightScale * 0.8f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 42f, true, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 35f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 38f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.6f, points);
                DoArc(ref pos, ref heading, radiusScale, 62f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.8f, 30f, false, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 58f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale, 33f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 45f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                break;
            case 3:
                DoStraight(ref pos, heading, straightScale * 0.75f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 48f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 28f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.15f, 72f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.65f, points);
                DoArc(ref pos, ref heading, radiusScale, 40f, false, points);
                DoArc(ref pos, ref heading, radiusScale, 40f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 64f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 36f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 36f, true, points);
                break;
            case 4:
                DoStraight(ref pos, heading, straightScale * 0.95f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.2f, 52f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.7f, 34f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.7f, 34f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 60f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.65f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 37f, false, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 46f, true, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 50f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale, 30f, true, points);
                break;
            case 5:
                DoStraight(ref pos, heading, straightScale * 0.85f, points);
                DoArc(ref pos, ref heading, radiusScale, 44f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.8f, 26f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.8f, 34f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 68f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 42f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 46f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.15f, 62f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 40f, true, points);
                break;
            case 6:
                DoStraight(ref pos, heading, straightScale * 0.8f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 50f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.8f, 32f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 32f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.6f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.2f, 70f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 44f, false, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale, 44f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.15f, 55f, true, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 30f, true, points);
                break;
            case 7:
                DoStraight(ref pos, heading, straightScale * 0.75f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 42f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.75f, 28f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.75f, 31f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 66f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.6f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 45f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 36f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.2f, 58f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 34f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 38f, true, points);
                break;
            case 8:
                DoStraight(ref pos, heading, straightScale * 0.88f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.05f, 47f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.35f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 41f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.15f, 62f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.65f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 39f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 36f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 52f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.3f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 33f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 44f, true, points);
                break;
            default:
                DoStraight(ref pos, heading, straightScale * 0.85f, points);
                DoArc(ref pos, ref heading, radiusScale, 43f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.85f, 28f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 36f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.55f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.2f, 65f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.5f, points);
                DoArc(ref pos, ref heading, radiusScale, 43f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 37f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.45f, points);
                DoArc(ref pos, ref heading, radiusScale * 1.1f, 54f, true, points);
                DoStraight(ref pos, heading, straightScale * 0.4f, points);
                DoArc(ref pos, ref heading, radiusScale * 0.9f, 30f, false, points);
                DoArc(ref pos, ref heading, radiusScale * 0.95f, 40f, true, points);
                break;
        }

        return points;
    }

    private static void DoStraight(ref Vector2 pos, Vector2 heading, float distance, List<Vector2> outPoints)
    {
        var samples = Mathf.Max(2, Mathf.CeilToInt(distance / 2.8f));
        for (var i = 1; i <= samples; i++)
        {
            var p = pos + (heading * distance * (i / (float)samples));
            AddUnique(outPoints, p);
        }

        pos += heading * distance;
    }

    private static void DoArc(ref Vector2 pos, ref Vector2 heading, float radius, float angleDeg, bool left, List<Vector2> outPoints)
    {
        var side = left ? new Vector2(-heading.y, heading.x) : new Vector2(heading.y, -heading.x);
        var center = pos + (side * radius);
        var start = pos - center;
        var signed = left ? angleDeg : -angleDeg;
        var steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(angleDeg) / 8f));

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var rot = Quaternion.Euler(0f, 0f, signed * t);
            var p = center + (Vector2)(rot * start);
            AddUnique(outPoints, p);
        }

        var endRot = Quaternion.Euler(0f, 0f, signed);
        pos = center + (Vector2)(endRot * start);
        heading = ((Vector2)(endRot * heading)).normalized;
    }

    private static void CloseLoopByDrift(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 3) return;
        var start = pts[0];
        var end = pts[pts.Count - 1];
        var delta = end - start;
        if (delta.sqrMagnitude < 0.0001f)
        {
            pts[pts.Count - 1] = start;
            return;
        }

        var last = pts.Count - 1;
        for (var i = 0; i <= last; i++)
        {
            var t = i / (float)last;
            pts[i] -= delta * t;
        }

        pts[last] = start;
    }

    private static void ClampInside(List<Vector2> pts, float halfW, float halfH)
    {
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            pts[i] = new Vector2(Mathf.Clamp(p.x, -halfW, halfW), Mathf.Clamp(p.y, -halfH, halfH));
        }
    }

    private static List<Vector2> ApplyChaikin(List<Vector2> points, int passes)
    {
        if (points == null)
        {
            return null;
        }

        var outPts = new List<Vector2>(points);
        if (passes <= 0 || outPts.Count < 4)
        {
            return outPts;
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
                var q = Vector2.Lerp(p0, p1, 0.25f);
                var r = Vector2.Lerp(p0, p1, 0.75f);
                outPts.Add(q);
                outPts.Add(r);
            }
        }

        return outPts;
    }

    private static Vector2[] ResampleArcLengthClosed(List<Vector2> points, int target)
    {
        if (points == null || points.Count < 3)
        {
            return null;
        }

        var n = points.Count;
        var lengths = new float[n + 1];
        var total = 0f;
        lengths[0] = 0f;
        for (var i = 0; i < n; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % n];
            total += Vector2.Distance(a, b);
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

            var segStart = points[seg];
            var segEnd = points[(seg + 1) % n];
            var segLen = Mathf.Max(1e-6f, lengths[seg + 1] - lengths[seg]);
            var t = Mathf.Clamp01((d - lengths[seg]) / segLen);
            outPts[i] = Vector2.Lerp(segStart, segEnd, t);
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

        if (HasSelfIntersections(center, 16, 4))
        {
            return false;
        }

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

        var cornerCount = 0;
        var inCorner = false;
        var lastTurnSign = 0;
        var signChanges = 0;

        for (var i = 0; i < n; i++)
        {
            var prev = tangent[(i - 1 + n) % n];
            curvature[i] = Vector2.Angle(prev, tangent[i]) / 180f;

            var isCorner = curvature[i] > 0.06f;
            if (isCorner && !inCorner)
            {
                cornerCount++;
            }

            inCorner = isCorner;

            var cross = Cross(prev, tangent[i]);
            var sign = cross > 0.0005f ? 1 : (cross < -0.0005f ? -1 : 0);
            if (sign != 0)
            {
                if (lastTurnSign != 0 && sign != lastTurnSign)
                {
                    signChanges++;
                }

                lastTurnSign = sign;
            }
        }

        if (cornerCount < 8 || signChanges < 2)
        {
            return false;
        }

        var baseHalfWidth = Mathf.Clamp(minDim * 0.035f, 0.9f, 2.2f);
        var halfWidth = BuildWidths(curvature, tangent, baseHalfWidth);
        track = new MarbleRaceTrack(center, tangent, normal, halfWidth, curvature, new sbyte[n]);
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

    private static void AddUnique(List<Vector2> pts, Vector2 p)
    {
        if (pts.Count == 0 || (pts[pts.Count - 1] - p).sqrMagnitude > 0.0001f)
        {
            pts.Add(p);
        }
    }
}
