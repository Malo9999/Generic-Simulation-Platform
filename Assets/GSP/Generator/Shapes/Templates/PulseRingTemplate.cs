using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Pulse Ring", fileName = "PulseRingTemplate")]
public class PulseRingTemplate : ShapeTemplateBase
{
    [SerializeField] private float ringRadiusPx = 28f;
    [SerializeField] private float thicknessPx = 2f;
    [SerializeField] private float outerGlowWidthPx = 10f;
    [SerializeField, Range(0f, 1f)] private float outerGlowAlphaMul = 0.18f;

    private void Reset()
    {
        ConfigureBase(ShapeId.PulseRing, "Rings", 128, 16);
        ringRadiusPx = 28f;
        thicknessPx = 2f;
        outerGlowWidthPx = 10f;
        outerGlowAlphaMul = 0.18f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeRingPing(TextureSize, tint, ringRadiusPx, thicknessPx, true, outerGlowWidthPx, outerGlowAlphaMul);
    }
}
