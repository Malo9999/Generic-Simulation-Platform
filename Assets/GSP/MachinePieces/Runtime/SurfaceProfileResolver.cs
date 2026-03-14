using UnityEngine;

public sealed class SurfaceProfileResolver
{
    public SurfaceProfile ResolveSurface(PieceSpec spec, PieceInstance instance, MachinePieceLibrary lib)
    {
        var key = string.IsNullOrWhiteSpace(instance?.surfaceOverride)
            ? spec?.surface?.surfaceProfileId
            : instance.surfaceOverride;

        if (string.IsNullOrWhiteSpace(key) || !lib.SurfaceProfiles.TryGetValue(key, out var profile))
        {
            return null;
        }

        return profile;
    }

    public Material BuildMaterial(SurfaceProfile surface)
    {
        var shader = Shader.Find(surface?.shaderKey);
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        var material = new Material(shader);
        if (surface != null)
        {
            material.color = surface.baseColor.ToColor();
            if (!string.IsNullOrWhiteSpace(surface.textureKey))
            {
                var tex = Resources.Load<Texture2D>(surface.textureKey);
                if (tex != null)
                {
                    material.mainTexture = tex;
                }
            }
        }

        return material;
    }
}
