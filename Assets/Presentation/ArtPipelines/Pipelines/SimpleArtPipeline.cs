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
    private const string PlaceholderSpriteName = "PlaceholderSprite";
    private const string PlaceholderArrowName = "PlaceholderArrow";
    private const string IconRootName = "IconRoot";

    private readonly Dictionary<string, Sprite[]> framesByBaseId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedSpriteSource> resolvedBaseByKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingBaseLogged = new(StringComparer.Ordinal);

    [SerializeField] private bool forceDebugPlaceholder = true;
    [SerializeField] private float placeholderScale = 0.5f;

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

        var spriteObject = new GameObject(PlaceholderSpriteName);
        spriteObject.transform.SetParent(rendererObject.transform, false);
        var dotRenderer = spriteObject.AddComponent<SpriteRenderer>();
        dotRenderer.sprite = DebugShapeSpriteFactory.GetCircleSprite();
        dotRenderer.color = BuildStableColor(key);
        RenderOrder.Apply(dotRenderer, RenderOrder.EntityBody);

        var arrowObject = new GameObject(PlaceholderArrowName);
        arrowObject.transform.SetParent(rendererObject.transform, false);
        arrowObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        var arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = DebugShapeSpriteFactory.GetArrowSprite();
        arrowRenderer.color = Color.Lerp(dotRenderer.color, Color.white, 0.2f);
        RenderOrder.Apply(arrowRenderer, RenderOrder.EntityArrow);

        var animator = rendererObject.AddComponent<SimplePipelineSpriteAnimator>();
        animator.Initialize(dotRenderer, arrowRenderer, placeholderScale);

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

        if (forceDebugPlaceholder)
        {
            ApplyPlaceholderSorting(renderer, debugOn: true);
            SetPlaceholderVisibility(renderer, dotVisible: true, arrowVisible: true);
            SetIconRootVisibility(renderer, true);
            animator.ApplyDebugFacing(velocity);
            animator.ApplyPulse(deltaTime);
            return;
        }

        ApplyPlaceholderSorting(renderer, debugOn: false);

        if (!IsAntEntity(key))
        {
            SetPlaceholderVisibility(renderer, dotVisible: false, arrowVisible: false);
            SetIconRootVisibility(renderer, true);
            return;
        }

        var resolvedSource = ResolveSpriteBase(key);
        if (!resolvedSource.HasValue || !TryGetFrames(resolvedSource, out var frames))
        {
            SetPlaceholderVisibility(renderer, dotVisible: false, arrowVisible: false);
            SetIconRootVisibility(renderer, true);
            return;
        }

        var speed = velocity.magnitude;
        var frameIndex = SelectFrameIndex(key.state, speed, animator, deltaTime);
        animator.lastSpriteBaseId = resolvedSource.BaseId;
        animator.Apply(frames, frameIndex);
        animator.ApplyContentFacing(velocity);
        SetPlaceholderVisibility(renderer, dotVisible: true, arrowVisible: false);
        SetIconRootVisibility(renderer, false);
    }

    private static void ApplyPlaceholderSorting(GameObject rendererRoot, bool debugOn)
    {
        var dotRenderer = rendererRoot.transform.Find(PlaceholderSpriteName)?.GetComponent<SpriteRenderer>();
        if (dotRenderer != null)
        {
            RenderOrder.Apply(dotRenderer, debugOn ? RenderOrder.DebugEntity : RenderOrder.EntityBody);
        }

        var arrowRenderer = rendererRoot.transform.Find(PlaceholderArrowName)?.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            RenderOrder.Apply(arrowRenderer, debugOn ? RenderOrder.DebugArrow : RenderOrder.EntityArrow);
        }
    }

    private static void SetPlaceholderVisibility(GameObject rendererRoot, bool dotVisible, bool arrowVisible)
    {
        var dotRenderer = rendererRoot.transform.Find(PlaceholderSpriteName)?.GetComponent<SpriteRenderer>();
        if (dotRenderer != null)
        {
            dotRenderer.enabled = dotVisible;
        }

        var arrowRenderer = rendererRoot.transform.Find(PlaceholderArrowName)?.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            arrowRenderer.enabled = arrowVisible;
        }
    }

    private static bool IsAntEntity(VisualKey key)
    {
        return string.Equals(NormalizeSegment(key.entityId, "default"), "ant", StringComparison.Ordinal);
    }

    private static void SetIconRootVisibility(GameObject renderer, bool visible)
    {
        var iconRoot = renderer.transform.Find(IconRootName);
        if (iconRoot == null && renderer.transform.parent != null)
        {
            iconRoot = renderer.transform.parent.Find(IconRootName);
        }

        if (iconRoot != null)
        {
            iconRoot.gameObject.SetActive(visible);
        }
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
            foreach (var lookupState in BuildAntStateLookupOrder(state))
            {
                yield return $"agent:ant:{species}:{role}:adult:{lookupState}";
                yield return $"agent:ant:{species}:{role}:{lookupState}";
                yield return $"agent:ant:{role}:{lookupState}";
                yield return $"agent:ant:{lookupState}";
            }

            yield break;
        }

        yield return $"agent:{entityType}:{species}:{role}:{state}";
        yield return $"agent:{entityType}:{role}:{state}";
        yield return $"agent:{entityType}:{state}";
    }

    private static IEnumerable<string> BuildAntStateLookupOrder(string desiredState)
    {
        var normalizedDesired = NormalizeSegment(desiredState, "idle");

        yield return normalizedDesired;
        if (!string.Equals(normalizedDesired, "walk", StringComparison.Ordinal))
        {
            yield return "walk";
        }

        if (!string.Equals(normalizedDesired, "idle", StringComparison.Ordinal))
        {
            yield return "idle";
        }
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

    private static Color BuildStableColor(VisualKey key)
    {
        var hashSource = $"{key.entityId}|{key.kind}|{key.variantSeed}";
        var hash = ComputeStableHash(hashSource);
        var hue = (hash % 360u) / 360f;
        return Color.HSVToRGB(hue, 0.7f, 0.95f);
    }

    private static uint ComputeStableHash(string value)
    {
        const uint fnvPrime = 16777619u;
        var hash = 2166136261u;
        if (string.IsNullOrEmpty(value))
        {
            return hash;
        }

        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= fnvPrime;
        }

        return hash;
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
        public SpriteRenderer DotRenderer { get; private set; }
        public SpriteRenderer ArrowRenderer { get; private set; }
        public string lastSpriteBaseId;
        public float animTime;
        public int lastFrame = -1;
        public bool initialized;
        private float debugScale = 0.5f;

        private float lastFacingDegrees;
        private bool hasFacing;

        public void Initialize(SpriteRenderer dotRenderer, SpriteRenderer arrowRenderer, float scale)
        {
            DotRenderer = dotRenderer;
            ArrowRenderer = arrowRenderer;
            debugScale = Mathf.Max(0.1f, scale);
            if (DotRenderer != null)
            {
                DotRenderer.transform.localScale = Vector3.one * debugScale;
            }

            if (ArrowRenderer != null)
            {
                ArrowRenderer.transform.localScale = Vector3.one * debugScale;
            }

            initialized = DotRenderer != null;
        }

        public void Apply(Sprite[] frames, int frameIndex)
        {
            if (!initialized || frames == null || frames.Length == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            if (clamped == lastFrame && DotRenderer.sprite == frames[clamped])
            {
                return;
            }

            DotRenderer.sprite = frames[clamped];
            DotRenderer.drawMode = SpriteDrawMode.Simple;
            DotRenderer.transform.localPosition = Vector3.zero;
            DotRenderer.transform.localScale = Vector3.one;
            lastFrame = clamped;
        }

        public void ApplyContentFacing(Vector2 velocity)
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

            DotRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, lastFacingDegrees);
            if (ArrowRenderer != null)
            {
                ArrowRenderer.transform.localRotation = Quaternion.identity;
            }
        }

        public void ApplyDebugFacing(Vector2 velocity)
        {
            if (ArrowRenderer == null)
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

            DotRenderer.transform.localRotation = Quaternion.identity;
            ArrowRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, lastFacingDegrees);
        }

        public void ApplyPulse(float deltaTime)
        {
            if (DotRenderer == null)
            {
                return;
            }

            animTime += deltaTime;
            var pulse = 1f + (0.08f * Mathf.Sin(animTime * Mathf.PI * 2f));
            DotRenderer.transform.localScale = Vector3.one * (debugScale * pulse);
            if (ArrowRenderer != null)
            {
                ArrowRenderer.transform.localScale = Vector3.one * debugScale;
            }
        }

        public void SetRendererVisibility(bool dotVisible, bool arrowVisible)
        {
            if (DotRenderer != null)
            {
                DotRenderer.enabled = dotVisible;
            }

            if (ArrowRenderer != null)
            {
                ArrowRenderer.enabled = arrowVisible;
            }
        }
    }
}
