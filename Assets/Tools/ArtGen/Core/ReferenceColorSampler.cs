using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class ReferenceColorSampler
{
    private static readonly Dictionary<string, Color32> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Color32 SampleOrFallback(string simulationId, string speciesId, Color32 fallback)
    {
        if (string.IsNullOrWhiteSpace(simulationId) || string.IsNullOrWhiteSpace(speciesId))
        {
            return fallback;
        }

        var cacheKey = simulationId + ":" + speciesId;
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var imagesFolder = Path.Combine("_References", simulationId, speciesId, "Images");
        var imagePath = SelectBestImage(imagesFolder);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            Cache[cacheKey] = fallback;
            return fallback;
        }

        if (!TryComputeForegroundAverage(imagePath, out var sampledColor))
        {
            Cache[cacheKey] = fallback;
            return fallback;
        }

        Cache[cacheKey] = sampledColor;
        return sampledColor;
    }

    private static string SelectBestImage(string imagesFolder)
    {
        if (!Directory.Exists(imagesFolder))
        {
            return string.Empty;
        }

        return Directory
            .EnumerateFiles(imagesFolder)
            .Where(path =>
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.Length)
            .Select(file => file.FullName)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool TryComputeForegroundAverage(string imagePath, out Color32 color)
    {
        color = default;

        var bytes = File.ReadAllBytes(imagePath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes, false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return false;
        }

        var pixels = texture.GetPixels32();
        if (pixels == null || pixels.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return false;
        }

        var background = EstimateBackgroundColor(texture, pixels);
        long sumR = 0;
        long sumG = 0;
        long sumB = 0;
        var count = 0;

        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            if (p.a < 24)
            {
                continue;
            }

            var colorDistance = Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b);
            if (colorDistance < 36)
            {
                continue;
            }

            sumR += p.r;
            sumG += p.g;
            sumB += p.b;
            count++;
        }

        UnityEngine.Object.DestroyImmediate(texture);

        if (count == 0)
        {
            return false;
        }

        color = new Color32((byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count), 255);
        return true;
    }

    private static Color32 EstimateBackgroundColor(Texture2D texture, Color32[] pixels)
    {
        var width = texture.width;
        var height = texture.height;
        var sampleIndexes = new[]
        {
            0,
            Mathf.Max(0, width - 1),
            Mathf.Max(0, (height - 1) * width),
            Mathf.Max(0, (height - 1) * width + width - 1)
        };

        long sumR = 0;
        long sumG = 0;
        long sumB = 0;
        var count = 0;

        for (var i = 0; i < sampleIndexes.Length; i++)
        {
            var idx = sampleIndexes[i];
            if (idx < 0 || idx >= pixels.Length)
            {
                continue;
            }

            var p = pixels[idx];
            sumR += p.r;
            sumG += p.g;
            sumB += p.b;
            count++;
        }

        if (count == 0)
        {
            return new Color32(255, 255, 255, 255);
        }

        return new Color32((byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count), 255);
    }
}
