using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Noise Blob", fileName = "NoiseBlobTemplate")]
public class NoiseBlobTemplate : ShapeTemplateBase
{
    [SerializeField] private int seed = 2024;
    [SerializeField] private int baseRadiusPx = 22;
    [SerializeField] private float amplitudePx = 2f;
    [SerializeField] private float frequency = 1.2f;
    [SerializeField] private int octaves = 2;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private float gain = 0.5f;

    [SerializeField] private bool useRimGradient = true;
    [SerializeField] private int rimWidthPx = 6;
    [SerializeField] private float innerMul = 1f;
    [SerializeField] private float outerMul = 0.78f;

    private void Reset()
    {
        ConfigureBase(ShapeId.NoiseBlob, "Blobs", 96, 16);
        seed = 2024;
        baseRadiusPx = 22;
        amplitudePx = 2f;
        frequency = 1.2f;
        octaves = 2;
        lacunarity = 2f;
        gain = 0.5f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeOrganic(
            TextureSize,
            tint,
            OrganicBlobMode.AmoebaNoise,
            seed,
            4,
            18f,
            0f,
            Mathf.Max(1, baseRadiusPx),
            Mathf.Max(0f, amplitudePx),
            Mathf.Max(0f, frequency),
            Mathf.Max(1, octaves),
            Mathf.Max(1f, lacunarity),
            Mathf.Clamp01(gain),
            0,
            0.2f,
            useRimGradient,
            Mathf.Max(1, rimWidthPx),
            Mathf.Max(0f, innerMul),
            Mathf.Max(0f, outerMul));
    }
}
