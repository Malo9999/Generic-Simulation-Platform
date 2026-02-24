using UnityEngine;
using UnityEngine.EventSystems;

public class MinimapSelectToFollow : MonoBehaviour, IPointerClickHandler
{
    public Rect worldBounds = new Rect(-32f, -32f, 64f, 64f);
    public MinimapMarkerOverlay overlay;
    public CameraFollowController followController;
    public float maxRadius = 6f;

    [HideInInspector] public Camera mainCamera;

    private RectTransform minimapRect;
    private Transform selectedTarget;

    private void Awake()
    {
        minimapRect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (followController == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            followController.followEnabled = !followController.followEnabled;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
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

        selectedTarget = nearest;
        if (overlay != null)
        {
            overlay.target = selectedTarget;
        }

        if (followController != null)
        {
            followController.target = selectedTarget;
            followController.followEnabled = true;
        }
    }

    private void ClearSelection()
    {
        selectedTarget = null;

        if (overlay != null)
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
        var simulationRoot = GameObject.Find("SimulationRoot")?.transform;
        if (simulationRoot == null)
        {
            simulationRoot = GameObject.Find("SimRoot")?.transform;
        }

        if (simulationRoot == null)
        {
            return null;
        }

        var candidates = simulationRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        var agentsLayer = LayerMask.NameToLayer("Agents");
        var requireAgentsLayer = agentsLayer >= 0;

        Transform nearest = null;
        var bestDistanceSq = maxRadius * maxRadius;

        foreach (var renderer in candidates)
        {
            if (renderer == null)
            {
                continue;
            }

            var candidateTransform = renderer.transform;
            if (requireAgentsLayer && candidateTransform.gameObject.layer != agentsLayer)
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
}
