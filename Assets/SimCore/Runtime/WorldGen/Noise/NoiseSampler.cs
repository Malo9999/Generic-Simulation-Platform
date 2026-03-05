using UnityEngine;

public static class NoiseSampler
{
    public static float Sample2D(in NoiseDescriptor desc, float x, float y, int seed)
    {
        var d = desc;
        if (d.frequency <= 0f) d.frequency = 0.0001f;
        if (d.amplitude <= 0f) d.amplitude = 1f;

        if (d.offset == Vector2.zero)
        {
            d.offset = DeriveOffset(seed, d.id);
        }

        var nx = x * d.frequency + d.offset.x;
        var ny = y * d.frequency + d.offset.y;

        if (d.warpType == WarpType.DomainWarp && d.warpAmplitude > 0f)
        {
            var warpFreq = d.warpFrequency <= 0f ? d.frequency * 0.5f : d.warpFrequency;
            var warpOct = Mathf.Max(1, d.warpOctaves);
            var wx = SampleFbmSigned(nx + 17.17f, ny - 4.12f, warpFreq, 2f, 0.5f, warpOct);
            var wy = SampleFbmSigned(nx - 31.73f, ny + 9.48f, warpFreq, 2f, 0.5f, warpOct);
            nx += wx * d.warpAmplitude;
            ny += wy * d.warpAmplitude;
        }

        var value = d.type switch
        {
            NoiseType.Perlin => Mathf.PerlinNoise(nx, ny),
            NoiseType.FBM => SampleFbm01(nx, ny, d.frequency, d.lacunarity, d.gain, d.octaves),
            NoiseType.Ridged => SampleRidged01(nx, ny, d.frequency, d.lacunarity, d.gain, d.octaves),
            NoiseType.Worley => SampleWorley01(nx, ny, seed, d.id),
            _ => Mathf.PerlinNoise(nx, ny)
        };

        return Mathf.Clamp01(value * d.amplitude);
    }

    public static Vector2 DeriveOffset(int seed, string id)
    {
        var h1 = Hash(seed, id, 0x2c9277b5u);
        var h2 = Hash(seed, id, 0x6e624eb7u);
        return new Vector2((h1 & 0xffff) / 17f, (h2 & 0xffff) / 17f);
    }

    private static float SampleFbm01(float x, float y, float baseFreq, float lacunarity, float gain, int octaves)
    {
        var signed = SampleFbmSigned(x, y, 1f, Mathf.Max(1.01f, lacunarity), Mathf.Clamp(gain, 0.01f, 1f), Mathf.Max(1, octaves));
        return Mathf.Clamp01(signed * 0.5f + 0.5f);
    }

    private static float SampleRidged01(float x, float y, float baseFreq, float lacunarity, float gain, int octaves)
    {
        var amp = 1f;
        var freq = 1f;
        var sum = 0f;
        var norm = 0f;
        for (var i = 0; i < Mathf.Max(1, octaves); i++)
        {
            var p = Mathf.PerlinNoise(x * freq, y * freq);
            var ridged = 1f - Mathf.Abs(2f * p - 1f);
            sum += ridged * amp;
            norm += amp;
            amp *= Mathf.Clamp(gain, 0.01f, 1f);
            freq *= Mathf.Max(1.01f, lacunarity);
        }

        return norm <= 0f ? 0.5f : Mathf.Clamp01(sum / norm);
    }

    private static float SampleFbmSigned(float x, float y, float freqMul, float lacunarity, float gain, int octaves)
    {
        var amp = 1f;
        var freq = Mathf.Max(0.0001f, freqMul);
        var sum = 0f;
        var norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            sum += (Mathf.PerlinNoise(x * freq, y * freq) * 2f - 1f) * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }

        return norm <= 0f ? 0f : sum / norm;
    }

    private static float SampleWorley01(float x, float y, int seed, string id)
    {
        var ix = Mathf.FloorToInt(x);
        var iy = Mathf.FloorToInt(y);
        var minDistSq = float.MaxValue;

        for (var oy = -1; oy <= 1; oy++)
        for (var ox = -1; ox <= 1; ox++)
        {
            var cx = ix + ox;
            var cy = iy + oy;
            var px = cx + Hash01(seed, id, cx, cy, 13u);
            var py = cy + Hash01(seed, id, cx, cy, 71u);
            var dx = px - x;
            var dy = py - y;
            var distSq = dx * dx + dy * dy;
            if (distSq < minDistSq) minDistSq = distSq;
        }

        var d = Mathf.Sqrt(minDistSq);
        return Mathf.Clamp01(d / 1.4142f);
    }

    private static float Hash01(int seed, string id, int x, int y, uint salt)
    {
        var h = (uint)seed;
        h ^= (uint)(x * 374761393);
        h ^= (uint)(y * 668265263);
        h = Hash(seed ^ x ^ y, id, salt ^ h);
        return (h & 0xffffff) / 16777215f;
    }

    private static uint Hash(int seed, string id, uint salt)
    {
        unchecked
        {
            uint h = (uint)seed ^ salt;
            if (!string.IsNullOrEmpty(id))
            {
                for (var i = 0; i < id.Length; i++)
                {
                    h ^= id[i];
                    h *= 16777619u;
                    h ^= h >> 16;
                }
            }

            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return h;
        }
    }
}
