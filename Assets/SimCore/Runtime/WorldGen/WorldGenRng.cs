using System;

[Serializable]
public struct WorldGenRng
{
    private uint state;

    public WorldGenRng(int seed)
    {
        state = (uint)seed;
        if (state == 0) state = 0x6d2b79f5u;
    }

    private WorldGenRng(uint state)
    {
        this.state = state == 0 ? 0x6d2b79f5u : state;
    }

    public uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    public float NextFloat01()
    {
        return (NextUInt() & 0x00FFFFFF) / 16777216f;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        var range = (uint)(maxExclusive - minInclusive);
        return (int)(NextUInt() % range) + minInclusive;
    }

    public WorldGenRng Fork(string streamName)
    {
        var h = StableHash(streamName);
        return new WorldGenRng(Mix(state, h));
    }

    private static uint StableHash(string text)
    {
        unchecked
        {
            uint h = 2166136261;
            if (!string.IsNullOrEmpty(text))
            {
                for (var i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619;
                }
            }
            return h;
        }
    }

    private static uint Mix(uint a, uint b)
    {
        unchecked
        {
            var x = a ^ (b + 0x9e3779b9u + (a << 6) + (a >> 2));
            x ^= x >> 15;
            x *= 0x2c1b3c6d;
            x ^= x >> 12;
            x *= 0x297a2d39;
            x ^= x >> 15;
            return x;
        }
    }
}
