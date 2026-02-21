using UnityEngine;

public static class BlueprintRasterizer
{
    public static void Render(PixelBlueprint2D blueprint, string layerName, int targetSize, int ox, int oy, Color32 color, Color32[] outPixels, int outWidth)
    {
        if (blueprint == null) return;
        var layer = blueprint.EnsureLayer(layerName);

        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * blueprint.width), 0, blueprint.width - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * blueprint.height), 0, blueprint.height - 1);
            if (layer.pixels[(sy * blueprint.width) + sx] > 0)
            {
                outPixels[((oy + y) * outWidth) + ox + x] = color;
            }
        }
    }
}
