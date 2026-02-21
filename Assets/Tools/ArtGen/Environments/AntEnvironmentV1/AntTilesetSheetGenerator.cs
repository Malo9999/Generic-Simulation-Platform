using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class AntTilesetSheetGenerator
{
    private readonly struct SheetTextureResult
    {
        public readonly Texture2D Texture;
        public readonly List<Sprite> Sprites;

        public SheetTextureResult(Texture2D texture, List<Sprite> sprites)
        {
            Texture = texture;
            Sprites = sprites;
        }
    }

    private static readonly string[] SurfaceTileNames =
    {
        "grass_plain", "grass_alt_a", "grass_alt_b", "path_straight_h",
        "path_straight_v", "path_corner_ne", "path_corner_nw", "path_cross",
        "decor_clover", "decor_pebble", "decor_flower", "nest_entrance_marker"
    };

    private static readonly string[] UndergroundTileNames =
    {
        "dirt_plain", "dirt_alt_a", "dirt_alt_b", "tunnel_h",
        "tunnel_v", "tunnel_corner_ne", "tunnel_corner_nw", "tunnel_t_junction",
        "tunnel_cross", "chamber_small", "chamber_medium", "chamber_queen_marker"
    };

    public readonly struct TileGenerationResult
    {
        public readonly Texture2D SurfaceTexture;
        public readonly Texture2D UndergroundTexture;
        public readonly List<Sprite> SurfaceSprites;
        public readonly List<Sprite> UndergroundSprites;

        public TileGenerationResult(Texture2D surfaceTexture, Texture2D undergroundTexture, List<Sprite> surfaceSprites, List<Sprite> undergroundSprites)
        {
            SurfaceTexture = surfaceTexture;
            UndergroundTexture = undergroundTexture;
            SurfaceSprites = surfaceSprites;
            UndergroundSprites = undergroundSprites;
        }
    }

    public static TileGenerationResult Generate(string outputFolder, int seed, int tileSize, AntPalettePreset palettePreset, bool overwrite)
    {
        var surfacePath = $"{outputFolder}/tiles_surface.png";
        var undergroundPath = $"{outputFolder}/tiles_underground.png";

        var surfaceResult = GenerateSheet(surfacePath, SurfaceTileNames, tileSize, seed, palettePreset, true, overwrite);
        var undergroundResult = GenerateSheet(undergroundPath, UndergroundTileNames, tileSize, seed + 811, palettePreset, false, overwrite);

        return new TileGenerationResult(surfaceResult.Texture, undergroundResult.Texture, surfaceResult.Sprites, undergroundResult.Sprites);
    }

    private static SheetTextureResult GenerateSheet(string path, IReadOnlyList<string> tileNames, int tileSize, int seed, AntPalettePreset palettePreset, bool surface, bool overwrite)
    {
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new SheetTextureResult(existing, sprites);
        }

        const int columns = 4;
        var rows = Mathf.CeilToInt(tileNames.Count / (float)columns);
        var width = columns * tileSize;
        var height = rows * tileSize;
        var pixels = new Color32[width * height];

        for (var i = 0; i < tileNames.Count; i++)
        {
            var col = i % columns;
            var row = rows - 1 - i / columns;
            DrawTile(pixels, width, col * tileSize, row * tileSize, tileSize, tileNames[i], seed + i * 97, palettePreset, surface);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, tileSize, BuildGridRects(tileNames, tileSize, columns));
        return new SheetTextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
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

    private static void DrawTile(Color32[] px, int width, int ox, int oy, int size, string tileId, int seed, AntPalettePreset palettePreset, bool isSurface)
    {
        if (isSurface)
        {
            var grass = GetSurfacePalette(palettePreset);
            Fill(px, width, ox, oy, size, size, grass.Base);
            Noise(px, width, ox, oy, size, seed, grass.Shade, grass.Light);
            DrawSurfaceFeature(px, width, ox, oy, size, tileId, seed);
            return;
        }

        var dirt = GetUndergroundPalette(palettePreset);
        Fill(px, width, ox, oy, size, size, dirt.Base);
        Noise(px, width, ox, oy, size, seed, dirt.Shade, dirt.Light);
        DrawUndergroundFeature(px, width, ox, oy, size, tileId);
    }

    private static void DrawSurfaceFeature(Color32[] px, int width, int ox, int oy, int size, string tileId, int seed)
    {
        var pathFill = new Color32(166, 119, 62, 255);
        var pathEdge = new Color32(130, 89, 48, 255);
        var pathDust = new Color32(191, 143, 83, 255);

        switch (tileId)
        {
            case "path_straight_h": DrawPathHorizontal(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed); break;
            case "path_straight_v": DrawPathVertical(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed); break;
            case "path_corner_ne": DrawPathVertical(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed, true); DrawPathHorizontal(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed, true); break;
            case "path_corner_nw": DrawPathVertical(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed, true); DrawPathHorizontal(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed, false, true); break;
            case "path_cross": DrawPathHorizontal(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed); DrawPathVertical(px, width, ox, oy, size, pathFill, pathEdge, pathDust, seed); break;
            case "decor_clover":
                Disk(px, width, ox + Scale(size, 0.34f), oy + Scale(size, 0.47f), Scale(size, 0.13f), new Color32(36, 109, 44, 255));
                Disk(px, width, ox + Scale(size, 0.50f), oy + Scale(size, 0.31f), Scale(size, 0.13f), new Color32(36, 109, 44, 255));
                Disk(px, width, ox + Scale(size, 0.66f), oy + Scale(size, 0.47f), Scale(size, 0.13f), new Color32(36, 109, 44, 255));
                Line(px, width, ox + Scale(size, 0.50f), oy + Scale(size, 0.47f), ox + Scale(size, 0.50f), oy + Scale(size, 0.75f), new Color32(48, 83, 43, 255));
                break;
            case "decor_pebble":
                Disk(px, width, ox + Scale(size, 0.34f), oy + Scale(size, 0.56f), Scale(size, 0.16f), new Color32(129, 126, 114, 255));
                Disk(px, width, ox + Scale(size, 0.59f), oy + Scale(size, 0.44f), Scale(size, 0.13f), new Color32(155, 151, 138, 255));
                Disk(px, width, ox + Scale(size, 0.69f), oy + Scale(size, 0.66f), Scale(size, 0.09f), new Color32(117, 114, 102, 255));
                break;
            case "decor_flower":
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.09f), new Color32(238, 212, 66, 255));
                Disk(px, width, ox + Scale(size, 0.38f), oy + size / 2, Scale(size, 0.09f), new Color32(209, 92, 161, 255));
                Disk(px, width, ox + Scale(size, 0.62f), oy + size / 2, Scale(size, 0.09f), new Color32(209, 92, 161, 255));
                Disk(px, width, ox + size / 2, oy + Scale(size, 0.38f), Scale(size, 0.09f), new Color32(209, 92, 161, 255));
                Disk(px, width, ox + size / 2, oy + Scale(size, 0.62f), Scale(size, 0.09f), new Color32(209, 92, 161, 255));
                break;
            case "nest_entrance_marker":
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.31f), new Color32(138, 95, 48, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.22f), new Color32(101, 67, 33, 255));
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.12f), new Color32(33, 24, 17, 255));
                break;
        }
    }

    private static void DrawUndergroundFeature(Color32[] px, int width, int ox, int oy, int size, string tileId)
    {
        var tunnelFill = new Color32(66, 44, 28, 255);
        var tunnelEdge = new Color32(46, 30, 20, 255);
        var chamberAccent = new Color32(157, 113, 72, 255);

        switch (tileId)
        {
            case "tunnel_h": DrawTunnelHorizontal(px, width, ox, oy, size, tunnelFill, tunnelEdge); break;
            case "tunnel_v": DrawTunnelVertical(px, width, ox, oy, size, tunnelFill, tunnelEdge); break;
            case "tunnel_corner_ne": DrawTunnelVertical(px, width, ox, oy, size, tunnelFill, tunnelEdge, true); DrawTunnelHorizontal(px, width, ox, oy, size, tunnelFill, tunnelEdge, true); break;
            case "tunnel_corner_nw": DrawTunnelVertical(px, width, ox, oy, size, tunnelFill, tunnelEdge, true); DrawTunnelHorizontal(px, width, ox, oy, size, tunnelFill, tunnelEdge, false, true); break;
            case "tunnel_t_junction": DrawTunnelVertical(px, width, ox, oy, size, tunnelFill, tunnelEdge, true); DrawTunnelHorizontal(px, width, ox, oy, size, tunnelFill, tunnelEdge); break;
            case "tunnel_cross": DrawTunnelVertical(px, width, ox, oy, size, tunnelFill, tunnelEdge); DrawTunnelHorizontal(px, width, ox, oy, size, tunnelFill, tunnelEdge); break;
            case "chamber_small": Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.28f), tunnelFill); Ring(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.31f), tunnelEdge); break;
            case "chamber_medium":
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.38f), tunnelFill);
                Ring(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.41f), tunnelEdge);
                for (var i = 0; i < 7; i++) Set(px, width, ox + Scale(size, 0.30f) + i * Mathf.Max(1, size / 16), oy + size / 2, chamberAccent);
                break;
            case "chamber_queen_marker":
                Disk(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.34f), tunnelFill);
                Ring(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.37f), tunnelEdge);
                Diamond(px, width, ox + size / 2, oy + size / 2, Scale(size, 0.16f), chamberAccent);
                break;
        }
    }

    private static void DrawPathHorizontal(Color32[] px, int width, int ox, int oy, int size, Color32 fill, Color32 edge, Color32 dust, int seed, bool topHalfOnly = false, bool leftHalfOnly = false)
    {
        var yMin = topHalfOnly ? size / 2 : Scale(size, 0.32f);
        var yMax = Scale(size, 0.69f);
        var xMin = leftHalfOnly ? 0 : Scale(size, 0.09f);
        var xMax = leftHalfOnly ? size / 2 : size - Scale(size, 0.09f);
        for (var y = yMin; y < yMax; y++)
        for (var x = xMin; x < xMax; x++)
            Set(px, width, ox + x, oy + y, Hash(seed, x, y, 17) % 11 == 0 ? dust : (y == yMin || y == yMax - 1 ? edge : fill));
    }

    private static void DrawPathVertical(Color32[] px, int width, int ox, int oy, int size, Color32 fill, Color32 edge, Color32 dust, int seed, bool topHalfOnly = false)
    {
        var xMin = Scale(size, 0.32f);
        var xMax = Scale(size, 0.69f);
        var yMin = topHalfOnly ? size / 2 : Scale(size, 0.09f);
        var yMax = size - Scale(size, 0.09f);
        for (var y = yMin; y < yMax; y++)
        for (var x = xMin; x < xMax; x++)
            Set(px, width, ox + x, oy + y, Hash(seed, x, y, 19) % 11 == 0 ? dust : (x == xMin || x == xMax - 1 ? edge : fill));
    }

    private static void DrawTunnelHorizontal(Color32[] px, int width, int ox, int oy, int size, Color32 fill, Color32 edge, bool topHalfOnly = false, bool leftHalfOnly = false)
    {
        var yMin = topHalfOnly ? size / 2 : Scale(size, 0.34f);
        var yMax = Scale(size, 0.66f);
        var xMin = leftHalfOnly ? 0 : Scale(size, 0.06f);
        var xMax = leftHalfOnly ? size / 2 : size - Scale(size, 0.06f);
        for (var y = yMin; y < yMax; y++)
        for (var x = xMin; x < xMax; x++)
            Set(px, width, ox + x, oy + y, (y == yMin || y == yMax - 1) ? edge : fill);
    }

    private static void DrawTunnelVertical(Color32[] px, int width, int ox, int oy, int size, Color32 fill, Color32 edge, bool topHalfOnly = false)
    {
        var xMin = Scale(size, 0.34f);
        var xMax = Scale(size, 0.66f);
        var yMin = topHalfOnly ? size / 2 : Scale(size, 0.06f);
        var yMax = size - Scale(size, 0.06f);
        for (var y = yMin; y < yMax; y++)
        for (var x = xMin; x < xMax; x++)
            Set(px, width, ox + x, oy + y, (x == xMin || x == xMax - 1) ? edge : fill);
    }

    private static void Noise(Color32[] px, int width, int ox, int oy, int size, int seed, Color32 dark, Color32 light)
    {
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var n = Hash(seed, x, y, 7) & 31;
            if (n < 3) Set(px, width, ox + x, oy + y, light);
            else if (n == 31) Set(px, width, ox + x, oy + y, dark);
        }
    }

    private static (Color32 Base, Color32 Shade, Color32 Light) GetSurfacePalette(AntPalettePreset preset) => preset switch
    {
        AntPalettePreset.Desert => (new Color32(170, 150, 96, 255), new Color32(143, 126, 80, 255), new Color32(201, 179, 120, 255)),
        AntPalettePreset.Twilight => (new Color32(86, 104, 96, 255), new Color32(68, 83, 77, 255), new Color32(108, 128, 120, 255)),
        _ => (new Color32(64, 146, 58, 255), new Color32(48, 112, 46, 255), new Color32(91, 181, 79, 255))
    };

    private static (Color32 Base, Color32 Shade, Color32 Light) GetUndergroundPalette(AntPalettePreset preset) => preset switch
    {
        AntPalettePreset.Desert => (new Color32(145, 111, 74, 255), new Color32(113, 84, 55, 255), new Color32(170, 133, 91, 255)),
        AntPalettePreset.Twilight => (new Color32(88, 72, 59, 255), new Color32(68, 55, 46, 255), new Color32(109, 89, 74, 255)),
        _ => (new Color32(110, 79, 48, 255), new Color32(84, 58, 35, 255), new Color32(136, 98, 60, 255))
    };

    private static int Scale(int size, float fraction) => Mathf.Max(1, Mathf.RoundToInt(size * fraction));
    private static void Fill(Color32[] px, int width, int x0, int y0, int w, int h, Color32 c) { for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) Set(px, width, x0 + x, y0 + y, c); }
    private static void Disk(Color32[] px, int width, int cx, int cy, int r, Color32 c) { var rr = r * r; for (var y = -r; y <= r; y++) for (var x = -r; x <= r; x++) if (x * x + y * y <= rr) Set(px, width, cx + x, cy + y, c); }
    private static void Ring(Color32[] px, int width, int cx, int cy, int r, Color32 c) { var o = r * r; var i = (r - 1) * (r - 1); for (var y = -r; y <= r; y++) for (var x = -r; x <= r; x++) { var d = x * x + y * y; if (d <= o && d >= i) Set(px, width, cx + x, cy + y, c); } }
    private static void Diamond(Color32[] px, int width, int cx, int cy, int r, Color32 c) { for (var y = -r; y <= r; y++) { var row = r - Mathf.Abs(y); for (var x = -row; x <= row; x++) Set(px, width, cx + x, cy + y, c); } }
    private static void Line(Color32[] px, int width, int x0, int y0, int x1, int y1, Color32 c) { var dx = Mathf.Abs(x1 - x0); var sx = x0 < x1 ? 1 : -1; var dy = -Mathf.Abs(y1 - y0); var sy = y0 < y1 ? 1 : -1; var err = dx + dy; while (true) { Set(px, width, x0, y0, c); if (x0 == x1 && y0 == y1) break; var e2 = err * 2; if (e2 >= dy) { err += dy; x0 += sx; } if (e2 <= dx) { err += dx; y0 += sy; } } }
    private static void Set(Color32[] px, int width, int x, int y, Color32 c) { if (x < 0 || y < 0) return; var i = y * width + x; if (i < 0 || i >= px.Length) return; px[i] = c; }

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
