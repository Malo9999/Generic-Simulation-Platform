using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Flat", fileName = "FlatArtPipeline")]
public class FlatArtPipeline : ArtPipelineBase
{
    [SerializeField] private bool forceDebugPlaceholder = true;
    [SerializeField] private float placeholderScale = 0.5f;

    public override ArtMode Mode => ArtMode.Flat;
    public override string DisplayName => "Flat";

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
        var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = DebugShapeSpriteFactory.GetSquareSprite();
        spriteRenderer.color = BuildStableColor(key);
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

        if (forceDebugPlaceholder)
        {
            ApplyPlaceholderSorting(renderer, debugOn: true);
            SetIconRootVisibility(renderer, true);
            SetPlaceholderVisible(renderer, true);
            return;
        }

        ApplyPlaceholderSorting(renderer, debugOn: false);
        // Flat keeps placeholder visuals for now even when debug forcing is disabled.
        SetIconRootVisibility(renderer, false);
        SetPlaceholderVisible(renderer, true);
    }

    private static void ApplyPlaceholderSorting(GameObject renderer, bool debugOn)
    {
        var spriteRenderer = renderer.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            RenderOrder.Apply(spriteRenderer, debugOn ? RenderOrder.DebugEntity : RenderOrder.EntityBody);
        }
    }

    private static void SetPlaceholderVisible(GameObject renderer, bool visible)
    {
        var spriteRenderer = renderer.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }

    private static void SetIconRootVisibility(GameObject renderer, bool fallbackVisible)
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
