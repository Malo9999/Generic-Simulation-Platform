using UnityEngine;

public sealed class FieldBufferController : MonoBehaviour, IFieldDepositBuffer
{
    [SerializeField] private FieldOverlaySettings settings = new();
    [SerializeField] private bool debugForceKnownPattern;

    private float[] values;
    private float[] scratch;
    private Color32[] uploadPixels;
    private Texture2D fieldTexture;
    private Rect worldBounds;
    private bool hasLoggedTextureDiagnostics;

    public FieldOverlaySettings Settings => settings;
    public Texture2D FieldTexture => fieldTexture;
    public Rect WorldBounds => worldBounds;

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && fieldTexture != null)
        {
            Initialize();
        }
    }

    private void Update()
    {
        if (fieldTexture == null)
        {
            return;
        }

        DecayAndDiffuse(Time.deltaTime);
        UploadTexture();
    }

    private void OnDestroy()
    {
        if (fieldTexture != null)
        {
            Destroy(fieldTexture);
            fieldTexture = null;
        }
    }

    public void SetValue(int x, int y, float value)
    {
        if (values == null)
        {
            return;
        }

        var clampedX = Mathf.Clamp(x, 0, settings.width - 1);
        var clampedY = Mathf.Clamp(y, 0, settings.height - 1);
        values[(clampedY * settings.width) + clampedX] = Mathf.Clamp01(value);
    }

    public void AddDeposit(Vector2 worldPos, float amount, float radius)
    {
        if (amount <= 0f || radius <= 0f || values == null || !TryWorldToUv(worldPos, out var uv))
        {
            return;
        }

        var px = Mathf.RoundToInt(uv.x * (settings.width - 1));
        var py = Mathf.RoundToInt(uv.y * (settings.height - 1));
        var radiusPx = Mathf.Max(1f, (radius / Mathf.Max(0.001f, worldBounds.width)) * settings.width);
        DepositCircle(px, py, amount * settings.intensity, radiusPx);
    }

    public void Clear()
    {
        if (values == null)
        {
            return;
        }

        System.Array.Clear(values, 0, values.Length);
        UploadTexture();
    }

    public void DecayAndDiffuse(float dt)
    {
        if (values == null)
        {
            return;
        }

        var decayFactor = Mathf.Exp(-Mathf.Clamp01(settings.decayPerSecond) * Mathf.Max(0f, dt));
        var diffuse = Mathf.Clamp01(settings.diffuseStrength);
        var width = settings.width;
        var height = settings.height;

        for (var y = 0; y < height; y++)
        {
            var yOffset = y * width;
            var yMinus = Mathf.Max(0, y - 1) * width;
            var yPlus = Mathf.Min(height - 1, y + 1) * width;

            for (var x = 0; x < width; x++)
            {
                var i = yOffset + x;
                var xMinus = Mathf.Max(0, x - 1);
                var xPlus = Mathf.Min(width - 1, x + 1);

                var center = values[i];
                var neighbors = values[yOffset + xMinus] + values[yOffset + xPlus] + values[yMinus + x] + values[yPlus + x];
                var blurred = (center * 0.6f) + (neighbors * 0.1f);
                var diffused = Mathf.Lerp(center, blurred, diffuse);
                scratch[i] = Mathf.Clamp01(diffused * decayFactor);
            }
        }

        var tmp = values;
        values = scratch;
        scratch = tmp;
    }

    public float Sample(Vector2 worldPos)
    {
        if (values == null || !TryWorldToUv(worldPos, out var uv))
        {
            return 0f;
        }

        var x = uv.x * (settings.width - 1);
        var y = uv.y * (settings.height - 1);

        var x0 = Mathf.Clamp((int)x, 0, settings.width - 1);
        var x1 = Mathf.Clamp(x0 + 1, 0, settings.width - 1);
        var y0 = Mathf.Clamp((int)y, 0, settings.height - 1);
        var y1 = Mathf.Clamp(y0 + 1, 0, settings.height - 1);

        var tx = x - x0;
        var ty = y - y0;

        var i00 = values[(y0 * settings.width) + x0];
        var i10 = values[(y0 * settings.width) + x1];
        var i01 = values[(y1 * settings.width) + x0];
        var i11 = values[(y1 * settings.width) + x1];

        var a = Mathf.Lerp(i00, i10, tx);
        var b = Mathf.Lerp(i01, i11, tx);
        return Mathf.Lerp(a, b, ty);
    }

    public void SetWorldBounds(Rect newBounds)
    {
        if (newBounds.width <= 0.01f || newBounds.height <= 0.01f)
        {
            return;
        }

        settings.worldBoundsMode = FieldWorldBoundsMode.Manual;
        settings.manualWorldBounds = newBounds;
        worldBounds = newBounds;
    }

    public void UploadFromScalarField(float[] sourceValues, int sourceWidth, int sourceHeight, bool normalize = true)
    {
        if (sourceValues == null || sourceValues.Length == 0 || values == null)
        {
            return;
        }

        var min = 0f;
        var max = 1f;
        if (normalize)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (var i = 0; i < sourceValues.Length; i++)
            {
                var v = sourceValues[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (max - min < 1e-6f)
            {
                min = 0f;
                max = 1f;
            }
        }

        for (var y = 0; y < settings.height; y++)
        {
            var srcY = Mathf.Clamp(Mathf.FloorToInt(((float)y / Mathf.Max(1, settings.height - 1)) * (sourceHeight - 1)), 0, Mathf.Max(0, sourceHeight - 1));
            for (var x = 0; x < settings.width; x++)
            {
                var srcX = Mathf.Clamp(Mathf.FloorToInt(((float)x / Mathf.Max(1, settings.width - 1)) * (sourceWidth - 1)), 0, Mathf.Max(0, sourceWidth - 1));
                var srcValue = sourceValues[(srcY * sourceWidth) + srcX];
                var normalized = normalize ? Mathf.Clamp01((srcValue - min) / Mathf.Max(1e-6f, max - min)) : Mathf.Clamp01(srcValue);
                values[(y * settings.width) + x] = normalized;
            }
        }

        UploadTexture();
    }

    private void Initialize()
    {
        worldBounds = settings.ResolveWorldBounds();
        var pixelCount = settings.width * settings.height;

        if (fieldTexture == null || fieldTexture.width != settings.width || fieldTexture.height != settings.height)
        {
            if (fieldTexture != null)
            {
                Destroy(fieldTexture);
            }

            fieldTexture = new Texture2D(settings.width, settings.height, TextureFormat.RGBA32, false, true)
            {
                name = "FieldOverlayTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        values = new float[pixelCount];
        scratch = new float[pixelCount];
        uploadPixels = new Color32[pixelCount];
        UploadTexture();
    }

    private void UploadTexture()
    {
        var low = settings.tintLow;
        var high = settings.tintHigh;
        var alphaMul = Mathf.Clamp01(settings.alphaMultiplier);
        var width = settings.width;
        var height = settings.height;

        if (debugForceKnownPattern)
        {
            var halfWidth = width / 2;
            var transparentBlack = new Color32(0, 0, 0, 0);
            var cyan = new Color(0f, 1f, 1f, 0.35f);
            var cyanPixel = (Color32)cyan;

            for (var y = 0; y < height; y++)
            {
                var yOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    uploadPixels[yOffset + x] = x < halfWidth ? transparentBlack : cyanPixel;
                }
            }
        }
        else
        {
            for (var i = 0; i < values.Length; i++)
            {
                var value = Mathf.Clamp01(values[i] * settings.intensity);
                var color = Color.Lerp(low, high, value);
                color.a *= value * alphaMul;
                uploadPixels[i] = color;
            }
        }

        LogTextureDiagnosticsOnce();
        fieldTexture.SetPixels32(uploadPixels);
        fieldTexture.Apply(false, false);
    }

    private void LogTextureDiagnosticsOnce()
    {
        if (hasLoggedTextureDiagnostics || values == null || values.Length == 0 || uploadPixels == null || uploadPixels.Length == 0)
        {
            return;
        }

        var minValue = float.MaxValue;
        var maxValue = float.MinValue;
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (value < minValue) minValue = value;
            if (value > maxValue) maxValue = value;
        }

        var centerX = Mathf.Clamp(settings.width / 2, 0, settings.width - 1);
        var centerY = Mathf.Clamp(settings.height / 2, 0, settings.height - 1);
        var centerIndex = (centerY * settings.width) + centerX;
        var centerPixel = uploadPixels[centerIndex];

        var foundNonZero = false;
        var firstNonZeroPixel = default(Color32);
        for (var i = 0; i < uploadPixels.Length; i++)
        {
            var pixel = uploadPixels[i];
            if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0 && pixel.a == 0)
            {
                continue;
            }

            firstNonZeroPixel = pixel;
            foundNonZero = true;
            break;
        }

        var firstNonZeroText = foundNonZero
            ? $"RGBA({firstNonZeroPixel.r}, {firstNonZeroPixel.g}, {firstNonZeroPixel.b}, {firstNonZeroPixel.a})"
            : "none";

        Debug.Log(
            $"[FieldBufferController] Texture diagnostics | tintLow={settings.tintLow} tintHigh={settings.tintHigh} alphaMultiplier={settings.alphaMultiplier:F3} valuesMin={minValue:F4} valuesMax={maxValue:F4} centerPixel=RGBA({centerPixel.r}, {centerPixel.g}, {centerPixel.b}, {centerPixel.a}) firstNonZeroPixel={firstNonZeroText} debugForceKnownPattern={debugForceKnownPattern}",
            this);

        hasLoggedTextureDiagnostics = true;
    }

    private void DepositCircle(int px, int py, float amount, float radiusPx)
    {
        var radiusInt = Mathf.CeilToInt(radiusPx);
        var minX = Mathf.Max(0, px - radiusInt);
        var maxX = Mathf.Min(settings.width - 1, px + radiusInt);
        var minY = Mathf.Max(0, py - radiusInt);
        var maxY = Mathf.Min(settings.height - 1, py + radiusInt);

        for (var y = minY; y <= maxY; y++)
        {
            var yOffset = y * settings.width;
            var dy = y - py;
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - px;
                var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (dist > radiusPx)
                {
                    continue;
                }

                var falloff = 1f - (dist / Mathf.Max(1e-4f, radiusPx));
                var index = yOffset + x;
                values[index] = Mathf.Clamp01(values[index] + (amount * falloff));
            }
        }
    }

    private bool TryWorldToUv(Vector2 worldPos, out Vector2 uv)
    {
        uv = Vector2.zero;
        if (worldBounds.width <= 0.01f || worldBounds.height <= 0.01f)
        {
            return false;
        }

        var u = (worldPos.x - worldBounds.xMin) / worldBounds.width;
        var v = (worldPos.y - worldBounds.yMin) / worldBounds.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        uv = new Vector2(u, v);
        return true;
    }
}
