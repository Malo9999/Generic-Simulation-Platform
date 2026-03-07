using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FieldOverlayRenderer : MonoBehaviour
{
    [SerializeField] private FieldBufferController fieldBuffer;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;
    private Material additiveMaterial;

    public void Configure(FieldBufferController controller)
    {
        fieldBuffer = controller;
        BindTexture();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
    }

    private void Start()
    {
        BindTexture();
    }

    private void LateUpdate()
    {
        if (fieldBuffer == null || fieldBuffer.FieldTexture == null)
        {
            return;
        }

        if (sprite == null || sprite.texture != fieldBuffer.FieldTexture)
        {
            BindTexture();
        }

        var settings = fieldBuffer.Settings;
        spriteRenderer.sortingOrder = settings.overlaySortOrder;

        var bounds = fieldBuffer.WorldBounds;
        transform.position = new Vector3(bounds.center.x, bounds.center.y, 1.5f);
        transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

        spriteRenderer.color = Color.white;
        ApplyBlendMode(settings.blendMode);
    }

    private void OnDestroy()
    {
        if (sprite != null)
        {
            Destroy(sprite);
        }

        if (additiveMaterial != null)
        {
            Destroy(additiveMaterial);
        }
    }

    private void BindTexture()
    {
        if (spriteRenderer == null || fieldBuffer == null || fieldBuffer.FieldTexture == null)
        {
            return;
        }

        if (sprite != null)
        {
            Destroy(sprite);
        }

        var tex = fieldBuffer.FieldTexture;
        sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0u, SpriteMeshType.FullRect);
        spriteRenderer.sprite = sprite;
        spriteRenderer.size = Vector2.one;
    }

    private void ApplyBlendMode(FieldOverlayBlendMode blendMode)
    {
        if (blendMode == FieldOverlayBlendMode.Additive)
        {
            if (additiveMaterial == null)
            {
                var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive");
                if (shader != null)
                {
                    additiveMaterial = new Material(shader);
                }
            }

            if (additiveMaterial != null)
            {
                spriteRenderer.sharedMaterial = additiveMaterial;
            }
            return;
        }

        spriteRenderer.sharedMaterial = null;
    }
}
