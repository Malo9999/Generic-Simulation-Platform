using System;
using UnityEngine;

public static class RngService
{
    private static int globalSeed;
    private static bool isGlobalSeedInitialized;

    static RngService()
    {
        globalSeed = 0;
        Global = new SeededRng(0);
        isGlobalSeedInitialized = false;
    }

    public static IRng Global { get; private set; }

    public static int GlobalSeed => globalSeed;

    public static void SetGlobalSeed(int seed)
    {
        globalSeed = seed;
        Global = new SeededRng(seed);
        isGlobalSeedInitialized = true;
    }

    public static void SetGlobal(int seed)
    {
        SetGlobalSeed(seed);
    }

    public static void SetGlobal(IRng rng)
    {
        if (rng == null)
        {
            return;
        }

        Global = rng;
        globalSeed = rng.Seed;
        isGlobalSeedInitialized = true;
    }

    public static IRng Fork(string salt)
    {
        if (!isGlobalSeedInitialized)
        {
            throw new InvalidOperationException("RngService.Fork called before global seed initialization. Call RngService.SetGlobalSeed(seed) during bootstrap.");
        }

        return new SeededRng(StableHashUtility.CombineSeed(globalSeed, salt));
    }

    public static IRng Create(int seed)
    {
        return new SeededRng(seed);
    }

    public static string BuildSignature(int seed, int count = 8)
    {
        var root = Create(seed);
        var world = Create(StableHashUtility.CombineSeed(seed, "WORLD:OBSTACLES"));
        var decor = Create(StableHashUtility.CombineSeed(seed, "DECOR:GRASS"));

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
