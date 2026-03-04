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

    public static Bumper[] GenerateBumpers(IRng rng, int count, float halfWidth, float halfHeight, float radius, float minDistance, float endzoneDepth, float endzoneHalfHeight, Rect[] keepouts)
    {
        _ = rng;
        _ = count;
        _ = minDistance;
        var adjustedRadius = radius * 0.33f;
        var xAbs = halfWidth * 0.18f;
        var y1 = halfHeight * 0.34f;
        var y2 = halfHeight * 0.12f;

        var rows = new[] { y1, y2, -y2, -y1 };
        var canPlace = false;

        for (var iter = 0; iter < 6; iter++)
        {
            canPlace = IsGridPlacementClear(xAbs, rows, adjustedRadius, halfWidth, endzoneDepth, endzoneHalfHeight, keepouts);
            if (canPlace)
            {
                break;
            }

            adjustedRadius *= 0.9f;
            xAbs *= 0.95f;
        }

        var bumpers = new Bumper[rows.Length * 2];
        for (var row = 0; row < rows.Length; row++)
        {
            var y = Mathf.Clamp(rows[row], -halfHeight + adjustedRadius + 1f, halfHeight - adjustedRadius - 1f);
            bumpers[row * 2] = new Bumper { position = new Vector2(-xAbs, y), radius = adjustedRadius };
            bumpers[(row * 2) + 1] = new Bumper { position = new Vector2(+xAbs, y), radius = adjustedRadius };
        }

        if (!canPlace)
        {
            Debug.LogWarning("[FantasySport] Bumper grid keepouts still intersect after uniform shrink; preserving aligned grid placement.");
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

    private static bool IsGridPlacementClear(
        float xAbs,
        float[] rows,
        float radius,
        float halfWidth,
        float endzoneDepth,
        float endzoneHalfHeight,
        Rect[] keepouts)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            var y = rows[i];
            var left = new Vector2(-xAbs, y);
            var right = new Vector2(+xAbs, y);
            if (IsBumperPlacementBlocked(left, halfWidth, endzoneDepth, endzoneHalfHeight, radius, keepouts) ||
                IsBumperPlacementBlocked(right, halfWidth, endzoneDepth, endzoneHalfHeight, radius, keepouts))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBumperPlacementBlocked(Vector2 candidate, float halfWidth, float endzoneDepth, float endzoneHalfHeight, float radius, Rect[] keepouts)
    {
        if (IsCircleInEndzone(candidate, radius + 0.6f, halfWidth, endzoneDepth, endzoneHalfHeight))
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

    private static bool IsCircleInEndzone(Vector2 center, float radius, float halfWidth, float endzoneDepth, float endzoneHalfHeight)
    {
        var left = Rect.MinMaxRect(-halfWidth, -endzoneHalfHeight, -halfWidth + endzoneDepth, endzoneHalfHeight);
        var right = Rect.MinMaxRect(halfWidth - endzoneDepth, -endzoneHalfHeight, halfWidth, endzoneHalfHeight);
        return CircleRectOverlap(center, radius, left) || CircleRectOverlap(center, radius, right);
    }
}
