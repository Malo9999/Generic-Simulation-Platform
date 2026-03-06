using UnityEngine;

public sealed class SlimeMoldDemoAgent : MonoBehaviour
{
    [SerializeField] private TrailBufferController trailBuffer;
    [SerializeField] private SlimeMoldTrailPreset preset;
    [SerializeField] private float moveSpeed = 1.7f;

    private Vector2 direction;

    public void Configure(TrailBufferController buffer, SlimeMoldTrailPreset demoPreset, Vector2 initialDirection)
    {
        trailBuffer = buffer;
        preset = demoPreset;
        direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
    }

    private void Update()
    {
        if (trailBuffer == null)
        {
            return;
        }

        if (preset != null)
        {
            var bias = TrailSteeringUtility.ComputeSteeringBias(trailBuffer, transform.position, direction, preset);
            var noise = (Mathf.PerlinNoise(Time.time * 0.45f, transform.GetInstanceID() * 0.013f) - 0.5f) * 0.35f;
            direction = Rotate(direction, bias + noise * 0.1f).normalized;
        }

        var pos = (Vector2)transform.position + (direction * moveSpeed * Time.deltaTime);
        var bounds = trailBuffer.WorldBounds;

        if (pos.x < bounds.xMin || pos.x > bounds.xMax)
        {
            direction.x = -direction.x;
            pos.x = Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax);
        }

        if (pos.y < bounds.yMin || pos.y > bounds.yMax)
        {
            direction.y = -direction.y;
            pos.y = Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax);
        }

        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    private static Vector2 Rotate(Vector2 vector, float amount)
    {
        var rad = amount * Mathf.Deg2Rad * 30f;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        return new Vector2((vector.x * cos) - (vector.y * sin), (vector.x * sin) + (vector.y * cos));
    }
}
