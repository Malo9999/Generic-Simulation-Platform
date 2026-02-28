using System.Collections.Generic;
using UnityEngine;

public static class MarbleRaceTrackValidator
{
    private const float MarbleRadius = 0.55f;
    private const int QualityPassScore = 70;

    public enum QualityBand
    {
        Red,
        Yellow,
        Green
    }

    public readonly struct QualityReport
    {
        public readonly int Score;
        public readonly float MaxTurnAngleDeg;
        public readonly int SharpCornerCount;
        public readonly float MinRadius;
        public readonly float AxisAlignedRatio;
        public readonly float SmoothnessJitter;
        public readonly float DirectionEntropy;
        public readonly int UniqueDirectionBins;
        public readonly bool BoxyFail;
        public readonly IReadOnlyList<string> Issues;
        public readonly IReadOnlyList<int> SharpCornerIndices;
        public readonly IReadOnlyList<int> AxisAlignedSegmentIndices;
        public readonly IReadOnlyList<int> MinRadiusIndices;

        public QualityReport(
            int score,
            float maxTurnAngleDeg,
            int sharpCornerCount,
            float minRadius,
            float axisAlignedRatio,
            float smoothnessJitter,
            float directionEntropy,
            int uniqueDirectionBins,
            bool boxyFail,
            List<string> issues,
            List<int> sharpCornerIndices,
            List<int> axisAlignedSegmentIndices,
            List<int> minRadiusIndices)
        {
            Score = score;
            MaxTurnAngleDeg = maxTurnAngleDeg;
            SharpCornerCount = sharpCornerCount;
            MinRadius = minRadius;
            AxisAlignedRatio = axisAlignedRatio;
            SmoothnessJitter = smoothnessJitter;
            DirectionEntropy = directionEntropy;
            UniqueDirectionBins = uniqueDirectionBins;
            BoxyFail = boxyFail;
            Issues = issues;
            SharpCornerIndices = sharpCornerIndices;
            AxisAlignedSegmentIndices = axisAlignedSegmentIndices;
            MinRadiusIndices = minRadiusIndices;
        }
    }

    public readonly struct Result
    {
        public readonly bool Passed;
        public readonly bool ValidityPassed;
        public readonly int QualityScore;
        public readonly QualityBand Band;
        public readonly IReadOnlyList<string> ValidityReasons;
        public readonly IReadOnlyList<string> QualityIssues;
        public readonly IReadOnlyList<string> Reasons;
        public readonly QualityReport Quality;

        public Result(bool passed, bool validityPassed, int qualityScore, QualityBand band, List<string> validityReasons, List<string> qualityIssues, List<string> reasons, QualityReport quality)
        {
            Passed = passed;
            ValidityPassed = validityPassed;
            QualityScore = qualityScore;
            Band = band;
            ValidityReasons = validityReasons;
            QualityIssues = qualityIssues;
            Reasons = reasons;
            Quality = quality;
        }
    }

    public static Result Validate(MarbleRaceTrack track, int marbleCount, Transform renderedTrackRoot = null)
    {
        var validityReasons = new List<string>();
        if (track == null || track.SampleCount < 8)
        {
            validityReasons.Add("Track data missing or too short.");
            var missingQuality = new QualityReport(0, 0f, 0, 0f, 1f, 1f, 0f, 0, true, new List<string> { "Quality checks skipped: invalid track." }, new List<int>(), new List<int>(), new List<int>());
            return new Result(false, false, 0, QualityBand.Red, validityReasons, new List<string>(missingQuality.Issues), new List<string>(validityReasons), missingQuality);
        }

        ValidateStartFinishOnRoad(track, validityReasons);
        ValidateSpawnFitsWidth(track, marbleCount, validityReasons);
        ValidateSelfIntersections(track, validityReasons);
        ValidateColliders(track, renderedTrackRoot, validityReasons);

        var validityPassed = validityReasons.Count == 0;
        if (validityPassed)
        {
            validityReasons.Add("All validity checks passed.");
        }

        var quality = EvaluateQuality(track);
        var band = GetBand(quality.Score);
        var passed = validityPassed && quality.Score >= QualityPassScore;

        var reasons = new List<string>();
        reasons.AddRange(validityReasons);
        reasons.Add($"Quality score: {quality.Score}/100 ({band}).");
        for (var i = 0; i < quality.Issues.Count; i++)
        {
            reasons.Add(quality.Issues[i]);
        }

        return new Result(passed, validityPassed, quality.Score, band, validityReasons, new List<string>(quality.Issues), reasons, quality);
    }

