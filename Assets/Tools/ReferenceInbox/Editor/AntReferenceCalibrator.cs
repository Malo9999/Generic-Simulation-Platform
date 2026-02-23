using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AntReferenceCalibrator : IReferenceCalibrator
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Ant/AntSpeciesLibrary.asset";

    public string CalibratorId => "calibrator.ant.v3";

    public bool CanCalibrate(PackRecipe recipe)
    {
        if (recipe == null) return false;
        return string.Equals(recipe.simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase)
               || recipe.entities.Any(entity => string.Equals(entity.archetypeId, "archetype.ant", StringComparison.OrdinalIgnoreCase));
    }

    public ReferenceCalibrationReport Calibrate(PackRecipe recipe)
    {
        var report = new ReferenceCalibrationReport { simulationId = recipe?.simulationId ?? "unknown" };
        if (recipe == null) return report;

        var library = AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath);
        if (library == null)
        {
            report.warnings.Add($"Ant species library not found at '{LibraryPath}'.");
            return report;
        }

        var simulationFolder = Path.Combine(ReferenceInboxScaffolder.ProjectRoot(), "_References", recipe.simulationId);
        var debugModelFolder = Path.Combine(recipe.outputFolder, "Debug", "Models");
        Directory.CreateDirectory(debugModelFolder);

        foreach (var asset in recipe.referenceAssets ?? new List<PackRecipe.ReferenceAssetNeed>())
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.assetId)) continue;
            if (!string.Equals(asset.entityId, "ant", StringComparison.OrdinalIgnoreCase)) continue;
            report.assetsProcessed++;

            if (asset.generationMode == PackRecipe.GenerationMode.Procedural)
            {
                report.assets.Add(new ReferenceCalibrationReport.AssetSummary
                {
                    calibratorId = CalibratorId,
                    assetId = asset.assetId,
                    mappedSpeciesId = ResolveSpeciesId(asset, library)
                });
                continue;
            }

            var images = EnumerateAssetImages(simulationFolder, asset.assetId);
            OutlineExtraction.OutlineResult bestOutline = null;
            string bestPath = null;
            var bestScore = float.MinValue;
            foreach (var image in images)
            {
                if (!OutlineExtraction.TryExtract(image, out var outline)) continue;
                report.imagesAnalyzed++;
                var score = OutlineExtraction.Score(outline);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOutline = outline;
                    bestPath = image;
                }
            }

            if (bestOutline == null)
            {
                report.warnings.Add($"{asset.assetId}: outline extraction failed.");
                continue;
            }

            var mappedSpeciesId = ResolveSpeciesId(asset, library);
            var profile = GetOrCreateProfile(library, mappedSpeciesId);

            var fitted = AntTopdownFitter.TryFit(bestOutline.mask, bestOutline.width, bestOutline.height, bestOutline.report.bbox, out var model);
            profile.hasFittedModel = fitted;
            profile.fittedModel = fitted ? model : null;
            if (fitted)
            {
                model.sourceImagePath = bestPath;
                model.sourceW = bestOutline.width;
                model.sourceH = bestOutline.height;
                report.profilesUpdated++;
            }

            var modelJson = JsonUtility.ToJson(fitted ? model : new AntTopdownModel(), true);
            File.WriteAllText(Path.Combine(debugModelFolder, $"{asset.assetId}_ant_model.json"), modelJson);

            report.assets.Add(new ReferenceCalibrationReport.AssetSummary
            {
                calibratorId = CalibratorId,
                assetId = asset.assetId,
                mappedSpeciesId = mappedSpeciesId,
                bestImagePath = bestPath,
                score = bestScore,
                fragmentCount = bestOutline.report.fragmentCount,
                warnings = new List<string>(bestOutline.report.warnings)
            });
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return report;
    }

    private static string ResolveSpeciesId(PackRecipe.ReferenceAssetNeed asset, AntSpeciesLibrary library)
    {
        if (!string.IsNullOrWhiteSpace(asset.mappedSpeciesId)) return asset.mappedSpeciesId;
        var fallback = library.profiles.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.speciesId));
        return fallback?.speciesId ?? "default";
    }

    private static AntSpeciesProfile GetOrCreateProfile(AntSpeciesLibrary library, string speciesId)
    {
        var existing = library.profiles.FirstOrDefault(p => string.Equals(p.speciesId, speciesId, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        var defaultProfile = library.profiles.FirstOrDefault(p => string.Equals(p.speciesId, "default", StringComparison.OrdinalIgnoreCase));
        var profile = defaultProfile != null ? CloneProfile(defaultProfile) : BuildReasonableDefault();
        profile.speciesId = speciesId;
        if (string.IsNullOrWhiteSpace(profile.displayName))
        {
            profile.displayName = speciesId;
        }

        library.profiles.Add(profile);
        return profile;
    }

    private static AntSpeciesProfile CloneProfile(AntSpeciesProfile source)
    {
        return new AntSpeciesProfile
        {
            speciesId = source.speciesId,
            displayName = source.displayName,
            baseColorId = source.baseColorId,
            headScale = source.headScale,
            thoraxScale = source.thoraxScale,
            abdomenScale = source.abdomenScale,
            legLengthScale = source.legLengthScale,
            antennaLengthScale = source.antennaLengthScale,
            petiolePinchStrength = source.petiolePinchStrength,
            soldierHeadMultiplier = source.soldierHeadMultiplier,
            soldierMandibleMultiplier = source.soldierMandibleMultiplier,
            queenAbdomenMultiplier = source.queenAbdomenMultiplier,
            referencePack = source.referencePack,
            hasFittedModel = source.hasFittedModel,
            fittedModel = source.fittedModel
        };
    }

    private static AntSpeciesProfile BuildReasonableDefault()
    {
        return new AntSpeciesProfile
        {
            speciesId = "default",
            displayName = "Default",
            baseColorId = "color.ant.default",
            headScale = 1f,
            thoraxScale = 1f,
            abdomenScale = 1f,
            legLengthScale = 1f,
            antennaLengthScale = 1f,
            petiolePinchStrength = 1f,
            soldierHeadMultiplier = 1.2f,
            soldierMandibleMultiplier = 1.2f,
            queenAbdomenMultiplier = 1.3f,
            hasFittedModel = false,
            fittedModel = null
        };
    }

    private static List<string> EnumerateAssetImages(string simulationFolder, string assetId)
    {
        var folder = Path.Combine(simulationFolder, assetId, "Images");
        if (!Directory.Exists(folder)) return new List<string>();
        return Directory.GetFiles(folder).Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant())).ToList();
    }
}
