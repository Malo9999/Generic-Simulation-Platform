using System.Collections.Generic;
using UnityEngine;

public struct TerrainFieldBuildParams
{
    public float valleyRadius;
    public float floodplainRadius;
    public float ridgeRadius;
    public float rockSlopeWeight;
    public float rockDrynessWeight;

    public static TerrainFieldBuildParams Default => new TerrainFieldBuildParams
    {
        valleyRadius = 12f,
        floodplainRadius = 8f,
        ridgeRadius = 32f,
        rockSlopeWeight = 0.7f,
        rockDrynessWeight = 0.3f
    };
}

public static class TerrainFieldBuilder
{
    private const float Epsilon = 1e-5f;

    public static TerrainCoherenceFields Build(WorldGridSpec grid, ScalarField normalizedHeight, IReadOnlyList<Vector2> mainChannelPoints, TerrainFieldBuildParams parameters)
    {
        var fields = new TerrainCoherenceFields(grid);

        if (normalizedHeight == null || normalizedHeight.values == null || normalizedHeight.values.Length == 0)
            return fields;

        var maxDistance = Mathf.Max(grid.cellSize, Mathf.Sqrt(grid.width * grid.width + grid.height * grid.height) * grid.cellSize);
        var valleyRadius = Mathf.Max(grid.cellSize, parameters.valleyRadius);
        var floodRadius = Mathf.Max(grid.cellSize, parameters.floodplainRadius);
        var ridgeRadius = Mathf.Max(valleyRadius, parameters.ridgeRadius);

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var distance = DistanceToPolyline(mainChannelPoints, p);
            if (float.IsInfinity(distance) || float.IsNaN(distance)) distance = maxDistance;

            var h = normalizedHeight[x, y];
            var slope = ComputeSlopeApprox(normalizedHeight, x, y);
            var distanceNorm = Mathf.Clamp01(distance / maxDistance);

            var valley = Mathf.Clamp01(1f - distance / valleyRadius);
            valley *= Mathf.Clamp01(1f - h * 0.65f + slope * 0.2f);

            var floodplain = Mathf.Clamp01(1f - distance / floodRadius);
            floodplain *= Mathf.Clamp01(1f - h * 1.25f);

            var ridgeDistance = Mathf.Clamp01(distance / ridgeRadius);
            var ridge = Mathf.Clamp01(ridgeDistance * (0.55f + h * 0.45f) * (1f - valley * 0.8f));
            var dryness = Mathf.Clamp01(1f - floodplain);
            var rockiness = Mathf.Clamp01(slope * parameters.rockSlopeWeight + dryness * parameters.rockDrynessWeight + ridge * 0.2f);

            fields.normalizedHeight[x, y] = h;
            fields.slopeApprox[x, y] = slope;
            fields.distanceToMainChannel[x, y] = distanceNorm;
            fields.valleyMask[x, y] = valley;
            fields.floodplainMask[x, y] = floodplain;
            fields.ridgeMask[x, y] = ridge;
            fields.rockinessMask[x, y] = rockiness;
        }

        MarkNormalized(fields);
        return fields;
    }

    private static void MarkNormalized(TerrainCoherenceFields fields)
    {
        fields.normalizedHeight.normalized = true;
        fields.normalizedHeight.min = 0f;
        fields.normalizedHeight.max = 1f;

        fields.slopeApprox.normalized = true;
        fields.slopeApprox.min = 0f;
        fields.slopeApprox.max = 1f;

        fields.distanceToMainChannel.normalized = true;
        fields.distanceToMainChannel.min = 0f;
        fields.distanceToMainChannel.max = 1f;

        fields.valleyMask.normalized = true;
        fields.valleyMask.min = 0f;
        fields.valleyMask.max = 1f;

        fields.floodplainMask.normalized = true;
        fields.floodplainMask.min = 0f;
        fields.floodplainMask.max = 1f;

        fields.ridgeMask.normalized = true;
        fields.ridgeMask.min = 0f;
        fields.ridgeMask.max = 1f;

        fields.rockinessMask.normalized = true;
        fields.rockinessMask.min = 0f;
        fields.rockinessMask.max = 1f;
    }

    private static float ComputeSlopeApprox(ScalarField heightField, int x, int y)
    {
        var grid = heightField.grid;
        var xPrev = Mathf.Max(0, x - 1);
        var xNext = Mathf.Min(grid.width - 1, x + 1);
        var yPrev = Mathf.Max(0, y - 1);
        var yNext = Mathf.Min(grid.height - 1, y + 1);
        var dx = (heightField[xNext, y] - heightField[xPrev, y]) * 0.5f;
        var dy = (heightField[x, yNext] - heightField[x, yPrev]) * 0.5f;
        return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2.4f);
    }

    private static float DistanceToPolyline(IReadOnlyList<Vector2> points, Vector2 sample)
    {
        if (points == null || points.Count < 2)
            return float.PositiveInfinity;

        var best = float.MaxValue;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < Epsilon) continue;

            var t = Mathf.Clamp01(Vector2.Dot(sample - a, ab) / lenSq);
            var projection = a + ab * t;
            var distance = Vector2.Distance(sample, projection);
            if (distance < best) best = distance;
        }

        return best;
    }
}
