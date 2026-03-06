using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Arc Sector", fileName = "ArcSectorTemplate")]
public class ArcSectorTemplate : ShapeTemplateBase
{
    [SerializeField] private float radiusPx = 30f;
    [SerializeField] private float angleDeg = 70f;
    [SerializeField] private float thicknessPx = 4f;

    [SerializeField] private bool useRimGradient = true;
    [SerializeField] private int rimWidthPx = 4;
    [SerializeField] private float innerMul = 1f;
    [SerializeField] private float outerMul = 0.8f;

    private void Reset()
    {
        ConfigureBase(ShapeId.ArcSector, "Markers", 96, 16);
        radiusPx = 30f;
        angleDeg = 70f;
        thicknessPx = 4f;
        useRimGradient = true;
        rimWidthPx = 4;
        innerMul = 1f;
        outerMul = 0.8f;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var pixels = new Color32[size * size];
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var radius = Mathf.Max(2f, radiusPx);
        var thickness = Mathf.Max(1f, thicknessPx);
        var innerRadius = Mathf.Max(0f, radius - thickness);
        var halfAngle = Mathf.Clamp(angleDeg * 0.5f, 1f, 179f);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = x - center.x;
            var dy = y - center.y;
            var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
            if (dist < innerRadius || dist > radius)
            {
                continue;
            }

            var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (Mathf.Abs(angle) > halfAngle)
            {
                continue;
            }

            var distToEdge = Mathf.Min(dist - innerRadius, radius - dist);
            var brightness = useRimGradient ? ApplyRimGradient(distToEdge, Mathf.Max(1, rimWidthPx)) : innerMul;
            pixels[(y * size) + x] = MultiplyColor(tint, brightness);
        }

        return pixels;
    }

    private float ApplyRimGradient(float distanceToEdgePx, int rimWidth)
    {
        if (distanceToEdgePx < rimWidth)
        {
            var t = Mathf.Clamp01(distanceToEdgePx / rimWidth);
            return Mathf.Lerp(outerMul, innerMul, t);
        }

        return innerMul;
    }

    private static Color32 MultiplyColor(Color tint, float brightness)
    {
        var c = tint;
        var mul = Mathf.Max(0f, brightness);
        c.r *= mul;
        c.g *= mul;
        c.b *= mul;
        return c;
    }
}
