using UnityEngine;
using System;
using System.Collections.Generic;

public static class ContentPackService
{
    public static ContentPack Current { get; private set; }
    private static readonly HashSet<string> MissingLogged = new(StringComparer.Ordinal);
    private static Sprite squarePlaceholderSprite;
    private static bool hasLoggedMissingAgentSprite;


    public static void Set(ContentPack pack)
    {
        Current = pack;
        MissingLogged.Clear();
        hasLoggedMissingAgentSprite = false;
        if (pack != null)
        {
            Debug.Log($"[ContentPack] Set '{pack.name}' sprites={pack.Sprites.Count} selections={pack.Selections.Count}");
        }
    }

    public static void Clear()
    {
        Current = null;
        MissingLogged.Clear();
        hasLoggedMissingAgentSprite = false;
        Debug.LogWarning("[ContentPack] Cleared (no pack)");
    }

    public static bool TryGetSprite(string id, out Sprite sprite)
    {
        if (Current == null)
        {
            return TryGetPlaceholderSprite(id, out sprite);
        }

        if (Current.TryGetSprite(id, out sprite))
        {
            return true;
        }

        if (TryGetWithFrameTolerance(id, out sprite))
        {
            return true;
        }

        if (TryGetWithRoleAlias(id, out sprite))
        {
            return true;
        }

        if (TryGetWithSpeciesFallback(id, out sprite))
        {
            return true;
        }

        if (IsPlaceholderPack(Current) && TryGetPlaceholderSprite(id, out sprite))
        {
            return true;
        }

        if (id != null && id.StartsWith("agent:", StringComparison.Ordinal))
        {
            LogMissingAgentSpriteOnce(id);
            return false;
        }

        LogOnce(id ?? string.Empty, $"[ContentPack] Missing sprite id: {id} (pack={Current.name})");
        return false;
    }

    public static string GetSpeciesId(string entityId, int variantIndex)
    {
        return Current == null ? "default" : Current.GetSpeciesId(entityId, variantIndex);
    }

    public static int GetClipFpsOrDefault(string entityId, string role, string stage, string state, int fallbackFps)
    {
        if (Current == null)
        {
            return fallbackFps;
        }

        var keyPrefix = $"agent:{entityId}:{role}:{stage}:{state}";
        if (!Current.TryGetClipMetadata(keyPrefix, out var clip) || clip.fps <= 0)
        {
            return fallbackFps;
        }

        return clip.fps;
    }

    private static bool TryGetWithFrameTolerance(string id, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var parts = id.Split(':');
        if (parts.Length == 0)
        {
            return false;
        }

        var lastIndex = parts.Length - 1;
        if (!int.TryParse(parts[lastIndex], out var frameIndex))
        {
            return false;
        }

        var normalizedFrame = frameIndex.ToString();
        if (string.Equals(parts[lastIndex], normalizedFrame, StringComparison.Ordinal))
        {
            return false;
        }

        parts[lastIndex] = normalizedFrame;
        var alternateId = string.Join(":", parts);
        return Current.TryGetSprite(alternateId, out sprite);
    }

    private static bool TryGetWithRoleAlias(string id, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (id.Contains(":soldier:", StringComparison.Ordinal))
        {
            return Current.TryGetSprite(id.Replace(":soldier:", ":warrior:"), out sprite);
        }

        if (id.Contains(":warrior:", StringComparison.Ordinal))
        {
            return Current.TryGetSprite(id.Replace(":warrior:", ":soldier:"), out sprite);
        }

        return false;
    }

    private static bool TryGetWithSpeciesFallback(string id, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(id) || !id.Contains(":default:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = id.Split(':');
        if (parts.Length < 4 || !string.Equals(parts[0], "agent", StringComparison.Ordinal))
        {
            return false;
        }

        var entityId = parts[1];
        var inferredSpecies = Current.InferFirstSpeciesId(entityId);
        if (string.IsNullOrWhiteSpace(inferredSpecies))
        {
            return false;
        }

        parts[2] = inferredSpecies;
        var alternateId = string.Join(":", parts);
        return Current.TryGetSprite(alternateId, out sprite);
    }

    private static bool TryGetPlaceholderSprite(string id, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (id.StartsWith("prop:", StringComparison.Ordinal))
        {
            sprite = id.Contains("outline", StringComparison.OrdinalIgnoreCase)
                ? PrimitiveSpriteLibrary.RoundedRectOutline()
                : PrimitiveSpriteLibrary.CircleFill();
            return true;
        }

        if (id.StartsWith("tile:", StringComparison.Ordinal))
        {
            sprite = GetSquarePlaceholderSprite();
            return true;
        }

        if (id.StartsWith("agent:", StringComparison.Ordinal))
        {
            sprite = id.Contains("mask", StringComparison.OrdinalIgnoreCase) || id.Contains("outline", StringComparison.OrdinalIgnoreCase)
                ? PrimitiveSpriteLibrary.CapsuleOutline()
                : PrimitiveSpriteLibrary.CapsuleFill();
            return true;
        }

        var normalized = id.ToLowerInvariant();
        if (string.Equals(normalized, "agent_alt", StringComparison.Ordinal))
        {
            sprite = PrimitiveSpriteLibrary.CapsuleOutline();
            return true;
        }

        return false;
    }

    private static bool IsPlaceholderPack(ContentPack pack)
    {
        return pack != null && string.Equals(pack.name, "DefaultPlaceholderContentPack", StringComparison.Ordinal);
    }

    private static Sprite GetSquarePlaceholderSprite()
    {
        if (squarePlaceholderSprite != null)
        {
            return squarePlaceholderSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);
        squarePlaceholderSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squarePlaceholderSprite;
    }

    private static void LogMissingAgentSpriteOnce(string id)
    {
        if (hasLoggedMissingAgentSprite)
        {
            return;
        }

        hasLoggedMissingAgentSprite = true;
        var packName = Current != null ? Current.name : "<none>";
        Debug.LogWarning($"Missing agent sprite id: {id} (pack={packName})");
    }

    private static void LogOnce(string key, string message)
    {
        var dedupeKey = key ?? string.Empty;
        if (!MissingLogged.Add(dedupeKey))
        {
            return;
        }

        Debug.LogWarning(message);
    }
}
