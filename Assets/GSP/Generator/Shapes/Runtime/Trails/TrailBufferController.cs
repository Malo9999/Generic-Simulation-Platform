using System.Collections.Generic;
using UnityEngine;

public sealed class TrailBufferController : MonoBehaviour, IFieldDepositBuffer
{
    [SerializeField] private TrailBufferSettings settings = new();
    [SerializeField, Min(8)] private int maxQueuedDeposits = 4096;

    private readonly List<DepositRequest> depositQueue = new();
    private float[] intensity;
    private float[] scratch;
    private Color32[] uploadPixels;
    private Texture2D trailTexture;
    private Rect worldBounds;
    private bool hasVisibleContent;

    public TrailBufferSettings Settings => settings;
    public Texture2D TrailTexture => trailTexture;
    public Rect WorldBounds => worldBounds;
    public bool HasVisibleContent => hasVisibleContent;

    private void Awake()
    {
        InitializeBuffer();
    }

    private void OnValidate()
    {
        maxQueuedDeposits = Mathf.Max(8, maxQueuedDeposits);
        if (isActiveAndEnabled && trailTexture != null)
        {
            InitializeBuffer();
        }
    }

    private void Update()
    {
        if (trailTexture == null)
        {
            return;
        }

        SimulateTrail(Time.deltaTime);
        UploadTexture();
        depositQueue.Clear();
    }

    private void OnDestroy()
    {
        if (trailTexture != null)
        {
            Destroy(trailTexture);
            trailTexture = null;
        }
    }

    public void AddDeposit(Vector2 worldPos, float amount, float radius)
    {
        QueueDeposit(worldPos, amount, radius);
    }

    public void QueueDeposit(Vector2 worldPos, float strength, float radiusScale = 1f)
    {
        if (!enabled || strength <= 0f)
        {
            return;
        }

        if (depositQueue.Count >= maxQueuedDeposits)
        {
            return;
        }

        if (!TryWorldToPixel(worldPos, out var px, out var py))
        {
            return;
        }

        depositQueue.Add(new DepositRequest(px, py, Mathf.Max(0f, strength), Mathf.Max(0.1f, radiusScale)));
    }

    public float Sample(Vector2 worldPos)
    {
        if (trailTexture == null || !TryWorldToUv(worldPos, out var uv))
        {
            return 0f;
        }

        var x = uv.x * (settings.textureWidth - 1);
        var y = uv.y * (settings.textureHeight - 1);

        var x0 = Mathf.Clamp((int)x, 0, settings.textureWidth - 1);
        var x1 = Mathf.Clamp(x0 + 1, 0, settings.textureWidth - 1);
        var y0 = Mathf.Clamp((int)y, 0, settings.textureHeight - 1);
        var y1 = Mathf.Clamp(y0 + 1, 0, settings.textureHeight - 1);

        var tx = x - x0;
        var ty = y - y0;

        var i00 = intensity[(y0 * settings.textureWidth) + x0];
        var i10 = intensity[(y0 * settings.textureWidth) + x1];
        var i01 = intensity[(y1 * settings.textureWidth) + x0];
        var i11 = intensity[(y1 * settings.textureWidth) + x1];

        var a = Mathf.Lerp(i00, i10, tx);
        var b = Mathf.Lerp(i01, i11, tx);
        return Mathf.Lerp(a, b, ty);
    }

    public Vector2 SampleApproxGradient(Vector2 worldPos)
    {
        var dx = worldBounds.width / Mathf.Max(1f, settings.textureWidth);
        var dy = worldBounds.height / Mathf.Max(1f, settings.textureHeight);

        var right = Sample(worldPos + new Vector2(dx, 0f));
        var left = Sample(worldPos + new Vector2(-dx, 0f));
        var up = Sample(worldPos + new Vector2(0f, dy));
        var down = Sample(worldPos + new Vector2(0f, -dy));

        return new Vector2(right - left, up - down);
    }

