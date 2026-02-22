using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class ManualAntPixelSheetImporterWindow : EditorWindow
{
    private const string MenuPath = "Tools/Generic Simulation Platform/Art/Import Manual Pixel Sheets…";
    private const string ValidateMenuPath = "Tools/Generic Simulation Platform/Art/Validate Ant ContentPack";
    private const string DefaultSourceFolder = "Assets/Presentation/ReferencePacks/AntColonies/Manual";

    private static readonly string[] SpeciesIds = { "fire_ant", "carpenter_ant", "pharaoh_ant" };
    private static readonly string[] RoleIds = { "worker", "soldier", "queen" };

    private string sourceFolder = DefaultSourceFolder;
    private ContentPack targetContentPack;
    private SheetLayoutOption selectedLayout = SheetLayoutOption.Idle2Walk4Run4;
    private int pixelsPerUnit = 64;
    private int frameSize = 64;
    private int spacing = 2;
    private int defaultFps = 8;

    private enum SheetLayoutOption
    {
        Idle2Walk4Run4,
        Idle2Walk3Run4Fight1
    }

    private readonly struct FrameDef
    {
        public readonly string State;
        public readonly int FrameIndex;

        public FrameDef(string state, int frameIndex)
        {
            State = state;
            FrameIndex = frameIndex;
        }
    }

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        var window = GetWindow<ManualAntPixelSheetImporterWindow>("Manual Ant Sheets");
        window.minSize = new Vector2(540, 320);
        window.EnsureDefaults();
    }

    [MenuItem(ValidateMenuPath)]
    public static void ValidateAntContentPackMenu()
    {
        var pack = ResolveTargetContentPack(null);
        if (pack == null)
        {
            Debug.LogWarning("[ManualAntImporter] No ContentPack selected and no fallback ContentPack found under Assets/Presentation/Packs/AntColonies.");
            return;
        }

        var result = ValidatePack(pack, SheetLayoutOption.Idle2Walk4Run4);
        if (result.missingIds.Count == 0)
        {
            Debug.Log($"[ManualAntImporter] Validation passed for '{pack.name}'. Checked {result.checkedCount} sprite IDs.");
            return;
        }

        Debug.LogWarning($"[ManualAntImporter] Validation found {result.missingIds.Count} missing sprite IDs in '{pack.name}'.\n{string.Join("\n", result.missingIds)}");
    }

    private void OnEnable() => EnsureDefaults();

    private void EnsureDefaults()
    {
        if (targetContentPack == null)
        {
            targetContentPack = ResolveFallbackContentPack("AntColonies");
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Manual Ant Pixel Sheet Importer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourceFolder = EditorGUILayout.TextField("Source folder", sourceFolder);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(EditorGUIUtility.labelWidth);
            if (GUILayout.Button("Use default", GUILayout.Width(120)))
            {
                sourceFolder = DefaultSourceFolder;
            }
        }

        targetContentPack = (ContentPack)EditorGUILayout.ObjectField("Target ContentPack", ResolveTargetContentPack(targetContentPack), typeof(ContentPack), false);
        selectedLayout = (SheetLayoutOption)EditorGUILayout.EnumPopup("Layout", selectedLayout);
        frameSize = EditorGUILayout.IntField("Frame size", frameSize);
        spacing = EditorGUILayout.IntField("Cell spacing", spacing);
        pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);
        defaultFps = EditorGUILayout.IntField("Clip FPS", defaultFps);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Species (fixed v1)", string.Join(", ", SpeciesIds));
        EditorGUILayout.LabelField("Roles", "worker, soldier, queen");

        EditorGUILayout.Space();
        if (GUILayout.Button("Import Manual Pixel Sheets"))
        {
            RunImport();
        }

        if (GUILayout.Button("Validate Target ContentPack"))
        {
            var pack = ResolveTargetContentPack(targetContentPack);
            if (pack == null)
            {
                Debug.LogWarning("[ManualAntImporter] No target ContentPack found.");
                return;
            }

            var result = ValidatePack(pack, selectedLayout);
            if (result.missingIds.Count == 0)
            {
                Debug.Log($"[ManualAntImporter] Validation passed for '{pack.name}'. Checked {result.checkedCount} sprite IDs.");
            }
            else
            {
                Debug.LogWarning($"[ManualAntImporter] Validation found {result.missingIds.Count} missing sprite IDs in '{pack.name}'.\n{string.Join("\n", result.missingIds)}");
            }
        }
    }

    private void RunImport()
    {
        var pack = ResolveTargetContentPack(targetContentPack);
        if (pack == null)
        {
            Debug.LogError("[ManualAntImporter] Could not resolve target ContentPack.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(sourceFolder))
        {
            Debug.LogError($"[ManualAntImporter] Source folder does not exist: {sourceFolder}");
            return;
        }

        var frameDefs = GetFrameDefs(selectedLayout);
        var importedById = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        var missingPairs = new List<string>();
        var importedSheetCount = 0;

        foreach (var speciesId in SpeciesIds)
        {
            foreach (var roleId in new[] { "worker", "soldier" })
            {
                if (!TryImportRoleSheets(speciesId, roleId, frameDefs, importedById, ref importedSheetCount, out var reason))
                {
                    missingPairs.Add(reason);
                }
            }

            var queenImported = TryImportRoleSheets(speciesId, "queen", frameDefs, importedById, ref importedSheetCount, out var queenReason);
            if (!queenImported)
            {
                missingPairs.Add(queenReason);
                ApplyQueenFallbackFromWorker(speciesId, frameDefs, importedById);
            }
        }

        var updatedCount = UpsertSpritesIntoPack(pack, importedById);
        UpsertClipMetadata(pack, selectedLayout, Mathf.Max(1, defaultFps));
        UpsertSpeciesSelection(pack);

        EditorUtility.SetDirty(pack);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ManualAntImporter] Import complete. Sheets imported={importedSheetCount}, Sprites imported={importedById.Count}, Pack entries updated={updatedCount}, Missing pairs={missingPairs.Count}." +
                  (missingPairs.Count > 0 ? $"\nMissing:\n{string.Join("\n", missingPairs)}" : string.Empty));
    }

    private bool TryImportRoleSheets(string speciesId, string roleId, IReadOnlyList<FrameDef> frameDefs, IDictionary<string, Sprite> importedById, ref int importedSheetCount, out string reason)
    {
        reason = string.Empty;
        var basePath = $"{sourceFolder}/{speciesId}_{roleId}_base.png";
        var maskPath = $"{sourceFolder}/{speciesId}_{roleId}_mask.png";

        var baseExists = File.Exists(Path.GetFullPath(basePath));
        var maskExists = File.Exists(Path.GetFullPath(maskPath));
        if (!baseExists || !maskExists)
        {
            reason = $"{speciesId}/{roleId}: {(baseExists ? "missing mask" : maskExists ? "missing base" : "missing base+mask")}";
            return false;
        }

        AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);

        ImportAndCollectSprites(basePath, speciesId, roleId, frameDefs, false, importedById);
        ImportAndCollectSprites(maskPath, speciesId, roleId, frameDefs, true, importedById);
        importedSheetCount += 2;
        return true;
    }

    private void ImportAndCollectSprites(string texturePath, string speciesId, string roleId, IReadOnlyList<FrameDef> frameDefs, bool isMask, IDictionary<string, Sprite> importedById)
    {
        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
        {
            Debug.LogError($"[ManualAntImporter] Could not load importer for {texturePath}");
            return;
        }

        var border = DetectOuterBorder(texturePath, frameSize, spacing);
        var spriteRects = BuildSpriteRects(speciesId, roleId, frameDefs, isMask, frameSize, spacing, border);
        ConfigureTextureImporter(importer, pixelsPerUnit, isMask);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var existingRects = provider.GetSpriteRects();
        if (existingRects is { Length: > 0 })
        {
            var existingByName = existingRects.ToDictionary(x => x.name, x => x, StringComparer.Ordinal);
            for (var i = 0; i < spriteRects.Count; i++)
            {
                if (!existingByName.TryGetValue(spriteRects[i].name, out var existing))
                {
                    continue;
                }

                var rect = spriteRects[i];
                rect.spriteID = existing.spriteID;
                spriteRects[i] = rect;
            }
        }

        provider.SetSpriteRects(spriteRects.ToArray());
        var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider?.SetNameFileIdPairs(spriteRects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

        var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>();
        foreach (var sprite in sprites)
        {
            importedById[sprite.name] = sprite;
        }
    }

    private static List<SpriteRect> BuildSpriteRects(string speciesId, string roleId, IReadOnlyList<FrameDef> frameDefs, bool isMask, int size, int gap, int border)
    {
        const int columns = 5;
        const int rows = 2;
        var rects = new List<SpriteRect>(frameDefs.Count);

        for (var i = 0; i < frameDefs.Count; i++)
        {
            var col = i % columns;
            var rowFromTop = i / columns;
            var rowFromBottom = (rows - 1) - rowFromTop;
            var x = border + col * (size + gap);
            var y = border + rowFromBottom * (size + gap);
            var id = $"agent:ant:{speciesId}:{roleId}:adult:{frameDefs[i].State}:{frameDefs[i].FrameIndex:00}";
            if (isMask)
            {
                id += "_mask";
            }

            rects.Add(new SpriteRect
            {
                name = id,
                rect = new Rect(x, y, size, size),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        return rects;
    }

    private static int DetectOuterBorder(string texturePath, int size, int gap)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            return 0;
        }

        const int columns = 5;
        const int rows = 2;
        var baseWidth = columns * size + (columns - 1) * gap;
        var baseHeight = rows * size + (rows - 1) * gap;

        if (texture.width == baseWidth && texture.height == baseHeight)
        {
            return 0;
        }

        var borderedWidth = baseWidth + (gap * 2);
        var borderedHeight = baseHeight + (gap * 2);
        return texture.width == borderedWidth && texture.height == borderedHeight ? gap : 0;
    }

    private static void ConfigureTextureImporter(TextureImporter importer, int ppu, bool isMask)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = Mathf.Max(1, ppu);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = !isMask || importer.sRGBTexture;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
    }

    private static int UpsertSpritesIntoPack(ContentPack pack, Dictionary<string, Sprite> importedById)
    {
        var textureEntries = pack.Textures?.ToList() ?? new List<ContentPack.TextureEntry>();
        var spriteEntries = pack.Sprites?.ToList() ?? new List<ContentPack.SpriteEntry>();

        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < spriteEntries.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(spriteEntries[i].id))
            {
                indexById[spriteEntries[i].id] = i;
            }
        }

        var updated = 0;
        foreach (var pair in importedById)
        {
            var entry = new ContentPack.SpriteEntry
            {
                id = pair.Key,
                category = "agent",
                sprite = pair.Value
            };

            if (indexById.TryGetValue(pair.Key, out var existingIndex))
            {
                var existingCategory = spriteEntries[existingIndex].category;
                if (!string.IsNullOrWhiteSpace(existingCategory))
                {
                    entry.category = existingCategory;
                }

                spriteEntries[existingIndex] = entry;
            }
            else
            {
                indexById[pair.Key] = spriteEntries.Count;
                spriteEntries.Add(entry);
            }

            updated++;
        }

        pack.SetEntries(textureEntries, spriteEntries);
        return updated;
    }

    private static void UpsertClipMetadata(ContentPack pack, SheetLayoutOption layout, int fps)
    {
        var metadata = pack.ClipMetadata?.ToList() ?? new List<ContentPack.ClipMetadataEntry>();
        var byPrefix = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < metadata.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(metadata[i].keyPrefix))
            {
                byPrefix[metadata[i].keyPrefix] = i;
            }
        }

        var states = GetStateFrameCounts(layout);
        foreach (var roleId in RoleIds)
        {
            foreach (var stateEntry in states)
            {
                var key = $"agent:ant:{roleId}:adult:{stateEntry.Key}";
                var clipEntry = new ContentPack.ClipMetadataEntry
                {
                    keyPrefix = key,
                    fps = fps,
                    frameCount = stateEntry.Value
                };

                if (byPrefix.TryGetValue(key, out var index))
                {
                    metadata[index] = clipEntry;
                }
                else
                {
                    byPrefix[key] = metadata.Count;
                    metadata.Add(clipEntry);
                }
            }
        }

        pack.SetClipMetadata(metadata);
    }

    private static void UpsertSpeciesSelection(ContentPack pack)
    {
        var selections = pack.Selections?.ToList() ?? new List<ContentPack.SpeciesSelection>();
        var antIndex = -1;
        for (var i = 0; i < selections.Count; i++)
        {
            if (string.Equals(selections[i].entityId, "ant", StringComparison.OrdinalIgnoreCase))
            {
                antIndex = i;
                break;
            }
        }

        var antSelection = new ContentPack.SpeciesSelection
        {
            entityId = "ant",
            speciesIds = SpeciesIds.ToList()
        };

        if (antIndex >= 0)
        {
            selections[antIndex] = antSelection;
        }
        else
        {
            selections.Add(antSelection);
        }

        pack.SetSelections(selections);
    }

    private static void ApplyQueenFallbackFromWorker(string speciesId, IReadOnlyList<FrameDef> frameDefs, IDictionary<string, Sprite> importedById)
    {
        foreach (var frame in frameDefs)
        {
            var workerBaseId = $"agent:ant:{speciesId}:worker:adult:{frame.State}:{frame.FrameIndex:00}";
            var workerMaskId = workerBaseId + "_mask";
            var queenBaseId = $"agent:ant:{speciesId}:queen:adult:{frame.State}:{frame.FrameIndex:00}";
            var queenMaskId = queenBaseId + "_mask";

            if (importedById.TryGetValue(workerBaseId, out var baseSprite))
            {
                importedById[queenBaseId] = baseSprite;
            }

            if (importedById.TryGetValue(workerMaskId, out var maskSprite))
            {
                importedById[queenMaskId] = maskSprite;
            }
        }
    }

    private static IReadOnlyList<FrameDef> GetFrameDefs(SheetLayoutOption layout)
    {
        return layout switch
        {
            SheetLayoutOption.Idle2Walk3Run4Fight1 => new[]
            {
                new FrameDef("idle", 0), new FrameDef("idle", 1),
                new FrameDef("walk", 0), new FrameDef("walk", 1), new FrameDef("walk", 2),
                new FrameDef("run", 0), new FrameDef("run", 1), new FrameDef("run", 2), new FrameDef("run", 3),
                new FrameDef("fight", 0)
            },
            _ => new[]
            {
                new FrameDef("idle", 0), new FrameDef("idle", 1),
                new FrameDef("walk", 0), new FrameDef("walk", 1), new FrameDef("walk", 2),
                new FrameDef("walk", 3),
                new FrameDef("run", 0), new FrameDef("run", 1), new FrameDef("run", 2), new FrameDef("run", 3)
            }
        };
    }

    private static Dictionary<string, int> GetStateFrameCounts(SheetLayoutOption layout)
    {
        return layout switch
        {
            SheetLayoutOption.Idle2Walk3Run4Fight1 => new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["idle"] = 2,
                ["walk"] = 3,
                ["run"] = 4,
                ["fight"] = 1
            },
            _ => new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["idle"] = 2,
                ["walk"] = 4,
                ["run"] = 4
            }
        };
    }

    private static (int checkedCount, List<string> missingIds) ValidatePack(ContentPack pack, SheetLayoutOption layout)
    {
        var missing = new List<string>();
        var frameDefs = GetFrameDefs(layout);
        var checkedCount = 0;

        foreach (var speciesId in SpeciesIds)
        {
            foreach (var roleId in RoleIds)
            {
                foreach (var frameDef in frameDefs)
                {
                    if (frameDef.State == "fight")
                    {
                        continue;
                    }

                    var baseId = $"agent:ant:{speciesId}:{roleId}:adult:{frameDef.State}:{frameDef.FrameIndex:00}";
                    var maskId = baseId + "_mask";

                    checkedCount += 2;
                    if (!pack.TryGetSprite(baseId, out _))
                    {
                        missing.Add(baseId);
                    }

                    if (!pack.TryGetSprite(maskId, out _))
                    {
                        missing.Add(maskId);
                    }
                }
            }
        }

        return (checkedCount, missing);
    }

    private static ContentPack ResolveTargetContentPack(ContentPack overridePack)
    {
        if (overridePack != null)
        {
            return overridePack;
        }

        if (Selection.activeObject is ContentPack selectedPack)
        {
            return selectedPack;
        }

        return ResolveFallbackContentPack("AntColonies");
    }

    private static ContentPack ResolveFallbackContentPack(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        var searchRoot = $"Assets/Presentation/Packs/{simulationId}";
        if (!AssetDatabase.IsValidFolder(searchRoot))
        {
            return null;
        }

        var guids = AssetDatabase.FindAssets("t:ContentPack", new[] { searchRoot });
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        ContentPack bestPack = null;
        DateTime newestWriteTime = DateTime.MinValue;
        string bestPath = null;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var absolutePath = Path.GetFullPath(path);
            var writeTime = File.Exists(absolutePath) ? File.GetLastWriteTimeUtc(absolutePath) : DateTime.MinValue;
            var candidate = AssetDatabase.LoadAssetAtPath<ContentPack>(path);
            if (candidate == null)
            {
                continue;
            }

            var isNewer = writeTime > newestWriteTime;
            var isTieButEarlierPath = writeTime == newestWriteTime && string.Compare(path, bestPath, StringComparison.Ordinal) < 0;
            if (!isNewer && !isTieButEarlierPath)
            {
                continue;
            }

            newestWriteTime = writeTime;
            bestPath = path;
            bestPack = candidate;
        }

        return bestPack;
    }
}
