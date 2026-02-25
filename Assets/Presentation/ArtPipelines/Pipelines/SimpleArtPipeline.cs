using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Simple", fileName = "SimpleArtPipeline")]
public class SimpleArtPipeline : ArtPipelineBase
{
    private const int TotalFrames = 10;
    private const float IdleSpeedThreshold = 0.05f;
    private const float RunSpeedThreshold = 1.5f;
    private const float IdleAnimRate = 1.8f;
    private const float WalkAnimRate = 5f;
    private const float RunAnimRate = 8f;

    private readonly Dictionary<string, Sprite[]> framesByBaseId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> resolvedBaseByKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingBaseLogged = new(StringComparer.Ordinal);

    public override ArtMode Mode => ArtMode.Simple;
    public override string DisplayName => "Simple";

    public override bool IsAvailable(ArtManifest manifest)
    {
        return true;
    }

    public override List<string> MissingRequirements(ArtManifest manifest)
    {
        return new List<string>();
    }

    public override GameObject CreateRenderer(VisualKey key, Transform parent)
    {
        GameObject rendererObject = new($"Renderer_{key.entityId}_{key.instanceId}");
        rendererObject.transform.SetParent(parent, false);

        var fallbackRenderer = rendererObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = null;

        var spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(rendererObject.transform, false);
        var pipelineRenderer = spriteObject.AddComponent<SpriteRenderer>();
        pipelineRenderer.sprite = null;

        var animator = rendererObject.AddComponent<SimplePipelineSpriteAnimator>();
        animator.Initialize(pipelineRenderer);

        return rendererObject;
    }

    public override void ApplyVisual(GameObject renderer, VisualKey key, Vector2 velocity, float deltaTime)
    {
        if (renderer == null || !IsAntColoniesAnt(key))
        {
            return;
        }

        var animator = renderer.GetComponent<SimplePipelineSpriteAnimator>();
        if (animator == null)
        {
            return;
        }

        var resolvedBase = ResolveSpriteBase(key);
        if (string.IsNullOrEmpty(resolvedBase) || !TryGetFrames(resolvedBase, out var frames))
        {
            SetFallbackVisibility(renderer, true);
            return;
        }

        var speed = velocity.magnitude;
        var frameIndex = SelectFrameIndex(key.state, speed, animator, deltaTime);
        animator.lastSpriteBaseId = resolvedBase;
        animator.Apply(frames, frameIndex);
        animator.ApplyFacing(velocity);
        SetFallbackVisibility(renderer, false);
    }

