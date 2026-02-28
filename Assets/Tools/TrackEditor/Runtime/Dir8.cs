using UnityEngine;

public enum Dir8
{
    N = 0,
    NE = 1,
    E = 2,
    SE = 3,
    S = 4,
    SW = 5,
    W = 6,
    NW = 7
}

public static class Dir8Extensions
{
    public static Vector2 ToVector2(this Dir8 dir)
    {
        return dir switch
        {
            Dir8.N => Vector2.up,
            Dir8.NE => new Vector2(1f, 1f).normalized,
            Dir8.E => Vector2.right,
            Dir8.SE => new Vector2(1f, -1f).normalized,
            Dir8.S => Vector2.down,
            Dir8.SW => new Vector2(-1f, -1f).normalized,
            Dir8.W => Vector2.left,
            Dir8.NW => new Vector2(-1f, 1f).normalized,
            _ => Vector2.right
        };
    }

    public static Dir8 Opposite(this Dir8 dir)
    {
        return RotateSteps45(dir, 4);
    }

    public static Dir8 RotateSteps45(this Dir8 dir, int steps)
    {
        return (Dir8)(((int)dir + steps % 8 + 8) % 8);
    }

    public static Dir8 FromAngleOrVector(Vector2 v)
    {
        if (v.sqrMagnitude < 0.0001f)
        {
            return Dir8.E;
        }

        var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        var step = Mathf.RoundToInt(angle / 45f);
        step = (step % 8 + 8) % 8;

        // Convert from mathematical angle basis (0 = E) to enum basis (0 = N).
        var enumValue = (2 - step + 8) % 8;
        return (Dir8)enumValue;
    }
}
