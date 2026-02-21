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
    [Tooltip("Camera clamp uses this collider's world-space bounds. Prefer a dedicated ArenaBounds BoxCollider2D that matches arena walls exactly.")]
    public Collider2D arenaBoundsCollider;
    [Tooltip("When true and arenaBoundsCollider is empty, auto-wire by searching for a BoxCollider2D named 'ArenaBounds'.")]
    public bool autoFindArenaBoundsCollider = true;

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
    public bool logClampDiagnostics = false;
    public bool drawClampGizmos = false;

    private bool _warnedMissingPixelPerfect;
    private float _nextClampLogTime;

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
        bool wasClamped;
        p = ClampPosition(p, out wasClamped);

        if (logClampDiagnostics && Time.unscaledTime >= _nextClampLogTime)
        {
            _nextClampLogTime = Time.unscaledTime + 1f;
            var worldBounds = GetWorldBounds();
            GetClampExtents(out float minX, out float maxX, out float minY, out float maxY);
            UnityEngine.Debug.Log(
                $"[GSP] Camera clamp bounds min=({worldBounds.min.x:F2},{worldBounds.min.y:F2}) max=({worldBounds.max.x:F2},{worldBounds.max.y:F2}) " +
                $"viewportClampX=[{minX:F2}..{maxX:F2}] viewportClampY=[{minY:F2}..{maxY:F2}] pos=({p.x:F2},{p.y:F2}) clamped={wasClamped}");
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

    public Vector3 ClampPosition(Vector3 candidate, out bool wasClamped)
    {
        wasClamped = false;
        if (targetCamera == null || !clampToBounds)
            return candidate;

        Vector3 clamped = candidate;
        GetClampExtents(out float minX, out float maxX, out float minY, out float maxY);

        float origX = clamped.x;
        float origY = clamped.y;

        clamped.x = minX > maxX ? GetWorldBounds().center.x : Mathf.Clamp(clamped.x, minX, maxX);
        clamped.y = minY > maxY ? GetWorldBounds().center.y : Mathf.Clamp(clamped.y, minY, maxY);
        wasClamped = !Mathf.Approximately(origX, clamped.x) || !Mathf.Approximately(origY, clamped.y);
        return clamped;
    }

    public Bounds GetWorldBounds()
    {
        GetWorldMinMax(out float minX, out float minY, out float maxX, out float maxY);
        Vector3 min = new Vector3(minX, minY, 0f);
        Vector3 max = new Vector3(maxX, maxY, 0f);
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    public void GetClampExtents(out float minX, out float maxX, out float minY, out float maxY)
    {
        GetWorldMinMax(out float worldMinX, out float worldMinY, out float worldMaxX, out float worldMaxY);

        float halfH = targetCamera != null ? targetCamera.orthographicSize : 0f;
        float halfW = halfH * (targetCamera != null ? targetCamera.aspect : 1f);

        minX = worldMinX + halfW;
        maxX = worldMaxX - halfW;
        minY = worldMinY + halfH;
        maxY = worldMaxY - halfH;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawClampGizmos) return;

        AutoWire();
        var worldBounds = GetWorldBounds();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldBounds.center, new Vector3(worldBounds.size.x, worldBounds.size.y, 0f));

        if (targetCamera == null) return;
        float halfH = targetCamera.orthographicSize;
        float halfW = halfH * targetCamera.aspect;
        Vector3 camCenter = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(camCenter, new Vector3(halfW * 2f, halfH * 2f, 0f));
    }

    public void StepZoom(int delta)
    {
        zoomLevel = Mathf.Clamp(zoomLevel + delta, minZoomLevel, maxZoomLevel);
        ApplyZoom();
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

        if (arenaBoundsCollider == null && autoFindArenaBoundsCollider)
        {
            arenaBoundsCollider = FindArenaBoundsCollider();
        }
    }

    private static Collider2D FindArenaBoundsCollider()
    {
        var allBoxColliders = UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);

        foreach (var collider in allBoxColliders)
        {
            if (collider != null && collider.gameObject.name == "ArenaBounds")
            {
                return collider;
            }
        }

        return null;
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
        if (arenaBoundsCollider == null && autoFindArenaBoundsCollider)
        {
            arenaBoundsCollider = FindArenaBoundsCollider();
        }

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
