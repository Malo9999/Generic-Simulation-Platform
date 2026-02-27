using UnityEngine;

public static class FantasySportHazards
{
    public struct Bumper
    {
        public Vector2 position;
        public float radius;
    }

    public struct SpeedPad
    {
        public Rect area;
        public float speedMultiplier;
    }

    public static Bumper[] GenerateBumpers(IRng rng, int count, float halfWidth, float halfHeight, float radius, float minDistance, float goalDepth, float goalHeight, Rect[] keepouts)
    {
        _ = rng;
        _ = minDistance;
        const float backlineBuffer = 4f;
        var adjustedRadius = radius * 0.8f;
        var requestedCount = Mathf.Max(2, count);
        var pairCount = Mathf.Max(1, requestedCount / 2);
        var bumpers = new Bumper[pairCount * 2];

        var xAbs = Mathf.Clamp(halfWidth * 0.22f, 6f, halfWidth - 10f);
        var yAbsTop = Mathf.Clamp(halfHeight * 0.42f, 6f, halfHeight - 10f);
        var rowY = new[] { yAbsTop, 0f, -yAbsTop };
        var yShiftPattern = new[] { 0f, 2f, -2f, 4f, -4f, 6f, -6f, 8f, -8f };

        for (var pair = 0; pair < pairCount; pair++)
        {
            var baseY = rowY[pair % rowY.Length];
            var mirrored = ResolveMirroredPair(
                xAbs,
                baseY,
                yShiftPattern,
                halfWidth,
                halfHeight,
                adjustedRadius,
                goalDepth,
                goalHeight,
                backlineBuffer,
                keepouts);

            bumpers[pair * 2] = new Bumper { position = new Vector2(-xAbs, mirrored), radius = adjustedRadius };
            bumpers[(pair * 2) + 1] = new Bumper { position = new Vector2(+xAbs, mirrored), radius = adjustedRadius };
        }

        return bumpers;
    }

    public static SpeedPad[] GenerateSymmetricPads(float halfWidth, float halfHeight, Vector2 preferredPadSize, float goalDepth)
    {
        const float minScale = 0.45f;
        const float slowMultiplier = 0.72f;
        const float speedMultiplier = 1.35f;
        const float edgeMargin = 5f;

        var xOuterAbs = Mathf.Clamp(halfWidth * 0.55f, goalDepth + 6f, halfWidth - edgeMargin);
        var xInnerAbs = Mathf.Clamp(halfWidth * 0.28f, 4f, xOuterAbs - (preferredPadSize.x * 0.9f));
        var yAbs = Mathf.Clamp(halfHeight * 0.28f, edgeMargin, halfHeight - edgeMargin);

        var scale = 1f;
        SpeedPad[] pads;
        while (true)
        {
            var size = preferredPadSize * scale;
            pads = new[]
            {
                new SpeedPad { area = CreateRect(-xOuterAbs, +yAbs, size), speedMultiplier = speedMultiplier },
                new SpeedPad { area = CreateRect(-xInnerAbs, -yAbs, size), speedMultiplier = slowMultiplier },
                new SpeedPad { area = CreateRect(+xOuterAbs, +yAbs, size), speedMultiplier = speedMultiplier },
                new SpeedPad { area = CreateRect(+xInnerAbs, -yAbs, size), speedMultiplier = slowMultiplier }
            };

            if (AllPadsDisjoint(pads) || scale <= minScale)
            {
                break;
            }

            scale = Mathf.Max(minScale, scale * 0.9f);
        }

        if (!AllPadsDisjoint(pads))
        {
            Debug.LogWarning("[FantasySport] Symmetric pad layout could not prevent overlaps; using minimum pad scale.");
        }

        return pads;
    }

    public static Rect[] GetPadKeepouts(SpeedPad[] pads, float padding)
    {
        if (pads == null || pads.Length == 0)
        {
            return System.Array.Empty<Rect>();
        }

        var keepouts = new Rect[pads.Length];
        for (var i = 0; i < pads.Length; i++)
        {
            var area = pads[i].area;
            keepouts[i] = Rect.MinMaxRect(area.xMin - padding, area.yMin - padding, area.xMax + padding, area.yMax + padding);
        }

        return keepouts;
    }

    private static Rect CreateRect(float centerX, float centerY, Vector2 size)
    {
        return new Rect(new Vector2(centerX - (size.x * 0.5f), centerY - (size.y * 0.5f)), size);
    }

    private static bool AllPadsDisjoint(SpeedPad[] pads)
    {
        for (var i = 0; i < pads.Length; i++)
        {
            if (pads[i].area.width <= 0f || pads[i].area.height <= 0f)
            {
                continue;
            }

            for (var j = i + 1; j < pads.Length; j++)
            {
                if (pads[j].area.width <= 0f || pads[j].area.height <= 0f)
                {
                    continue;
                }

                if (pads[i].area.Overlaps(pads[j].area))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CircleRectOverlap(Vector2 center, float radius, Rect rect)
    {
        var closest = new Vector2(Mathf.Clamp(center.x, rect.xMin, rect.xMax), Mathf.Clamp(center.y, rect.yMin, rect.yMax));
        return (closest - center).sqrMagnitude < radius * radius;
    }

    private static float ResolveMirroredPair(
        float xAbs,
        float baseY,
        float[] yShiftPattern,
        float halfWidth,
        float halfHeight,
        float radius,
        float goalDepth,
        float goalHeight,
        float backlineBuffer,
        Rect[] keepouts)
    {
        var maxY = halfHeight - radius - 1f;
        var minY = -maxY;

        for (var i = 0; i < yShiftPattern.Length; i++)
        {
            var y = Mathf.Clamp(baseY + yShiftPattern[i], minY, maxY);
            var left = new Vector2(-xAbs, y);
            var right = new Vector2(+xAbs, y);
            if (!IsBumperPlacementBlocked(left, halfWidth, goalDepth, goalHeight, radius, backlineBuffer, keepouts) &&
                !IsBumperPlacementBlocked(right, halfWidth, goalDepth, goalHeight, radius, backlineBuffer, keepouts))
            {
                return y;
            }
        }

        return Mathf.Clamp(baseY, minY, maxY);
    }

    private static bool IsBumperPlacementBlocked(Vector2 candidate, float halfWidth, float goalDepth, float goalHeight, float radius, float backlineBuffer, Rect[] keepouts)
    {
        if (Mathf.Abs(candidate.y) <= goalHeight * 0.5f && Mathf.Abs(Mathf.Abs(candidate.x) - halfWidth) <= backlineBuffer)
        {
            return true;
        }

        if (IsPointInGoalZone(candidate, halfWidth, goalDepth, goalHeight))
        {
            return true;
        }

        if (keepouts == null)
        {
            return false;
        }

        for (var k = 0; k < keepouts.Length; k++)
        {
            if (CircleRectOverlap(candidate, radius + 0.6f, keepouts[k]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointInGoalZone(Vector2 p, float halfWidth, float goalDepth, float goalHeight)
    {
        var yOk = Mathf.Abs(p.y) <= goalHeight * 0.5f;
        var left = p.x >= -halfWidth && p.x <= -halfWidth + goalDepth;
        var right = p.x <= halfWidth && p.x >= halfWidth - goalDepth;
        return yOk && (left || right);
    }
}
