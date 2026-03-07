using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ShapeShowcaseCameraController : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minOrthoSize = 2f;
    [SerializeField] private float maxOrthoSize = 40f;

    [Header("Pan")]
    [SerializeField] private float panSpeed = 1f;

    [Header("Framing")]
    [SerializeField] private float frameMargin = 1.1f;

    private Camera cachedCamera;
    private Vector3 defaultPosition;
    private float defaultOrthoSize;

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        defaultPosition = transform.position;
        defaultOrthoSize = cachedCamera.orthographicSize;

        if (!cachedCamera.orthographic)
        {
            Debug.LogWarning($"{nameof(ShapeShowcaseCameraController)} requires an orthographic camera.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
        {
            return;
        }

        HandleZoom(mouse);
        HandlePan(mouse);

        if (keyboard.fKey.wasPressedThisFrame)
        {
            FrameShowcaseContent();
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetView();
        }
    }

    private void HandleZoom(Mouse mouse)
    {
        var scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            return;
        }

        cachedCamera.orthographicSize = Mathf.Clamp(
            cachedCamera.orthographicSize - (scrollY * zoomSpeed * 0.01f),
            minOrthoSize,
            maxOrthoSize);
    }

    private void HandlePan(Mouse mouse)
    {
        if (!mouse.middleButton.isPressed && !mouse.rightButton.isPressed)
        {
            return;
        }

        var delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        var worldA = cachedCamera.ScreenToWorldPoint(new Vector3(0f, 0f, cachedCamera.nearClipPlane));
        var worldB = cachedCamera.ScreenToWorldPoint(new Vector3(delta.x, delta.y, cachedCamera.nearClipPlane));
        var worldDelta = worldB - worldA;

        transform.position -= worldDelta * panSpeed;
    }

    private void ResetView()
    {
        transform.position = defaultPosition;
        cachedCamera.orthographicSize = Mathf.Clamp(defaultOrthoSize, minOrthoSize, maxOrthoSize);
    }

    private void FrameShowcaseContent()
    {
        var bootstrap = FindAnyObjectByType<ShapeShowcaseBootstrap>();
        if (bootstrap == null)
        {
            return;
        }

        if (!TryBuildShowcaseBounds(bootstrap.transform, out var bounds))
        {
            return;
        }

        var center = bounds.center;
        transform.position = new Vector3(center.x, center.y, transform.position.z);

        var extentY = bounds.extents.y;
        var extentX = bounds.extents.x / Mathf.Max(0.01f, cachedCamera.aspect);
        var targetSize = Mathf.Max(extentY, extentX) * frameMargin;

        cachedCamera.orthographicSize = Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize);
    }

    private static bool TryBuildShowcaseBounds(Transform root, out Bounds bounds)
    {
        var hasBounds = false;
        bounds = default;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (!hasBounds)
            {
                bounds = sr.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(sr.bounds);
        }

        var textMeshes = root.GetComponentsInChildren<TextMesh>();
        foreach (var tm in textMeshes)
        {
            var tmRenderer = tm.GetComponent<Renderer>();
            if (tmRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = tmRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(tmRenderer.bounds);
        }

        return hasBounds;
    }
}
