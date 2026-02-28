using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using Component = UnityEngine.Component;

public class ArenaCameraPolicy : MonoBehaviour
{
    [Header("Auto-wired (can be empty)")]
    public Camera targetCamera;

    [Tooltip("Leave empty; we auto-find the PixelPerfectCamera component by name on the target camera.")]
    public Component pixelPerfectComponent;

    [Header("World Bounds (fallback if no collider)")]
    public Vector2 worldSize = new Vector2(200f, 120f);
    public bool centeredAtOrigin = true; // true: [-W/2..+W/2], false: [0..W]
    public bool clampToBounds = true;

    [Header("Optional: auto-bounds from collider")]
    [Tooltip("Camera clamp uses this collider\'s world-space bounds. Ensure this collider fully covers the playable arena extents.")]
    public Collider2D arenaBoundsCollider;
    public bool autoFindArenaBoundsCollider = true;

    [Header("Fit To Bounds")]
    [Range(0f, 0.5f)]
    public float fitMarginPercent = 0.06f;

    [Header("Pixel Perfect Zoom")]
    public Vector2Int baseRefResolution = new Vector2Int(480, 270);
    public float zoomStep = 1.5f;
    public int zoomLevel = 0;
    public int minZoomLevel = -6;
    public int maxZoomLevel = 6;

    [Header("Pan Quality")]
    public bool snapRigToPixelGrid = false;
    public float fallbackPPU = 32f;

    [Header("Debug")]
    public bool logAutoWire = false;
    public bool logZoomChanges = false;

    private bool _warnedMissingPixelPerfect;

    private void Reset() => AutoWire();
    private void OnValidate() => AutoWire();

    private void Awake()
    {
        AutoWire();
        ApplyZoom();
    }

    private void LateUpdate()
    {
        AutoWire();
        if (targetCamera == null) return;

        Vector3 p = transform.position;

        GetWorldMinMax(out float minX, out float minY, out float maxX, out float maxY);

        if (clampToBounds)
        {
            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            float worldW = maxX - minX;
            float worldH = maxY - minY;

            if (halfW * 2f >= worldW) p.x = (minX + maxX) * 0.5f;
            else p.x = Mathf.Clamp(p.x, minX + halfW, maxX - halfW);

            if (halfH * 2f >= worldH) p.y = (minY + maxY) * 0.5f;
            else p.y = Mathf.Clamp(p.y, minY + halfH, maxY - halfH);
        }

        if (snapRigToPixelGrid)
        {
            float ppu = GetPPU();
            if (ppu > 0.01f)
            {
                p.x = Mathf.Round(p.x * ppu) / ppu;
                p.y = Mathf.Round(p.y * ppu) / ppu;
            }
        }

        transform.position = p;
    }

    public void StepZoom(int delta)
    {
        zoomLevel = Mathf.Clamp(zoomLevel + delta, minZoomLevel, maxZoomLevel);
        ApplyZoom();
    }

    public void FitToBounds()
    {
        AutoWire();
        if (targetCamera == null)
        {
            return;
        }

        GetWorldMinMax(out var minX, out var minY, out var maxX, out var maxY);
        var width = Mathf.Max(0.01f, maxX - minX);
        var height = Mathf.Max(0.01f, maxY - minY);
        var pad = Mathf.Max(width, height) * Mathf.Clamp01(fitMarginPercent);

        width += pad * 2f;
        height += pad * 2f;

        var aspect = Mathf.Max(0.01f, targetCamera.aspect);
        var requiredOrthographicSize = Mathf.Max(height * 0.5f, (width * 0.5f) / aspect);

        var center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, transform.position.z);
        transform.position = center;

        if (pixelPerfectComponent == null)
        {
            targetCamera.orthographicSize = requiredOrthographicSize;
            return;
        }

        var bestLevel = minZoomLevel;
        var foundFit = false;

        for (var level = maxZoomLevel; level >= minZoomLevel; level--)
        {
            zoomLevel = level;
            ApplyZoom();

            if (targetCamera.orthographicSize + 0.001f >= requiredOrthographicSize)
            {
                bestLevel = level;
                foundFit = true;
                break;
            }
        }

        if (!foundFit)
        {
            bestLevel = minZoomLevel;
        }

