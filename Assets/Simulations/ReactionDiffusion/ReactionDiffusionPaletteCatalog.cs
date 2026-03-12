using UnityEngine;

using UnityEngine;

public static class ReactionDiffusionPaletteCatalog
{
    public readonly struct Palette
    {
        public readonly Color baseColor;
        public readonly Color reseedColor;
        public readonly Color hotspotColor;

        public Palette(Color baseColor, Color reseedColor, Color hotspotColor)
        {
            this.baseColor = baseColor;
            this.reseedColor = reseedColor;
            this.hotspotColor = hotspotColor;
        }
    }

    public static Palette Get(ReactionDiffusionPalettePreset preset)
    {
        switch (preset)
        {
            case ReactionDiffusionPalettePreset.Ember:
                return new Palette(
                    new Color(0.98f, 0.98f, 0.98f, 1f),
                    new Color(1.00f, 0.18f, 0.12f, 1f),
                    new Color(1.00f, 0.62f, 0.18f, 1f));

            case ReactionDiffusionPalettePreset.Arctic:
                return new Palette(
                    new Color(0.92f, 0.97f, 1.00f, 1f),
                    new Color(0.18f, 0.78f, 1.00f, 1f),
                    new Color(0.62f, 0.92f, 1.00f, 1f));

            case ReactionDiffusionPalettePreset.Coral:
                return new Palette(
                    new Color(0.98f, 0.96f, 0.92f, 1f),
                    new Color(1.00f, 0.42f, 0.34f, 1f),
                    new Color(0.86f, 0.36f, 0.74f, 1f));

            case ReactionDiffusionPalettePreset.Toxic:
                return new Palette(
                    new Color(0.86f, 1.00f, 0.92f, 1f),
                    new Color(0.24f, 1.00f, 0.42f, 1f),
                    new Color(0.12f, 0.78f, 0.68f, 1f));

            case ReactionDiffusionPalettePreset.RoyalNeon:
                return new Palette(
                    new Color(0.88f, 0.98f, 1.00f, 1f),
                    new Color(1.00f, 0.14f, 0.74f, 1f),
                    new Color(0.36f, 0.52f, 1.00f, 1f));

            case ReactionDiffusionPalettePreset.Volcanic:
                return new Palette(
                    new Color(0.96f, 0.94f, 0.92f, 1f),
                    new Color(1.00f, 0.20f, 0.12f, 1f),
                    new Color(1.00f, 0.78f, 0.20f, 1f));

            case ReactionDiffusionPalettePreset.Moonlight:
                return new Palette(
                    new Color(0.92f, 0.94f, 1.00f, 1f),
                    new Color(0.36f, 0.58f, 1.00f, 1f),
                    new Color(0.58f, 0.36f, 1.00f, 1f));

            case ReactionDiffusionPalettePreset.Monochrome:
            default:
                return new Palette(
                    new Color(1.00f, 1.00f, 1.00f, 1f),
                    new Color(1.00f, 0.30f, 0.30f, 1f),
                    new Color(1.00f, 0.82f, 0.36f, 1f));
        }
    }
}