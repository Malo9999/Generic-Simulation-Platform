using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Triangle Agent", fileName = "TriangleAgentTemplate")]
public class TriangleAgentTemplate : ShapeTemplateBase
{
    [SerializeField] private float tipRadiusPx = 20f;
    [SerializeField] private float baseWidthPx = 18f;
    [SerializeField] private float baseOffsetPx = 6f;
    [SerializeField] private int outlinePx;

    private void Reset()
    {
        ConfigureBase(ShapeId.TriangleAgent, "Agents", 64, 16);
        tipRadiusPx = 20f;
        baseWidthPx = 18f;
        baseOffsetPx = 6f;
        outlinePx = 0;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeTriangleAgent(TextureSize, tint, tipRadiusPx, baseWidthPx, baseOffsetPx, Mathf.Max(0, outlinePx));
    }
}
