using UnityEngine;

public static class Direction8
{
    private const float MinMagnitudeSqr = 0.0001f;

    public static int FromVector(Vector2 v)
    {
        if (v.sqrMagnitude <= MinMagnitudeSqr)
        {
            return 0;
        }

        var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0f)
        {
            angle += 360f;
        }

        var sector = Mathf.RoundToInt(angle / 45f) % 8;
        return sector;
    }

    public static float ToAngleDeg(int dir)
    {
        var normalized = ((dir % 8) + 8) % 8;
        return normalized * 45f;
    }
}
