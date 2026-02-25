using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Iso L1 4Dir", fileName = "IsoL1_4DirPipeline")]
public class IsoL1_4DirPipeline : ArtPipelineBase
{
    private const string Requirement = "iso4.agents";

    [SerializeField] private bool forceDebugPlaceholder = true;
    [SerializeField] private float placeholderScale = 0.5f;

    public override ArtMode Mode => ArtMode.IsoL1_4Dir;
    public override string DisplayName => "Iso L1 4Dir";

    public override bool IsAvailable(ArtManifest manifest)
    {
        if (forceDebugPlaceholder)
        {
            return true;
        }

        return manifest != null && manifest.Has(Requirement);
    }

    public override List<string> MissingRequirements(ArtManifest manifest)
    {
        if (forceDebugPlaceholder)
        {
            return new List<string>();
        }

        return IsAvailable(manifest) ? new List<string>() : new List<string> { Requirement };
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
        spriteRenderer.sprite = DebugShapeSpriteFactory.GetDiamondSprite();
        spriteRenderer.color = BuildStableColor(key);
        RenderOrder.Apply(spriteRenderer, RenderOrder.EntityBody);
        spriteRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, placeholderScale);

        var arrowObject = new GameObject("Arrow");
        arrowObject.transform.SetParent(rendererObject.transform, false);
        arrowObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        var arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = DebugShapeSpriteFactory.GetArrowSprite();
        arrowRenderer.color = Color.Lerp(spriteRenderer.color, Color.white, 0.2f);
        RenderOrder.Apply(arrowRenderer, RenderOrder.EntityArrow);
        arrowRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, placeholderScale);

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
            ApplySnappedFacing(renderer, velocity);
            return;
        }

        ApplyPlaceholderSorting(renderer, debugOn: false);
        // Keep placeholder-only in this phase, while preserving the toggle interface.
        SetIconRootVisibility(renderer, false);
        SetPlaceholderVisible(renderer, true);
        ApplySnappedFacing(renderer, velocity);
    }


    private static void ApplyPlaceholderSorting(GameObject renderer, bool debugOn)
    {
        var spriteRenderer = renderer.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            RenderOrder.Apply(spriteRenderer, debugOn ? RenderOrder.DebugEntity : RenderOrder.EntityBody);
        }

        var arrowRenderer = renderer.transform.Find("Arrow")?.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            RenderOrder.Apply(arrowRenderer, debugOn ? RenderOrder.DebugArrow : RenderOrder.EntityArrow);
        }
    }
    private static void ApplySnappedFacing(GameObject renderer, Vector2 velocity)
    {
        var arrow = renderer.transform.Find("Arrow");
        if (arrow == null)
        {
            return;
        }

        if (velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float facingDegrees;
        if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
        {
            facingDegrees = velocity.x >= 0f ? 0f : 180f;
        }
        else
        {
            facingDegrees = velocity.y >= 0f ? 90f : 270f;
        }

        arrow.localRotation = Quaternion.Euler(0f, 0f, facingDegrees);
    }

    private static void SetPlaceholderVisible(GameObject renderer, bool visible)
    {
        var spriteRenderer = renderer.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }

        var arrowRenderer = renderer.transform.Find("Arrow")?.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            arrowRenderer.enabled = visible;
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

}
