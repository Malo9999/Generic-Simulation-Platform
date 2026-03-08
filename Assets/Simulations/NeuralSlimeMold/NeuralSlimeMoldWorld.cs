using UnityEngine;
using UnityEngine.Serialization;

public enum NeuralSlimeWorldPreset
{
    OpenField = 0,
    CorridorCross = 1,
    CorridorMazeLite = 2,
    Custom = 3,
    ClusteredFood = 4,
    FoodDecayMigration = 5
}

public enum NeuralFoodDebugPreset
{
    Off = 0,
    Center3 = 1,
    Corners4 = 2,
    FoodDominanceTest = 3,
    FoodInfluenceDebug = 4,
    FoodDecayMigration = 5
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
    [FormerlySerializedAs("radius")]
    [Min(0.1f)] public float consumeRadius;
    [FormerlySerializedAs("strength")]
    [Min(0f)] public float baseStrength;
    [FormerlySerializedAs("capacity")]
    [Min(0f)] public float capacity;
    [FormerlySerializedAs("depletionRate")]
    [Min(0f)] public float consumeRate;
    [Min(0f)] public float regrowRate;
    public bool startActive;
}

public struct NeuralFoodNodeState
{
    public Vector2 position;
    public float consumeRadius;
    public float baseStrength;
    public float capacity;
    public float currentCapacity;
    public float consumeRate;
    public float regrowRate;
    public bool active;
    public float timeSinceDepleted;

    public float Capacity01 => capacity <= 0f ? 0f : Mathf.Clamp01(currentCapacity / capacity);
}

[System.Serializable]
public struct NeuralObstacle
{
    public NeuralObstacleShape shape;
    public Vector2 center;
    [Min(0.1f)] public float radius;
    public Vector2 size;
}
