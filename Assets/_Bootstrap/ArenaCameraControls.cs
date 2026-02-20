using UnityEngine;
using UnityEngine.U2D;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-900)]
public class ArenaCameraControls : MonoBehaviour
{
    [Header("Zoom")]
    [Range(0.05f, 2.0f)] public float zoomStep = 0.45f;
    public float fastZoomMultiplier = 8f;
    public int minRefHeight = 180;
    public int maxRefHeight = 2160;

    [Header("Pan")]
    public float panSpeed = 1.0f;
    public bool allowRightMousePan = true;
    public bool allowSpaceLeftPan = true;

    [Header("Smooth")]
    public bool smooth = true;
    public float smoothPos = 18f;
    public float smoothZoom = 18f;

    Camera _cam;
    PixelPerfectCamera _ppc;
    ArenaCameraPolicy _policy;

    float _arenaW, _arenaH;
    bool _bottomLeftOrigin;

    int _baseRefX, _baseRefY;

    Vector3 _targetPos;
    int _targetRefX, _targetRefY;

    bool _dragging;
    Vector3 _dragStartWorld;
    Vector3 _dragStartCamPos;

    float _camZ;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _ppc = GetComponent<PixelPerfectCamera>();

        _policy = UnityEngine.Object.FindFirstObjectByType<ArenaCameraPolicy>();
        if (_policy == null) _policy = UnityEngine.Object.FindAnyObjectByType<ArenaCameraPolicy>();

        if (_cam == null || _policy == null)
        {
            UnityEngine.Debug.LogWarning("[ArenaCameraControls] Missing Camera or ArenaCameraPolicy.");
            enabled = false;
            return;
        }

        _camZ = _cam.transform.position.z;

        _arenaW = _policy.targetWidthPx / (float)_policy.assetsPPU;
        _arenaH = _policy.targetHeightPx / (float)_policy.assetsPPU;
        _bottomLeftOrigin = (_policy.originMode == ArenaCameraPolicy.OriginMode.BottomLeft);

        _baseRefX = _policy.targetWidthPx;
        _baseRefY = _policy.targetHeightPx;

        // Start from current camera pos (preserves Z!)
        _targetPos = _cam.transform.position;

        // Start at full arena view
        _targetRefX = _baseRefX;
        _targetRefY = _baseRefY;

        if (_ppc != null)
        {
            _ppc.refResolutionX = _baseRefX;
            _ppc.refResolutionY = _baseRefY;
        }

        ResetFullView();
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        HandlePresets();

        if (_ppc != null)
        {
            if (smooth)
            {
                int curY = _ppc.refResolutionY;
                float y = Mathf.Lerp(curY, _targetRefY, 1f - Mathf.Exp(-smoothZoom * Time.unscaledDeltaTime));
                _ppc.refResolutionY = Mathf.RoundToInt(y);

                float aspect = _baseRefX / (float)_baseRefY;
                _ppc.refResolutionX = Mathf.RoundToInt(_ppc.refResolutionY * aspect);
            }
            else
            {
                _ppc.refResolutionY = _targetRefY;
                _ppc.refResolutionX = _targetRefX;
            }
        }

