using UnityEngine;

public static class TrailSteeringUtility
{
    public static float ComputeSteeringBias(TrailBufferController buffer, Vector2 position, Vector2 forward, SlimeMoldSteeringSettings settings)
    {
        if (buffer == null || settings == null)
        {
            return 0f;
        }

        var direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
        var leftDir = Rotate(direction, settings.sensorAngleDeg);
        var rightDir = Rotate(direction, -settings.sensorAngleDeg);

        var ahead = buffer.SampleApproxValue(position + (direction * settings.sensorDistance));
        var left = buffer.SampleApproxValue(position + (leftDir * settings.sensorDistance));
        var right = buffer.SampleApproxValue(position + (rightDir * settings.sensorDistance));

        if (ahead >= left && ahead >= right)
        {
            return 0f;
        }

        if (left > right)
        {
            return settings.turnStrength;
        }

        if (right > left)
        {
            return -settings.turnStrength;
        }

        return 0f;
    }

    private static Vector2 Rotate(Vector2 dir, float angleDeg)
    {
        var rad = angleDeg * Mathf.Deg2Rad;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        return new Vector2((dir.x * cos) - (dir.y * sin), (dir.x * sin) + (dir.y * cos));
    }
}
