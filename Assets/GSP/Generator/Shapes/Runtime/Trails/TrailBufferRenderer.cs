using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class TrailBufferRenderer : MonoBehaviour
{
    [SerializeField] private TrailBufferController trailBuffer;
    [SerializeField] private int sortingOrder = -10;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;
    private bool loggedMaterialState;
    private bool loggedHiddenState;

    public void Configure(TrailBufferController controller)
    {
        trailBuffer = controller;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.color = Color.white;
        ApplySafeMaterialFallback();
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

        ApplySafeMaterialFallback();

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

    private void ApplySafeMaterialFallback()
    {
        var assignedMaterial = spriteRenderer.sharedMaterial;
        var useAssignedMaterial = assignedMaterial != null && assignedMaterial.shader != null && assignedMaterial.shader.isSupported;
        if (!useAssignedMaterial)
        {
            spriteRenderer.sharedMaterial = null;
        }

        if (!loggedMaterialState)
        {
            var matName = assignedMaterial != null ? assignedMaterial.name : "<default>";
            var shaderName = assignedMaterial != null && assignedMaterial.shader != null ? assignedMaterial.shader.name : "<none>";
            var status = useAssignedMaterial ? "using-assigned" : "using-default";
            Debug.Log($"[TrailBufferRenderer] material={matName} shader={shaderName} state={status}", this);
            loggedMaterialState = true;
        }
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
            Debug.Log("[TrailBufferRenderer] overlay hidden because trail buffer is empty", this);
            loggedHiddenState = true;
        }
    }
}
