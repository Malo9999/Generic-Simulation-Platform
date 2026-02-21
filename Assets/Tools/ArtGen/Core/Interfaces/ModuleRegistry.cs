using System;
using System.Collections.Generic;
using UnityEditor;

public static class ModuleRegistry
{
    private static Dictionary<string, IArchetypeModule> archetypes;
    private static Dictionary<string, IEnvironmentModule> environments;
    private static List<IPackPreset> presets;

    private static void EnsureLoaded()
    {
        if (archetypes != null) return;

        archetypes = new Dictionary<string, IArchetypeModule>(StringComparer.Ordinal);
        environments = new Dictionary<string, IEnvironmentModule>(StringComparer.Ordinal);
        presets = new List<IPackPreset>();

        foreach (var type in TypeCache.GetTypesDerivedFrom<IArchetypeModule>())
        {
            if (type.IsAbstract) continue;
            if (Activator.CreateInstance(type) is IArchetypeModule module) archetypes[module.ArchetypeId] = module;
        }

        foreach (var type in TypeCache.GetTypesDerivedFrom<IEnvironmentModule>())
        {
            if (type.IsAbstract) continue;
            if (Activator.CreateInstance(type) is IEnvironmentModule module) environments[module.EnvironmentId] = module;
        }

        foreach (var type in TypeCache.GetTypesDerivedFrom<IPackPreset>())
        {
            if (type.IsAbstract) continue;
            if (Activator.CreateInstance(type) is IPackPreset preset) presets.Add(preset);
        }
    }

    public static IArchetypeModule GetArchetype(string id)
    {
        EnsureLoaded();
        return archetypes.TryGetValue(id, out var module) ? module : null;
    }

    public static IEnvironmentModule GetEnvironment(string id)
    {
        EnsureLoaded();
        return environments.TryGetValue(id, out var module) ? module : null;
    }

    public static List<IPackPreset> ListPresets()
    {
        EnsureLoaded();
        return new List<IPackPreset>(presets);
    }
}
