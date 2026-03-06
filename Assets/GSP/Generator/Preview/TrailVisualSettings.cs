using UnityEngine;

[System.Serializable]
public sealed class TrailVisualSettings
{
    [Min(16)] public int textureWidth = 512;
    [Min(16)] public int textureHeight = 288;
    [Min(1)] public int depositRadiusPx = 2;
    [Min(0f)] public float depositStrength = 1.2f;
    [Range(0f, 1f)] public float decayPerSecond = 0.80f;
    [Range(0f, 1f)] public float diffuseStrength = 0.07f;
    public Color tintColor = new(0.35f, 0.95f, 1f, 1f);
    [Range(0f, 1f)] public float alphaMultiplier = 0.35f;

    public void ApplyTo(TrailBufferSettings target)
    {
        if (target == null)
        {
            return;
        }

        target.textureWidth = textureWidth;
        target.textureHeight = textureHeight;
        target.depositRadiusPx = depositRadiusPx;
        target.depositStrength = depositStrength;
        target.decayPerSecond = decayPerSecond;
        target.diffuseStrength = diffuseStrength;

        var resolvedTint = tintColor;
        resolvedTint.a = Mathf.Clamp01(alphaMultiplier);
        target.tintColor = resolvedTint;
    }
}
