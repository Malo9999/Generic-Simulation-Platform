using System;

public sealed class SeededRng : IRng
{
    private readonly System.Random random;

    public SeededRng(int seed)
    {
        Seed = seed;
        random = new System.Random(seed);
    }

    public int Seed { get; }

    public float Value()
    {
        return (float)random.NextDouble();
    }

    public float Range(float minInclusive, float maxInclusive)
    {
        return minInclusive + ((maxInclusive - minInclusive) * Value());
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        return random.Next(minInclusive, maxExclusive);
    }

    public double NextDouble()
    {
        return random.NextDouble();
    }
}
