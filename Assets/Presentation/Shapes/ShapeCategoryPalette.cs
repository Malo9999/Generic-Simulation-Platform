using UnityEngine;

public enum ShapePaletteCategory
{
    Core,
    Organic,
    Agents,
    Markers,
    Lines
}

[CreateAssetMenu(fileName = "ShapeCategoryPalette", menuName = "Presentation/Shapes/Category Palette")]
public sealed class ShapeCategoryPalette : ScriptableObject
{
    [SerializeField] private Color coreColor = new(0.388f, 0.91f, 0.949f, 1f);
    [SerializeField] private Color organicColor = new(0.31f, 0.82f, 0.71f, 1f);
    [SerializeField] private Color agentsColor = new(0.604f, 0.969f, 0.784f, 1f);
    [SerializeField] private Color markersColor = new(0.561f, 0.847f, 1f, 1f);
    [SerializeField] private Color linesColor = new(0.349f, 0.941f, 0.91f, 1f);

    public Color GetColor(ShapePaletteCategory category)
    {
        return category switch
        {
            ShapePaletteCategory.Core => coreColor,
            ShapePaletteCategory.Organic => organicColor,
            ShapePaletteCategory.Agents => agentsColor,
            ShapePaletteCategory.Markers => markersColor,
            ShapePaletteCategory.Lines => linesColor,
            _ => coreColor
        };
    }
}
