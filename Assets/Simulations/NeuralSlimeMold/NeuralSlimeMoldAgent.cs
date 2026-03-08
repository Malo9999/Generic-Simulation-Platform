using UnityEngine;

[System.Serializable]
public struct NeuralControllerParams
{
    public float leftWeight;
    public float centerWeight;
    public float rightWeight;
    public float densityWeight;
    public float noiseWeight;
}

public struct NeuralSlimeMoldAgent
{
    public Vector2 position;
    public float heading;
    public float speed;
    public float sensorAngle;
    public float sensorDistance;
    public float turnRate;
    public float depositAmount;
    public NeuralControllerParams controller;
}
