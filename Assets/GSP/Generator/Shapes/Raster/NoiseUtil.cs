using UnityEngine;

public static class RasterNoiseUtil
{
    public static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = seed;
            h = (h * 397) ^ x;
            h = (h * 397) ^ y;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    public static float ValueNoise01(float x, float y, int seed)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        var tx = x - x0;
        var ty = y - y0;

        var a = Hash01(x0, y0, seed);
        var b = Hash01(x1, y0, seed);
        var c = Hash01(x0, y1, seed);
        var d = Hash01(x1, y1, seed);

        var sx = tx * tx * (3f - (2f * tx));
        var sy = ty * ty * (3f - (2f * ty));

        var nx0 = Mathf.Lerp(a, b, sx);
        var nx1 = Mathf.Lerp(c, d, sx);
        return Mathf.Lerp(nx0, nx1, sy);
    }
}