    private static bool IsAntColoniesAnt(VisualKey key)
    {
        return string.Equals(key.simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase)
            && string.Equals(key.entityId, "ant", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveSpriteBase(VisualKey key)
    {
        var cacheKey = $"{key.entityId}|{key.kind}|{key.state}|{key.variantSeed}";
        if (resolvedBaseByKey.TryGetValue(cacheKey, out var cachedBase))
        {
            return cachedBase;
        }

        var role = string.IsNullOrWhiteSpace(key.kind) ? "worker" : key.kind.Trim().ToLowerInvariant();
        var state = string.IsNullOrWhiteSpace(key.state) ? "idle" : key.state.Trim().ToLowerInvariant();
        var species = ContentPackService.GetSpeciesId("ant", key.variantSeed);
        if (string.IsNullOrWhiteSpace(species))
        {
            species = "default";
        }

        var candidates = new[]
        {
            $"ant.{species}.{role}.{state}",
            $"ant.{role}.{state}",
            $"ant.{state}"
        };

        foreach (var candidate in candidates)
        {
            if (HasFrame(candidate, 1))
            {
                resolvedBaseByKey[cacheKey] = candidate;
                return candidate;
            }
        }

        resolvedBaseByKey[cacheKey] = string.Empty;
        return string.Empty;
    }

    private bool HasFrame(string baseId, int frameNumber)
    {
        var pack = ContentPackService.Current;
        if (pack == null)
        {
            return false;
        }

        return pack.TryGetSprite(BuildFrameId(baseId, frameNumber), out _);
    }

    private bool TryGetFrames(string baseId, out Sprite[] frames)
    {
        if (framesByBaseId.TryGetValue(baseId, out frames))
        {
            return true;
        }

        frames = new Sprite[TotalFrames];
        var pack = ContentPackService.Current;
        if (pack == null)
        {
            return false;
        }

        for (var i = 0; i < TotalFrames; i++)
        {
            var spriteId = BuildFrameId(baseId, i + 1);
            if (!pack.TryGetSprite(spriteId, out frames[i]) || frames[i] == null)
            {
                if (missingBaseLogged.Add(baseId))
                {
                    Debug.LogWarning($"[SimpleArtPipeline] Missing frame '{spriteId}' for '{baseId}'. Falling back to icon renderer.");
                }

                return false;
            }

            if (frames[i].texture != null)
            {
                frames[i].texture.filterMode = FilterMode.Point;
            }
        }

        framesByBaseId[baseId] = frames;
        return true;
    }

    private static string BuildFrameId(string baseId, int frameNumber)
    {
        return $"{baseId}.f{frameNumber:00}";
    }

    private static int SelectFrameIndex(string state, float speed, SimplePipelineSpriteAnimator animator, float deltaTime)
    {
        var normalizedState = state?.ToLowerInvariant() ?? string.Empty;
        if (normalizedState.IndexOf("fight", StringComparison.Ordinal) >= 0 || normalizedState.IndexOf("attack", StringComparison.Ordinal) >= 0)
        {
            return 9;
        }

        if (speed < IdleSpeedThreshold || string.Equals(normalizedState, "idle", StringComparison.Ordinal))
        {
            animator.animTime += deltaTime * IdleAnimRate;
            return Mathf.FloorToInt(animator.animTime) % 2;
        }

        var isRun = string.Equals(normalizedState, "run", StringComparison.Ordinal) || speed > RunSpeedThreshold;
        if (isRun)
        {
            animator.animTime += deltaTime * RunAnimRate;
            return 5 + (Mathf.FloorToInt(animator.animTime) % 4);
        }

        animator.animTime += deltaTime * WalkAnimRate;
        return 2 + (Mathf.FloorToInt(animator.animTime) % 3);
    }

    private static void SetFallbackVisibility(GameObject renderer, bool fallbackVisible)
    {
        var fallbackRenderer = renderer.GetComponent<SpriteRenderer>();
        if (fallbackRenderer != null)
        {
            fallbackRenderer.enabled = fallbackVisible;
        }

        var externalRenderers = renderer.transform.parent != null
            ? renderer.transform.parent.GetComponentsInChildren<SpriteRenderer>(true)
            : Array.Empty<SpriteRenderer>();

        for (var i = 0; i < externalRenderers.Length; i++)
        {
            var sr = externalRenderers[i];
            if (sr == null || sr.transform.IsChildOf(renderer.transform))
            {
                continue;
            }

            if (string.Equals(sr.gameObject.name, "Base", StringComparison.Ordinal)
                || string.Equals(sr.gameObject.name, "Mask", StringComparison.Ordinal)
                || string.Equals(sr.gameObject.name, "IconRoot", StringComparison.Ordinal))
            {
                sr.enabled = fallbackVisible;
            }
        }

        var pipelineRenderer = renderer.GetComponent<SimplePipelineSpriteAnimator>()?.SpriteRenderer;
        if (pipelineRenderer != null)
        {
            pipelineRenderer.enabled = !fallbackVisible;
        }
    }

    private sealed class SimplePipelineSpriteAnimator : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer { get; private set; }
        public string lastSpriteBaseId;
        public float animTime;
        public int lastFrame = -1;
        public bool initialized;

        private float lastFacingDegrees;
        private bool hasFacing;

        public void Initialize(SpriteRenderer spriteRenderer)
        {
            SpriteRenderer = spriteRenderer;
            initialized = SpriteRenderer != null;
        }

        public void Apply(Sprite[] frames, int frameIndex)
        {
            if (!initialized || frames == null || frames.Length == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            if (clamped == lastFrame && SpriteRenderer.sprite == frames[clamped])
            {
                return;
            }

            SpriteRenderer.sprite = frames[clamped];
            SpriteRenderer.drawMode = SpriteDrawMode.Simple;
            SpriteRenderer.transform.localPosition = Vector3.zero;
            SpriteRenderer.transform.localScale = Vector3.one;
            lastFrame = clamped;
        }

        public void ApplyFacing(Vector2 velocity)
        {
            if (!initialized)
            {
                return;
            }

            if (velocity.sqrMagnitude > 0.0001f)
            {
                lastFacingDegrees = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                hasFacing = true;
            }

            if (!hasFacing)
            {
                return;
            }

            SpriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, lastFacingDegrees);
        }
    }
}
