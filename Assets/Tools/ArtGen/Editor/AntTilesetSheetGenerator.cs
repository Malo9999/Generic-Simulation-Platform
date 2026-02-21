using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AntTilesetSheetGenerator
{
    private static readonly string[] TileNames =
    {
        "ground_plain", "ground_alt_a", "ground_alt_b", "ground_alt_c",
        "wall_rock_a", "wall_rock_b", "nest_entrance", "nest_interior",
        "food_crumb", "food_pile", "debug_pheromone_low", "debug_pheromone_high"
    };

    public static AntPackGenerator.TextureResult Generate(string outputFolder, int seed, int tileSize, AntPalettePreset palettePreset, bool overwrite)
    {
        var path = $"{outputFolder}/tileset.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new AntPackGenerator.TextureResult(existing, sprites);
        }

        const int columns = 4;
        var rows = Mathf.CeilToInt(TileNames.Length / (float)columns);
        var width = columns * tileSize;
        var height = rows * tileSize;
        var pixels = new Color32[width * height];

        var p = GetPalette(palettePreset);
        for (var i = 0; i < TileNames.Length; i++)
        {
            var col = i % columns;
            var row = rows - 1 - i / columns;
            DrawTile(pixels, width, col * tileSize, row * tileSize, tileSize, TileNames[i], seed + i * 29, p);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, tileSize, AntPackGenerator.BuildGridRects(TileNames, tileSize, columns));
        return new AntPackGenerator.TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static void DrawTile(Color32[] px, int width, int ox, int oy, int size, string tileId, int seed, Palette p)
    {
        Fill(px, width, ox, oy, size, size, p.Ground);
        Noise(px, width, ox, oy, size, seed, p.GroundDark, p.GroundLight);

        switch (tileId)
        {
            case "wall_rock_a":
            case "wall_rock_b":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 3, p.Rock);
                Disk(px, width, ox + size / 2 + size / 5, oy + size / 2 - size / 6, size / 5, p.RockDark);
                break;
            case "nest_entrance":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 3, p.Nest);
                Disk(px, width, ox + size / 2, oy + size / 2, size / 5, p.Void);
                break;
            case "nest_interior":
                Fill(px, width, ox, oy, size, size, p.Nest);
                Noise(px, width, ox, oy, size, seed + 3, p.NestDark, p.GroundLight);
                break;
            case "food_crumb":
                Disk(px, width, ox + size / 2, oy + size / 2, size / 6, p.Food);
                break;
            case "food_pile":
                Disk(px, width, ox + size / 2 - size / 8, oy + size / 2, size / 6, p.Food);
                Disk(px, width, ox + size / 2 + size / 8, oy + size / 2, size / 5, p.FoodDark);
                break;
            case "debug_pheromone_low":
                Fill(px, width, ox, oy, size, size, new Color32(16, 16, 16, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, size / 4, new Color32(50, 110, 220, 255));
                break;
            case "debug_pheromone_high":
                Fill(px, width, ox, oy, size, size, new Color32(16, 16, 16, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, size / 3, new Color32(245, 70, 50, 255));
                break;
        }
    }

    private static Palette GetPalette(AntPalettePreset preset) => preset switch
    {
        AntPalettePreset.Desert => new Palette(new Color32(190, 161, 104, 255), new Color32(162, 136, 84, 255), new Color32(212, 184, 132, 255)),
        AntPalettePreset.Twilight => new Palette(new Color32(81, 90, 105, 255), new Color32(63, 70, 82, 255), new Color32(102, 112, 130, 255)),
        _ => new Palette(new Color32(101, 142, 78, 255), new Color32(77, 111, 60, 255), new Color32(125, 166, 102, 255))
    };

    private readonly struct Palette
    {
        public readonly Color32 Ground;
        public readonly Color32 GroundDark;
        public readonly Color32 GroundLight;
        public readonly Color32 Rock;
        public readonly Color32 RockDark;
        public readonly Color32 Nest;
        public readonly Color32 NestDark;
        public readonly Color32 Food;
        public readonly Color32 FoodDark;
        public readonly Color32 Void;

        public Palette(Color32 g, Color32 gd, Color32 gl)
        {
            Ground = g; GroundDark = gd; GroundLight = gl;
            Rock = new Color32((byte)(g.r - 15), (byte)(g.g - 20), (byte)(g.b - 15), 255);
            RockDark = new Color32((byte)(gd.r - 10), (byte)(gd.g - 10), (byte)(gd.b - 10), 255);
            Nest = new Color32(124, 85, 52, 255);
            NestDark = new Color32(98, 64, 40, 255);
            Food = new Color32(225, 196, 82, 255);
            FoodDark = new Color32(191, 154, 53, 255);
            Void = new Color32(28, 22, 17, 255);
        }
    }

    private static void Noise(Color32[] px, int width, int ox, int oy, int size, int seed, Color32 dark, Color32 light)
    {
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var n = Hash(seed, x, y, 7) & 31;
            if (n < 2) Set(px, width, ox + x, oy + y, dark);
            if (n > 29) Set(px, width, ox + x, oy + y, light);
        }
    }

    private static void Fill(Color32[] px, int width, int x0, int y0, int w, int h, Color32 c)
    {
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            Set(px, width, x0 + x, y0 + y, c);
    }

    private static void Disk(Color32[] px, int width, int cx, int cy, int r, Color32 c)
    {
        var rr = r * r;
        for (var y = -r; y <= r; y++)
        for (var x = -r; x <= r; x++)
            if (x * x + y * y <= rr) Set(px, width, cx + x, cy + y, c);
    }

    private static void Set(Color32[] px, int width, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0) return;
        var i = y * width + x;
        if (i < 0 || i >= px.Length) return;
        px[i] = c;
    }

    private static int Hash(int seed, int x, int y, int salt)
    {
        unchecked
        {
            var h = seed;
            h = (h * 397) ^ (x * 73856093);
            h = (h * 397) ^ (y * 19349663);
            h = (h * 397) ^ (salt * 83492791);
            return h ^ (h >> 16);
        }
    }
}
