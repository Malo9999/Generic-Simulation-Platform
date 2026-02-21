using UnityEngine;

public static class ContentPackService
{
    public static ContentPack Current { get; private set; }

    public static void Set(ContentPack pack)
    {
        Current = pack;
    }

    public static void Clear()
    {
        Current = null;
    }

    public static bool TryGetSprite(string id, out Sprite sprite)
    {
        if (Current == null)
        {
            sprite = null;
            return false;
        }

        return Current.TryGetSprite(id, out sprite);
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
}
