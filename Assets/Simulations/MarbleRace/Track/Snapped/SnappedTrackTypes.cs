using UnityEngine;

public enum Dir8
{
    E = 0,
    NE = 1,
    N = 2,
    NW = 3,
    W = 4,
    SW = 5,
    S = 6,
    SE = 7
}

public readonly struct Segment
{
    public readonly Vector2Int a;
    public readonly Vector2Int b;

    public Segment(Vector2Int p0, Vector2Int p1)
    {
        if (p0.x < p1.x || (p0.x == p1.x && p0.y <= p1.y))
        {
            a = p0;
            b = p1;
        }
        else
        {
            a = p1;
            b = p0;
        }
    }
}

public static class SnappedTrackTypes
{
    public static readonly Vector2Int[] DirDelta8 =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(1, -1)
    };

    public static readonly float[] DirAngleDeg8 =
    {
        0f,
        45f,
        90f,
        135f,
        180f,
        225f,
        270f,
        315f
    };

    public static int TurnLeft45(int dir) => (dir + 1) & 7;

    public static int TurnRight45(int dir) => (dir + 7) & 7;

    public static int TurnLeft90(int dir) => (dir + 2) & 7;

    public static int TurnRight90(int dir) => (dir + 6) & 7;

    public static bool IsDiagonal(int dir) => (dir & 1) == 1;

    public static float StepLength(int dir) => IsDiagonal(dir) ? 1.41421356f : 1f;
}
