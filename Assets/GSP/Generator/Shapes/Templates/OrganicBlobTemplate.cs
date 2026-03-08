using UnityEngine;

public enum OrganicBlobMode
{
    Metaball,
    AmoebaNoise
}

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Organic Blob", fileName = "OrganicBlobTemplate")]
public class OrganicBlobTemplate : ShapeTemplateBase
{
    [SerializeField] private OrganicBlobMode mode = OrganicBlobMode.Metaball;
    [SerializeField] private int seed = 1337;
    [SerializeField] private int lobeCount = 4;
    [SerializeField] private float radiusPx = 18f;
    [SerializeField] private float jitterPx = 8f;

    [Header("Amoeba Noise")]
    [SerializeField] private int baseRadiusPx = 18;
    [SerializeField] private float noiseAmplitudePx = 4f;
    [SerializeField] private float noiseFrequency = 2.5f;
    [SerializeField] private int noiseOctaves = 3;
    [SerializeField] private float noiseLacunarity = 2f;
    [SerializeField] private float noiseGain = 0.5f;
    [SerializeField] private int rimSoftnessPx;
    [SerializeField] private float symmetryBreak = 0.35f;

    [Header("Rim Gradient")]
    [SerializeField] private bool useRimGradient = true;
    [SerializeField] private int rimWidthPx = 6;
    [SerializeField] private float innerMul = 1f;
    [SerializeField] private float outerMul = 0.78f;

    private void Reset()
    {
        ConfigureBase(ShapeId.OrganicMetaball, "Blobs", 96, 16);
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeOrganic(
            TextureSize,
            tint,
            mode,
            seed,
            Mathf.Max(2, lobeCount),
            radiusPx,
            jitterPx,
            Mathf.Max(1, baseRadiusPx),
            Mathf.Max(0f, noiseAmplitudePx),
            Mathf.Max(0f, noiseFrequency),
            Mathf.Max(1, noiseOctaves),
            Mathf.Max(1f, noiseLacunarity),
            Mathf.Clamp01(noiseGain),
            Mathf.Max(0, rimSoftnessPx),
            Mathf.Clamp01(symmetryBreak),
            useRimGradient,
            Mathf.Max(1, rimWidthPx),
            Mathf.Max(0f, innerMul),
            Mathf.Max(0f, outerMul));
    }

    public void ApplyMetaballDefaults()
    {
        mode = OrganicBlobMode.Metaball;
        seed = 1337;
        lobeCount = 4;
        radiusPx = 18f;
        jitterPx = 8f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 999;
        baseRadiusPx = 18;
        noiseAmplitudePx = 5f;
        noiseFrequency = 3f;
        noiseOctaves = 3;
        noiseGain = 0.5f;
        noiseLacunarity = 2f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.35f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }
}
