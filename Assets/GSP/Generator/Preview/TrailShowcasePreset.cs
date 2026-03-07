using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Preview/Trail Showcase Preset", fileName = "TrailPreset")]
public sealed class TrailShowcasePreset : ScriptableObject
{
    public TrailVisualSettings trailVisual = new();
    public SlimeMoldSteeringSettings slimeSteering = new();

    public void ApplyTo(TrailVisualSettings visualTarget, SlimeMoldSteeringSettings steeringTarget)
    {
        if (visualTarget != null)
        {
            visualTarget.textureWidth = trailVisual.textureWidth;
            visualTarget.textureHeight = trailVisual.textureHeight;
            visualTarget.depositRadiusPx = trailVisual.depositRadiusPx;
            visualTarget.depositStrength = trailVisual.depositStrength;
            visualTarget.decayPerSecond = trailVisual.decayPerSecond;
            visualTarget.diffuseStrength = trailVisual.diffuseStrength;
            visualTarget.tintColor = trailVisual.tintColor;
            visualTarget.alphaMultiplier = trailVisual.alphaMultiplier;
        }

        if (steeringTarget != null)
        {
            steeringTarget.sensorDistance = slimeSteering.sensorDistance;
            steeringTarget.sensorAngleDeg = slimeSteering.sensorAngleDeg;
            steeringTarget.turnStrength = slimeSteering.turnStrength;
            steeringTarget.forwardSpeed = slimeSteering.forwardSpeed;
            steeringTarget.jitter = slimeSteering.jitter;
        }
    }
}