        zoomLevel = Mathf.Clamp(bestLevel, minZoomLevel, maxZoomLevel);
        ApplyZoom();
    }

    public void BindArenaBounds(Collider2D boundsCollider, bool fitToBounds)
    {
        if (boundsCollider != null)
        {
            arenaBoundsCollider = boundsCollider;
        }

        if (fitToBounds)
        {
            FitToBounds();
        }
    }

    public bool TryGetWorldBoundsRect(out Rect boundsRect)
    {
        GetWorldMinMax(out var minX, out var minY, out var maxX, out var maxY);
        boundsRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return boundsRect.width > 0f && boundsRect.height > 0f;
    }

    private void ApplyZoom()
    {
        AutoWire();

        if (pixelPerfectComponent == null)
        {
            if (!_warnedMissingPixelPerfect)
            {
                _warnedMissingPixelPerfect = true;
                UnityEngine.Debug.LogWarning("[GSP] PixelPerfectCamera component not found on the target camera. Zoom will not work.");
            }
            return;
        }

        float factor = Mathf.Pow(zoomStep, zoomLevel);

        int rx = Mathf.Max(64, Mathf.RoundToInt(baseRefResolution.x * factor));
        int ry = Mathf.RoundToInt(rx * 9f / 16f);

        bool okX = TrySetMember(pixelPerfectComponent, "refResolutionX", rx);
        bool okY = TrySetMember(pixelPerfectComponent, "refResolutionY", ry);

        // Some implementations use a Vector2Int refResolution instead
        if (!okX || !okY)
        {
            TrySetMember(pixelPerfectComponent, "refResolution", new Vector2Int(rx, ry));
        }

        if (logZoomChanges)
            UnityEngine.Debug.Log($"[GSP] Zoom level={zoomLevel} -> refRes={rx}x{ry} (appliedTo={pixelPerfectComponent.GetType().FullName})");
    }

    private void AutoWire()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>(true);

        if (pixelPerfectComponent == null && targetCamera != null)
        {
            pixelPerfectComponent = FindPixelPerfectOnCamera(targetCamera);
        }

        if (autoFindArenaBoundsCollider && arenaBoundsCollider == null)
        {
            var arenaBoundsObject = GameObject.Find("ArenaBounds");
            if (arenaBoundsObject != null)
            {
                arenaBoundsCollider = arenaBoundsObject.GetComponent<Collider2D>();
            }
        }

        if (pixelPerfectComponent == null)
        {
            // fallback: scan all cameras (useful if targetCamera got swapped)
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                var pp = FindPixelPerfectOnCamera(cam);
                if (pp != null)
                {
                    pixelPerfectComponent = pp;
                    break;
                }
            }
        }

        if (logAutoWire)
        {
            UnityEngine.Debug.Log($"[GSP] AutoWire cam={(targetCamera ? targetCamera.name : "null")} pixelPerfect={(pixelPerfectComponent ? pixelPerfectComponent.GetType().FullName : "NULL")}");
        }
    }

    private static Component FindPixelPerfectOnCamera(Camera cam)
    {
        // We detect by type name to survive namespace/package differences.
        var comps = cam.GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            if (c.GetType().Name == "PixelPerfectCamera")
                return c;
        }
        return null;
    }

    private float GetPPU()
    {
        if (pixelPerfectComponent == null) return fallbackPPU;

        // Common member names
        if (TryGetMember<float>(pixelPerfectComponent, "assetsPPU", out var ppu)) return ppu;
        if (TryGetMember<float>(pixelPerfectComponent, "assetsPixelsPerUnit", out ppu)) return ppu;

        // In case it’s an int
        if (TryGetMember<int>(pixelPerfectComponent, "assetsPPU", out var ppuI)) return ppuI;
        if (TryGetMember<int>(pixelPerfectComponent, "assetsPixelsPerUnit", out ppuI)) return ppuI;

        return fallbackPPU;
    }

    private void GetWorldMinMax(out float minX, out float minY, out float maxX, out float maxY)
    {
        if (arenaBoundsCollider != null)
        {
            var b = arenaBoundsCollider.bounds;
            minX = b.min.x; minY = b.min.y;
            maxX = b.max.x; maxY = b.max.y;
            return;
        }

        if (centeredAtOrigin)
        {
            minX = -worldSize.x * 0.5f;
            minY = -worldSize.y * 0.5f;
            maxX = worldSize.x * 0.5f;
            maxY = worldSize.y * 0.5f;
        }
        else
        {
            minX = 0f; minY = 0f;
            maxX = worldSize.x; maxY = worldSize.y;
        }
    }

    private static bool TrySetMember(Component c, string name, object value)
    {
        var t = c.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var f = t.GetField(name, flags);
        if (f != null)
        {
            f.SetValue(c, value);
            return true;
        }

        var p = t.GetProperty(name, flags);
        if (p != null && p.CanWrite)
        {
            p.SetValue(c, value);
            return true;
        }

        return false;
    }

    private static bool TryGetMember<T>(Component c, string name, out T value)
    {
        value = default;
        var t = c.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var f = t.GetField(name, flags);
        if (f != null && f.GetValue(c) is T fv)
        {
            value = fv;
            return true;
        }

        var p = t.GetProperty(name, flags);
        if (p != null && p.CanRead && p.GetValue(c) is T pv)
        {
            value = pv;
            return true;
        }

        return false;
    }
}
