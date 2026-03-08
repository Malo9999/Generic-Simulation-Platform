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


    public void ApplyAmoebaWideDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 1301;
        baseRadiusPx = 20;
        noiseAmplitudePx = 3.8f;
        noiseFrequency = 2.15f;
        noiseOctaves = 3;
        noiseGain = 0.48f;
        noiseLacunarity = 1.9f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.3f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaLobedDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 2237;
        baseRadiusPx = 18;
        noiseAmplitudePx = 6.6f;
        noiseFrequency = 3.35f;
        noiseOctaves = 3;
        noiseGain = 0.54f;
        noiseLacunarity = 2.05f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.4f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaSprawlDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 3541;
        baseRadiusPx = 19;
        noiseAmplitudePx = 8.2f;
        noiseFrequency = 2.75f;
        noiseOctaves = 4;
        noiseGain = 0.58f;
        noiseLacunarity = 2.1f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.62f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaPseudopodDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 4703;
        baseRadiusPx = 17;
        noiseAmplitudePx = 7.1f;
        noiseFrequency = 2.05f;
        noiseOctaves = 3;
        noiseGain = 0.55f;
        noiseLacunarity = 2f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.66f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaCompactDefaults()
    {
        mode = OrganicBlobMode.AmoebaNoise;
        seed = 5813;
        baseRadiusPx = 16;
        noiseAmplitudePx = 2.9f;
        noiseFrequency = 2.8f;
        noiseOctaves = 2;
        noiseGain = 0.45f;
        noiseLacunarity = 1.85f;
        rimSoftnessPx = 0;
        symmetryBreak = 0.2f;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }
}
