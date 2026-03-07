using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/Recipe Preset")]
public class WorldRecipePresetSO : ScriptableObject
{
    public string recipeId;
    public string presetName;
    public bool useCurrentGrid = true;
    public WorldGridSpec gridDefaults;
    [TextArea(4, 20)]
    public string settingsJson;
    [TextArea(2, 8)]
    public string noiseJson;
    public string settingsType;
    public string createdAtUtc;

    public static WorldRecipePresetSO Create(string recipe, string name, WorldGridSpec grid, bool preserveCurrentGrid, string settingsSnapshotJson, string noiseSnapshotJson, string settingsTypeName)
    {
        var preset = CreateInstance<WorldRecipePresetSO>();
        preset.recipeId = recipe;
        preset.presetName = name;
        preset.useCurrentGrid = preserveCurrentGrid;
        preset.gridDefaults = grid;
        preset.settingsJson = settingsSnapshotJson;
        preset.noiseJson = noiseSnapshotJson;
        preset.settingsType = settingsTypeName;
        preset.createdAtUtc = DateTime.UtcNow.ToString("o");
        return preset;

    }
}
