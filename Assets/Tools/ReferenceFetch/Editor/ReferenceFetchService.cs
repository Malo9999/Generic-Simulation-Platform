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
        ["LionMale"] = new[] { "male lion", "Panthera leo male", "lion male" },
        ["LionFemale"] = new[] { "lioness", "Panthera leo female", "lion female" },
        ["LionCub"] = new[] { "lion cub", "Panthera leo cub" },
    };

    public void Fetch(ReferenceFetchRequest request)
    {
        var simulationFolder = Path.Combine(ProjectRoot(), "_References", Sanitize(request.SimulationName));
        Directory.CreateDirectory(simulationFolder);
        var fetchReport = new ReferenceFetchReport
        {
            SimulationName = request.SimulationName,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            DryRun = request.DryRun,
        };

        foreach (var asset in request.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            var assetName = asset.Trim();
            var assetFolder = Path.Combine(simulationFolder, Sanitize(assetName));
            var imagesFolder = Path.Combine(assetFolder, "Images");
            Directory.CreateDirectory(assetFolder);
            Directory.CreateDirectory(imagesFolder);

            var metadataPath = Path.Combine(imagesFolder, "meta.jsonl");
            EnsureFileExists(metadataPath);
            var knownHashes = LoadKnownHashes(metadataPath);
            var mergedStats = new ReferenceCandidateStats();
            var candidates = new List<ReferenceCandidate>();
            var seenFileUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queryReports = new List<QueryAttemptReport>();
            string successfulQuery = null;

            foreach (var query in ExpandQueries(assetName))
            {
                var needed = request.ImagesPerAsset - candidates.Count;
                if (needed <= 0)
                {
                    break;
                }

                var queryResult = wikimediaClient.Search(query, Math.Max(needed * 4, request.ImagesPerAsset), request.MinWidth, request.LicenseFilter);
                mergedStats.Merge(queryResult.Stats);
                queryReports.Add(new QueryAttemptReport
                {
                    Query = query,
                    FoundRawCandidates = queryResult.Stats.FoundRawCandidates,
                    RejectedByLicense = queryResult.Stats.RejectedByLicense,
                    RejectedByFileType = queryResult.Stats.RejectedByFileType,
                    RejectedByResolution = queryResult.Stats.RejectedByResolution,
                    Accepted = queryResult.Stats.Accepted,
                });

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

                if (queryResult.Candidates.Count > 0 && string.IsNullOrWhiteSpace(successfulQuery))
                {
                    successfulQuery = query;
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
                    var imagePath = Path.Combine(imagesFolder, NextImageFileName(imagesFolder, extension));
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

            LogQueryDebug(assetName, queryReports, successfulQuery, candidates);
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

            Debug.Log($"[ReferenceFetch] Asset '{assetName}' downloaded {downloaded} image(s) to {imagesFolder}");

            fetchReport.Assets.Add(new AssetFetchReport
            {
                AssetName = assetName,
                SuccessfulQuery = successfulQuery ?? string.Empty,
                Downloaded = downloaded,
                QueryAttempts = queryReports,
                Stats = mergedStats,
            });
        }

        var fetchReportPath = Path.Combine(simulationFolder, "fetch_report.json");
        File.WriteAllText(fetchReportPath, JsonConvert.SerializeObject(fetchReport, Formatting.Indented) + Environment.NewLine, Encoding.UTF8);

        if (request.DryRun)
        {
            var dryRunPath = Path.Combine(simulationFolder, "dryrun_report.json");
            File.WriteAllText(dryRunPath, JsonConvert.SerializeObject(fetchReport, Formatting.Indented) + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static IEnumerable<string> ExpandQueries(string assetKey)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (QueryVariantsByAsset.TryGetValue(assetKey, out var mappedVariants))
        {
            foreach (var variant in mappedVariants)
            {
                if (seen.Add(variant))
                {
                    yield return variant;
                }
            }
        }

        var humanized = Regex.Replace(assetKey, "([a-z])([A-Z])", "$1 $2");
        if (!string.Equals(humanized, assetKey, StringComparison.Ordinal) && seen.Add(humanized))
        {
            yield return humanized;
        }

        if (seen.Add(assetKey))
        {
            yield return assetKey;
        }
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, string.Empty, Encoding.UTF8);
        }
    }

    private static void LogQueryDebug(string assetName, List<QueryAttemptReport> queryReports, string successfulQuery, List<ReferenceCandidate> candidates)
    {
        var winner = string.IsNullOrWhiteSpace(successfulQuery) ? "none" : successfulQuery;
        Debug.Log($"[ReferenceFetch][Debug] Asset '{assetName}' successfulQuery={winner}");
        foreach (var report in queryReports)
        {
            Debug.Log($"[ReferenceFetch][Debug] Asset '{assetName}' query='{report.Query}' raw={report.FoundRawCandidates} rejectedByFileType={report.RejectedByFileType} rejectedByLicense={report.RejectedByLicense} rejectedByResolution={report.RejectedByResolution} accepted={report.Accepted}");
        }

        var previewCount = Math.Min(3, candidates.Count);
        for (var i = 0; i < previewCount; i++)
        {
            var candidate = candidates[i];
            Debug.Log($"[ReferenceFetch][Debug] Asset '{assetName}' candidate#{i + 1}: url={candidate.FileUrl} mime={candidate.Mime} width={candidate.Width} license={candidate.License}");
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

internal sealed class ReferenceFetchReport
{
    [JsonProperty("simulationName")]
    public string SimulationName;

    [JsonProperty("generatedAtUtc")]
    public string GeneratedAtUtc;

    [JsonProperty("dryRun")]
    public bool DryRun;

    [JsonProperty("assets")]
    public readonly List<AssetFetchReport> Assets = new();
}

internal sealed class AssetFetchReport
{
    [JsonProperty("assetName")]
    public string AssetName;

    [JsonProperty("successfulQuery")]
    public string SuccessfulQuery;

    [JsonProperty("downloaded")]
    public int Downloaded;

    [JsonProperty("stats")]
    public ReferenceCandidateStats Stats;

    [JsonProperty("queryAttempts")]
    public List<QueryAttemptReport> QueryAttempts;
}

internal sealed class QueryAttemptReport
{
    [JsonProperty("query")]
    public string Query;

    [JsonProperty("foundRawCandidates")]
    public int FoundRawCandidates;

    [JsonProperty("rejectedByLicense")]
    public int RejectedByLicense;

    [JsonProperty("rejectedByFileType")]
    public int RejectedByFileType;

    [JsonProperty("rejectedByResolution")]
    public int RejectedByResolution;

    [JsonProperty("accepted")]
    public int Accepted;
}
