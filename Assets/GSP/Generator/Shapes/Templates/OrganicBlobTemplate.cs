using UnityEngine;

public enum OrganicBlobMode
{
    Metaball,
    AmoebaNoise,
    AmoebaSolidGrowth
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

    [Header("Amoeba Solid Growth")]
    [SerializeField] private int growthSeed = 4703;
    [SerializeField] private float coreRadiusPx = 16f;
    [SerializeField] private int tipCountMin = 4;
    [SerializeField] private int tipCountMax = 6;
    [SerializeField] private int stepCountMin = 12;
    [SerializeField] private int stepCountMax = 26;
    [SerializeField] private float stepLengthPx = 1.5f;
    [SerializeField] private float headingPersistence = 0.78f;
    [SerializeField] private float outwardBias = 0.85f;
    [SerializeField] private float noiseTurnStrength = 0.5f;
    [SerializeField] private float branchChance = 0.2f;
    [SerializeField] private float branchStepFraction = 0.52f;
    [SerializeField] private float thicknessStartPx = 5.4f;
    [SerializeField] private float thicknessEndPx = 1.6f;
    [SerializeField] private float coreBlendBoost = 2f;
    [SerializeField] private float edgeJitterPx = 0.45f;
    [SerializeField] private bool fillInteriorHoles = true;
    [SerializeField] private bool keepLargestComponent = true;
    [SerializeField] private bool discardSmallIslands = true;

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
            Mathf.Max(0f, outerMul),
            growthSeed,
            Mathf.Max(2f, coreRadiusPx),
            Mathf.Max(1, tipCountMin),
            Mathf.Max(1, tipCountMax),
            Mathf.Max(3, stepCountMin),
            Mathf.Max(3, stepCountMax),
            Mathf.Max(0.5f, stepLengthPx),
            Mathf.Clamp01(headingPersistence),
            Mathf.Clamp(outwardBias, 0f, 2f),
            Mathf.Clamp(noiseTurnStrength, 0f, 1.5f),
            Mathf.Clamp01(branchChance),
            Mathf.Clamp(branchStepFraction, 0.2f, 0.95f),
            Mathf.Max(1f, thicknessStartPx),
            Mathf.Max(0.8f, thicknessEndPx),
            Mathf.Max(0f, coreBlendBoost),
            Mathf.Max(0f, edgeJitterPx),
            fillInteriorHoles,
            keepLargestComponent,
            discardSmallIslands);
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

    public void ApplyAmoebaCrawlerDefaults()
    {
        mode = OrganicBlobMode.AmoebaSolidGrowth;
        growthSeed = 6121;
        coreRadiusPx = 14.6f;
        tipCountMin = 3;
        tipCountMax = 4;
        stepCountMin = 18;
        stepCountMax = 30;
        stepLengthPx = 1.45f;
        headingPersistence = 0.84f;
        outwardBias = 1.2f;
        noiseTurnStrength = 0.3f;
        branchChance = 0.1f;
        branchStepFraction = 0.44f;
        thicknessStartPx = 5.4f;
        thicknessEndPx = 1.4f;
        coreBlendBoost = 1.7f;
        edgeJitterPx = 0.35f;
        fillInteriorHoles = true;
        keepLargestComponent = true;
        discardSmallIslands = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaStarDefaults()
    {
        mode = OrganicBlobMode.AmoebaSolidGrowth;
        growthSeed = 6203;
        coreRadiusPx = 13.8f;
        tipCountMin = 5;
        tipCountMax = 7;
        stepCountMin = 14;
        stepCountMax = 24;
        stepLengthPx = 1.35f;
        headingPersistence = 0.8f;
        outwardBias = 0.95f;
        noiseTurnStrength = 0.38f;
        branchChance = 0.2f;
        branchStepFraction = 0.5f;
        thicknessStartPx = 5f;
        thicknessEndPx = 1.55f;
        coreBlendBoost = 1.8f;
        edgeJitterPx = 0.4f;
        fillInteriorHoles = true;
        keepLargestComponent = true;
        discardSmallIslands = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaBranchDefaults()
    {
        mode = OrganicBlobMode.AmoebaSolidGrowth;
        growthSeed = 6331;
        coreRadiusPx = 14.4f;
        tipCountMin = 3;
        tipCountMax = 5;
        stepCountMin = 15;
        stepCountMax = 25;
        stepLengthPx = 1.35f;
        headingPersistence = 0.74f;
        outwardBias = 0.9f;
        noiseTurnStrength = 0.54f;
        branchChance = 0.46f;
        branchStepFraction = 0.56f;
        thicknessStartPx = 5.2f;
        thicknessEndPx = 1.3f;
        coreBlendBoost = 1.9f;
        edgeJitterPx = 0.45f;
        fillInteriorHoles = true;
        keepLargestComponent = true;
        discardSmallIslands = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaWideArmsDefaults()
    {
        mode = OrganicBlobMode.AmoebaSolidGrowth;
        growthSeed = 6419;
        coreRadiusPx = 17f;
        tipCountMin = 4;
        tipCountMax = 6;
        stepCountMin = 11;
        stepCountMax = 19;
        stepLengthPx = 1.35f;
        headingPersistence = 0.83f;
        outwardBias = 0.82f;
        noiseTurnStrength = 0.25f;
        branchChance = 0.15f;
        branchStepFraction = 0.4f;
        thicknessStartPx = 5.9f;
        thicknessEndPx = 1.8f;
        coreBlendBoost = 2.2f;
        edgeJitterPx = 0.35f;
        fillInteriorHoles = true;
        keepLargestComponent = true;
        discardSmallIslands = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaHunterDefaults()
    {
        mode = OrganicBlobMode.AmoebaSolidGrowth;
        growthSeed = 6581;
        coreRadiusPx = 13.2f;
        tipCountMin = 3;
        tipCountMax = 4;
        stepCountMin = 20;
        stepCountMax = 34;
        stepLengthPx = 1.55f;
        headingPersistence = 0.82f;
        outwardBias = 1.28f;
        noiseTurnStrength = 0.36f;
        branchChance = 0.12f;
        branchStepFraction = 0.42f;
        thicknessStartPx = 4.9f;
        thicknessEndPx = 1.1f;
        coreBlendBoost = 1.6f;
        edgeJitterPx = 0.32f;
        fillInteriorHoles = true;
        keepLargestComponent = true;
        discardSmallIslands = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }
}
