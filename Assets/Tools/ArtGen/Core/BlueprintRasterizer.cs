using UnityEngine;

public static class BlueprintRasterizer
{
    public static void Render(PixelBlueprint2D blueprint, string layerName, int targetSize, int ox, int oy, Color32 color, Color32[] outPixels, int outWidth)
    {
        if (blueprint == null) return;
        var layer = blueprint.EnsureLayer(layerName);
        var filled = new bool[targetSize * targetSize];

        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * blueprint.width), 0, blueprint.width - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * blueprint.height), 0, blueprint.height - 1);
            filled[(y * targetSize) + x] = layer.pixels[(sy * blueprint.width) + sx] > 0;
        }

        var drawOutline = string.Equals(layerName, "body", System.StringComparison.OrdinalIgnoreCase);
        var outlineColor = new Color32((byte)(color.r * 0.35f), (byte)(color.g * 0.35f), (byte)(color.b * 0.35f), 255);

        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var idx = (y * targetSize) + x;
            if (filled[idx])
            {
                var shade = Mathf.Lerp(1.08f, 0.90f, y / (float)Mathf.Max(1, targetSize - 1));
                outPixels[((oy + y) * outWidth) + ox + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * shade), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * shade), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * shade), 0, 255),
                    color.a);
            }
            else if (drawOutline && HasFilledNeighbor(filled, targetSize, targetSize, x, y))
            {
                outPixels[((oy + y) * outWidth) + ox + x] = outlineColor;
            }
        }
    }

    private static bool HasFilledNeighbor(bool[] filled, int width, int height, int x, int y)
    {
        for (var oy = -1; oy <= 1; oy++)
        for (var ox = -1; ox <= 1; ox++)
        {
            if (ox == 0 && oy == 0) continue;
            var nx = x + ox;
            var ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
            if (filled[(ny * width) + nx]) return true;
        }

        return false;
    }
}
