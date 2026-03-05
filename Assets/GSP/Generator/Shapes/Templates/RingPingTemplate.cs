using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Ring Ping", fileName = "RingPingTemplate")]
public class RingPingTemplate : ShapeTemplateBase
{
    [SerializeField] private float ringRadiusPx = 26f;
    [SerializeField] private float thicknessPx = 3f;
    [SerializeField] private bool includeOuterGlow = true;
    [SerializeField] private float outerGlowWidthPx = 12f;
    [SerializeField, Range(0f, 1f)] private float outerGlowAlpha = 0.15f;

    private void Reset()
    {
        ConfigureBase(ShapeId.RingPing, "Rings", 128, 16);
        ringRadiusPx = 26f;
        thicknessPx = 3f;
        includeOuterGlow = true;
        outerGlowWidthPx = 12f;
        outerGlowAlpha = 0.15f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeRingPing(TextureSize, tint, ringRadiusPx, thicknessPx, includeOuterGlow, outerGlowWidthPx, outerGlowAlpha);
    }
}
