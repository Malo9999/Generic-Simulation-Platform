using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "SimulationCatalog", menuName = "GSP/Simulation Catalog")]
public class SimulationCatalog : ScriptableObject
{
    [SerializeField] private ContentPack globalDefaultContentPack;
    [SerializeField] private List<SimulationCatalogEntry> simulations = new();

    public ContentPack GlobalDefaultContentPack => globalDefaultContentPack;
    public IReadOnlyList<SimulationCatalogEntry> Simulations => simulations;

    public SimulationCatalogEntry FindById(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        for (var i = 0; i < simulations.Count; i++)
        {
            var entry = simulations[i];
            if (entry != null && string.Equals(entry.simulationId, simulationId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Discover Simulations")]
    public void AutoDiscoverSimulations()
    {
        const string simulationsRoot = "Assets/Simulations";
        var previousEntries = new Dictionary<string, SimulationCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < simulations.Count; i++)
        {
            var existing = simulations[i];
            if (existing == null || string.IsNullOrWhiteSpace(existing.simulationId))
            {
                continue;
            }

            previousEntries[existing.simulationId] = existing;
        }

        var rebuiltEntries = new List<SimulationCatalogEntry>();

        if (!AssetDatabase.IsValidFolder(simulationsRoot))
        {
            Debug.LogWarning($"SimulationCatalog: Missing folder '{simulationsRoot}'.");
            return;
        }

        foreach (var directory in Directory.GetDirectories(simulationsRoot))
        {
            var simulationId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(simulationId))
            {
                continue;
            }

            var presetPath = $"{simulationsRoot}/{simulationId}/Presets/default.json";
            var preset = AssetDatabase.LoadAssetAtPath<TextAsset>(presetPath);

            var prefabPath = $"{simulationsRoot}/{simulationId}/{simulationId}Runner.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            previousEntries.TryGetValue(simulationId, out var existing);

            var preservedPreset = existing != null && existing.defaultPreset != null ? existing.defaultPreset : preset;
            var preservedContentPack = existing != null ? existing.defaultContentPack : null;

            rebuiltEntries.Add(new SimulationCatalogEntry
            {
                simulationId = simulationId,
                runnerPrefab = prefab,
                defaultPreset = preservedPreset,
                defaultContentPack = preservedContentPack
            });
        }

        rebuiltEntries.Sort((a, b) => string.Compare(a?.simulationId, b?.simulationId, StringComparison.OrdinalIgnoreCase));
        simulations = rebuiltEntries;
        AssignGlobalDefaultContentPackIfMissing();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"SimulationCatalog: Discovered {simulations.Count} simulations.");
    }

    public void AssignGlobalDefaultContentPackIfMissing()
    {
        if (globalDefaultContentPack != null)
        {
            return;
        }

        const string defaultPlaceholderPackPath = "Assets/_Bootstrap/DefaultPlaceholderContentPack.asset";
        var fallback = AssetDatabase.LoadAssetAtPath<ContentPack>(defaultPlaceholderPackPath);
        if (fallback == null)
        {
            return;
        }

        globalDefaultContentPack = fallback;
        EditorUtility.SetDirty(this);
    }
#endif
}

[Serializable]
public class SimulationCatalogEntry
{
    public string simulationId;
    public GameObject runnerPrefab;
    public TextAsset defaultPreset;
    public ContentPack defaultContentPack;
}
