using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AntTilesetsV1Generator
{
    private const int TileSize = 32;
    private const int PixelsPerUnit = 32;
    private const int Columns = 8;
    private const int DefaultSeed = 137531;

    private const string OutputFolder = "Assets/Generated/Tilesets/Ants";
    private const string SurfacePath = OutputFolder + "/ant_surface_tileset.png";
    private const string UndergroundPath = OutputFolder + "/ant_underground_tileset.png";

    [MenuItem("Tools/GSP/Generate Ant Tilesets")]
    public static void GenerateFromMenu() => Generate(DefaultSeed);

    public static void Generate(int seed)
    {
        EnsureFolder("Assets/Generated");
        EnsureFolder("Assets/Generated/Tilesets");
        EnsureFolder(OutputFolder);

        var surfaceTiles = BuildSurfaceTiles();
        var undergroundTiles = BuildUndergroundTiles();

        GenerateSheet(SurfacePath, surfaceTiles, seed, true);
        GenerateSheet(UndergroundPath, undergroundTiles, seed, false);

        AssetDatabase.Refresh();
        Debug.Log($"[AntTilesetsV1Generator] Generated ant tilesets with seed {seed}");
    }

    private static List<TileSpec> BuildSurfaceTiles()
    {
        var tiles = new List<TileSpec>();
        for (int i = 0; i < 4; i++) tiles.Add(new TileSpec($"surface_grass_{i}", TileCategory.SurfaceGrass, i));
        for (int i = 0; i < 3; i++) tiles.Add(new TileSpec($"surface_dirt_{i}", TileCategory.SurfaceDirt, i));
        for (int i = 0; i < 16; i++) tiles.Add(new TileSpec($"surface_transition_{i:X1}", TileCategory.SurfaceTransition, i));
        for (int i = 0; i < 8; i++) tiles.Add(new TileSpec($"surface_decor_{i}", TileCategory.SurfaceDecor, i));
        for (int i = 0; i < 2; i++) tiles.Add(new TileSpec($"surface_nest_entrance_{i}", TileCategory.SurfaceNestEntrance, i));
        return tiles;
    }

    private static List<TileSpec> BuildUndergroundTiles()
    {
        var tiles = new List<TileSpec>();
        for (int i = 0; i < 4; i++) tiles.Add(new TileSpec($"underground_dirt_{i}", TileCategory.UndergroundDirt, i));
        for (int i = 0; i < 3; i++) tiles.Add(new TileSpec($"underground_tunnel_floor_{i}", TileCategory.UndergroundTunnelFloor, i));
        for (int i = 0; i < 16; i++) tiles.Add(new TileSpec($"underground_tunnel_edge_{i:X1}", TileCategory.UndergroundTunnelEdge, i));
        for (int i = 0; i < 2; i++) tiles.Add(new TileSpec($"underground_chamber_{i}", TileCategory.UndergroundChamber, i));
        for (int i = 0; i < 6; i++) tiles.Add(new TileSpec($"underground_decor_{i}", TileCategory.UndergroundDecor, i));
        return tiles;
    }

    private static void GenerateSheet(string outputPath, IReadOnlyList<TileSpec> tiles, int seed, bool surface)
    {
        int rows = Mathf.CeilToInt(tiles.Count / (float)Columns);
        int width = Columns * TileSize;
        int height = rows * TileSize;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        for (int i = 0; i < tiles.Count; i++)
        {
            int col = i % Columns;
            int row = rows - 1 - i / Columns;
            DrawTile(pixels, width, col * TileSize, row * TileSize, seed, tiles[i], surface);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureImporter(outputPath, tiles, rows);
    }

    private static void DrawTile(Color32[] pixels, int width, int ox, int oy, int seed, TileSpec tile, bool surface)
    {
        Color32 grass = new Color32(95, 171, 84, 255);
        Color32 grassDark = new Color32(67, 127, 59, 255);
        Color32 grassLight = new Color32(126, 199, 116, 255);
        Color32 dirt = new Color32(143, 102, 62, 255);
        Color32 dirtDark = new Color32(112, 76, 45, 255);
        Color32 dirtLight = new Color32(174, 128, 82, 255);
        Color32 tunnel = new Color32(154, 118, 78, 255);
        Color32 chamber = new Color32(171, 136, 94, 255);

        FillRect(pixels, width, ox, oy, TileSize, TileSize, surface ? grass : dirt);

        switch (tile.Category)
        {
            case TileCategory.SurfaceGrass:
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 43, grassDark, grassLight);
                break;
            case TileCategory.SurfaceDirt:
                FillRect(pixels, width, ox, oy, TileSize, TileSize, dirt);
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 43, dirtDark, dirtLight);
                break;
            case TileCategory.SurfaceTransition:
                DrawTransition(pixels, width, ox, oy, seed, tile.Variant, grass, dirt, grassDark, dirtDark);
                break;
            case TileCategory.SurfaceDecor:
                DrawSurfaceDecor(pixels, width, ox, oy, seed, tile.Variant, grass, dirt);
                break;
            case TileCategory.SurfaceNestEntrance:
                DrawNestEntrance(pixels, width, ox, oy, tile.Variant, dirt, dirtDark);
                break;
            case TileCategory.UndergroundDirt:
                FillRect(pixels, width, ox, oy, TileSize, TileSize, dirt);
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 73, dirtDark, dirtLight);
                break;
            case TileCategory.UndergroundTunnelFloor:
                FillRect(pixels, width, ox, oy, TileSize, TileSize, tunnel);
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 79, dirtDark, dirtLight);
                break;
            case TileCategory.UndergroundTunnelEdge:
                DrawTunnelEdge(pixels, width, ox, oy, seed, tile.Variant, dirt, tunnel, dirtDark);
                break;
            case TileCategory.UndergroundChamber:
                FillRect(pixels, width, ox, oy, TileSize, TileSize, chamber);
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 97, dirtDark, dirtLight);
                break;
            case TileCategory.UndergroundDecor:
                FillRect(pixels, width, ox, oy, TileSize, TileSize, dirt);
                ApplyNoise(pixels, width, ox, oy, seed + tile.Variant * 107, dirtDark, dirtLight);
                DrawUndergroundDecor(pixels, width, ox, oy, seed, tile.Variant);
                break;
        }
    }

    private static void DrawTransition(Color32[] pixels, int width, int ox, int oy, int seed, int mask, Color32 grass, Color32 dirt, Color32 grassDark, Color32 dirtDark)
    {
        bool north = (mask & 1) != 0;
        bool east = (mask & 2) != 0;
        bool south = (mask & 4) != 0;
        bool west = (mask & 8) != 0;

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                float influence = 0f;
                if (north) influence = Mathf.Max(influence, Mathf.Clamp01((10f - y) / 10f));
                if (east) influence = Mathf.Max(influence, Mathf.Clamp01((x - 21f) / 10f));
                if (south) influence = Mathf.Max(influence, Mathf.Clamp01((y - 21f) / 10f));
                if (west) influence = Mathf.Max(influence, Mathf.Clamp01((10f - x) / 10f));

                int n = Hash(seed + mask * 17, x, y, 0) & 255;
                float wobble = (n - 128) / 400f;
                influence = Mathf.Clamp01(influence + wobble);

                Color32 c = Color32.Lerp(grass, dirt, influence);
                if (n < 20) c = Color32.Lerp(c, grassDark, 0.5f);
                if (n > 236) c = Color32.Lerp(c, dirtDark, 0.5f);
                SetPixel(pixels, width, ox + x, oy + y, c);
            }
        }
    }

    private static void DrawSurfaceDecor(Color32[] pixels, int width, int ox, int oy, int seed, int variant, Color32 grass, Color32 dirt)
    {
        FillRect(pixels, width, ox, oy, TileSize, TileSize, grass);
        ApplyNoise(pixels, width, ox, oy, seed + variant * 19, new Color32(57, 110, 52, 255), new Color32(128, 194, 115, 255));
        Color32 flowerA = new Color32(232, 95, 150, 255);
        Color32 flowerB = new Color32(255, 229, 115, 255);
        Color32 pebble = new Color32(139, 136, 126, 255);

        if (variant < 3)
        {
            DrawDisk(pixels, width, ox + 8 + variant * 6, oy + 18, 3, flowerA);
            DrawDisk(pixels, width, ox + 15 + variant * 3, oy + 12, 2, flowerB);
        }
        else if (variant < 6)
        {
            DrawDisk(pixels, width, ox + 11 + variant, oy + 17, 4, pebble);
            DrawDisk(pixels, width, ox + 20, oy + 12 + (variant % 2), 2, pebble);
        }
        else
        {
            DrawLine(pixels, width, ox + 6, oy + 23, ox + 24, oy + 9, new Color32(115, 82, 44, 255));
            DrawLine(pixels, width, ox + 8, oy + 24, ox + 26, oy + 11, dirt);
            DrawDisk(pixels, width, ox + 10 + variant, oy + 9 + variant, 2, new Color32(88, 127, 60, 255));
        }
    }

    private static void DrawNestEntrance(Color32[] pixels, int width, int ox, int oy, int variant, Color32 dirt, Color32 dirtDark)
    {
        FillRect(pixels, width, ox, oy, TileSize, TileSize, new Color32(93, 153, 79, 255));
        ApplyNoise(pixels, width, ox, oy, 900 + variant * 31, new Color32(60, 114, 52, 255), new Color32(122, 188, 108, 255));

        int r = variant == 0 ? 9 : 7;
        DrawDisk(pixels, width, ox + 16, oy + 16, r + 2, dirt);
        DrawDisk(pixels, width, ox + 16, oy + 16, r, dirtDark);
        DrawDisk(pixels, width, ox + 16, oy + 16, r - 3, new Color32(34, 25, 18, 255));

        if (variant == 1)
        {
            DrawLine(pixels, width, ox + 6, oy + 9, ox + 12, oy + 13, dirtDark);
            DrawLine(pixels, width, ox + 20, oy + 19, ox + 27, oy + 24, dirtDark);
        }
    }

    private static void DrawTunnelEdge(Color32[] pixels, int width, int ox, int oy, int seed, int mask, Color32 dirt, Color32 tunnel, Color32 dirtDark)
    {
        bool north = (mask & 1) != 0;
        bool east = (mask & 2) != 0;
        bool south = (mask & 4) != 0;
        bool west = (mask & 8) != 0;

        FillRect(pixels, width, ox, oy, TileSize, TileSize, dirt);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                float influence = 0f;
                if (north) influence = Mathf.Max(influence, Mathf.Clamp01((10f - y) / 10f));
                if (east) influence = Mathf.Max(influence, Mathf.Clamp01((x - 21f) / 10f));
                if (south) influence = Mathf.Max(influence, Mathf.Clamp01((y - 21f) / 10f));
                if (west) influence = Mathf.Max(influence, Mathf.Clamp01((10f - x) / 10f));

                int n = Hash(seed + mask * 23, x, y, 2) & 255;
                influence = Mathf.Clamp01(influence + (n - 128) / 450f);
                Color32 c = Color32.Lerp(dirt, tunnel, influence);
                if (n < 18) c = Color32.Lerp(c, dirtDark, 0.6f);
                SetPixel(pixels, width, ox + x, oy + y, c);
            }
        }
    }

    private static void DrawUndergroundDecor(Color32[] pixels, int width, int ox, int oy, int seed, int variant)
    {
        Color32 rock = new Color32(133, 128, 119, 255);
        Color32 root = new Color32(108, 74, 44, 255);

        if (variant % 2 == 0)
        {
            DrawDisk(pixels, width, ox + 10 + (variant % 3) * 3, oy + 17, 4, rock);
            DrawDisk(pixels, width, ox + 19, oy + 12 + (variant % 2), 3, rock);
        }
        else
        {
            DrawLine(pixels, width, ox + 5, oy + 7 + variant, ox + 25, oy + 24 - variant, root);
            DrawLine(pixels, width, ox + 8, oy + 8 + variant, ox + 27, oy + 25 - variant, new Color32(129, 92, 55, 255));
        }

        int sparkle = Hash(seed, variant, 0, 9) & 7;
        DrawDisk(pixels, width, ox + 5 + sparkle * 3, oy + 6 + sparkle * 2, 1, new Color32(186, 164, 129, 255));
    }

    private static void ApplyNoise(Color32[] pixels, int width, int ox, int oy, int seed, Color32 dark, Color32 light)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = Hash(seed, x, y, 1) & 31;
                if (n <= 1) SetPixel(pixels, width, ox + x, oy + y, dark);
                if (n >= 30) SetPixel(pixels, width, ox + x, oy + y, light);
            }
        }
    }

    private static void ConfigureImporter(string path, IReadOnlyList<TileSpec> tiles, int rows)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            Debug.LogError($"Unable to configure importer for {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var meta = new List<SpriteMetaData>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            int col = i % Columns;
            int row = rows - 1 - i / Columns;
            meta.Add(new SpriteMetaData
            {
                name = tiles[i].Name,
                rect = new Rect(col * TileSize, row * TileSize, TileSize, TileSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        importer.spritesheet = meta.ToArray();
        importer.SaveAndReimport();
    }

    private static void FillRect(Color32[] pixels, int width, int x0, int y0, int w, int h, Color32 c)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                SetPixel(pixels, width, x0 + x, y0 + y, c);
            }
        }
    }

    private static void DrawDisk(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        int rr = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= rr)
                {
                    SetPixel(pixels, width, cx + x, cy + y, color);
                }
            }
        }
    }

    private static void DrawLine(Color32[] pixels, int width, int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            SetPixel(pixels, width, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(Color32[] pixels, int width, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0) return;
        int idx = y * width + x;
        if (idx < 0 || idx >= pixels.Length) return;
        pixels[idx] = c;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static int Hash(int seed, int x, int y, int salt)
    {
        unchecked
        {
            int h = seed;
            h = (h * 397) ^ (x * 73856093);
            h = (h * 397) ^ (y * 19349663);
            h = (h * 397) ^ (salt * 83492791);
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return h;
        }
    }

    private readonly struct TileSpec
    {
        public readonly string Name;
        public readonly TileCategory Category;
        public readonly int Variant;

        public TileSpec(string name, TileCategory category, int variant)
        {
            Name = name;
            Category = category;
            Variant = variant;
        }
    }

    private enum TileCategory
    {
        SurfaceGrass,
        SurfaceDirt,
        SurfaceTransition,
        SurfaceDecor,
        SurfaceNestEntrance,
        UndergroundDirt,
        UndergroundTunnelFloor,
        UndergroundTunnelEdge,
        UndergroundChamber,
        UndergroundDecor
    }
}
