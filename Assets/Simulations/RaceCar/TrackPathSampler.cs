using System;
using UnityEngine;

public sealed class TrackPathSampler
{
    private readonly Vector2[] points;
    private readonly float[] cumulativeDistances;
    private readonly float totalLength;

    public float TotalLength => totalLength;

    public TrackPathSampler(Vector2[] polyline)
    {
        if (polyline == null || polyline.Length < 2)
        {
            throw new ArgumentException("TrackPathSampler requires at least 2 points.", nameof(polyline));
        }

        points = polyline;
        cumulativeDistances = new float[polyline.Length];

        var accumulated = 0f;
        cumulativeDistances[0] = 0f;

        for (var i = 1; i < polyline.Length; i++)
        {
            accumulated += Vector2.Distance(polyline[i - 1], polyline[i]);
            cumulativeDistances[i] = accumulated;
        }

        totalLength = Mathf.Max(accumulated, 0.0001f);
    }

    public (Vector2 pos, Vector2 tangent) SampleByDistance(float s)
    {
        var wrappedS = WrapDistance(s);

        for (var i = 1; i < points.Length; i++)
        {
            if (wrappedS > cumulativeDistances[i])
            {
                continue;
            }

            var segStart = points[i - 1];
            var segEnd = points[i];
            var seg = segEnd - segStart;
            var segLen = seg.magnitude;
            if (segLen <= 0.0001f)
            {
                continue;
            }

            var segS = wrappedS - cumulativeDistances[i - 1];
            var t = Mathf.Clamp01(segS / segLen);
            var pos = Vector2.Lerp(segStart, segEnd, t);
            var tangent = seg / segLen;
            return (pos, tangent);
        }

        var fallbackStart = points[Mathf.Max(0, points.Length - 2)];
        var fallbackEnd = points[points.Length - 1];
        var fallbackSeg = fallbackEnd - fallbackStart;
        var fallbackTangent = fallbackSeg.sqrMagnitude > 0.0001f ? fallbackSeg.normalized : Vector2.right;
        return (fallbackEnd, fallbackTangent);
    }

    public float FindNearestDistance(Vector2 worldPos)
    {
        var bestSq = float.MaxValue;
        var bestS = 0f;

        for (var i = 1; i < points.Length; i++)
        {
            var segStart = points[i - 1];
            var segEnd = points[i];
            var seg = segEnd - segStart;
            var segLenSq = seg.sqrMagnitude;
            if (segLenSq <= 0.000001f)
            {
                continue;
            }

            var t = Mathf.Clamp01(Vector2.Dot(worldPos - segStart, seg) / segLenSq);
            var closest = segStart + seg * t;
            var distSq = (worldPos - closest).sqrMagnitude;

            if (distSq < bestSq)
            {
                bestSq = distSq;
                var segLen = Mathf.Sqrt(segLenSq);
                bestS = cumulativeDistances[i - 1] + (segLen * t);
            }
        }

        return WrapDistance(bestS);
    }

    private float WrapDistance(float s)
    {
        if (totalLength <= 0.0001f)
        {
            return 0f;
        }

        var wrapped = s % totalLength;
        if (wrapped < 0f)
        {
            wrapped += totalLength;
        }

        return wrapped;
    }
}
