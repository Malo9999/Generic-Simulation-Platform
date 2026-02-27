using System.Collections.Generic;
using UnityEngine;

public static class MarbleRaceTrackValidator
{
    private const float MarbleRadius = 0.55f;

    public readonly struct Result
    {
        public readonly bool Passed;
        public readonly IReadOnlyList<string> Reasons;

        public Result(bool passed, List<string> reasons)
        {
            Passed = passed;
            Reasons = reasons;
        }
    }

    public static Result Validate(MarbleRaceTrack track, int marbleCount, Transform renderedTrackRoot = null)
    {
        var reasons = new List<string>();
        if (track == null || track.SampleCount < 8)
        {
            reasons.Add("Track data missing or too short.");
            return new Result(false, reasons);
        }

        ValidateStartFinishOnRoad(track, reasons);
        ValidateSpawnFitsWidth(track, marbleCount, reasons);
        ValidateSelfIntersections(track, reasons);
        ValidateColliders(track, renderedTrackRoot, reasons);

        var passed = reasons.Count == 0;

        if (passed)
        {
            reasons.Add("All validation checks passed.");
        }

        return new Result(passed, reasons);
    }

    private static void ValidateStartFinishOnRoad(MarbleRaceTrack track, List<string> reasons)
    {
        var startHalfWidth = track.HalfWidth[0];
        if (startHalfWidth <= MarbleRadius)
        {
            reasons.Add($"Start width too small for marbles (halfWidth={startHalfWidth:F2}).");
        }

        var left = track.Center[0] + (track.Normal[0] * startHalfWidth);
        var right = track.Center[0] - (track.Normal[0] * startHalfWidth);
        var mid = (left + right) * 0.5f;
        var dist = Vector2.Distance(mid, track.Center[0]);
        if (dist > 0.01f)
        {
            reasons.Add("Start/finish marker is not centered on road.");
        }
    }

    private static void ValidateSpawnFitsWidth(MarbleRaceTrack track, int marbleCount, List<string> reasons)
    {
        var safeCount = Mathf.Max(2, marbleCount);
        var maxLane = Mathf.Max(0f, track.HalfWidth[0] - MarbleRadius - 0.25f);
        var cols = Mathf.Min(4, safeCount);
        var minLaneSpacing = MarbleRadius * 2.1f;
        while (cols > 1)
        {
            var laneSpacing = (2f * maxLane) / (cols - 1);
            if (laneSpacing >= minLaneSpacing)
            {
                break;
            }

            cols--;
        }

        for (var i = 0; i < cols; i++)
        {
            var lane = cols == 1 ? 0f : Mathf.Lerp(-maxLane, maxLane, (i + 0.5f) / cols);
            if (Mathf.Abs(lane) + MarbleRadius > track.HalfWidth[0])
            {
                reasons.Add($"Spawn lane {i} overflows road width at start.");
                return;
            }
        }
    }

    private static void ValidateSelfIntersections(MarbleRaceTrack track, List<string> reasons)
    {
        var n = track.SampleCount;
        for (var i = 0; i < n; i++)
        {
            var a1 = track.Center[i];
            var a2 = track.Center[(i + 1) % n];

            for (var j = i + 2; j < n; j++)
            {
                if (Mathf.Abs(i - j) <= 1 || (i == 0 && j == n - 1))
                {
                    continue;
                }

                var b1 = track.Center[j];
                var b2 = track.Center[(j + 1) % n];
                if (!SegmentsIntersect(a1, a2, b1, b2))
                {
                    continue;
                }

                var aLayer = track.GetLayer(i);
                var bLayer = track.GetLayer(j);
                if (aLayer == bLayer)
                {
                    reasons.Add($"Self-intersection at segments {i}-{i + 1} and {j}-{j + 1} without crossover declaration.");
                    return;
                }
            }
        }
    }

    private static void ValidateColliders(MarbleRaceTrack track, Transform renderedTrackRoot, List<string> reasons)
    {
        if (renderedTrackRoot == null)
        {
            reasons.Add("Track colliders missing: no rendered track root provided.");
            return;
        }

        var inner = renderedTrackRoot.Find("TrackBoundaryInnerCollider")?.GetComponent<EdgeCollider2D>();
        var outer = renderedTrackRoot.Find("TrackBoundaryOuterCollider")?.GetComponent<EdgeCollider2D>();
        if (inner == null || outer == null)
        {
            reasons.Add("Track colliders missing: expected inner and outer edge colliders.");
            return;
        }

        ValidateColliderMatchesBoundary(track, inner, 1f, "inner", reasons);
        ValidateColliderMatchesBoundary(track, outer, -1f, "outer", reasons);
    }

    private static void ValidateColliderMatchesBoundary(MarbleRaceTrack track, EdgeCollider2D collider, float side, string label, List<string> reasons)
    {
        if (collider.points == null || collider.pointCount < track.SampleCount / 2)
        {
            reasons.Add($"{label} collider has too few points ({collider.pointCount}).");
            return;
        }

        var expected = track.SampleCount + 1;
        if (Mathf.Abs(collider.pointCount - expected) > 2)
        {
            reasons.Add($"{label} collider point count mismatch (got {collider.pointCount}, expected about {expected}).");
            return;
        }

        var compareCount = Mathf.Min(track.SampleCount, collider.pointCount - 1);
        var maxDelta = 0f;
        for (var i = 0; i < compareCount; i++)
        {
            var boundary = track.Center[i] + (track.Normal[i] * track.HalfWidth[i] * side);
            maxDelta = Mathf.Max(maxDelta, Vector2.Distance(boundary, collider.points[i]));
        }

        if (maxDelta > 0.1f)
        {
            reasons.Add($"{label} collider diverges from road boundary (max delta {maxDelta:F3}).");
        }
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
}
