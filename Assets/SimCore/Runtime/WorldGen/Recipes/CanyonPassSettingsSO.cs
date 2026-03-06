using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/CanyonPass")]
public class CanyonPassSettingsSO : WorldRecipeSettingsSO
{
    public QualityMode qualityMode = QualityMode.FastPreview;

    [Header("Baseline (active)")]
    public float canyonDepth = 0.6f;
    public float wallSteepness = 2.5f;
    public float passWidth = 10f;
    public float boulderDensity = 0.05f;
    public float noiseStrength = 0.2f;
    public float wallRoughness = 0.35f;
    public Vector2 gradientDir = new Vector2(1f, 0f);
    public float TwistAmplitude = 3f;
    public float MeanderFrequency = 0.05f;
    public float WarpAmplitude = 2f;
    public float WarpFrequency = 0.03f;

    public NoiseDescriptor HeightNoise = NoiseDescriptor.CreateDefault("canyon_height");
    public NoiseDescriptor WetnessNoise = NoiseDescriptor.CreateDefault("canyon_wetness");
    public NoiseDescriptor WarpNoise = NoiseDescriptor.CreateDefault("canyon_warp");

    [Header("Advanced (serialized for compatibility, ignored by baseline)")]
    public float PathNoiseStrength = 0.25f;
    public int chokeCount = 2;
    public int BasinCount = 1;
    public int SideGullyCount = 1;
    public float asymmetryStrength = 0.28f;
    public float erosionStrength = 0.22f;
    public float FlowInertia = 0.58f;
    public float WidthVariation = 0.65f;
    public float HeightContrast = 1.3f;
    public float HeightGamma = 0.92f;
}
