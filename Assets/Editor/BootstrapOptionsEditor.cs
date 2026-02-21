#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BootstrapOptions))]
public class BootstrapOptionsInspector : Editor
{
    private SerializedProperty simulationIdProp;
    private SerializedProperty simulationCatalogProp;
    private SerializedProperty presetJsonProp;
    private SerializedProperty seedPolicyProp;
    private SerializedProperty fixedSeedProp;
    private SerializedProperty allowHotkeySwitchProp;
    private SerializedProperty showOverlayProp;
    private SerializedProperty persistSelectionToPresetProp;

    private void OnEnable()
    {
        simulationIdProp = serializedObject.FindProperty("simulationId");
        simulationCatalogProp = serializedObject.FindProperty("simulationCatalog");
        presetJsonProp = serializedObject.FindProperty("presetJson");
        seedPolicyProp = serializedObject.FindProperty("seedPolicy");
        fixedSeedProp = serializedObject.FindProperty("fixedSeed");
        allowHotkeySwitchProp = serializedObject.FindProperty("allowHotkeySwitch");
        showOverlayProp = serializedObject.FindProperty("showOverlay");
        persistSelectionToPresetProp = serializedObject.FindProperty("persistSelectionToPreset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(simulationCatalogProp);
        DrawSimulationIdField();

        EditorGUILayout.PropertyField(presetJsonProp);
        EditorGUILayout.PropertyField(seedPolicyProp);
        EditorGUILayout.PropertyField(fixedSeedProp);
        EditorGUILayout.PropertyField(allowHotkeySwitchProp);
        EditorGUILayout.PropertyField(showOverlayProp);
        EditorGUILayout.PropertyField(persistSelectionToPresetProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSimulationIdField()
    {
        var catalog = simulationCatalogProp.objectReferenceValue as SimulationCatalog;
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("Assign a SimulationCatalog to choose a simulation from a dropdown.", MessageType.Warning);
            EditorGUILayout.PropertyField(simulationIdProp);
            return;
        }

        var simulationIds = catalog.Simulations
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.simulationId))
            .Select(entry => entry.simulationId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (simulationIds.Length == 0)
        {
            EditorGUILayout.HelpBox("SimulationCatalog has no simulation entries. Enter a simulationId manually.", MessageType.Warning);
            EditorGUILayout.PropertyField(simulationIdProp);
            return;
        }

        var currentSimulationId = simulationIdProp.stringValue;
        var selectedIndex = Array.FindIndex(simulationIds, id =>
            string.Equals(id, currentSimulationId, StringComparison.OrdinalIgnoreCase));

        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(currentSimulationId))
        {
            EditorGUILayout.HelpBox($"Current simulationId '{currentSimulationId}' is not in the SimulationCatalog.", MessageType.Info);
        }

        selectedIndex = Mathf.Max(0, selectedIndex);
        var newIndex = EditorGUILayout.Popup("Simulation Id", selectedIndex, simulationIds);
        if (newIndex >= 0 && newIndex < simulationIds.Length)
        {
            simulationIdProp.stringValue = simulationIds[newIndex];
        }
    }
}

public static class BootstrapOptionsAssetEditor
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
        var type = Type.GetType(componentType) ?? AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == componentType);

        if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            root.AddComponent(type);
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
#endif
