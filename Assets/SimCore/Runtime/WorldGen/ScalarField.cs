using System;
using UnityEngine;

[Serializable]
public class ScalarField
{
    public string id;
    public WorldGridSpec grid;
    public float[] values;
    public float min;
    public float max;
    public bool normalized;

    public ScalarField(string id, WorldGridSpec grid)
    {
        this.id = id;
        this.grid = grid;
        values = new float[Mathf.Max(1, grid.width * grid.height)];
        min = 0f;
        max = 1f;
        normalized = false;
    }

    public float this[int x, int y]
    {
        get => values[grid.Index(x, y)];
        set => values[grid.Index(x, y)] = value;
    }

    public void Normalize01InPlace()
    {
        if (values == null || values.Length == 0)
        {
            min = 0f;
            max = 1f;
            normalized = true;
            return;
        }

        min = float.MaxValue;
        max = float.MinValue;
        for (var i = 0; i < values.Length; i++)
        {
            var v = values[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        var span = max - min;
        if (span <= 1e-6f)
        {
            for (var i = 0; i < values.Length; i++) values[i] = 0f;
            min = 0f;
            max = 1f;
            normalized = true;
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Mathf.Clamp01((values[i] - min) / span);
        }

        min = 0f;
        max = 1f;
        normalized = true;
    }
}
