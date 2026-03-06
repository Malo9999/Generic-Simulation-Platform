using UnityEngine;

public sealed class TrailEmitter : MonoBehaviour
{
    [SerializeField] private TrailBufferController targetBuffer;
    [SerializeField, Min(0f)] private float emissionStrength = 1f;
    [SerializeField, Min(0.1f)] private float emissionRadiusScale = 1f;
    [SerializeField] private bool emitEveryFrame = true;

    public void Configure(TrailBufferController buffer, float strength, float radiusScale)
    {
        targetBuffer = buffer;
        emissionStrength = Mathf.Max(0f, strength);
        emissionRadiusScale = Mathf.Max(0.1f, radiusScale);
    }

    private void LateUpdate()
    {
        if (!emitEveryFrame || targetBuffer == null || emissionStrength <= 0f)
        {
            return;
        }

        var position = transform.position;
        targetBuffer.QueueDeposit(new Vector2(position.x, position.y), emissionStrength, emissionRadiusScale);
    }
}
