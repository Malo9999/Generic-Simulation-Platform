using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class AntPropsGenerator
{
    public readonly struct TextureResult
    {
        public readonly Texture2D Texture;
        public readonly List<Sprite> Sprites;

        public TextureResult(Texture2D texture, List<Sprite> sprites)
        {
            Texture = texture;
            Sprites = sprites;
        }
    }

    private static readonly string[] SpriteNames =
    {
        "prop_nest_entrance_small", "prop_nest_entrance_medium", "prop_food_small", "prop_food_medium",
        "prop_food_large", "prop_carry_pellet", "prop_dust_puff"
    };

    public static TextureResult Generate(string outputFolder, int seed, int tileSize, AntPalettePreset palettePreset, bool overwrite)
    {
        var path = $"{outputFolder}/props.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new TextureResult(existing, sprites);
        }

        const int columns = 4;
        var rows = Mathf.CeilToInt(SpriteNames.Length / (float)columns);
        var width = columns * tileSize;
        var height = rows * tileSize;
        var px = new Color32[width * height];

        for (var i = 0; i < SpriteNames.Length; i++)
        {
            var col = i % columns;
            var row = rows - 1 - i / columns;
            DrawProp(px, width, col * tileSize, row * tileSize, tileSize, SpriteNames[i], seed + i * 37);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(px);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, tileSize, BuildGridRects(SpriteNames, tileSize, columns));
        return new TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static List<SpriteRect> BuildGridRects(IReadOnlyList<string> names, int tileSize, int columns)
    {
        var rows = Mathf.CeilToInt(names.Count / (float)columns);
        var rects = new List<SpriteRect>(names.Count);
        for (var index = 0; index < names.Count; index++)
        {
            var col = index % columns;
            var row = rows - 1 - index / columns;
            rects.Add(new SpriteRect
            {
                name = names[index],
                rect = new Rect(col * tileSize, row * tileSize, tileSize, tileSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        return rects;
    }

    private static void DrawProp(Color32[] px, int width, int ox, int oy, int size, string id, int seed)
    {
        switch (id)
        {
            case "prop_nest_entrance_small":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 5, new Color32(107, 71, 44, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, size / 9, new Color32(33, 24, 16, 255));
                break;
            case "prop_nest_entrance_medium":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 4, new Color32(122, 82, 54, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, size / 7, new Color32(33, 24, 16, 255));
                break;
            case "prop_food_small":
            case "prop_food_medium":
            case "prop_food_large":
                int r = id.EndsWith("small") ? size / 10 : id.EndsWith("medium") ? size / 8 : size / 6;
                Disk(px, width, ox + size / 2, oy + size / 2, r, new Color32(226, 193, 88, 255));
                break;
            case "prop_carry_pellet":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 10, new Color32(218, 214, 188, 255));
                break;
            case "prop_dust_puff":
                for (int i = 0; i < 4; i++)
                {
                    int j = Hash(seed, i, 0, 2) % (size / 5);
                    Disk(px, width, ox + size / 2 - size / 8 + i * (size / 12), oy + size / 2 + j - size / 10, size / 11, new Color32(189, 181, 168, 170));
                }
                break;
        }
    }

    private static void Disk(Color32[] px, int width, int cx, int cy, int r, Color32 c)
    {
        int rr = r * r;
        for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
            if (x * x + y * y <= rr)
                Set(px, width, cx + x, cy + y, c);
    }

    private static void Set(Color32[] px, int width, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0) return;
        int i = y * width + x;
        if (i < 0 || i >= px.Length) return;
        px[i] = c;
    }

    private static int Hash(int seed, int x, int y, int salt)
    {
        unchecked
        {
            int h = seed;
            h = (h * 397) ^ (x * 73856093);
            h = (h * 397) ^ (y * 19349663);
            h = (h * 397) ^ (salt * 83492791);
            return h;
        }
    }
}
