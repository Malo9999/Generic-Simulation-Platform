using UnityEngine;
using UnityEngine.U2D;

[DefaultExecutionOrder(-1000)]
public class ArenaCameraPolicy : MonoBehaviour
{
    [Header("Debug Arena (Pixels)")]
    public int targetWidthPx = 1920;
    public int targetHeightPx = 1080;

    [Header("Pixels Per Unit (match your sprites)")]
    public int assetsPPU = 32;

    public enum OriginMode { BottomLeft, Centered }
    [Header("Coordinate convention used by your sims")]
    public OriginMode originMode = OriginMode.BottomLeft;

    [Header("Pixel Perfect")]
    public bool forcePixelPerfect = true;
    public bool useUpscaleRT = true;
    public bool pixelSnapping = false;
    public bool cropFrameX = false;
    public bool cropFrameY = false;

    [Header("Optional: draw arena border")]
    public bool drawBorder = true;
    public float borderWidthWorld = 0.05f;
    public int borderSortingOrder = 1000;

    Camera _cam;
    PixelPerfectCamera _ppc;
    LineRenderer _lr;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;

        if (_cam == null)
        {
            UnityEngine.Debug.LogWarning("[ArenaCameraPolicy] No Camera found.");
            return;
        }

        _ppc = _cam.GetComponent<PixelPerfectCamera>();

        if (forcePixelPerfect && _ppc != null)
        {
            _ppc.assetsPPU = assetsPPU;
            _ppc.refResolutionX = targetWidthPx;
            _ppc.refResolutionY = targetHeightPx;
            _ppc.upscaleRT = useUpscaleRT;
            _ppc.pixelSnapping = pixelSnapping;
            _ppc.cropFrameX = cropFrameX;
            _ppc.cropFrameY = cropFrameY;
        }

        // World-space arena size from pixels + PPU
        float w = targetWidthPx / (float)assetsPPU;
        float h = targetHeightPx / (float)assetsPPU;

        // Center camera based on convention
        Vector3 pos = _cam.transform.position;
        if (originMode == OriginMode.BottomLeft)
        {
            pos.x = w * 0.5f;
            pos.y = h * 0.5f;
        }
        else
        {
            pos.x = 0f;
            pos.y = 0f;
        }
        _cam.transform.position = pos;

        if (drawBorder)
            EnsureBorder(w, h);
    }

    void EnsureBorder(float w, float h)
    {
        if (_lr == null)
        {
            var go = GameObject.Find("__ArenaBorder");
            if (go == null) go = new GameObject("__ArenaBorder");

            _lr = go.GetComponent<LineRenderer>();
            if (_lr == null) _lr = go.AddComponent<LineRenderer>();

            _lr.useWorldSpace = true;
            _lr.loop = true;
            _lr.positionCount = 4;
            _lr.material = new Material(Shader.Find("Sprites/Default"));
            _lr.sortingOrder = borderSortingOrder;
            _lr.startWidth = borderWidthWorld;
            _lr.endWidth = borderWidthWorld;
        }

        float x0, y0, x1, y1;
        if (originMode == OriginMode.BottomLeft)
        {
            x0 = 0f; y0 = 0f; x1 = w; y1 = h;
        }
        else
        {
            x0 = -w * 0.5f; y0 = -h * 0.5f; x1 = w * 0.5f; y1 = h * 0.5f;
        }

        _lr.SetPosition(0, new Vector3(x0, y0, 0f));
        _lr.SetPosition(1, new Vector3(x1, y0, 0f));
        _lr.SetPosition(2, new Vector3(x1, y1, 0f));
        _lr.SetPosition(3, new Vector3(x0, y1, 0f));
    }
}