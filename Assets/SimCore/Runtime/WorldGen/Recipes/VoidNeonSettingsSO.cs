using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/VoidNeon")]
public class VoidNeonSettingsSO : WorldRecipeSettingsSO
{
    public int nodeCount = 48;
    public float nodeMinDist = 8f;
    public int kNearest = 3;
    public float edgeWidthMin = 2.5f;
    public float edgeWidthMax = 5.5f;
    public float organicJitter = 0.22f;
    public int smoothIterations = 1;
    public int marginCells = 2;
    public float anchorSpacing = 6f;
    public float glowFalloff = 10f;
    public float noiseScale = 0.03f;
    public NoiseDescriptor HeightNoise = NoiseDescriptor.CreateDefault("voidneon_height");
    public NoiseDescriptor WetnessNoise = NoiseDescriptor.CreateDefault("voidneon_wetness");
    public NoiseDescriptor WarpNoise = NoiseDescriptor.CreateDefault("voidneon_warp");
    public NoiseDescriptor GlowNoise = NoiseDescriptor.CreateDefault("voidneon_glow");
}
