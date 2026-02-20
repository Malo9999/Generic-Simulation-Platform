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
    [SerializeField] private List<SimulationCatalogEntry> simulations = new();

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
        simulations.Clear();

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

            simulations.Add(new SimulationCatalogEntry
            {
                simulationId = simulationId,
                runnerPrefab = prefab,
                defaultPreset = preset
            });
        }

        simulations.Sort((a, b) => string.Compare(a?.simulationId, b?.simulationId, StringComparison.OrdinalIgnoreCase));
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"SimulationCatalog: Discovered {simulations.Count} simulations.");
    }
#endif
}

[Serializable]
public class SimulationCatalogEntry
{
    public string simulationId;
    public GameObject runnerPrefab;
    public TextAsset defaultPreset;
}
