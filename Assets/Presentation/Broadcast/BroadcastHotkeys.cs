using UnityEngine;

public class BroadcastHotkeys : MonoBehaviour
{
    public CameraFollowController followController;
    public MinimapMarkerOverlay overlay;
    public MinimapSelectToFollow selector;


    private void Update()
    {
        if (WasFollowPressed())
        {
            ToggleFollow();
        }

        if (WasClearPressed())
        {
            ClearSelection();
        }
    }

    private void ToggleFollow()
    {
        if (followController == null)
        {
            return;
        }

        followController.followEnabled = !followController.followEnabled;
    }

    private void ClearSelection()
    {
        if (overlay != null)
        {
            overlay.target = null;
        }

        if (followController != null)
        {
            followController.target = null;
        }

        if (selector != null)
        {
            selector.ClearSelection();
        }
    }

    private static bool WasFollowPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.fKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.F);
    }

    private static bool WasClearPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.escapeKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }
}
