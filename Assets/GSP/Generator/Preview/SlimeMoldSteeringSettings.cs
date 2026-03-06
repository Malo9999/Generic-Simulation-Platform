using UnityEngine;

[System.Serializable]
public sealed class SlimeMoldSteeringSettings
{
    [Min(0f)] public float sensorDistance = 1.2f;
    [Range(0f, 180f)] public float sensorAngleDeg = 35f;
    [Range(0f, 2f)] public float turnStrength = 0.8f;
    [Min(0f)] public float forwardSpeed = 1.0f;
    [Range(0f, 1f)] public float jitter = 0.05f;
}
