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
    private readonly Dictionary<string, ResolvedSpriteSource> resolvedBaseByKey = new(StringComparer.Ordinal);
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
        if (renderer == null)
        {
            return;
        }

        var animator = renderer.GetComponent<SimplePipelineSpriteAnimator>();
        if (animator == null)
        {
            return;
        }

        var resolvedSource = ResolveSpriteBase(key);
        if (!resolvedSource.HasValue || !TryGetFrames(resolvedSource, out var frames))
        {
            SetFallbackVisibility(renderer, true);
            return;
        }

        var speed = velocity.magnitude;
        var frameIndex = SelectFrameIndex(key.state, speed, animator, deltaTime);
        animator.lastSpriteBaseId = resolvedSource.BaseId;
        animator.Apply(frames, frameIndex);
        animator.ApplyFacing(velocity);
        SetFallbackVisibility(renderer, false);
    }

    private ResolvedSpriteSource ResolveSpriteBase(VisualKey key)
    {
        var entityType = NormalizeSegment(key.entityId, "default");
        var role = NormalizeSegment(key.kind, "default");
        var state = NormalizeSegment(key.state, "idle");
        var species = ContentPackService.GetSpeciesId(entityType, key.variantSeed);
        if (string.IsNullOrWhiteSpace(species))
        {
            species = "default";
        }

        var cacheKey = $"{entityType}|{role}|{state}|{species}";
        if (resolvedBaseByKey.TryGetValue(cacheKey, out var cachedBase))
        {
            return cachedBase;
        }

        foreach (var candidate in BuildCandidates(entityType, species, role, state))
        {
            if (TryResolveSource(candidate, out var resolvedSource))
            {
                resolvedBaseByKey[cacheKey] = resolvedSource;
                return resolvedSource;
            }
        }

        if (missingBaseLogged.Add(cacheKey))
        {
            Debug.LogWarning($"[SimpleArtPipeline] No sprite prefix found for '{cacheKey}'. Falling back to icon renderer.");
        }

        resolvedBaseByKey[cacheKey] = default;
        return default;
    }

    private static string NormalizeSegment(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant();
    }

    private static IEnumerable<string> BuildCandidates(string entityType, string species, string role, string state)
    {
        if (string.Equals(entityType, "ant", StringComparison.Ordinal))
        {
            yield return $"agent:ant:{species}:{role}:adult:{state}";
            yield return $"agent:ant:{species}:{role}:{state}";
            yield return $"agent:ant:{role}:{state}";
            yield return $"agent:ant:{state}";
            yield break;
        }

        yield return $"agent:{entityType}:{species}:{role}:{state}";
        yield return $"agent:{entityType}:{role}:{state}";
        yield return $"agent:{entityType}:{state}";
    }

    private static bool TryResolveSource(string baseId, out ResolvedSpriteSource resolvedSource)
    {
        resolvedSource = default;
        var pack = ContentPackService.Current;
        if (pack == null)
        {
            return false;
        }

        if (TryDetectScheme(pack, baseId, out var scheme))
        {
            resolvedSource = new ResolvedSpriteSource(baseId, scheme);
            return true;
        }

        return false;
    }

    private bool TryGetFrames(ResolvedSpriteSource resolvedSource, out Sprite[] frames)
    {
        var cacheId = resolvedSource.CacheId;
        if (framesByBaseId.TryGetValue(cacheId, out frames))
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
            var spriteId = BuildFrameId(resolvedSource.BaseId, resolvedSource.Scheme, i);
            if (!pack.TryGetSprite(spriteId, out frames[i]) || frames[i] == null)
            {
                if (missingBaseLogged.Add(cacheId))
                {
                    Debug.LogWarning($"[SimpleArtPipeline] Missing frame '{spriteId}' for '{resolvedSource.BaseId}'. Falling back to icon renderer.");
                }

                return false;
            }

            if (frames[i].texture != null)
            {
                frames[i].texture.filterMode = FilterMode.Point;
            }
        }

        framesByBaseId[cacheId] = frames;
        return true;
    }

    private static bool TryDetectScheme(ContentPack pack, string prefix, out FrameIndexScheme scheme)
    {
        if (pack.TryGetSprite($"{prefix}:00", out _))
        {
            scheme = FrameIndexScheme.ZeroBasedTwoDigit;
            return true;
        }

        if (pack.TryGetSprite($"{prefix}:0", out _))
        {
            scheme = FrameIndexScheme.ZeroBasedPlain;
            return true;
        }

        if (pack.TryGetSprite($"{prefix}:01", out _))
        {
            scheme = FrameIndexScheme.OneBasedTwoDigit;
            return true;
        }

        if (pack.TryGetSprite($"{prefix}:1", out _))
        {
            scheme = FrameIndexScheme.OneBasedPlain;
            return true;
        }

        scheme = default;
        return false;
    }

    private static string BuildFrameId(string baseId, FrameIndexScheme scheme, int frameIndex)
    {
        return scheme switch
        {
            FrameIndexScheme.ZeroBasedTwoDigit => $"{baseId}:{frameIndex:00}",
            FrameIndexScheme.ZeroBasedPlain => $"{baseId}:{frameIndex}",
            FrameIndexScheme.OneBasedTwoDigit => $"{baseId}:{frameIndex + 1:00}",
            FrameIndexScheme.OneBasedPlain => $"{baseId}:{frameIndex + 1}",
            _ => $"{baseId}:{frameIndex:00}"
        };
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
        var iconRoot = renderer.transform.Find("IconRoot");
        if (iconRoot == null && renderer.transform.parent != null)
        {
            iconRoot = renderer.transform.parent.Find("IconRoot");
        }

        if (iconRoot != null)
        {
            iconRoot.gameObject.SetActive(fallbackVisible);
        }

        var pipelineRenderer = renderer.GetComponent<SimplePipelineSpriteAnimator>()?.SpriteRenderer;
        if (pipelineRenderer != null)
        {
            pipelineRenderer.enabled = !fallbackVisible;
        }
    }

    private enum FrameIndexScheme
    {
        ZeroBasedTwoDigit,
        ZeroBasedPlain,
        OneBasedTwoDigit,
        OneBasedPlain
    }

    private readonly struct ResolvedSpriteSource
    {
        public ResolvedSpriteSource(string baseId, FrameIndexScheme scheme)
        {
            BaseId = baseId;
            Scheme = scheme;
        }

        public string BaseId { get; }
        public FrameIndexScheme Scheme { get; }
        public bool HasValue => !string.IsNullOrEmpty(BaseId);
        public string CacheId => $"{BaseId}|{Scheme}";
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
