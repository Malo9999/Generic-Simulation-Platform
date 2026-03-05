using UnityEngine;

public static class MetaballUtil
{
    public static float SampleField(float x, float y, Vector2[] centers, float[] radii)
    {
        var field = 0f;
        for (var i = 0; i < centers.Length; i++)
        {
            var d = Vector2.Distance(new Vector2(x, y), centers[i]);
            field += (radii[i] * radii[i]) / Mathf.Max(1f, d * d);
        }

        return field;
    }
}
