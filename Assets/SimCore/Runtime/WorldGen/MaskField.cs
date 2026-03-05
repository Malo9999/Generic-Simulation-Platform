using System;
using UnityEngine;

public enum MaskEncoding
{
    Boolean = 0,
    Categorical = 1
}

[Serializable]
public class MaskField
{
    public string id;
    public WorldGridSpec grid;
    public byte[] values;
    public MaskEncoding encoding;
    public string[] categories;

    public MaskField(string id, WorldGridSpec grid, MaskEncoding encoding)
    {
        this.id = id;
        this.grid = grid;
        this.encoding = encoding;
        values = new byte[Mathf.Max(1, grid.width * grid.height)];
        categories = Array.Empty<string>();
    }

    public byte this[int x, int y]
    {
        get => values[grid.Index(x, y)];
        set => values[grid.Index(x, y)] = value;
    }
}
