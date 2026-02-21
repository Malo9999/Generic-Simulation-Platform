using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class AntAnimationSheetGenerator
{
    public static AntPackGenerator.TextureResult GenerateFromBlueprint(string outputFolder, int antSpriteSize, AntPalettePreset palette, bool overwrite, AntSpeciesBlueprintSet blueprintSet)
    {
        var path = $"{outputFolder}/ants_anim.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new AntPackGenerator.TextureResult(existing, sprites);
        }

        if (blueprintSet == null)
        {
            return new AntPackGenerator.TextureResult(null, new List<Sprite>());
        }

        var clips = blueprintSet.clips.Where(c => c.frames != null && c.frames.Count > 0).ToList();
        if (clips.Count == 0)
        {
            return new AntPackGenerator.TextureResult(null, new List<Sprite>());
        }

        var maxFrames = clips.Max(c => c.frames.Count);
        var columns = maxFrames * 2;
        var rows = clips.Count;
        var texture = new Texture2D(columns * antSpriteSize, rows * antSpriteSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[texture.width * texture.height];
        var spriteRects = new List<SpriteRect>();

        for (var row = 0; row < rows; row++)
        {
            var clip = clips[row];
            var rowFromBottom = rows - 1 - row;
            for (var frameIndex = 0; frameIndex < clip.frames.Count; frameIndex++)
            {
                var frame = clip.frames[frameIndex];
                var baseCol = frameIndex * 2;
                Render(frame.blueprint, false, antSpriteSize, pixels, texture.width, baseCol * antSpriteSize, rowFromBottom * antSpriteSize, palette);
                Render(frame.blueprint, true, antSpriteSize, pixels, texture.width, (baseCol + 1) * antSpriteSize, rowFromBottom * antSpriteSize, palette);

                var baseName = $"ant_{blueprintSet.speciesId}_{clip.roleId}_{clip.clipId}_{frameIndex}";
                spriteRects.Add(MakeRect(baseName, baseCol, rowFromBottom, antSpriteSize));
                spriteRects.Add(MakeRect($"{baseName}_mask", baseCol + 1, rowFromBottom, antSpriteSize));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, antSpriteSize, spriteRects);
        return new AntPackGenerator.TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static SpriteRect MakeRect(string name, int col, int row, int size)
    {
        return new SpriteRect
        {
            name = name,
            rect = new Rect(col * size, row * size, size, size),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = GUID.Generate()
        };
    }

    private static void Render(PixelBlueprint2D bp, bool maskOnly, int targetSize, Color32[] outPixels, int outWidth, int ox, int oy, AntPalettePreset palette)
    {
        if (bp == null)
        {
            return;
        }

        var body = bp.EnsureLayer("body");
        var stripe = bp.EnsureLayer("stripe");
        var outline = BuildOutline(body.pixels, bp.width, bp.height);
        var colors = palette switch
        {
            AntPalettePreset.Desert => (new Color32(158, 126, 79, 255), new Color32(97, 74, 44, 255), new Color32(191, 158, 111, 255), new Color32(35, 25, 16, 255)),
            AntPalettePreset.Twilight => (new Color32(71, 74, 108, 255), new Color32(44, 46, 68, 255), new Color32(103, 108, 153, 255), new Color32(21, 21, 35, 255)),
            _ => (new Color32(68, 54, 41, 255), new Color32(40, 31, 23, 255), new Color32(95, 77, 57, 255), new Color32(19, 14, 9, 255))
        };

        for (var y = 0; y < targetSize; y++)
        {
            for (var x = 0; x < targetSize; x++)
            {
                var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * bp.width), 0, bp.width - 1);
                var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * bp.height), 0, bp.height - 1);
                var idx = (sy * bp.width) + sx;
                Color32 c = new Color32(0, 0, 0, 0);

                if (maskOnly)
                {
                    if (stripe.pixels[idx] > 0) c = Color.white;
                }
                else if (outline[idx])
                {
                    c = colors.Item4;
                }
                else if (body.pixels[idx] > 0)
                {
                    var t = sy / (float)Mathf.Max(1, bp.height - 1);
                    c = t < 0.35f ? colors.Item3 : (t > 0.75f ? colors.Item2 : colors.Item1);
                }

                outPixels[((oy + y) * outWidth) + ox + x] = c;
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
                if (body[i] > 0) continue;
                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        var nx = x + ox;
                        var ny = y + oy;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && body[(ny * width) + nx] > 0)
                        {
                            outline[i] = true;
                        }
                    }
                }
            }
        }

        return outline;
    }
}
