using UnityEngine;

public sealed class MarbleRaceTrack
{
    public readonly Vector2[] Center;
    public readonly Vector2[] Tangent;
    public readonly Vector2[] Normal;
    public readonly float[] HalfWidth;
    public readonly float[] Curvature;
    public readonly int SampleCount;

    public MarbleRaceTrack(
        Vector2[] center,
        Vector2[] tangent,
        Vector2[] normal,
        float[] halfWidth,
        float[] curvature)
    {
        Center = center;
        Tangent = tangent;
        Normal = normal;
        HalfWidth = halfWidth;
        Curvature = curvature;
        SampleCount = center != null ? center.Length : 0;
    }

    public int Wrap(int i)
    {
        if (SampleCount <= 0)
        {
            return 0;
        }

        var wrapped = i % SampleCount;
        return wrapped < 0 ? wrapped + SampleCount : wrapped;
    }

    public int ForwardDelta(int from, int to)
    {
        if (SampleCount <= 0)
        {
            return 0;
        }

        var delta = Wrap(to) - Wrap(from);
        if (delta < 0)
        {
            delta += SampleCount;
        }

        return delta;
    }
}
