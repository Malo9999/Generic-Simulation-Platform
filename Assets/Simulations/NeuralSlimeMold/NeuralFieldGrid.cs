using UnityEngine;

public sealed class NeuralFieldGrid
{
    private readonly int width;
    private readonly int height;
    private readonly float[] field;
    private readonly float[] scratch;
    private readonly Vector2 worldSize;
    private readonly bool wrapEdges;

    private const float DiagonalWeight = 0.70710678f;
    private const float WeakSignalPruneThreshold = 0.015f;
    private const float WeakSignalExtraDecay = 0.22f;
    private const float StrongSignalPreserveThreshold = 0.08f;
    private const float StrongSignalPreserveBoost = 0.05f;

    private const float BorderBleedFactor = 0.82f;
    private const int BorderBleedThickness = 2;

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

    public void DepositKernel(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);

        AddAt(x, y, amount * 1.0f);
        AddAt(x - 1, y, amount * 0.45f);
        AddAt(x + 1, y, amount * 0.45f);
        AddAt(x, y - 1, amount * 0.45f);
        AddAt(x, y + 1, amount * 0.45f);

        AddAt(x - 1, y - 1, amount * 0.20f);
        AddAt(x + 1, y - 1, amount * 0.20f);
        AddAt(x - 1, y + 1, amount * 0.20f);
        AddAt(x + 1, y + 1, amount * 0.20f);
    }

    public void DepositDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        if (amount <= 0f || radiusWorld <= 0f)
        {
            return;
        }

        var uv = WorldToUv(worldPosition);
        var gx = uv.x * (width - 1);
        var gy = uv.y * (height - 1);

        var radiusX = Mathf.Max(1, Mathf.CeilToInt((radiusWorld / worldSize.x) * width));
        var radiusY = Mathf.Max(1, Mathf.CeilToInt((radiusWorld / worldSize.y) * height));

        var minX = Mathf.Clamp(Mathf.FloorToInt(gx) - radiusX, 0, width - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(gx) + radiusX, 0, width - 1);
        var minY = Mathf.Clamp(Mathf.FloorToInt(gy) - radiusY, 0, height - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(gy) + radiusY, 0, height - 1);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - gx) / Mathf.Max(1f, radiusX);
                var dy = (y - gy) / Mathf.Max(1f, radiusY);
                var d2 = (dx * dx) + (dy * dy);
                if (d2 > 1f)
                {
                    continue;
                }

                var weight = 1f - d2;
                field[(y * width) + x] += amount * weight;
            }
        }
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

    public void ScrubDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        if (amount <= 0f || radiusWorld <= 0f)
        {
            return;
        }

        var uv = WorldToUv(worldPosition);
        var gx = uv.x * (width - 1);
        var gy = uv.y * (height - 1);

        var radiusX = Mathf.Max(1, Mathf.CeilToInt((radiusWorld / worldSize.x) * width));
        var radiusY = Mathf.Max(1, Mathf.CeilToInt((radiusWorld / worldSize.y) * height));

        var minX = Mathf.Clamp(Mathf.FloorToInt(gx) - radiusX, 0, width - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(gx) + radiusX, 0, width - 1);
        var minY = Mathf.Clamp(Mathf.FloorToInt(gy) - radiusY, 0, height - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(gy) + radiusY, 0, height - 1);

        var scrub = Mathf.Clamp01(amount);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - gx) / Mathf.Max(1f, radiusX);
                var dy = (y - gy) / Mathf.Max(1f, radiusY);
                var d2 = (dx * dx) + (dy * dy);
                if (d2 > 1f)
                {
                    continue;
                }

                var weight = 1f - d2;
                var idx = (y * width) + x;
                field[idx] = Mathf.Max(0f, field[idx] * (1f - (scrub * weight)));
            }
        }
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

    public float EstimateLocalCurvature(Vector2 worldPosition, float sampleRadiusWorld)
    {
        var c = SampleBilinear(worldPosition);
        var l = SampleBilinear(worldPosition + new Vector2(-sampleRadiusWorld, 0f));
        var r = SampleBilinear(worldPosition + new Vector2(sampleRadiusWorld, 0f));
        var u = SampleBilinear(worldPosition + new Vector2(0f, sampleRadiusWorld));
        var d = SampleBilinear(worldPosition + new Vector2(0f, -sampleRadiusWorld));

        var laplacian = Mathf.Abs((l + r + u + d) - (4f * c));
        return laplacian;
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

                var cardinalSum = n + s + e + w;
                var diagonalSum = (nw + ne + sw + se) * DiagonalWeight;
                var neighborAvg = (cardinalSum + diagonalSum) / (4f + (4f * DiagonalWeight));

                var diffused = Mathf.Lerp(c, neighborAvg, kDiff);

                if (c >= StrongSignalPreserveThreshold)
                {
                    diffused = Mathf.Lerp(diffused, c, StrongSignalPreserveBoost);
                }

                var decayFactor = baseDecayFactor;
                if (diffused < WeakSignalPruneThreshold)
                {
                    decayFactor *= Mathf.Clamp01(1f - (WeakSignalExtraDecay * dt));
                }

                if (x < BorderBleedThickness || x > width - 1 - BorderBleedThickness ||
                    y < BorderBleedThickness || y > height - 1 - BorderBleedThickness)
                {
                    diffused *= BorderBleedFactor;
                }

                scratch[idx] = Mathf.Max(0f, diffused * decayFactor);
            }
        }

        for (var i = 0; i < field.Length; i++)
        {
            field[i] = scratch[i];
        }
    }

    private void AddAt(int x, int y, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        field[(y * width) + x] += amount;
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