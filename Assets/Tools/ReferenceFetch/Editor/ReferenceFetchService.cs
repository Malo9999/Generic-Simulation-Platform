using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed class ReferenceFetchService
{
    private readonly WikimediaClient wikimediaClient = new();
    private static readonly Dictionary<string, string[]> QueryVariantsByAsset = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CarpenterAnt"] = new[] { "carpenter ant", "Camponotus", "Camponotus pennsylvanicus", "black carpenter ant" },
        ["FireAnt"] = new[] { "fire ant", "Solenopsis invicta", "red imported fire ant" },
        ["PharaohAnt"] = new[] { "Pharaoh ant", "Monomorium pharaonis" },
    };

    public void Fetch(ReferenceFetchRequest request)
    {
        var simulationFolder = Path.Combine(ProjectRoot(), "_References", Sanitize(request.SimulationName));
        Directory.CreateDirectory(simulationFolder);

        foreach (var asset in request.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            var assetName = asset.Trim();
            var assetFolder = Path.Combine(simulationFolder, Sanitize(assetName));
            Directory.CreateDirectory(assetFolder);

            var metadataPath = Path.Combine(assetFolder, "meta.jsonl");
            var knownHashes = LoadKnownHashes(metadataPath);
            var mergedStats = new ReferenceCandidateStats();
            var candidates = new List<ReferenceCandidate>();
            var seenFileUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var query in BuildQueries(request.SimulationName, assetName))
            {
                var needed = request.ImagesPerAsset - candidates.Count;
                if (needed <= 0)
                {
                    break;
                }

                var queryResult = wikimediaClient.Search(query, Math.Max(needed * 4, request.ImagesPerAsset), request.MinWidth, request.LicenseFilter);
                mergedStats.Merge(queryResult.Stats);
                foreach (var candidate in queryResult.Candidates)
                {
                    if (seenFileUrls.Add(candidate.FileUrl))
                    {
                        candidates.Add(candidate);
                    }

                    if (candidates.Count >= request.ImagesPerAsset)
                    {
                        break;
                    }
                }

                if (candidates.Count >= request.ImagesPerAsset)
                {
                    break;
                }
            }

            var downloaded = 0;
            foreach (var candidate in candidates)
            {
                if (downloaded >= request.ImagesPerAsset)
                {
                    break;
                }

                if (request.DryRun)
                {
                    Debug.Log($"[ReferenceFetch][DryRun] {assetName}: {candidate.FileUrl} [{candidate.Mime}] {candidate.Width}x{candidate.Height} ({candidate.License})");
                    downloaded++;
                    continue;
                }

                try
                {
                    var bytes = DownloadBytes(candidate.FileUrl);
                    var hash = ComputeSha256(bytes);
                    if (knownHashes.Contains(hash))
                    {
                        continue;
                    }

                    var extension = string.IsNullOrWhiteSpace(candidate.FileExtension) ? GetExtension(candidate.FileUrl) : candidate.FileExtension;
                    var imagePath = Path.Combine(assetFolder, NextImageFileName(assetFolder, extension));
                    File.WriteAllBytes(imagePath, bytes);

                    var row = new ReferenceMetadataRow
                    {
                        AssetName = assetName,
                        Source = candidate.Source,
                        PageUrl = candidate.PageUrl,
                        FileUrl = candidate.FileUrl,
                        Mime = candidate.Mime,
                        FileExtension = candidate.FileExtension,
                        License = candidate.License,
                        Author = candidate.Author,
                        Attribution = candidate.Attribution,
                        Width = candidate.Width,
                        Height = candidate.Height,
                        Sha256 = hash,
                    };

                    File.AppendAllText(metadataPath, JsonConvert.SerializeObject(row) + Environment.NewLine, Encoding.UTF8);
                    knownHashes.Add(hash);
                    downloaded++;
                    System.Threading.Thread.Sleep(600);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ReferenceFetch] Failed to download '{candidate.FileUrl}': {ex.Message}");
                }
            }

            Debug.Log($"[ReferenceFetch] Asset '{assetName}' stats: foundRawCandidates={mergedStats.FoundRawCandidates}, rejectedByLicense={mergedStats.RejectedByLicense}, rejectedByFileType={mergedStats.RejectedByFileType}, rejectedByResolution={mergedStats.RejectedByResolution}, accepted={mergedStats.Accepted}");

            if (request.DryRun && candidates.Count > 0)
            {
                var count = Math.Min(request.ImagesPerAsset, candidates.Count);
                for (var i = 0; i < count; i++)
                {
                    var top = candidates[i];
                    Debug.Log($"[ReferenceFetch][DryRun][Accepted] {assetName} #{i + 1}: {top.FileUrl} [{top.Mime}] {top.Width}x{top.Height} ({top.License})");
                }
            }

            Debug.Log($"[ReferenceFetch] Asset '{assetName}' downloaded {downloaded} image(s) to {assetFolder}");
        }
    }

    private static IEnumerable<string> BuildQueries(string simulationName, string asset)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (QueryVariantsByAsset.TryGetValue(asset, out var mappedVariants))
        {
            foreach (var variant in mappedVariants)
            {
                var query = $"{variant} {simulationName}".Trim();
                if (seen.Add(query))
                {
                    yield return query;
                }
            }
        }

        var humanized = Regex.Replace(asset, "([a-z])([A-Z])", "$1 $2");
        var fallbackQuery = $"{humanized} {simulationName}".Trim();
        if (seen.Add(fallbackQuery))
        {
            yield return fallbackQuery;
        }
    }

    private static byte[] DownloadBytes(string url)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "GenericSimulationPlatformReferenceFetcher/1.0");
        return client.GetByteArrayAsync(url).GetAwaiter().GetResult();
    }

    private static string NextImageFileName(string assetFolder, string extension)
    {
        var files = Directory.GetFiles(assetFolder, "img_*.*", SearchOption.TopDirectoryOnly);
        var max = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length < 7 || !int.TryParse(name.Substring(4), out var value))
            {
                continue;
            }

            if (value > max)
            {
                max = value;
            }
        }

        return $"img_{max + 1:000}{extension}";
    }

    private static HashSet<string> LoadKnownHashes(string metadataPath)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(metadataPath))
        {
            return hashes;
        }

        foreach (var line in File.ReadLines(metadataPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var obj = JObject.Parse(line);
                var hash = obj.Value<string>("sha256");
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    hashes.Add(hash);
                }
            }
            catch
            {
                // Ignore invalid legacy lines.
            }
        }

        return hashes;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(bytes);
        return BytesToHexLower(hashBytes);
    }

    private static string BytesToHexLower(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
        }

        return sb.ToString();
    }

    private static string GetExtension(string fileUrl)
    {
        var uri = new Uri(fileUrl);
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5)
        {
            return string.Empty;
        }

        return ext;
    }

    private static string ProjectRoot()
    {
        var assetsPath = Application.dataPath;
        return Directory.GetParent(assetsPath)?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Trim();
    }
}

