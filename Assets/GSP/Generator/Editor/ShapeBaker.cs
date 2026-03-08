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

        var changed = false;
        changed |= EnsurePackTemplate(pack, CreateTemplate<DotCoreTemplate>("DotCore_Default.asset", t =>
            t.ConfigureBase(ShapeId.DotCore, "Dots", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<GlowDotTemplate>("GlowDot_Default.asset", null));
        changed |= EnsurePackTemplate(pack, CreateTemplate<GlowDotTemplate>("GlowDotSmall_Default.asset", t => t.ApplySmallDefaults()));
        changed |= EnsurePackTemplate(pack, CreateTemplate<RingPingTemplate>("RingPing_Default.asset", null));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicMetaball_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicMetaball, "Blobs", 96, 16);
            t.ApplyMetaballDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoeba_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoeba, "Blobs", 96, 16);
            t.ApplyAmoebaDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaWide_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaWide, "Blobs", 96, 16);
            t.ApplyAmoebaWideDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaLobed_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaLobed, "Blobs", 96, 16);
            t.ApplyAmoebaLobedDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaSprawl_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaSprawl, "Blobs", 96, 16);
            t.ApplyAmoebaSprawlDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaPseudopod_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaPseudopod, "Blobs", 96, 16);
            t.ApplyAmoebaPseudopodDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaCompact_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaCompact, "Blobs", 96, 16);
            t.ApplyAmoebaCompactDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaCrawler_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaCrawler, "Blobs", 96, 16);
            t.ApplyAmoebaVectorCrawlerDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaStar_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaStar, "Blobs", 96, 16);
            t.ApplyAmoebaVectorStarDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaBranch_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaBranch, "Blobs", 96, 16);
            t.ApplyAmoebaVectorBranchDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaWideArms_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaWideArms, "Blobs", 96, 16);
            t.ApplyAmoebaVectorWideDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<OrganicBlobTemplate>("OrganicAmoebaHunter_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.OrganicAmoebaHunter, "Blobs", 96, 16);
            t.ApplyAmoebaVectorHunterDefaults();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<StrokeTemplate>("StrokeScribble_Default.asset", null));
        changed |= EnsurePackTemplate(pack, CreateTemplate<TriangleAgentTemplate>("TriangleAgent_Default.asset", t =>
            t.ConfigureBase(ShapeId.TriangleAgent, "Agents", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<DiamondAgentTemplate>("DiamondAgent_Default.asset", t =>
            t.ConfigureBase(ShapeId.DiamondAgent, "Agents", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<ArrowAgentTemplate>("ArrowAgent_Default.asset", t =>
            t.ConfigureBase(ShapeId.ArrowAgent, "Agents", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<CrossMarkerTemplate>("CrossMarker_Default.asset", t =>
            t.ConfigureBase(ShapeId.CrossMarker, "Markers", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<ArcSectorTemplate>("ArcSector_Default.asset", t =>
            t.ConfigureBase(ShapeId.ArcSector, "Markers", 96, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<LineSegmentTemplate>("LineSegment_Default.asset", t =>
            t.ConfigureBase(ShapeId.LineSegment, "Lines", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<FilamentTemplate>("Filament_Default.asset", t =>
        {
            t.ConfigureBase(ShapeId.Filament, "Lines", 128, 16);
            t.ApplyDefaultSettings();
        }));
        changed |= EnsurePackTemplate(pack, CreateTemplate<NoiseBlobTemplate>("NoiseBlob_Default.asset", t =>
            t.ConfigureBase(ShapeId.NoiseBlob, "Blobs", 64, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<FieldBlobTemplate>("FieldBlob_Default.asset", t =>
            t.ConfigureBase(ShapeId.FieldBlob, "Fields", 128, 16)));
        changed |= EnsurePackTemplate(pack, CreateTemplate<PulseRingTemplate>("PulseRing_Default.asset", t =>
            t.ConfigureBase(ShapeId.PulseRing, "Rings", 64, 16)));

        var templateCount = pack.templates.Count;
        if (templateCount < 27)
        {
            Debug.LogWarning($"TemplatePack_DefaultNeon migration incomplete. Expected 27 templates, found {templateCount}.");
        }
        else if (changed)
        {
            Debug.Log("TemplatePack_DefaultNeon repaired/upgraded to 27 templates.");
        }

        if (changed)
        {
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
            ConfigureIfIncomplete(existing, configure);
            return existing;
        }

        var created = ScriptableObject.CreateInstance<T>();
        configure?.Invoke(created);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static void ConfigureIfIncomplete<T>(T template, System.Action<T> configure) where T : ShapeTemplateBase
    {
        if (template == null || configure == null)
        {
            return;
        }

        var isIncomplete = string.IsNullOrWhiteSpace(template.Id)
            || string.IsNullOrWhiteSpace(template.CategoryFolder)
            || template.TextureSize <= 0
            || template.PixelsPerUnit <= 0;

        if (!isIncomplete)
        {
            return;
        }

        configure(template);
        EditorUtility.SetDirty(template);
    }

    private static bool EnsurePackTemplate(ShapeTemplatePack pack, ShapeTemplateBase template)
    {
        if (pack == null || template == null)
        {
            return false;
        }

        foreach (var existing in pack.templates)
        {
            if (existing != null && existing.Id == template.Id)
            {
                return false;
            }
        }

        pack.templates.Add(template);
        return true;
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
        EnsureFolder(OutputRoot + "/Agents");
        EnsureFolder(OutputRoot + "/Lines");
        EnsureFolder(OutputRoot + "/Markers");
        EnsureFolder(OutputRoot + "/Fields");
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
