using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SeededRng : IRng
{
    private readonly Pcg32Rng pcg;

    public SeededRng(int seed)
    {
        Seed = seed;
        pcg = new Pcg32Rng(seed);
    }

    public int Seed { get; }

    public uint NextUInt() => pcg.NextUInt();

    public int NextInt(int minInclusive, int maxExclusive)
    {
        return pcg.NextInt(minInclusive, maxExclusive);
    }

    public float NextFloat01()
    {
        return pcg.NextFloat01();
    }

    public float Range(float minInclusive, float maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        return minInclusive + ((maxInclusive - minInclusive) * NextFloat01());
    }

    public bool Chance(float p01)
    {
        if (p01 <= 0f) return false;
        if (p01 >= 1f) return true;
        return NextFloat01() < p01;
    }

    public void Shuffle<T>(IList<T> list)
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public Vector2 InsideUnitCircle()
    {
        var angle = Range(0f, Mathf.PI * 2f);
        var radius = Mathf.Sqrt(NextFloat01());
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    public int Sign()
    {
        return (NextUInt() & 1u) == 0u ? -1 : 1;
    }

    public int PickIndexWeighted(IReadOnlyList<float> weights)
    {
        if (weights == null || weights.Count == 0)
        {
            return -1;
        }

        var total = 0f;
        for (var i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i]);
        }

        if (total <= 0f)
        {
            return NextInt(0, weights.Count);
        }

        var pick = Range(0f, total);
        var cumulative = 0f;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (pick <= cumulative)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }

    public float Value() => NextFloat01();

    public int Range(int minInclusive, int maxExclusive) => NextInt(minInclusive, maxExclusive);

    public double NextDouble() => NextFloat01();
}
