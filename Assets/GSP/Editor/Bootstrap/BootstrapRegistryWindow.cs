#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    private sealed class BootstrapEntry
    {
        public string ClassName;
        public GspBootstrapKind Kind;
        public string AssetPath;
        public string Notes;
        public string Source;
        public MonoScript Script;
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

    private readonly List<BootstrapEntry> allEntries = new();
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

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEntry(BootstrapEntry entry)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(entry.ClassName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Kind", entry.Kind.ToString());
        EditorGUILayout.LabelField("Asset Path", entry.AssetPath);
        EditorGUILayout.LabelField("Notes", string.IsNullOrWhiteSpace(entry.Notes) ? "-" : entry.Notes);
        EditorGUILayout.LabelField("Heuristic Source", entry.Source);

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

    private void RefreshEntries()
    {
        allEntries.Clear();

        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
            {
                continue;
            }

            var type = script.GetClass();
            if (type == null)
            {
                continue;
            }

            var attribute = type.GetCustomAttribute<GspBootstrapAttribute>(false);
            var hasBootstrapName = GspBootstrapConventions.MatchesBootstrapNaming(type.Name);
            if (attribute == null && !hasBootstrapName)
            {
                continue;
            }

            var entry = new BootstrapEntry
            {
                ClassName = type.Name,
                Kind = attribute?.Kind ?? GspBootstrapConventions.GuessKindFromPath(path),
                AssetPath = path,
                Notes = attribute?.Notes ?? string.Empty,
                Source = attribute != null ? "Attribute" : "Naming",
                Script = script
            };

            allEntries.Add(entry);
        }

        allEntries.Sort((left, right) =>
        {
            var kindCompare = left.Kind.CompareTo(right.Kind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            return string.Compare(left.ClassName, right.ClassName, StringComparison.Ordinal);
        });

        Repaint();
    }

    private bool ShouldShow(BootstrapEntry entry)
    {
        if (filter == FilterKind.All)
        {
            return true;
        }

        return entry.Kind == (GspBootstrapKind)filter;
    }
}
#endif
