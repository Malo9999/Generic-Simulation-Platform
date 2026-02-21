using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AntSpriteSheetGenerator
{
    private static readonly string[] SpriteNames = { "ant_worker", "ant_worker_mask", "ant_soldier", "ant_soldier_mask" };

    private static readonly string[] WorkerTemplate =
    {
        "................................",
        "................................",
        "..............................#.",
        ".............................##.",
        "............................###.",
        "...........................##...",
        ".......................####.....",
        "...............#####.###........",
        ".............##########.........",
        "....##.....##########...........",
        "...SSS#########################.",
        "....SSSS#######################.",
        "......SSSS####################..",
        "........SSS#################....",
        ".............##########.........",
        "..............#######...........",
        "...............#####............",
        "..............#######...........",
        ".............##########.........",
        "........SSS#################....",
        "......SSSS####################..",
        "....SSSS#######################.",
        "...SSS#########################.",
        "....##.....##########...........",
        ".............##########.........",
        "...............#####.###........",
        ".......................####.....",
        "...........................##...",
        "............................###.",
        ".............................##.",
        "..............................#.",
        "................................"
    };

    private static readonly string[] SoldierTemplate =
    {
        "................................",
        "................................",
        "............................####",
        "...........................#####",
        "..........................######",
        ".........................####...",
        "......................######....",
        "...............#####.#####......",
        ".............############.......",
        "....##.....##############.......",
        "...SSSS########################.",
        "....SSSSS######################.",
        "......SSSS####################..",
        "........SSSS##################..",
        ".............##############.....",
        "..............###########.......",
        "...............########.........",
        "..............###########.......",
        ".............##############.....",
        "........SSSS##################..",
        "......SSSS####################..",
        "....SSSSS######################.",
        "...SSSS########################.",
        "....##.....##############.......",
        ".............############.......",
        "...............#####.#####......",
        "......................######....",
        ".........................####...",
        "..........................######",
        "...........................#####",
        "............................####",
        "................................"
    };

    public static AntPackGenerator.TextureResult Generate(string outputFolder, int seed, int antSpriteSize, AntPalettePreset palettePreset, bool overwrite)
    {
        var path = $"{outputFolder}/ants.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new AntPackGenerator.TextureResult(existing, sprites);
        }

        const int columns = 4;
        var width = columns * antSpriteSize;
        var height = antSpriteSize;
        var pixels = new Color32[width * height];

        for (var i = 0; i < columns; i++)
        {
            var ox = i * antSpriteSize;
            var isSoldier = i >= 2;
            var isMask = (i % 2) == 1;
            DrawAnt(pixels, width, ox, 0, antSpriteSize, seed + (i * 101), isSoldier, isMask, palettePreset);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, antSpriteSize, AntPackGenerator.BuildGridRects(SpriteNames, antSpriteSize, columns));
        return new AntPackGenerator.TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static void DrawAnt(Color32[] px, int width, int ox, int oy, int size, int seed, bool soldier, bool maskOnly, AntPalettePreset palettePreset)
    {
        BuildTemplateMasks(size, soldier, out var bodyMask, out var stripeMask);
        if (!ValidateSilhouette(bodyMask, size, seed, soldier))
        {
            Debug.LogWarning($"Ant silhouette validation failed for {(soldier ? "soldier" : "worker")}. Falling back to built-in template.");
            BuildTemplateMasks(size, soldier, out bodyMask, out stripeMask);
        }

        if (maskOnly)
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (GetMask(stripeMask, size, x, y))
                    {
                        Set(px, width, ox + x, oy + y, Color.white);
                    }
                }
            }

            return;
        }

        var colors = GetPalette(palettePreset, soldier);
        var outlineMask = BuildOutlineMask(bodyMask, size);
        var top = FindTop(bodyMask, size);
        var bottom = FindBottom(bodyMask, size);
        var span = Mathf.Max(1, bottom - top);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (GetMask(outlineMask, size, x, y))
                {
                    Set(px, width, ox + x, oy + y, colors.Outline);
                }
            }
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(bodyMask, size, x, y))
                {
                    continue;
                }

                var normalizedY = (y - top) / (float)span;
                var c = colors.Body;
                if (normalizedY < 0.33f)
                {
                    c = colors.Highlight;
                }
                else if (normalizedY > 0.76f)
                {
                    c = colors.Shade;
                }

                Set(px, width, ox + x, oy + y, c);
            }
        }
    }

    private static void BuildTemplateMasks(int size, bool soldier, out bool[] bodyMask, out bool[] stripeMask)
    {
        var template = soldier ? SoldierTemplate : WorkerTemplate;
        var sourceHeight = template.Length;
        var sourceWidth = template[0].Length;

        bodyMask = new bool[size * size];
        stripeMask = new bool[size * size];

        for (var y = 0; y < size; y++)
        {
            var ty = Mathf.Clamp((int)((y / (float)size) * sourceHeight), 0, sourceHeight - 1);
            for (var x = 0; x < size; x++)
            {
                var tx = Mathf.Clamp((int)((x / (float)size) * sourceWidth), 0, sourceWidth - 1);
                var ch = template[ty][tx];
                var body = ch == '#' || ch == 'S';
                SetMask(bodyMask, size, x, y, body);
                SetMask(stripeMask, size, x, y, ch == 'S');
            }
        }
    }

    private static bool[] BuildOutlineMask(bool[] bodyMask, int size)
    {
        var outlineMask = new bool[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (GetMask(bodyMask, size, x, y))
                {
                    continue;
                }

                if (HasNeighbor(bodyMask, size, x, y))
                {
                    SetMask(outlineMask, size, x, y, true);
                }
            }
        }

        return outlineMask;
    }

    private static bool ValidateSilhouette(bool[] bodyMask, int size, int seed, bool soldier)
    {
        var spans = new float[size];
        for (var x = 0; x < size; x++)
        {
            var count = 0;
            for (var y = 0; y < size; y++)
            {
                if (GetMask(bodyMask, size, x, y))
                {
                    count++;
                }
            }

            spans[x] = count;
        }

        var smoothed = new float[size];
        for (var x = 0; x < size; x++)
        {
            var left = Mathf.Max(0, x - 1);
            var right = Mathf.Min(size - 1, x + 1);
            smoothed[x] = (spans[left] + spans[x] + spans[right]) / 3f;
        }

        var localMinima = 0;
        var maxSpan = smoothed.Max();
        var minSpan = maxSpan;
        for (var x = 1; x < size - 1; x++)
        {
            if (smoothed[x] < minSpan)
            {
                minSpan = smoothed[x];
            }

            if (smoothed[x] < smoothed[x - 1] && smoothed[x] <= smoothed[x + 1] && smoothed[x] > 0f)
            {
                localMinima++;
            }
        }

        var middleStart = Mathf.RoundToInt(size * 0.35f);
        var middleEnd = Mathf.RoundToInt(size * 0.7f);
        var middleMin = maxSpan;
        for (var x = middleStart; x <= middleEnd; x++)
        {
            middleMin = Mathf.Min(middleMin, smoothed[x]);
        }

        var hasWaistDip = maxSpan > 0f && middleMin < maxSpan * 0.65f;
        var hasSegmentProfile = localMinima >= 2 || hasWaistDip;

        var antennaPixels = 0;
        var frontStart = Mathf.RoundToInt(size * 0.72f);
        var topBandEnd = Mathf.RoundToInt(size * 0.35f);
        var bottomBandStart = Mathf.RoundToInt(size * 0.65f);
        for (var y = 0; y < topBandEnd; y++)
        {
            for (var x = frontStart; x < size; x++)
            {
                if (GetMask(bodyMask, size, x, y))
                {
                    antennaPixels++;
                }
            }
        }

        for (var y = bottomBandStart; y < size; y++)
        {
            for (var x = frontStart; x < size; x++)
            {
                if (GetMask(bodyMask, size, x, y))
                {
                    antennaPixels++;
                }
            }
        }

        var lowerBandStart = Mathf.RoundToInt(size * 0.65f);
        var lowerBandEnd = size - 1;
        var leftLegPixels = 0;
        var rightLegPixels = 0;
        for (var y = lowerBandStart; y <= lowerBandEnd; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(bodyMask, size, x, y))
                {
                    continue;
                }

                if (x < size * 0.5f)
                {
                    leftLegPixels++;
                }
                else
                {
                    rightLegPixels++;
                }
            }
        }

        var hasLegPresence = leftLegPixels >= Mathf.RoundToInt(size * 1.3f) && rightLegPixels >= Mathf.RoundToInt(size * 1.3f);
        var hasAntennae = antennaPixels >= Mathf.Max(4, size / 8);

        var valid = hasSegmentProfile && hasAntennae && hasLegPresence;
        if (!valid)
        {
            Debug.LogWarning($"Ant silhouette validator failed ({(soldier ? "soldier" : "worker")}) seed={seed}. profile={hasSegmentProfile}, antennae={hasAntennae}, legs={hasLegPresence}");
        }

        return valid;
    }

    private static int FindTop(bool[] mask, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (GetMask(mask, size, x, y))
                {
                    return y;
                }
            }
        }

        return 0;
    }

    private static int FindBottom(bool[] mask, int size)
    {
        for (var y = size - 1; y >= 0; y--)
        {
            for (var x = 0; x < size; x++)
            {
                if (GetMask(mask, size, x, y))
                {
                    return y;
                }
            }
        }

        return size - 1;
    }

    private static (Color32 Body, Color32 Shade, Color32 Highlight, Color32 Outline) GetPalette(AntPalettePreset preset, bool soldier)
    {
        switch (preset)
        {
            case AntPalettePreset.Desert:
                return soldier
                    ? (new Color32(118, 82, 45, 255), new Color32(88, 61, 33, 255), new Color32(149, 105, 62, 255), new Color32(30, 20, 12, 255))
                    : (new Color32(134, 98, 58, 255), new Color32(100, 72, 41, 255), new Color32(166, 124, 79, 255), new Color32(30, 20, 12, 255));
            case AntPalettePreset.Twilight:
                return soldier
                    ? (new Color32(74, 62, 98, 255), new Color32(55, 46, 76, 255), new Color32(97, 84, 128, 255), new Color32(17, 14, 24, 255))
                    : (new Color32(87, 71, 116, 255), new Color32(65, 54, 87, 255), new Color32(114, 95, 146, 255), new Color32(17, 14, 24, 255));
            default:
                return soldier
                    ? (new Color32(72, 40, 27, 255), new Color32(48, 27, 19, 255), new Color32(100, 59, 39, 255), new Color32(18, 10, 8, 255))
                    : (new Color32(88, 51, 33, 255), new Color32(60, 36, 24, 255), new Color32(118, 73, 49, 255), new Color32(18, 10, 8, 255));
        }
    }

    private static bool HasNeighbor(bool[] mask, int size, int x, int y)
    {
        for (var ny = -1; ny <= 1; ny++)
        {
            for (var nx = -1; nx <= 1; nx++)
            {
                if ((nx != 0 || ny != 0) && GetMask(mask, size, x + nx, y + ny))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool GetMask(bool[] mask, int size, int x, int y) => x >= 0 && y >= 0 && x < size && y < size && mask[y * size + x];
    private static void SetMask(bool[] mask, int size, int x, int y, bool v) { if (x >= 0 && y >= 0 && x < size && y < size) mask[y * size + x] = v; }

    private static void Set(Color32[] px, int width, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0)
        {
            return;
        }

        var i = y * width + x;
        if (i < 0 || i >= px.Length)
        {
            return;
        }

        px[i] = c;
    }
}
