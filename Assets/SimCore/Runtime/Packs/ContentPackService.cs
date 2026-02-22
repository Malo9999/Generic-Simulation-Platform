using UnityEngine;
using System;
using System.Collections.Generic;

public static class ContentPackService
{
    public static ContentPack Current { get; private set; }
    private static readonly HashSet<string> MissingLogged = new(StringComparer.Ordinal);

    public static void Set(ContentPack pack)
    {
        Current = pack;
        MissingLogged.Clear();
        if (pack != null)
        {
            Debug.Log($"[ContentPack] Set '{pack.name}' sprites={pack.Sprites.Count} selections={pack.Selections.Count}");
        }
    }

    public static void Clear()
    {
        Current = null;
        MissingLogged.Clear();
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

        if (TryGetPlaceholderSprite(id, out sprite))
        {
            return true;
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

        var normalized = id.ToLowerInvariant();

        if (normalized.Contains("agent"))
        {
            sprite = normalized.Contains("mask") || normalized.Contains("outline")
                ? PrimitiveSpriteLibrary.CapsuleOutline()
                : PrimitiveSpriteLibrary.CapsuleFill();
            return true;
        }

        if (normalized.Contains("food") || normalized.Contains("target") || normalized.Contains("pickup") || normalized.Contains("ball"))
        {
            sprite = normalized.Contains("outline")
                ? PrimitiveSpriteLibrary.CircleOutline()
                : PrimitiveSpriteLibrary.CircleFill();
            return true;
        }

        if (normalized.Contains("obstacle"))
        {
            sprite = normalized.Contains("outline")
                ? PrimitiveSpriteLibrary.RoundedRectOutline()
                : PrimitiveSpriteLibrary.RoundedRectFill();
            return true;
        }

        if (normalized.Contains("goal") || normalized.Contains("finish") || normalized.Contains("marker") || normalized.Contains("decor") || normalized.Contains("background"))
        {
            sprite = PrimitiveSpriteLibrary.CircleOutline();
            return true;
        }

        if (string.Equals(normalized, "agent_alt", StringComparison.Ordinal))
        {
            sprite = PrimitiveSpriteLibrary.CapsuleOutline();
            return true;
        }

        return false;
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
