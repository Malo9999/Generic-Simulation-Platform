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

    [Header("Rim Gradient")]
    [SerializeField] private bool useRimGradient = true;
    [SerializeField] private int rimWidthPx = 4;
    [SerializeField] private float innerMul = 1f;
    [SerializeField] private float outerMul = 0.75f;

    private void Reset()
    {
        ConfigureBase(ShapeId.Filament, "Lines", 128, 16);
        ApplyDefaultSettings();
    }

    public void ApplyDefaultSettings()
    {
        lengthPx = 90f;
        thicknessStartPx = 6f;
        thicknessEndPx = 3f;
        bend = 0.35f;
        waveAmpPx = 1.5f;
        waveFreq = 2f;
        useRimGradient = true;
        rimWidthPx = 4;
        innerMul = 1f;
        outerMul = 0.75f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var pixels = new Color32[size * size];
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        var safeLength = Mathf.Max(8f, lengthPx);
        var start = new Vector2(center.x - (safeLength * 0.5f), center.y);
        var end = new Vector2(center.x + (safeLength * 0.5f), center.y);
        var control = center + (Vector2.up * (bend * safeLength * 0.5f));

        var sampleCount = Mathf.Clamp(Mathf.CeilToInt(safeLength * 1.25f), 24, 256);
        var samples = new Vector2[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (sampleCount - 1f);
            var p = EvalQuadratic(start, control, end, t);
            var tangent = EvalQuadraticDerivative(start, control, end, t).normalized;
            var normal = new Vector2(-tangent.y, tangent.x);
            var wave = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(0f, waveFreq)) * waveAmpPx;
            samples[i] = p + (normal * wave);
        }

        var rimWidth = Mathf.Max(1, rimWidthPx);
        var safeStartThickness = Mathf.Max(1f, thicknessStartPx);
        var safeEndThickness = Mathf.Max(1f, thicknessEndPx);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var p = new Vector2(x, y);
            var nearestDist = float.MaxValue;
            var nearestT = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var d = Vector2.Distance(p, samples[i]);
                if (d >= nearestDist)
                {
                    continue;
                }

                nearestDist = d;
                nearestT = i / (sampleCount - 1f);
            }

            var thickness = Mathf.Lerp(safeStartThickness, safeEndThickness, nearestT);
            var radius = thickness * 0.5f;
            if (nearestDist > radius)
            {
                continue;
            }

            var distToEdge = radius - nearestDist;
            var brightness = useRimGradient ? ApplyRimGradient(distToEdge, rimWidth) : innerMul;
            pixels[(y * size) + x] = MultiplyColor(tint, brightness);
        }

        return pixels;
    }

    private Vector2 EvalQuadratic(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        var omt = 1f - t;
        return (omt * omt * a) + (2f * omt * t * b) + (t * t * c);
    }

    private Vector2 EvalQuadraticDerivative(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        return (2f * (1f - t) * (b - a)) + (2f * t * (c - b));
    }

    private float ApplyRimGradient(float distanceToEdgePx, int rimWidth)
    {
        if (rimWidth <= 0)
        {
            return innerMul;
        }

        if (distanceToEdgePx < rimWidth)
        {
            var t = Mathf.Clamp01(distanceToEdgePx / rimWidth);
            return Mathf.Lerp(outerMul, innerMul, t);
        }

        return innerMul;
    }

    private Color32 MultiplyColor(Color tint, float brightness)
    {
        var c = tint;
        var mul = Mathf.Max(0f, brightness);
        c.r *= mul;
        c.g *= mul;
        c.b *= mul;
        return c;
    }
}
