using UnityEngine;

public class BroadcastViewportLayout : MonoBehaviour
{
    public bool enableRightStrip = true;
    [Range(0.1f, 0.4f)] public float rightStripWidth = 0.25f;

    private int lastWidth;
    private int lastHeight;

    private void Start()
    {
        ApplyLayout(force: true);
    }

    private void LateUpdate()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            ApplyLayout(force: true);
        }
    }

    private void OnEnable()
    {
        ApplyLayout(force: true);
    }

    private void OnDisable()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            camera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    private void ApplyLayout(bool force)
    {
        if (!force)
        {
            return;
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;

        var camera = Camera.main;
        if (camera != null)
        {
            camera.rect = enableRightStrip
                ? new Rect(0f, 0f, 1f - rightStripWidth, 1f)
                : new Rect(0f, 0f, 1f, 1f);
        }

        var canvas = GameObject.Find("BroadcastCanvas");
        if (canvas == null)
        {
            return;
        }

        var strip = canvas.transform.Find("UiRightStrip");
        if (strip == null)
        {
            return;
        }

        var stripRect = strip as RectTransform;
        if (stripRect == null)
        {
            return;
        }

        if (enableRightStrip)
        {
            stripRect.anchorMin = new Vector2(1f - rightStripWidth, 0f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = Vector2.zero;
            strip.gameObject.SetActive(true);
        }
        else
        {
            strip.gameObject.SetActive(false);
        }
    }
}
