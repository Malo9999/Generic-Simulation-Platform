using System;

public static class StableHash
{
    public static int Hash32(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return unchecked((int)2166136261u);
        }

        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        var hash = offset;

        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return unchecked((int)hash);
    }
}
