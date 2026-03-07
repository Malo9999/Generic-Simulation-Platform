using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Arrow Agent", fileName = "ArrowAgentTemplate")]
public class ArrowAgentTemplate : ShapeTemplateBase
{
    [SerializeField] private float lengthPx = 28f;
    [SerializeField] private float headWidthPx = 18f;
    [SerializeField] private float tailLengthPx = 10f;
    [SerializeField] private float tailWidthPx = 6f;
    [SerializeField] private bool useOutline;

    private void Reset()
    {
        ConfigureBase(ShapeId.ArrowAgent, "Agents", 64, 16);
        lengthPx = 28f;
        headWidthPx = 18f;
        tailLengthPx = 10f;
        tailWidthPx = 6f;
        useOutline = false;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var pixels = new Color32[size * size];
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        var tipX = center.x + (lengthPx * 0.5f);
        var tailX = center.x - (lengthPx * 0.5f);
        var headBaseX = tipX - Mathf.Max(4f, lengthPx - tailLengthPx);
        var halfHead = Mathf.Max(2f, headWidthPx * 0.5f);
        var halfTail = Mathf.Max(1f, tailWidthPx * 0.5f);

        var tip = new Vector2(tipX, center.y);
        var upper = new Vector2(headBaseX, center.y + halfHead);
        var lower = new Vector2(headBaseX, center.y - halfHead);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var p = new Vector2(x, y);
            var inHead = PointInTriangle(p, tip, upper, lower);
            var inTail = x >= tailX && x <= headBaseX && Mathf.Abs(y - center.y) <= halfTail;
            if (!inHead && !inTail)
            {
                continue;
            }

            pixels[(y * size) + x] = tint;
        }

        if (useOutline)
        {
            DrawOutline(pixels, size, tint);
        }

        return pixels;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static void DrawOutline(Color32[] pixels, int size, Color tint)
    {
        var source = (Color32[])pixels.Clone();
        var outline = tint;
        outline.r = (byte)Mathf.Clamp(outline.r * 0.55f, 0f, 255f);
        outline.g = (byte)Mathf.Clamp(outline.g * 0.55f, 0f, 255f);
        outline.b = (byte)Mathf.Clamp(outline.b * 0.55f, 0f, 255f);

        for (var y = 1; y < size - 1; y++)
        for (var x = 1; x < size - 1; x++)
        {
            var idx = (y * size) + x;
            if (source[idx].a > 0)
            {
                continue;
            }

            if (source[idx - 1].a > 0 || source[idx + 1].a > 0 || source[idx - size].a > 0 || source[idx + size].a > 0)
            {
                pixels[idx] = outline;
            }
        }
    }
}
