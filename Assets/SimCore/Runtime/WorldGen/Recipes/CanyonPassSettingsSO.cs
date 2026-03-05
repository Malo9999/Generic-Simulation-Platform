using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Settings/CanyonPass")]
public class CanyonPassSettingsSO : WorldRecipeSettingsSO
{
    public float canyonDepth = 0.6f;
    public float wallSteepness = 2.5f;
    public float passWidth = 10f;
    public float passTwist = 0.18f;
    public int chokeCount = 2;
    public float boulderDensity = 0.05f;
}
