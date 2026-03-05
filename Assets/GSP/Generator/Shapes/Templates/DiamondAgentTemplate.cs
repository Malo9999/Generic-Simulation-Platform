using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Diamond Agent", fileName = "DiamondAgentTemplate")]
public class DiamondAgentTemplate : ShapeTemplateBase
{
    [SerializeField] private float diamondRadiusPx = 18f;
    [SerializeField] private int outlinePx;

    private void Reset()
    {
        ConfigureBase(ShapeId.DiamondAgent, "Agents", 64, 16);
        diamondRadiusPx = 18f;
        outlinePx = 0;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeDiamondAgent(TextureSize, tint, diamondRadiusPx, Mathf.Max(0, outlinePx));
    }
}
