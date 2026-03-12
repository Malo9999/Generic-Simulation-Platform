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

        if (GUILayout.Button("Refresh Scene Usage", EditorStyles.toolbarButton, GUILayout.Width(140f)))
        {
            ResolveSceneUsageForVisibleEntries();
        }

        if (GUILayout.Button("Open Bootstrap Registry", EditorStyles.toolbarButton, GUILayout.Width(160f)))
        {
            BootstrapRegistryWindow.Open();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static string BuildUsageSummary(BootstrapRegistryIndex.Entry entry)
    {
        var sceneSummary = entry.SceneUsageResolutionState == BootstrapRegistryIndex.UsageResolutionState.Resolved
            ? $"{entry.SceneUsages.Count} scene(s)"
            : "scene usage not resolved";
        var prefabSummary = entry.PrefabUsageResolutionState == BootstrapRegistryIndex.UsageResolutionState.Resolved
            ? $"{entry.PrefabUsages.Count} prefab(s)"
            : "prefab usage not resolved";

        return $"{sceneSummary}, {prefabSummary}, source: {entry.ClassificationSource}";
    }

    private void DrawSimulationRow(BootstrapRegistryIndex.Entry entry)
    {
        var sceneUsageResolved = entry.SceneUsageResolutionState == BootstrapRegistryIndex.UsageResolutionState.Resolved;
        var primaryScene = sceneUsageResolved ? entry.PrimarySceneUsage : null;
        var hasMultipleScenes = sceneUsageResolved && entry.SceneUsages.Count > 1;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(entry.ClassName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Primary Scene", primaryScene?.AssetPath ?? (sceneUsageResolved ? "no scene found" : "scene usage not resolved"));
        if (hasMultipleScenes)
        {
            EditorGUILayout.LabelField("Scenes", $"{entry.SceneUsages.Count} associated (using deterministic first-by-path rule)");
        }

        EditorGUILayout.LabelField("Script Asset", entry.AssetPath);
        EditorGUILayout.LabelField("Usage", BuildUsageSummary(entry));

        if (!sceneUsageResolved)
        {
            EditorGUILayout.HelpBox("Scene usage not resolved. Click Refresh Scene Usage.", MessageType.None);
        }
        else if (primaryScene == null)
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

    private bool OpenPrimaryScene(BootstrapRegistryIndex.Entry entry)
    {
        EnsureSceneUsageResolved(entry);
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

    private void SelectBootstrapInScene(BootstrapRegistryIndex.Entry entry, bool openSceneIfNeeded)
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
        simulationEntries.AddRange(BootstrapRegistryIndex.Build(BootstrapRegistryIndex.BuildOptions.WithPrefabUsage())
            .Where(entry => entry.ResolvedKind == GspBootstrapKind.Simulation)
            .OrderBy(entry => entry.ClassName, StringComparer.Ordinal));
        Repaint();
    }

    private void ResolveSceneUsageForVisibleEntries()
    {
        BootstrapRegistryIndex.ResolveSceneUsage(simulationEntries);
        Repaint();
    }

    private static void EnsureSceneUsageResolved(BootstrapRegistryIndex.Entry entry)
    {
        if (entry.SceneUsageResolutionState == BootstrapRegistryIndex.UsageResolutionState.Resolved)
        {
            return;
        }

        BootstrapRegistryIndex.ResolveSceneUsage(new List<BootstrapRegistryIndex.Entry> { entry });
    }
}
#endif
