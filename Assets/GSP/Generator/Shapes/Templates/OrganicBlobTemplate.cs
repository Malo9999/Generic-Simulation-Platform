using UnityEngine;

public enum OrganicBlobMode
{
    Metaball,
    AmoebaNoise,
    AmoebaVectorField
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

    [Header("Amoeba Vector Field")]
    [SerializeField] private int vfSeed = 4703;
    [SerializeField] private float vfBodyRadiusPx = 16f;
    [SerializeField] private int vfTipCountMin = 4;
    [SerializeField] private int vfTipCountMax = 6;
    [SerializeField] private int vfStepCountMin = 12;
    [SerializeField] private int vfStepCountMax = 26;
    [SerializeField] private float vfStepLengthPx = 1.5f;
    [SerializeField] private float vfTurnStrength = 0.27f;
    [SerializeField] private float vfOutwardBias = 0.85f;
    [SerializeField] private float vfNoiseInfluence = 0.5f;
    [SerializeField] private float vfBranchChance = 0.2f;
    [SerializeField] private float vfBranchAngleDeg = 34f;
    [SerializeField] private float vfBranchLengthMul = 0.52f;
    [SerializeField] private float vfThicknessStartPx = 5.4f;
    [SerializeField] private float vfThicknessEndPx = 1.6f;
    [SerializeField] private float vfCoreThicknessBoost = 2f;
    [SerializeField] private float vfEdgeJitterPx = 0.45f;
    [SerializeField] private bool vfHoleFill = true;
    [SerializeField] private bool vfKeepLargestComponent = true;

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
            vfSeed,
            Mathf.Max(2f, vfBodyRadiusPx),
            Mathf.Max(1, vfTipCountMin),
            Mathf.Max(1, vfTipCountMax),
            Mathf.Max(3, vfStepCountMin),
            Mathf.Max(3, vfStepCountMax),
            Mathf.Max(0.5f, vfStepLengthPx),
            Mathf.Clamp(vfTurnStrength, 0f, 1.2f),
            Mathf.Clamp(vfOutwardBias, 0f, 2f),
            Mathf.Clamp(vfNoiseInfluence, 0f, 1.5f),
            Mathf.Clamp01(vfBranchChance),
            Mathf.Clamp(vfBranchAngleDeg, 5f, 85f),
            Mathf.Clamp(vfBranchLengthMul, 0.2f, 0.95f),
            Mathf.Max(1f, vfThicknessStartPx),
            Mathf.Max(0.8f, vfThicknessEndPx),
            Mathf.Max(0f, vfCoreThicknessBoost),
            Mathf.Max(0f, vfEdgeJitterPx),
            vfHoleFill,
            vfKeepLargestComponent);
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

    public void ApplyAmoebaVectorCrawlerDefaults()
    {
        mode = OrganicBlobMode.AmoebaVectorField;
        vfSeed = 6121;
        vfBodyRadiusPx = 14.6f;
        vfTipCountMin = 3;
        vfTipCountMax = 4;
        vfStepCountMin = 18;
        vfStepCountMax = 30;
        vfStepLengthPx = 1.45f;
        vfTurnStrength = 0.2f;
        vfOutwardBias = 1.2f;
        vfNoiseInfluence = 0.3f;
        vfBranchChance = 0.1f;
        vfBranchAngleDeg = 26f;
        vfBranchLengthMul = 0.44f;
        vfThicknessStartPx = 5.4f;
        vfThicknessEndPx = 1.4f;
        vfCoreThicknessBoost = 1.7f;
        vfEdgeJitterPx = 0.35f;
        vfHoleFill = true;
        vfKeepLargestComponent = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaVectorStarDefaults()
    {
        mode = OrganicBlobMode.AmoebaVectorField;
        vfSeed = 6203;
        vfBodyRadiusPx = 13.8f;
        vfTipCountMin = 5;
        vfTipCountMax = 7;
        vfStepCountMin = 14;
        vfStepCountMax = 24;
        vfStepLengthPx = 1.35f;
        vfTurnStrength = 0.22f;
        vfOutwardBias = 0.95f;
        vfNoiseInfluence = 0.38f;
        vfBranchChance = 0.2f;
        vfBranchAngleDeg = 34f;
        vfBranchLengthMul = 0.5f;
        vfThicknessStartPx = 5f;
        vfThicknessEndPx = 1.55f;
        vfCoreThicknessBoost = 1.8f;
        vfEdgeJitterPx = 0.4f;
        vfHoleFill = true;
        vfKeepLargestComponent = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaVectorBranchDefaults()
    {
        mode = OrganicBlobMode.AmoebaVectorField;
        vfSeed = 6331;
        vfBodyRadiusPx = 14.4f;
        vfTipCountMin = 3;
        vfTipCountMax = 5;
        vfStepCountMin = 15;
        vfStepCountMax = 25;
        vfStepLengthPx = 1.35f;
        vfTurnStrength = 0.3f;
        vfOutwardBias = 0.9f;
        vfNoiseInfluence = 0.54f;
        vfBranchChance = 0.46f;
        vfBranchAngleDeg = 38f;
        vfBranchLengthMul = 0.56f;
        vfThicknessStartPx = 5.2f;
        vfThicknessEndPx = 1.3f;
        vfCoreThicknessBoost = 1.9f;
        vfEdgeJitterPx = 0.45f;
        vfHoleFill = true;
        vfKeepLargestComponent = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaVectorWideDefaults()
    {
        mode = OrganicBlobMode.AmoebaVectorField;
        vfSeed = 6419;
        vfBodyRadiusPx = 17f;
        vfTipCountMin = 4;
        vfTipCountMax = 6;
        vfStepCountMin = 11;
        vfStepCountMax = 19;
        vfStepLengthPx = 1.35f;
        vfTurnStrength = 0.2f;
        vfOutwardBias = 0.82f;
        vfNoiseInfluence = 0.25f;
        vfBranchChance = 0.15f;
        vfBranchAngleDeg = 28f;
        vfBranchLengthMul = 0.4f;
        vfThicknessStartPx = 5.9f;
        vfThicknessEndPx = 1.8f;
        vfCoreThicknessBoost = 2.2f;
        vfEdgeJitterPx = 0.35f;
        vfHoleFill = true;
        vfKeepLargestComponent = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaVectorHunterDefaults()
    {
        mode = OrganicBlobMode.AmoebaVectorField;
        vfSeed = 6581;
        vfBodyRadiusPx = 13.2f;
        vfTipCountMin = 3;
        vfTipCountMax = 4;
        vfStepCountMin = 20;
        vfStepCountMax = 34;
        vfStepLengthPx = 1.55f;
        vfTurnStrength = 0.24f;
        vfOutwardBias = 1.28f;
        vfNoiseInfluence = 0.36f;
        vfBranchChance = 0.12f;
        vfBranchAngleDeg = 24f;
        vfBranchLengthMul = 0.42f;
        vfThicknessStartPx = 4.9f;
        vfThicknessEndPx = 1.1f;
        vfCoreThicknessBoost = 1.6f;
        vfEdgeJitterPx = 0.32f;
        vfHoleFill = true;
        vfKeepLargestComponent = true;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }
}
