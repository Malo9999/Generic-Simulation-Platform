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

    public static Bumper[] GenerateBumpers(IRng rng, int count, float halfWidth, float halfHeight, float radius, float minDistance, float goalDepth, float goalHeight)
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

    public static SpeedPad[] GenerateSymmetricPads(float halfWidth, float halfHeight, Vector2 preferredPadSize, float goalDepth, Bumper[] bumpers)
    {
        const float margin = 3f;
        const float minScale = 0.45f;
        const float slowMultiplier = 0.72f;
        const float speedMultiplier = 1.35f;

        var safeXMin = -halfWidth + margin;
        var safeXMax = halfWidth - margin;
        var safeYMin = -halfHeight + margin;
        var safeYMax = halfHeight - margin;
        var goalKeepout = goalDepth + 2f;

        var xLeft = Mathf.Clamp(-halfWidth * 0.35f, Mathf.Max(safeXMin, -halfWidth + goalKeepout), -margin);
        var xRight = Mathf.Clamp(halfWidth * 0.35f, margin, Mathf.Min(safeXMax, halfWidth - goalKeepout));
        var yA = Mathf.Clamp(halfHeight * 0.25f, safeYMin, safeYMax);
        var yB = Mathf.Clamp(-halfHeight * 0.25f, safeYMin, safeYMax);

        var scale = 1f;
        SpeedPad[] pads;
        while (true)
        {
            var size = preferredPadSize * scale;
            pads = new[]
            {
                new SpeedPad { area = CreateRect(xLeft, yA, size), speedMultiplier = speedMultiplier },
                new SpeedPad { area = CreateRect(xLeft, yB, size), speedMultiplier = slowMultiplier },
                new SpeedPad { area = CreateRect(xRight, yA, size), speedMultiplier = speedMultiplier },
                new SpeedPad { area = CreateRect(xRight, yB, size), speedMultiplier = slowMultiplier }
            };

            if (AllPadsDisjoint(pads) || scale <= minScale)
            {
                break;
            }

            scale *= 0.9f;
        }

        if (!AllPadsDisjoint(pads))
        {
            Debug.LogWarning("[FantasySport] Symmetric pad layout could not prevent overlaps; disabling colliding pads.");
            DisableOverlappingPads(pads);
        }

        ShiftPadsAwayFromBumpers(pads, bumpers, safeYMin, safeYMax);
        if (!AllPadsDisjoint(pads))
        {
            Debug.LogWarning("[FantasySport] Symmetric pad layout still overlaps after bumper shifts; disabling colliding pads.");
            DisableOverlappingPads(pads);
        }

        var activeCount = 0;
        for (var i = 0; i < pads.Length; i++)
        {
            if (pads[i].area.width > 0f && pads[i].area.height > 0f)
            {
                activeCount++;
            }
        }

        var result = new SpeedPad[activeCount];
        var index = 0;
        for (var i = 0; i < pads.Length; i++)
        {
            if (pads[i].area.width <= 0f || pads[i].area.height <= 0f)
            {
                continue;
            }

            result[index++] = pads[i];
        }

        return result;
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

    private static void DisableOverlappingPads(SpeedPad[] pads)
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
                    pads[j].area = new Rect(0f, 0f, 0f, 0f);
                }
            }
        }
    }

    private static void ShiftPadsAwayFromBumpers(SpeedPad[] pads, Bumper[] bumpers, float safeYMin, float safeYMax)
    {
        for (var i = 0; i < pads.Length; i++)
        {
            if (pads[i].area.width <= 0f || pads[i].area.height <= 0f)
            {
                continue;
            }

            for (var b = 0; b < bumpers.Length; b++)
            {
                if (!CircleRectOverlap(bumpers[b].position, bumpers[b].radius, pads[i].area))
                {
                    continue;
                }

                var shifted = TryShiftPad(pads, i, bumpers, safeYMin, safeYMax, +2f) || TryShiftPad(pads, i, bumpers, safeYMin, safeYMax, -2f);
                if (!shifted)
                {
                    pads[i].area = new Rect(0f, 0f, 0f, 0f);
                    break;
                }
            }
        }
    }

    private static bool TryShiftPad(SpeedPad[] pads, int padIndex, Bumper[] bumpers, float safeYMin, float safeYMax, float yDelta)
    {
        var pad = pads[padIndex];
        var center = pad.area.center;
        var targetY = Mathf.Clamp(center.y + yDelta, safeYMin + (pad.area.height * 0.5f), safeYMax - (pad.area.height * 0.5f));
        if (Mathf.Abs(targetY - center.y) < 0.01f)
        {
            return false;
        }

        pad.area.center = new Vector2(center.x, targetY);
        for (var i = 0; i < bumpers.Length; i++)
        {
            if (CircleRectOverlap(bumpers[i].position, bumpers[i].radius, pad.area))
            {
                return false;
            }
        }

        pads[padIndex] = pad;
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
