using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class TrailBufferRenderer : MonoBehaviour
{
    [SerializeField] private TrailBufferController trailBuffer;
    [SerializeField] private int sortingOrder = -10;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;

    public void Configure(TrailBufferController controller)
    {
        trailBuffer = controller;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
    }

    private void Start()
    {
        BindTexture();
    }

    private void LateUpdate()
    {
        if (trailBuffer == null || trailBuffer.TrailTexture == null)
        {
            return;
        }

        if (sprite == null || sprite.texture != trailBuffer.TrailTexture)
        {
            BindTexture();
        }

        var bounds = trailBuffer.WorldBounds;
        transform.position = new Vector3(bounds.center.x, bounds.center.y, 1f);
        transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

        var tint = trailBuffer.Settings.tintColor;
        spriteRenderer.color = new Color(1f, 1f, 1f, tint.a);
    }

    private void OnDestroy()
    {
        if (sprite != null)
        {
            Destroy(sprite);
        }
    }

    private void BindTexture()
    {
        if (trailBuffer == null || trailBuffer.TrailTexture == null)
        {
            return;
        }

        if (sprite != null)
        {
            Destroy(sprite);
        }

        var tex = trailBuffer.TrailTexture;
        sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0u, SpriteMeshType.FullRect);
        spriteRenderer.sprite = sprite;
        spriteRenderer.size = Vector2.one;
    }
}
