using UnityEngine;

[CreateAssetMenu(fileName = "ShapeMaterialPalette", menuName = "Presentation/Shapes/Material Palette")]
public sealed class ShapeMaterialPalette : ScriptableObject
{
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Material organicMaterial;
    [SerializeField] private Material agentsMaterial;
    [SerializeField] private Material markersMaterial;
    [SerializeField] private Material linesMaterial;

    public Material GetMaterialForShape(string shapeId)
    {
        return GetMaterial(ShapeIdToCategory(shapeId));
    }

    public Material GetMaterial(ShapePaletteCategory category)
    {
        return category switch
        {
            ShapePaletteCategory.Core => coreMaterial,
            ShapePaletteCategory.Organic => organicMaterial,
            ShapePaletteCategory.Agents => agentsMaterial,
            ShapePaletteCategory.Markers => markersMaterial,
            ShapePaletteCategory.Lines => linesMaterial,
            _ => coreMaterial
        };
    }

    public static ShapePaletteCategory ShapeIdToCategory(string shapeId)
    {
        return shapeId switch
        {
            ShapeId.DotCore => ShapePaletteCategory.Core,
            ShapeId.DotGlow => ShapePaletteCategory.Core,
            ShapeId.DotGlowSmall => ShapePaletteCategory.Core,
            ShapeId.RingPing => ShapePaletteCategory.Core,
            ShapeId.PulseRing => ShapePaletteCategory.Core,
            ShapeId.OrganicMetaball => ShapePaletteCategory.Organic,
            ShapeId.OrganicAmoeba => ShapePaletteCategory.Organic,
            ShapeId.NoiseBlob => ShapePaletteCategory.Organic,
            ShapeId.FieldBlob => ShapePaletteCategory.Organic,
            ShapeId.TriangleAgent => ShapePaletteCategory.Agents,
            ShapeId.DiamondAgent => ShapePaletteCategory.Agents,
            ShapeId.ArrowAgent => ShapePaletteCategory.Agents,
            ShapeId.CrossMarker => ShapePaletteCategory.Markers,
            ShapeId.ArcSector => ShapePaletteCategory.Markers,
            ShapeId.LineSegment => ShapePaletteCategory.Lines,
            ShapeId.StrokeScribble => ShapePaletteCategory.Lines,
            ShapeId.Filament => ShapePaletteCategory.Lines,
            _ => ShapePaletteCategory.Core
        };
    }
}

public static class ShapeMaterialPaletteLoader
{
    private static ShapeMaterialPalette runtimeFallbackPalette;

    public static ShapeMaterialPalette Load()
    {
        var loadedPalette = Resources.Load<ShapeMaterialPalette>("ShapeMaterialPalette");
        if (loadedPalette != null)
        {
            return loadedPalette;
        }

        if (runtimeFallbackPalette == null)
        {
            runtimeFallbackPalette = ScriptableObject.CreateInstance<ShapeMaterialPalette>();
        }

        return runtimeFallbackPalette;
    }
}
