using UnityEngine;

public class WorldSelectionHighlighter : MonoBehaviour
{
    public SelectionService selectionService;

    private GameObject activeRing;
    private static Sprite s_ringSprite;

    private void OnEnable()
    {
        if (selectionService == null)
        {
            selectionService = SelectionService.Instance;
        }

        if (selectionService != null)
        {
            selectionService.SelectedChanged += OnSelectedChanged;
            OnSelectedChanged(selectionService.Selected);
        }
    }

    private void OnDisable()
    {
        if (selectionService != null)
        {
            selectionService.SelectedChanged -= OnSelectedChanged;
        }

        DestroyRing();
    }

    private void OnSelectedChanged(Transform selected)
    {
        DestroyRing();
        if (selected == null)
        {
            return;
        }

        activeRing = new GameObject("SelectionRing", typeof(SpriteRenderer));
        activeRing.transform.SetParent(selected, false);
        activeRing.transform.localPosition = Vector3.zero;

        var spriteRenderer = activeRing.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetRingSprite();
        spriteRenderer.color = new Color(1f, 0.92f, 0.2f, 0.95f);
        spriteRenderer.sortingOrder = 1000;

        var scale = 1.75f;
        var selectedRenderer = selected.GetComponentInChildren<SpriteRenderer>();
        if (selectedRenderer != null)
        {
            var bounds = selectedRenderer.bounds.size;
            var maxDimension = Mathf.Max(bounds.x, bounds.y);
            scale = Mathf.Clamp(maxDimension * 1.8f, 1.5f, 3f);
        }

        activeRing.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void DestroyRing()
    {
        if (activeRing != null)
        {
            Destroy(activeRing);
            activeRing = null;
        }
    }

    private static Sprite GetRingSprite()
    {
        if (s_ringSprite != null)
        {
            return s_ringSprite;
        }

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SelectionRingTexture"
        };

        var center = (size - 1) * 0.5f;
        var innerRadius = size * 0.28f;
        var outerRadius = size * 0.40f;
        var clear = new Color(0f, 0f, 0f, 0f);
        var ring = Color.white;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, distance >= innerRadius && distance <= outerRadius ? ring : clear);
            }
        }

        texture.Apply();

        s_ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        s_ringSprite.name = "SelectionRingSprite";
        return s_ringSprite;
    }
}
