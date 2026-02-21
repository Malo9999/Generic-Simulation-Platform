using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MinimapClickToPan : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public Camera mainCamera;
    public ArenaCameraPolicy cameraPolicy;
    public ArenaCameraControls cameraControls;

    // Default guess for a 64x64 arena centered at (0,0): -32..32
    // If your arena is 0..64 instead, change to new Rect(0,0,64,64)
    public Rect worldBounds = new Rect(-32, -32, 64, 64);

    RectTransform _rt;

    void Awake() => _rt = GetComponent<RectTransform>();

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (cameraPolicy == null && mainCamera != null)
            cameraPolicy = mainCamera.GetComponentInParent<ArenaCameraPolicy>();
        if (cameraControls == null && cameraPolicy != null)
            cameraControls = cameraPolicy.GetComponent<ArenaCameraControls>();
    }

    public void OnPointerClick(PointerEventData eventData) => Move(eventData);
    public void OnDrag(PointerEventData eventData) => Move(eventData);

    void Move(PointerEventData e)
    {
        if (mainCamera == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, e.position, e.pressEventCamera, out var local))
            return;

        var r = _rt.rect;

        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);

        Bounds bounds = cameraPolicy != null
            ? cameraPolicy.GetWorldBounds()
            : new Bounds(
                new Vector3(worldBounds.center.x, worldBounds.center.y, 0f),
                new Vector3(worldBounds.width, worldBounds.height, 0f));

        float wx = Mathf.Lerp(bounds.min.x, bounds.max.x, u);
        float wy = Mathf.Lerp(bounds.min.y, bounds.max.y, v);

        Vector3 target = new Vector3(wx, wy, 0f);
        if (cameraPolicy != null)
            target = cameraPolicy.ClampPosition(target, out _);

        if (cameraControls != null)
        {
            target.z = cameraPolicy != null ? cameraPolicy.transform.position.z : 0f;
            cameraControls.TeleportToWorldPosition(target);
            return;
        }

        if (cameraPolicy != null)
        {
            target.z = cameraPolicy.transform.position.z;
            cameraPolicy.transform.position = target;
            return;
        }

        var p = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(target.x, target.y, p.z);
    }
}
