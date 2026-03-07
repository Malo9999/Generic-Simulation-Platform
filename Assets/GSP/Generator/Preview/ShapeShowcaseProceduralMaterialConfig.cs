using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShapeShowcaseProceduralMaterialConfig
{
    [SerializeField] private bool useProceduralMaterials;
    [SerializeField] private bool fallbackToBaselineOnFailure = true;
    [SerializeField] private ShapeMaterialPalette materialPalette;
    [SerializeField, Min(0f)] private float defaultIntensity = 1f;
    [SerializeField] private List<ShapeProceduralOverride> perShapeOverrides = new();

    public bool UseProceduralMaterials => useProceduralMaterials;
    public bool FallbackToBaselineOnFailure => fallbackToBaselineOnFailure;

    public bool TryGetShapeOverride(string shapeId, out ShapeProceduralOverride shapeOverride)
    {
        shapeOverride = null;
        if (string.IsNullOrWhiteSpace(shapeId) || perShapeOverrides == null)
        {
            return false;
        }

        for (var i = 0; i < perShapeOverrides.Count; i++)
        {
            var candidate = perShapeOverrides[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.shapeId))
            {
                continue;
            }

            if (string.Equals(candidate.shapeId, shapeId, StringComparison.Ordinal))
            {
                shapeOverride = candidate;
                return true;
            }
        }

        return false;
    }

    public bool IsEnabledForShape(string shapeId)
    {
        if (!useProceduralMaterials)
        {
            return false;
        }

        if (TryGetShapeOverride(shapeId, out var shapeOverride))
        {
            return shapeOverride.enableProcedural;
        }

        return true;
    }

    public float ResolveIntensity(string shapeId)
    {
        if (TryGetShapeOverride(shapeId, out var shapeOverride))
        {
            return Mathf.Max(0f, shapeOverride.intensity);
        }

        return Mathf.Max(0f, defaultIntensity);
    }

    public Material ResolveMaterial(string shapeId, ShapePaletteCategory category)
    {
        if (TryGetShapeOverride(shapeId, out var shapeOverride) && shapeOverride.materialOverride != null)
        {
            return shapeOverride.materialOverride;
        }

        if (materialPalette == null)
        {
            return null;
        }

        return materialPalette.GetMaterial(category);
    }
}

[Serializable]
public sealed class ShapeProceduralOverride
{
    public string shapeId;
    public bool enableProcedural = true;
    [Min(0f)] public float intensity = 1f;
    public Material materialOverride;
}
