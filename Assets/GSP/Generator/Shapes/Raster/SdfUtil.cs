using UnityEngine;

public static class SdfUtil
{
    public static float CircleSignedDistance(float x, float y, Vector2 center, float radius)
    {
        return Vector2.Distance(new Vector2(x, y), center) - radius;
    }

    public static float HardCircleAlpha(float x, float y, Vector2 center, float radius)
    {
        return CircleSignedDistance(x, y, center, radius) <= 0f ? 1f : 0f;
    }

    public static float SoftCircleAlpha(float x, float y, Vector2 center, float innerRadius, float outerRadius, float exponent)
    {
        var d = Vector2.Distance(new Vector2(x, y), center);
        if (d <= innerRadius)
        {
            return 1f;
        }

        if (d >= outerRadius)
        {
            return 0f;
        }

        var t = 1f - Mathf.InverseLerp(innerRadius, outerRadius, d);
        return Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, exponent));
    }

    public static float RingAlpha(float x, float y, Vector2 center, float radius, float thickness)
    {
        var d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), center) - radius);
        return d <= (thickness * 0.5f) ? 1f : 0f;
    }

    public static float OutlineAlpha(float x, float y, Vector2 center, float radius, float thickness)
    {
        var outer = HardCircleAlpha(x, y, center, radius + thickness);
        var inner = HardCircleAlpha(x, y, center, radius);
        return Mathf.Clamp01(outer - inner);
    }
}
