using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Line Segment", fileName = "LineSegmentTemplate")]
public class LineSegmentTemplate : ShapeTemplateBase
{
    [SerializeField] private float lengthPx = 44f;
    [SerializeField] private float thicknessPx = 2f;
    [SerializeField] private bool roundedCaps = true;

    private void Reset()
    {
        ConfigureBase(ShapeId.LineSegment, "Lines", 64, 16);
        lengthPx = 44f;
        thicknessPx = 2f;
        roundedCaps = true;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeLineSegment(TextureSize, tint, lengthPx, thicknessPx, roundedCaps);
    }
}