    public static QualityReport EvaluateQuality(MarbleRaceTrack track)
    {
        var issues = new List<string>();
        var sharpCornerIndices = new List<int>();
        var axisAlignedSegmentIndices = new List<int>();
        var minRadiusIndices = new List<int>();

        if (track == null || track.SampleCount < 8)
        {
            issues.Add("Insufficient points for quality analysis.");
            return new QualityReport(0, 0f, 0, 0f, 1f, 1f, 0f, 0, true, issues, sharpCornerIndices, axisAlignedSegmentIndices, minRadiusIndices);
        }

        var n = track.SampleCount;
        var segmentAngles = new float[n];
        var turns = new float[n];
        var maxTurn = 0f;
        var minRadius = float.MaxValue;
        var axisAligned = 0;
        var sumTurnDiff = 0f;
        var turnDiffCount = 0;

        var directionBins = new int[8];

        for (var i = 0; i < n; i++)
        {
            var a = track.Center[i];
            var b = track.Center[(i + 1) % n];
            var d = b - a;
            segmentAngles[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            var bin = Mathf.FloorToInt(Mathf.Repeat(segmentAngles[i] + 180f, 360f) / 45f) % directionBins.Length;
            directionBins[bin]++;

            var axisDelta = AxisAlignmentDelta(segmentAngles[i]);
            if (axisDelta <= 10f)
            {
                axisAligned++;
                axisAlignedSegmentIndices.Add(i);
            }
        }

        const int turnWindow = 2;
        for (var i = 0; i < n; i++)
        {
            var prev = track.Center[(i - turnWindow + n) % n];
            var p = track.Center[i];
            var next = track.Center[(i + turnWindow) % n];
            var a = (p - prev).normalized;
            var b = (next - p).normalized;
            var unsignedTurn = Vector2.Angle(a, b);
            maxTurn = Mathf.Max(maxTurn, unsignedTurn);
            if (unsignedTurn >= 55f)
            {
                sharpCornerIndices.Add(i);
            }

            turns[i] = Mathf.DeltaAngle(segmentAngles[(i - turnWindow + n) % n], segmentAngles[(i + turnWindow) % n]);
            var avgLen = 0.5f * (Vector2.Distance(prev, p) + Vector2.Distance(p, next));
            var theta = Mathf.Abs(turns[i]) * Mathf.Deg2Rad;
            var radius = theta > 1e-3f ? avgLen / theta : float.MaxValue;
            if (radius < minRadius)
            {
                minRadius = radius;
                minRadiusIndices.Clear();
                minRadiusIndices.Add(i);
            }
            else if (Mathf.Abs(radius - minRadius) < 0.05f)
            {
                minRadiusIndices.Add(i);
            }

            var diff = Mathf.Abs(turns[i] - turns[(i - 1 + n) % n]);
            if (!float.IsNaN(diff))
            {
                sumTurnDiff += diff;
                turnDiffCount++;
            }
        }

        var sharpCornerCount = sharpCornerIndices.Count;
        var minRadiusEstimate = EstimateRobustMinRadius(track.Center, 2, 14);
        if (minRadiusEstimate > 0f && minRadiusEstimate < float.MaxValue)
        {
            minRadius = Mathf.Min(minRadius, minRadiusEstimate);
        }

        var axisRatio = axisAligned / (float)n;
        var avgTurnDiff = turnDiffCount > 0 ? sumTurnDiff / turnDiffCount : 0f;
        var entropy = ComputeDirectionEntropy(directionBins, n);
        var uniqueDirectionBins = CountUniqueBins(directionBins);

        var score = 100f;

        if (maxTurn >= 75f)
        {
            score -= 24f;
            issues.Add($"FAIL: max turn angle too sharp ({maxTurn:F1}° >= 75°).");
        }
        else if (maxTurn >= 60f)
        {
            score -= 12f;
            issues.Add($"WARN: sharp max turn angle ({maxTurn:F1}°).");
        }

        if (sharpCornerCount >= 10)
        {
            score -= 20f;
            issues.Add($"FAIL: too many sharp corners ({sharpCornerCount}).");
        }
        else if (sharpCornerCount >= 5)
        {
            score -= 10f;
            issues.Add($"WARN: elevated sharp corner count ({sharpCornerCount}).");
        }

        var avgHalfWidth = 0f;
        for (var i = 0; i < n; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= Mathf.Max(1, n);
        var trackWidth = avgHalfWidth * 2f;
        var minAllowedRadius = trackWidth * 1.5f;
        if (minRadius < minAllowedRadius)
        {
            score -= 20f;
            issues.Add($"FAIL: min curvature radius too low ({minRadius:F2} < {minAllowedRadius:F2}).");
        }

        var boxyFail = axisRatio >= 0.72f;
        if (boxyFail)
        {
            score -= 26f;
            issues.Add($"FAIL: boxy track (axis-aligned segments {axisRatio * 100f:F0}% >= 72%).");
        }
        else if (axisRatio >= 0.48f)
        {
            score -= 12f;
            issues.Add($"WARN: high axis-aligned segment ratio ({axisRatio * 100f:F0}%).");
        }

        if (avgTurnDiff > 20f)
        {
            score -= 14f;
            issues.Add($"WARN: turning jitter is high ({avgTurnDiff:F1}° avg delta).");
        }

        if (entropy < 0.58f || uniqueDirectionBins <= 4)
        {
            score -= 16f;
            issues.Add($"WARN: low direction variety (entropy={entropy:F2}, bins={uniqueDirectionBins}).");
        }

        var clampedScore = Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);
        if (issues.Count == 0)
        {
            issues.Add("Quality checks passed.");
        }

        return new QualityReport(clampedScore, maxTurn, sharpCornerCount, minRadius, axisRatio, avgTurnDiff, entropy, uniqueDirectionBins, boxyFail, issues, sharpCornerIndices, axisAlignedSegmentIndices, minRadiusIndices);
    }

    private static float EstimateRobustMinRadius(Vector2[] center, int step, int percentile)
    {
        if (center == null || center.Length < 8)
        {
            return float.MaxValue;
        }

        var n = center.Length;
        var radii = new List<float>(n);
        for (var i = 0; i < n; i++)
        {
            var a = center[(i - step + n) % n];
            var b = center[i];
            var c = center[(i + step) % n];

            var radius = Circumradius(a, b, c);
            if (!float.IsNaN(radius) && !float.IsInfinity(radius) && radius > 0f)
            {
                radii.Add(radius);
            }
        }

        if (radii.Count == 0)
        {
            return float.MaxValue;
        }

        radii.Sort();
        var percentile01 = Mathf.Clamp01(percentile / 100f);
        var idx = Mathf.Clamp(Mathf.FloorToInt((radii.Count - 1) * percentile01), 0, radii.Count - 1);
        return radii[idx];
    }

    private static float Circumradius(Vector2 a, Vector2 b, Vector2 c)
    {
        var ab = Vector2.Distance(a, b);
        var bc = Vector2.Distance(b, c);
        var ca = Vector2.Distance(c, a);
        var area2 = Mathf.Abs(Cross(b - a, c - a));
        if (area2 < 1e-4f)
        {
            return float.MaxValue;
        }

        return (ab * bc * ca) / (2f * area2);
    }

    private static QualityBand GetBand(int score)
    {
        if (score >= 80)
        {
            return QualityBand.Green;
        }

        return score >= 60 ? QualityBand.Yellow : QualityBand.Red;
    }

    private static float AxisAlignmentDelta(float angleDeg)
    {
        var normalized = Mathf.Repeat(angleDeg, 90f);
        return Mathf.Min(normalized, 90f - normalized);
    }

    private static float ComputeDirectionEntropy(int[] bins, int total)
    {
        if (total <= 0)
        {
            return 0f;
        }

        var entropy = 0f;
        for (var i = 0; i < bins.Length; i++)
        {
            if (bins[i] <= 0)
            {
                continue;
            }

            var p = bins[i] / (float)total;
            entropy -= p * Mathf.Log(p, 2f);
        }

        var maxEntropy = Mathf.Log(bins.Length, 2f);
        return maxEntropy > 0f ? entropy / maxEntropy : 0f;
    }

    private static int CountUniqueBins(int[] bins)
    {
        var count = 0;
        for (var i = 0; i < bins.Length; i++)
        {
            if (bins[i] > 0)
            {
                count++;
            }
        }

        return count;
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
