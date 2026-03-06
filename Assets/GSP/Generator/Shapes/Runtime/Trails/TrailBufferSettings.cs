using UnityEngine;

[System.Serializable]
public sealed class TrailBufferSettings
{
    [Min(16)] public int textureWidth = 512;
    [Min(16)] public int textureHeight = 288;
    [Min(1f)] public float pixelsPerUnit = 24f;
    public Rect worldBounds = new(-12f, -6.75f, 24f, 13.5f);
    public bool useWorldBounds = true;
    [Range(0f, 1f)] public float decayPerSecond = 0.85f;
    [Range(0f, 1f)] public float diffuseStrength = 0.05f;
    [Min(0f)] public float depositStrength = 1f;
    [Min(1)] public int depositRadiusPx = 4;
    public bool useAdditiveComposite = true;
    public Color tintColor = new(0.35f, 0.95f, 1f, 0.72f);

    public float AlphaMultiplier => Mathf.Clamp01(tintColor.a);

    public Rect ResolveWorldBounds()
    {
        if (useWorldBounds && worldBounds.width > 0.01f && worldBounds.height > 0.01f)
        {
            return worldBounds;
        }

        var widthUnits = textureWidth / Mathf.Max(1f, pixelsPerUnit);
        var heightUnits = textureHeight / Mathf.Max(1f, pixelsPerUnit);
        return new Rect(-widthUnits * 0.5f, -heightUnits * 0.5f, widthUnits, heightUnits);
    }
}
