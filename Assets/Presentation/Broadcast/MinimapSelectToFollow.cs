using UnityEngine;
using UnityEngine.EventSystems;

public class MinimapSelectToFollow : MonoBehaviour, IPointerClickHandler
{
    public Rect worldBounds = new Rect(-32f, -32f, 64f, 64f);
    public MinimapMarkerOverlay overlay;
    public CameraFollowController followController;
    public SelectionService selectionService;
    public float maxRadius = 6f;

    [HideInInspector] public Camera mainCamera;

    private RectTransform minimapRect;
    private void Awake()
    {
        minimapRect = GetComponent<RectTransform>();

        if (selectionService == null)
        {
            selectionService = SelectionService.Instance;
        }

        if (selectionService != null)
        {
            selectionService.SelectedChanged += OnSelectedChanged;
        }
    }

    private void OnDestroy()
    {
        if (selectionService != null)
        {
            selectionService.SelectedChanged -= OnSelectedChanged;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (minimapRect == null)
        {
            minimapRect = GetComponent<RectTransform>();
        }

        if (minimapRect == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect,
                eventData.position,
                eventData.pressEventCamera,
                out var local))
        {
            return;
        }

        var rect = minimapRect.rect;
        var u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        var v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        var worldPoint = new Vector2(
            Mathf.Lerp(worldBounds.xMin, worldBounds.xMax, u),
            Mathf.Lerp(worldBounds.yMin, worldBounds.yMax, v));

        var nearest = FindNearestTarget(worldPoint);
        if (nearest == null)
        {
            return;
        }

        if (selectionService == null)
        {
            selectionService = SelectionService.Instance;
        }

        if (selectionService != null)
        {
            selectionService.SetSelected(nearest, SelectionSource.Minimap);
        }
        else if (overlay != null)
        {
            overlay.target = nearest;
        }

        if (followController != null)
        {
            followController.followEnabled = true;
        }
    }

    public void ClearSelection()
    {
        if (selectionService != null)
        {
            selectionService.Clear();
        }
        else if (overlay != null)
        {
            overlay.target = null;
        }

        if (followController != null)
        {
            followController.target = null;
        }
    }

    private Transform FindNearestTarget(Vector2 worldPoint)
    {
        var entitiesRoot = SelectionRules.FindEntitiesRoot();
        if (entitiesRoot == null)
        {
            return null;
        }

        var candidates = entitiesRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        Transform nearest = null;
        var bestDistanceSq = maxRadius * maxRadius;

        foreach (var renderer in candidates)
        {
            if (renderer == null)
            {
                continue;
            }

            var candidateTransform = SelectionRules.ResolveSelectableSim(renderer.transform, entitiesRoot);
            if (candidateTransform == null)
            {
                continue;
            }

            var candidatePosition = candidateTransform.position;
            var dx = candidatePosition.x - worldPoint.x;
            var dy = candidatePosition.y - worldPoint.y;
            var distanceSq = dx * dx + dy * dy;
            if (distanceSq > bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            nearest = candidateTransform;
        }

        return nearest;
    }

    private void OnSelectedChanged(Transform selected)
    {
        if (overlay != null)
        {
            overlay.target = selected;
        }
    }
}
