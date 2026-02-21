using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

internal sealed class WikimediaClient
{
    private const string ApiUrl = "https://commons.wikimedia.org/w/api.php";

    public List<ReferenceCandidate> Search(string query, int desiredCount, int minWidth, LicenseFilter licenseFilter)
    {
        var url = $"{ApiUrl}?action=query&generator=search&gsrnamespace=6&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit=50&prop=imageinfo&iiprop=url|size|extmetadata&format=json";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "GenericSimulationPlatformReferenceFetcher/1.0");
        var response = client.GetStringAsync(url).GetAwaiter().GetResult();
        var root = JObject.Parse(response);

        var pages = root["query"]?["pages"] as JObject;
        var results = new List<ReferenceCandidate>();
        if (pages == null)
        {
            return results;
        }

        foreach (var page in pages.Properties())
        {
            var imageInfo = page.Value["imageinfo"]?[0];
            if (imageInfo == null)
            {
                continue;
            }

            var width = imageInfo.Value<int?>("width") ?? 0;
            var height = imageInfo.Value<int?>("height") ?? 0;
            if (width < minWidth)
            {
                continue;
            }

            var fileUrl = imageInfo.Value<string>("url");
            var pageUrl = page.Value.Value<string>("canonicalurl") ?? imageInfo.Value<string>("descriptionurl");
            if (string.IsNullOrWhiteSpace(fileUrl) || string.IsNullOrWhiteSpace(pageUrl))
            {
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
                continue;
            }

            results.Add(new ReferenceCandidate
            {
                Source = "wikimedia",
                PageUrl = pageUrl,
                FileUrl = fileUrl,
                License = string.IsNullOrWhiteSpace(licenseUrl) ? license : $"{license} ({licenseUrl})",
                Author = author,
                Attribution = attribution,
                Width = width,
                Height = height,
            });

            if (results.Count >= desiredCount)
            {
                break;
            }
        }

        return results;
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
