using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MinimapClickToPan : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public Camera mainCamera;
    public Camera minimapCamera;
    public bool clampToWorldBounds = true;
    public bool routeThroughArenaCameraPolicy = true;
    public bool debugLogClicks = false;

    // Default guess for a 64x64 arena centered at (0,0): -32..32
    // If your arena is 0..64 instead, change to new Rect(0,0,64,64)
    public Rect worldBounds = new Rect(-32, -32, 64, 64);

    RectTransform _rt;

    void Awake() => _rt = GetComponent<RectTransform>();

    public void OnPointerClick(PointerEventData eventData) => Move(eventData);
    public void OnDrag(PointerEventData eventData) => Move(eventData);

    void Move(PointerEventData e)
    {
        if (mainCamera == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, e.position, e.pressEventCamera, out var local))
            return;

        var r = _rt.rect;

        float u = Mathf.Clamp01(Mathf.InverseLerp(r.xMin, r.xMax, local.x));
        float v = Mathf.Clamp01(Mathf.InverseLerp(r.yMin, r.yMax, local.y));

        float wx;
        float wy;
        if (minimapCamera != null)
        {
            var wp = minimapCamera.ViewportToWorldPoint(new Vector3(u, v, minimapCamera.nearClipPlane));
            wx = wp.x;
            wy = wp.y;
        }
        else
        {
            wx = Mathf.Lerp(worldBounds.xMin, worldBounds.xMax, u);
            wy = Mathf.Lerp(worldBounds.yMin, worldBounds.yMax, v);
        }

        if (clampToWorldBounds)
        {
            wx = Mathf.Clamp(wx, worldBounds.xMin, worldBounds.xMax);
            wy = Mathf.Clamp(wy, worldBounds.yMin, worldBounds.yMax);
        }

        var didRouteThroughPolicy = false;
        if (routeThroughArenaCameraPolicy)
        {
            var policy = UnityEngine.Object.FindAnyObjectByType<ArenaCameraPolicy>();
            if (policy != null && policy.targetCamera == mainCamera)
            {
                var policyPosition = policy.transform.position;
                policy.transform.position = new Vector3(wx, wy, policyPosition.z);
                didRouteThroughPolicy = true;
            }
        }

        if (!didRouteThroughPolicy)
        {
            var p = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(wx, wy, p.z);
        }

        if (debugLogClicks)
        {
            Debug.Log($"[MinimapClickToPan] uv=<{u:F3},{v:F3}> world=<{wx:F3},{wy:F3}> bounds=<{worldBounds.xMin:F3},{worldBounds.yMin:F3},{worldBounds.xMax:F3},{worldBounds.yMax:F3}> minimapCam={(minimapCamera != null ? minimapCamera.name : "null")} routePolicy={didRouteThroughPolicy}");
        }
    }
}
