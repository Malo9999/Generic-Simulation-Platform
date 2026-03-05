using UnityEngine;

public static class NoiseUtil
{
    public static float Sample2D(NoiseDescriptor descriptor, float x, float y)
    {
        var nx = x * descriptor.scale + descriptor.offset.x;
        var ny = y * descriptor.scale + descriptor.offset.y;

        if (descriptor.domainWarp.enabled)
        {
            var wx = Mathf.PerlinNoise(nx * descriptor.domainWarp.frequency, ny * descriptor.domainWarp.frequency) - 0.5f;
            var wy = Mathf.PerlinNoise((nx + 123.4f) * descriptor.domainWarp.frequency, (ny + 456.7f) * descriptor.domainWarp.frequency) - 0.5f;
            nx += wx * descriptor.domainWarp.amplitude;
            ny += wy * descriptor.domainWarp.amplitude;
        }

        switch (descriptor.type)
        {
            case NoiseType.Perlin:
                return Mathf.PerlinNoise(nx, ny);
            case NoiseType.FBM:
                return SampleFbm(descriptor, nx, ny);
            default:
                return Mathf.PerlinNoise(nx, ny);
        }
    }

    private static float SampleFbm(NoiseDescriptor descriptor, float x, float y)
    {
        var amp = 1f;
        var freq = 1f;
        var sum = 0f;
        var norm = 0f;
        var octaves = Mathf.Max(1, descriptor.octaves);
        for (var i = 0; i < octaves; i++)
        {
            sum += (Mathf.PerlinNoise(x * freq, y * freq) * 2f - 1f) * amp;
            norm += amp;
            amp *= descriptor.gain;
            freq *= descriptor.lacunarity;
        }

        if (norm <= 0f)
        {
            return 0.5f;
        }

        return Mathf.Clamp01(sum / norm * 0.5f + 0.5f);
    }
}
