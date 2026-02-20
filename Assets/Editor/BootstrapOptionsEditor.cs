#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BootstrapOptionsEditor
{
    private const string AssetPath = "Assets/_Bootstrap/BootstrapOptions.asset";

    [MenuItem("Tools/GSP/Create Bootstrap Options Asset")]
    public static void CreateOrFindBootstrapOptions()
    {
        var asset = AssetDatabase.LoadAssetAtPath<BootstrapOptions>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BootstrapOptions>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created BootstrapOptions asset at {AssetPath}");
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("Tools/GSP/Create Placeholder Runner Prefabs")]
    public static void CreatePlaceholderRunnerPrefabs()
    {
        CreatePrefab("AntColonies", "AntColoniesRunner");
        CreatePrefab("MarbleRace", "MarbleRaceRunner");
        CreatePrefab("RaceCar", "RaceCarRunner");
        CreatePrefab("FantasySport", "FantasySportRunner");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Placeholder runner prefabs created/updated under Assets/Resources/Simulations.");
    }

    private static void CreatePrefab(string simulationId, string componentType)
    {
        var folder = $"Assets/Resources/Simulations/{simulationId}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            var parent = "Assets/Resources/Simulations";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Simulations");
            }

            AssetDatabase.CreateFolder("Assets/Resources/Simulations", simulationId);
        }

        var prefabPath = $"{folder}/{simulationId}Runner.prefab";
        var root = new GameObject($"{simulationId}Runner");
        var type = System.Type.GetType(componentType) ?? System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == componentType);

        if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            root.AddComponent(type);
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }
}
#endif
