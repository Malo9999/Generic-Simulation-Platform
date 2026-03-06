using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Trails/Slime Mold Trail Preset", fileName = "SlimeMoldTrailPreset")]
public sealed class SlimeMoldTrailPreset : ScriptableObject
{
    [Min(0f)] public float sensorDistance = 1.5f;
    [Range(0f, 180f)] public float sensorAngleDeg = 35f;
    [Range(0f, 2f)] public float turnStrength = 0.9f;
    [Min(0f)] public float depositStrength = 1.2f;
    [Range(0f, 1f)] public float decayPerSecond = 0.80f;
    [Range(0f, 1f)] public float diffuseStrength = 0.08f;

    public void ApplyTo(TrailBufferSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.depositStrength = depositStrength;
        settings.decayPerSecond = decayPerSecond;
        settings.diffuseStrength = diffuseStrength;
    }
}
