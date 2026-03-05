using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Glow Dot", fileName = "GlowDotTemplate")]
public class GlowDotTemplate : ShapeTemplateBase
{
    [SerializeField] private float innerBrightRadiusPx = 14f;
    [SerializeField] private float outerRadiusPx = 44f;
    [SerializeField] private float falloffExponent = 2.6f;
    [SerializeField, Range(0f, 1f)] private float alphaMultiplier = 0.35f;

    private void Reset()
    {
        ConfigureBase(ShapeId.DotGlow, "Glows", 128, 16);
        innerBrightRadiusPx = 14f;
        outerRadiusPx = 44f;
        falloffExponent = 2.6f;
        alphaMultiplier = 0.35f;
    }

    public void ApplySmallDefaults()
    {
        ConfigureBase(ShapeId.DotGlowSmall, "Glows", 64, 16);
        innerBrightRadiusPx = 6f;
        outerRadiusPx = 22f;
        falloffExponent = 2.2f;
        alphaMultiplier = 0.22f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeGlowDot(TextureSize, tint, innerBrightRadiusPx, outerRadiusPx, falloffExponent, alphaMultiplier);
    }
}
