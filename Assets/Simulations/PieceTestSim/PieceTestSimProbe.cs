using UnityEngine;

public sealed class PieceTestSimProbe : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float radius = 0.2f;
    [SerializeField, Min(0f)] private float gravityScale = 1f;

    public void Configure(float gravityScaleValue)
    {
        gravityScale = gravityScaleValue;
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = gravityScale;
        }
    }

    private void Awake()
    {
        var rb = gameObject.GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = gravityScale;

        var col = gameObject.GetComponent<CircleCollider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
        col.radius = radius;

        var sr = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        sr.color = Color.yellow;
    }
}
