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

    public static WorldSpline GetNearestSpline(WorldMapRuntime map, Vector2 worldPos)
    {
        if (map?.splines == null) return null;

        WorldSpline nearest = null;
        var bestD2 = float.MaxValue;
        foreach (var pair in map.splines)
        {
            var spline = pair.Value;
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

    public static ScatterPoint GetRandomAnchor(WorldMapRuntime map, string scatterId, System.Random rng)
    {
        if (map?.scatters == null || rng == null || !map.scatters.TryGetValue(scatterId, out var scatter) || scatter?.points == null || scatter.points.Count == 0)
            return default;

        return scatter.points[rng.Next(0, scatter.points.Count)];
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
        return IsWalkable(walkable, worldPos);
    }

    public static bool IsWalkable(WorldMapRuntime map, Vector2 worldPos)
    {
        if (map?.masks == null || !map.masks.TryGetValue("walkable", out var walkable) || walkable == null) return true;
        return IsWalkable(walkable, worldPos);
    }

    public static int GetZoneId(WorldMapRuntime map, Vector2 worldPos)
    {
        if (map?.masks == null || !map.masks.TryGetValue("zones", out var zones) || zones == null) return 0;
        if (!TryGetCell(zones.grid, worldPos, out var x, out var y)) return -1;
        return zones[x, y];
    }

    public static ScatterPoint GetNearestNode(WorldMapRuntime map, Vector2 worldPos)
    {
        if (map?.scatters == null || !map.scatters.TryGetValue("anchors_nodes", out var nodes) || nodes?.points == null || nodes.points.Count == 0)
            return default;

        var index = GetNearestPointIndex(nodes.points, worldPos);
        return index >= 0 ? nodes.points[index] : default;
    }

    public static int GetNearestNodeIndex(WorldMapRuntime map, Vector2 worldPos)
    {
        if (map?.scatters == null || !map.scatters.TryGetValue("anchors_nodes", out var nodes) || nodes?.points == null || nodes.points.Count == 0)
            return -1;
        return GetNearestPointIndex(nodes.points, worldPos);
    }

    private static int GetNearestPointIndex(List<ScatterPoint> points, Vector2 worldPos)
    {
        var best = -1;
        var bestD2 = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var d2 = (points[i].pos - worldPos).sqrMagnitude;
            if (d2 >= bestD2) continue;
            bestD2 = d2;
            best = i;
        }

        return best;
    }

    private static bool IsWalkable(MaskField walkable, Vector2 worldPos)
    {
        if (!TryGetCell(walkable.grid, worldPos, out var x, out var y)) return false;
        return walkable[x, y] > 0;
    }

    public static bool TryGetCell(WorldGridSpec grid, Vector2 worldPos, out int x, out int y)
    {
        var local = worldPos - grid.originWorld;
        x = Mathf.FloorToInt(local.x / Mathf.Max(0.0001f, grid.cellSize));
        y = Mathf.FloorToInt(local.y / Mathf.Max(0.0001f, grid.cellSize));
        return grid.InBounds(x, y);
    }
}
