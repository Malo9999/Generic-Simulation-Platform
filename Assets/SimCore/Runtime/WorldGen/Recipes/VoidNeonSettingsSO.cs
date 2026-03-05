using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/VoidNeon")]
public class VoidNeonSettingsSO : WorldRecipeSettingsSO
{
    public int railsCount = 3;
    public float railLengthFactor = 0.8f;
    public float railCurvature = 0.18f;
    public float railWidth = 3f;
    public float emitterSpacing = 8f;
    public int marginCells = 2;
    public float glowFalloff = 10f;
    public float noiseScale = 0.03f;
}
