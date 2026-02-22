using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AntReferenceCalibrator : IReferenceCalibrator
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Ant/AntSpeciesLibrary.asset";

    public string CalibratorId => "calibrator.ant.v1";

    public bool CanCalibrate(PackRecipe recipe)
    {
        if (recipe == null)
        {
            return false;
        }

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
        if (!Directory.Exists(simulationFolder))
        {
            report.warnings.Add($"Reference folder not found: {simulationFolder}");
            return report;
        }

        var assets = recipe.referenceAssets ?? new List<PackRecipe.ReferenceAssetNeed>();
        foreach (var asset in assets)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.assetId))
            {
                continue;
            }

            report.assetsProcessed++;
            var imagePaths = EnumerateAssetImages(simulationFolder, asset.assetId, report.warnings);
            if (imagePaths.Count == 0)
            {
                report.warnings.Add($"No images found for asset '{asset.assetId}'.");
                continue;
            }

            var samples = new List<SampleTraits>();
            foreach (var imagePath in imagePaths)
            {
                if (TryAnalyzeImage(imagePath, out var traits))
                {
                    samples.Add(traits);
                    report.imagesAnalyzed++;
                }
            }

            if (samples.Count == 0)
            {
                report.warnings.Add($"No analyzable images found for asset '{asset.assetId}'.");
                continue;
            }

            var mappedSpeciesId = MapAssetToSpeciesId(asset.assetId, library);
            var profile = library.profiles.FirstOrDefault(p => string.Equals(p.speciesId, mappedSpeciesId, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                report.warnings.Add($"No profile found for mapped species '{mappedSpeciesId}' from asset '{asset.assetId}'.");
                continue;
            }

            ApplyTraits(profile, samples);
            report.profilesUpdated++;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return report;
    }

    private static List<string> EnumerateAssetImages(string simulationFolder, string assetId, List<string> warnings)
    {
        var assetFolder = Path.Combine(simulationFolder, assetId);
        var manifestPath = Path.Combine(assetFolder, "manifest.json");
        var output = new List<string>();

        if (File.Exists(manifestPath))
        {
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<ReferenceInboxService.AssetManifest>(manifestJson);
            if (manifest?.profile != null)
            {
                foreach (var entry in manifest.profile)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.to))
                    {
                        continue;
                    }

                    var candidate = Path.Combine(assetFolder, "Images", entry.to);
                    if (File.Exists(candidate))
                    {
                        output.Add(candidate);
                    }
                }
            }

            if (manifest?.top != null)
            {
                foreach (var entry in manifest.top)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.to))
                    {
                        continue;
                    }

                    var candidate = Path.Combine(assetFolder, "Topview", entry.to);
                    if (File.Exists(candidate))
                    {
                        output.Add(candidate);
                    }
                }
            }

            if (output.Count > 0)
            {
                return output;
            }
        }

        output.AddRange(EnumerateImageFiles(Path.Combine(assetFolder, "Images")));
        output.AddRange(EnumerateImageFiles(Path.Combine(assetFolder, "Topview")));

        if (output.Count == 0)
        {
            warnings.Add($"Asset '{assetId}' has no manifest entries or direct Images/Topview files.");
        }

        return output;
    }

    private static IEnumerable<string> EnumerateImageFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        var files = Directory.GetFiles(folder);
        for (var i = 0; i < files.Length; i++)
        {
            var ext = Path.GetExtension(files[i]).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
            {
                yield return files[i];
            }
        }
    }

    private static bool TryAnalyzeImage(string imagePath, out SampleTraits traits)
    {
        traits = default;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(imagePath);
        }
        catch
        {
            return false;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes, false))
        {
            UnityEngine.Object.DestroyImmediate(tex);
            return false;
        }

        var pixels = tex.GetPixels32();
        if (pixels == null || pixels.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(tex);
            return false;
        }

        var width = tex.width;
        var height = tex.height;
        var brightnessSum = 0f;
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            brightnessSum += (c.r + c.g + c.b) / (3f * 255f);
        }

        var avgBrightness = brightnessSum / pixels.Length;
        var threshold = Mathf.Clamp01(avgBrightness * 0.85f);

        var minX = width;
        var maxX = 0;
        var minY = height;
        var maxY = 0;
        var bodyCount = 0;
        var hueSum = 0f;
        var hueCount = 0;
        var rowMass = new int[height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = pixels[y * width + x];
                var lum = (c.r + c.g + c.b) / (3f * 255f);
                if (lum >= threshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                bodyCount++;
                rowMass[y]++;

                Color.RGBToHSV(new Color32(c.r, c.g, c.b, 255), out var hue, out var sat, out _);
                if (sat > 0.2f)
                {
                    hueSum += hue;
                    hueCount++;
                }
            }
        }

        UnityEngine.Object.DestroyImmediate(tex);

        if (bodyCount <= 0 || maxX <= minX || maxY <= minY)
        {
            return false;
        }

        var bboxWidth = maxX - minX + 1f;
        var bboxHeight = maxY - minY + 1f;
        var thirds = new float[3];

        for (var y = minY; y <= maxY; y++)
        {
            var mass = rowMass[y];
            if (mass <= 0)
            {
                continue;
            }

            var segmentT = Mathf.InverseLerp(minY, maxY, y);
            var idx = Mathf.Clamp(Mathf.FloorToInt(segmentT * 3f), 0, 2);
            thirds[idx] += mass;
        }

        var sumThirds = Mathf.Max(1f, thirds[0] + thirds[1] + thirds[2]);
        var headRatio = Mathf.Max(0.2f, thirds[0] / sumThirds * 3f);
        var thoraxRatio = Mathf.Max(0.2f, thirds[1] / sumThirds * 3f);
        var abdomenRatio = Mathf.Max(0.2f, thirds[2] / sumThirds * 3f);

        var minRow = int.MaxValue;
        var maxRow = 0;
        for (var y = minY; y <= maxY; y++)
        {
            minRow = Mathf.Min(minRow, rowMass[y]);
            maxRow = Mathf.Max(maxRow, rowMass[y]);
        }

        var pinch = maxRow <= 0 ? 1f : Mathf.Clamp(1f + (1f - (minRow / (float)maxRow)) * 1.5f, 0.6f, 2.5f);
        var hueMean = hueCount > 0 ? hueSum / hueCount : -1f;

        traits = new SampleTraits
        {
            headScale = headRatio,
            thoraxScale = thoraxRatio,
            abdomenScale = abdomenRatio,
            petiolePinchStrength = pinch,
            aspectRatio = bboxWidth / Mathf.Max(1f, bboxHeight),
            hue = hueMean
        };

        return true;
    }

    private static void ApplyTraits(AntSpeciesProfile profile, List<SampleTraits> samples)
    {
        var avgHead = samples.Average(s => s.headScale);
        var avgThorax = samples.Average(s => s.thoraxScale);
        var avgAbdomen = samples.Average(s => s.abdomenScale);
        var avgPinch = samples.Average(s => s.petiolePinchStrength);
        var avgAspect = samples.Average(s => s.aspectRatio);
        var avgHue = samples.Where(s => s.hue >= 0f).Select(s => s.hue).DefaultIfEmpty(-1f).Average();

        profile.headScale = Mathf.Clamp(avgHead, 0.6f, 1.8f);
        profile.thoraxScale = Mathf.Clamp(avgThorax, 0.6f, 1.8f);
        profile.abdomenScale = Mathf.Clamp(avgAbdomen, 0.6f, 1.9f);
        profile.petiolePinchStrength = Mathf.Clamp(avgPinch, 0.6f, 2.5f);

        // Use aspect ratio to gently adapt appendages.
        profile.legLengthScale = Mathf.Clamp(0.8f + avgAspect * 0.25f, 0.8f, 1.5f);
        profile.antennaLengthScale = Mathf.Clamp(0.9f + avgAspect * 0.15f, 0.8f, 1.4f);

        var hinted = HueToColorFamily(avgHue);
        if (!string.IsNullOrWhiteSpace(hinted))
        {
            profile.baseColorId = hinted;
        }
    }

    private static string MapAssetToSpeciesId(string assetId, AntSpeciesLibrary library)
    {
        var normalized = assetId.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        if (normalized.Contains("fire")) return "solenopsis_like";
        if (normalized.Contains("carpenter")) return "camponotus_like";
        if (normalized.Contains("pharaoh")) return "lasius_niger_like";

        // fallback: try a direct fuzzy match to species id/display name
        var fallback = library.profiles.FirstOrDefault(profile =>
            normalized.Contains(profile.speciesId.Replace("_", string.Empty).ToLowerInvariant()) ||
            (!string.IsNullOrWhiteSpace(profile.displayName) && normalized.Contains(profile.displayName.Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant())));

        return fallback?.speciesId ?? library.profiles[0].speciesId;
    }

    private static string HueToColorFamily(double hue)
    {
        if (hue < 0) return null;
        if (hue < 0.08 || hue > 0.93) return "red";
        if (hue < 0.16) return "orange";
        if (hue < 0.22) return "yellow";
        if (hue < 0.45) return "green";
        if (hue < 0.7) return "blue";
        return "brown";
    }

    private struct SampleTraits
    {
        public float headScale;
        public float thoraxScale;
        public float abdomenScale;
        public float petiolePinchStrength;
        public float aspectRatio;
        public float hue;
    }
}
