using UnityEngine;

public static class PheromoneOverlayProfile
{
    public static void Apply(FieldOverlaySettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.width = 256;
        settings.height = 144;
        settings.decayPerSecond = 0.80f;
        settings.diffuseStrength = 0.05f;
        settings.intensity = 1f;
        settings.alphaMultiplier = 0.35f;
        settings.blendMode = FieldOverlayBlendMode.Alpha;
        settings.tintLow = FieldOverlayPalette.DefaultLow;
        settings.tintHigh = FieldOverlayPalette.PheromoneCyan;
    }
}
