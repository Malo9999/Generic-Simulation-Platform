using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/SavannaRiver")]
public class SavannaRiverSettingsSO : WorldRecipeSettingsSO
{
    public QualityMode qualityMode = QualityMode.FastPreview;

    [Header("Baseline (active)")]
    public float riverWidth = 5f;
    public float floodplainWidth = 12f;
    public float bankWidth = 4f;
    public float valleyWidth = 26f;
    public float valleyDepth = 0.22f;
    public float treeDensity = 0.08f;
    public float rockDensity = 0.03f;
    public Vector2 gradientDir = new Vector2(0f, 1f);
    public float waterLevel = 0.45f;
    public float carveStrength = 0.35f;
    public float MeanderAmp = 1.15f; // Flow / Meander Strength
    public float MeanderFreq = 1.6f; // Meander Frequency (cycles across map)
    public float RiverWarpAmplitude = 0.9f; // River Warp Strength
    public float RiverWarpFrequency = 0.025f;
    public float WidthVariationStrength = 0.2f;
    public float CurvatureNoiseStrength = 0.3f;
    public float heightNoiseStrength = 1f;
    [Header("Quality Richness (NormalPreview/Bake only)")]
    [Tooltip("FastPreview always stays single-river baseline. Tributaries are only considered in NormalPreview/Bake.")]
    [Range(0, 1)] public int TributaryCount = 1;
    public float TributaryWidthFactor = 0.52f;
    public float TributaryMeanderFactor = 0.62f;
    [Tooltip("FastPreview ignores cutoff/oxbow generation. NormalPreview/Bake can apply at most one valid cutoff.")]
    [Range(0f, 1f)] public float CutoffChance = 0.18f;
    public float OxbowWidthFactor = 0.45f;
    [Range(0f, 1f)] public float TerraceStrength = 0.28f;
    [Range(1, 8)] public int TerraceCount = 4;
    [Range(0f, 1f)] public float BankRoughnessStrength = 0.25f;

    public NoiseDescriptor HeightNoise = NoiseDescriptor.CreateDefault("savanna_height");
    public NoiseDescriptor WetnessNoise = NoiseDescriptor.CreateDefault("savanna_wetness");
    public NoiseDescriptor WarpNoise = NoiseDescriptor.CreateDefault("savanna_warp");

    [Header("Advanced (serialized for compatibility, ignored by baseline)")]
    public float SourceEdgeBias = 0.75f;
    public float FlowInertia = 0.55f;
    public float FlowNoiseStrength = 0.22f;
    public float WidthVariation = 0.55f;
    public int SideChannelCount = 1;
    public float SideChannelWidthFactor = 0.6f;
    public int WetlandCount = 3;
    public float WetlandNoiseStrength = 0.5f;
    public int KopjeCount = 3;
    public float KopjeNoiseStrength = 0.5f;
    public float HeightContrast = 1.25f;
    public float HeightGamma = 0.95f;
    public float WetnessContrast = 1.15f;
    public float WetnessGamma = 0.9f;
}
