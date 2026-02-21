using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AntSpriteSheetGenerator
{
    private static readonly string[] SpriteNames = { "ant_worker", "ant_worker_mask", "ant_soldier", "ant_soldier_mask" };

    public static AntPackGenerator.TextureResult Generate(string outputFolder, int seed, int tileSize, AntPalettePreset palettePreset, bool overwrite)
    {
        var path = $"{outputFolder}/ants.png";
        if (!overwrite && File.Exists(path))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();
            return new AntPackGenerator.TextureResult(existing, sprites);
        }

        const int columns = 4;
        var width = columns * tileSize;
        var height = tileSize;
        var pixels = new Color32[width * height];

        for (var i = 0; i < 4; i++)
        {
            var ox = i * tileSize;
            var isSoldier = i >= 2;
            var isMask = (i % 2) == 1;
            DrawAnt(pixels, width, ox, 0, tileSize, seed + i * 101, isSoldier, isMask);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var spritesOut = ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, tileSize, AntPackGenerator.BuildGridRects(SpriteNames, tileSize, columns));
        return new AntPackGenerator.TextureResult(AssetDatabase.LoadAssetAtPath<Texture2D>(path), spritesOut);
    }

    private static void DrawAnt(Color32[] px, int width, int ox, int oy, int size, int seed, bool soldier, bool mask)
    {
        var body = mask ? new Color32(0, 0, 0, 0) : (soldier ? new Color32(67, 34, 22, 255) : new Color32(82, 45, 29, 255));
        var stripe = mask ? new Color32(255, 255, 255, 255) : new Color32(214, 68, 57, 255);
        var outline = mask ? new Color32(0, 0, 0, 0) : new Color32(22, 14, 10, 255);

        int cx = ox + size / 2;
        int cy = oy + size / 2;
        int abdomen = soldier ? size / 5 : size / 6;
        int thorax = soldier ? size / 6 : size / 7;
        int head = soldier ? size / 6 : size / 8;

        Disk(px, width, cx - size / 7, cy, abdomen, body);
        Disk(px, width, cx + size / 20, cy, thorax, body);
        Disk(px, width, cx + size / 5, cy, head, body);

        Line(px, width, cx - size / 10, cy - size / 8, cx + size / 8, cy - size / 8, stripe);
        for (int leg = -1; leg <= 1; leg++)
        {
            int jitter = Hash(seed, leg, 0, 3) % 2;
            Line(px, width, cx - size / 20, cy + leg * (size / 8), cx - size / 3, cy + leg * (size / 6) + jitter, outline);
            Line(px, width, cx + size / 8, cy + leg * (size / 8), cx + size / 3, cy + leg * (size / 6) - jitter, outline);
        }

        if (soldier)
        {
            Line(px, width, cx + size / 4, cy - size / 12, cx + size / 3, cy - size / 5, outline);
            Line(px, width, cx + size / 4, cy + size / 12, cx + size / 3, cy + size / 5, outline);
        }
    }

    private static void Disk(Color32[] px, int width, int cx, int cy, int r, Color32 c)
    {
        var rr = r * r;
        for (var y = -r; y <= r; y++)
        for (var x = -r; x <= r; x++)
            if (x * x + y * y <= rr) Set(px, width, cx + x, cy + y, c);
    }

    private static void Line(Color32[] px, int width, int x0, int y0, int x1, int y1, Color32 c)
    {
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            Set(px, width, x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            var e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
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
            return h;
        }
    }
}
