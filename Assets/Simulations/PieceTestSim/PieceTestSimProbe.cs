using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PieceTestSimProbe : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float radius = 0.2f;
    [SerializeField, Min(0f)] private float gravityScale = 1f;

    public void Configure(float gravityScaleValue)
    {
        gravityScale = gravityScaleValue;

        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.gravityScale = gravityScale;
        }
    }

    private void Awake()
    {
        if (!TryGetComponent<Rigidbody2D>(out var rb))
        {
            enabled = false;
            Debug.LogError($"[PieceTestSim] {name} is missing Rigidbody2D. Disabling PieceTestSimProbe to fail safely.", this);
            return;
        }

        rb.gravityScale = gravityScale;

        if (TryGetComponent<CircleCollider2D>(out var col))
        {
            col.radius = radius;
        }

        if (TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.color = Color.yellow;
        }
    }
}
