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
        public float durationSeconds;
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

            bumpers[created++] = new Bumper
            {
                position = candidate,
                radius = radius
            };
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

    public static SpeedPad[] GenerateSpeedPads(IRng rng, int padsPerHalf, float halfWidth, float halfHeight, Vector2 padSize, float goalDepth, float goalHeight, Bumper[] bumpers, float bumperClearance, float speedMultiplier, float duration)
    {
        var total = padsPerHalf * 2;
        var pads = new SpeedPad[total];
        var created = 0;

        for (var side = 0; side < 2; side++)
        {
            var sign = side == 0 ? -1f : 1f;
            var localCreated = 0;
            var attempts = 0;
            while (localCreated < padsPerHalf && attempts < 300)
            {
                attempts++;
                var center = new Vector2(
                    sign * rng.Range(halfWidth * 0.1f, halfWidth * 0.6f),
                    rng.Range(-halfHeight * 0.7f, halfHeight * 0.7f));

                var rect = new Rect(center - (padSize * 0.5f), padSize);
                if (IntersectsGoal(rect, halfWidth, goalDepth, goalHeight))
                {
                    continue;
                }

                var clearBumper = true;
                for (var i = 0; i < bumpers.Length; i++)
                {
                    var closest = new Vector2(
                        Mathf.Clamp(bumpers[i].position.x, rect.xMin, rect.xMax),
                        Mathf.Clamp(bumpers[i].position.y, rect.yMin, rect.yMax));
                    if (Vector2.Distance(closest, bumpers[i].position) < bumpers[i].radius + bumperClearance)
                    {
                        clearBumper = false;
                        break;
                    }
                }

                if (!clearBumper)
                {
                    continue;
                }

                pads[created++] = new SpeedPad
                {
                    area = rect,
                    speedMultiplier = speedMultiplier,
                    durationSeconds = duration
                };
                localCreated++;
            }
        }

        if (created == total)
        {
            return pads;
        }

        var result = new SpeedPad[created];
        for (var i = 0; i < created; i++)
        {
            result[i] = pads[i];
        }

        return result;
    }

    private static bool IntersectsGoal(Rect rect, float halfWidth, float goalDepth, float goalHeight)
    {
        var left = new Rect(-halfWidth, -goalHeight * 0.5f, goalDepth, goalHeight);
        var right = new Rect(halfWidth - goalDepth, -goalHeight * 0.5f, goalDepth, goalHeight);
        return rect.Overlaps(left) || rect.Overlaps(right);
    }

    private static bool IsPointInGoalZone(Vector2 p, float halfWidth, float goalDepth, float goalHeight)
    {
        var yOk = Mathf.Abs(p.y) <= goalHeight * 0.5f;
        var left = p.x >= -halfWidth && p.x <= -halfWidth + goalDepth;
        var right = p.x <= halfWidth && p.x >= halfWidth - goalDepth;
        return yOk && (left || right);
    }
}