internal sealed class ReferenceFetchRequest
{
    public string SimulationName;
    public List<string> Assets;
    public int ImagesPerAsset;
    public int MinWidth;
    public bool DryRun;
    public LicenseFilter LicenseFilter;
}

internal sealed class LicenseFilter
{
    public bool AllowCc0;
    public bool AllowPublicDomain;
    public bool AllowCcBy;
    public bool AllowCcBySa;

    public bool IsAllowed(string rawLicense)
    {
        var normalized = (rawLicense ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (AllowCc0 && normalized.Contains("cc0"))
        {
            return true;
        }

        if (AllowPublicDomain && (normalized.Contains("public domain") || normalized.StartsWith("pd")))
        {
            return true;
        }

        if (AllowCcBySa && normalized.Contains("cc-by-sa"))
        {
            return true;
        }

        if (AllowCcBy && normalized.Contains("cc-by") && !normalized.Contains("cc-by-sa"))
        {
            return true;
        }

        return false;
    }
}

internal sealed class ReferenceCandidate
{
    public string Source;
    public string PageUrl;
    public string FileUrl;
    public string Mime;
    public string FileExtension;
    public string License;
    public string Author;
    public string Attribution;
    public int Width;
    public int Height;
}

internal sealed class ReferenceCandidateStats
{
    public int FoundRawCandidates;
    public int RejectedByLicense;
    public int RejectedByFileType;
    public int RejectedByResolution;
    public int Accepted;

    public void Merge(ReferenceCandidateStats other)
    {
        FoundRawCandidates += other.FoundRawCandidates;
        RejectedByLicense += other.RejectedByLicense;
        RejectedByFileType += other.RejectedByFileType;
        RejectedByResolution += other.RejectedByResolution;
        Accepted += other.Accepted;
    }
}

internal sealed class WikimediaSearchResult
{
    public readonly List<ReferenceCandidate> Candidates = new();
    public readonly ReferenceCandidateStats Stats = new();
}

internal sealed class ReferenceMetadataRow
{
    [JsonProperty("assetName")]
    public string AssetName;

    [JsonProperty("source")]
    public string Source;

    [JsonProperty("pageUrl")]
    public string PageUrl;

    [JsonProperty("fileUrl")]
    public string FileUrl;

    [JsonProperty("mime")]
    public string Mime;

    [JsonProperty("fileExtension")]
    public string FileExtension;

    [JsonProperty("license")]
    public string License;

    [JsonProperty("author")]
    public string Author;

    [JsonProperty("attribution")]
    public string Attribution;

    [JsonProperty("width")]
    public int Width;

    [JsonProperty("height")]
    public int Height;

    [JsonProperty("sha256")]
    public string Sha256;
}
