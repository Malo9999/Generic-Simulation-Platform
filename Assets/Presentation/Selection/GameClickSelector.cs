using UnityEngine;
using UnityEngine.EventSystems;

public class GameClickSelector : MonoBehaviour
{
    public Camera worldCamera;
    public SelectionService selectionService;
    public float maxPickRadius = 1.5f;

    private void Update()
    {
        if (!WasPrimaryClickThisFrame())
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        if (selectionService == null)
        {
            selectionService = SelectionService.Instance;
        }

        if (selectionService == null)
        {
            return;
        }

        var world = cameraToUse.ScreenToWorldPoint(Input.mousePosition);
        var worldPoint = new Vector2(world.x, world.y);
        var entitiesRoot = SelectionRules.FindEntitiesRoot();
        var target = FindNearestSelectable(worldPoint, entitiesRoot);
        if (target != null)
        {
            selectionService.SetSelected(target);
        }
    }

    private Transform FindNearestSelectable(Vector2 worldPoint, Transform entitiesRoot)
    {
        if (entitiesRoot == null)
        {
            return null;
        }

        var renderers = entitiesRoot.GetComponentsInChildren<SpriteRenderer>(true);
        Transform nearest = null;
        var bestDistanceSq = maxPickRadius * maxPickRadius;

        foreach (var spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            var selectable = SelectionRules.ResolveSelectableSim(spriteRenderer.transform, entitiesRoot);
            if (selectable == null)
            {
                continue;
            }

            var containsPoint = spriteRenderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, spriteRenderer.bounds.center.z));
            var position = selectable.position;
            var dx = position.x - worldPoint.x;
            var dy = position.y - worldPoint.y;
            var distanceSq = dx * dx + dy * dy;

            if (!containsPoint && distanceSq > bestDistanceSq)
            {
                continue;
            }

            if (!containsPoint && nearest != null && distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            nearest = selectable;
        }

        return nearest;
    }

    private static bool WasPrimaryClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            return mouse.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }
}
