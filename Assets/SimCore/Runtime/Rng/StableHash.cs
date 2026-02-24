public static class StableHash
{
    public static uint StableHash32(string text)
    {
        var value = string.IsNullOrEmpty(text) ? string.Empty : text;
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return hash;
    }

    public static int CombineSeed(int rootSeed, string saltString)
    {
        var saltHash = unchecked((int)StableHash32(saltString));
        return unchecked(rootSeed * 16777619) ^ saltHash;
    }
}
