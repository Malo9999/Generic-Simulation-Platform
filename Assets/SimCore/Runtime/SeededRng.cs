using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SeededRng : IRng
{
    private ulong state0;
    private ulong state1;

    public SeededRng(int seed)
    {
        Seed = seed;

        var sm = (ulong)(uint)seed;
        state0 = SplitMix64(ref sm);
        state1 = SplitMix64(ref sm);
        if (state0 == 0UL && state1 == 0UL)
        {
            state1 = 0x9E3779B97F4A7C15UL;
        }
    }

    public int Seed { get; }

    public uint NextUInt()
    {
        var s1 = state0;
        var s0 = state1;
        state0 = s0;
        s1 ^= s1 << 23;
        state1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
        var sum = state1 + s0;
        return (uint)(sum & 0xFFFFFFFFu);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        var range = (uint)(maxExclusive - minInclusive);
        var limit = uint.MaxValue - (uint.MaxValue % range);
        uint value;
        do
        {
            value = NextUInt();
        } while (value >= limit);

        return minInclusive + (int)(value % range);
    }

    public float NextFloat01()
    {
        // 24-bit precision float in [0,1).
        return (NextUInt() >> 8) * (1.0f / 16777216.0f);
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

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
