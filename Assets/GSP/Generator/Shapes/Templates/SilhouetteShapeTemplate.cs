using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Silhouette Shape", fileName = "SilhouetteShapeTemplate")]
public class SilhouetteShapeTemplate : ShapeTemplateBase
{
    [Header("Silhouette")]
    [SerializeField] private Texture2D silhouette;
    [SerializeField, Range(0f, 1f)] private float alphaThreshold = 0.1f;
    [SerializeField] private bool fitToSafeInnerBox = true;
    [SerializeField, Min(0)] private int paddingPixels = 12;
    [SerializeField] private bool invert = false;

    public Texture2D Silhouette => silhouette;

    public void ConfigureSilhouette(Texture2D texture, float threshold, bool fitAndNormalize, int paddingPx, bool invertAlpha = false)
    {
        silhouette = texture;
        alphaThreshold = Mathf.Clamp01(threshold);
        fitToSafeInnerBox = fitAndNormalize;
        paddingPixels = Mathf.Max(0, paddingPx);
        invert = invertAlpha;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var output = new Color32[size * size];
        if (silhouette == null)
        {
            return output;
        }

        var src = silhouette.GetPixels32();
        var srcWidth = Mathf.Max(1, silhouette.width);
        var srcHeight = Mathf.Max(1, silhouette.height);

        if (!TryGetAlphaBounds(src, srcWidth, srcHeight, out var bounds))
        {
            return output;
        }

        var safeScale = fitToSafeInnerBox ? 0.82f : 1f;
        var safeSize = Mathf.Max(1f, (size - (paddingPixels * 2f)) * safeScale);
        var srcAspect = bounds.width / Mathf.Max(1f, bounds.height);
        var dstWidth = srcAspect >= 1f ? safeSize : safeSize * srcAspect;
        var dstHeight = srcAspect >= 1f ? safeSize / Mathf.Max(0.001f, srcAspect) : safeSize;

        var centerX = size * 0.5f;
        var centerY = size * 0.5f;
        var minX = centerX - (dstWidth * 0.5f);
        var minY = centerY - (dstHeight * 0.5f);

        var tint32 = (Color32)tint;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var index = y * size + x;
                var nx = (x + 0.5f - minX) / Mathf.Max(0.001f, dstWidth);
                var ny = (y + 0.5f - minY) / Mathf.Max(0.001f, dstHeight);
                if (nx < 0f || nx > 1f || ny < 0f || ny > 1f)
                {
                    output[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var srcX = bounds.xMin + nx * bounds.width;
                var srcY = bounds.yMin + ny * bounds.height;
                var sample = SampleNearest(src, srcWidth, srcHeight, srcX, srcY);
                var alpha = sample.a / 255f;
                if (invert)
                {
                    alpha = 1f - alpha;
                }

                if (alpha <= alphaThreshold)
                {
                    output[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                output[index] = new Color32(
                    tint32.r,
                    tint32.g,
                    tint32.b,
                    (byte)Mathf.RoundToInt(tint32.a * alpha));
            }
        }

        return output;
    }

    private static Color32 SampleNearest(Color32[] pixels, int width, int height, float x, float y)
    {
        var sx = Mathf.Clamp(Mathf.RoundToInt(x), 0, width - 1);
        var sy = Mathf.Clamp(Mathf.RoundToInt(y), 0, height - 1);
        return pixels[sy * width + sx];
    }

    private bool TryGetAlphaBounds(Color32[] pixels, int width, int height, out Rect bounds)
    {
        var found = false;
        var minX = width;
        var maxX = 0;
        var minY = height;
        var maxY = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = pixels[y * width + x].a / 255f;
                if (invert)
                {
                    alpha = 1f - alpha;
                }

                if (alpha <= alphaThreshold)
                {
                    continue;
                }

                found = true;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (!found)
        {
            bounds = default;
            return false;
        }

        bounds = Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        return true;
    }
}
