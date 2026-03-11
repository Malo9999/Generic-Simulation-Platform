#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SimulationLauncherWindow : EditorWindow
{
    private readonly List<BootstrapRegistryIndex.Entry> simulationEntries = new();
    private Vector2 scroll;
    private string search = string.Empty;
    [SerializeField] private bool autoEnterPlayMode;

    [MenuItem("Tools/GSP/Simulation Launcher")]
    public static void Open()
    {
        GetWindow<SimulationLauncherWindow>("Simulation Launcher");
    }

    private void OnEnable()
    {
        RefreshEntries();
    }

    private void OnGUI()
    {
        DrawToolbar();

        var visible = simulationEntries
            .Where(entry => string.IsNullOrWhiteSpace(search) || entry.ClassName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(entry => entry.ClassName, StringComparer.Ordinal)
            .ToList();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (simulationEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("No simulation bootstraps found.", MessageType.Info);
        }
        else if (visible.Count == 0)
        {
            EditorGUILayout.HelpBox("No simulation bootstraps match the current search.", MessageType.Info);
        }
        else
        {
            foreach (var entry in visible)
            {
                DrawSimulationRow(entry);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Search", GUILayout.Width(45f));
        search = GUILayout.TextField(search, EditorStyles.toolbarTextField);
        autoEnterPlayMode = GUILayout.Toggle(autoEnterPlayMode, "Auto-Play on Run", EditorStyles.toolbarButton, GUILayout.Width(120f));

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            RefreshEntries();
        }

        if (GUILayout.Button("Open Bootstrap Registry", EditorStyles.toolbarButton, GUILayout.Width(160f)))
        {
            BootstrapRegistryWindow.Open();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static string BuildUsageSummary(BootstrapRegistryIndex.Entry entry)
    {
        return $"{entry.SceneUsages.Count} scene(s), {entry.PrefabUsages.Count} prefab(s), source: {entry.ClassificationSource}";
    }

    private void DrawSimulationRow(BootstrapRegistryIndex.Entry entry)
    {
        var primaryScene = entry.PrimarySceneUsage;
        var hasMultipleScenes = entry.SceneUsages.Count > 1;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(entry.ClassName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Primary Scene", primaryScene?.AssetPath ?? "no scene found");
        if (hasMultipleScenes)
        {
            EditorGUILayout.LabelField("Scenes", $"{entry.SceneUsages.Count} associated (using deterministic first-by-path rule)");
        }

        EditorGUILayout.LabelField("Script Asset", entry.AssetPath);
        EditorGUILayout.LabelField("Usage", BuildUsageSummary(entry));

        if (primaryScene == null)
        {
            EditorGUILayout.HelpBox("No scene usage found for this simulation.", MessageType.None);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Scene", GUILayout.Width(95f)))
        {
            OpenPrimaryScene(entry);
        }

        if (GUILayout.Button("Ping Script", GUILayout.Width(95f)) && entry.Script != null)
        {
            EditorGUIUtility.PingObject(entry.Script);
        }

        if (GUILayout.Button("Select Bootstrap In Scene", GUILayout.Width(170f)))
        {
            SelectBootstrapInScene(entry, openSceneIfNeeded: true);
        }

        if (GUILayout.Button(autoEnterPlayMode ? "Run (Open + Play)" : "Run (Open)", GUILayout.Width(115f)))
        {
            RunSimulation(entry);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private static bool OpenPrimaryScene(BootstrapRegistryIndex.Entry entry)
    {
        var primaryScene = entry.PrimarySceneUsage;
        if (primaryScene == null || string.IsNullOrWhiteSpace(primaryScene.AssetPath))
        {
            EditorUtility.DisplayDialog("Simulation Launcher", $"No scene usage found for {entry.ClassName}.", "OK");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(primaryScene.AssetPath) == null)
        {
            EditorUtility.DisplayDialog("Simulation Launcher", $"Scene not found: {primaryScene.AssetPath}", "OK");
            return false;
        }

        EditorSceneManager.OpenScene(primaryScene.AssetPath, OpenSceneMode.Single);
        return true;
    }

    private static MonoBehaviour FindFirstBootstrapInOpenScene(Type bootstrapType)
    {
        if (bootstrapType == null)
        {
            return null;
        }

        return Resources.FindObjectsOfTypeAll(bootstrapType)
            .OfType<MonoBehaviour>()
            .Where(candidate => candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded)
            .OrderBy(candidate => candidate.gameObject.scene.path, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.gameObject.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static void SelectBootstrapInScene(BootstrapRegistryIndex.Entry entry, bool openSceneIfNeeded)
    {
        if (openSceneIfNeeded && !OpenPrimaryScene(entry))
        {
            return;
        }

        var found = FindFirstBootstrapInOpenScene(entry.Type);
        if (found == null)
        {
            EditorUtility.DisplayDialog(
                "Simulation Launcher",
                $"Opened scene but could not find an active {entry.ClassName} instance.",
                "OK");
            return;
        }

        Selection.activeGameObject = found.gameObject;
        EditorGUIUtility.PingObject(found.gameObject);
    }

    private void RunSimulation(BootstrapRegistryIndex.Entry entry)
    {
        if (!OpenPrimaryScene(entry))
        {
            return;
        }

        SelectBootstrapInScene(entry, openSceneIfNeeded: false);

        if (autoEnterPlayMode && !EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
        }
    }

    private void RefreshEntries()
    {
        simulationEntries.Clear();
        simulationEntries.AddRange(BootstrapRegistryIndex.Build()
            .Where(entry => entry.ResolvedKind == GspBootstrapKind.Simulation)
            .OrderBy(entry => entry.ClassName, StringComparer.Ordinal));
        Repaint();
    }
}
#endif
