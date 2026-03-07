using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteProceduralMaterialApplier : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static readonly int MotionEnabledId = Shader.PropertyToID("_MotionEnabled");
    private static readonly int MotionTimeId = Shader.PropertyToID("_MotionTime");
    private static readonly int MotionPhaseId = Shader.PropertyToID("_MotionPhase");
    private static readonly int MotionAmplitudeId = Shader.PropertyToID("_MotionAmplitude");
    private static readonly int MotionTintStrengthId = Shader.PropertyToID("_MotionTintStrength");
    private static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

    private static readonly int[] PrimaryColorPropertyIds =
    {
        ColorId,
        BaseColorId,
        TintId,
        TintColorId,
        FillColorId,
        MainColorId
    };

    private static readonly HashSet<string> LoggedFailures = new();

    private SpriteRenderer targetRenderer;
    private Material baselineSharedMaterial;
    private Material runtimeInstanceMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Color baseTint = Color.white;
    private float intensity = 1f;
    private float phase;
    private bool shaderUsesSpriteVertexColor;
    private int resolvedPrimaryColorProperty = -1;
    private int resolvedEmissionColorProperty = -1;

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
        phase = ComputeStablePhase(shapeId, seed);
        baseTint = renderer.color;

        resolvedPrimaryColorProperty = ResolvePrimaryColorProperty(runtimeInstanceMaterial);
        resolvedEmissionColorProperty = ResolveEmissionColorProperty(runtimeInstanceMaterial);
        shaderUsesSpriteVertexColor = ShaderUsesSpriteVertexColor(runtimeInstanceMaterial);

        ApplyProceduralState(animationTime: 0f, animationAmplitude: 1f);
        return ProceduralMaterialApplyStatus.Applied;
    }

    public void ApplyAnimationFrame(float animationTime, float animationAmplitude)
    {
        if (targetRenderer == null || runtimeInstanceMaterial == null)
        {
            return;
        }

        ApplyProceduralState(animationTime, animationAmplitude);
    }

    private void ApplyProceduralState(float animationTime, float animationAmplitude)
    {
        if (targetRenderer == null || runtimeInstanceMaterial == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
        propertyBlock.Clear();

        // Tint contract: apply body tint exactly once.
        // - For sprite shaders that consume vertex/sprite color, keep SpriteRenderer.color = tint and neutralize material primary tint to white.
        // - For non-sprite shaders, set SpriteRenderer.color = white and write tint to the material primary color property/properties.
        var safeTint = SanitizeColor(baseTint);
        if (shaderUsesSpriteVertexColor)
        {
            targetRenderer.color = safeTint;
            ApplyPrimaryTint(Color.white);
        }
        else
        {
            targetRenderer.color = Color.white;
            ApplyPrimaryTint(safeTint);
        }

        ApplyEmissionTint(safeTint);

        if (runtimeInstanceMaterial.HasProperty(MotionEnabledId))
        {
            propertyBlock.SetFloat(MotionEnabledId, 1f);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionTimeId))
        {
            propertyBlock.SetFloat(MotionTimeId, animationTime);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionPhaseId))
        {
            propertyBlock.SetFloat(MotionPhaseId, phase);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionAmplitudeId))
        {
            var safeAmplitude = Mathf.Max(0f, animationAmplitude) * Mathf.Max(0f, intensity);
            propertyBlock.SetFloat(MotionAmplitudeId, safeAmplitude);
        }

        if (runtimeInstanceMaterial.HasProperty(MotionTintStrengthId))
        {
            var sourceStrength = runtimeInstanceMaterial.GetFloat(MotionTintStrengthId);
            var safeStrength = Mathf.Clamp01(sourceStrength) * Mathf.Clamp(Mathf.Max(0f, intensity), 0f, 2f);
            propertyBlock.SetFloat(MotionTintStrengthId, safeStrength);
        }

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyPrimaryTint(Color tint)
    {
        var wroteAny = false;
        for (var i = 0; i < PrimaryColorPropertyIds.Length; i++)
        {
            var propertyId = PrimaryColorPropertyIds[i];
            if (!runtimeInstanceMaterial.HasProperty(propertyId))
            {
                continue;
            }

            propertyBlock.SetColor(propertyId, tint);
            wroteAny = true;
        }

        if (!wroteAny && resolvedPrimaryColorProperty == -1)
        {
            LogFailureOnce(ProceduralMaterialApplyStatus.FallbackMissingProperty, runtimeInstanceMaterial.name);
        }
    }

    private void ApplyEmissionTint(Color bodyTint)
    {
        if (resolvedEmissionColorProperty == -1)
        {
            return;
        }

        var sourceEmission = runtimeInstanceMaterial.GetColor(resolvedEmissionColorProperty);
        var safeEmission = SanitizeColor(sourceEmission);
        var tintInfluence = Color.Lerp(Color.white, bodyTint, 0.55f);
        var intensityScale = Mathf.Lerp(0.7f, 1.4f, Mathf.Clamp01(intensity));
        var emission = MultiplyRgb(safeEmission, tintInfluence) * intensityScale;
        emission.a = safeEmission.a;
        propertyBlock.SetColor(resolvedEmissionColorProperty, emission);
    }

    private static Color MultiplyRgb(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
    }

    private static Color SanitizeColor(Color color)
    {
        return new Color(
            Mathf.Clamp(color.r, 0f, 2f),
            Mathf.Clamp(color.g, 0f, 2f),
            Mathf.Clamp(color.b, 0f, 2f),
            Mathf.Clamp01(color.a));
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

    private static int ResolvePrimaryColorProperty(Material material)
    {
        if (material == null)
        {
            return -1;
        }

        for (var i = 0; i < PrimaryColorPropertyIds.Length; i++)
        {
            var propertyId = PrimaryColorPropertyIds[i];
            if (material.HasProperty(propertyId))
            {
                return propertyId;
            }
        }

        return -1;
    }

    private static int ResolveEmissionColorProperty(Material material)
    {
        if (material == null)
        {
            return -1;
        }

        return material.HasProperty(EmissionColorId) ? EmissionColorId : -1;
    }

    private static bool ShaderUsesSpriteVertexColor(Material material)
    {
        if (material == null)
        {
            return false;
        }

        // ShapeMotion2D (and sprite shaders in this project) consume sprite vertex color.
        return material.HasProperty(RendererColorId);
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
