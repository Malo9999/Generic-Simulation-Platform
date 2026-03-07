using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FieldOverlayRenderer : MonoBehaviour
{
    [SerializeField] private FieldBufferController fieldBuffer;
    [SerializeField] private Material additiveOverlayMaterial;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;
    private bool hasLoggedBlendMaterialSelection;

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
        var useAdditiveMaterial = blendMode == FieldOverlayBlendMode.Additive && IsUsableMaterial(additiveOverlayMaterial);

        if (!hasLoggedBlendMaterialSelection)
        {
            LogBlendMaterialSelection(blendMode, useAdditiveMaterial);
            hasLoggedBlendMaterialSelection = true;
        }

        if (useAdditiveMaterial)
        {
            spriteRenderer.sharedMaterial = additiveOverlayMaterial;
            return;
        }

        spriteRenderer.sharedMaterial = null;
    }

    private void LogBlendMaterialSelection(FieldOverlayBlendMode blendMode, bool useAdditiveMaterial)
    {
        if (blendMode == FieldOverlayBlendMode.Additive)
        {
            if (additiveOverlayMaterial == null)
            {
                Debug.Log("[FieldOverlayRenderer] blend=Additive material=null using default sprite material fallback");
                return;
            }

            var shaderName = additiveOverlayMaterial.shader != null ? additiveOverlayMaterial.shader.name : "<null>";
            if (!useAdditiveMaterial)
            {
                Debug.Log($"[FieldOverlayRenderer] blend=Additive material={additiveOverlayMaterial.name} shader={shaderName} using default sprite material fallback");
                return;
            }

            Debug.Log($"[FieldOverlayRenderer] blend=Additive material={additiveOverlayMaterial.name} shader={shaderName} using additive overlay material");
            return;
        }

        Debug.Log($"[FieldOverlayRenderer] blend={blendMode} using default sprite material fallback");
    }

    private static bool IsUsableMaterial(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.isSupported;
    }
}
