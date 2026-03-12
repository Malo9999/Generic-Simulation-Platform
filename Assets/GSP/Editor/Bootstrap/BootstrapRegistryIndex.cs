#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class BootstrapRegistryIndex
{
    private const string UnresolvedSceneObjectName = "(resolved without scene open)";
    private static readonly Dictionary<string, List<UsageRecord>> SceneUsageByScriptGuidCache = new(StringComparer.Ordinal);

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
        var allEntries = BuildMetadataIndex();

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

    internal static List<Entry> BuildMetadataIndex()
    {
        InvalidateSceneUsageCache();
        var allEntries = DiscoverEntries();
        MarkSceneUsageUnresolved(allEntries);
        MarkPrefabUsageUnresolved(allEntries);
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
            ResolveSceneUsageForEntry(entry);
        }
    }

    internal static void ResolveSceneUsageForEntry(Entry entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.SceneUsages.Clear();
        foreach (var usage in GetSceneUsageForEntry(entry))
        {
            entry.SceneUsages.Add(usage);
        }

        entry.SceneUsageResolutionState = UsageResolutionState.Resolved;
    }

    internal static bool TryGetPrimaryScenePath(Entry entry, out string path)
    {
        path = null;
        if (entry == null)
        {
            return false;
        }

        if (entry.SceneUsageResolutionState != UsageResolutionState.Resolved)
        {
            ResolveSceneUsageForEntry(entry);
        }

        var primary = entry.PrimarySceneUsage;
        if (primary == null || string.IsNullOrWhiteSpace(primary.AssetPath))
        {
            return false;
        }

        path = primary.AssetPath;
        return true;
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
        foreach (var entry in entries)
        {
            ResolveSceneUsageForEntry(entry);
        }
    }

    private static List<UsageRecord> GetSceneUsageForEntry(Entry entry)
    {
        var scriptGuid = AssetDatabase.AssetPathToGUID(entry.AssetPath);
        if (string.IsNullOrWhiteSpace(scriptGuid))
        {
            return new List<UsageRecord>();
        }

        if (!SceneUsageByScriptGuidCache.TryGetValue(scriptGuid, out var cachedUsages))
        {
            cachedUsages = BuildSceneUsageRecordsForScript(scriptGuid);
            SceneUsageByScriptGuidCache[scriptGuid] = cachedUsages;
        }

        return cachedUsages;
    }

    private static List<UsageRecord> BuildSceneUsageRecordsForScript(string scriptGuid)
    {
        var usages = new List<UsageRecord>();
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (var sceneGuid in sceneGuids)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
            {
                continue;
            }

            try
            {
                var sceneText = File.ReadAllText(scenePath);
                if (!sceneText.Contains($"guid: {scriptGuid}", StringComparison.Ordinal))
                {
                    continue;
                }

                usages.Add(new UsageRecord
                {
                    AssetPath = scenePath,
                    GameObjectName = UnresolvedSceneObjectName,
                    Asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BootstrapRegistry] Failed to inspect scene '{scenePath}' while resolving usage. {ex.Message}");
            }
        }

        usages.Sort((left, right) =>
        {
            var pathCompare = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            return string.Compare(left.GameObjectName, right.GameObjectName, StringComparison.Ordinal);
        });

        return usages;
    }

    private static void InvalidateSceneUsageCache()
    {
        SceneUsageByScriptGuidCache.Clear();
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
