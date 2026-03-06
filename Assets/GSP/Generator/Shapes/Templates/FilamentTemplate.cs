using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Filament", fileName = "FilamentTemplate")]
public class FilamentTemplate : ShapeTemplateBase
{
    [SerializeField] private float lengthPx = 90f;
    [SerializeField] private float thicknessStartPx = 6f;
    [SerializeField] private float thicknessEndPx = 3f;
    [SerializeField] private float bend = 0.35f;
    [SerializeField] private float waveAmpPx = 1.5f;
    [SerializeField] private float waveFreq = 2f;
    [SerializeField] private int seed = 1337;

    [Header("Rim Gradient")]
    [SerializeField] private bool useRimGradient = true;
    [SerializeField] private int rimWidthPx = 4;
    [SerializeField] private float innerMul = 1f;
    [SerializeField] private float outerMul = 0.75f;

    private void Reset()
    {
        ConfigureBase(ShapeId.Filament, "Lines", 128, 16);
        lengthPx = 90f;
        thicknessStartPx = 6f;
        thicknessEndPx = 3f;
        bend = 0.35f;
        waveAmpPx = 1.5f;
        waveFreq = 2f;
        seed = 1337;
        useRimGradient = true;
        rimWidthPx = 4;
        innerMul = 1f;
        outerMul = 0.75f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        return ShapeRasterizer.RasterizeFilament(
            TextureSize,
            tint,
            Mathf.Max(1f, lengthPx),
            Mathf.Max(0.25f, thicknessStartPx),
            Mathf.Max(0.25f, thicknessEndPx),
            bend,
            Mathf.Max(0f, waveAmpPx),
            Mathf.Max(0f, waveFreq),
            seed,
            useRimGradient,
            Mathf.Max(1, rimWidthPx),
            Mathf.Max(0f, innerMul),
            Mathf.Max(0f, outerMul));
    }
}