        if (smooth)
        {
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position,
                _targetPos,
                1f - Mathf.Exp(-smoothPos * Time.unscaledDeltaTime)
            );
        }
        else
        {
            _cam.transform.position = _targetPos;
        }
    }

    void HandleZoom()
    {
        float scroll = GetScrollY();
        if (Mathf.Abs(scroll) < 0.001f) return;

        float step = zoomStep * (IsShiftPressed() ? fastZoomMultiplier : 1f);
        float factor = 1f - (scroll * step);
        factor = Mathf.Clamp(factor, 0.2f, 5f);

        if (_ppc != null)
        {
            int newY = Mathf.RoundToInt(_targetRefY * factor);
            newY = Mathf.Clamp(newY, minRefHeight, maxRefHeight);

            float aspect = _baseRefX / (float)_baseRefY;
            int newX = Mathf.RoundToInt(newY * aspect);

            _targetRefY = newY;
            _targetRefX = newX;

            _targetPos = ClampCam(_targetPos);
        }
        else
        {
            _cam.orthographicSize *= factor;
            _targetPos = ClampCam(_targetPos);
        }
    }

    void HandlePan()
    {
        bool startDrag =
            GetMouseButtonDown(2) ||
            (allowRightMousePan && GetMouseButtonDown(1)) ||
            (allowSpaceLeftPan && IsSpacePressed() && GetMouseButtonDown(0));

        bool stopDrag =
            GetMouseButtonUp(2) ||
            (allowRightMousePan && GetMouseButtonUp(1)) ||
            (allowSpaceLeftPan && GetMouseButtonUp(0));

        if (startDrag)
        {
            _dragging = true;
            _dragStartWorld = _cam.ScreenToWorldPoint(GetMousePosition());
            _dragStartCamPos = _targetPos;
        }

        if (stopDrag) _dragging = false;
        if (!_dragging) return;

        Vector3 nowWorld = _cam.ScreenToWorldPoint(GetMousePosition());
        Vector3 delta = _dragStartWorld - nowWorld;
        delta.z = 0f;

        _targetPos = _dragStartCamPos + delta * panSpeed;
        _targetPos = ClampCam(_targetPos);
    }

    void HandlePresets()
    {
        if (GetKeyDownDigit(0)) { ResetFullView(); return; }

        if (GetKeyDownF())
        {
            Vector3 w = _cam.ScreenToWorldPoint(GetMousePosition());
            w.z = _camZ;
            _targetPos = ClampCam(w);
        }

        if (GetKeyDownDigit(1)) SnapGrid(0, 0);
        if (GetKeyDownDigit(2)) SnapGrid(1, 0);
        if (GetKeyDownDigit(3)) SnapGrid(2, 0);
        if (GetKeyDownDigit(4)) SnapGrid(0, 1);
        if (GetKeyDownDigit(5)) SnapGrid(1, 1);
        if (GetKeyDownDigit(6)) SnapGrid(2, 1);
        if (GetKeyDownDigit(7)) SnapGrid(0, 2);
        if (GetKeyDownDigit(8)) SnapGrid(1, 2);
        if (GetKeyDownDigit(9)) SnapGrid(2, 2);
    }

    void ResetFullView()
    {
        if (_ppc != null)
        {
            _targetRefX = _baseRefX;
            _targetRefY = _baseRefY;
        }

        Vector3 p = _targetPos;
        p.z = _camZ;

        if (_bottomLeftOrigin)
        {
            p.x = _arenaW * 0.5f;
            p.y = _arenaH * 0.5f;
        }
        else
        {
            p.x = 0f; p.y = 0f;
        }

        _targetPos = ClampCam(p);
    }

    void SnapGrid(int gx, int gy)
    {
        float x0, y0, x1, y1;
        if (_bottomLeftOrigin)
        {
            x0 = 0f; y0 = 0f; x1 = _arenaW; y1 = _arenaH;
        }
        else
        {
            x0 = -_arenaW * 0.5f; y0 = -_arenaH * 0.5f; x1 = _arenaW * 0.5f; y1 = _arenaH * 0.5f;
        }

        float tx = Mathf.Lerp(x0, x1, (gx + 0.5f) / 3f);
        float ty = Mathf.Lerp(y0, y1, (gy + 0.5f) / 3f);

        var p = _targetPos;
        p.x = tx; p.y = ty; p.z = _camZ;
        _targetPos = ClampCam(p);
    }

    Vector3 ClampCam(Vector3 p)
    {
        p.z = _camZ;

        float viewH, viewW;
        if (_ppc != null)
        {
            viewH = _targetRefY / (float)_policy.assetsPPU;
            viewW = _targetRefX / (float)_policy.assetsPPU;
        }
        else
        {
            viewH = _cam.orthographicSize * 2f;
            viewW = viewH * _cam.aspect;
        }

        float halfW = viewW * 0.5f;
        float halfH = viewH * 0.5f;

        float minX, minY, maxX, maxY;
        if (_bottomLeftOrigin)
        {
            minX = 0f; minY = 0f; maxX = _arenaW; maxY = _arenaH;
        }
        else
        {
            minX = -_arenaW * 0.5f; minY = -_arenaH * 0.5f; maxX = _arenaW * 0.5f; maxY = _arenaH * 0.5f;
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        float arenaW = (maxX - minX);
        float arenaH = (maxY - minY);

        if (viewW >= arenaW)
        {
            float slackX = (viewW - arenaW) * 0.5f;
            p.x = Mathf.Clamp(p.x, centerX - slackX, centerX + slackX);
        }
        else
        {
            p.x = Mathf.Clamp(p.x, minX + halfW, maxX - halfW);
        }

        if (viewH >= arenaH)
        {
            float slackY = (viewH - arenaH) * 0.5f;
            p.y = Mathf.Clamp(p.y, centerY - slackY, centerY + slackY);
        }
        else
        {
            p.y = Mathf.Clamp(p.y, minY + halfH, maxY - halfH);
        }

        return p;
    }

    // ---- Input wrappers ----

    Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m != null)
        {
            var v = m.position.ReadValue();
            return new Vector3(v.x, v.y, 0f);
        }
#endif
        return Input.mousePosition;
    }

    float GetScrollY()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m != null) return m.scroll.ReadValue().y / 120f;
#endif
        return Input.mouseScrollDelta.y;
    }

    bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        return button switch
        {
            0 => m.leftButton.wasPressedThisFrame,
            1 => m.rightButton.wasPressedThisFrame,
            2 => m.middleButton.wasPressedThisFrame,
            _ => false
        };
#else
        return Input.GetMouseButtonDown(button);
#endif
    }

    bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        return button switch
        {
            0 => m.leftButton.wasReleasedThisFrame,
            1 => m.rightButton.wasReleasedThisFrame,
            2 => m.middleButton.wasReleasedThisFrame,
            _ => false
        };
#else
        return Input.GetMouseButtonUp(button);
#endif
    }

    bool IsShiftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    bool IsSpacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.spaceKey.isPressed;
#else
        return Input.GetKey(KeyCode.Space);
#endif
    }

    bool GetKeyDownF()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    bool GetKeyDownDigit(int d)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return d switch
        {
            0 => kb.digit0Key.wasPressedThisFrame,
            1 => kb.digit1Key.wasPressedThisFrame,
            2 => kb.digit2Key.wasPressedThisFrame,
            3 => kb.digit3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame,
            5 => kb.digit5Key.wasPressedThisFrame,
            6 => kb.digit6Key.wasPressedThisFrame,
            7 => kb.digit7Key.wasPressedThisFrame,
            8 => kb.digit8Key.wasPressedThisFrame,
            9 => kb.digit9Key.wasPressedThisFrame,
            _ => false
        };
#else
        return Input.GetKeyDown(KeyCode.Alpha0 + d);
#endif
    }
}