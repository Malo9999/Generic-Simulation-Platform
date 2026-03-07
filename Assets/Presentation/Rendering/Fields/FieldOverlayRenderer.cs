using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FieldOverlayRenderer : MonoBehaviour
{
    [SerializeField] private FieldBufferController fieldBuffer;
    [SerializeField] private Material additiveOverlayMaterial;

    private const float VisibilityEpsilon = 1e-4f;

    private SpriteRenderer spriteRenderer;
    private Sprite runtimeSprite;
    private bool hasLoggedAdditiveFallback;
    private bool hasLoggedHiddenEmptyBuffer;

    public void Configure(FieldBufferController controller)
    {
        fieldBuffer = controller;
        BindTexture();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnforceSafeSpriteRendererState();
    }

    private void Start()
    {
        BindTexture();
    }

    private void LateUpdate()
    {
        if (fieldBuffer == null || fieldBuffer.FieldTexture == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        if (runtimeSprite == null || runtimeSprite.texture != fieldBuffer.FieldTexture)
        {
            BindTexture();
        }

        var settings = fieldBuffer.Settings;
        spriteRenderer.sortingOrder = settings.overlaySortOrder;

        var bounds = fieldBuffer.WorldBounds;
        transform.position = new Vector3(bounds.center.x, bounds.center.y, 1.5f);
        transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

        EnforceSafeSpriteRendererState();

        var shouldShowOverlay = fieldBuffer.HasVisibleContent || fieldBuffer.CurrentMaxValue > VisibilityEpsilon;
        spriteRenderer.enabled = shouldShowOverlay;

        if (!shouldShowOverlay)
        {
            if (!hasLoggedHiddenEmptyBuffer)
            {
                Debug.Log("[FieldOverlayRenderer] overlay hidden because field buffer is empty", this);
                hasLoggedHiddenEmptyBuffer = true;
            }

            return;
        }

        hasLoggedHiddenEmptyBuffer = false;
        ApplyBlendMode(settings.blendMode);
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null)
        {
            Destroy(runtimeSprite);
            runtimeSprite = null;
        }
    }

    private void BindTexture()
    {
        if (spriteRenderer == null || fieldBuffer == null || fieldBuffer.FieldTexture == null)
        {
            return;
        }

        var texture = fieldBuffer.FieldTexture;
        if (texture == null)
        {
            return;
        }

        var boundSprite = spriteRenderer.sprite;
        var hasStaleTexture = boundSprite == null || boundSprite.texture != texture;
        if (!hasStaleTexture)
        {
            return;
        }

        if (runtimeSprite != null)
        {
            Destroy(runtimeSprite);
            runtimeSprite = null;
        }

        runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width, 0u, SpriteMeshType.FullRect);
        spriteRenderer.sprite = runtimeSprite;
        spriteRenderer.size = Vector2.one;
    }

    private void EnforceSafeSpriteRendererState()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.sharedMaterial = null;
        spriteRenderer.color = Color.white;
    }

    private void ApplyBlendMode(FieldOverlayBlendMode blendMode)
    {
        if (blendMode == FieldOverlayBlendMode.Additive)
        {
            if (IsUsableMaterial(additiveOverlayMaterial))
            {
                spriteRenderer.sharedMaterial = additiveOverlayMaterial;
                return;
            }

            if (!hasLoggedAdditiveFallback)
            {
                Debug.Log("[FieldOverlayRenderer] additiveOverlayMaterial missing or unsupported; using default SpriteRenderer material");
                hasLoggedAdditiveFallback = true;
            }

            spriteRenderer.sharedMaterial = null;
            return;
        }

        spriteRenderer.sharedMaterial = null;
    }

    private static bool IsUsableMaterial(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.isSupported;
    }
}
