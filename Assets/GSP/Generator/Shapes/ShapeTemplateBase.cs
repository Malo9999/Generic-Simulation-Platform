using UnityEngine;

public abstract class ShapeTemplateBase : ScriptableObject
{
    [SerializeField] private string shapeId = ShapeId.DotCore;
    [SerializeField] private string categoryFolder = "Dots";
    [SerializeField] private int textureSize = 64;
    [SerializeField] private int ppu = 16;

    public string ShapeId => shapeId;
    public string CategoryFolder => string.IsNullOrWhiteSpace(categoryFolder) ? "Generated" : categoryFolder;
    public int TextureSize => Mathf.Max(8, textureSize);
    public int PixelsPerUnit => Mathf.Max(1, ppu);

    public virtual string DefaultFileName => shapeId;

    public void ConfigureBase(string id, string folder, int size, int pixelsPerUnit)
    {
        shapeId = id;
        categoryFolder = folder;
        textureSize = size;
        ppu = pixelsPerUnit;
    }

    public abstract Color32[] Rasterize(Color tint);
}
