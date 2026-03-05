using System;
using System.Collections.Generic;
using UnityEngine;

public enum NoiseType
{
    Value = 0,
    Perlin = 1,
    FBM = 2,
    Ridged = 3,
    Worley = 4
}

[Serializable]
public struct DomainWarpDescriptor
{
    public bool enabled;
    public float amplitude;
    public float frequency;
}

[Serializable]
public class NoiseDescriptor
{
    public string id;
    public NoiseType type = NoiseType.Perlin;
    public float scale = 0.01f;
    public int octaves = 4;
    public float lacunarity = 2f;
    public float gain = 0.5f;
    public Vector2 offset;
    public DomainWarpDescriptor domainWarp;
}

[Serializable]
public class NoiseDescriptorSet
{
    public List<NoiseDescriptor> descriptors = new List<NoiseDescriptor>();

    public NoiseDescriptor GetOrCreate(string id, WorldGenRng rng)
    {
        for (var i = 0; i < descriptors.Count; i++)
        {
            if (descriptors[i].id == id)
            {
                return descriptors[i];
            }
        }

        var d = new NoiseDescriptor
        {
            id = id,
            type = NoiseType.FBM,
            offset = new Vector2(rng.NextFloat01() * 10000f, rng.NextFloat01() * 10000f)
        };
        descriptors.Add(d);
        return d;
    }
}
