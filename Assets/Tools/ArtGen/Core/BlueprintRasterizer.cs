using UnityEngine;

public static class BlueprintRasterizer
{
    public struct LayerStyle
    {
        public readonly string layerName;
        public readonly ToneRamp ramp;
        public readonly bool drawOutline;
        public readonly string outlineMaskLayerName;

        public LayerStyle(string layerName, ToneRamp ramp, bool drawOutline = true, string outlineMaskLayerName = null)
        {
            this.layerName = layerName;
            this.ramp = ramp;
            this.drawOutline = drawOutline;
            this.outlineMaskLayerName = outlineMaskLayerName;
        }
    }

    public struct ToneRamp
    {
        public readonly Color32 baseColor;
        public readonly Color32 shadowColor;
        public readonly Color32 highlightColor;
        public readonly Color32 outlineColor;

        public ToneRamp(Color32 baseColor, Color32 shadowColor, Color32 highlightColor, Color32 outlineColor)
        {
            this.baseColor = baseColor;
            this.shadowColor = shadowColor;
            this.highlightColor = highlightColor;
            this.outlineColor = outlineColor;
        }
    }

    public static void Render(PixelBlueprint2D blueprint, string layerName, int targetSize, int ox, int oy, Color32 color, Color32[] outPixels, int outWidth, string outlineMaskLayerName = null)
    {
        if (blueprint == null) return;

        var filled = BuildFilledMask(blueprint, layerName, targetSize);
        var outlineMask = BuildOutlineMask(blueprint, layerName, outlineMaskLayerName, targetSize);
        var drawOutline = string.Equals(layerName, "body", System.StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(outlineMaskLayerName);
        var ramp = new ToneRamp(
            baseColor: color,
            shadowColor: ScaleColor(color, 0.90f),
            highlightColor: ScaleColor(color, 1.08f),
            outlineColor: new Color32((byte)(color.r * 0.35f), (byte)(color.g * 0.35f), (byte)(color.b * 0.35f), 255));

        DrawFilledAndOutline(filled, outlineMask, targetSize, ox, oy, outPixels, outWidth, ramp, drawOutline);
    }

    public static void Render(PixelBlueprint2D blueprint, string layerName, int targetSize, int ox, int oy, ToneRamp ramp, Color32[] outPixels, int outWidth, bool drawOutline = true, string outlineMaskLayerName = null)
    {
        if (blueprint == null) return;
        var filled = BuildFilledMask(blueprint, layerName, targetSize);
        var outlineMask = BuildOutlineMask(blueprint, layerName, outlineMaskLayerName, targetSize);
        DrawFilledAndOutline(filled, outlineMask, targetSize, ox, oy, outPixels, outWidth, ramp, drawOutline);
    }

    public static void RenderLayers(PixelBlueprint2D blueprint, int targetSize, int ox, int oy, Color32[] outPixels, int outWidth, params LayerStyle[] styles)
    {
        if (styles == null)
        {
            return;
        }

        for (var i = 0; i < styles.Length; i++)
        {
            var style = styles[i];
            if (string.IsNullOrWhiteSpace(style.layerName))
            {
                continue;
            }

            Render(blueprint, style.layerName, targetSize, ox, oy, style.ramp, outPixels, outWidth, style.drawOutline, style.outlineMaskLayerName);
        }
    }

    private static void DrawFilledAndOutline(bool[] filled, bool[] outlineMask, int targetSize, int ox, int oy, Color32[] outPixels, int outWidth, ToneRamp ramp, bool drawOutline)
    {
        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var idx = (y * targetSize) + x;
            if (filled[idx])
            {
                outPixels[((oy + y) * outWidth) + ox + x] = SelectBandTone(targetSize, y, ramp);
            }
            else if (drawOutline && HasFilledNeighbor(outlineMask, targetSize, targetSize, x, y))
            {
                outPixels[((oy + y) * outWidth) + ox + x] = ramp.outlineColor;
            }
        }
    }

    private static Color32 SelectBandTone(int size, int y, ToneRamp ramp)
    {
        if (size <= 1)
        {
            return ramp.baseColor;
        }

        var topBandEnd = Mathf.FloorToInt((size - 1) * 0.33f);
        var bottomBandStart = Mathf.CeilToInt((size - 1) * 0.67f);
        if (y <= topBandEnd) return ramp.highlightColor;
        if (y >= bottomBandStart) return ramp.shadowColor;
        return ramp.baseColor;
    }

    private static bool[] BuildFilledMask(PixelBlueprint2D blueprint, string layerName, int targetSize)
    {
        var layer = blueprint.EnsureLayer(layerName);
        var filled = new bool[targetSize * targetSize];

        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * blueprint.width), 0, blueprint.width - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * blueprint.height), 0, blueprint.height - 1);
            filled[(y * targetSize) + x] = layer.pixels[(sy * blueprint.width) + sx] > 0;
        }

        return filled;
    }

    private static bool[] BuildOutlineMask(PixelBlueprint2D blueprint, string layerName, string outlineMaskLayerName, int targetSize)
    {
        var resolvedLayerName = string.IsNullOrWhiteSpace(outlineMaskLayerName) ? layerName : outlineMaskLayerName;
        return BuildFilledMask(blueprint, resolvedLayerName, targetSize);
    }

    private static Color32 ScaleColor(Color32 color, float factor)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255),
            color.a);
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
