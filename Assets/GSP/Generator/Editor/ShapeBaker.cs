#if UNITY_EDITOR
using System.Collections.Generic;
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
    public const string AmoebaPackPath = TemplatesRoot + "/TemplatePack_Amoeba.asset";
    // Place amoeba silhouette PNGs here: Assets/GSP/Generator/Silhouettes/Amoeba/
    public const string AmoebaSilhouetteRoot = "Assets/GSP/Generator/Silhouettes/Amoeba";

    private static readonly AmoebaDefinition[] AmoebaDefinitions =
    {
        new("amoeba_blob.png", ShapeId.AmoebaBlob, "AmoebaBlob_Silhouette.asset"),
        new("amoeba_lobed.png", ShapeId.AmoebaLobed, "AmoebaLobed_Silhouette.asset"),
        new("amoeba_star.png", ShapeId.AmoebaStar, "AmoebaStar_Silhouette.asset"),
        new("amoeba_crawler.png", ShapeId.AmoebaCrawler, "AmoebaCrawler_Silhouette.asset"),
        new("amoeba_wide.png", ShapeId.AmoebaWide, "AmoebaWide_Silhouette.asset"),
        new("amoeba_branch.png", ShapeId.AmoebaBranch, "AmoebaBranch_Silhouette.asset"),
        new("amoeba_hunter.png", ShapeId.AmoebaHunter, "AmoebaHunter_Silhouette.asset"),
        new("amoeba_compact.png", ShapeId.AmoebaCompact, "AmoebaCompact_Silhouette.asset"),
        new("amoeba_split.png", ShapeId.AmoebaSplit, "AmoebaSplit_Silhouette.asset"),
        new("amoeba_spread.png", ShapeId.AmoebaSpread, "AmoebaSpread_Silhouette.asset")
    };

    private static bool legacyFolderNoticeShown;

    public static ShapeTemplatePack GeneratePack(ShapePackPreset preset)
    {
        var pack = preset switch
        {
            ShapePackPreset.Amoeba => EnsureAmoebaPack(),
            _ => EnsureDefaultNeonPack()
        };

        BakePack(pack);
        return pack;
    }

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

    public static ShapeTemplatePack EnsureDefaultNeonPack()
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

        var templates = new List<ShapeTemplateBase>
        {
            CreateTemplate<DotCoreTemplate>("DotCore_Default.asset", t => t.ConfigureBase(ShapeId.DotCore, "Dots", 64, 16)),
            CreateTemplate<GlowDotTemplate>("GlowDot_Default.asset", null),
            CreateTemplate<GlowDotTemplate>("GlowDotSmall_Default.asset", t => t.ApplySmallDefaults()),
            CreateTemplate<RingPingTemplate>("RingPing_Default.asset", null),
            CreateTemplate<OrganicBlobTemplate>("OrganicMetaball_Default.asset", t =>
            {
                t.ConfigureBase(ShapeId.OrganicMetaball, "Blobs", 96, 16);
                t.ApplyMetaballDefaults();
            }),
            CreateTemplate<OrganicBlobTemplate>("OrganicAmoeba_Default.asset", t =>
            {
                t.ConfigureBase(ShapeId.OrganicAmoeba, "Blobs", 96, 16);
                t.ApplyAmoebaDefaults();
            }),
            CreateTemplate<StrokeTemplate>("StrokeScribble_Default.asset", null),
            CreateTemplate<TriangleAgentTemplate>("TriangleAgent_Default.asset", t => t.ConfigureBase(ShapeId.TriangleAgent, "Agents", 64, 16)),
            CreateTemplate<DiamondAgentTemplate>("DiamondAgent_Default.asset", t => t.ConfigureBase(ShapeId.DiamondAgent, "Agents", 64, 16)),
            CreateTemplate<ArrowAgentTemplate>("ArrowAgent_Default.asset", t => t.ConfigureBase(ShapeId.ArrowAgent, "Agents", 64, 16)),
            CreateTemplate<CrossMarkerTemplate>("CrossMarker_Default.asset", t => t.ConfigureBase(ShapeId.CrossMarker, "Markers", 64, 16)),
            CreateTemplate<ArcSectorTemplate>("ArcSector_Default.asset", t => t.ConfigureBase(ShapeId.ArcSector, "Markers", 96, 16)),
            CreateTemplate<LineSegmentTemplate>("LineSegment_Default.asset", t => t.ConfigureBase(ShapeId.LineSegment, "Lines", 64, 16)),
            CreateTemplate<FilamentTemplate>("Filament_Default.asset", t =>
            {
                t.ConfigureBase(ShapeId.Filament, "Lines", 128, 16);
                t.ApplyDefaultSettings();
            }),
            CreateTemplate<NoiseBlobTemplate>("NoiseBlob_Default.asset", t => t.ConfigureBase(ShapeId.NoiseBlob, "Blobs", 64, 16)),
            CreateTemplate<FieldBlobTemplate>("FieldBlob_Default.asset", t => t.ConfigureBase(ShapeId.FieldBlob, "Fields", 128, 16)),
            CreateTemplate<PulseRingTemplate>("PulseRing_Default.asset", t => t.ConfigureBase(ShapeId.PulseRing, "Rings", 64, 16))
        };

        var changed = AssignPackTemplates(pack, templates);
        if (pack.templates.Count < 17)
        {
            Debug.LogWarning($"TemplatePack_DefaultNeon migration incomplete. Expected 17 templates, found {pack.templates.Count}.");
        }
        else if (changed)
        {
            Debug.Log("TemplatePack_DefaultNeon repaired/upgraded to 17 templates.");
        }

        if (changed)
        {
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
        }

        return pack;
    }

    public static ShapeTemplatePack EnsureAmoebaPack()
    {
        EnsureCanonicalFolders();
        EnsureFolder(AmoebaSilhouetteRoot);

        var pack = AssetDatabase.LoadAssetAtPath<ShapeTemplatePack>(AmoebaPackPath);
        if (pack == null)
        {
            pack = ScriptableObject.CreateInstance<ShapeTemplatePack>();
            pack.name = "TemplatePack_Amoeba";
            pack.tint = Color.white;
            AssetDatabase.CreateAsset(pack, AmoebaPackPath);
        }

        var templates = new List<ShapeTemplateBase>();
        for (var i = 0; i < AmoebaDefinitions.Length; i++)
        {
            var def = AmoebaDefinitions[i];
            var silhouettePath = $"{AmoebaSilhouetteRoot}/{def.FileName}";
            if (!File.Exists(silhouettePath))
            {
                Debug.LogWarning($"Amoeba silhouette missing: {silhouettePath}");
                continue;
            }

            var silhouette = AssetDatabase.LoadAssetAtPath<Texture2D>(silhouettePath);
            if (!ValidateSilhouette(silhouettePath, silhouette))
            {
                continue;
            }

            var template = CreateTemplate<SilhouetteShapeTemplate>(def.TemplateAssetName, t =>
            {
                t.ConfigureBase(def.ShapeId, "Blobs", 128, 16);
                t.ConfigureSilhouette(silhouette, 0.1f, true, 12);
            });
            template.ConfigureBase(def.ShapeId, "Blobs", 128, 16);
            template.ConfigureSilhouette(silhouette, 0.1f, true, 12);
            EditorUtility.SetDirty(template);
            templates.Add(template);
        }

        var changed = AssignPackTemplates(pack, templates);
        if (changed)
        {
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
        }

        return pack;
    }

    private static bool ValidateSilhouette(string path, Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogWarning($"Amoeba silhouette failed to load: {path}");
            return false;
        }

        if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
        {
            Debug.LogWarning($"Amoeba silhouette is not readable (enable Read/Write): {path}");
            return false;
        }

        var pixels = texture.GetPixels32();
        var nonTransparent = 0;
        var minX = texture.width;
        var minY = texture.height;
        var maxX = 0;
        var maxY = 0;

        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                var alpha = pixels[y * texture.width + x].a;
                if (alpha <= 8)
                {
                    continue;
                }

                nonTransparent++;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (nonTransparent < 32)
        {
            Debug.LogWarning($"Amoeba silhouette is empty or too small: {path}");
            return false;
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        if (width < 8 || height < 8)
        {
            Debug.LogWarning($"Amoeba silhouette footprint is tiny ({width}x{height}): {path}");
        }

        return true;
    }

    private static bool AssignPackTemplates(ShapeTemplatePack pack, List<ShapeTemplateBase> targetTemplates)
    {
        if (pack == null)
        {
            return false;
        }

        var changed = pack.templates.Count != targetTemplates.Count;
        if (!changed)
        {
            for (var i = 0; i < targetTemplates.Count; i++)
            {
                if (pack.templates[i] != targetTemplates[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        pack.templates.Clear();
        for (var i = 0; i < targetTemplates.Count; i++)
        {
            if (targetTemplates[i] != null)
            {
                pack.templates.Add(targetTemplates[i]);
            }
        }

        return true;
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

    private readonly struct AmoebaDefinition
    {
        public AmoebaDefinition(string fileName, string shapeId, string templateAssetName)
        {
            FileName = fileName;
            ShapeId = shapeId;
            TemplateAssetName = templateAssetName;
        }

        public string FileName { get; }
        public string ShapeId { get; }
        public string TemplateAssetName { get; }
    }
}
#endif
