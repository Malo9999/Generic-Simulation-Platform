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
        var bumpers = new Bumper[count];
        var created = 0;
        var attempts = 0;

        while (created < count && attempts < 400)
        {
            attempts++;
            var candidate = new Vector2(
                rng.Range(-halfWidth * 0.35f, halfWidth * 0.35f),
                rng.Range(-halfHeight * 0.75f, halfHeight * 0.75f));

            if (IsPointInGoalZone(candidate, halfWidth, goalDepth, goalHeight))
            {
                continue;
            }

            if (keepouts != null)
            {
                var overlapsKeepout = false;
                for (var k = 0; k < keepouts.Length; k++)
                {
                    if (!CircleRectOverlap(candidate, radius + 0.6f, keepouts[k]))
                    {
                        continue;
                    }

                    overlapsKeepout = true;
                    break;
                }

                if (overlapsKeepout)
                {
                    continue;
                }
            }

            var valid = true;
            for (var i = 0; i < created; i++)
            {
                if (Vector2.Distance(candidate, bumpers[i].position) < minDistance)
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
            {
                continue;
            }

            bumpers[created++] = new Bumper { position = candidate, radius = radius };
        }

        if (created == count)
        {
            return bumpers;
        }

        var result = new Bumper[created];
        for (var i = 0; i < created; i++)
        {
            result[i] = bumpers[i];
        }

        return result;
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

    private static bool IsPointInGoalZone(Vector2 p, float halfWidth, float goalDepth, float goalHeight)
    {
        var yOk = Mathf.Abs(p.y) <= goalHeight * 0.5f;
        var left = p.x >= -halfWidth && p.x <= -halfWidth + goalDepth;
        var right = p.x <= halfWidth && p.x >= halfWidth - goalDepth;
        return yOk && (left || right);
    }
}
