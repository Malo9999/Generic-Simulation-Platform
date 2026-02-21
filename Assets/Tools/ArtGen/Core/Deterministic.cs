using System;
using System.Collections.Generic;

public static class Deterministic
{
    public static int StableHash32(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }
            return hash;
        }
    }

    public static int DeriveSeed(int baseSeed, string salt)
    {
        unchecked
        {
            return (baseSeed * 397) ^ StableHash32(salt ?? string.Empty);
        }
    }

    public static Random SystemRandom(int seed) => new(seed);

    public static void Shuffle<T>(IList<T> list, int seed)
    {
        var rng = SystemRandom(seed);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static List<T> PickN<T>(IReadOnlyList<T> list, int count, int seed)
    {
        var copy = new List<T>(list);
        Shuffle(copy, seed);
        if (count < copy.Count) copy.RemoveRange(count, copy.Count - count);
        return copy;
    }
}
