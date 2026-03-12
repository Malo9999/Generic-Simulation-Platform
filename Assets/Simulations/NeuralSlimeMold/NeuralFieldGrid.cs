using UnityEngine;

public sealed class NeuralFieldGrid
{
    private readonly int width;
    private readonly int height;
    private readonly float[] field;
    private readonly float[] scratch;
    private readonly float[] veinField;
    private readonly float[] veinScratch;
    private readonly float[] combinedField;
    private bool[] blockedMask;
    private readonly Vector2 worldSize;
    private readonly bool wrapEdges;
    private bool combinedDirty = true;

    private const float DiagonalWeight = 0.70710678f;
    private const float WeakSignalPruneThreshold = 0.015f;
    private const float WeakSignalExtraDecay = 0.22f;
    private const float StrongSignalPreserveThreshold = 0.08f;
    private const float StrongSignalPreserveBoost = 0.05f;

    private const float BorderBleedFactor = 0.82f;
    private const int BorderBleedThickness = 2;

    private const float VeinSampleWeight = 1.45f;
    private const float VeinDiffusionScale = 0.35f;
    private const float VeinDecayScale = 0.18f;
    private const float VeinPreserveThreshold = 0.12f;
    private const float VeinPreserveBoost = 0.14f;

    public NeuralFieldGrid(int width, int height, Vector2 worldSize, bool wrapEdges)
    {
        this.width = Mathf.Max(4, width);
        this.height = Mathf.Max(4, height);
        this.worldSize = new Vector2(Mathf.Max(2f, worldSize.x), Mathf.Max(2f, worldSize.y));
        this.wrapEdges = wrapEdges;
        field = new float[this.width * this.height];
        scratch = new float[field.Length];
        veinField = new float[field.Length];
        veinScratch = new float[field.Length];
        combinedField = new float[field.Length];
    }

    public int Width => width;
    public int Height => height;
    public Vector2 WorldSize => worldSize;
    public float[] Raw
    {
        get
        {
            RebuildCombinedField();
            return combinedField;
        }
    }

    public void Clear(float value = 0f)
    {
        for (var i = 0; i < field.Length; i++)
        {
            field[i] = value;
            scratch[i] = value;
            veinField[i] = 0f;
            veinScratch[i] = 0f;
            combinedField[i] = value;
        }
        combinedDirty = false;
    }

    public void Deposit(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);
        if (IsBlockedCell(x, y))
        {
            return;
        }

