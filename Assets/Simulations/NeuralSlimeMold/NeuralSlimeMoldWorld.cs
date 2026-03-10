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
