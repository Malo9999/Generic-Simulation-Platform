using System.Collections.Generic;
using UnityEngine;

public static class SplineRasterizer
{
    public static MaskField RasterizeLanes(string id, WorldGridSpec grid, IReadOnlyList<WorldSpline> splines, float extraRadius)
    {
        var mask = new MaskField(id, grid, MaskEncoding.Boolean);
        if (splines == null) return mask;

        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            if (IsNearAnySpline(p, splines, extraRadius)) mask[x, y] = 1;
        }

        return mask;
    }

    private static bool IsNearAnySpline(Vector2 p, IReadOnlyList<WorldSpline> splines, float extraRadius)
    {
        for (var s = 0; s < splines.Count; s++)
        {
            var spline = splines[s];
            if (spline?.points == null || spline.points.Count < 2) continue;
            var radius = Mathf.Max(0f, spline.baseWidth * 0.5f + extraRadius);
            var radiusSq = radius * radius;
            for (var i = 1; i < spline.points.Count; i++)
            {
                var closest = WorldMapQuery.GetNearestPointOnSegment(spline.points[i - 1], spline.points[i], p);
                if ((p - closest).sqrMagnitude <= radiusSq) return true;
            }
        }

        return false;
    }
}
