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

        for (var i = 0; i < 4; i++)
        {
            var ox = i * antSpriteSize;
            var isSoldier = i >= 2;
            var isMask = (i % 2) == 1;
            DrawAnt(pixels, width, ox, 0, antSpriteSize, seed + i * 101, isSoldier, isMask);
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

    private static void DrawAnt(Color32[] px, int width, int ox, int oy, int size, int seed, bool soldier, bool maskOnly)
    {
        var cellMask = new bool[size * size];
        var cx = size / 2;
        var cy = size / 2;

        FillDisk(cellMask, size, cx - Scale(size, 0.20f), cy, Scale(size, soldier ? 0.18f : 0.16f)); // abdomen
        FillDisk(cellMask, size, cx + Scale(size, 0.02f), cy, Scale(size, 0.12f)); // thorax
        FillDisk(cellMask, size, cx + Scale(size, soldier ? 0.21f : 0.18f), cy - Scale(size, 0.01f), Scale(size, soldier ? 0.13f : 0.10f)); // head

        // waist/petiole pinch
        FillRect(cellMask, size, cx - Scale(size, 0.08f), cy - Scale(size, 0.03f), Scale(size, 0.08f), Scale(size, 0.06f), false);

        // legs (6)
        for (var leg = 0; leg < 3; leg++)
        {
            var y = cy - Scale(size, 0.12f) + leg * Scale(size, 0.12f);
            var jitter = (Hash(seed, leg, 0, 5) & 1) == 0 ? 1 : -1;
            DrawMaskLine(cellMask, size, cx - Scale(size, 0.01f), y, cx - Scale(size, 0.30f), y - Scale(size, 0.10f) + jitter);
            DrawMaskLine(cellMask, size, cx + Scale(size, 0.06f), y, cx + Scale(size, 0.31f), y - Scale(size, 0.10f) - jitter);
        }

        // antennae
        DrawMaskLine(cellMask, size, cx + Scale(size, soldier ? 0.25f : 0.21f), cy - Scale(size, 0.05f), cx + Scale(size, 0.34f), cy - Scale(size, 0.18f));
        DrawMaskLine(cellMask, size, cx + Scale(size, soldier ? 0.25f : 0.21f), cy + Scale(size, 0.01f), cx + Scale(size, 0.35f), cy + Scale(size, 0.14f));

        // soldier mandibles
        if (soldier)
        {
            DrawMaskLine(cellMask, size, cx + Scale(size, 0.33f), cy - Scale(size, 0.04f), cx + Scale(size, 0.42f), cy - Scale(size, 0.11f));
            DrawMaskLine(cellMask, size, cx + Scale(size, 0.33f), cy + Scale(size, 0.02f), cx + Scale(size, 0.42f), cy + Scale(size, 0.09f));
        }

        var body = soldier ? new Color32(67, 34, 22, 255) : new Color32(82, 45, 29, 255);
        var shade = soldier ? new Color32(46, 24, 15, 255) : new Color32(56, 31, 21, 255);
        var highlight = soldier ? new Color32(99, 53, 34, 255) : new Color32(116, 67, 46, 255);
        var outline = new Color32(18, 10, 8, 255);

        var stripeMinX = cx - Scale(size, 0.28f);
        var stripeMaxX = cx - Scale(size, 0.14f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(cellMask, size, x, y))
                {
                    continue;
                }

                if (maskOnly)
                {
                    if (x >= stripeMinX && x <= stripeMaxX && Mathf.Abs(y - cy) <= Scale(size, 0.08f))
                    {
                        Set(px, width, ox + x, oy + y, Color.white);
                    }

                    continue;
                }

                var c = body;
                if (y > cy + Scale(size, 0.09f)) c = shade;
                else if (y < cy - Scale(size, 0.10f)) c = highlight;
                Set(px, width, ox + x, oy + y, c);
            }
        }

        if (maskOnly)
        {
            return;
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (GetMask(cellMask, size, x, y) || !HasNeighbor(cellMask, size, x, y))
                {
                    continue;
                }

                Set(px, width, ox + x, oy + y, outline);
            }
        }
    }

    private static int Scale(int size, float fraction) => Mathf.Max(1, Mathf.RoundToInt(size * fraction));

    private static void FillDisk(bool[] mask, int size, int cx, int cy, int r)
    {
        var rr = r * r;
        for (var y = -r; y <= r; y++)
        for (var x = -r; x <= r; x++)
            if (x * x + y * y <= rr)
                SetMask(mask, size, cx + x, cy + y, true);
    }

    private static void FillRect(bool[] mask, int size, int x0, int y0, int w, int h, bool value)
    {
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            SetMask(mask, size, x0 + x, y0 + y, value);
    }

    private static void DrawMaskLine(bool[] mask, int size, int x0, int y0, int x1, int y1)
    {
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            SetMask(mask, size, x0, y0, true);
            if (x0 == x1 && y0 == y1) break;
            var e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static bool HasNeighbor(bool[] mask, int size, int x, int y)
    {
        for (var ny = -1; ny <= 1; ny++)
        for (var nx = -1; nx <= 1; nx++)
            if ((nx != 0 || ny != 0) && GetMask(mask, size, x + nx, y + ny))
                return true;
        return false;
    }

    private static bool GetMask(bool[] mask, int size, int x, int y) => x >= 0 && y >= 0 && x < size && y < size && mask[y * size + x];
    private static void SetMask(bool[] mask, int size, int x, int y, bool v) { if (x >= 0 && y >= 0 && x < size && y < size) mask[y * size + x] = v; }

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
