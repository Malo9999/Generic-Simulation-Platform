using UnityEngine;

public static class RngService
{
    private static int globalSeed;

    static RngService()
    {
        SetGlobal(0);
    }

    public static IRng Global { get; private set; }

    public static int GlobalSeed => globalSeed;

    public static void SetGlobal(int seed)
    {
        globalSeed = seed;
        Global = new SeededRng(seed);
    }

    public static void SetGlobal(IRng rng)
    {
        if (rng == null)
        {
            return;
        }

        Global = rng;
        globalSeed = rng.Seed;
    }

    public static IRng Fork(string salt)
    {
        return new SeededRng(StableHash.CombineSeed(globalSeed, salt));
    }

    public static IRng Create(int seed)
    {
        return new SeededRng(seed);
    }

    public static string BuildSignature(int seed, int count = 8)
    {
        var root = Create(seed);
        var world = Create(StableHash.CombineSeed(seed, "WORLD:OBSTACLES"));
        var decor = Create(StableHash.CombineSeed(seed, "DECOR:GRASS"));

        var signature = $"seed={seed}";
        for (var i = 0; i < count; i++)
        {
            signature += $"|r{i}:{root.NextInt(0, 100000)}";
        }

        var decorPoint = decor.InsideUnitCircle();
        signature += $"|obs:{world.NextInt(0, 100000)}|decor:{decorPoint.x:F4},{decorPoint.y:F4}";
        return signature;
    }
}
