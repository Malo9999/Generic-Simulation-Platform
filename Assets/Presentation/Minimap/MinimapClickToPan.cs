using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MinimapClickToPan : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public Camera mainCamera;

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

        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);

        float wx = Mathf.Lerp(worldBounds.xMin, worldBounds.xMax, u);
        float wy = Mathf.Lerp(worldBounds.yMin, worldBounds.yMax, v);

        var p = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(wx, wy, p.z);
    }
}