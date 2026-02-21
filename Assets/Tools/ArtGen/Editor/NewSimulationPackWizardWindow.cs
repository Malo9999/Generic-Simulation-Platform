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

        if (GUILayout.Button("Create"))
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

            var recipePath = $"{recipe.outputFolder}/PackRecipe.asset";
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

            var report = PackBuildPipeline.Build(recipe, overwrite);
            var content = AssetDatabase.LoadAssetAtPath<ContentPack>($"{recipe.outputFolder}/ContentPack.asset");
            AssignDefaultContentPack(recipe.simulationId, content);
            EditorGUIUtility.PingObject(content);
            Debug.Log(report.Summary);
        }
    }
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
