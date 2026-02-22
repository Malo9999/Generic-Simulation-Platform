using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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
    private const string AllowedMediaType = "BITMAP";

    public WikimediaSearchResult Search(string query, int desiredCount, int minWidth, LicenseFilter licenseFilter)
    {
        var searchTitles = SearchFileTitles(query, Math.Max(desiredCount * 4, desiredCount));
        var result = new WikimediaSearchResult();
        if (searchTitles.Count == 0)
        {
            return result;
        }

        var imageInfos = LoadImageInfos(searchTitles, 1600);
        foreach (var imageInfo in imageInfos)
        {
            result.Stats.FoundRawCandidates++;

            var fileUrl = imageInfo.ThumbUrl;
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                fileUrl = imageInfo.Url;
            }

            var pageUrl = imageInfo.DescriptionUrl;
            if (string.IsNullOrWhiteSpace(fileUrl) || string.IsNullOrWhiteSpace(pageUrl))
            {
                result.Stats.RejectedByFileType++;
                continue;
            }

            if (!IsAllowedFileType(imageInfo.Mime, imageInfo.MediaType))
            {
                result.Stats.RejectedByFileType++;
                continue;
            }

            if (imageInfo.Width < minWidth)
            {
                result.Stats.RejectedByResolution++;
                continue;
            }

            var license = MetadataValue(imageInfo.ExtMetadata, "LicenseShortName");
            var licenseUrl = MetadataValue(imageInfo.ExtMetadata, "LicenseUrl");
            var author = CleanupMetadata(MetadataValue(imageInfo.ExtMetadata, "Artist"));
            var attribution = CleanupMetadata(MetadataValue(imageInfo.ExtMetadata, "Attribution"));
            if (string.IsNullOrWhiteSpace(attribution))
            {
                attribution = CleanupMetadata(MetadataValue(imageInfo.ExtMetadata, "Credit"));
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
                Mime = imageInfo.Mime,
                FileExtension = GetExtension(fileUrl),
                License = string.IsNullOrWhiteSpace(licenseUrl) ? license : $"{license} ({licenseUrl})",
                Author = author,
                Attribution = attribution,
                Width = imageInfo.Width,
                Height = imageInfo.Height,
            });
            result.Stats.Accepted++;

            if (result.Candidates.Count >= desiredCount)
            {
                break;
            }
        }

        return result;
    }

    private static List<string> SearchFileTitles(string query, int limit)
    {
        var cappedLimit = Math.Clamp(limit, 1, 50);
        var url = $"{ApiUrl}?action=query&list=search&srnamespace=6&srsearch={Uri.EscapeDataString(query)}&srlimit={cappedLimit}&format=json";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "GenericSimulationPlatformReferenceFetcher/1.0");
        var response = client.GetStringAsync(url).GetAwaiter().GetResult();
        var root = JObject.Parse(response);
        var searchItems = root["query"]?["search"] as JArray;
        if (searchItems == null)
        {
            return new List<string>();
        }

        var titles = new List<string>();
        foreach (var item in searchItems)
        {
            var title = item.Value<string>("title");
            if (!string.IsNullOrWhiteSpace(title))
            {
                titles.Add(title);
            }
        }

        return titles;
    }

    private static List<WikiImageInfo> LoadImageInfos(IReadOnlyList<string> fileTitles, int urlWidth)
    {
        var results = new List<WikiImageInfo>();
        if (fileTitles.Count == 0)
        {
            return results;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "GenericSimulationPlatformReferenceFetcher/1.0");

        const int batchSize = 25;
        for (var i = 0; i < fileTitles.Count; i += batchSize)
        {
            var chunk = fileTitles.Skip(i).Take(batchSize);
            var titlesParam = Uri.EscapeDataString(string.Join("|", chunk));
            var urlBuilder = new StringBuilder();
            urlBuilder.Append($"{ApiUrl}?action=query&titles={titlesParam}&prop=imageinfo");
            urlBuilder.Append("&iiprop=url|mime|size|mediatype|extmetadata");
            if (urlWidth > 0)
            {
                urlBuilder.Append($"&iiurlwidth={urlWidth}");
            }

            urlBuilder.Append("&format=json");

            var response = client.GetStringAsync(urlBuilder.ToString()).GetAwaiter().GetResult();
            var root = JObject.Parse(response);
            var pages = root["query"]?["pages"] as JObject;
            if (pages == null)
            {
                continue;
            }

            foreach (var page in pages.Properties())
            {
                var imageInfo = page.Value["imageinfo"]?[0];
                if (imageInfo == null)
                {
                    continue;
                }

                results.Add(new WikiImageInfo
                {
                    Url = imageInfo.Value<string>("url") ?? string.Empty,
                    ThumbUrl = imageInfo.Value<string>("thumburl") ?? string.Empty,
                    Mime = imageInfo.Value<string>("mime") ?? string.Empty,
                    MediaType = imageInfo.Value<string>("mediatype") ?? string.Empty,
                    Width = imageInfo.Value<int?>("width") ?? 0,
                    Height = imageInfo.Value<int?>("height") ?? 0,
                    DescriptionUrl = imageInfo.Value<string>("descriptionurl") ?? string.Empty,
                    ExtMetadata = imageInfo["extmetadata"],
                });
            }
        }

        return results;
    }

    private static bool IsAllowedFileType(string mime, string mediaType)
    {
        if (!string.Equals(mediaType, AllowedMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AllowedMimeTypes.Contains(mime);
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

    private sealed class WikiImageInfo
    {
        public string Url;
        public string ThumbUrl;
        public string Mime;
        public string MediaType;
        public int Width;
        public int Height;
        public string DescriptionUrl;
        public JToken ExtMetadata;
    }
}
