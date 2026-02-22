#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SimulationCatalogBuilder
{
    private const string SimulationsRoot = "Assets/Simulations";
    private const string BootstrapFolder = "Assets/_Bootstrap";
    private const string CatalogAssetPath = BootstrapFolder + "/SimulationCatalog.asset";
    private const string BootstrapOptionsAssetPath = BootstrapFolder + "/BootstrapOptions.asset";

    [MenuItem("GSP/Dev/Rebuild Simulation Catalog")]
    public static void RebuildCatalogAndCreateRunnerPrefabs()
    {
        EnsureFolder(BootstrapFolder);

        var catalog = EnsureAsset<SimulationCatalog>(CatalogAssetPath);
        var options = EnsureAsset<BootstrapOptions>(BootstrapOptionsAssetPath);

        if (options.simulationCatalog != catalog)
        {
            options.simulationCatalog = catalog;
            EditorUtility.SetDirty(options);
        }

        if (!AssetDatabase.IsValidFolder(SimulationsRoot))
        {
            Debug.LogError($"SimulationCatalogBuilder: Missing simulations root '{SimulationsRoot}'.");
            return;
        }

        foreach (var directory in Directory.GetDirectories(SimulationsRoot))
        {
            var simulationId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(simulationId))
            {
                continue;
            }

            EnsureDefaultPreset(simulationId);
            EnsureRunnerPrefab(simulationId);
            ValidateRunnerPrefab(simulationId);
        }

        catalog.AutoDiscoverSimulations();
        catalog.AssignGlobalDefaultContentPackIfMissing();
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(options);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SimulationCatalogBuilder: Rebuilt catalog and validated runner prefabs.");
    }

    private static T EnsureAsset<T>(string assetPath) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void EnsureDefaultPreset(string simulationId)
    {
        var presetFolder = $"{SimulationsRoot}/{simulationId}/Presets";
        EnsureFolder(presetFolder);

        var presetPath = $"{presetFolder}/default.json";
        if (File.Exists(presetPath))
        {
            return;
        }

        var json = "{\n  \"simulationId\": \"" + simulationId + "\"\n}";
        File.WriteAllText(presetPath, json);
        AssetDatabase.ImportAsset(presetPath);
        Debug.Log($"SimulationCatalogBuilder: Created missing preset '{presetPath}'.");
    }

    private static void EnsureRunnerPrefab(string simulationId)
    {
        var prefabPath = $"{SimulationsRoot}/{simulationId}/{simulationId}Runner.prefab";
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            return;
        }

        var runnerTypeName = $"{simulationId}Runner";
        var runnerType = FindTypeByName(runnerTypeName);

        if (runnerType == null || !typeof(MonoBehaviour).IsAssignableFrom(runnerType))
        {
            Debug.LogError($"SimulationCatalogBuilder: Could not find runner type '{runnerTypeName}'. Create or rename the runner script to match this name.");
            return;
        }

        var tempRunner = new GameObject(runnerTypeName);
        tempRunner.AddComponent(runnerType);
        PrefabUtility.SaveAsPrefabAsset(tempRunner, prefabPath);
        UnityEngine.Object.DestroyImmediate(tempRunner);

        AssetDatabase.ImportAsset(prefabPath);
        Debug.Log($"SimulationCatalogBuilder: Created missing runner prefab '{prefabPath}'.");
    }

    private static void ValidateRunnerPrefab(string simulationId)
    {
        var prefabPath = $"{SimulationsRoot}/{simulationId}/{simulationId}Runner.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"SimulationCatalogBuilder: Missing runner prefab '{prefabPath}'.");
            return;
        }

        var hasRunner = prefab.GetComponents<MonoBehaviour>()
            .Any(component => component != null && component is ISimulationRunner);

        if (!hasRunner)
        {
            Debug.LogError($"SimulationCatalogBuilder: Prefab '{prefabPath}' does not contain a component implementing ISimulationRunner.");
        }
    }

    private static Type FindTypeByName(string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            for (var j = 0; j < types.Length; j++)
            {
                var type = types[j];
                if (type != null && type.Name == typeName)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        var normalizedPath = assetFolderPath.Replace("\\", "/");
        var parts = normalizedPath.Split('/');
        var current = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
#endif
