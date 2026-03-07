using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteProceduralMaterialApplier : MonoBehaviour
{
    private static readonly int MotionEnabledId = Shader.PropertyToID("_MotionEnabled");
    private static readonly int MotionTimeId = Shader.PropertyToID("_MotionTime");
    private static readonly int MotionPhaseId = Shader.PropertyToID("_MotionPhase");
    private static readonly int MotionAmplitudeId = Shader.PropertyToID("_MotionAmplitude");
    private static readonly int MotionTintStrengthId = Shader.PropertyToID("_MotionTintStrength");

    private static readonly HashSet<string> LoggedFailures = new();

    private SpriteRenderer targetRenderer;
    private Material baselineSharedMaterial;
    private Material runtimeInstanceMaterial;
    private MaterialPropertyBlock propertyBlock;
    private float intensity = 1f;

    public ProceduralMaterialApplyStatus TryApply(
        SpriteRenderer renderer,
        string shapeId,
        ShapePaletteCategory category,
        ShapeShowcaseProceduralMaterialConfig config,
        int seed)
    {
        targetRenderer = renderer;
        if (renderer == null)
        {
            return ProceduralMaterialApplyStatus.FallbackUnsupportedRenderer;
        }

        baselineSharedMaterial = renderer.sharedMaterial;
        renderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());

        if (config == null || !config.IsEnabledForShape(shapeId))
        {
            return ProceduralMaterialApplyStatus.SkippedDisabled;
        }

        var sourceMaterial = config.ResolveMaterial(shapeId, category);
        if (sourceMaterial == null)
        {
            return HandleFailure(ProceduralMaterialApplyStatus.FallbackMissingMaterial, shapeId, config);
        }

        var shader = sourceMaterial.shader;
        if (shader == null || !shader.isSupported)
        {
            return HandleFailure(ProceduralMaterialApplyStatus.FallbackMissingShader, shapeId, config);
        }

        if (!sourceMaterial.HasProperty(MotionEnabledId))
        {
            return HandleFailure(ProceduralMaterialApplyStatus.FallbackMissingProperty, shapeId, config);
        }

        runtimeInstanceMaterial = new Material(sourceMaterial)
        {
            name = $"Runtime_{sourceMaterial.name}_{shapeId}"
        };

        renderer.material = runtimeInstanceMaterial;
        intensity = config.ResolveIntensity(shapeId);

        var phase = ComputeStablePhase(shapeId, seed);
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(MotionEnabledId, 1f);
        if (runtimeInstanceMaterial.HasProperty(MotionPhaseId))
        {
            propertyBlock.SetFloat(MotionPhaseId, phase);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionAmplitudeId))
        {
            propertyBlock.SetFloat(MotionAmplitudeId, intensity);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionTintStrengthId))
        {
            propertyBlock.SetFloat(MotionTintStrengthId, Mathf.Clamp01(intensity));
        }

        renderer.SetPropertyBlock(propertyBlock);
        return ProceduralMaterialApplyStatus.Applied;
    }

    public void ApplyAnimationFrame(float animationTime, float animationAmplitude)
    {
        if (targetRenderer == null || runtimeInstanceMaterial == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        if (runtimeInstanceMaterial.HasProperty(MotionEnabledId))
        {
            propertyBlock.SetFloat(MotionEnabledId, 1f);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionTimeId))
        {
            propertyBlock.SetFloat(MotionTimeId, animationTime);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionAmplitudeId))
        {
            propertyBlock.SetFloat(MotionAmplitudeId, Mathf.Max(0f, animationAmplitude) * intensity);
        }

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private ProceduralMaterialApplyStatus HandleFailure(ProceduralMaterialApplyStatus status, string shapeId, ShapeShowcaseProceduralMaterialConfig config)
    {
        LogFailureOnce(status, shapeId);
        if (config != null && config.FallbackToBaselineOnFailure && targetRenderer != null)
        {
            targetRenderer.sharedMaterial = baselineSharedMaterial;
            targetRenderer.SetPropertyBlock(null);
        }

        return status;
    }

    private static void LogFailureOnce(ProceduralMaterialApplyStatus status, string key)
    {
        var hash = $"{status}:{key}";
        if (LoggedFailures.Add(hash))
        {
            Debug.LogWarning($"[ShapeShowcase][ProceduralMaterial] {status} for {key}; using baseline fallback.");
        }
    }

    private static float ComputeStablePhase(string shapeId, int seed)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + seed;
            if (!string.IsNullOrWhiteSpace(shapeId))
            {
                for (var i = 0; i < shapeId.Length; i++)
                {
                    hash = (hash * 31) + shapeId[i];
                }
            }

            var normalized = (hash & 0x7FFFFFFF) / (float)int.MaxValue;
            return normalized * Mathf.PI * 2f;
        }
    }
}

public enum ProceduralMaterialApplyStatus
{
    Applied,
    SkippedDisabled,
    FallbackMissingMaterial,
    FallbackMissingShader,
    FallbackMissingProperty,
    FallbackUnsupportedRenderer
}
