using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class TrailBufferRenderer : MonoBehaviour
{
    [SerializeField] private TrailBufferController trailBuffer;
    [SerializeField] private int sortingOrder = -10;
    [SerializeField] private Material runtimeTrailMaterial;
    [SerializeField] private Material showcaseDefaultSpriteMaterial;

    private static Material runtimeSpriteFallbackMaterial;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;
    private bool loggedMaterialFallbackState;
    private bool loggedUnsupportedFallbackState;
    private bool loggedShownState;
    private bool loggedHiddenState;

    public void Configure(TrailBufferController controller)
    {
        trailBuffer = controller;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ResetRendererState();
        spriteRenderer.sprite = null;
        spriteRenderer.enabled = false;
    }

    private void Start()
    {
        BindTexture();
    }

    private void LateUpdate()
    {
        if (trailBuffer == null || trailBuffer.TrailTexture == null)
        {
            SetHidden(true);
            return;
        }

        if (sprite == null || sprite.texture != trailBuffer.TrailTexture)
        {
            BindTexture();
        }

        ResetRendererState();

        var hideRenderer = !trailBuffer.HasVisibleContent;
        SetHidden(hideRenderer);
        if (hideRenderer)
        {
            return;
        }

        var bounds = trailBuffer.WorldBounds;
        transform.position = new Vector3(bounds.center.x, bounds.center.y, 1f);
        transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
        spriteRenderer.color = Color.white;
        if (!loggedShownState)
        {
            Debug.Log("[TrailBuffer] showing runtime texture", this);
            loggedShownState = true;
        }
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
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.sprite = sprite;
        spriteRenderer.size = Vector2.one;
    }

    private void ResetRendererState()
    {
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.color = Color.white;

        var useRuntimeMaterial = runtimeTrailMaterial != null && runtimeTrailMaterial.shader != null && runtimeTrailMaterial.shader.isSupported;
        if (useRuntimeMaterial)
        {
            spriteRenderer.sharedMaterial = runtimeTrailMaterial;
            return;
        }

        var defaultSpriteMaterial = ResolveDefaultSpriteMaterial();
        spriteRenderer.sharedMaterial = defaultSpriteMaterial;

        if (defaultSpriteMaterial == null)
        {
            if (!loggedUnsupportedFallbackState)
            {
                Debug.LogWarning("[TrailBuffer] URP sprite fallback shader unavailable; using SpriteRenderer default material", this);
                loggedUnsupportedFallbackState = true;
            }

            return;
        }

        if (!loggedMaterialFallbackState)
        {
            Debug.Log("[TrailBuffer] using explicit URP sprite fallback material", this);
            loggedMaterialFallbackState = true;
        }
    }


    private Material ResolveDefaultSpriteMaterial()
    {
        if (showcaseDefaultSpriteMaterial != null && showcaseDefaultSpriteMaterial.shader != null && showcaseDefaultSpriteMaterial.shader.isSupported)
        {
            return showcaseDefaultSpriteMaterial;
        }

        runtimeSpriteFallbackMaterial ??= CreateRuntimeSpriteFallbackMaterial();
        return runtimeSpriteFallbackMaterial;
    }

    private static Material CreateRuntimeSpriteFallbackMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null || !shader.isSupported)
        {
            return null;
        }

        var mat = new Material(shader);
        mat.name = "Runtime_ShowcaseSpriteFallback";
        return mat;
    }

    private void SetHidden(bool hidden)
    {
        if (spriteRenderer.enabled == !hidden)
        {
            return;
        }

        spriteRenderer.enabled = !hidden;

        if (hidden && !loggedHiddenState)
        {
            Debug.Log("[TrailBuffer] hidden because empty", this);
            loggedHiddenState = true;
        }
    }
}
