#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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
        public GspBootstrapKind ResolvedKind;
        public string AssetPath;
        public string Notes;
        public string ClassificationSource;
        public MonoScript Script;
        public Type Type;
        public readonly List<UsageRecord> SceneUsages = new();
        public readonly List<UsageRecord> PrefabUsages = new();

        public string UsageStatus
        {
            get
            {
                var hasScene = SceneUsages.Count > 0;
                var hasPrefab = PrefabUsages.Count > 0;
                if (hasScene && hasPrefab)
                {
                    return "Used In Scene + Prefab";
                }

                if (hasScene)
                {
                    return "Used In Scene";
                }

                if (hasPrefab)
                {
                    return "Used In Prefab";
                }

                return "Script Only";
            }
        }
    }

    private sealed class UsageRecord
    {
        public string AssetPath;
        public string GameObjectName;
        public UnityEngine.Object Asset;
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
        EditorGUILayout.LabelField("Kind", entry.ResolvedKind.ToString());
        EditorGUILayout.LabelField("Asset Path", entry.AssetPath);
        EditorGUILayout.LabelField("Notes", string.IsNullOrWhiteSpace(entry.Notes) ? "-" : entry.Notes);
        EditorGUILayout.LabelField("Heuristic Source", entry.ClassificationSource);
        EditorGUILayout.LabelField("Usage Status", entry.UsageStatus);

        DrawUsageSection("Scene Usage", entry.SceneUsages);
        DrawUsageSection("Prefab Usage", entry.PrefabUsages);

        if (entry.SceneUsages.Count == 0 && entry.PrefabUsages.Count == 0)
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

    private static void DrawUsageSection(string heading, List<UsageRecord> usages)
    {
        EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
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
                ResolvedKind = ResolveKind(attribute, path),
                AssetPath = path,
                Notes = attribute?.Notes ?? string.Empty,
                ClassificationSource = attribute != null ? "Attribute" : "Naming",
                Script = script,
                Type = type
            };

            allEntries.Add(entry);
        }

        allEntries.Sort((left, right) =>
        {
            var kindCompare = left.ResolvedKind.CompareTo(right.ResolvedKind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            return string.Compare(left.ClassName, right.ClassName, StringComparison.Ordinal);
        });

        ScanPrefabUsage();
        ScanSceneUsage();

        Repaint();
    }

    private void ScanPrefabUsage()
    {
        var entriesByType = allEntries.ToDictionary(entry => entry.Type);
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (var guid in prefabGuids)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                continue;
            }

            RegisterUsageForRoot(entriesByType, prefabRoot, prefabPath, isScene: false, prefabRoot);
        }
    }

    private void ScanSceneUsage()
    {
        var entriesByType = allEntries.ToDictionary(entry => entry.Type);
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (var guid in sceneGuids)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(guid);
            var existingScene = SceneManager.GetSceneByPath(scenePath);
            var alreadyLoaded = existingScene.isLoaded;
            var scene = existingScene;

            try
            {
                if (!alreadyLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    RegisterUsageForRoot(entriesByType, root, scenePath, isScene: true, AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BootstrapRegistry] Failed to scan scene usage for '{scenePath}'. Bootstrap registry data remains available. {ex}");
            }
            finally
            {
                if (!alreadyLoaded && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }

    private static void RegisterUsageForRoot(
        IReadOnlyDictionary<Type, BootstrapEntry> entriesByType,
        GameObject root,
        string assetPath,
        bool isScene,
        UnityEngine.Object asset)
    {
        var monoBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var monoBehaviour in monoBehaviours)
        {
            if (monoBehaviour == null)
            {
                continue;
            }

            var type = monoBehaviour.GetType();
            if (!entriesByType.TryGetValue(type, out var entry))
            {
                continue;
            }

            var usage = new UsageRecord
            {
                AssetPath = assetPath,
                GameObjectName = monoBehaviour.gameObject.name,
                Asset = asset
            };

            if (isScene)
            {
                entry.SceneUsages.Add(usage);
            }
            else
            {
                entry.PrefabUsages.Add(usage);
            }
        }
    }

    private bool ShouldShow(BootstrapEntry entry)
    {
        if (filter == FilterKind.All)
        {
            return true;
        }

        return entry.ResolvedKind == GetFilterKind(filter);
    }

    private static GspBootstrapKind ResolveKind(GspBootstrapAttribute attribute, string assetPath)
    {
        if (attribute != null)
        {
            return attribute.Kind;
        }

        return GspBootstrapConventions.GuessKindFromPath(assetPath);
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
