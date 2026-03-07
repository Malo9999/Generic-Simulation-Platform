using UnityEngine;

public enum FieldWorldBoundsMode
{
    Auto = 0,
    Manual = 1
}

public enum FieldOverlayBlendMode
{
    Alpha = 0,
    Additive = 1
}

[System.Serializable]
public sealed class FieldOverlaySettings
{
    [Min(16)] public int width = 256;
    [Min(16)] public int height = 144;
    [Min(1f)] public float pixelsPerUnit = 24f;
    public FieldWorldBoundsMode worldBoundsMode = FieldWorldBoundsMode.Auto;
    public Rect manualWorldBounds = new(-12f, -6.75f, 24f, 13.5f);
    [Range(0f, 1f)] public float decayPerSecond = 0.80f;
    [Range(0f, 1f)] public float diffuseStrength = 0.05f;
    [Min(0f)] public float intensity = 1f;
    [Range(0f, 1f)] public float alphaMultiplier = 0.35f;
    public Color tintLow = new(0f, 0f, 0f, 0f);
    public Color tintHigh = new(0.3f, 0.92f, 1f, 1f);
    public FieldOverlayBlendMode blendMode = FieldOverlayBlendMode.Alpha;
    public int overlaySortOrder = -12;

    public Rect ResolveWorldBounds()
    {
        if (worldBoundsMode == FieldWorldBoundsMode.Manual && manualWorldBounds.width > 0.01f && manualWorldBounds.height > 0.01f)
        {
            return manualWorldBounds;
        }

        var widthUnits = width / Mathf.Max(1f, pixelsPerUnit);
        var heightUnits = height / Mathf.Max(1f, pixelsPerUnit);
        return new Rect(-widthUnits * 0.5f, -heightUnits * 0.5f, widthUnits, heightUnits);
    }
}
