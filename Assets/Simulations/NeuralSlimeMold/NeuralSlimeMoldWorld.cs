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
        var dishRadius = 30f;

        return new NeuralSlimeMoldArenaSetup
        {
            mapSize = new Vector2(72f, 72f),
            colonyHub = Vector2.zero,
            colonyHubRadius = 1.8f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(-18f, 20f), strength, capacity, radius, rate),
                Food(new Vector2(20f, 18f), strength, capacity, radius, rate),
                Food(new Vector2(-22f, -16f), strength, capacity, radius, rate),
                Food(new Vector2(21f, -20f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                RingAnchor(new Vector2(0f, dishRadius), 2.4f),
                RingAnchor(new Vector2(21f, 21f), 2.3f),
                RingAnchor(new Vector2(dishRadius, 0f), 2.4f),
                RingAnchor(new Vector2(21f, -21f), 2.3f),
                RingAnchor(new Vector2(0f, -dishRadius), 2.4f),
                RingAnchor(new Vector2(-21f, -21f), 2.3f),
                RingAnchor(new Vector2(-dishRadius, 0f), 2.4f),
                RingAnchor(new Vector2(-21f, 21f), 2.3f)
            },
            corridorBands = new[]
            {
                Band(new Vector2(0f, 0f), new Vector2(14f, 14f), 0f, 0.95f),
                Band(new Vector2(-10f, 11f), new Vector2(22f, 6f), 35f, 0.72f),
                Band(new Vector2(11f, 10f), new Vector2(22f, 6f), -30f, 0.72f),
                Band(new Vector2(-12f, -11f), new Vector2(24f, 6f), -28f, 0.67f),
                Band(new Vector2(12f, -12f), new Vector2(24f, 6f), 30f, 0.67f)
            },
            useWorldObstacles = true
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
            colonyHubRadius = 1.9f,
            manualFoodConfigs = new[]
            {
                Food(new Vector2(34f, 21f), strength, capacity, radius, rate),
                Food(new Vector2(34f, -21f), strength, capacity, radius, rate)
            },
            worldObstacles = new[]
            {
                // Left start chamber shell + gate.
                Rect(new Vector2(-36f, 19f), new Vector2(30f, 6f)),
                Rect(new Vector2(-36f, -19f), new Vector2(30f, 6f)),
                Rect(new Vector2(-50f, 0f), new Vector2(4f, 44f)),
                Rect(new Vector2(-22f, 9f), new Vector2(4f, 14f)),
                Rect(new Vector2(-22f, -9f), new Vector2(4f, 14f)),

                // Center wall structure that forms two narrow corridor routes.
                Rect(new Vector2(-4f, 24f), new Vector2(40f, 8f)),
                Rect(new Vector2(-4f, -24f), new Vector2(40f, 8f)),
                Rect(new Vector2(-2f, 0f), new Vector2(20f, 10f)),
                Rect(new Vector2(17f, 0f), new Vector2(10f, 18f)),

                // Upper destination chamber shell + narrow entrance.
                Rect(new Vector2(31f, 34f), new Vector2(34f, 6f)),
                Rect(new Vector2(31f, 8f), new Vector2(34f, 6f)),
                Rect(new Vector2(46f, 21f), new Vector2(4f, 32f)),
                Rect(new Vector2(18f, 25f), new Vector2(8f, 18f)),

                // Lower destination chamber shell + narrow entrance.
                Rect(new Vector2(31f, -8f), new Vector2(34f, 6f)),
                Rect(new Vector2(31f, -34f), new Vector2(34f, 6f)),
                Rect(new Vector2(46f, -21f), new Vector2(4f, 32f)),
                Rect(new Vector2(18f, -25f), new Vector2(8f, 18f)),

                // Splitter islands encourage route commitment and reduce random drift.
                Rect(new Vector2(6f, 12f), new Vector2(8f, 8f)),
                Rect(new Vector2(6f, -12f), new Vector2(8f, 8f))
            },
            corridorBands = new[]
            {
                // Start chamber lift for immediate colony emergence.
                Band(new Vector2(-35f, 0f), new Vector2(10f, 10f), 0f, 1.2f),

                // Upper route from gate to upper chamber.
                Band(new Vector2(-24f, 9f), new Vector2(14f, 4f), 0f, 1.15f),
                Band(new Vector2(-10f, 9f), new Vector2(14f, 4f), 0f, 1.1f),
                Band(new Vector2(5f, 14f), new Vector2(16f, 4f), 0f, 1.04f),
                Band(new Vector2(22f, 19f), new Vector2(18f, 4.5f), 0f, 1.0f),

                // Lower route from gate to lower chamber.
                Band(new Vector2(-24f, -9f), new Vector2(14f, 4f), 0f, 1.15f),
                Band(new Vector2(-10f, -9f), new Vector2(14f, 4f), 0f, 1.1f),
                Band(new Vector2(5f, -14f), new Vector2(16f, 4f), 0f, 1.04f),
                Band(new Vector2(22f, -19f), new Vector2(18f, 4.5f), 0f, 1.0f),

                // Weak central crossover keeps choices possible without flattening hierarchy.
                Band(new Vector2(-10f, 0f), new Vector2(7f, 12f), 90f, 0.28f)
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
