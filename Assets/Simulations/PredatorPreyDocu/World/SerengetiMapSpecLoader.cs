using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class SerengetiMapSpecLoader
{
    public static SerengetiMapSpec LoadOrThrow(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            throw new ArgumentException("Map id is required.", nameof(mapId));
        }

        var text = TryLoadFromResources(mapId);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = TryLoadFromStreamingAssets(mapId);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Map '{mapId}' not found. Expected Resources/Maps/{mapId}.json or StreamingAssets/Maps/{mapId}.json.");
        }

        SerengetiMapSpec spec;
        try
        {
            spec = JsonConvert.DeserializeObject<SerengetiMapSpec>(text);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Map '{mapId}' JSON parse failed: {ex.Message}", ex);
        }

        var errors = SerengetiMapSpecValidator.Validate(spec);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Map '{mapId}' failed validation:\n - {string.Join("\n - ", errors)}");
        }

        return spec;
    }

    private static string TryLoadFromResources(string mapId)
    {
        var textAsset = Resources.Load<TextAsset>($"Maps/{mapId}");
        return textAsset != null ? textAsset.text : null;
    }

    private static string TryLoadFromStreamingAssets(string mapId)
    {
        var path = Path.Combine(Application.streamingAssetsPath, "Maps", $"{mapId}.json");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
