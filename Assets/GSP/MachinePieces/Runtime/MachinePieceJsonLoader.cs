using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class MachinePieceJsonLoader
{
    public const string SharedPieceSpecsFolder = "Assets/GSP/MachinePieces/PieceSpecs";
    public const string SharedSurfaceProfilesFolder = "Assets/GSP/MachinePieces/SurfaceProfiles";

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

    public static MachinePieceLibrary BuildLibraryOrThrow(
        IEnumerable<TextAsset> pieceSpecAssets,
        IEnumerable<TextAsset> surfaceProfileAssets,
        bool includeSharedAssets = true,
        string diagnosticsPrefix = "MachinePieces")
    {
        var lib = new MachinePieceLibrary();
        var errors = new List<string>();
        var diagnostics = new List<string>();

        var discoveredPieceSpecs = CollectAssets(pieceSpecAssets, includeSharedAssets, SharedPieceSpecsFolder, diagnostics, "piece spec");
        var discoveredSurfaceProfiles = CollectAssets(surfaceProfileAssets, includeSharedAssets, SharedSurfaceProfilesFolder, diagnostics, "surface profile");

        diagnostics.Add($"Discovered {discoveredPieceSpecs.Count} piece spec assets.");
        diagnostics.Add($"Discovered {discoveredSurfaceProfiles.Count} surface profile assets.");

        foreach (var asset in discoveredPieceSpecs)
        {
            PieceSpec spec;
            try
            {
                spec = LoadFromTextAssetOrThrow<PieceSpec>(asset, $"PieceSpec '{asset.name}'");
            }
            catch (Exception ex)
            {
                var reason = $"Rejected piece spec asset '{asset.name}': {ex.Message}";
                errors.Add(reason);
                diagnostics.Add(reason);
                continue;
            }

            var validation = MachinePieceValidation.ValidatePieceSpec(spec);
            if (validation.Count > 0)
            {
                foreach (var validationError in validation)
                {
                    var reason = $"Rejected piece spec '{spec.id}' from '{asset.name}': {validationError}";
                    errors.Add(reason);
                    diagnostics.Add(reason);
                }

                continue;
            }

            lib.PieceSpecs[spec.id] = spec;
            diagnostics.Add($"Loaded piece spec '{spec.id}' from '{asset.name}'.");
        }

        foreach (var asset in discoveredSurfaceProfiles)
        {
            SurfaceProfile profile;
            try
            {
                profile = LoadFromTextAssetOrThrow<SurfaceProfile>(asset, $"SurfaceProfile '{asset.name}'");
            }
            catch (Exception ex)
            {
                var reason = $"Rejected surface profile asset '{asset.name}': {ex.Message}";
                errors.Add(reason);
                diagnostics.Add(reason);
                continue;
            }

            var validation = MachinePieceValidation.ValidateSurfaceProfile(profile);
            if (validation.Count > 0)
            {
                foreach (var validationError in validation)
                {
                    var reason = $"Rejected surface profile '{profile.id}' from '{asset.name}': {validationError}";
                    errors.Add(reason);
                    diagnostics.Add(reason);
                }

                continue;
            }

            lib.SurfaceProfiles[profile.id] = profile;
            diagnostics.Add($"Loaded surface profile '{profile.id}' from '{asset.name}'.");
        }

        if (diagnostics.Count > 0)
        {
            Debug.Log($"[{diagnosticsPrefix}] Library load diagnostics:\n - {string.Join("\n - ", diagnostics)}");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"MachinePieces library validation failed:\n - {string.Join("\n - ", errors)}");
        }

        return lib;
    }

    private static List<TextAsset> CollectAssets(
        IEnumerable<TextAsset> explicitAssets,
        bool includeSharedAssets,
        string sharedFolder,
        List<string> diagnostics,
        string kind)
    {
        var collected = new List<TextAsset>();
        var seen = new HashSet<TextAsset>();

        foreach (var asset in explicitAssets ?? Array.Empty<TextAsset>())
        {
            if (asset == null || !seen.Add(asset))
            {
                continue;
            }

            collected.Add(asset);
            diagnostics.Add($"Discovered {kind} '{asset.name}' from inspector assignment.");
        }

        if (!includeSharedAssets)
        {
            return collected;
        }

#if UNITY_EDITOR
        foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { sharedFolder }))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null || !seen.Add(asset))
            {
                continue;
            }

            collected.Add(asset);
            diagnostics.Add($"Discovered {kind} '{asset.name}' from shared folder '{sharedFolder}' ({assetPath}).");
        }
#else
        var resourcesFolder = ToResourcesRelativePath(sharedFolder);
        if (!string.IsNullOrWhiteSpace(resourcesFolder))
        {
            foreach (var asset in Resources.LoadAll<TextAsset>(resourcesFolder))
            {
                if (asset == null || !seen.Add(asset))
                {
                    continue;
                }

                collected.Add(asset);
                diagnostics.Add($"Discovered {kind} '{asset.name}' from Resources/{resourcesFolder}.");
            }
        }
#endif

        return collected;
    }

    private static string ToResourcesRelativePath(string assetPath)
    {
        const string resourcesSegment = "/Resources/";
        var idx = assetPath.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return string.Empty;
        }

        var relativePath = assetPath[(idx + resourcesSegment.Length)..];
        return Path.ChangeExtension(relativePath, null);
    }
}
