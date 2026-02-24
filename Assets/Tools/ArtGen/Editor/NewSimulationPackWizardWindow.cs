using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class NewSimulationPackWizardWindow : EditorWindow
{
    private static readonly string[] AntSpeciesNames = { "FireAnt", "CarpenterAnt", "PharaohAnt", "WeaverAnt", "ArmyAnt" };

    private List<IPackPreset> presets;
    private int presetIndex;
    private string packId = "AntPack_Auto";
    private int seed = 12345;
    private int tileSize = 32;
    private int agentSpriteSize = 64;
    private bool overwrite;
    private readonly Dictionary<string, int> speciesOverrides = new();
    private string currentRecipePath;
    private Vector2 referencePlanScroll;
    private bool advancedReferencePlanEdit;
    private bool recipeWriteBlocked;
    private string assignmentStatusLine;

    [MenuItem("GSP/Art/New Simulation Pack…")]
    public static void Open() => GetWindow<NewSimulationPackWizardWindow>("New Simulation Pack");

    private void OnEnable()
    {
        presets = ModuleRegistry.ListPresets();
        if (presets.Count == 0) Debug.LogWarning("No pack presets found.");
    }

    private void OnGUI()
    {
        if (presets == null || presets.Count == 0)
        {
            EditorGUILayout.HelpBox("No presets discovered.", MessageType.Warning);
            EditorGUILayout.HelpBox("No IPackPreset implementations found. Ensure pack presets compile (not behind defines/asmdefs).", MessageType.Warning);
            if (GUILayout.Button("Log preset discovery diagnostics"))
            {
                LogPresetDiscoveryDiagnostics();
            }

            return;
        }

        var presetNames = presets.ConvertAll(p => p.PresetId).ToArray();
        presetIndex = EditorGUILayout.Popup("Preset", presetIndex, presetNames);
        packId = EditorGUILayout.TextField("Pack Id", packId);
        seed = EditorGUILayout.IntField("Seed", seed);
        tileSize = EditorGUILayout.IntField("Tile Size", tileSize);
        agentSpriteSize = EditorGUILayout.IntField("Agent Sprite Size", agentSpriteSize);
        overwrite = EditorGUILayout.Toggle("Overwrite", overwrite);

        var recipePath = BuildRecipePath();
        var recipeExists = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath) != null;
        EditorGUILayout.LabelField($"Recipe path: {recipePath} (exists: {(recipeExists ? "yes" : "no")})");

        var preview = presets[presetIndex].CreateDefaultRecipe(packId, seed);
        foreach (var entity in preview.entities)
        {
            var key = entity.entityId;
            var value = speciesOverrides.TryGetValue(key, out var saved) ? saved : entity.speciesCount;
            speciesOverrides[key] = EditorGUILayout.IntField($"{entity.entityId} Species", value);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("References-first Workflow", EditorStyles.boldLabel);

        if (GUILayout.Button("1) Create / Update Recipe"))
        {
            var recipe = CreateOrUpdateRecipe();
            if (recipe != null)
            {
                Debug.Log($"[Wizard] Recipe ready: {currentRecipePath}");
            }
        }

        if (recipeWriteBlocked)
        {
            EditorGUILayout.HelpBox("Recipe exists. Enable Overwrite or change PackId.", MessageType.Warning);
        }

        DrawReferencePlanTable();

        if (GUILayout.Button("2) Create Reference Folder Structure"))
        {
            var recipe = GetOrCreateRecipe();
            if (recipe != null)
            {
                var paths = ReferenceInboxScaffolder.EnsureStructure(recipe);
                for (var i = 0; i < paths.Length; i++)
                {
                    Debug.Log($"[References] {paths[i]}");
                }
            }
        }

        EditorGUILayout.HelpBox("3) Drop source images into _References/<Sim>/<Asset>/Images.", MessageType.Info);

        if (GUILayout.Button("4) Calibrate From References"))
        {
            var recipe = GetOrCreateRecipe();
            if (recipe != null)
            {
                var report = ReferenceCalibrationService.Calibrate(recipe);
                Debug.Log(report.Summary);
                for (var i = 0; i < report.warnings.Count; i++)
                {
                    Debug.LogWarning($"[References] {report.warnings[i]}");
                }
            }
        }

        if (GUILayout.Button("5) Build Pack"))
        {
            var recipe = GetOrCreateRecipe();
            if (recipe != null)
            {
                var report = PackBuildPipeline.Build(recipe, overwrite);
                var content = AssetDatabase.LoadAssetAtPath<ContentPack>($"{recipe.outputFolder}/ContentPack.asset");
                assignmentStatusLine = AssignDefaultContentPack(recipe.simulationId, content);
                EditorGUIUtility.PingObject(content);
                Debug.Log(report.Summary);
            }
        }

        if (!string.IsNullOrWhiteSpace(assignmentStatusLine))
        {
            EditorGUILayout.HelpBox(assignmentStatusLine, MessageType.Info);
        }
    }


    private static void LogPresetDiscoveryDiagnostics()
    {
        var presetTypes = ModuleRegistry.ListPackPresetTypes();
        var failures = ModuleRegistry.ListPresetInstantiationFailures();

        Debug.Log($"[PresetDiagnostics] IPackPreset type count: {presetTypes.Count}");
        for (var i = 0; i < presetTypes.Count; i++)
        {
            var type = presetTypes[i];
            Debug.Log($"[PresetDiagnostics] Type[{i}] {type.FullName}");
        }

        if (failures.Count == 0)
        {
            Debug.Log("[PresetDiagnostics] Instantiation failures: none");
            return;
        }

        Debug.LogWarning($"[PresetDiagnostics] Instantiation failures: {failures.Count}");
        for (var i = 0; i < failures.Count; i++)
        {
            Debug.LogWarning($"[PresetDiagnostics] Failure[{i}] {failures[i]}");
        }
    }

    private void DrawReferencePlanTable()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<PackRecipe>(BuildRecipePath());
        if (recipe == null)
        {
            EditorGUILayout.HelpBox("Create or update recipe to preview the reference asset plan.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Reference Asset Plan", EditorStyles.boldLabel);
        advancedReferencePlanEdit = EditorGUILayout.ToggleLeft("Advanced edit", advancedReferencePlanEdit);

        DrawReferencePlanHeader();
        using var scroll = new EditorGUILayout.ScrollViewScope(referencePlanScroll, GUILayout.MaxHeight(220f));
        referencePlanScroll = scroll.scrollPosition;

        using (new EditorGUI.DisabledScope(!advancedReferencePlanEdit))
        {
            for (var i = 0; i < recipe.referenceAssets.Count; i++)
            {
                var item = recipe.referenceAssets[i];
                if (item == null) continue;

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.BeginHorizontal();
                    item.assetId = EditorGUILayout.TextField(item.assetId ?? string.Empty);
                    item.entityId = EditorGUILayout.TextField(item.entityId ?? string.Empty);
                    item.mappedSpeciesId = EditorGUILayout.TextField(item.mappedSpeciesId ?? string.Empty);
                    item.minImages = Mathf.Max(1, EditorGUILayout.IntField(item.minImages));
                    item.variantCount = Mathf.Max(1, EditorGUILayout.IntField(item.variantCount));
                    item.generationMode = (PackRecipe.GenerationMode)EditorGUILayout.EnumPopup(item.generationMode);
                    EditorGUILayout.EndHorizontal();

                    var imagesFolder = GetAssetImageFolder(recipe.simulationId, item.assetId);
                    var foundCount = CountReferenceImages(imagesFolder);
                    EditorGUILayout.LabelField($"foundCount: {foundCount}");

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Create folders"))
                    {
                        ReferenceInboxScaffolder.EnsureAssetStructure(recipe.simulationId, item.assetId, item.minImages);
                    }

                    if (GUILayout.Button("Open folder") && Directory.Exists(imagesFolder))
                    {
                        EditorUtility.RevealInFinder(imagesFolder);
                    }

                    if (GUILayout.Button("Remove row"))
                    {
                        recipe.referenceAssets.RemoveAt(i);
                        EditorUtility.SetDirty(recipe);
                        AssetDatabase.SaveAssets();
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        if (!advancedReferencePlanEdit)
        {
            EditorGUILayout.HelpBox("Plan is read-only. Enable Advanced edit to add, remove, or modify rows.", MessageType.Info);
        }

        if (advancedReferencePlanEdit && GUILayout.Button("Add row"))
        {
            recipe.referenceAssets.Add(new PackRecipe.ReferenceAssetNeed { assetId = "NewAsset", minImages = 1 });
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        if (advancedReferencePlanEdit && GUI.changed)
        {
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }
    }

    private static void DrawReferencePlanHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("assetId", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("entityId", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("mappedSpeciesId", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("minImages", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("variants", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("mode", EditorStyles.miniBoldLabel);
        }
    }

    private static string GetAssetImageFolder(string simulationId, string assetId)
    {
        return Path.Combine(ReferenceInboxScaffolder.ProjectRoot(), "_References", simulationId, assetId ?? string.Empty, "Images");
    }

    private static int CountReferenceImages(string imagesFolder)
    {
        if (!Directory.Exists(imagesFolder)) return 0;
        var files = Directory.GetFiles(imagesFolder);
        var count = 0;
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".webp") count++;
        }

        return count;
    }

    private PackRecipe GetOrCreateRecipe()
    {
        var path = BuildRecipePath();
        var existing = AssetDatabase.LoadAssetAtPath<PackRecipe>(path);
        if (existing != null)
        {
            currentRecipePath = path;
            return existing;
        }

        return CreateOrUpdateRecipe();
    }

    private PackRecipe CreateOrUpdateRecipe()
    {
        var recipe = presets[presetIndex].CreateDefaultRecipe(packId, seed);
        recipe.tileSize = tileSize;
        recipe.agentSpriteSize = agentSpriteSize;
        recipe.outputFolder = $"Assets/Presentation/Packs/{recipe.simulationId}/{packId}";
        ImportSettingsUtil.EnsureFolder(recipe.outputFolder);
        foreach (var entity in recipe.entities)
        {
            if (speciesOverrides.TryGetValue(entity.entityId, out var count)) entity.speciesCount = Mathf.Max(1, count);
        }

        ApplySpeciesCountOverrides(recipe, speciesOverrides);

        var recipePath = BuildRecipePath(recipe);
        var existing = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath);
        recipeWriteBlocked = existing != null && !overwrite;
        if (recipeWriteBlocked)
        {
            DestroyImmediate(recipe);
            currentRecipePath = recipePath;
            return null;
        }

        if (existing != null)
        {
            EditorUtility.CopySerialized(recipe, existing);
            DestroyImmediate(recipe);
            recipe = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(recipe, recipePath);
        }

        currentRecipePath = recipePath;
        AssetDatabase.SaveAssets();
        return recipe;
    }

    private static void ApplySpeciesCountOverrides(PackRecipe recipe, IReadOnlyDictionary<string, int> uiSpeciesOverrides)
    {
        if (recipe == null || !string.Equals(recipe.simulationId, "AntColonies", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var antSpeciesCount = 1;
        for (var i = 0; i < recipe.entities.Count; i++)
        {
            var entity = recipe.entities[i];
            if (!string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase)) continue;

            if (uiSpeciesOverrides.TryGetValue(entity.entityId, out var overriddenCount))
            {
                entity.speciesCount = overriddenCount;
            }

            antSpeciesCount = Mathf.Clamp(entity.speciesCount, 1, AntSpeciesNames.Length);
            entity.speciesCount = antSpeciesCount;
            break;
        }

        var updatedReferenceAssets = new List<PackRecipe.ReferenceAssetNeed>();
        for (var i = 0; i < antSpeciesCount; i++)
        {
            var antName = AntSpeciesNames[i];
            updatedReferenceAssets.Add(new PackRecipe.ReferenceAssetNeed
            {
                assetId = antName,
                entityId = "ant",
                mappedSpeciesId = antName,
                minImages = 1,
                generationMode = PackRecipe.GenerationMode.OutlineDriven,
                variantCount = 1
            });
        }

        for (var i = 0; i < recipe.referenceAssets.Count; i++)
        {
            var existing = recipe.referenceAssets[i];
            if (existing == null) continue;
            var isNamedAnt = Array.Exists(AntSpeciesNames, name => string.Equals(name, existing.assetId, StringComparison.OrdinalIgnoreCase));
            if (isNamedAnt) continue;
            updatedReferenceAssets.Add(existing);
        }

        recipe.referenceAssets = updatedReferenceAssets;
    }

    private string BuildRecipePath()
    {
        var recipe = presets[presetIndex].CreateDefaultRecipe(packId, seed);
        var path = BuildRecipePath(recipe);
        DestroyImmediate(recipe);
        return path;
    }

    private static string BuildRecipePath(PackRecipe recipe) => $"Assets/Presentation/Packs/{recipe.simulationId}/{recipe.packId}/PackRecipe.asset";
    private static string AssignDefaultContentPack(string simulationId, ContentPack builtContentPack)
    {
        if (builtContentPack == null || string.IsNullOrWhiteSpace(simulationId))
        {
            return "Default content pack assignment skipped: missing simulation id or generated ContentPack.";
        }

        var catalog = FindSimulationCatalog();
        if (catalog == null)
        {
            Debug.LogWarning("NewSimulationPackWizardWindow: SimulationCatalog not found; skipping default content pack assignment.");
            return "Default content pack assignment skipped: SimulationCatalog not found.";
        }

        var entry = catalog.FindById(simulationId);
        if (entry == null)
        {
            Debug.LogWarning($"NewSimulationPackWizardWindow: Simulation '{simulationId}' not found in catalog.");
            return $"Default content pack assignment skipped: simulation '{simulationId}' not found in SimulationCatalog.";
        }

        entry.defaultContentPack = builtContentPack;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        var packPath = AssetDatabase.GetAssetPath(builtContentPack);
        return $"Assigned Default Content Pack for {simulationId} = {packPath}";
    }

    private static SimulationCatalog FindSimulationCatalog()
    {
        const string preferredPath = "Assets/_Bootstrap/SimulationCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(preferredPath);
        if (catalog != null)
        {
            return catalog;
        }

        var guids = AssetDatabase.FindAssets("t:SimulationCatalog");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            catalog = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(path);
            if (catalog != null)
            {
                return catalog;
            }
        }

        return null;
    }

}
