using System;
using UnityEngine;

public static class MotionShaderAutoWiring
{
    private const string MotionShaderName = "Presentation/ShapeMotion2D";

    public static bool TryAttachAndConfigure(SpriteRenderer renderer, VisualKey key, string shapeId, MotionShaderProfile profile)
    {
        if (renderer == null || !IsSupportedShapeId(shapeId) || !UsesMotionShaderMaterial(renderer))
        {
            return false;
        }

        var driver = renderer.GetComponent<MotionShaderDriver>();
        if (driver == null)
        {
            driver = renderer.gameObject.AddComponent<MotionShaderDriver>();
        }

        driver.Configure(renderer, key, shapeId, profile ?? MotionShaderProfile.LoadRuntimeProfile());
        return true;
    }

    public static bool IsSupportedShapeId(string shapeId)
    {
        return shapeId switch
        {
            ShapeId.OrganicAmoeba => true,
            ShapeId.OrganicMetaball => true,
            ShapeId.FieldBlob => true,
            ShapeId.Filament => true,
            _ => false
        };
    }

    private static bool UsesMotionShaderMaterial(SpriteRenderer renderer)
    {
        var material = renderer.sharedMaterial;
        var shader = material != null ? material.shader : null;
        return shader != null && string.Equals(shader.name, MotionShaderName, StringComparison.Ordinal);
    }
}
