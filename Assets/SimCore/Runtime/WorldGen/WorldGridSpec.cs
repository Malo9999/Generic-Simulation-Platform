using System;
using UnityEngine;

[Serializable]
public struct WorldGridSpec
{
    public int width;
    public int height;
    public float cellSize;
    public Vector2 originWorld;

    public int Index(int x, int y)
    {
        return y * width + x;
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public Vector2 CellCenterWorld(int x, int y)
    {
        return originWorld + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    }
}
