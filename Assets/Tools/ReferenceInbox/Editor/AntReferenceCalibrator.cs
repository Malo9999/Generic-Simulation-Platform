using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AntReferenceCalibrator : IReferenceCalibrator
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Ant/AntSpeciesLibrary.asset";

    public string CalibratorId => "calibrator.ant.v2";

    public bool CanCalibrate(PackRecipe recipe)
    {
        if (recipe == null) return false;
        return string.Equals(recipe.simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase)
               || recipe.entities.Any(entity => string.Equals(entity.archetypeId, "archetype.ant", StringComparison.OrdinalIgnoreCase));
    }

    public ReferenceCalibrationReport Calibrate(PackRecipe recipe)
    {
        var report = new ReferenceCalibrationReport { simulationId = recipe?.simulationId ?? "unknown" };
        if (recipe == null)
        {
            report.warnings.Add("Recipe is null.");
            return report;
        }

        var library = AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath);
        if (library == null)
        {
            report.warnings.Add($"Ant species library not found at '{LibraryPath}'.");
            return report;
        }

        var simulationFolder = Path.Combine(ReferenceInboxScaffolder.ProjectRoot(), "_References", recipe.simulationId);
        var debugOutlineFolder = Path.Combine(recipe.outputFolder, "Debug", "Outlines");
        var debugModelFolder = Path.Combine(recipe.outputFolder, "Debug", "Models");
        Directory.CreateDirectory(debugOutlineFolder);
        Directory.CreateDirectory(debugModelFolder);

        foreach (var asset in recipe.referenceAssets ?? new List<PackRecipe.ReferenceAssetNeed>())
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.assetId)) continue;
            report.assetsProcessed++;

            var images = EnumerateAssetImages(simulationFolder, asset.assetId);
            if (images.Count == 0)
            {
                report.warnings.Add($"No images found for asset '{asset.assetId}'.");
                continue;
            }

            var best = OutlineExtraction.SelectBestTopdownImage(images);
            if (string.IsNullOrWhiteSpace(best) || !OutlineExtraction.TryExtract(best, out var outline))
            {
                report.warnings.Add($"{asset.assetId}: outline extraction failed.");
                continue;
            }

            report.imagesAnalyzed++;
            var mappedSpeciesId = MapAssetToSpeciesId(asset.assetId, library);
            var profile = library.profiles.FirstOrDefault(p => string.Equals(p.speciesId, mappedSpeciesId, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                report.warnings.Add($"No profile found for mapped species '{mappedSpeciesId}' from asset '{asset.assetId}'.");
                continue;
            }

            var fitted = AntTopdownFitter.TryFit(outline.mask, outline.width, outline.height, outline.report.bbox, out var model);
            profile.hasFittedModel = fitted;
            profile.fittedModel = fitted ? model : null;

            if (fitted)
            {
                model.sourceImagePath = best;
                model.sourceW = outline.width;
                model.sourceH = outline.height;
                profile.headScale = Mathf.Clamp(model.headRadii01.x / 0.08f, 0.6f, 1.8f);
                profile.thoraxScale = Mathf.Clamp(model.thoraxRadii01.x / 0.1f, 0.6f, 1.8f);
                profile.abdomenScale = Mathf.Clamp(model.abdomenRadii01.x / 0.15f, 0.6f, 1.9f);
                profile.petiolePinchStrength = Mathf.Clamp(0.8f + model.pinchStrength, 0.6f, 2.5f);
                profile.legLengthScale = Mathf.Clamp(model.legLengths01.Average() / 0.18f, 0.8f, 1.5f);
                profile.antennaLengthScale = Mathf.Clamp(model.antennaLen01 / 0.16f, 0.8f, 1.4f);
                report.profilesUpdated++;
            }
            else
            {
                report.warnings.Add($"{asset.assetId}: fitted model invalid, will use golden ant fallback.");
            }

            var debugPng = Path.Combine(debugOutlineFolder, $"{asset.assetId}_silhouette.png").Replace('\\', '/');
            OutlineExtraction.SaveMaskPng(debugPng, outline.mask, outline.width, outline.height);
            var modelJson = JsonUtility.ToJson(fitted ? model : new AntTopdownModel(), true);
            var refModelPath = Path.Combine(simulationFolder, asset.assetId, "ant_model.json");
            Directory.CreateDirectory(Path.GetDirectoryName(refModelPath) ?? string.Empty);
            File.WriteAllText(refModelPath, modelJson);
            File.WriteAllText(Path.Combine(debugModelFolder, $"{asset.assetId}_ant_model.json"), modelJson);

            Debug.Log($"[References] {asset.assetId}: fittedModel={(fitted ? "OK" : "FALLBACK")} (w={outline.width}, coverage={outline.report.coverage:F3}, pinch={(fitted ? model.pinchStrength : 0f):F2})");
            foreach (var warning in outline.report.warnings) report.warnings.Add($"{asset.assetId}: {warning}");
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }

    private static List<string> EnumerateAssetImages(string simulationFolder, string assetId)
    {
        var folder = Path.Combine(simulationFolder, assetId, "Images");
        if (!Directory.Exists(folder)) return new List<string>();
        return Directory.GetFiles(folder)
            .Where(path =>
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".webp";
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MapAssetToSpeciesId(string assetId, AntSpeciesLibrary library)
    {
        var normalized = assetId.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        if (normalized.Contains("fire")) return "solenopsis_like";
        if (normalized.Contains("carpenter")) return "camponotus_like";
        if (normalized.Contains("pharaoh")) return "lasius_niger_like";
        if (normalized.Contains("black")) return "lasius_niger_like";
        if (normalized.Contains("redwood")) return "formica_like";
        return library.profiles[0].speciesId;
    }
}
