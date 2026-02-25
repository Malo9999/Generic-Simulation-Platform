using UnityEngine;

public class WorldSelectionHighlighter : MonoBehaviour
{
    public SelectionService selectionService;

    private GameObject activeHalo;

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

        DestroyHalo();
    }

    private void OnSelectedChanged(Transform selected)
    {
        DestroyHalo();
        if (selected == null)
        {
            return;
        }

        activeHalo = new GameObject("SelectionHalo");
        activeHalo.transform.SetParent(selected, false);
        activeHalo.transform.localPosition = Vector3.zero;

        CreateHaloRenderer(activeHalo.transform, "HaloBase", SelectionHaloSpriteFactory.GetBaseHaloSprite(), RenderOrder.SelectionRing, Color.white);

        var shineRenderer = CreateHaloRenderer(
            activeHalo.transform,
            "HaloShine",
            SelectionHaloSpriteFactory.GetShineHaloSprite(),
            RenderOrder.SelectionHalo,
            Color.white);

        shineRenderer.gameObject.AddComponent<SelectionHaloTwinkle>();

        var scale = 1f;
        var selectedRenderer = selected.GetComponentInChildren<SpriteRenderer>();
        if (selectedRenderer != null)
        {
            var radiusWorld = Mathf.Max(selectedRenderer.bounds.extents.x, selectedRenderer.bounds.extents.y);
            var desiredWorldRadius = radiusWorld * 1.15f;
            var ringWorldRadiusAtScale1 = SelectionHaloSpriteFactory.DefaultRadius / SelectionHaloSpriteFactory.HaloPixelsPerUnit;
            scale = ringWorldRadiusAtScale1 > 0f
                ? desiredWorldRadius / ringWorldRadiusAtScale1
                : 1f;
        }

        scale = Mathf.Clamp(scale, 0.8f, 2.2f);
        activeHalo.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void DestroyHalo()
    {
        if (activeHalo != null)
        {
            Destroy(activeHalo);
            activeHalo = null;
        }
    }

    private static SpriteRenderer CreateHaloRenderer(Transform parent, string name, Sprite sprite, int sortingOrder, Color color)
    {
        var child = new GameObject(name, typeof(SpriteRenderer));
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;

        var renderer = child.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        RenderOrder.Apply(renderer, sortingOrder);
        renderer.drawMode = SpriteDrawMode.Simple;
        return renderer;
    }
}
