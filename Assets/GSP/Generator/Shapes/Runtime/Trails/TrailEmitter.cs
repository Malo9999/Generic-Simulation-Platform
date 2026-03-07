using UnityEngine;

public sealed class TrailEmitter : MonoBehaviour
{
    [SerializeField] private TrailBufferController targetBuffer;
    [SerializeField] private FieldBufferController fieldOverlayBuffer;
    [SerializeField, Min(0f)] private float emissionStrength = 1f;
    [SerializeField, Min(0.1f)] private float emissionRadiusScale = 1f;
    [SerializeField] private bool emitEveryFrame = true;

    private IFieldDepositBuffer buffer;

    public void Configure(TrailBufferController trailBuffer, float strength, float radiusScale)
    {
        targetBuffer = trailBuffer;
        fieldOverlayBuffer = null;
        buffer = trailBuffer;
        emissionStrength = Mathf.Max(0f, strength);
        emissionRadiusScale = Mathf.Max(0.1f, radiusScale);
    }

    public void Configure(FieldBufferController fieldBuffer, float strength, float radiusScale)
    {
        fieldOverlayBuffer = fieldBuffer;
        targetBuffer = null;
        buffer = fieldBuffer;
        emissionStrength = Mathf.Max(0f, strength);
        emissionRadiusScale = Mathf.Max(0.1f, radiusScale);
    }

    private void Awake()
    {
        ResolveBuffer();
    }

    private void OnValidate()
    {
        ResolveBuffer();
    }

    private void LateUpdate()
    {
        if (!emitEveryFrame || buffer == null || emissionStrength <= 0f)
        {
            return;
        }

        var position = transform.position;
        buffer.AddDeposit(new Vector2(position.x, position.y), emissionStrength, emissionRadiusScale);
    }

    private void ResolveBuffer()
    {
        if (fieldOverlayBuffer != null)
        {
            buffer = fieldOverlayBuffer;
            return;
        }

        buffer = targetBuffer;
    }
}
