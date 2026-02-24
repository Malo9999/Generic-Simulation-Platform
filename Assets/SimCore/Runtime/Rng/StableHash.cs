public static class StableHash
{
    public static uint StableHash32(string text)
    {
        return StableHashUtility.Fnv1a32(text);
    }

    public static int CombineSeed(int rootSeed, string saltString)
    {
        return StableHashUtility.CombineSeed(rootSeed, saltString);
    }
}
