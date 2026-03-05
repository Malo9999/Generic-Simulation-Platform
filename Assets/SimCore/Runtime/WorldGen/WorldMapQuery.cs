using System.Collections.Generic;
using UnityEngine;

public static class WorldMapQuery
{
    public static bool TryGetSpline(WorldMap map, string id, out WorldSpline spline)
    {
        spline = null;
        if (map?.splines == null || string.IsNullOrEmpty(id)) return false;
        for (var i = 0; i < map.splines.Count; i++)
        {
            if (map.splines[i]?.id != id) continue;
            spline = map.splines[i];
            return true;
        }

        return false;
    }

    public static WorldSpline GetNearestSpline(WorldMap map, Vector2 worldPos)
    {
        if (map?.splines == null) return null;
        WorldSpline nearest = null;
        var bestD2 = float.MaxValue;
        for (var i = 0; i < map.splines.Count; i++)
        {
            var spline = map.splines[i];
            if (spline?.points == null || spline.points.Count < 2) continue;
            var nearestPoint = GetNearestPointOnSpline(spline, worldPos);
            var d2 = (nearestPoint - worldPos).sqrMagnitude;
            if (d2 >= bestD2) continue;
            bestD2 = d2;
            nearest = spline;
        }

        return nearest;
    }

    public static Vector2 GetNearestPointOnSpline(WorldSpline spline, Vector2 worldPos)
    {
        if (spline?.points == null || spline.points.Count == 0) return worldPos;
        var best = spline.points[0];
        var bestD2 = float.MaxValue;
        for (var i = 1; i < spline.points.Count; i++)
        {
            var p = GetNearestPointOnSegment(spline.points[i - 1], spline.points[i], worldPos);
            var d2 = (p - worldPos).sqrMagnitude;
            if (d2 >= bestD2) continue;
            bestD2 = d2;
            best = p;
        }

        return best;
    }

    public static Vector2 GetNearestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        var denom = ab.sqrMagnitude;
        if (denom < 1e-6f) return a;
        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        return a + ab * t;
    }

    public static ScatterPoint GetRandomAnchor(WorldMap map, string scatterId, WorldGenRng rng)
    {
        if (map?.scatters == null || !map.scatters.TryGetValue(scatterId, out var scatter) || scatter.points.Count == 0)
            return default;

        return scatter.points[rng.NextInt(0, scatter.points.Count)];
    }

    public static IEnumerable<ScatterPoint> GetAnchorsInRadius(WorldMap map, Vector2 pos, float r, string scatterId)
    {
        if (map?.scatters == null || !map.scatters.TryGetValue(scatterId, out var scatter) || scatter.points == null) yield break;
        var r2 = r * r;
        for (var i = 0; i < scatter.points.Count; i++)
        {
            var pt = scatter.points[i];
            if ((pt.pos - pos).sqrMagnitude <= r2) yield return pt;
        }
    }

    public static bool IsWalkable(WorldMap map, Vector2 worldPos)
    {
        if (map?.masks == null || !map.masks.TryGetValue("walkable", out var walkable) || walkable == null) return true;

        var local = worldPos - walkable.grid.originWorld;
        var x = Mathf.FloorToInt(local.x / Mathf.Max(0.0001f, walkable.grid.cellSize));
        var y = Mathf.FloorToInt(local.y / Mathf.Max(0.0001f, walkable.grid.cellSize));
        if (!walkable.grid.InBounds(x, y)) return false;
        return walkable[x, y] > 0;
    }
}
