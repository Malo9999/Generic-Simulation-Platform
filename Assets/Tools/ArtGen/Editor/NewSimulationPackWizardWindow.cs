using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class NewSimulationPackWizardWindow : EditorWindow
{
    private List<IPackPreset> presets;
    private int presetIndex;
    private string packId = "AntPack_Auto";
    private int seed = 12345;
    private int tileSize = 32;
    private int agentSpriteSize = 64;
    private bool overwrite;
    private readonly Dictionary<string, int> speciesOverrides = new();
    private string currentRecipePath;

    [MenuItem("Tools/Generic Simulation Platform/Packs/New Simulation Pack…")]
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
            return;
        }

        var presetNames = presets.ConvertAll(p => p.PresetId).ToArray();
        presetIndex = EditorGUILayout.Popup("Preset", presetIndex, presetNames);
        packId = EditorGUILayout.TextField("Pack Id", packId);
        seed = EditorGUILayout.IntField("Seed", seed);
        tileSize = EditorGUILayout.IntField("Tile Size", tileSize);
        agentSpriteSize = EditorGUILayout.IntField("Agent Sprite Size", agentSpriteSize);
        overwrite = EditorGUILayout.Toggle("Overwrite", overwrite);

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
                AssignDefaultContentPack(recipe.simulationId, content);
                EditorGUIUtility.PingObject(content);
                Debug.Log(report.Summary);
            }
        }
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

        var recipePath = BuildRecipePath(recipe);
        var existing = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath);
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

    private string BuildRecipePath()
    {
        var recipe = presets[presetIndex].CreateDefaultRecipe(packId, seed);
        var path = BuildRecipePath(recipe);
        DestroyImmediate(recipe);
        return path;
    }

    private static string BuildRecipePath(PackRecipe recipe) => $"Assets/Presentation/Packs/{recipe.simulationId}/{recipe.packId}/PackRecipe.asset";
    private static void AssignDefaultContentPack(string simulationId, ContentPack builtContentPack)
    {
        if (builtContentPack == null || string.IsNullOrWhiteSpace(simulationId))
        {
            return;
        }

        var catalog = FindSimulationCatalog();
        if (catalog == null)
        {
            Debug.LogWarning("NewSimulationPackWizardWindow: SimulationCatalog not found; skipping default content pack assignment.");
            return;
        }

        var entry = catalog.FindById(simulationId);
        if (entry == null)
        {
            Debug.LogWarning($"NewSimulationPackWizardWindow: Simulation '{simulationId}' not found in catalog.");
            return;
        }

        entry.defaultContentPack = builtContentPack;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
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
