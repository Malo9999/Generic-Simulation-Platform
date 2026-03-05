using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Stroke", fileName = "StrokeTemplate")]
public class StrokeTemplate : ShapeTemplateBase
{
    [SerializeField] private int seed = 90210;
    [SerializeField] private int steps = 14;
    [SerializeField] private float widthPx = 2f;
    [SerializeField] private float stridePx = 4.5f;

    private void Reset()
    {
        ConfigureBase(ShapeId.StrokeScribble, "Strokes", 96, 16);
        seed = 90210;
        steps = 14;
        widthPx = 2f;
        stridePx = 4.5f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeStroke(TextureSize, tint, seed, Mathf.Max(2, steps), widthPx, stridePx);
    }
}
