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
    private Vector3 baseLocalPosition;
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
        targetRenderer.transform.localPosition = baseLocalPosition;
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
        baseLocalPosition = targetRenderer.transform.localPosition;
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
        var autoPhase = profile.enableAutoPhase && Mathf.Approximately(profile.phaseOffset, 0f) ? runtimePhase : 0f;
        var t = (animationTime * profile.frequency) + profile.phaseOffset + autoPhase;

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
            case ShapeAnimType.Spin:
                ApplySpin(t);
                break;
            case ShapeAnimType.Drift:
                ApplyDrift(t);
                break;
            case ShapeAnimType.Flicker:
                ApplyFlicker(t);
                break;
            case ShapeAnimType.Breathe:
                ApplyBreathe(t);
                break;
            case ShapeAnimType.Ripple:
                ApplyRipple(t);
                break;
            case ShapeAnimType.Orbit:
                ApplyOrbit(t);
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
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = baseColor;
    }

    private void ApplyGlowPulse(float t)
    {
        var pulse = Mathf.Sin(t);
        var scaleMul = 1f + (pulse * profile.scalePulse);
        var brightnessMul = Mathf.Max(0f, 1f + (pulse * profile.brightnessPulse));

        targetRenderer.transform.localScale = baseScale * scaleMul;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = ScaleColor(baseColor, brightnessMul);
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
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = baseColor;
    }

    private void ApplySpin(float t)
    {
        targetRenderer.transform.localScale = baseScale;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation + (t * profile.rotationSpeedDeg));
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = baseColor;
    }

    private void ApplyDrift(float t)
    {
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localScale = baseScale;
        targetRenderer.transform.localPosition = baseLocalPosition + new Vector3(
            Mathf.Sin(t * 0.71f) * profile.amplitude,
            Mathf.Cos(t * 0.93f) * profile.amplitude,
            0f);
        targetRenderer.color = baseColor;
    }

    private void ApplyFlicker(float t)
    {
        targetRenderer.transform.localScale = baseScale;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localPosition = baseLocalPosition;
        var pulse = 0.5f + (0.5f * Mathf.Sin(t));
        var brightness = 1f + ((pulse * 2f - 1f) * profile.brightnessPulse);
        targetRenderer.color = ScaleColor(baseColor, brightness);
    }

    private void ApplyBreathe(float t)
    {
        var scaleMul = 1f + (Mathf.Sin(t) * profile.scalePulse);
        targetRenderer.transform.localScale = baseScale * scaleMul;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = baseColor;
    }

    private void ApplyRipple(float t)
    {
        var pulse = Mathf.Sin(t);
        var scaleMul = 1f + (pulse * profile.scalePulse);
        var brightness = 1f + (pulse * profile.brightnessPulse);
        targetRenderer.transform.localScale = baseScale * scaleMul;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localPosition = baseLocalPosition;
        targetRenderer.color = ScaleColor(baseColor, brightness);
    }

    private void ApplyOrbit(float t)
    {
        targetRenderer.transform.localScale = baseScale;
        targetRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
        targetRenderer.transform.localPosition = baseLocalPosition + new Vector3(
            Mathf.Cos(t) * profile.orbitRadius,
            Mathf.Sin(t) * profile.orbitRadius,
            0f);
        targetRenderer.color = baseColor;
    }

    private static Color ScaleColor(Color source, float brightness)
    {
        var mul = Mathf.Max(0f, brightness);
        return new Color(
            Mathf.Clamp01(source.r * mul),
            Mathf.Clamp01(source.g * mul),
            Mathf.Clamp01(source.b * mul),
            Mathf.Clamp01(source.a * mul));
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
