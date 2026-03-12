#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class BootstrapRegistryWindow : EditorWindow
{
    private enum FilterKind
    {
        All,
        Platform,
        Simulation,
        Preview,
        Tool,
        Unknown
    }

    private static readonly GUIContent[] FilterTabs =
    {
        new("All"),
        new("Platform"),
        new("Simulation"),
        new("Preview"),
        new("Tool"),
        new("Unknown")
    };

    private readonly List<BootstrapRegistryIndex.Entry> allEntries = new();
    private Vector2 scroll;
    [SerializeField] private FilterKind filter = FilterKind.All;

    [MenuItem("Tools/GSP/Bootstrap Registry")]
    public static void Open()
    {
        GetWindow<BootstrapRegistryWindow>("Bootstrap Registry");
    }

    private void OnEnable()
    {
        RefreshEntries();
    }

    private void OnGUI()
    {
        DrawToolbar();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        var visible = allEntries.Where(ShouldShow).ToList();
        if (visible.Count == 0)
        {
            EditorGUILayout.HelpBox("No bootstrap candidates found for the selected filter.", MessageType.Info);
        }
        else
        {
            foreach (var entry in visible)
            {
                DrawEntry(entry);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var selected = GUILayout.Toolbar((int)filter, FilterTabs, EditorStyles.toolbarButton, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        filter = (FilterKind)selected;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            RefreshEntries();
        }

        if (GUILayout.Button("Resolve Scene Usage", EditorStyles.toolbarButton, GUILayout.Width(135f)))
        {
            ResolveSceneUsage();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawEntry(BootstrapRegistryIndex.Entry entry)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(entry.ClassName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Kind", entry.ResolvedKind.ToString());
        EditorGUILayout.LabelField("Asset Path", entry.AssetPath);
        EditorGUILayout.LabelField("Notes", string.IsNullOrWhiteSpace(entry.Notes) ? "-" : entry.Notes);
        EditorGUILayout.LabelField("Heuristic Source", entry.ClassificationSource);
        EditorGUILayout.LabelField("Usage Status", entry.UsageStatus);

        DrawUsageSection("Scene Usage", entry.SceneUsages, entry.SceneUsageResolutionState);
        DrawUsageSection("Prefab Usage", entry.PrefabUsages, entry.PrefabUsageResolutionState);

        if (entry.SceneUsageResolutionState != BootstrapRegistryIndex.UsageResolutionState.Resolved)
        {
            EditorGUILayout.HelpBox("Scene usage unresolved. Use Resolve Scene Usage to populate scene references.", MessageType.None);
        }
        else if (entry.SceneUsages.Count == 0 && entry.PrefabUsages.Count == 0)
        {
            EditorGUILayout.HelpBox("No scene/prefab references found", MessageType.None);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping", GUILayout.Width(75f)) && entry.Script != null)
        {
            EditorGUIUtility.PingObject(entry.Script);
        }

        if (GUILayout.Button("Open Script", GUILayout.Width(100f)) && entry.Script != null)
        {
            AssetDatabase.OpenAsset(entry.Script);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private static void DrawUsageSection(
        string heading,
        List<BootstrapRegistryIndex.UsageRecord> usages,
        BootstrapRegistryIndex.UsageResolutionState resolutionState)
    {
        EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
        if (resolutionState != BootstrapRegistryIndex.UsageResolutionState.Resolved)
        {
            EditorGUILayout.LabelField("-", "Not resolved");
            return;
        }

        if (usages.Count == 0)
        {
            EditorGUILayout.LabelField("-", "None");
            return;
        }

        foreach (var usage in usages)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{usage.AssetPath} :: {usage.GameObjectName}");
            if (GUILayout.Button("Ping", GUILayout.Width(75f)) && usage.Asset != null)
            {
                EditorGUIUtility.PingObject(usage.Asset);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void RefreshEntries()
    {
        allEntries.Clear();
        allEntries.AddRange(BootstrapRegistryIndex.Build(BootstrapRegistryIndex.BuildOptions.WithPrefabUsage()));
        Repaint();
    }

    private void ResolveSceneUsage()
    {
        BootstrapRegistryIndex.ResolveSceneUsage(allEntries);
        Repaint();
    }

    private bool ShouldShow(BootstrapRegistryIndex.Entry entry)
    {
        if (filter == FilterKind.All)
        {
            return true;
        }

        return entry.ResolvedKind == GetFilterKind(filter);
    }

    private static GspBootstrapKind GetFilterKind(FilterKind selectedFilter)
    {
        return selectedFilter switch
        {
            FilterKind.Platform => GspBootstrapKind.Platform,
            FilterKind.Simulation => GspBootstrapKind.Simulation,
            FilterKind.Preview => GspBootstrapKind.Preview,
            FilterKind.Tool => GspBootstrapKind.Tool,
            FilterKind.Unknown => GspBootstrapKind.Unknown,
            _ => GspBootstrapKind.Unknown
        };
    }
}
#endif
