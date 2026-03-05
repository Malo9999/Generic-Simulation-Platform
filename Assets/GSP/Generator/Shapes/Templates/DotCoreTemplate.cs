using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Dot Core", fileName = "DotCoreTemplate")]
public class DotCoreTemplate : ShapeTemplateBase
{
    [SerializeField] private float radiusPx = 10f;
    [SerializeField] private float outlinePx = 2f;
    [SerializeField, Range(0f, 1f)] private float outlineAlpha = 0.9f;
    [SerializeField, Min(0f)] private float outlineColorMultiplier = 0.25f;

    private void Reset()
    {
        ConfigureBase(ShapeId.DotCore, "Dots", 64, 16);
        radiusPx = 10f;
        outlinePx = 2f;
        outlineAlpha = 0.9f;
        outlineColorMultiplier = 0.25f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeDotCore(TextureSize, tint, radiusPx, outlinePx, outlineAlpha, outlineColorMultiplier);
    }
}
