public static class StableHash
{
    public static int Hash32(string value)
    {
        return unchecked((int)StableHashUtility.Fnv1a32(value));
    }

    public static uint StableHash32(string text)
    {
        return StableHashUtility.Fnv1a32(text);
    }

    public static int CombineSeed(int rootSeed, string saltString)
    {
        return StableHashUtility.CombineSeed(rootSeed, saltString);
    }
}
