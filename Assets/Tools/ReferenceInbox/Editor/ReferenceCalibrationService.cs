using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ReferenceCalibrationService
{
    public static ReferenceCalibrationReport Calibrate(PackRecipe recipe)
    {
        var combined = new ReferenceCalibrationReport { simulationId = recipe?.simulationId ?? "unknown" };
        if (recipe == null)
        {
            combined.warnings.Add("Recipe is null.");
            return combined;
        }

        var calibrators = TypeCache.GetTypesDerivedFrom<IReferenceCalibrator>()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Select(t => Activator.CreateInstance(t) as IReferenceCalibrator)
            .Where(c => c != null)
            .ToList();

        foreach (var calibrator in calibrators)
        {
            if (!calibrator.CanCalibrate(recipe)) continue;
            var report = calibrator.Calibrate(recipe);
            Merge(combined, report);
        }

        if (combined.assetsProcessed == 0 && combined.warnings.Count == 0)
        {
            combined.warnings.Add("No matching calibrator found for recipe.");
        }

        WriteReport(recipe, combined);
        return combined;
    }

    private static void Merge(ReferenceCalibrationReport target, ReferenceCalibrationReport source)
    {
        if (source == null) return;
        target.assetsProcessed += source.assetsProcessed;
        target.imagesAnalyzed += source.imagesAnalyzed;
        target.profilesUpdated += source.profilesUpdated;
        target.warnings.AddRange(source.warnings);
        target.assets.AddRange(source.assets);
    }

    private static void WriteReport(PackRecipe recipe, ReferenceCalibrationReport report)
    {
        var path = Path.Combine(recipe.outputFolder, "Debug", "ReferenceCalibrationReport.json").Replace('\\', '/');
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();
    }
}
