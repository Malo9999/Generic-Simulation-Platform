using System;
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

        var textAsset = Resources.Load<TextAsset>($"Maps/{mapId}");
        if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
        {
            throw new InvalidOperationException($"Map '{mapId}' not found. Expected Resources/Maps/{mapId}.json.");
        }

        var text = textAsset.text;

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

}
