using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShapeLibraryProvider
{
    private const string DefaultResourcePath = "Shapes/ShapeLibrary";

    private static ShapeLibrary cachedLibrary;
    private static Dictionary<string, Sprite> spriteById;
    private static bool initialized;

    public static bool TryGetSprite(string id, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        EnsureInitialized();
        if (spriteById == null)
        {
            return false;
        }

        return spriteById.TryGetValue(id, out sprite) && sprite != null;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        cachedLibrary = Resources.Load<ShapeLibrary>(DefaultResourcePath);
        if (cachedLibrary == null)
        {
            return;
        }

        var entries = cachedLibrary.Entries;
        spriteById = new Dictionary<string, Sprite>(entries.Count, StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.id) || entry.sprite == null)
            {
                continue;
            }

            spriteById[entry.id] = entry.sprite;
        }
    }
}
