using UnityEngine;

public enum NeuralObstacleShape
{
    Circle = 0,
    Rectangle = 1
}

[System.Serializable]
public struct NeuralObstacle
{
    public NeuralObstacleShape shape;
    public Vector2 center;
    [Min(0.1f)] public float radius;
    public Vector2 size;
}

[System.Serializable]
public struct NeuralCorridorBand
{
    public Vector2 center;
    public Vector2 size;
    [Range(-180f, 180f)] public float angleDegrees;
    [Min(0f)] public float strength;
}

[System.Serializable]
public struct NeuralFoodNodeConfig
{
    public Vector2 position;
    [Min(0f)] public float baseStrength;
    [Min(0f)] public float capacity;
    [Min(0f)] public float consumeRadius;
    [Min(0f)] public float consumeRate;
}

public struct NeuralFoodNodeState
{
    public Vector2 position;
    public float baseStrength;
    public float capacity;
    public float currentCapacity;
    public float consumeRadius;
    public float consumeRate;

    public float Capacity01 => capacity <= 0f ? 0f : Mathf.Clamp01(currentCapacity / capacity);
    public float EffectiveStrength => baseStrength * Capacity01;
}

public struct NeuralSlimeMoldArenaSetup
{
    public Vector2 mapSize;
    public Vector2 colonyHub;
    public float colonyHubRadius;
    public NeuralFoodNodeConfig[] manualFoodConfigs;
    public NeuralObstacle[] worldObstacles;
    public NeuralCorridorBand[] corridorBands;
    public bool useWorldObstacles;
}

public static class NeuralSlimeMoldArenaPresetBuilder
{
    public static NeuralSlimeMoldArenaSetup Build(
        NeuralSlimeMoldBootstrap.NeuralSlimeMoldArenaPreset preset,
        float foodStrength,
        float foodCapacity,
        float consumeRadius,
        float consumeRate)
    {
        return preset switch
        {
            NeuralSlimeMoldBootstrap.NeuralSlimeMoldArenaPreset.CorridorRun => BuildCorridorRun(foodStrength, foodCapacity, consumeRadius, consumeRate),
            NeuralSlimeMoldBootstrap.NeuralSlimeMoldArenaPreset.Crossroads => BuildCrossroads(foodStrength, foodCapacity, consumeRadius, consumeRate),
            NeuralSlimeMoldBootstrap.NeuralSlimeMoldArenaPreset.MazeLite => BuildMazeLite(foodStrength, foodCapacity, consumeRadius, consumeRate),
            NeuralSlimeMoldBootstrap.NeuralSlimeMoldArenaPreset.RingWorld => BuildRingWorld(foodStrength, foodCapacity, consumeRadius, consumeRate),
            _ => BuildOpenPetriDish(foodStrength, foodCapacity, consumeRadius, consumeRate)
        };
    }

