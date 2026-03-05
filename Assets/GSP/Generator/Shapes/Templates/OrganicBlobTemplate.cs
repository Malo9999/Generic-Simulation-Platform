using UnityEngine;

public enum OrganicBlobMode
{
    Metaball,
    Amoeba
}

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Organic Blob", fileName = "OrganicBlobTemplate")]
public class OrganicBlobTemplate : ShapeTemplateBase
{
    [SerializeField] private OrganicBlobMode mode = OrganicBlobMode.Metaball;
    [SerializeField] private int seed = 1337;
    [SerializeField] private int lobeCount = 4;
    [SerializeField] private float radiusPx = 18f;
    [SerializeField] private float jitterPx = 8f;

    private void Reset()
    {
        ConfigureBase(ShapeId.OrganicMetaball, "Blobs", 96, 16);
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeOrganic(TextureSize, tint, mode, seed, Mathf.Max(2, lobeCount), radiusPx, jitterPx);
    }
}
