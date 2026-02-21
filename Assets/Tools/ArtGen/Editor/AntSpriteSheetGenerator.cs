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

        for (var i = 0; i < columns; i++)
        {
            var ox = i * antSpriteSize;
            var isSoldier = i >= 2;
            var isMask = (i % 2) == 1;
            DrawAnt(pixels, width, ox, 0, antSpriteSize, seed + (i * 101), isSoldier, isMask, palettePreset);
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

    private static void DrawAnt(Color32[] px, int width, int ox, int oy, int size, int seed, bool soldier, bool maskOnly, AntPalettePreset palettePreset)
    {
        var silhouette = new bool[size * size];
        var abdomenMask = new bool[size * size];

        var cx = size / 2f;
        var cy = size / 2f;
        var t = Mathf.Max(2, Mathf.RoundToInt(size / 32f));

        var abdomenCx = cx - size * 0.18f;
        var thoraxCx = cx + size * 0.01f;
        var headCx = cx + size * (soldier ? 0.20f : 0.16f);

        FillEllipse(silhouette, size, abdomenCx, cy, size * 0.21f, size * 0.18f);
        FillEllipse(abdomenMask, size, abdomenCx, cy, size * 0.21f, size * 0.18f);
        FillEllipse(silhouette, size, thoraxCx, cy, size * 0.13f, size * 0.11f);
        FillEllipse(silhouette, size, headCx, cy, size * (soldier ? 0.13f : 0.10f), size * (soldier ? 0.12f : 0.09f));

        var petioleW = Mathf.RoundToInt(size * 0.05f);
        var petioleH = Mathf.RoundToInt(size * 0.08f);
        FillRect(silhouette, size, Mathf.RoundToInt(cx - size * 0.08f), Mathf.RoundToInt(cy - petioleH * 0.5f), petioleW, petioleH, true);

        var legOffsets = new[] { -size * 0.11f, 0f, size * 0.11f };
        foreach (var legOffset in legOffsets)
        {
            var anchorX = thoraxCx;
            var anchorY = cy + legOffset + ((Hash(seed, Mathf.RoundToInt(legOffset), 13) & 1) == 0 ? -1 : 1) * size * 0.01f;

            DrawThickLine(silhouette, size, anchorX - size * 0.03f, anchorY, anchorX - size * 0.17f, anchorY - size * 0.08f, t);
            DrawThickLine(silhouette, size, anchorX - size * 0.17f, anchorY - size * 0.08f, anchorX - size * 0.30f, anchorY - size * 0.13f, t);

            DrawThickLine(silhouette, size, anchorX + size * 0.03f, anchorY, anchorX + size * 0.18f, anchorY - size * 0.08f, t);
            DrawThickLine(silhouette, size, anchorX + size * 0.18f, anchorY - size * 0.08f, anchorX + size * 0.30f, anchorY - size * 0.13f, t);
        }

        var antennaOriginX = headCx + size * (soldier ? 0.07f : 0.05f);
        var antennaWiggle = ((Hash(seed, 7, 23) & 1) == 0 ? -1f : 1f) * size * 0.01f;
        DrawThickLine(silhouette, size, antennaOriginX, cy - size * 0.03f, antennaOriginX + size * 0.12f, cy - size * 0.17f + antennaWiggle, t);
        DrawThickLine(silhouette, size, antennaOriginX, cy + size * 0.03f, antennaOriginX + size * 0.12f, cy + size * 0.17f - antennaWiggle, t);

        if (soldier)
        {
            var mandibleX = headCx + size * 0.12f;
            DrawThickLine(silhouette, size, mandibleX, cy - size * 0.04f, mandibleX + size * 0.10f, cy - size * 0.11f, t);
            DrawThickLine(silhouette, size, mandibleX, cy + size * 0.04f, mandibleX + size * 0.10f, cy + size * 0.11f, t);
        }

        var stripeMask = new bool[size * size];
        var stripeMin = abdomenCx - size * 0.08f;
        var stripeMax = abdomenCx + size * 0.03f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(abdomenMask, size, x, y))
                {
                    continue;
                }

                if (x >= stripeMin && x <= stripeMax)
                {
                    SetMask(stripeMask, size, x, y, true);
                }
            }
        }

        if (maskOnly)
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (GetMask(stripeMask, size, x, y))
                    {
                        Set(px, width, ox + x, oy + y, Color.white);
                    }
                }
            }

            return;
        }

        var colors = GetPalette(palettePreset, soldier);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(silhouette, size, x, y) && HasNeighbor(silhouette, size, x, y))
                {
                    Set(px, width, ox + x, oy + y, colors.Outline);
                }
            }
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!GetMask(silhouette, size, x, y))
                {
                    continue;
                }

                var c = colors.Body;
                if (y < cy - size * 0.07f)
                {
                    c = colors.Highlight;
                }
                else if (y > cy + size * 0.08f)
                {
                    c = colors.Shade;
                }

                Set(px, width, ox + x, oy + y, c);
            }
        }
    }

    private static (Color32 Body, Color32 Shade, Color32 Highlight, Color32 Outline) GetPalette(AntPalettePreset preset, bool soldier)
    {
        switch (preset)
        {
            case AntPalettePreset.Desert:
                return soldier
                    ? (new Color32(118, 82, 45, 255), new Color32(88, 61, 33, 255), new Color32(149, 105, 62, 255), new Color32(30, 20, 12, 255))
                    : (new Color32(134, 98, 58, 255), new Color32(100, 72, 41, 255), new Color32(166, 124, 79, 255), new Color32(30, 20, 12, 255));
            case AntPalettePreset.Twilight:
                return soldier
                    ? (new Color32(74, 62, 98, 255), new Color32(55, 46, 76, 255), new Color32(97, 84, 128, 255), new Color32(17, 14, 24, 255))
                    : (new Color32(87, 71, 116, 255), new Color32(65, 54, 87, 255), new Color32(114, 95, 146, 255), new Color32(17, 14, 24, 255));
            default:
                return soldier
                    ? (new Color32(72, 40, 27, 255), new Color32(48, 27, 19, 255), new Color32(100, 59, 39, 255), new Color32(18, 10, 8, 255))
                    : (new Color32(88, 51, 33, 255), new Color32(60, 36, 24, 255), new Color32(118, 73, 49, 255), new Color32(18, 10, 8, 255));
        }
    }

    private static void FillEllipse(bool[] mask, int size, float cx, float cy, float rx, float ry)
    {
        var minX = Mathf.FloorToInt(cx - rx);
        var maxX = Mathf.CeilToInt(cx + rx);
        var minY = Mathf.FloorToInt(cy - ry);
        var maxY = Mathf.CeilToInt(cy + ry);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var nx = (x - cx) / rx;
                var ny = (y - cy) / ry;
                if (nx * nx + ny * ny <= 1f)
                {
                    SetMask(mask, size, x, y, true);
                }
            }
        }
    }

    private static void FillRect(bool[] mask, int size, int x0, int y0, int w, int h, bool value)
    {
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            SetMask(mask, size, x0 + x, y0 + y, value);
    }

    private static void DrawThickLine(bool[] mask, int size, float x0, float y0, float x1, float y1, int thickness)
    {
        var steps = Mathf.Max(Mathf.Abs(Mathf.RoundToInt(x1 - x0)), Mathf.Abs(Mathf.RoundToInt(y1 - y0)));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0f : i / (float)steps;
            var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            FillEllipse(mask, size, x, y, thickness * 0.5f, thickness * 0.5f);
        }
    }


    private static int Hash(int seed, int a, int b)
    {
        unchecked
        {
            var h = seed;
            h = (h * 397) ^ (a * 73856093);
            h = (h * 397) ^ (b * 19349663);
            return h;
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
        if (x < 0 || y < 0)
        {
            return;
        }

        var i = y * width + x;
        if (i < 0 || i >= px.Length)
        {
            return;
        }

        px[i] = c;
    }
}