    private static NeuralSlimeMoldArenaSetup BuildOpenPetriDish(float strength, float capacity, float radius, float rate)
    {
        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(72f, 72f),
            colonyHub = Vector2.zero,
            colonyHubRadius = 5.6f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(0f, 28f), strength, capacity, radius, rate),
                Food(new Vector2(-25f, -20f), strength, capacity, radius, rate),
                Food(new Vector2(25f, -20f), strength, capacity, radius, rate)
            },
            worldObstacles = System.Array.Empty<NeuralObstacle>(),
            corridorBands = new[]
            {
                Band(new Vector2(0f, 6f), new Vector2(58f, 12f), 0f, 0.18f),
                Band(new Vector2(0f, -8f), new Vector2(58f, 12f), 0f, 0.14f)
            },
            useWorldObstacles = false
        };
    }

    private static NeuralSlimeMoldArenaSetup BuildCorridorRun(float strength, float capacity, float radius, float rate)
    {
        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(84f, 56f),
            colonyHub = new Vector2(-34f, 0f),
            colonyHubRadius = 5.3f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(36f, 0f), strength, capacity, radius, rate),
                Food(new Vector2(27f, 19f), strength, capacity, radius, rate),
                Food(new Vector2(27f, -19f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                Rect(new Vector2(-6f, 12.5f), new Vector2(64f, 7f)),
                Rect(new Vector2(-6f, -12.5f), new Vector2(64f, 7f)),
                Rect(new Vector2(-24f, 0f), new Vector2(2.4f, 14f)),
                Rect(new Vector2(14f, 6f), new Vector2(6f, 9.5f)),
                Rect(new Vector2(14f, -6f), new Vector2(6f, 9.5f)),
                Rect(new Vector2(28f, 12f), new Vector2(18f, 6f)),
                Rect(new Vector2(28f, -12f), new Vector2(18f, 6f))
            },
            corridorBands = new[]
            {
                Band(new Vector2(-2f, 0f), new Vector2(78f, 7.5f), 0f, 1f),
                Band(new Vector2(18f, 18f), new Vector2(24f, 6f), 0f, 0.86f),
                Band(new Vector2(18f, -18f), new Vector2(24f, 6f), 0f, 0.86f)
            },
            useWorldObstacles = true
        };
    }

    private static NeuralSlimeMoldArenaSetup BuildCrossroads(float strength, float capacity, float radius, float rate)
    {
        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(80f, 80f),
            colonyHub = Vector2.zero,
            colonyHubRadius = 5.4f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(0f, 31f), strength, capacity, radius, rate),
                Food(new Vector2(31f, 0f), strength, capacity, radius, rate),
                Food(new Vector2(0f, -31f), strength, capacity, radius, rate),
                Food(new Vector2(-31f, 0f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                Rect(new Vector2(-10f, 10f), new Vector2(10f, 10f)),
                Rect(new Vector2(10f, 10f), new Vector2(10f, 10f)),
                Rect(new Vector2(-10f, -10f), new Vector2(10f, 10f)),
                Rect(new Vector2(10f, -10f), new Vector2(10f, 10f))
            },
            corridorBands = new[]
            {
                Band(Vector2.zero, new Vector2(62f, 8f), 0f, 1f),
                Band(Vector2.zero, new Vector2(62f, 8f), 90f, 1f),
                Band(Vector2.zero, new Vector2(68f, 6f), 45f, 0.66f),
                Band(Vector2.zero, new Vector2(68f, 6f), -45f, 0.66f)
            },
            useWorldObstacles = true
        };
    }

    private static NeuralSlimeMoldArenaSetup BuildMazeLite(float strength, float capacity, float radius, float rate)
    {
        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(96f, 68f),
            colonyHub = new Vector2(-35f, 0f),
            colonyHubRadius = 2.1f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(32f, 20f), strength, capacity, radius, rate),
                Food(new Vector2(32f, -20f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                // Left start chamber (hub nest) walls.
                Rect(new Vector2(-35f, 14f), new Vector2(30f, 8f)),
                Rect(new Vector2(-35f, -14f), new Vector2(30f, 8f)),
                Rect(new Vector2(-48f, 0f), new Vector2(4f, 36f)),
                Rect(new Vector2(-18f, 4f), new Vector2(4f, 16f)),
                Rect(new Vector2(-18f, -4f), new Vector2(4f, 16f)),

                // Corridor separators and dead-space closure around center.
                Rect(new Vector2(-2f, 15f), new Vector2(20f, 7f)),
                Rect(new Vector2(-2f, -15f), new Vector2(20f, 7f)),
                Rect(new Vector2(2f, 0f), new Vector2(12f, 8f)),
                Rect(new Vector2(16f, 0f), new Vector2(12f, 10f)),

                // Upper-right destination chamber shell.
                Rect(new Vector2(31f, 33f), new Vector2(34f, 6f)),
                Rect(new Vector2(31f, 7f), new Vector2(34f, 6f)),
                Rect(new Vector2(46f, 20f), new Vector2(4f, 32f)),

                // Lower-right destination chamber shell.
                Rect(new Vector2(31f, -7f), new Vector2(34f, 6f)),
                Rect(new Vector2(31f, -33f), new Vector2(34f, 6f)),
                Rect(new Vector2(46f, -20f), new Vector2(4f, 32f)),

                // Hard blockers to keep pressure inside corridors/chambers.
                Rect(new Vector2(-1f, 26f), new Vector2(24f, 8f)),
                Rect(new Vector2(-1f, -26f), new Vector2(24f, 8f)),
                Rect(new Vector2(24f, 0f), new Vector2(14f, 12f))
            },
            corridorBands = new[]
            {
                // Core start-chamber lift for early emergence.
                Band(new Vector2(-35f, 0f), new Vector2(12f, 11f), 0f, 1.1f),

                // Upper trunk: hub chamber gate -> upper destination chamber.
                Band(new Vector2(-23f, 8f), new Vector2(14f, 4.2f), 0f, 1.12f),
                Band(new Vector2(-8f, 8f), new Vector2(14f, 4.0f), 0f, 1.08f),
                Band(new Vector2(8f, 13.5f), new Vector2(20f, 4.2f), 0f, 1.04f),
                Band(new Vector2(24f, 18f), new Vector2(16f, 4.6f), 0f, 0.98f),

                // Lower trunk: hub chamber gate -> lower destination chamber.
                Band(new Vector2(-23f, -8f), new Vector2(14f, 4.2f), 0f, 1.12f),
                Band(new Vector2(-8f, -8f), new Vector2(14f, 4.0f), 0f, 1.08f),
                Band(new Vector2(8f, -13.5f), new Vector2(20f, 4.2f), 0f, 1.04f),
                Band(new Vector2(24f, -18f), new Vector2(16f, 4.6f), 0f, 0.98f),

                // Keep cross-links possible but weaker than trunk routes.
                Band(new Vector2(-12f, 0f), new Vector2(8f, 14f), 90f, 0.32f)
            },
            useWorldObstacles = true
        };
    }

    private static NeuralSlimeMoldArenaSetup BuildRingWorld(float strength, float capacity, float radius, float rate)
    {
        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(76f, 76f),
            colonyHub = new Vector2(-2f, -2f),
            colonyHubRadius = 4.5f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(0f, 30f), strength, capacity, radius, rate),
                Food(new Vector2(27f, 14f), strength, capacity, radius, rate),
                Food(new Vector2(27f, -14f), strength, capacity, radius, rate),
                Food(new Vector2(0f, -30f), strength, capacity, radius, rate),
                Food(new Vector2(-27f, 14f), strength, capacity, radius, rate),
                Food(new Vector2(-27f, -14f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                RingAnchor(new Vector2(0f, 26f), 2.6f),
                RingAnchor(new Vector2(22f, 13f), 2.6f),
                RingAnchor(new Vector2(22f, -13f), 2.6f),
                RingAnchor(new Vector2(0f, -26f), 2.6f),
                RingAnchor(new Vector2(-22f, 13f), 2.6f),
                RingAnchor(new Vector2(-22f, -13f), 2.6f)
            },
            corridorBands = new[]
            {
                Band(Vector2.zero, new Vector2(56f, 7f), 0f, 0.78f),
                Band(Vector2.zero, new Vector2(56f, 7f), 90f, 0.78f),
                Band(Vector2.zero, new Vector2(62f, 6f), 30f, 0.62f),
                Band(Vector2.zero, new Vector2(62f, 6f), -30f, 0.62f),
                Band(Vector2.zero, new Vector2(62f, 6f), 150f, 0.62f),
                Band(Vector2.zero, new Vector2(62f, 6f), -150f, 0.62f)
            },
            useWorldObstacles = true
        };
    }

    private static NeuralFoodNodeConfig Food(Vector2 position, float strength, float capacity, float radius, float rate)
    {
        return new NeuralFoodNodeConfig
        {
            position = position,
            baseStrength = Mathf.Max(0f, strength),
            capacity = Mathf.Max(0f, capacity),
            consumeRadius = Mathf.Max(0f, radius),
            consumeRate = Mathf.Max(0f, rate)
        };
    }

    private static NeuralObstacle Rect(Vector2 center, Vector2 size)
    {
        return new NeuralObstacle
        {
            shape = NeuralObstacleShape.Rectangle,
            center = center,
            radius = 0.5f,
            size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y))
        };
    }

    private static NeuralObstacle RingAnchor(Vector2 center, float radius)
    {
        return new NeuralObstacle
        {
            shape = NeuralObstacleShape.Circle,
            center = center,
            radius = Mathf.Max(0.1f, radius),
            size = Vector2.one * Mathf.Max(0.2f, radius * 2f)
        };
    }

    private static NeuralCorridorBand Band(Vector2 center, Vector2 size, float angleDegrees, float strength)
    {
        return new NeuralCorridorBand
        {
            center = center,
            size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y)),
            angleDegrees = angleDegrees,
            strength = Mathf.Max(0f, strength)
        };
    }
}