    private void InitializeBuffer()
    {
        worldBounds = settings.ResolveWorldBounds();
        var pixelCount = settings.textureWidth * settings.textureHeight;

        if (trailTexture == null || trailTexture.width != settings.textureWidth || trailTexture.height != settings.textureHeight)
        {
            if (trailTexture != null)
            {
                Destroy(trailTexture);
            }

            trailTexture = new Texture2D(settings.textureWidth, settings.textureHeight, TextureFormat.RGBA32, false, true)
            {
                name = "TrailBufferTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        intensity = new float[pixelCount];
        scratch = new float[pixelCount];
        uploadPixels = new Color32[pixelCount];
        hasVisibleContent = false;
        depositQueue.Clear();
        UploadTexture();
    }

    private void SimulateTrail(float deltaTime)
    {
        var decayFactor = Mathf.Exp(-Mathf.Clamp01(settings.decayPerSecond) * deltaTime);
        var diffuse = Mathf.Clamp01(settings.diffuseStrength);
        var width = settings.textureWidth;
        var height = settings.textureHeight;

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

                var center = intensity[i];
                var neighbors = intensity[yOffset + xMinus] + intensity[yOffset + xPlus] + intensity[yMinus + x] + intensity[yPlus + x];
                var blurred = (center * 0.6f) + (neighbors * 0.1f);
                var diffused = Mathf.Lerp(center, blurred, diffuse);
                scratch[i] = Mathf.Clamp01(diffused * decayFactor);
            }
        }

        var tmp = intensity;
        intensity = scratch;
        scratch = tmp;

        ApplyDeposits();
    }

    private void ApplyDeposits()
    {
        if (depositQueue.Count == 0)
        {
            return;
        }

        var width = settings.textureWidth;
        var height = settings.textureHeight;

        for (var n = 0; n < depositQueue.Count; n++)
        {
            var deposit = depositQueue[n];
            var radius = Mathf.Max(1f, settings.depositRadiusPx * deposit.RadiusScale);
            var radiusInt = Mathf.CeilToInt(radius);
            var minX = Mathf.Max(0, deposit.Px - radiusInt);
            var maxX = Mathf.Min(width - 1, deposit.Px + radiusInt);
            var minY = Mathf.Max(0, deposit.Py - radiusInt);
            var maxY = Mathf.Min(height - 1, deposit.Py + radiusInt);
            var weightedStrength = deposit.Strength * settings.depositStrength;

            for (var y = minY; y <= maxY; y++)
            {
                var yOffset = y * width;
                var dy = y - deposit.Py;
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = x - deposit.Px;
                    var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                    if (dist > radius)
                    {
                        continue;
                    }

                    var falloff = 1f - (dist / radius);
                    var delta = weightedStrength * falloff;
                    var index = yOffset + x;
                    if (settings.useAdditiveComposite)
                    {
                        intensity[index] = Mathf.Clamp01(intensity[index] + delta);
                    }
                    else
                    {
                        intensity[index] = Mathf.Max(intensity[index], delta);
                    }
                }
            }
        }
    }

    private void UploadTexture()
    {
        var tint = settings.tintColor;
        var r = Mathf.Clamp01(tint.r);
        var g = Mathf.Clamp01(tint.g);
        var b = Mathf.Clamp01(tint.b);
        var alphaMul = settings.AlphaMultiplier;
        hasVisibleContent = false;

        for (var i = 0; i < intensity.Length; i++)
        {
            var value = Mathf.Clamp01(intensity[i]);
            if (value > 0f)
            {
                hasVisibleContent = true;
            }

            uploadPixels[i] = new Color(r * value, g * value, b * value, value * alphaMul);
        }

        trailTexture.SetPixels32(uploadPixels);
        trailTexture.Apply(false, false);
    }

    private bool TryWorldToPixel(Vector2 worldPos, out int px, out int py)
    {
        px = 0;
        py = 0;
        if (!TryWorldToUv(worldPos, out var uv))
        {
            return false;
        }

        px = Mathf.Clamp(Mathf.RoundToInt(uv.x * (settings.textureWidth - 1)), 0, settings.textureWidth - 1);
        py = Mathf.Clamp(Mathf.RoundToInt(uv.y * (settings.textureHeight - 1)), 0, settings.textureHeight - 1);
        return true;
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

    private readonly struct DepositRequest
    {
        public DepositRequest(int px, int py, float strength, float radiusScale)
        {
            Px = px;
            Py = py;
            Strength = strength;
            RadiusScale = radiusScale;
        }

        public int Px { get; }
        public int Py { get; }
        public float Strength { get; }
        public float RadiusScale { get; }
    }
}
