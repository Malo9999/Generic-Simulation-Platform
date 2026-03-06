using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Field Blob", fileName = "FieldBlobTemplate")]
public class FieldBlobTemplate : ShapeTemplateBase
{
    [SerializeField] private float radiusPx = 28f;
    [SerializeField] private float falloffExponent = 2.4f;
    [SerializeField, Range(0f, 1f)] private float alphaMultiplier = 0.28f;

    private void Reset()
    {
        ConfigureBase(ShapeId.FieldBlob, "Fields", 128, 16);
        radiusPx = 28f;
        falloffExponent = 2.4f;
        alphaMultiplier = 0.28f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var pixels = new Color32[size * size];
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var radius = Mathf.Max(1f, radiusPx);
        var outer = radius * 1.8f;

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var a = SdfUtil.SoftCircleAlpha(x, y, center, radius, outer, Mathf.Max(0.1f, falloffExponent));
            if (a <= 0f)
            {
                continue;
            }

            var c = tint;
            c.a = Mathf.Clamp01(a * alphaMultiplier);
            pixels[(y * size) + x] = c;
        }

        return pixels;
    }
}
