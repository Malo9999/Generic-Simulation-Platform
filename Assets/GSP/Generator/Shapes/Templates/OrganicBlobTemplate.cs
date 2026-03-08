using UnityEngine;

public enum OrganicBlobMode
{
    Metaball,
    AmoebaNoise,
    AmoebaPseudopod
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

    [Header("Amoeba Pseudopod")]
    [SerializeField] private float coreRadiusPx = 17f;
    [SerializeField] private int armCountMin = 3;
    [SerializeField] private int armCountMax = 6;
    [SerializeField] private float armLengthMinPx = 10f;
    [SerializeField] private float armLengthMaxPx = 24f;
    [SerializeField] private float armWidthMinPx = 2.5f;
    [SerializeField] private float armWidthMaxPx = 7f;
    [SerializeField] private float armTaper = 0.8f;
    [SerializeField] private float armCurvature = 0.3f;
    [SerializeField] private float branchChance = 0.2f;
    [SerializeField] private float branchLengthMul = 0.45f;
    [SerializeField] private float bodyIrregularity = 0.25f;
    [SerializeField] private float edgeJitterPx = 0.6f;
    [SerializeField] private int fillMarginPx = 1;
    [SerializeField] private bool allowInteriorHoles;

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
            Mathf.Max(1f, coreRadiusPx),
            Mathf.Max(1, armCountMin),
            Mathf.Max(1, armCountMax),
            Mathf.Max(1f, armLengthMinPx),
            Mathf.Max(1f, armLengthMaxPx),
            Mathf.Max(1f, armWidthMinPx),
            Mathf.Max(1f, armWidthMaxPx),
            Mathf.Clamp01(armTaper),
            Mathf.Clamp01(armCurvature),
            Mathf.Clamp01(branchChance),
            Mathf.Clamp(branchLengthMul, 0.2f, 0.95f),
            Mathf.Clamp01(bodyIrregularity),
            Mathf.Max(0f, edgeJitterPx),
            Mathf.Clamp(fillMarginPx, 0, 4),
            allowInteriorHoles);
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

    public void ApplyAmoebaPseudopodCrawlerDefaults()
    {
        mode = OrganicBlobMode.AmoebaPseudopod;
        seed = 6121;
        coreRadiusPx = 15f;
        armCountMin = 3;
        armCountMax = 4;
        armLengthMinPx = 16f;
        armLengthMaxPx = 28f;
        armWidthMinPx = 2.4f;
        armWidthMaxPx = 5.4f;
        armTaper = 0.86f;
        armCurvature = 0.34f;
        branchChance = 0.15f;
        branchLengthMul = 0.42f;
        bodyIrregularity = 0.3f;
        edgeJitterPx = 0.55f;
        fillMarginPx = 1;
        allowInteriorHoles = false;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaPseudopodStarDefaults()
    {
        mode = OrganicBlobMode.AmoebaPseudopod;
        seed = 6203;
        coreRadiusPx = 14f;
        armCountMin = 5;
        armCountMax = 7;
        armLengthMinPx = 11f;
        armLengthMaxPx = 20f;
        armWidthMinPx = 2.2f;
        armWidthMaxPx = 5.8f;
        armTaper = 0.78f;
        armCurvature = 0.26f;
        branchChance = 0.22f;
        branchLengthMul = 0.45f;
        bodyIrregularity = 0.2f;
        edgeJitterPx = 0.5f;
        fillMarginPx = 1;
        allowInteriorHoles = false;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaPseudopodBranchDefaults()
    {
        mode = OrganicBlobMode.AmoebaPseudopod;
        seed = 6331;
        coreRadiusPx = 14.5f;
        armCountMin = 3;
        armCountMax = 5;
        armLengthMinPx = 13f;
        armLengthMaxPx = 22f;
        armWidthMinPx = 2.3f;
        armWidthMaxPx = 5.3f;
        armTaper = 0.82f;
        armCurvature = 0.31f;
        branchChance = 0.52f;
        branchLengthMul = 0.52f;
        bodyIrregularity = 0.28f;
        edgeJitterPx = 0.55f;
        fillMarginPx = 1;
        allowInteriorHoles = false;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaPseudopodWideDefaults()
    {
        mode = OrganicBlobMode.AmoebaPseudopod;
        seed = 6419;
        coreRadiusPx = 17f;
        armCountMin = 4;
        armCountMax = 6;
        armLengthMinPx = 9f;
        armLengthMaxPx = 18f;
        armWidthMinPx = 2.8f;
        armWidthMaxPx = 6.4f;
        armTaper = 0.7f;
        armCurvature = 0.2f;
        branchChance = 0.2f;
        branchLengthMul = 0.4f;
        bodyIrregularity = 0.22f;
        edgeJitterPx = 0.45f;
        fillMarginPx = 1;
        allowInteriorHoles = false;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }

    public void ApplyAmoebaPseudopodHunterDefaults()
    {
        mode = OrganicBlobMode.AmoebaPseudopod;
        seed = 6581;
        coreRadiusPx = 13.5f;
        armCountMin = 3;
        armCountMax = 4;
        armLengthMinPx = 15f;
        armLengthMaxPx = 30f;
        armWidthMinPx = 2f;
        armWidthMaxPx = 4.8f;
        armTaper = 0.9f;
        armCurvature = 0.38f;
        branchChance = 0.12f;
        branchLengthMul = 0.4f;
        bodyIrregularity = 0.34f;
        edgeJitterPx = 0.5f;
        fillMarginPx = 1;
        allowInteriorHoles = false;
        useRimGradient = true;
        rimWidthPx = 6;
        innerMul = 1f;
        outerMul = 0.78f;
    }
}
