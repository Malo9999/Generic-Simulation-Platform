using UnityEngine;

[DisallowMultipleComponent]
public sealed class MotionShaderDriver : MonoBehaviour
{
    private static readonly int MotionEnabledId = Shader.PropertyToID("_MotionEnabled");
    private static readonly int MotionModeId = Shader.PropertyToID("_MotionMode");
    private static readonly int MotionTimeId = Shader.PropertyToID("_MotionTime");
    private static readonly int MotionPhaseId = Shader.PropertyToID("_MotionPhase");
    private static readonly int MotionAmplitudeId = Shader.PropertyToID("_MotionAmplitude");
    private static readonly int MotionFrequencyId = Shader.PropertyToID("_MotionFrequency");
    private static readonly int MotionSecondaryAmplitudeId = Shader.PropertyToID("_MotionSecondaryAmplitude");
    private static readonly int MotionSecondaryFrequencyId = Shader.PropertyToID("_MotionSecondaryFrequency");
    private static readonly int MotionFlowDirId = Shader.PropertyToID("_MotionFlowDir");
    private static readonly int MotionTintStrengthId = Shader.PropertyToID("_MotionTintStrength");

    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private bool motionEnabled = true;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField, Min(0f)] private float masterIntensity = 1f;
    [SerializeField] private MotionFamily family = MotionFamily.None;
    [SerializeField] private MotionFamilyDefaults defaults;

    private MaterialPropertyBlock propertyBlock;
    private float stablePhase;
    private bool initialized;

    public void Configure(SpriteRenderer renderer, VisualKey key, string shapeId, MotionShaderProfile profile)
    {
        targetRenderer = renderer;
        var resolvedFamily = ResolveFamily(shapeId);
        family = resolvedFamily;

        if (profile == null)
        {
            profile = MotionShaderProfile.LoadRuntimeProfile();
        }

        defaults = profile.GetDefaults(resolvedFamily);
        stablePhase = ComputeStablePhase(key, shapeId);
        Cache();
        ApplyMotionProperties();
    }

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Cache();
        ApplyMotionProperties();
    }

    private void OnDisable()
    {
        if (targetRenderer == null)
        {
            return;
        }

        Cache();
        propertyBlock.SetFloat(MotionEnabledId, 0f);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void LateUpdate()
    {
        ApplyMotionProperties();
    }

    private void Cache()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        initialized = targetRenderer != null;
    }

    private void ApplyMotionProperties()
    {
        if (!initialized || targetRenderer == null)
        {
            return;
        }

        var enabledValue = motionEnabled && family != MotionFamily.None ? 1f : 0f;
        var timeValue = useUnscaledTime ? Time.unscaledTime : Time.time;
        var amplitude = defaults.amplitude * Mathf.Max(0f, masterIntensity);
        var secondaryAmplitude = defaults.secondaryAmplitude * Mathf.Max(0f, masterIntensity);

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(MotionEnabledId, enabledValue);
        propertyBlock.SetFloat(MotionModeId, (float)defaults.mode);
        propertyBlock.SetFloat(MotionTimeId, timeValue);
        propertyBlock.SetFloat(MotionPhaseId, stablePhase);
        propertyBlock.SetFloat(MotionAmplitudeId, amplitude);
        propertyBlock.SetFloat(MotionFrequencyId, defaults.frequency);
        propertyBlock.SetFloat(MotionSecondaryAmplitudeId, secondaryAmplitude);
        propertyBlock.SetFloat(MotionSecondaryFrequencyId, defaults.secondaryFrequency);
        propertyBlock.SetVector(MotionFlowDirId, defaults.flowDirection);
        propertyBlock.SetFloat(MotionTintStrengthId, defaults.tintStrength * Mathf.Clamp01(masterIntensity));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private static MotionFamily ResolveFamily(string shapeId)
    {
        return MotionShaderProfile.TryResolveFamily(shapeId, out var family) ? family : MotionFamily.None;
    }

    private static float ComputeStablePhase(VisualKey key, string shapeId)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + key.instanceId;
            hash = (hash * 31) + key.variantSeed;
            hash = (hash * 31) + HashString(key.entityId);
            hash = (hash * 31) + HashString(key.kind);
            hash = (hash * 31) + HashString(key.state);
            hash = (hash * 31) + HashString(shapeId);
            var normalized = (hash & 0x7FFFFFFF) / (float)int.MaxValue;
            return normalized * Mathf.PI * 2f;
        }
    }

    private static int HashString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            var hash = 23;
            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }
}
