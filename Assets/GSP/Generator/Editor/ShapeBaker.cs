#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ShapeBaker
{
    public const string OutputRoot = "Assets/Presentation/Shapes/Resources";
    public const string LegacyRoot = "Assets/Presentation/Shapes/Generated";
    public const string TemplatesRoot = "Assets/Presentation/Shapes/Templates";
    public const string LibraryAssetPath = OutputRoot + "/ShapeLibrary.asset";
    public const string DefaultPackPath = TemplatesRoot + "/TemplatePack_DefaultNeon.asset";

    private static bool legacyFolderNoticeShown;

    public static Sprite BakeTemplate(ShapeTemplateBase template, Color tint)
    {
        if (template == null)
        {
            return null;
        }

        EnsureCanonicalFolders();

        var folder = $"{OutputRoot}/{template.CategoryFolder}";
        EnsureFolder(folder);

        var pixels = template.Rasterize(tint);
        var texture = ShapeSpriteFactory.CreateTexture(template.TextureSize, pixels);
        var png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        var outputPath = $"{folder}/{template.DefaultFileName}.png";
        File.WriteAllBytes(outputPath, png);
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureImporter(outputPath, template.PixelsPerUnit);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        var library = EnsureLibraryAsset();
        library.Set(template.Id, sprite, template);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        NotifyLegacyFolderDetected();
        return sprite;
    }

    public static void BakePack(ShapeTemplatePack pack)
    {
        if (pack == null)
        {
            return;
        }

        foreach (var t in pack.templates)
        {
            if (t != null)
            {
                BakeTemplate(t, pack.tint);
            }
        }
    }

    public static ShapeTemplatePack EnsureDefaultNeonPackAndBake()
    {
        EnsureCanonicalFolders();

        var pack = AssetDatabase.LoadAssetAtPath<ShapeTemplatePack>(DefaultPackPath);
        if (pack == null)
        {
            pack = ScriptableObject.CreateInstance<ShapeTemplatePack>();
            pack.name = "TemplatePack_DefaultNeon";
            pack.tint = new Color(0.5f, 0.95f, 1f, 1f);
            AssetDatabase.CreateAsset(pack, DefaultPackPath);
        }

        if (pack.templates.Count == 0)
        {
            pack.templates.Add(CreateTemplate<DotCoreTemplate>("DotCore_Default.asset", t =>
                t.ConfigureBase(ShapeId.DotCore, "Dots", 64, 16)));

            pack.templates.Add(CreateTemplate<GlowDotTemplate>("GlowDot_Default.asset", null));
            pack.templates.Add(CreateTemplate<GlowDotTemplate>("GlowDotSmall_Default.asset", t => t.ApplySmallDefaults()));
            pack.templates.Add(CreateTemplate<RingPingTemplate>("RingPing_Default.asset", null));
            pack.templates.Add(CreateTemplate<OrganicBlobTemplate>("OrganicMetaball_Default.asset", t =>
                t.ConfigureBase(ShapeId.OrganicMetaball, "Blobs", 96, 16)));
            pack.templates.Add(CreateTemplate<OrganicBlobTemplate>("OrganicAmoeba_Default.asset", t =>
                t.ConfigureBase(ShapeId.OrganicAmoeba, "Blobs", 96, 16)));
            pack.templates.Add(CreateTemplate<StrokeTemplate>("StrokeScribble_Default.asset", null));
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
        }

        BakePack(pack);
        return pack;
    }

    private static T CreateTemplate<T>(string fileName, System.Action<T> configure) where T : ShapeTemplateBase
    {
        var path = $"{TemplatesRoot}/{fileName}";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            return existing;
        }

        var created = ScriptableObject.CreateInstance<T>();
        configure?.Invoke(created);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static ShapeLibrary EnsureLibraryAsset()
    {
        var library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(LibraryAssetPath);
        if (library != null)
        {
            return library;
        }

        library = ScriptableObject.CreateInstance<ShapeLibrary>();
        AssetDatabase.CreateAsset(library, LibraryAssetPath);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static void ConfigureImporter(string path, int ppu)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Mathf.Max(1, ppu);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void EnsureCanonicalFolders()
    {
        EnsureFolder(OutputRoot);
        EnsureFolder(OutputRoot + "/Dots");
        EnsureFolder(OutputRoot + "/Glows");
        EnsureFolder(OutputRoot + "/Rings");
        EnsureFolder(OutputRoot + "/Blobs");
        EnsureFolder(OutputRoot + "/Strokes");
        EnsureFolder(TemplatesRoot);
    }

    private static void NotifyLegacyFolderDetected()
    {
        if (legacyFolderNoticeShown || !AssetDatabase.IsValidFolder(LegacyRoot))
        {
            return;
        }

        legacyFolderNoticeShown = true;
        EditorUtility.DisplayDialog(
            "Shape Baker Migration",
            "Old Generated folder detected. New canonical location is Resources. Old folder is now obsolete.",
            "OK");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var slash = folder.LastIndexOf('/');
        var parent = folder.Substring(0, slash);
        var name = folder[(slash + 1)..];
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
