using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

internal sealed class WikimediaClient
{
    private const string ApiUrl = "https://commons.wikimedia.org/w/api.php";
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
    };
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".djvu", ".tif", ".tiff", ".svg", ".xcf", ".psd", ".webm", ".ogv",
    };

    public WikimediaSearchResult Search(string query, int desiredCount, int minWidth, LicenseFilter licenseFilter)
    {
        var url = $"{ApiUrl}?action=query&generator=search&gsrnamespace=6&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit=50&prop=imageinfo&iiprop=url|size|mime|extmetadata&format=json";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "GenericSimulationPlatformReferenceFetcher/1.0");
        var response = client.GetStringAsync(url).GetAwaiter().GetResult();
        var root = JObject.Parse(response);

        var pages = root["query"]?["pages"] as JObject;
        var result = new WikimediaSearchResult();
        if (pages == null)
        {
            return result;
        }

        foreach (var page in pages.Properties())
        {
            var imageInfo = page.Value["imageinfo"]?[0];
            if (imageInfo == null)
            {
                continue;
            }

            result.Stats.FoundRawCandidates++;

            var fileUrl = imageInfo.Value<string>("url");
            var pageUrl = page.Value.Value<string>("canonicalurl") ?? imageInfo.Value<string>("descriptionurl");
            var mime = imageInfo.Value<string>("mime") ?? string.Empty;
            var extension = GetExtension(fileUrl);
            if (string.IsNullOrWhiteSpace(fileUrl) || string.IsNullOrWhiteSpace(pageUrl))
            {
                result.Stats.RejectedByFileType++;
                continue;
            }

            if (!IsAllowedFileType(mime, extension))
            {
                result.Stats.RejectedByFileType++;
                continue;
            }

            var width = imageInfo.Value<int?>("width") ?? 0;
            var height = imageInfo.Value<int?>("height") ?? 0;
            if (width < minWidth)
            {
                result.Stats.RejectedByResolution++;
                continue;
            }

            var ext = imageInfo["extmetadata"];
            var license = MetadataValue(ext, "LicenseShortName");
            var licenseUrl = MetadataValue(ext, "LicenseUrl");
            var author = CleanupMetadata(MetadataValue(ext, "Artist"));
            var attribution = CleanupMetadata(MetadataValue(ext, "Attribution"));
            if (string.IsNullOrWhiteSpace(attribution))
            {
                attribution = CleanupMetadata(MetadataValue(ext, "Credit"));
            }

            if (!licenseFilter.IsAllowed(license))
            {
                result.Stats.RejectedByLicense++;
                continue;
            }

            result.Candidates.Add(new ReferenceCandidate
            {
                Source = "wikimedia",
                PageUrl = pageUrl,
                FileUrl = fileUrl,
                Mime = mime,
                FileExtension = extension,
                License = string.IsNullOrWhiteSpace(licenseUrl) ? license : $"{license} ({licenseUrl})",
                Author = author,
                Attribution = attribution,
                Width = width,
                Height = height,
            });
            result.Stats.Accepted++;

            if (result.Candidates.Count >= desiredCount)
            {
                break;
            }
        }

        return result;
    }

    private static bool IsAllowedFileType(string mime, string extension)
    {
        if (BlockedExtensions.Contains(extension))
        {
            return false;
        }

        return AllowedMimeTypes.Contains(mime) && AllowedExtensions.Contains(extension);
    }

    private static string GetExtension(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return System.IO.Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
    }

    private static string MetadataValue(JToken token, string field)
    {
        return token?[field]?["value"]?.Value<string>() ?? string.Empty;
    }

    private static string CleanupMetadata(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var noHtml = Regex.Replace(value, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noHtml).Trim();
    }
}
