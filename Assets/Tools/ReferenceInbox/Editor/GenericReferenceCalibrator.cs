using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class GenericReferenceCalibrator : IReferenceCalibrator
{
    public string CalibratorId => "calibrator.generic.v1";

    public bool CanCalibrate(PackRecipe recipe) => recipe != null;

    public ReferenceCalibrationReport Calibrate(PackRecipe recipe)
    {
        var report = new ReferenceCalibrationReport { simulationId = recipe.simulationId };
        var simulationFolder = Path.Combine(ReferenceInboxScaffolder.ProjectRoot(), "_References", recipe.simulationId);
        var debugOutlineFolder = Path.Combine(recipe.outputFolder, "Debug", "Outlines");
        Directory.CreateDirectory(debugOutlineFolder);

        foreach (var asset in recipe.referenceAssets ?? new List<PackRecipe.ReferenceAssetNeed>())
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.assetId)) continue;
            report.assetsProcessed++;
            var summary = new ReferenceCalibrationReport.AssetSummary
            {
                calibratorId = CalibratorId,
                assetId = asset.assetId,
                mappedSpeciesId = asset.mappedSpeciesId
            };

            var images = EnumerateAssetImages(simulationFolder, asset.assetId);
            if (images.Count < Math.Max(1, asset.minImages))
            {
                var warning = $"{asset.assetId}: found {images.Count} image(s), minImages={asset.minImages}";
                report.warnings.Add(warning);
                summary.warnings.Add(warning);
            }

            var bestScore = float.MinValue;
            OutlineExtraction.OutlineResult bestOutline = null;
            string bestPath = null;
            foreach (var image in images)
            {
                if (!OutlineExtraction.TryExtract(image, out var outline)) continue;
                var score = OutlineExtraction.Score(outline);
                report.imagesAnalyzed++;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOutline = outline;
                    bestPath = image;
                }
            }

            if (bestOutline == null)
            {
                var warning = $"{asset.assetId}: no valid outline extracted.";
                report.warnings.Add(warning);
                summary.warnings.Add(warning);
                report.assets.Add(summary);
                continue;
            }

            var silhouettePath = Path.Combine(debugOutlineFolder, $"{asset.assetId}_best.png").Replace('\\', '/');
            OutlineExtraction.SaveMaskPng(silhouettePath, bestOutline.mask, bestOutline.width, bestOutline.height);
            summary.bestImagePath = bestPath;
            summary.silhouettePath = silhouettePath;
            summary.score = bestScore;
            summary.fragmentCount = bestOutline.report.fragmentCount;
            summary.warnings.AddRange(bestOutline.report.warnings);
            report.assets.Add(summary);
        }

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
}
