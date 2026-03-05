using System;
using UnityEngine;

[Serializable]
public struct NoiseDescriptor
{
    public string id;
    public NoiseType type;
    public int octaves;
    public float frequency;
    public float lacunarity;
    public float gain;
    public float amplitude;
    public Vector2 offset;
    public WarpType warpType;
    public float warpAmplitude;
    public float warpFrequency;
    public int warpOctaves;

    public static NoiseDescriptor CreateDefault(string id)
    {
        return new NoiseDescriptor
        {
            id = id,
            type = NoiseType.FBM,
            octaves = 4,
            frequency = 0.02f,
            lacunarity = 2f,
            gain = 0.5f,
            amplitude = 1f,
            warpType = WarpType.None,
            warpAmplitude = 0f,
            warpFrequency = 0.03f,
            warpOctaves = 2
        };
    }
}
