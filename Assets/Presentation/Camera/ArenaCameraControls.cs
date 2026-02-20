using UnityEngine;
using UnityEngine.InputSystem;

public class ArenaCameraControls : MonoBehaviour
{
    [Header("References")]
    public ArenaCameraPolicy policy;

    [Header("Pan")]
    public float panSpeedUnitsPerSecond = 12f;
    [Tooltip("Lower = snappier, higher = smoother but laggier.")]
    public float panSmoothTime = 0.06f;
    public bool allowRightMouseDrag = true;

    [Header("Zoom")]
    public bool enableZoom = true;
    [Tooltip("+1 means wheel up zooms IN (smaller view). If inverted, set -1.")]
    public int scrollZoomDirection = +1;

    [Header("Debug")]
    public bool logZoom = false;

    private Camera _cam;
    private Vector3 _desiredPos;
    private Vector3 _vel;

    private bool _dragging;
    private Vector3 _dragStartRigPos;
    private Vector3 _dragStartMouseWorld;

    private void Reset()
    {
        if (policy == null) policy = GetComponent<ArenaCameraPolicy>();
    }

    private void Awake()
    {
        if (policy == null) policy = GetComponent<ArenaCameraPolicy>();
        _cam = policy != null ? policy.targetCamera : GetComponentInChildren<Camera>();
        _desiredPos = transform.position;
    }

    private void Update()
    {
        if (policy == null) return;

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

        // --- RMB drag pan (world-space) ---
        if (allowRightMouseDrag && mouse != null && _cam != null)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _dragging = true;
                _dragStartRigPos = _desiredPos;
                _dragStartMouseWorld = MouseWorldZ0(mouse.position.ReadValue());
            }
            else if (mouse.rightButton.wasReleasedThisFrame)
            {
                _dragging = false;
            }
            else if (_dragging && mouse.rightButton.isPressed)
            {
                Vector3 now = MouseWorldZ0(mouse.position.ReadValue());
                Vector3 delta = _dragStartMouseWorld - now;
                _desiredPos = _dragStartRigPos + delta;
            }
        }

        // Apply smoothing
        transform.position = Vector3.SmoothDamp(
            transform.position,
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

    private Vector3 MouseWorldZ0(Vector2 mouseScreenPos)
    {
        float zDist = _cam != null ? -_cam.transform.position.z : 10f;
        var sp = new Vector3(mouseScreenPos.x, mouseScreenPos.y, zDist);
        var wp = _cam.ScreenToWorldPoint(sp);
        wp.z = 0f;
        return wp;
    }
}