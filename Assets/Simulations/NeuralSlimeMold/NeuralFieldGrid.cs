using UnityEngine;

public sealed class NeuralFieldGrid
{
    private readonly int width;
    private readonly int height;
    private readonly float[] field;
    private readonly float[] scratch;
    private readonly Vector2 worldSize;
    private readonly bool wrapEdges;

    // Internal shaping constants. Kept private so the rest of your code does not change.
    private const float DiagonalWeight = 0.70710678f;
    private const float WeakSignalPruneThreshold = 0.015f;
    private const float WeakSignalExtraDecay = 0.22f;
    private const float StrongSignalPreserveThreshold = 0.08f;
    private const float StrongSignalPreserveBoost = 0.05f;

    public NeuralFieldGrid(int width, int height, Vector2 worldSize, bool wrapEdges)
    {
        this.width = Mathf.Max(4, width);
        this.height = Mathf.Max(4, height);
        this.worldSize = new Vector2(Mathf.Max(2f, worldSize.x), Mathf.Max(2f, worldSize.y));
        this.wrapEdges = wrapEdges;
        field = new float[this.width * this.height];
        scratch = new float[field.Length];
    }

    public int Width => width;
    public int Height => height;
    public Vector2 WorldSize => worldSize;
    public float[] Raw => field;

    public void Clear(float value = 0f)
    {
        for (var i = 0; i < field.Length; i++)
        {
            field[i] = value;
            scratch[i] = value;
        }
    }

    public void Deposit(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);
        field[(y * width) + x] += amount;
    }

    public void ScrubAt(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);
        var idx = (y * width) + x;
        field[idx] = Mathf.Max(0f, field[idx] * (1f - Mathf.Clamp01(amount)));
    }

    public float SampleBilinear(Vector2 worldPosition)
    {
        var uv = WorldToUv(worldPosition);
        var x = uv.x * (width - 1);
        var y = uv.y * (height - 1);

        var x0 = Mathf.Clamp((int)x, 0, width - 1);
        var y0 = Mathf.Clamp((int)y, 0, height - 1);
        var x1 = Mathf.Min(x0 + 1, width - 1);
        var y1 = Mathf.Min(y0 + 1, height - 1);

        var tx = x - x0;
        var ty = y - y0;

        var v00 = field[(y0 * width) + x0];
        var v10 = field[(y0 * width) + x1];
        var v01 = field[(y1 * width) + x0];
        var v11 = field[(y1 * width) + x1];

        var a = Mathf.Lerp(v00, v10, tx);
        var b = Mathf.Lerp(v01, v11, tx);
        return Mathf.Lerp(a, b, ty);
    }

    public void Step(float diffusion, float decayPerSecond, float dt)
    {
        var kDiff = Mathf.Clamp01(diffusion);
        var baseDecayFactor = Mathf.Clamp01(1f - (Mathf.Max(0f, decayPerSecond) * dt));

        for (var y = 0; y < height; y++)
        {
            var yUp = y == 0 ? (wrapEdges ? height - 1 : 0) : y - 1;
            var yDown = y == height - 1 ? (wrapEdges ? 0 : height - 1) : y + 1;

            for (var x = 0; x < width; x++)
            {
                var xLeft = x == 0 ? (wrapEdges ? width - 1 : 0) : x - 1;
                var xRight = x == width - 1 ? (wrapEdges ? 0 : width - 1) : x + 1;

                var idx = (y * width) + x;

                var c = field[idx];

                var n = field[(yUp * width) + x];
                var s = field[(yDown * width) + x];
                var w = field[(y * width) + xLeft];
                var e = field[(y * width) + xRight];

                var nw = field[(yUp * width) + xLeft];
                var ne = field[(yUp * width) + xRight];
                var sw = field[(yDown * width) + xLeft];
                var se = field[(yDown * width) + xRight];

                // Weighted 8-neighbor average gives a rounder, less blocky spread.
                var cardinalSum = n + s + e + w;
                var diagonalSum = (nw + ne + sw + se) * DiagonalWeight;
                var neighborAvg = (cardinalSum + diagonalSum) / (4f + (4f * DiagonalWeight));

                // Diffusion toward neighborhood mean.
                var diffused = Mathf.Lerp(c, neighborAvg, kDiff);

                // Preserve stronger veins a little so they do not instantly wash out.
                if (c >= StrongSignalPreserveThreshold)
                {
                    diffused = Mathf.Lerp(diffused, c, StrongSignalPreserveBoost);
                }

                // Weak background haze dies a bit faster than useful structure.
                var decayFactor = baseDecayFactor;
                if (diffused < WeakSignalPruneThreshold)
                {
                    decayFactor *= Mathf.Clamp01(1f - (WeakSignalExtraDecay * dt));
                }

                scratch[idx] = Mathf.Max(0f, diffused * decayFactor);
            }
        }

        for (var i = 0; i < field.Length; i++)
        {
            field[i] = scratch[i];
        }
    }

    private void WorldToGrid(Vector2 worldPosition, out int x, out int y)
    {
        var uv = WorldToUv(worldPosition);
        x = Mathf.Clamp(Mathf.RoundToInt(uv.x * (width - 1)), 0, width - 1);
        y = Mathf.Clamp(Mathf.RoundToInt(uv.y * (height - 1)), 0, height - 1);
    }

    private Vector2 WorldToUv(Vector2 worldPosition)
    {
        var u = Mathf.InverseLerp(-worldSize.x * 0.5f, worldSize.x * 0.5f, worldPosition.x);
        var v = Mathf.InverseLerp(-worldSize.y * 0.5f, worldSize.y * 0.5f, worldPosition.y);
        if (wrapEdges)
        {
            return new Vector2(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
        }

        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }
}