using UnityEngine;

public sealed class SlimeMoldDemoAgent : MonoBehaviour
{
    [SerializeField] private TrailBufferController trailBuffer;
    [SerializeField] private FieldBufferController fieldOverlayBuffer;
    [SerializeField] private SlimeMoldSteeringSettings steeringSettings = new();

    private IFieldDepositBuffer buffer;
    private Vector2 direction;

    public void Configure(TrailBufferController sourceBuffer, SlimeMoldSteeringSettings settings, Vector2 initialDirection)
    {
        trailBuffer = sourceBuffer;
        fieldOverlayBuffer = null;
        buffer = sourceBuffer;
        steeringSettings = settings ?? new SlimeMoldSteeringSettings();
        direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
    }

    public void Configure(FieldBufferController sourceBuffer, SlimeMoldSteeringSettings settings, Vector2 initialDirection)
    {
        fieldOverlayBuffer = sourceBuffer;
        trailBuffer = null;
        buffer = sourceBuffer;
        steeringSettings = settings ?? new SlimeMoldSteeringSettings();
        direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
    }

    private void Awake()
    {
        ResolveBuffer();
    }

    private void OnValidate()
    {
        ResolveBuffer();
    }

    private void Update()
    {
        if (buffer == null)
        {
            return;
        }

        var bias = TrailSteeringUtility.ComputeSteeringBias(buffer, transform.position, direction, steeringSettings);
        var noise = (Mathf.PerlinNoise(Time.time * 0.45f, transform.GetInstanceID() * 0.013f) - 0.5f) * 0.35f;
        direction = Rotate(direction, bias + noise * steeringSettings.jitter).normalized;

        var pos = (Vector2)transform.position + (direction * steeringSettings.forwardSpeed * Time.deltaTime);
        var bounds = buffer.WorldBounds;

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

    private void ResolveBuffer()
    {
        if (fieldOverlayBuffer != null)
        {
            buffer = fieldOverlayBuffer;
            return;
        }

        buffer = trailBuffer;
    }

    private static Vector2 Rotate(Vector2 vector, float amount)
    {
        var rad = amount * Mathf.Deg2Rad * 30f;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        return new Vector2((vector.x * cos) - (vector.y * sin), (vector.x * sin) + (vector.y * cos));
    }
}
