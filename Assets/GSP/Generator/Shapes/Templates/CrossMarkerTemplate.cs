using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Templates/Cross Marker", fileName = "CrossMarkerTemplate")]
public class CrossMarkerTemplate : ShapeTemplateBase
{
    [SerializeField] private float armLengthPx = 18f;
    [SerializeField] private float armThicknessPx = 4f;
    [SerializeField] private bool roundedCaps = true;

    private void Reset()
    {
        ConfigureBase(ShapeId.CrossMarker, "Markers", 64, 16);
        armLengthPx = 18f;
        armThicknessPx = 4f;
        roundedCaps = true;
    }

    public override Color32[] Rasterize(Color tint)
    {
        var size = TextureSize;
        var pixels = new Color32[size * size];
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var halfLength = Mathf.Max(1f, armLengthPx * 0.5f);
        var halfThickness = Mathf.Max(1f, armThicknessPx * 0.5f);

        var hA = new Vector2(center.x - halfLength, center.y);
        var hB = new Vector2(center.x + halfLength, center.y);
        var vA = new Vector2(center.x, center.y - halfLength);
        var vB = new Vector2(center.x, center.y + halfLength);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var p = new Vector2(x, y);
            var onHorizontal = roundedCaps
                ? DistanceToSegment(p, hA, hB) <= halfThickness
                : x >= hA.x && x <= hB.x && Mathf.Abs(y - center.y) <= halfThickness;
            var onVertical = roundedCaps
                ? DistanceToSegment(p, vA, vB) <= halfThickness
                : y >= vA.y && y <= vB.y && Mathf.Abs(x - center.x) <= halfThickness;

            if (onHorizontal || onVertical)
            {
                pixels[(y * size) + x] = tint;
            }
        }

        return pixels;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
        {
            return Vector2.Distance(p, a);
        }

        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        var closest = a + (ab * t);
        return Vector2.Distance(p, closest);
    }
}
