using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/SavannaRiver")]
public class SavannaRiverSettingsSO : WorldRecipeSettingsSO
{
    public float riverWidth = 5f;
    public float meanderAmp = 0.15f;
    public float meanderFreq = 2f;
    public float floodplainWidth = 12f;
    public float bankWidth = 4f;
    public float treeDensity = 0.08f;
    public float rockDensity = 0.03f;
    public Vector2 gradientDir = new Vector2(0f, 1f);
    public float waterLevel = 0.45f;
    public float carveStrength = 0.35f;
    public float RiverWarpAmplitude = 5f;
    public float RiverWarpFrequency = 0.025f;
    public NoiseDescriptor HeightNoise = NoiseDescriptor.CreateDefault("savanna_height");
    public NoiseDescriptor WetnessNoise = NoiseDescriptor.CreateDefault("savanna_wetness");
    public NoiseDescriptor WarpNoise = NoiseDescriptor.CreateDefault("savanna_warp");
}
