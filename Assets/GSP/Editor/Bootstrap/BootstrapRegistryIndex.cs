#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class BootstrapRegistryIndex
{
    internal enum UsageResolutionState
    {
        NotResolved,
        Resolved
    }

    internal sealed class BuildOptions
    {
        public bool IncludePrefabUsage = true;
        public bool IncludeSceneUsage;

        public static BuildOptions MetadataOnly()
        {
            return new BuildOptions
            {
                IncludePrefabUsage = false,
                IncludeSceneUsage = false
            };
        }

        public static BuildOptions WithPrefabUsage()
        {
            return new BuildOptions
            {
                IncludePrefabUsage = true,
                IncludeSceneUsage = false
            };
        }

        public static BuildOptions FullUsage()
        {
            return new BuildOptions
            {
                IncludePrefabUsage = true,
                IncludeSceneUsage = true
            };
        }
    }

    internal sealed class Entry
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
        public UsageResolutionState SceneUsageResolutionState = UsageResolutionState.NotResolved;
        public UsageResolutionState PrefabUsageResolutionState = UsageResolutionState.NotResolved;

        public string UsageStatus
        {
            get
            {
                var hasScene = SceneUsages.Count > 0;
                var hasPrefab = PrefabUsages.Count > 0;

                if (SceneUsageResolutionState == UsageResolutionState.NotResolved
                    || PrefabUsageResolutionState == UsageResolutionState.NotResolved)
                {
                    return "Usage Not Fully Resolved";
                }

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

        // Deterministic primary scene rule: first usage after stable path/name sort.
        public UsageRecord PrimarySceneUsage => SceneUsages
            .OrderBy(usage => usage.AssetPath, StringComparer.Ordinal)
            .ThenBy(usage => usage.GameObjectName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    internal sealed class UsageRecord
    {
        public string AssetPath;
        public string GameObjectName;
        public UnityEngine.Object Asset;
    }

    internal static List<Entry> Build(BuildOptions options = null)
    {
        options ??= BuildOptions.WithPrefabUsage();
        var allEntries = DiscoverEntries();

        if (options.IncludePrefabUsage)
        {
            ScanPrefabUsage(allEntries);
        }
        else
        {
            MarkPrefabUsageUnresolved(allEntries);
        }

        if (options.IncludeSceneUsage)
        {
            ScanSceneUsage(allEntries);
        }
        else
        {
            MarkSceneUsageUnresolved(allEntries);
        }

        return allEntries;
    }

    internal static void ResolveSceneUsage(List<Entry> entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            entry.SceneUsages.Clear();
        }

        ScanSceneUsage(entries);
    }

    private static List<Entry> DiscoverEntries()
    {
        var allEntries = new List<Entry>();
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

            allEntries.Add(new Entry
            {
                ClassName = type.Name,
                ResolvedKind = ResolveKind(attribute, path),
                AssetPath = path,
                Notes = attribute?.Notes ?? string.Empty,
                ClassificationSource = attribute != null ? "Attribute" : "Naming",
                Script = script,
                Type = type
            });
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

        return allEntries;
    }

    private static void ScanPrefabUsage(List<Entry> entries)
    {
        foreach (var entry in entries)
        {
            entry.PrefabUsages.Clear();
        }

        var entriesByType = entries.ToDictionary(entry => entry.Type);
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

        foreach (var entry in entries)
        {
            entry.PrefabUsageResolutionState = UsageResolutionState.Resolved;
        }
    }

    private static void ScanSceneUsage(List<Entry> entries)
    {
        var entriesByType = entries.ToDictionary(entry => entry.Type);
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        var previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
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
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        foreach (var entry in entries)
        {
            entry.SceneUsageResolutionState = UsageResolutionState.Resolved;
        }
    }

    private static void MarkSceneUsageUnresolved(List<Entry> entries)
    {
        foreach (var entry in entries)
        {
            entry.SceneUsages.Clear();
            entry.SceneUsageResolutionState = UsageResolutionState.NotResolved;
        }
    }

    private static void MarkPrefabUsageUnresolved(List<Entry> entries)
    {
        foreach (var entry in entries)
        {
            entry.PrefabUsages.Clear();
            entry.PrefabUsageResolutionState = UsageResolutionState.NotResolved;
        }
    }

    private static void RegisterUsageForRoot(
        IReadOnlyDictionary<Type, Entry> entriesByType,
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

            if (!entriesByType.TryGetValue(monoBehaviour.GetType(), out var entry))
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

    private static GspBootstrapKind ResolveKind(GspBootstrapAttribute attribute, string assetPath)
    {
        if (attribute != null)
        {
            return attribute.Kind;
        }

        return GspBootstrapConventions.GuessKindFromPath(assetPath);
    }
}
#endif
