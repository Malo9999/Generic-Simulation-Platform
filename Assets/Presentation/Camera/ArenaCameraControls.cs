using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ArenaCameraControls : MonoBehaviour
{
    [Header("References")]
    public ArenaCameraPolicy policy;

    [Header("Pan")]
    public float panSpeedUnitsPerSecond = 12f;
    [Tooltip("Lower = snappier, higher = smoother but laggier.")]
    public float panSmoothTime = 0.06f;
    public bool allowRightMouseDrag = true;
    public bool allowMiddleMouseDrag = true;
    public bool blockDragStartOverUI = true;

    [Header("Zoom")]
    public bool enableZoom = true;
    [Tooltip("+1 means wheel up zooms IN (smaller view). If inverted, set -1.")]
    public int scrollZoomDirection = +1;

    [Header("Debug")]
    public bool logZoom = false;

    private Camera _cam;
    private Transform _rig;
    private Vector3 _desiredPos;
    private Vector3 _vel;

    private bool _dragging;
    private Vector3 _dragStartRigPos;
    private Vector2 _dragStartMouseScreen;

    private void Reset()
    {
        if (policy == null) policy = GetComponent<ArenaCameraPolicy>();
    }

    private void Awake()
    {
        if (policy == null) policy = GetComponent<ArenaCameraPolicy>();
        ResolveReferences();
        _desiredPos = _rig.position;
    }

    private void Update()
    {
        if (policy == null) return;
        ResolveReferences();

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // --- Keyboard pan (WASD + arrows) ---
        Vector3 dir = Vector3.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
        }

        if (!_dragging && dir.sqrMagnitude > 0f)
        {
            dir.Normalize();
            _desiredPos += dir * panSpeedUnitsPerSecond * Time.unscaledDeltaTime;
        }

        // --- Mouse drag pan ---
        if (mouse != null && _cam != null)
        {
            bool dragPressedThisFrame =
                (allowRightMouseDrag && mouse.rightButton.wasPressedThisFrame) ||
                (allowMiddleMouseDrag && mouse.middleButton.wasPressedThisFrame);

            bool dragReleasedThisFrame =
                (allowRightMouseDrag && mouse.rightButton.wasReleasedThisFrame) ||
                (allowMiddleMouseDrag && mouse.middleButton.wasReleasedThisFrame);

            bool dragHeld =
                (allowRightMouseDrag && mouse.rightButton.isPressed) ||
                (allowMiddleMouseDrag && mouse.middleButton.isPressed);

            if (dragPressedThisFrame)
            {
                if (blockDragStartOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    _dragging = false;
                }
                else
                {
                    _dragging = true;
                    _dragStartRigPos = _desiredPos;
                    _dragStartMouseScreen = mouse.position.ReadValue();
                }
            }
            else if (dragReleasedThisFrame)
            {
                _dragging = false;
            }
            else if (_dragging && !dragHeld)
            {
                // Handles missed MouseUp when focus leaves the game view/window.
                _dragging = false;
            }
            else if (_dragging && dragHeld)
            {
                Vector2 now = mouse.position.ReadValue();
                Vector2 pixelDelta = now - _dragStartMouseScreen;
                _desiredPos = _dragStartRigPos + ScreenToWorldPanDelta(-pixelDelta);
            }
        }

        // Apply smoothing
        _rig.position = Vector3.SmoothDamp(
            _rig.position,
            _desiredPos,
            ref _vel,
            panSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        // --- Zoom ---
        if (enableZoom && mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;

            // Convert scroll to a single step (trackpads can give small values)
            int step = 0;
            if (scrollY > 0.01f) step = +1;
            else if (scrollY < -0.01f) step = -1;

            if (step != 0)
            {
                // wheel up => zoom in by default
                policy.StepZoom(step * scrollZoomDirection);

                if (logZoom)
                    UnityEngine.Debug.Log($"[GSP] Zoom step {step} -> level {policy.zoomLevel}");
            }
        }
    }

    private Vector3 ScreenToWorldPanDelta(Vector2 pixelDelta)
    {
        if (_cam == null)
            return new Vector3(pixelDelta.x, pixelDelta.y, 0f);

        if (!_cam.orthographic)
        {
            float zDist = Mathf.Max(0.01f, -_cam.transform.position.z);
            Vector3 a = _cam.ScreenToWorldPoint(new Vector3(0f, 0f, zDist));
            Vector3 b = _cam.ScreenToWorldPoint(new Vector3(pixelDelta.x, pixelDelta.y, zDist));
            Vector3 d = b - a;
            d.z = 0f;
            return d;
        }

        float worldUnitsPerPixelY = (2f * _cam.orthographicSize) / Mathf.Max(1, _cam.pixelHeight);
        float worldUnitsPerPixelX = (2f * _cam.orthographicSize * _cam.aspect) / Mathf.Max(1, _cam.pixelWidth);

        return new Vector3(pixelDelta.x * worldUnitsPerPixelX, pixelDelta.y * worldUnitsPerPixelY, 0f);
    }

    private void OnDisable()
    {
        StopDragging();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            StopDragging();
    }

    private void ResolveReferences()
    {
        _rig = policy != null ? policy.transform : transform;
        _cam = policy != null ? policy.targetCamera : _cam;

        if (_cam == null)
            _cam = GetComponentInChildren<Camera>();

        if (!_dragging)
            _desiredPos = _rig.position;
    }

    private void StopDragging()
    {
        _dragging = false;
        _vel = Vector3.zero;
        _dragStartRigPos = _desiredPos;
    }

    public void TeleportToWorldPosition(Vector3 worldPosition)
    {
        ResolveReferences();

        Vector3 target = worldPosition;
        if (policy != null)
        {
            target = policy.ClampPosition(target, out _);
        }

        StopDragging();
        _desiredPos = target;
        if (_rig != null)
            _rig.position = target;
    }
}
