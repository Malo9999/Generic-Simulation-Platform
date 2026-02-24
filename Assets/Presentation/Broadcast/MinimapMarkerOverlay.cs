using UnityEngine;
using UnityEngine.UI;

public class MinimapMarkerOverlay : MonoBehaviour
{
    public RectTransform minimapRect;
    public Camera minimapCamera;
    public Transform target;
    public Image markerImage;

    private const string MarkerName = "SelectedMarker";

    private void OnEnable()
    {
        EnsureMarker();
    }

    private void Start()
    {
        EnsureMarker();
    }

    private void Update()
    {
        EnsureMarker();
        if (markerImage == null || minimapRect == null || minimapCamera == null || target == null)
        {
            SetMarkerVisible(false);
            return;
        }

        var viewportPoint = minimapCamera.WorldToViewportPoint(target.position);
        if (viewportPoint.z < 0f)
        {
            SetMarkerVisible(false);
            return;
        }

        var anchoredPosition = new Vector2(
            (viewportPoint.x - 0.5f) * minimapRect.rect.width,
            (viewportPoint.y - 0.5f) * minimapRect.rect.height);

        markerImage.rectTransform.anchoredPosition = anchoredPosition;
        SetMarkerVisible(true);
    }

    private void EnsureMarker()
    {
        if (minimapRect == null)
        {
            minimapRect = transform as RectTransform;
        }

        if (minimapRect == null)
        {
            return;
        }

        if (markerImage == null)
        {
            var existing = minimapRect.Find(MarkerName);
            if (existing != null)
            {
                markerImage = existing.GetComponent<Image>();
            }
        }

        if (markerImage != null)
        {
            ConfigureMarkerImage(markerImage);
            return;
        }

        var markerObject = new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(minimapRect, false);
        markerImage = markerObject.GetComponent<Image>();
        ConfigureMarkerImage(markerImage);
    }

    private static void ConfigureMarkerImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.color = Color.yellow;
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(10f, 10f);
    }

    private void SetMarkerVisible(bool visible)
    {
        if (markerImage != null && markerImage.gameObject.activeSelf != visible)
        {
            markerImage.gameObject.SetActive(visible);
        }
    }
}
