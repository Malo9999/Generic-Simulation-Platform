using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportSettingsUtil
{
    public static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var slash = folderPath.LastIndexOf('/');
        var parent = folderPath.Substring(0, slash);
        var name = folderPath.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static List<Sprite> ConfigureAsPixelArtMultiple(string texturePath, int pixelsPerUnit, List<SpriteRect> spriteRects)
    {
        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
        {
            Debug.LogError($"Unable to configure importer for {texturePath}");
            return new List<Sprite>();
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;

        var factories = new SpriteDataProviderFactories();
        factories.Init();

        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var existing = provider.GetSpriteRects();
        if (existing is { Length: > 0 })
        {
            var byName = existing.ToDictionary(x => x.name, x => x);
            for (var i = 0; i < spriteRects.Count; i++)
            {
                if (byName.TryGetValue(spriteRects[i].name, out var previous))
                {
                    var r = spriteRects[i];
                    r.spriteID = previous.spriteID;
                    spriteRects[i] = r;
                }
            }
        }

        provider.SetSpriteRects(spriteRects.ToArray());
        var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null)
        {
            nameProvider.SetNameFileIdPairs(spriteRects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
        }

        provider.Apply();
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

        return AssetDatabase.LoadAllAssetRepresentationsAtPath(texturePath).OfType<Sprite>().OrderBy(x => x.name).ToList();
    }
}
