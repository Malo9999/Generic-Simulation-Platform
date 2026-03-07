using System.Collections.Generic;
using UnityEngine;

public static class PoissonDisk2D
{
    public static List<Vector2> Sample(Rect bounds, float minDist, int maxPoints, WorldGenRng rng)
    {
        var samples = new List<Vector2>();
        if (maxPoints <= 0 || bounds.width <= 0f || bounds.height <= 0f) return samples;

        minDist = Mathf.Max(0.01f, minDist);
        var cell = minDist / Mathf.Sqrt(2f);
        var gridW = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cell));
        var gridH = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cell));
        var grid = new int[gridW * gridH];
        for (var i = 0; i < grid.Length; i++) grid[i] = -1;

        Vector2 RandomInBounds()
        {
            return new Vector2(
                bounds.xMin + rng.NextFloat01() * bounds.width,
                bounds.yMin + rng.NextFloat01() * bounds.height);
        }

        var first = RandomInBounds();
        samples.Add(first);
        var active = new List<int> { 0 };
        PlaceInGrid(first, 0);

        while (active.Count > 0 && samples.Count < maxPoints)
        {
            var ai = active[rng.NextInt(0, active.Count)];
            var center = samples[ai];
            var found = false;

            for (var attempt = 0; attempt < 24; attempt++)
            {
                var ang = rng.NextFloat01() * Mathf.PI * 2f;
                var dist = minDist * (1f + rng.NextFloat01());
                var candidate = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                if (!bounds.Contains(candidate) || !IsValid(candidate)) continue;

                var idx = samples.Count;
                samples.Add(candidate);
                active.Add(idx);
                PlaceInGrid(candidate, idx);
                found = true;
                break;
            }

            if (!found)
            {
                active.Remove(ai);
            }
        }

        return samples;

        bool IsValid(Vector2 point)
        {
            var gx = Mathf.Clamp((int)((point.x - bounds.xMin) / cell), 0, gridW - 1);
            var gy = Mathf.Clamp((int)((point.y - bounds.yMin) / cell), 0, gridH - 1);
            for (var y = Mathf.Max(0, gy - 2); y <= Mathf.Min(gridH - 1, gy + 2); y++)
            for (var x = Mathf.Max(0, gx - 2); x <= Mathf.Min(gridW - 1, gx + 2); x++)
            {
                var si = grid[y * gridW + x];
                if (si < 0) continue;
                if ((samples[si] - point).sqrMagnitude < minDist * minDist) return false;
            }

            return true;
        }

        void PlaceInGrid(Vector2 point, int sampleIndex)
        {
            var gx = Mathf.Clamp((int)((point.x - bounds.xMin) / cell), 0, gridW - 1);
            var gy = Mathf.Clamp((int)((point.y - bounds.yMin) / cell), 0, gridH - 1);
            grid[gy * gridW + gx] = sampleIndex;
        }
    }
}
