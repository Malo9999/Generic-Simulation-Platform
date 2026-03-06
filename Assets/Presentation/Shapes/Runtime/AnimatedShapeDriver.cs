using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimatedShapeDriver : MonoBehaviour
{
    private const float AmoebaMaxRotationDegrees = 1.5f;
    private const float FilamentMaxRotationDegrees = 2f;

    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private AnimatedShapeProfile profile = new AnimatedShapeProfile();
    [SerializeField] private int optionalSeed;

    private Vector3 baseScale = Vector3.one;
    private float baseRotation;
    private Color baseColor = Color.white;
    private float runtimePhase;
    private bool initialized;

    public void Configure(SpriteRenderer renderer, AnimatedShapeProfile animationProfile, int seed)
    {
        targetRenderer = renderer;
        profile = animationProfile ?? new AnimatedShapeProfile();
        optionalSeed = seed;
        CacheBaseline();
    }

    private void Awake()
    {
        CacheBaseline();
    }

    private void OnEnable()
    {
        CacheBaseline();
        ApplyAtCurrentTime();
    }

    private void OnDisable()
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.transform.localScale = baseScale;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.color = baseColor;
    }

    private void LateUpdate()
    {
        ApplyAtCurrentTime();
    }

    private void CacheBaseline()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetRenderer == null)
        {
            initialized = false;
            return;
        }

        baseScale = targetRenderer.transform.localScale;
        baseRotation = targetRenderer.transform.localEulerAngles.z;
        baseColor = targetRenderer.color;
        runtimePhase = ComputeRuntimePhase();
        initialized = true;
    }

    private void ApplyAtCurrentTime()
    {
        if (!initialized || profile == null || profile.animType == ShapeAnimType.None || targetRenderer == null)
        {
            return;
        }

        var animationTime = profile.useUnscaledTime ? Time.unscaledTime : Time.time;
        var t = (animationTime * profile.frequency) + profile.phaseOffset + runtimePhase;

        switch (profile.animType)
        {
            case ShapeAnimType.AmoebaWobble:
                ApplyAmoebaWobble(t);
                break;
            case ShapeAnimType.GlowPulse:
                ApplyGlowPulse(t);
                break;
            case ShapeAnimType.FilamentWave:
                ApplyFilamentWave(t);
                break;
        }
    }

    private void ApplyAmoebaWobble(float t)
    {
        var sx = 1f + (Mathf.Sin(t) * profile.amplitude);
        var sy = 1f + (Mathf.Cos(t * 1.13f) * profile.amplitude * 0.85f);
        var pulse = 1f + (Mathf.Sin(t * 0.83f) * profile.scalePulse);

        var scale = baseScale;
        scale.x *= sx * pulse;
        scale.y *= sy * pulse;
        targetRenderer.transform.localScale = scale;

        var rotation = baseRotation + (Mathf.Sin(t * 0.91f) * AmoebaMaxRotationDegrees);
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        targetRenderer.color = baseColor;
    }

    private void ApplyGlowPulse(float t)
    {
        var pulse = Mathf.Sin(t);
        var scaleMul = 1f + (pulse * profile.scalePulse);
        var brightnessMul = Mathf.Max(0f, 1f + (pulse * profile.brightnessPulse));

        targetRenderer.transform.localScale = baseScale * scaleMul;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);

        var color = baseColor;
        color.r = Mathf.Clamp01(color.r * brightnessMul);
        color.g = Mathf.Clamp01(color.g * brightnessMul);
        color.b = Mathf.Clamp01(color.b * brightnessMul);
        color.a = Mathf.Clamp01(color.a * brightnessMul);
        targetRenderer.color = color;
    }

    private void ApplyFilamentWave(float t)
    {
        var sx = 1f + (Mathf.Sin(t * 1.11f) * profile.amplitude);
        var sy = 1f + (Mathf.Cos(t * 0.89f) * profile.amplitude * 0.75f);
        var pulse = 1f + (Mathf.Sin(t * 0.57f) * profile.scalePulse);

        var scale = baseScale;
        scale.x *= sx * pulse;
        scale.y *= sy;
        targetRenderer.transform.localScale = scale;

        var rotation = baseRotation + (Mathf.Sin(t * 0.72f) * FilamentMaxRotationDegrees);
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        targetRenderer.color = baseColor;
    }

    private float ComputeRuntimePhase()
    {
        var instanceId = GetInstanceID();
        var position = transform.position;

        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + instanceId;
            hash = (hash * 31) + Mathf.RoundToInt(position.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(position.y * 100f);
            hash = (hash * 31) + optionalSeed;

            var normalized = (hash & 0xFFFF) / 65535f;
            return normalized * Mathf.PI * 2f;
        }
    }
}
