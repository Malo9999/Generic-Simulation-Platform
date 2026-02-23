using UnityEngine;

public readonly struct AntSpeciesPalette
{
    public readonly Color32 outlineColor;
    public readonly Color32 baseColor;
    public readonly Color32 shadowColor;
    public readonly Color32 highlightColor;
    public readonly Color32 stripeColor;

    public AntSpeciesPalette(Color32 outlineColor, Color32 baseColor, Color32 shadowColor, Color32 highlightColor, Color32 stripeColor)
    {
        this.outlineColor = outlineColor;
        this.baseColor = baseColor;
        this.shadowColor = shadowColor;
        this.highlightColor = highlightColor;
        this.stripeColor = stripeColor;
    }
}

public static class AntSpeciesPaletteResolver
{
    public static AntSpeciesPalette Resolve(string baseColorId)
    {
        return (baseColorId ?? string.Empty).ToLowerInvariant() switch
        {
            "red" => new AntSpeciesPalette(new Color32(52, 27, 22, 255), new Color32(156, 67, 52, 255), new Color32(111, 43, 33, 255), new Color32(194, 101, 80, 255), new Color32(232, 199, 154, 255)),
            "brown" => new AntSpeciesPalette(new Color32(35, 24, 15, 255), new Color32(101, 71, 45, 255), new Color32(70, 49, 31, 255), new Color32(132, 94, 62, 255), new Color32(211, 191, 152, 255)),
            "yellow" => new AntSpeciesPalette(new Color32(49, 40, 20, 255), new Color32(164, 140, 76, 255), new Color32(116, 97, 52, 255), new Color32(202, 177, 108, 255), new Color32(242, 225, 170, 255)),
            "black" => new AntSpeciesPalette(new Color32(15, 15, 19, 255), new Color32(58, 58, 61, 255), new Color32(37, 37, 42, 255), new Color32(83, 83, 90, 255), new Color32(171, 170, 156, 255)),
            _ => new AntSpeciesPalette(new Color32(29, 23, 18, 255), new Color32(82, 67, 55, 255), new Color32(58, 47, 39, 255), new Color32(108, 89, 73, 255), new Color32(205, 187, 155, 255))
        };
    }
}
