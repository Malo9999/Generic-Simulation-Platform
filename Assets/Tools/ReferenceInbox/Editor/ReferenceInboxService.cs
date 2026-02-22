using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class ReferenceInboxService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    public static ValidationSummary ValidateSimulation(string simulationName, string rootFolder, int minWidth)
    {
        return AnalyzeSimulation(simulationName, rootFolder, minWidth);
    }

    public static NormalizeSessionReport NormalizeSimulation(string simulationName, string rootFolder, int minWidth)
    {
        var summary = AnalyzeSimulation(simulationName, rootFolder, minWidth);
        var sessionReport = new NormalizeSessionReport
        {
            simulation = simulationName,
            rootFolder = rootFolder,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            assets = new List<NormalizeAssetReport>(),
            warnings = new List<string>(summary.warnings)
        };

        if (!summary.simulationFolderExists)
        {
            WriteSessionReport(rootFolder, simulationName, sessionReport);
            return sessionReport;
        }

        foreach (AssetAnalysis asset in summary.assets)
        {
            var manifest = new AssetManifest
            {
                simulation = simulationName,
                asset = asset.assetName,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                profile = new List<ManifestEntry>(),
                top = new List<ManifestEntry>(),
                warnings = new List<string>(asset.warnings)
            };

            if (asset.profileFolderMissing)
            {
                manifest.warnings.Add("Missing required profile folder (accepted names: Images, profile).");
            }

            var assetReport = new NormalizeAssetReport
            {
                asset = asset.assetName,
                profileRenamed = 0,
                topRenamed = 0,
                warnings = new List<string>()
            };

            ProcessCategory(asset.profileFiles, "profile", asset.profileFolderPath, manifest.profile, manifest.warnings, ref assetReport.profileRenamed);
            ProcessCategory(asset.topFiles, "top", asset.topFolderPath, manifest.top, manifest.warnings, ref assetReport.topRenamed);

            if (asset.profileFiles.Count == 0)
            {
                manifest.warnings.Add("No supported profile images found.");
            }

            if (asset.topFolderPath != null && asset.topFiles.Count == 0)
            {
                manifest.warnings.Add("No supported top images found.");
            }

            string manifestPath = Path.Combine(asset.assetFolderPath, "manifest.json");
            WriteJson(manifestPath, manifest);

            assetReport.manifestPath = manifestPath;
            assetReport.warnings = manifest.warnings;
            sessionReport.assets.Add(assetReport);
        }

        WriteSessionReport(rootFolder, simulationName, sessionReport);
        return sessionReport;
    }

    private static ValidationSummary AnalyzeSimulation(string simulationName, string rootFolder, int minWidth)
    {
        var summary = new ValidationSummary
        {
            simulation = simulationName,
            rootFolder = rootFolder,
            simulationFolderExists = false,
            assets = new List<AssetAnalysis>(),
            warnings = new List<string>()
        };

        string simulationPath = Path.Combine(rootFolder, simulationName);
        if (!Directory.Exists(simulationPath))
        {
            summary.warnings.Add($"Simulation folder not found: {simulationPath}");
            return summary;
        }

        summary.simulationFolderExists = true;

        string[] assetDirs = Directory.GetDirectories(simulationPath);
        foreach (string assetDir in assetDirs.OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var analysis = new AssetAnalysis
            {
                assetName = Path.GetFileName(assetDir),
                assetFolderPath = assetDir,
                profileFolderPath = ResolveProfileFolder(assetDir),
                topFolderPath = ResolveTopFolder(assetDir),
                profileFiles = new List<ImageFileInfo>(),
                topFiles = new List<ImageFileInfo>(),
                profileFolderMissing = false,
                warnings = new List<string>()
            };

            if (analysis.profileFolderPath == null)
            {
                analysis.profileFolderMissing = true;
                analysis.warnings.Add("Missing required profile folder (accepted names: Images, profile).");
            }
            else
            {
                analysis.profileFiles = CollectImages(analysis.profileFolderPath, minWidth, analysis.warnings, "profile");
                if (analysis.profileFiles.Count == 0)
                {
                    analysis.warnings.Add("No supported profile images found.");
                }
            }

            if (analysis.topFolderPath == null)
            {
                analysis.warnings.Add("Optional top folder not found (accepted names: Topview, topview, top).");
            }
            else
            {
                analysis.topFiles = CollectImages(analysis.topFolderPath, minWidth, analysis.warnings, "top");
                if (analysis.topFiles.Count == 0)
                {
                    analysis.warnings.Add("No supported top images found.");
                }
            }

            summary.assets.Add(analysis);
        }

        if (summary.assets.Count == 0)
        {
            summary.warnings.Add($"No assets found under simulation: {simulationPath}");
        }

        return summary;
    }

    private static string ResolveProfileFolder(string assetFolder)
    {
        string imagesPath = Path.Combine(assetFolder, "Images");
        if (Directory.Exists(imagesPath))
        {
            return imagesPath;
        }

        string profilePath = Path.Combine(assetFolder, "profile");
        if (Directory.Exists(profilePath))
        {
            return profilePath;
        }

        return null;
    }

    private static string ResolveTopFolder(string assetFolder)
    {
        string topviewAliasPath = Path.Combine(assetFolder, "Topview");
        if (Directory.Exists(topviewAliasPath))
        {
            return topviewAliasPath;
        }

        string topViewPath = Path.Combine(assetFolder, "topview");
        if (Directory.Exists(topViewPath))
        {
            return topViewPath;
        }

        string topPath = Path.Combine(assetFolder, "top");
        if (Directory.Exists(topPath))
        {
            return topPath;
        }

        return null;
    }

    private static List<ImageFileInfo> CollectImages(string folderPath, int minWidth, List<string> warnings, string category)
    {
        var files = new List<ImageFileInfo>();

        foreach (string filePath in Directory.GetFiles(folderPath))
        {
            string extension = Path.GetExtension(filePath);
            if (!SupportedExtensions.Contains(extension))
            {
                continue;
            }

            long bytes = new FileInfo(filePath).Length;
            int width = 0;
            int height = 0;

            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                var texture = new Texture2D(2, 2);

                if (texture.LoadImage(imageBytes, true))
                {
                    width = texture.width;
                    height = texture.height;
                }
                else
                {
                    warnings.Add($"{category}: Unable to decode image '{Path.GetFileName(filePath)}'.");
                }

                UnityEngine.Object.DestroyImmediate(texture);
            }
            catch (Exception ex)
            {
                warnings.Add($"{category}: Failed to inspect '{Path.GetFileName(filePath)}': {ex.Message}");
            }

            if (width > 0 && width < minWidth)
            {
                warnings.Add($"{category}: '{Path.GetFileName(filePath)}' width {width}px below minimum {minWidth}px.");
            }

            if (bytes < 25 * 1024)
            {
                warnings.Add($"{category}: '{Path.GetFileName(filePath)}' is {bytes} bytes (<25KB); may be thumbnail.");
            }

            files.Add(new ImageFileInfo
            {
                fullPath = filePath,
                fileName = Path.GetFileName(filePath),
                extension = extension,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath).ToString("o"),
                width = width,
                height = height
            });
        }

        return files
            .OrderBy(file => DateTime.Parse(file.lastWriteTimeUtc))
            .ThenBy(file => file.fileName, StringComparer.Ordinal)
            .ToList();
    }

    private static void ProcessCategory(
        List<ImageFileInfo> sortedFiles,
        string prefix,
        string folderPath,
        List<ManifestEntry> outputEntries,
        List<string> warnings,
        ref int renamedCount)
    {
        if (folderPath == null || sortedFiles.Count == 0)
        {
            return;
        }

        bool alreadyNormalized = sortedFiles
            .Select((file, index) => string.Equals(file.fileName, BuildFinalName(prefix, index + 1, file.extension), StringComparison.Ordinal))
            .All(isMatch => isMatch);

        if (!alreadyNormalized)
        {
            var tempInfos = new List<TempRenameInfo>(sortedFiles.Count);
            for (int i = 0; i < sortedFiles.Count; i++)
            {
                ImageFileInfo file = sortedFiles[i];
                string tempName = $"__tmp_{Guid.NewGuid():N}_{i + 1:D3}{file.extension.ToLowerInvariant()}";
                string tempPath = Path.Combine(folderPath, tempName);
                File.Move(file.fullPath, tempPath);

                tempInfos.Add(new TempRenameInfo
                {
                    tempPath = tempPath,
                    originalName = file.fileName,
                    extension = file.extension,
                    width = file.width,
                    height = file.height
                });
            }

            for (int i = 0; i < tempInfos.Count; i++)
            {
                TempRenameInfo temp = tempInfos[i];
                string finalName = BuildFinalName(prefix, i + 1, temp.extension);
                string finalPath = Path.Combine(folderPath, finalName);
                File.Move(temp.tempPath, finalPath);

                outputEntries.Add(new ManifestEntry
                {
                    from = temp.originalName,
                    to = finalName,
                    width = temp.width,
                    height = temp.height
                });
            }

            renamedCount += sortedFiles.Count;
            return;
        }

        for (int i = 0; i < sortedFiles.Count; i++)
        {
            ImageFileInfo file = sortedFiles[i];
            outputEntries.Add(new ManifestEntry
            {
                from = file.fileName,
                to = BuildFinalName(prefix, i + 1, file.extension),
                width = file.width,
                height = file.height
            });
        }

        warnings.Add($"{prefix}: Already normalized; no rename required.");
    }

    private static string BuildFinalName(string prefix, int index, string extension)
    {
        return $"{prefix}_{index:D3}{extension.ToLowerInvariant()}";
    }

    private static void WriteSessionReport(string rootFolder, string simulationName, NormalizeSessionReport report)
    {
        string simulationPath = Path.Combine(rootFolder, simulationName);
        Directory.CreateDirectory(simulationPath);

        string reportPath = Path.Combine(simulationPath, "_normalize_report.json");
        WriteJson(reportPath, report);
    }

    private static void WriteJson<T>(string path, T value)
    {
        string json = JsonUtility.ToJson(value, true);
        File.WriteAllText(path, json);
    }

    [Serializable]
    public class ValidationSummary
    {
        public string simulation;
        public string rootFolder;
        public bool simulationFolderExists;
        public List<AssetAnalysis> assets;
        public List<string> warnings;
    }

    [Serializable]
    public class AssetAnalysis
    {
        public string assetName;
        public string assetFolderPath;
        public string profileFolderPath;
        public string topFolderPath;
        public bool profileFolderMissing;
        public List<ImageFileInfo> profileFiles;
        public List<ImageFileInfo> topFiles;
        public List<string> warnings;
    }

    [Serializable]
    public class ImageFileInfo
    {
        public string fullPath;
        public string fileName;
        public string extension;
        public string lastWriteTimeUtc;
        public int width;
        public int height;
    }

    [Serializable]
    public class AssetManifest
    {
        public string simulation;
        public string asset;
        public string timestampUtc;
        public List<ManifestEntry> profile;
        public List<ManifestEntry> top;
        public List<string> warnings;
    }

    [Serializable]
    public class ManifestEntry
    {
        public string from;
        public string to;
        public int width;
        public int height;
    }

    [Serializable]
    private class TempRenameInfo
    {
        public string tempPath;
        public string originalName;
        public string extension;
        public int width;
        public int height;
    }

    [Serializable]
    public class NormalizeSessionReport
    {
        public string simulation;
        public string rootFolder;
        public string timestampUtc;
        public List<NormalizeAssetReport> assets;
        public List<string> warnings;
    }

    [Serializable]
    public class NormalizeAssetReport
    {
        public string asset;
        public int profileRenamed;
        public int topRenamed;
        public string manifestPath;
        public List<string> warnings;
    }
}
