using UnityEngine;

public enum NeuralSlimeWorldPreset
{
    OpenField = 0,
    CorridorCross = 1,
    CorridorMazeLite = 2,
    Custom = 3,
    CorridorTest = 1,
    IslandObstacles = 2,
    ClusteredFood = 4
}


public enum NeuralFoodDebugPreset
{
    Off = 0,
    Center3 = 1,
    Corners4 = 2,
    FoodDominanceTest = 3,
    FoodInfluenceDebug = 4
}

public enum NeuralObstacleShape
{
    Circle = 0,
    Rectangle = 1
}

[System.Serializable]
public struct NeuralFoodNodeConfig
{
    public Vector2 position;
    [Min(0.1f)] public float radius;
    [Min(0f)] public float strength;
    [Min(0f)] public float capacity;
    [Min(0f)] public float depletionRate;
    [Min(0f)] public float regrowRate;
    public bool startActive;
}

public struct NeuralFoodNodeState
{
    public Vector2 position;
    public float radius;
    public float strength;
    public float maxCapacity;
    public float capacity;
    public float depletionRate;
    public float regrowRate;
    public bool active;

    public float Capacity01 => maxCapacity <= 0f ? 0f : Mathf.Clamp01(capacity / maxCapacity);
    public float EffectiveStrength => active ? strength * Capacity01 : 0f;
}

[System.Serializable]
public struct NeuralObstacle
{
    public NeuralObstacleShape shape;
    public Vector2 center;
    [Min(0.1f)] public float radius;
    public Vector2 size;
}
