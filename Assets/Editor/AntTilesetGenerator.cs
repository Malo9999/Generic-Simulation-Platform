using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class AntTilesetGenerator
{
    private const int TileSizePx = 32;
    private const int PixelsPerUnit = 32;
    private const int Seed = 472891;

    private const string RootFolder = "Assets/Generated";
    private const string TilesetFolder = "Assets/Generated/Tilesets";
    private const string AntsFolder = "Assets/Generated/Tilesets/Ants";
    private const string SurfacePath = AntsFolder + "/ant_surface_tileset.png";
    private const string UndergroundPath = AntsFolder + "/ant_underground_tileset.png";
    private const string IndexMapPath = AntsFolder + "/AntTilesetSpriteIndexMap.asset";

    private static readonly string[] SurfaceTileNames =
    {
        "grass_plain",
        "grass_alt_a",
        "grass_alt_b",
        "path_straight_h",
        "path_straight_v",
        "path_corner_ne",
        "path_corner_nw",
        "path_cross",
        "decor_clover",
        "decor_pebble",
        "decor_flower",
        "nest_entrance_marker"
    };

    private static readonly string[] UndergroundTileNames =
    {
        "dirt_plain",
        "dirt_alt_a",
        "dirt_alt_b",
        "tunnel_h",
        "tunnel_v",
        "tunnel_corner_ne",
        "tunnel_corner_nw",
        "tunnel_t_junction",
        "tunnel_cross",
        "chamber_small",
        "chamber_medium",
        "chamber_queen_marker"
    };

    public static void GenerateAntTilesets()
    {
        EnsureFolderHierarchy();

        GenerateTilesheet(SurfacePath, SurfaceTileNames, AntTilesheetKind.Surface);
        GenerateTilesheet(UndergroundPath, UndergroundTileNames, AntTilesheetKind.Underground);
        CreateOrUpdateIndexMap();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Ant tilesets generated with deterministic seed {Seed}.\n- {SurfacePath}\n- {UndergroundPath}");
    }

    private static void GenerateTilesheet(string texturePath, IReadOnlyList<string> tileNames, AntTilesheetKind sheet)
    {
        const int columns = 4;
        int rows = Mathf.CeilToInt(tileNames.Count / (float)columns);

        int width = columns * TileSizePx;
        int height = rows * TileSizePx;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 0);
        }

        for (int index = 0; index < tileNames.Count; index++)
        {
            int col = index % columns;
            int row = rows - 1 - index / columns;
            int originX = col * TileSizePx;
            int originY = row * TileSizePx;

            DrawTile(pixels, width, originX, originY, index, tileNames[index], sheet);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImporter(texturePath, tileNames, columns, rows);
    }

    private static void ConfigureTextureImporter(string texturePath, IReadOnlyList<string> tileNames, int columns, int rows)
    {
        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
        {
            Debug.LogError($"Failed to load importer at '{texturePath}'.");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;

        var dataProviderFactories = new SpriteDataProviderFactories();
        dataProviderFactories.Init();

        var spriteEditorDataProvider = dataProviderFactories.GetSpriteEditorDataProviderFromObject(importer);
        spriteEditorDataProvider.InitSpriteEditorDataProvider();

        var spriteRectDataProvider = spriteEditorDataProvider.GetDataProvider<ISpriteRectDataProvider>();
        var spriteRects = new List<SpriteRect>(tileNames.Count);
        for (int index = 0; index < tileNames.Count; index++)
        {
            int col = index % columns;
            int row = rows - 1 - index / columns;

            var spriteRect = new SpriteRect
            {
                name = tileNames[index],
                rect = new Rect(col * TileSizePx, row * TileSizePx, TileSizePx, TileSizePx),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            };

            spriteRects.Add(spriteRect);
        }

        spriteRectDataProvider.SetSpriteRects(spriteRects.ToArray());
        spriteEditorDataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static void CreateOrUpdateIndexMap()
    {
        var entries = new List<AntTilesetSpriteIndexMap.Entry>(SurfaceTileNames.Length + UndergroundTileNames.Length);

        for (int i = 0; i < SurfaceTileNames.Length; i++)
        {
            entries.Add(new AntTilesetSpriteIndexMap.Entry
            {
                tileName = SurfaceTileNames[i],
                sheet = AntTilesheetKind.Surface,
                spriteIndex = i
            });
        }

        for (int i = 0; i < UndergroundTileNames.Length; i++)
        {
            entries.Add(new AntTilesetSpriteIndexMap.Entry
            {
                tileName = UndergroundTileNames[i],
                sheet = AntTilesheetKind.Underground,
                spriteIndex = i
            });
        }

        var map = AssetDatabase.LoadAssetAtPath<AntTilesetSpriteIndexMap>(IndexMapPath);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<AntTilesetSpriteIndexMap>();
            map.SetEntries(entries);
            AssetDatabase.CreateAsset(map, IndexMapPath);
        }
        else
        {
            map.SetEntries(entries);
            EditorUtility.SetDirty(map);
        }
    }

    private static void DrawTile(Color32[] pixels, int width, int originX, int originY, int tileIndex, string tileName, AntTilesheetKind sheet)
    {
        if (sheet == AntTilesheetKind.Surface)
        {
            DrawSurfaceTile(pixels, width, originX, originY, tileIndex, tileName);
            return;
        }

        DrawUndergroundTile(pixels, width, originX, originY, tileIndex, tileName);
    }

    private static void DrawSurfaceTile(Color32[] pixels, int width, int originX, int originY, int tileIndex, string tileName)
    {
        Color32 grassBase = new Color32(64, 146, 58, 255);
        Color32 grassShade = new Color32(48, 112, 46, 255);
        Color32 grassLight = new Color32(91, 181, 79, 255);
        Color32 pathBase = new Color32(166, 119, 62, 255);
        Color32 pathEdge = new Color32(130, 89, 48, 255);
        Color32 pathDust = new Color32(191, 143, 83, 255);

        FillRect(pixels, width, originX, originY, TileSizePx, TileSizePx, grassBase);

        for (int y = 0; y < TileSizePx; y++)
        {
            for (int x = 0; x < TileSizePx; x++)
            {
                int n = Hash(Seed + tileIndex * 97, x, y, 1) & 15;
                if (n < 2)
                {
                    SetPixel(pixels, width, originX + x, originY + y, grassLight);
                }
                else if (n == 15)
                {
                    SetPixel(pixels, width, originX + x, originY + y, grassShade);
                }
            }
        }

        switch (tileName)
        {
            case "path_straight_h":
                DrawPathHorizontal(pixels, width, originX, originY, pathBase, pathEdge, pathDust);
                break;
            case "path_straight_v":
                DrawPathVertical(pixels, width, originX, originY, pathBase, pathEdge, pathDust);
                break;
            case "path_corner_ne":
                DrawPathVertical(pixels, width, originX, originY, pathBase, pathEdge, pathDust, true);
                DrawPathHorizontal(pixels, width, originX, originY, pathBase, pathEdge, pathDust, true);
                break;
            case "path_corner_nw":
                DrawPathVertical(pixels, width, originX, originY, pathBase, pathEdge, pathDust, true);
                DrawPathHorizontal(pixels, width, originX, originY, pathBase, pathEdge, pathDust, false, true);
                break;
            case "path_cross":
                DrawPathHorizontal(pixels, width, originX, originY, pathBase, pathEdge, pathDust);
                DrawPathVertical(pixels, width, originX, originY, pathBase, pathEdge, pathDust);
                break;
            case "decor_clover":
                DrawCircle(pixels, width, originX + 11, originY + 15, 4, new Color32(36, 109, 44, 255));
                DrawCircle(pixels, width, originX + 16, originY + 10, 4, new Color32(36, 109, 44, 255));
                DrawCircle(pixels, width, originX + 21, originY + 15, 4, new Color32(36, 109, 44, 255));
                DrawLine(pixels, width, originX + 16, originY + 15, originX + 16, originY + 24, new Color32(48, 83, 43, 255));
                break;
            case "decor_pebble":
                DrawCircle(pixels, width, originX + 10, originY + 18, 5, new Color32(129, 126, 114, 255));
                DrawCircle(pixels, width, originX + 19, originY + 14, 4, new Color32(155, 151, 138, 255));
                DrawCircle(pixels, width, originX + 22, originY + 21, 3, new Color32(117, 114, 102, 255));
                break;
            case "decor_flower":
                DrawCircle(pixels, width, originX + 16, originY + 16, 3, new Color32(238, 212, 66, 255));
                DrawCircle(pixels, width, originX + 12, originY + 16, 3, new Color32(209, 92, 161, 255));
                DrawCircle(pixels, width, originX + 20, originY + 16, 3, new Color32(209, 92, 161, 255));
                DrawCircle(pixels, width, originX + 16, originY + 12, 3, new Color32(209, 92, 161, 255));
                DrawCircle(pixels, width, originX + 16, originY + 20, 3, new Color32(209, 92, 161, 255));
                break;
            case "nest_entrance_marker":
                DrawCircle(pixels, width, originX + 16, originY + 16, 10, new Color32(138, 95, 48, 255));
                DrawCircle(pixels, width, originX + 16, originY + 16, 7, new Color32(101, 67, 33, 255));
                DrawCircle(pixels, width, originX + 16, originY + 16, 4, new Color32(33, 24, 17, 255));
                break;
        }
    }

    private static void DrawUndergroundTile(Color32[] pixels, int width, int originX, int originY, int tileIndex, string tileName)
    {
        Color32 dirtBase = new Color32(110, 79, 48, 255);
        Color32 dirtShade = new Color32(84, 58, 35, 255);
        Color32 dirtLight = new Color32(136, 98, 60, 255);
        Color32 tunnelBase = new Color32(66, 44, 28, 255);
        Color32 tunnelEdge = new Color32(46, 30, 20, 255);
        Color32 chamberAccent = new Color32(157, 113, 72, 255);

        FillRect(pixels, width, originX, originY, TileSizePx, TileSizePx, dirtBase);

        for (int y = 0; y < TileSizePx; y++)
        {
            for (int x = 0; x < TileSizePx; x++)
            {
                int n = Hash(Seed + tileIndex * 131, x, y, 7) & 31;
                if (n < 3)
                {
                    SetPixel(pixels, width, originX + x, originY + y, dirtLight);
                }
                else if (n == 31)
                {
                    SetPixel(pixels, width, originX + x, originY + y, dirtShade);
                }
            }
        }

        switch (tileName)
        {
            case "tunnel_h":
                DrawTunnelHorizontal(pixels, width, originX, originY, tunnelBase, tunnelEdge);
                break;
            case "tunnel_v":
                DrawTunnelVertical(pixels, width, originX, originY, tunnelBase, tunnelEdge);
                break;
            case "tunnel_corner_ne":
                DrawTunnelVertical(pixels, width, originX, originY, tunnelBase, tunnelEdge, true);
                DrawTunnelHorizontal(pixels, width, originX, originY, tunnelBase, tunnelEdge, true);
                break;
            case "tunnel_corner_nw":
                DrawTunnelVertical(pixels, width, originX, originY, tunnelBase, tunnelEdge, true);
                DrawTunnelHorizontal(pixels, width, originX, originY, tunnelBase, tunnelEdge, false, true);
                break;
            case "tunnel_t_junction":
                DrawTunnelVertical(pixels, width, originX, originY, tunnelBase, tunnelEdge, true);
                DrawTunnelHorizontal(pixels, width, originX, originY, tunnelBase, tunnelEdge);
                break;
            case "tunnel_cross":
                DrawTunnelVertical(pixels, width, originX, originY, tunnelBase, tunnelEdge);
                DrawTunnelHorizontal(pixels, width, originX, originY, tunnelBase, tunnelEdge);
                break;
            case "chamber_small":
                DrawCircle(pixels, width, originX + 16, originY + 16, 9, tunnelBase);
                DrawCircleOutline(pixels, width, originX + 16, originY + 16, 10, tunnelEdge);
                break;
            case "chamber_medium":
                DrawCircle(pixels, width, originX + 16, originY + 16, 12, tunnelBase);
                DrawCircleOutline(pixels, width, originX + 16, originY + 16, 13, tunnelEdge);
                for (int i = 0; i < 14; i += 2)
                {
                    SetPixel(pixels, width, originX + 9 + i, originY + 16, chamberAccent);
                }
                break;
            case "chamber_queen_marker":
                DrawCircle(pixels, width, originX + 16, originY + 16, 11, tunnelBase);
                DrawCircleOutline(pixels, width, originX + 16, originY + 16, 12, tunnelEdge);
                DrawDiamond(pixels, width, originX + 16, originY + 16, 5, chamberAccent);
                break;
        }
    }

    private static void DrawPathHorizontal(Color32[] pixels, int width, int originX, int originY, Color32 fill, Color32 edge, Color32 dust, bool topHalfOnly = false, bool leftHalfOnly = false)
    {
        int yMin = topHalfOnly ? TileSizePx / 2 : 10;
        int yMax = 22;
        int xMin = leftHalfOnly ? 0 : 3;
        int xMax = leftHalfOnly ? TileSizePx / 2 : TileSizePx - 3;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var color = (y == yMin || y == yMax - 1) ? edge : fill;
                if (Hash(Seed, x, y, 21) % 11 == 0)
                {
                    color = dust;
                }

                SetPixel(pixels, width, originX + x, originY + y, color);
            }
        }
    }

    private static void DrawPathVertical(Color32[] pixels, int width, int originX, int originY, Color32 fill, Color32 edge, Color32 dust, bool topHalfOnly = false)
    {
        int xMin = 10;
        int xMax = 22;
        int yMin = topHalfOnly ? TileSizePx / 2 : 3;
        int yMax = TileSizePx - 3;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var color = (x == xMin || x == xMax - 1) ? edge : fill;
                if (Hash(Seed, x, y, 23) % 11 == 0)
                {
                    color = dust;
                }

                SetPixel(pixels, width, originX + x, originY + y, color);
            }
        }
    }

    private static void DrawTunnelHorizontal(Color32[] pixels, int width, int originX, int originY, Color32 fill, Color32 edge, bool topHalfOnly = false, bool leftHalfOnly = false)
    {
        int yMin = topHalfOnly ? TileSizePx / 2 : 11;
        int yMax = 21;
        int xMin = leftHalfOnly ? 0 : 2;
        int xMax = leftHalfOnly ? TileSizePx / 2 : TileSizePx - 2;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var color = (y == yMin || y == yMax - 1) ? edge : fill;
                SetPixel(pixels, width, originX + x, originY + y, color);
            }
        }
    }

    private static void DrawTunnelVertical(Color32[] pixels, int width, int originX, int originY, Color32 fill, Color32 edge, bool topHalfOnly = false)
    {
        int xMin = 11;
        int xMax = 21;
        int yMin = topHalfOnly ? TileSizePx / 2 : 2;
        int yMax = TileSizePx - 2;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var color = (x == xMin || x == xMax - 1) ? edge : fill;
                SetPixel(pixels, width, originX + x, originY + y, color);
            }
        }
    }

    private static void DrawCircle(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        int rSquared = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= rSquared)
                {
                    SetPixel(pixels, width, cx + x, cy + y, color);
                }
            }
        }
    }

    private static void DrawCircleOutline(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        int outer = radius * radius;
        int inner = (radius - 1) * (radius - 1);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int d = x * x + y * y;
                if (d <= outer && d >= inner)
                {
                    SetPixel(pixels, width, cx + x, cy + y, color);
                }
            }
        }
    }

    private static void DrawDiamond(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            int rowRadius = radius - Mathf.Abs(y);
            for (int x = -rowRadius; x <= rowRadius; x++)
            {
                SetPixel(pixels, width, cx + x, cy + y, color);
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
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

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

    private static void FillRect(Color32[] pixels, int width, int x, int y, int rectWidth, int rectHeight, Color32 color)
    {
        for (int yPos = y; yPos < y + rectHeight; yPos++)
        {
            for (int xPos = x; xPos < x + rectWidth; xPos++)
            {
                SetPixel(pixels, width, xPos, yPos, color);
            }
        }
    }

    private static void SetPixel(Color32[] pixels, int width, int x, int y, Color32 color)
    {
        int height = pixels.Length / width;
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        int index = y * width + x;
        pixels[index] = color;
    }

    private static int Hash(int seed, int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)(x * 374761393);
            h ^= (uint)(y * 668265263);
            h ^= (uint)salt * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return (int)(h ^ (h >> 16));
        }
    }

    private static void EnsureFolderHierarchy()
    {
        EnsureFolder("Assets", "Generated");
        EnsureFolder(RootFolder, "Tilesets");
        EnsureFolder(TilesetFolder, "Ants");
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        string fullPath = $"{parentPath}/{folderName}";
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