        field[(y * width) + x] += amount;
        combinedDirty = true;
    }

    public void DepositKernel(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);

        AddAt(field, x, y, amount * 1.0f);
        AddAt(field, x - 1, y, amount * 0.45f);
        AddAt(field, x + 1, y, amount * 0.45f);
        AddAt(field, x, y - 1, amount * 0.45f);
        AddAt(field, x, y + 1, amount * 0.45f);

        AddAt(field, x - 1, y - 1, amount * 0.20f);
        AddAt(field, x + 1, y - 1, amount * 0.20f);
        AddAt(field, x - 1, y + 1, amount * 0.20f);
        AddAt(field, x + 1, y + 1, amount * 0.20f);
        combinedDirty = true;
    }

    public void DepositVeinKernel(Vector2 worldPosition, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        WorldToGrid(worldPosition, out var x, out var y);

        AddAt(veinField, x, y, amount * 1.0f);
        AddAt(veinField, x - 1, y, amount * 0.65f);
        AddAt(veinField, x + 1, y, amount * 0.65f);
        AddAt(veinField, x, y - 1, amount * 0.65f);
        AddAt(veinField, x, y + 1, amount * 0.65f);

        AddAt(veinField, x - 1, y - 1, amount * 0.35f);
        AddAt(veinField, x + 1, y - 1, amount * 0.35f);
        AddAt(veinField, x - 1, y + 1, amount * 0.35f);
        AddAt(veinField, x + 1, y + 1, amount * 0.35f);
        combinedDirty = true;
    }

    public void DepositDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        DepositDiscTo(field, worldPosition, radiusWorld, amount, 1f);
    }

    public void DepositVeinDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        DepositDiscTo(veinField, worldPosition, radiusWorld, amount, 1.15f);
    }

    private void DepositDiscTo(float[] target, Vector2 worldPosition, float radiusWorld, float amount, float softnessScale)
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

                var weight = Mathf.Pow(1f - d2, softnessScale);
                if (!IsBlockedCell(x, y))
                {
                    target[(y * width) + x] += amount * weight;
                }
            }
        }

        combinedDirty = true;
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
        combinedDirty = true;
    }

    public void ScrubDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        ScrubDiscOn(field, worldPosition, radiusWorld, amount);
    }

    public void ScrubVeinDisc(Vector2 worldPosition, float radiusWorld, float amount)
    {
        ScrubDiscOn(veinField, worldPosition, radiusWorld, amount);
    }

    private void ScrubDiscOn(float[] target, Vector2 worldPosition, float radiusWorld, float amount)
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
                target[idx] = Mathf.Max(0f, target[idx] * (1f - (scrub * weight)));
            }
        }

        combinedDirty = true;
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

        var v00 = SampleCombinedAtIndex((y0 * width) + x0);
        var v10 = SampleCombinedAtIndex((y0 * width) + x1);
        var v01 = SampleCombinedAtIndex((y1 * width) + x0);
        var v11 = SampleCombinedAtIndex((y1 * width) + x1);

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

    public void Step(
        float diffusion,
        float decayPerSecond,
        float dt,
        float trunkTrailThreshold = StrongSignalPreserveThreshold,
        float trunkStabilityBoost = 0f,
        float duplicateSuppressionRadiusWorld = 0f)
    {
        StepLayer(field, scratch, diffusion, decayPerSecond, dt, trunkTrailThreshold, trunkStabilityBoost, duplicateSuppressionRadiusWorld, StrongSignalPreserveThreshold, StrongSignalPreserveBoost);
        StepLayer(veinField, veinScratch, diffusion * VeinDiffusionScale, decayPerSecond * VeinDecayScale, dt, Mathf.Max(trunkTrailThreshold, VeinPreserveThreshold), trunkStabilityBoost + 0.25f, duplicateSuppressionRadiusWorld * 0.75f, VeinPreserveThreshold, VeinPreserveBoost);
        combinedDirty = true;
    }

    private void StepLayer(float[] source, float[] target, float diffusion, float decayPerSecond, float dt, float trunkTrailThreshold, float trunkStabilityBoost, float duplicateSuppressionRadiusWorld, float preserveThreshold, float preserveBoost)
    {
        var kDiff = Mathf.Clamp01(diffusion);
        var clampedDecayPerSecond = Mathf.Max(0f, decayPerSecond);
        var clampedDt = Mathf.Max(0f, dt);

        if (clampedDt <= 0f)
        {
            return;
        }

        var hasSuppression = duplicateSuppressionRadiusWorld > 0f;
        var hasStability = trunkStabilityBoost > 0f;

        if (kDiff <= 0f && clampedDecayPerSecond <= 0f && !hasSuppression && !hasStability)
        {
            return;
        }

        var baseDecayFactor = Mathf.Clamp01(1f - (clampedDecayPerSecond * clampedDt));
        var trunkThreshold = Mathf.Max(0f, trunkTrailThreshold);
        var stabilityBoost = Mathf.Max(0f, trunkStabilityBoost);
        var suppressionRadius = Mathf.Max(0f, duplicateSuppressionRadiusWorld);
        var suppressionRadiusX = Mathf.Max(1, Mathf.RoundToInt((suppressionRadius / worldSize.x) * width));
        var suppressionRadiusY = Mathf.Max(1, Mathf.RoundToInt((suppressionRadius / worldSize.y) * height));

        if (kDiff <= 0f && !hasSuppression && !hasStability)
        {
            for (var i = 0; i < source.Length; i++)
            {
                if (IsBlockedIndex(i))
                {
                    source[i] = 0f;
                    continue;
                }

                var value = source[i];
                if (value < WeakSignalPruneThreshold)
                {
                    value *= Mathf.Clamp01(1f - (WeakSignalExtraDecay * clampedDt));
                }

                source[i] = Mathf.Max(0f, value * baseDecayFactor);
            }

            return;
        }

        for (var y = 0; y < height; y++)
        {
            var yUp = y == 0 ? (wrapEdges ? height - 1 : 0) : y - 1;
            var yDown = y == height - 1 ? (wrapEdges ? 0 : height - 1) : y + 1;

            for (var x = 0; x < width; x++)
            {
                var xLeft = x == 0 ? (wrapEdges ? width - 1 : 0) : x - 1;
                var xRight = x == width - 1 ? (wrapEdges ? 0 : width - 1) : x + 1;

                var idx = (y * width) + x;
                var c = source[idx];

                if (IsBlockedIndex(idx))
                {
                    target[idx] = 0f;
                    continue;
                }

                var n = source[(yUp * width) + x];
                var s = source[(yDown * width) + x];
                var w = source[(y * width) + xLeft];
                var e = source[(y * width) + xRight];
                var nw = source[(yUp * width) + xLeft];
                var ne = source[(yUp * width) + xRight];
                var sw = source[(yDown * width) + xLeft];
                var se = source[(yDown * width) + xRight];

                n = IsBlockedCell(x, yUp) ? c : n;
                s = IsBlockedCell(x, yDown) ? c : s;
                w = IsBlockedCell(xLeft, y) ? c : w;
                e = IsBlockedCell(xRight, y) ? c : e;
                nw = IsBlockedCell(xLeft, yUp) ? c : nw;
                ne = IsBlockedCell(xRight, yUp) ? c : ne;
                sw = IsBlockedCell(xLeft, yDown) ? c : sw;
                se = IsBlockedCell(xRight, yDown) ? c : se;

                var cardinalSum = n + s + e + w;
                var diagonalSum = (nw + ne + sw + se) * DiagonalWeight;
                var neighborAvg = (cardinalSum + diagonalSum) / (4f + (4f * DiagonalWeight));

                var diffused = Mathf.Lerp(c, neighborAvg, kDiff);

                if (c >= preserveThreshold)
                {
                    diffused = Mathf.Lerp(diffused, c, preserveBoost);
                }

                var decayFactor = baseDecayFactor;

                if (stabilityBoost > 0f && diffused >= trunkThreshold)
                {
                    var trunkStrength = Mathf.Clamp01((diffused - trunkThreshold) / Mathf.Max(0.0001f, trunkThreshold + 0.05f));
                    decayFactor = Mathf.Lerp(decayFactor, 1f, Mathf.Clamp01(stabilityBoost * clampedDt * (0.65f + (0.35f * trunkStrength))));
                }

                if (diffused < WeakSignalPruneThreshold)
                {
                    decayFactor *= Mathf.Clamp01(1f - (WeakSignalExtraDecay * clampedDt));
                }

                if (suppressionRadius > 0f && diffused < trunkThreshold)
                {
                    var nearbyStrong = SampleNearbyStrongest(source, x, y, suppressionRadiusX, suppressionRadiusY);
                    if (nearbyStrong > diffused)
                    {
                        var duplicatePressure = Mathf.Clamp01((nearbyStrong - diffused) / Mathf.Max(0.0001f, trunkThreshold));
                        decayFactor *= Mathf.Clamp01(1f - (duplicatePressure * clampedDt * 1.25f));
                    }
                }

                if (x < BorderBleedThickness || x > width - 1 - BorderBleedThickness ||
                    y < BorderBleedThickness || y > height - 1 - BorderBleedThickness)
                {
                    diffused *= BorderBleedFactor;
                }

                target[idx] = Mathf.Max(0f, diffused * decayFactor);
            }
        }

        for (var i = 0; i < source.Length; i++)
        {
            source[i] = target[i];
        }
    }

    public void SetBlockedMask(bool[] blockedMask)
    {
        if (blockedMask == null || blockedMask.Length != field.Length)
        {
            this.blockedMask = null;
            return;
        }

        this.blockedMask = blockedMask;

        for (var i = 0; i < field.Length; i++)
        {
            if (this.blockedMask[i])
            {
                field[i] = 0f;
                scratch[i] = 0f;
                veinField[i] = 0f;
                veinScratch[i] = 0f;
                combinedField[i] = 0f;
            }
        }

        combinedDirty = true;
    }

    public int WorldToIndex(Vector2 worldPosition)
    {
        WorldToGrid(worldPosition, out var x, out var y);
        return (y * width) + x;
    }

    public Vector2 GridToWorld(int x, int y)
    {
        var clampedX = Mathf.Clamp(x, 0, width - 1);
        var clampedY = Mathf.Clamp(y, 0, height - 1);
        var u = width <= 1 ? 0f : clampedX / (float)(width - 1);
        var v = height <= 1 ? 0f : clampedY / (float)(height - 1);
        var worldX = Mathf.Lerp(-worldSize.x * 0.5f, worldSize.x * 0.5f, u);
        var worldY = Mathf.Lerp(-worldSize.y * 0.5f, worldSize.y * 0.5f, v);
        return new Vector2(worldX, worldY);
    }

    private float SampleNearbyStrongest(float[] source, int x, int y, int radiusX, int radiusY)
    {
        var maxTrail = 0f;
        maxTrail = Mathf.Max(maxTrail, source[(y * width) + Mathf.Clamp(x - radiusX, 0, width - 1)]);
        maxTrail = Mathf.Max(maxTrail, source[(y * width) + Mathf.Clamp(x + radiusX, 0, width - 1)]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y - radiusY, 0, height - 1) * width) + x]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y + radiusY, 0, height - 1) * width) + x]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y - radiusY, 0, height - 1) * width) + Mathf.Clamp(x - radiusX, 0, width - 1)]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y - radiusY, 0, height - 1) * width) + Mathf.Clamp(x + radiusX, 0, width - 1)]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y + radiusY, 0, height - 1) * width) + Mathf.Clamp(x - radiusX, 0, width - 1)]);
        maxTrail = Mathf.Max(maxTrail, source[(Mathf.Clamp(y + radiusY, 0, height - 1) * width) + Mathf.Clamp(x + radiusX, 0, width - 1)]);
        return maxTrail;
    }

    private void AddAt(float[] target, int x, int y, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        if (IsBlockedCell(x, y))
        {
            return;
        }
        target[(y * width) + x] += amount;
    }

    private float SampleCombinedAtIndex(int idx)
    {
        return field[idx] + (veinField[idx] * VeinSampleWeight);
    }

    private void RebuildCombinedField()
    {
        if (!combinedDirty)
        {
            return;
        }

        for (var i = 0; i < combinedField.Length; i++)
        {
            combinedField[i] = SampleCombinedAtIndex(i);
        }

        combinedDirty = false;
    }

    private bool IsBlockedCell(int x, int y)
    {
        if (blockedMask == null)
        {
            return false;
        }

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return blockedMask[(y * width) + x];
    }

    private bool IsBlockedIndex(int idx)
    {
        return blockedMask != null && idx >= 0 && idx < blockedMask.Length && blockedMask[idx];
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
