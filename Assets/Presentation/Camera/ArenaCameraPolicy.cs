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
    [Tooltip("When enabled, keep full camera extents inside arena bounds (legacy behavior). When disabled, only camera center is clamped so zoom-out can exceed arena extents.")]
    public bool clampViewExtentsToBounds = false;

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
    [Min(16)]
    public int minPixelPerfectRefResolutionX = 16;

    [Header("Optional Soft Zoom-Out Limit")]
    [Tooltip("Deprecated: zoom is no longer capped from world bounds. Kept for backward inspector compatibility.")]
    public bool useSoftMaxOrthoLimit = false;
    [Min(1f)]
    public float softMaxOrthoFitMultiplier = 1.5f;

    [Header("Simulation-Specific PixelPerfect Overrides")]
    [Tooltip("FantasySport broadcast mode: reduce letterboxing while keeping PixelPerfect camera active.")]
    public bool applyFantasySportPixelPerfectOverrides = true;

    [Header("Pan Quality")]
    public bool snapRigToPixelGrid = false;
    public float fallbackPPU = 32f;

    [Header("Debug")]
    public bool logAutoWire = false;
    public bool logZoomChanges = false;
    public bool debugHud = false;

    private bool _warnedMissingPixelPerfect;
    private float _baseOrthographicSize = -1f;
    private float _lastOrthoBeforeApply;
    private float _lastOrthoAfterApply;
    private float _lastDesiredOrtho;
    private float _lastMaxAllowedOrtho;
    private Rect _lastBoundsRect;
    private string _lastOrthoWriter = "(none)";
    private int _lastOrthoWriteFrame = -1;
    private float _lastFitToBoundsTime = -1f;
    private int _lastFitToBoundsFrame = -1;

    private void Reset() => AutoWire();
    private void OnValidate() => AutoWire();

    private void Awake()
    {
        AutoWire();
        CacheBaseOrthographicSize();
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
            if (clampViewExtentsToBounds)
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
            else
            {
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.y = Mathf.Clamp(p.y, minY, maxY);
            }
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

        if (debugHud)
        {
            CacheDebugState();
        }
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

        SyncZoomLevelToOrtho(requiredOrthographicSize);
        _lastFitToBoundsTime = Time.unscaledTime;
        _lastFitToBoundsFrame = Time.frameCount;
        ApplyZoom("FitToBounds");
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

    public void SetOrthoFromExternal(float value, string writer, bool syncZoomLevel)
    {
        if (syncZoomLevel)
        {
            SyncZoomLevelToOrtho(value);
        }

        SetOrtho(value, writer);
    }

    private void ApplyZoom(string writer = "ApplyZoom")
    {
        AutoWire();
        CacheBaseOrthographicSize();

        float factor = Mathf.Pow(zoomStep, zoomLevel);
        var pixelPerfectActive = IsPixelPerfectActive();
        var beforeOrtho = targetCamera != null ? targetCamera.orthographicSize : 0f;
        var desiredOrtho = Mathf.Max(0.01f, _baseOrthographicSize * factor);
        const float maxAllowedOrtho = -1f;

        _lastOrthoBeforeApply = beforeOrtho;
        _lastDesiredOrtho = desiredOrtho;
        _lastMaxAllowedOrtho = maxAllowedOrtho;
        CacheBoundsForDebug();

        SetOrtho(desiredOrtho, writer);

        if (!pixelPerfectActive)
        {
            _lastOrthoAfterApply = targetCamera != null ? targetCamera.orthographicSize : beforeOrtho;

            if (!_warnedMissingPixelPerfect)
            {
                _warnedMissingPixelPerfect = true;
                UnityEngine.Debug.LogWarning("[GSP] PixelPerfectCamera missing/disabled on target camera. Using Camera.orthographicSize fallback zoom.");
            }
            return;
        }

        _warnedMissingPixelPerfect = false;

        ApplyFantasySportPixelPerfectOverrides();

        var useFantasySportRefResolution = IsCurrentSimulation("FantasySport");
        int rx = useFantasySportRefResolution
            ? Mathf.Max(minPixelPerfectRefResolutionX, baseRefResolution.x)
            : Mathf.Max(minPixelPerfectRefResolutionX, Mathf.RoundToInt(baseRefResolution.x * factor));
        int ry = useFantasySportRefResolution
            ? Mathf.Max(1, baseRefResolution.y)
            : Mathf.RoundToInt(rx * 9f / 16f);

        bool okX = TrySetMember(pixelPerfectComponent, "refResolutionX", rx);
        bool okY = TrySetMember(pixelPerfectComponent, "refResolutionY", ry);

        // Some implementations use a Vector2Int refResolution instead
        if (!okX || !okY)
        {
            TrySetMember(pixelPerfectComponent, "refResolution", new Vector2Int(rx, ry));
        }

        if (logZoomChanges)
            UnityEngine.Debug.Log($"[GSP] Zoom level={zoomLevel} -> refRes={rx}x{ry} (appliedTo={pixelPerfectComponent.GetType().FullName})");

        _lastOrthoAfterApply = targetCamera != null ? targetCamera.orthographicSize : beforeOrtho;
    }

    private void ApplyFantasySportPixelPerfectOverrides()
    {
        if (!applyFantasySportPixelPerfectOverrides || pixelPerfectComponent == null || !IsCurrentSimulation("FantasySport"))
        {
            return;
        }

        TrySetMember(pixelPerfectComponent, "stretchFill", true);
        TrySetMember(pixelPerfectComponent, "cropFrameX", false);
        TrySetMember(pixelPerfectComponent, "cropFrameY", false);
        TrySetMember(pixelPerfectComponent, "upscaleRT", false);
    }

    private bool IsCurrentSimulation(string simulationId)
    {
        var bootstrapper = UnityEngine.Object.FindAnyObjectByType<Bootstrapper>();
        return bootstrapper != null && string.Equals(bootstrapper.CurrentSimulationId, simulationId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void CacheBoundsForDebug()
    {
        GetWorldMinMax(out var minX, out var minY, out var maxX, out var maxY);
        _lastBoundsRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void CacheDebugState()
    {
        var after = targetCamera != null ? targetCamera.orthographicSize : 0f;
        _lastOrthoAfterApply = after;
        CacheBoundsForDebug();
    }

    private void OnGUI()
    {
        if (!debugHud)
        {
            return;
        }

        var pixelPerfectPresent = pixelPerfectComponent != null;
        var pixelPerfectEnabled = IsPixelPerfectActive();
        var upscaleRT = TryGetPixelPerfectMember("upscaleRT", out bool upscale) ? upscale : false;
        var cropX = TryGetPixelPerfectMember("cropFrameX", out bool cx) ? cx : false;
        var cropY = TryGetPixelPerfectMember("cropFrameY", out bool cy) ? cy : false;
        var stretchFill = TryGetPixelPerfectMember("stretchFill", out bool sf) ? sf : false;
        var refX = TryGetPixelPerfectMember("refResolutionX", out int rx) ? rx : -1;
        var refY = TryGetPixelPerfectMember("refResolutionY", out int ry) ? ry : -1;
        var ppu = GetPPU();

        var appliedOrtho = targetCamera != null ? targetCamera.orthographicSize : 0f;
        var fitToBoundsInfo = _lastFitToBoundsFrame == Time.frameCount
            ? $"yes (frame {Time.frameCount})"
            : _lastFitToBoundsTime >= 0f
                ? $"no (last t={_lastFitToBoundsTime:F2}s frame={_lastFitToBoundsFrame})"
                : "no";

        const int x = 10;
        const int y = 10;
        const int width = 760;
        const int height = 250;

        GUI.Box(new Rect(x, y, width, height), "Arena Camera Debug HUD");
        GUILayout.BeginArea(new Rect(x + 10, y + 24, width - 20, height - 34));
        GUILayout.Label($"Screen: {Screen.width}x{Screen.height}");
        GUILayout.Label($"PixelPerfect: present={pixelPerfectPresent} active={pixelPerfectEnabled} ref={refX}x{refY} assetsPPU={ppu:F2} cropX={cropX} cropY={cropY} upscaleRT={upscaleRT} stretchFill={stretchFill}");
        GUILayout.Label($"Zoom: level={zoomLevel} min={minZoomLevel} max={maxZoomLevel} step={zoomStep:F3}");
        GUILayout.Label($"Ortho: desired={_lastDesiredOrtho:F3} applied={appliedOrtho:F3} before={_lastOrthoBeforeApply:F3} maxAllowed={_lastMaxAllowedOrtho:F3}");
        GUILayout.Label($"Bounds rect: x={_lastBoundsRect.xMin:F2} y={_lastBoundsRect.yMin:F2} w={_lastBoundsRect.width:F2} h={_lastBoundsRect.height:F2}");
        GUILayout.Label($"FitToBounds ran this frame: {fitToBoundsInfo}");
        GUILayout.Label($"lastWriter(frame): {_lastOrthoWriter} ({_lastOrthoWriteFrame})");
        GUILayout.EndArea();
    }

    private void SyncZoomLevelToOrtho(float orthographicSize)
    {
        CacheBaseOrthographicSize();
        var baseOrtho = Mathf.Max(0.01f, _baseOrthographicSize);
        var safeZoomStep = Mathf.Max(1.0001f, zoomStep);
        var normalized = Mathf.Max(0.01f, orthographicSize) / baseOrtho;
        var level = Mathf.RoundToInt(Mathf.Log(normalized) / Mathf.Log(safeZoomStep));
        zoomLevel = Mathf.Clamp(level, minZoomLevel, maxZoomLevel);
    }

    private void SetOrtho(float value, string writer)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = Mathf.Max(0.01f, value);
        _lastOrthoWriter = string.IsNullOrWhiteSpace(writer) ? "(unknown)" : writer;
        _lastOrthoWriteFrame = Time.frameCount;
    }

    private bool IsPixelPerfectActive()
    {
        if (pixelPerfectComponent == null)
        {
            return false;
        }

        if (pixelPerfectComponent is Behaviour behaviour)
        {
            return behaviour.isActiveAndEnabled;
        }

        return true;
    }

    private void CacheBaseOrthographicSize()
    {
        if (targetCamera == null || _baseOrthographicSize > 0f)
        {
            return;
        }

        _baseOrthographicSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
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

    private bool TryGetPixelPerfectMember<T>(string name, out T value)
    {
        value = default;
        return pixelPerfectComponent != null && TryGetMember(pixelPerfectComponent, name, out value);
    }
}
