using System;

public sealed class Pcg32Rng
{
    private const ulong DefaultStream = 1442695040888963407UL;

    private ulong state;
    private ulong increment;

    public Pcg32Rng(int seed)
    {
        Seed = seed;
        var initialState = ((ulong)(uint)seed << 1) | 1UL;
        var stream = DefaultStream ^ (((ulong)(uint)seed << 33) | 1UL);
        Initialize(initialState, stream);
    }

    public int Seed { get; }

    public uint NextUInt()
    {
        var oldState = state;
        state = unchecked((oldState * 6364136223846793005UL) + increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotate = (int)(oldState >> 59);
        return (xorShifted >> rotate) | (xorShifted << ((-rotate) & 31));
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        var range = (uint)(maxExclusive - minInclusive);
        var threshold = unchecked((uint)(0 - range)) % range;
        while (true)
        {
            var value = NextUInt();
            if (value >= threshold)
            {
                return minInclusive + (int)(value % range);
            }
        }
    }

    public float NextFloat01()
    {
        return (NextUInt() >> 8) * (1.0f / 16777216.0f);
    }

    private void Initialize(ulong initialState, ulong stream)
    {
        state = 0UL;
        increment = (stream << 1) | 1UL;
        NextUInt();
        state = unchecked(state + initialState);
        NextUInt();
    }
}
