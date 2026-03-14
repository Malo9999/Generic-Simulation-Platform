using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class MachinePieceJsonLoader
{
    public static T LoadFromTextAssetOrThrow<T>(TextAsset asset, string label)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            throw new InvalidOperationException($"{label} text asset is missing.");
        }

        try
        {
            var model = JsonConvert.DeserializeObject<T>(asset.text);
            if (model == null)
            {
                throw new InvalidOperationException($"{label} parsed null model.");
            }

            return model;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse {label}: {ex.Message}", ex);
        }
    }

    public static MachinePieceLibrary BuildLibraryOrThrow(IEnumerable<TextAsset> pieceSpecAssets, IEnumerable<TextAsset> surfaceProfileAssets)
    {
        var lib = new MachinePieceLibrary();
        var errors = new List<string>();

        foreach (var asset in pieceSpecAssets)
        {
            var spec = LoadFromTextAssetOrThrow<PieceSpec>(asset, $"PieceSpec '{asset.name}'");
            var validation = MachinePieceValidation.ValidatePieceSpec(spec);
            if (validation.Count > 0)
            {
                errors.AddRange(validation);
                continue;
            }

            lib.PieceSpecs[spec.id] = spec;
        }

        foreach (var asset in surfaceProfileAssets)
        {
            var profile = LoadFromTextAssetOrThrow<SurfaceProfile>(asset, $"SurfaceProfile '{asset.name}'");
            var validation = MachinePieceValidation.ValidateSurfaceProfile(profile);
            if (validation.Count > 0)
            {
                errors.AddRange(validation);
                continue;
            }

            lib.SurfaceProfiles[profile.id] = profile;
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"MachinePieces library validation failed:\n - {string.Join("\n - ", errors)}");
        }

        return lib;
    }
}
