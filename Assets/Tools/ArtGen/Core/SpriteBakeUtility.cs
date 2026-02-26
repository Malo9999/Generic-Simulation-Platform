using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpriteBakeUtility
{
    public static void EnsureTextureImportSettings(string texturePath)
    {
        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
        {
            Debug.LogError($"[SpriteBakeUtility] Unable to configure importer for '{texturePath}'.");
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    public static List<Sprite> BakeSpritesFromGrid(
        Texture2D sheet,
        int frameW,
        int frameH,
        int cols,
        int rows,
        int pad,
        Vector2 pivot,
        float pixelsPerUnit,
        Func<int, string> spriteNameForIndex)
    {
        var baked = new List<Sprite>();
        if (sheet == null || frameW <= 0 || frameH <= 0 || cols <= 0 || rows <= 0)
        {
            return baked;
        }

        var total = cols * rows;
        var maxColsByWidth = (sheet.width + pad) / (frameW + pad);
        var maxRowsByHeight = (sheet.height + pad) / (frameH + pad);
        var safeTotal = Mathf.Min(total, Mathf.Max(0, maxColsByWidth * maxRowsByHeight));

        for (var index = 0; index < safeTotal; index++)
        {
            var col = index % cols;
            var rowFromTop = index / cols;
            var x = pad + col * (frameW + pad);
            var yFromTop = pad + rowFromTop * (frameH + pad);
            var y = sheet.height - yFromTop - frameH;
            if (x < 0 || y < 0 || x + frameW > sheet.width || y + frameH > sheet.height)
            {
                continue;
            }

            var rect = new Rect(x, y, frameW, frameH);
            var sprite = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = spriteNameForIndex?.Invoke(index) ?? $"sprite:{index:000}";
            sprite.hideFlags = HideFlags.HideInHierarchy;
            baked.Add(sprite);
        }

        return baked;
    }

    public static void AddOrReplaceSubAssets(UnityEngine.Object parentAsset, IReadOnlyList<Sprite> sprites)
    {
        if (parentAsset == null)
        {
            return;
        }

        var path = AssetDatabase.GetAssetPath(parentAsset);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var subAsset in subAssets)
        {
            if (subAsset is not Sprite existing)
            {
                continue;
            }

            if ((existing.hideFlags & HideFlags.HideInHierarchy) == 0)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(existing, true);
        }

        if (sprites != null)
        {
            foreach (var sprite in sprites.Where(s => s != null))
            {
                try
                {
                    AssetDatabase.AddObjectToAsset(sprite, path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SpriteBakeUtility] Failed to add sprite '{sprite.name}' to '{path}': {ex}");
                }
            }
        }

        EditorUtility.SetDirty(parentAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }
}
