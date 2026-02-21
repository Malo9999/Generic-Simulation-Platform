using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AntTilesetGenerator
{
    private const int TileSize = 32;
    private const int SurfaceColumns = 8;
    private const int UndergroundColumns = 8;
    private const string OutputFolder = "Assets/Generated/Tilesets/Ants";
    private const string SurfacePath = OutputFolder + "/ant_surface_tileset.png";
    private const string UndergroundPath = OutputFolder + "/ant_underground_tileset.png";
    private const string SeedPrefKey = "GSP.AntTilesets.Seed";

    [MenuItem("Tools/GSP/Generate Ant Tilesets")]
    public static void GenerateFromMenu()
    {
        var seed = EditorPrefs.GetInt(SeedPrefKey, 1337);
        GenerateAll(seed);
    }

    [MenuItem("Tools/GSP/Set Ant Tileset Seed")]
    public static void SetSeed()
    {
        AntTilesetSeedWindow.ShowWindow();
    }

    public static void GenerateAll(int seed)
    {
        Directory.CreateDirectory(OutputFolder);

        var surface = BuildSurfaceTiles(seed);
        var underground = BuildUndergroundTiles(seed + 92821);

        WriteTilesheet(surface, SurfaceColumns, SurfacePath);
        WriteTilesheet(underground, UndergroundColumns, UndergroundPath);

        AssetDatabase.Refresh();
        ConfigureImporter(SurfacePath, surface, SurfaceColumns);
        ConfigureImporter(UndergroundPath, underground, UndergroundColumns);

        EditorPrefs.SetInt(SeedPrefKey, seed);
        Debug.Log($"Generated ant tilesets with seed {seed}:\n{SurfacePath}\n{UndergroundPath}");
    }

    private static List<TileData> BuildSurfaceTiles(int seed)
    {
        var rng = new System.Random(seed);
        var tiles = new List<TileData>();

        for (var i = 0; i < 4; i++)
        {
            tiles.Add(new TileData($"surface_grass_{i:D2}", MakeGrassTile(rng)));
        }

        for (var i = 0; i < 3; i++)
        {
            tiles.Add(new TileData($"surface_dirt_{i:D2}", MakeDirtTile(rng, false)));
        }

        for (var mask = 0; mask < 16; mask++)
        {
            tiles.Add(new TileData($"surface_transition_{mask:D2}", MakeSurfaceTransitionTile(rng, mask)));
        }

        for (var i = 0; i < 8; i++)
        {
            tiles.Add(new TileData($"surface_decor_{i:D2}", MakeSurfaceDecorTile(rng, i)));
        }

        for (var i = 0; i < 2; i++)
        {
            tiles.Add(new TileData($"surface_nest_{i:D2}", MakeNestEntranceTile(rng, i)));
        }

        return tiles;
    }

    private static List<TileData> BuildUndergroundTiles(int seed)
    {
        var rng = new System.Random(seed);
        var tiles = new List<TileData>();

        for (var i = 0; i < 4; i++)
        {
            tiles.Add(new TileData($"underground_dirt_{i:D2}", MakeDirtTile(rng, true)));
        }

        for (var i = 0; i < 3; i++)
        {
            tiles.Add(new TileData($"underground_tunnel_floor_{i:D2}", MakeTunnelFloorTile(rng)));
        }

        for (var mask = 0; mask < 16; mask++)
        {
            tiles.Add(new TileData($"underground_tunnel_wall_{mask:D2}", MakeTunnelWallTile(rng, mask)));
        }

        for (var i = 0; i < 2; i++)
        {
            tiles.Add(new TileData($"underground_chamber_{i:D2}", MakeChamberFloorTile(rng)));
        }

        for (var i = 0; i < 6; i++)
        {
            tiles.Add(new TileData($"underground_decor_{i:D2}", MakeUndergroundDecorTile(rng, i)));
        }

        return tiles;
    }

    private static void WriteTilesheet(IReadOnlyList<TileData> tiles, int columns, string outputPath)
    {
        var rows = Mathf.CeilToInt((float)tiles.Count / columns);
        var texture = new Texture2D(columns * TileSize, rows * TileSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var clearPixels = new Color32[texture.width * texture.height];
        for (var i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = new Color32(0, 0, 0, 0);
        }

        texture.SetPixels32(clearPixels);

        for (var index = 0; index < tiles.Count; index++)
        {
            var x = (index % columns) * TileSize;
            var y = (rows - 1 - index / columns) * TileSize;
            texture.SetPixels32(x, y, TileSize, TileSize, tiles[index].Pixels);
        }

        texture.Apply(false, false);
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureImporter(string assetPath, IReadOnlyList<TileData> tiles, int columns)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;

        var rows = Mathf.CeilToInt((float)tiles.Count / columns);
        var metas = new List<SpriteMetaData>();

        for (var i = 0; i < tiles.Count; i++)
        {
            var col = i % columns;
            var row = rows - 1 - i / columns;
            metas.Add(new SpriteMetaData
            {
                name = tiles[i].Name,
                rect = new Rect(col * TileSize, row * TileSize, TileSize, TileSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        importer.spritesheet = metas.ToArray();
        importer.SaveAndReimport();
    }

    private static Color32[] MakeGrassTile(System.Random rng)
    {
        var tile = NewTile(new Color32(80, 145, 72, 255));
        Scatter(tile, rng, new Color32(88, 160, 78, 255), 140);
        Scatter(tile, rng, new Color32(64, 118, 56, 255), 95);
        Scatter(tile, rng, new Color32(122, 188, 98, 255), 30);
        return tile;
    }

    private static Color32[] MakeDirtTile(System.Random rng, bool underground)
    {
        var baseColor = underground ? new Color32(101, 74, 46, 255) : new Color32(122, 88, 52, 255);
        var tile = NewTile(baseColor);
        Scatter(tile, rng, Add(baseColor, 10, 8, 4), 95);
        Scatter(tile, rng, Add(baseColor, -16, -12, -8), 80);
        Scatter(tile, rng, Add(baseColor, -30, -22, -18), 26);
        return tile;
    }

    private static Color32[] MakeSurfaceTransitionTile(System.Random rng, int mask)
    {
        var grass = MakeGrassTile(rng);
        var dirt = MakeDirtTile(rng, false);
        var center = new Vector2(15.5f, 15.5f);

        var hasTop = (mask & 1) != 0;
        var hasRight = (mask & 2) != 0;
        var hasBottom = (mask & 4) != 0;
        var hasLeft = (mask & 8) != 0;

        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var idx = Index(x, y);
                var pull = 0f;

                if (hasTop)
                {
                    pull += Mathf.Clamp01((y - 12f) / 20f);
                }

                if (hasBottom)
                {
                    pull += Mathf.Clamp01((20f - y) / 20f);
                }

                if (hasRight)
                {
                    pull += Mathf.Clamp01((x - 12f) / 20f);
                }

                if (hasLeft)
                {
                    pull += Mathf.Clamp01((20f - x) / 20f);
                }

                if (Vector2.Distance(new Vector2(x, y), center) < 9f && (mask == 0 || mask == 15))
                {
                    pull += 0.35f;
                }

                if (pull > 0.65f)
                {
                    grass[idx] = dirt[idx];
                }
                else if (pull > 0.45f)
                {
                    grass[idx] = Color.Lerp(grass[idx], dirt[idx], 0.5f);
                }
            }
        }

        return grass;
    }

    private static Color32[] MakeSurfaceDecorTile(System.Random rng, int variant)
    {
        var tile = MakeGrassTile(rng);

        if (variant < 3)
        {
            for (var i = 0; i < 3 + variant; i++)
            {
                DrawFlower(tile, rng.Next(4, 28), rng.Next(5, 27), rng);
            }
        }
        else if (variant < 6)
        {
            for (var i = 0; i < 4 + variant; i++)
            {
                DrawPebble(tile, rng.Next(3, 29), rng.Next(4, 28), rng);
            }
        }
        else
        {
            for (var i = 0; i < 7 + variant; i++)
            {
                DrawLeaf(tile, rng.Next(3, 29), rng.Next(3, 29), rng);
            }
        }

        return tile;
    }

    private static Color32[] MakeNestEntranceTile(System.Random rng, int variant)
    {
        var tile = MakeDirtTile(rng, false);
        var holeColor = new Color32(54, 35, 20, 255);
        var ringColor = new Color32(142, 100, 60, 255);

        var cx = 16 + (variant == 0 ? -2 : 2);
        var cy = variant == 0 ? 14 : 17;
        var radius = variant == 0 ? 8 : 7;

        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (dist <= radius)
                {
                    tile[Index(x, y)] = holeColor;
                }
                else if (dist <= radius + 1.5f)
                {
                    tile[Index(x, y)] = ringColor;
                }
            }
        }

        Scatter(tile, rng, Add(ringColor, 18, 12, 8), 26);
        return tile;
    }

    private static Color32[] MakeTunnelFloorTile(System.Random rng)
    {
        var tile = NewTile(new Color32(122, 90, 58, 255));
        Scatter(tile, rng, new Color32(140, 104, 64, 255), 110);
        Scatter(tile, rng, new Color32(94, 67, 43, 255), 90);
        return tile;
    }

    private static Color32[] MakeTunnelWallTile(System.Random rng, int mask)
    {
        var wall = MakeDirtTile(rng, true);
        var floor = MakeTunnelFloorTile(rng);

        var hasTop = (mask & 1) != 0;
        var hasRight = (mask & 2) != 0;
        var hasBottom = (mask & 4) != 0;
        var hasLeft = (mask & 8) != 0;

        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var edge = 0f;

                if (hasTop)
                {
                    edge += Mathf.Clamp01((y - 10f) / 22f);
                }

                if (hasBottom)
                {
                    edge += Mathf.Clamp01((22f - y) / 22f);
                }

                if (hasRight)
                {
                    edge += Mathf.Clamp01((x - 10f) / 22f);
                }

                if (hasLeft)
                {
                    edge += Mathf.Clamp01((22f - x) / 22f);
                }

                var idx = Index(x, y);
                if (edge > 0.6f)
                {
                    wall[idx] = floor[idx];
                }
                else if (edge > 0.42f)
                {
                    wall[idx] = Color.Lerp(wall[idx], floor[idx], 0.45f);
                }
            }
        }

        return wall;
    }

    private static Color32[] MakeChamberFloorTile(System.Random rng)
    {
        var tile = NewTile(new Color32(133, 98, 64, 255));
        Scatter(tile, rng, new Color32(154, 116, 75, 255), 130);
        Scatter(tile, rng, new Color32(100, 74, 46, 255), 85);

        for (var i = 0; i < 5; i++)
        {
            DrawPebble(tile, rng.Next(4, 28), rng.Next(4, 28), rng);
        }

        return tile;
    }

    private static Color32[] MakeUndergroundDecorTile(System.Random rng, int variant)
    {
        var tile = MakeDirtTile(rng, true);

        if (variant % 2 == 0)
        {
            for (var i = 0; i < 7 + variant; i++)
            {
                DrawPebble(tile, rng.Next(3, 29), rng.Next(3, 29), rng);
            }
        }
        else
        {
            for (var i = 0; i < 2 + variant; i++)
            {
                DrawRoot(tile, rng.Next(2, 12), rng.Next(8, 24), rng);
            }
        }

        return tile;
    }

    private static void DrawFlower(Color32[] tile, int x, int y, System.Random rng)
    {
        var center = new Color32(238, 191, 84, 255);
        var petals = rng.Next(0, 3) switch
        {
            0 => new Color32(230, 90, 104, 255),
            1 => new Color32(246, 225, 145, 255),
            _ => new Color32(186, 212, 242, 255)
        };

        SetPixel(tile, x, y, center);
        SetPixel(tile, x + 1, y, petals);
        SetPixel(tile, x - 1, y, petals);
        SetPixel(tile, x, y + 1, petals);
        SetPixel(tile, x, y - 1, petals);
    }

    private static void DrawPebble(Color32[] tile, int x, int y, System.Random rng)
    {
        var baseColor = rng.Next(0, 2) == 0 ? new Color32(162, 162, 152, 255) : new Color32(142, 137, 128, 255);
        SetPixel(tile, x, y, baseColor);
        SetPixel(tile, x + 1, y, Add(baseColor, -14, -14, -14));
        SetPixel(tile, x, y + 1, Add(baseColor, 12, 12, 12));
    }

    private static void DrawLeaf(Color32[] tile, int x, int y, System.Random rng)
    {
        var leaf = rng.Next(0, 2) == 0 ? new Color32(100, 155, 70, 255) : new Color32(137, 122, 64, 255);
        SetPixel(tile, x, y, leaf);
        SetPixel(tile, x + 1, y + 1, Add(leaf, -12, -12, -12));
        SetPixel(tile, x - 1, y - 1, Add(leaf, 12, 12, 12));
    }

    private static void DrawRoot(Color32[] tile, int x, int y, System.Random rng)
    {
        var root = new Color32(115, 82, 54, 255);
        var length = rng.Next(10, 18);
        var slope = rng.Next(-1, 2);

        for (var i = 0; i < length; i++)
        {
            var px = x + i;
            var py = y + slope * (i / 4);
            SetPixel(tile, px, py, root);
            SetPixel(tile, px, py + 1, Add(root, 10, 8, 6));
        }
    }

    private static Color32[] NewTile(Color32 fill)
    {
        var pixels = new Color32[TileSize * TileSize];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = fill;
        }

        return pixels;
    }

    private static void Scatter(Color32[] tile, System.Random rng, Color32 color, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var x = rng.Next(0, TileSize);
            var y = rng.Next(0, TileSize);
            tile[Index(x, y)] = color;
        }
    }

    private static void SetPixel(Color32[] tile, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= TileSize || y >= TileSize)
        {
            return;
        }

        tile[Index(x, y)] = color;
    }

    private static int Index(int x, int y)
    {
        return y * TileSize + x;
    }

    private static Color32 Add(Color32 c, int r, int g, int b)
    {
        return new Color32(
            (byte)Mathf.Clamp(c.r + r, 0, 255),
            (byte)Mathf.Clamp(c.g + g, 0, 255),
            (byte)Mathf.Clamp(c.b + b, 0, 255),
            c.a);
    }

    private readonly struct TileData
    {
        public TileData(string name, Color32[] pixels)
        {
            Name = name;
            Pixels = pixels;
        }

        public string Name { get; }
        public Color32[] Pixels { get; }
    }
}

public class AntTilesetSeedWindow : EditorWindow
{
    private int _seed;

    public static void ShowWindow()
    {
        var window = GetWindow<AntTilesetSeedWindow>("Ant Tileset Seed");
        window.minSize = new Vector2(280f, 90f);
        window.Show();
    }

    private void OnEnable()
    {
        _seed = EditorPrefs.GetInt("GSP.AntTilesets.Seed", 1337);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Set deterministic seed for ant tilesets", EditorStyles.wordWrappedLabel);
        _seed = EditorGUILayout.IntField("Seed", _seed);

        GUILayout.Space(10f);
        if (GUILayout.Button("Save"))
        {
            EditorPrefs.SetInt("GSP.AntTilesets.Seed", _seed);
            Close();
        }
    }
}
