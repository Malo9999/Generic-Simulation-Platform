using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Flat", fileName = "FlatArtPipeline")]
public class FlatArtPipeline : ArtPipelineBase
{
    private const string SpriteName = "Sprite";
    private const string IconRootName = "IconRoot";
    private const string MaskName = "Mask";

    [SerializeField] private bool forceDebugPlaceholder = true;
    [SerializeField] private DebugPlaceholderMode defaultDebugMode = DebugPlaceholderMode.Replace;
    [SerializeField] private float placeholderScale = 0.5f;

    private bool debugEnabled;
    private DebugPlaceholderMode debugMode;

    public override ArtMode Mode => ArtMode.Flat;
    public override string DisplayName => "Flat";

    private void OnEnable()
    {
        debugEnabled = forceDebugPlaceholder;
        debugMode = defaultDebugMode;
    }

    public override void ConfigureDebug(bool enabled, DebugPlaceholderMode mode)
    {
        forceDebugPlaceholder = enabled;
        debugEnabled = enabled;
        debugMode = mode;
    }

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

        var spriteObject = new GameObject(SpriteName);
        spriteObject.transform.SetParent(rendererObject.transform, false);
        var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = DebugShapeSpriteFactory.GetSquareSprite();
        spriteRenderer.color = PlaceholderColorPalette.GetColor(key);
        RenderOrder.Apply(spriteRenderer, RenderOrder.EntityBody);
        spriteRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, placeholderScale);

        return rendererObject;
    }

    public override void ApplyVisual(GameObject renderer, VisualKey key, Vector2 velocity, float deltaTime)
    {
        if (renderer == null)
        {
            return;
        }

        if (debugEnabled)
        {
            ApplyPlaceholderSorting(renderer, debugOn: true);
            SetPlaceholderVisible(renderer, true);
            if (debugMode == DebugPlaceholderMode.Replace)
            {
                SetIconRootVisibility(renderer, false);
                SetDebugReplaceVisibility(renderer);
            }

            return;
        }

        ApplyPlaceholderSorting(renderer, debugOn: false);
        SetPlaceholderVisible(renderer, false);
        SetIconRootVisibility(renderer, true);
    }

    private static void ApplyPlaceholderSorting(GameObject renderer, bool debugOn)
    {
        var spriteRenderer = renderer.transform.Find(SpriteName)?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            RenderOrder.Apply(spriteRenderer, debugOn ? RenderOrder.DebugEntity : RenderOrder.EntityBody);
        }
    }

    private static void SetPlaceholderVisible(GameObject renderer, bool visible)
    {
        var spriteRenderer = renderer.transform.Find(SpriteName)?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }


    private static void SetDebugReplaceVisibility(GameObject rendererRoot)
    {
        var spriteRenderers = rendererRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            var rendererName = spriteRenderer.gameObject.name;
            spriteRenderer.enabled = string.Equals(rendererName, SpriteName, StringComparison.Ordinal)
                || string.Equals(rendererName, MaskName, StringComparison.Ordinal);
        }
    }

    private static void SetIconRootVisibility(GameObject renderer, bool fallbackVisible)
    {
        var iconRoot = renderer.transform.Find(IconRootName);
        if (iconRoot == null && renderer.transform.parent != null)
        {
            iconRoot = renderer.transform.parent.Find(IconRootName);
        }

        if (iconRoot != null)
        {
            iconRoot.gameObject.SetActive(fallbackVisible);
        }
    }

    private static Color BuildStableColor(VisualKey key)
    {
        var hashSource = $"{key.entityId}|{key.kind}|{key.variantSeed}";
        var hash = ComputeStableHash(hashSource);
        var hue = (hash % 360u) / 360f;
        return Color.HSVToRGB(hue, 0.65f, 0.95f);
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

}
