using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmoebaShowcaseMotion : MonoBehaviour
{
    private enum DominantAxisMode
    {
        AutoByShapeId = 0,
        Horizontal = 1,
        Vertical = 2,
        Isotropic = 3
    }

    [Header("Motion")]
    [SerializeField, Range(0f, 0.2f)] private float pulseAmplitude = 0.045f;
    [SerializeField, Range(0.05f, 1f)] private float pulseSpeed = 0.34f;
    [SerializeField, Range(0f, 8f)] private float wobbleRotationDeg = 1.8f;
    [SerializeField, Range(0.05f, 1f)] private float wobbleSpeed = 0.30f;
    [SerializeField, Range(0f, 0.2f)] private float axisStretchAmplitude = 0.065f;
    [SerializeField, Range(0.05f, 1f)] private float axisStretchSpeed = 0.24f;
    [SerializeField, Range(0f, 0.3f)] private float driftAmplitude = 0.038f;
    [SerializeField, Range(0.05f, 1f)] private float driftSpeed = 0.17f;
    [SerializeField] private DominantAxisMode dominantAxisMode = DominantAxisMode.AutoByShapeId;
    [SerializeField, Min(0f)] private float intensity = 1f;
    [SerializeField] private int optionalSeed;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;

    private float pulsePhase;
    private float wobblePhase;
    private float stretchPhase;
    private float driftPhase;

    private float pulseAmpResolved;
    private float pulseSpeedResolved;
    private float wobbleDegResolved;
    private float wobbleSpeedResolved;
    private float stretchAmpResolved;
    private float stretchSpeedResolved;
    private float driftAmpResolved;
    private float driftSpeedResolved;

    private bool hasHorizontalBias;
    private bool initialized;

    public void Configure(string shapeId, float motionIntensity, int seed)
    {
        optionalSeed = seed;
        intensity = Mathf.Max(0f, motionIntensity);
        ResolveDominantAxis(shapeId);
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
        RestoreBaseline();
    }

    private void LateUpdate()
    {
        ApplyAtCurrentTime();
    }

    private void CacheBaseline()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalScale = transform.localScale;

        var seed = ComputeSeed();
        var variation0 = HashToSigned01(seed, 1);
        var variation1 = HashToSigned01(seed, 2);
        var variation2 = HashToSigned01(seed, 3);
        var variation3 = HashToSigned01(seed, 4);

        pulsePhase = HashTo01(seed, 10) * Mathf.PI * 2f;
        wobblePhase = HashTo01(seed, 11) * Mathf.PI * 2f;
        stretchPhase = HashTo01(seed, 12) * Mathf.PI * 2f;
        driftPhase = HashTo01(seed, 13) * Mathf.PI * 2f;

        pulseAmpResolved = Mathf.Max(0f, pulseAmplitude * (1f + (variation0 * 0.22f)));
        pulseSpeedResolved = Mathf.Max(0.01f, pulseSpeed * (1f + (variation1 * 0.16f)));
        wobbleDegResolved = Mathf.Max(0f, wobbleRotationDeg * (1f + (variation2 * 0.25f)));
        wobbleSpeedResolved = Mathf.Max(0.01f, wobbleSpeed * (1f + (variation3 * 0.2f)));

        stretchAmpResolved = Mathf.Max(0f, axisStretchAmplitude * (1f + (variation1 * 0.18f)));
        stretchSpeedResolved = Mathf.Max(0.01f, axisStretchSpeed * (1f + (variation0 * 0.14f)));
        driftAmpResolved = Mathf.Max(0f, driftAmplitude * (1f + (variation2 * 0.24f)));
        driftSpeedResolved = Mathf.Max(0.01f, driftSpeed * (1f + (variation3 * 0.18f)));

        initialized = true;
    }

    private void RestoreBaseline()
    {
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
        transform.localScale = baseLocalScale;
    }

    private void ApplyAtCurrentTime()
    {
        if (!initialized)
        {
            return;
        }

        var resolvedIntensity = Mathf.Max(0f, intensity);
        if (resolvedIntensity <= 0f)
        {
            RestoreBaseline();
            return;
        }

        var t = Time.unscaledTime;

        var pulse = Mathf.Sin((t * pulseSpeedResolved * Mathf.PI * 2f) + pulsePhase) * pulseAmpResolved * resolvedIntensity;
        var stretch = Mathf.Sin((t * stretchSpeedResolved * Mathf.PI * 2f) + stretchPhase) * stretchAmpResolved * resolvedIntensity;
        var wobble = Mathf.Sin((t * wobbleSpeedResolved * Mathf.PI * 2f) + wobblePhase) * wobbleDegResolved * resolvedIntensity;

        var driftPrimary = Mathf.Sin((t * driftSpeedResolved * Mathf.PI * 2f) + driftPhase);
        var driftSecondary = Mathf.Cos((t * driftSpeedResolved * 0.83f * Mathf.PI * 2f) + driftPhase * 1.21f);

        var pulseMul = 1f + pulse;
        var stretchAmount = hasHorizontalBias ? stretch : stretch * 0.35f;

        var scaleX = pulseMul * (1f + stretchAmount);
        var scaleY = pulseMul * (1f - stretchAmount * 0.6f);

        transform.localScale = new Vector3(baseLocalScale.x * scaleX, baseLocalScale.y * scaleY, baseLocalScale.z);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, wobble);

        var drift = driftAmpResolved * resolvedIntensity;
        var driftX = driftPrimary * drift;
        var driftY = driftSecondary * drift * 0.42f;

        if (!hasHorizontalBias)
        {
            driftX *= 0.5f;
        }

        transform.localPosition = baseLocalPosition + new Vector3(driftX, driftY, 0f);
    }

    private void ResolveDominantAxis(string shapeId)
    {
        switch (dominantAxisMode)
        {
            case DominantAxisMode.Horizontal:
                hasHorizontalBias = true;
                return;
            case DominantAxisMode.Vertical:
                hasHorizontalBias = false;
                return;
            case DominantAxisMode.Isotropic:
                hasHorizontalBias = false;
                axisStretchAmplitude = Mathf.Min(axisStretchAmplitude, 0.045f);
                return;
            case DominantAxisMode.AutoByShapeId:
            default:
                break;
        }

        var id = shapeId != null ? shapeId.ToLowerInvariant() : string.Empty;
        hasHorizontalBias = id.Contains("crawler")
                            || id.Contains("wide")
                            || id.Contains("branch")
                            || id.Contains("hunter")
                            || id.Contains("spread");

        if (!hasHorizontalBias)
        {
            axisStretchAmplitude = Mathf.Min(axisStretchAmplitude, 0.05f);
        }
    }

    private int ComputeSeed()
    {
        var nameHash = gameObject.name != null ? gameObject.name.GetHashCode() : 0;
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + nameHash;
            hash = (hash * 31) + transform.GetSiblingIndex();
            hash = (hash * 31) + optionalSeed;
            return hash;
        }
    }

    private static float HashTo01(int seed, int channel)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 31) + (channel * 997);
            var normalized = (hash & 0xFFFF) / 65535f;
            return normalized;
        }
    }

    private static float HashToSigned01(int seed, int channel)
    {
        return (HashTo01(seed, channel) * 2f) - 1f;
    }
}
