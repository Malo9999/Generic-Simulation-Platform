using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AntSpriteSheetGenerator
{
    private static readonly string[] SpriteNames = { "ant_worker", "ant_worker_mask", "ant_soldier", "ant_soldier_mask" };

    public static AntPackGenerator.TextureResult Generate(string outputFolder, int seed, int antSpriteSize, AntPalettePreset palettePreset, bool overwrite)
    {
        return GenerateBase(outputFolder, seed, antSpriteSize, palettePreset, overwrite, null);
    }

    public static AntPackGenerator.TextureResult GenerateBase(string outputFolder, int seed, int antSpriteSize, AntPalettePreset palettePreset, bool overwrite, AntSpeciesBlueprintSet blueprintSet)
    {
        var path = $"{outputFolder}/ants.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new AntPackGenerator.TextureResult(existing, sprites);
        }

        blueprintSet ??= AntBlueprintFactory.EnsureDefaultSpeciesBlueprints(outputFolder, false);
        var worker = blueprintSet.FindRole("worker")?.basePose;
        var soldier = blueprintSet.FindRole("soldier")?.basePose;
        if (worker == null || soldier == null)
        {
            blueprintSet = AntBlueprintFactory.EnsureDefaultSpeciesBlueprints(outputFolder, true);
            worker = blueprintSet.FindRole("worker")?.basePose;
            soldier = blueprintSet.FindRole("soldier")?.basePose;
        }

        const int columns = 4;
        var width = columns * antSpriteSize;
        var height = antSpriteSize;
        var pixels = new Color32[width * height];

        RenderBlueprintPair(worker, antSpriteSize, 0, pixels, width, palettePreset, false);
        RenderBlueprintPair(worker, antSpriteSize, antSpriteSize, pixels, width, palettePreset, true);
        RenderBlueprintPair(soldier, antSpriteSize, antSpriteSize * 2, pixels, width, palettePreset, false);
        RenderBlueprintPair(soldier, antSpriteSize, antSpriteSize * 3, pixels, width, palettePreset, true);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, antSpriteSize, AntPackGenerator.BuildGridRects(SpriteNames, antSpriteSize, columns));
        return new AntPackGenerator.TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static void RenderBlueprintPair(PixelBlueprint2D blueprint, int targetSize, int ox, Color32[] outPixels, int outWidth, AntPalettePreset preset, bool maskOnly)
    {
        if (blueprint == null)
        {
            return;
        }

        var body = blueprint.EnsureLayer("body");
        var stripe = blueprint.EnsureLayer("stripe");
        var colors = GetPalette(preset);
        var outline = BuildOutline(body.pixels, blueprint.width, blueprint.height);

        for (var y = 0; y < targetSize; y++)
        {
            for (var x = 0; x < targetSize; x++)
            {
                var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * blueprint.width), 0, blueprint.width - 1);
                var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * blueprint.height), 0, blueprint.height - 1);
                var idx = (sy * blueprint.width) + sx;
                Color32 c = new Color32(0, 0, 0, 0);

                if (maskOnly)
                {
                    if (stripe.pixels[idx] > 0)
                    {
                        c = Color.white;
                    }
                }
                else if (outline[idx])
                {
                    c = colors.Outline;
                }
                else if (body.pixels[idx] > 0)
                {
                    var t = sy / (float)Mathf.Max(1, blueprint.height - 1);
                    c = t < 0.35f ? colors.Highlight : (t > 0.75f ? colors.Shade : colors.Body);
                }

                outPixels[(y * outWidth) + ox + x] = c;
            }
        }
    }

    private static bool[] BuildOutline(byte[] body, int width, int height)
    {
        var outline = new bool[body.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                if (body[i] > 0)
                {
                    continue;
                }

                var hasBodyNeighbor = false;
                for (var oy = -1; oy <= 1 && !hasBodyNeighbor; oy++)
                {
                    for (var ox = -1; ox <= 1 && !hasBodyNeighbor; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        var nx = x + ox;
                        var ny = y + oy;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && body[(ny * width) + nx] > 0)
                        {
                            hasBodyNeighbor = true;
                        }
                    }
                }

                outline[i] = hasBodyNeighbor;
            }
        }

        return outline;
    }

    private static (Color32 Body, Color32 Shade, Color32 Highlight, Color32 Outline) GetPalette(AntPalettePreset preset)
    {
        return preset switch
        {
            AntPalettePreset.Desert => (new Color32(158, 126, 79, 255), new Color32(97, 74, 44, 255), new Color32(191, 158, 111, 255), new Color32(35, 25, 16, 255)),
            AntPalettePreset.Twilight => (new Color32(71, 74, 108, 255), new Color32(44, 46, 68, 255), new Color32(103, 108, 153, 255), new Color32(21, 21, 35, 255)),
            _ => (new Color32(68, 54, 41, 255), new Color32(40, 31, 23, 255), new Color32(95, 77, 57, 255), new Color32(19, 14, 9, 255))
        };
    }
}
