#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AntBlueprintFactory
{
    private const string MenuPath = "GSP/Art/Create Default Species Blueprints";

    [MenuItem(MenuPath)]
    public static void CreateFromMenu()
    {
        var abs = EditorUtility.OpenFolderPanel("Choose Pack Folder", Application.dataPath, string.Empty);
        if (string.IsNullOrWhiteSpace(abs))
        {
            return;
        }

        var rel = ToAssetPath(abs);
        if (string.IsNullOrEmpty(rel))
        {
            Debug.LogError("Selected folder must be inside this project's Assets folder.");
            return;
        }

        var set = EnsureDefaultSpeciesBlueprints(rel, true);
        EditorGUIUtility.PingObject(set);
    }

    public static AntSpeciesBlueprintSet EnsureDefaultSpeciesBlueprints(string outputFolder, bool overwriteAssets)
    {
        var packName = Path.GetFileName(outputFolder.TrimEnd('/'));
        var speciesId = MakeSafeId(packName);
        var speciesFolder = $"{outputFolder}/Blueprints/Species/{speciesId}";
        ImportSettingsUtil.EnsureFolder($"{outputFolder}/Blueprints");
        ImportSettingsUtil.EnsureFolder($"{outputFolder}/Blueprints/Species");
        ImportSettingsUtil.EnsureFolder(speciesFolder);

        var worker = CreateOrUpdateBlueprint($"{speciesFolder}/worker_base.asset", overwriteAssets, false, 0, 0, false);
        var soldier = CreateOrUpdateBlueprint($"{speciesFolder}/soldier_base.asset", overwriteAssets, true, 0, 0, false);

        var frames = new List<(string role, string clip, int index, PixelBlueprint2D blueprint)>();
        frames.Add(("worker", "idle", 0, CreateOrUpdateBlueprint($"{speciesFolder}/worker_idle_0.asset", overwriteAssets, false, 0, 0, false)));
        frames.Add(("worker", "idle", 1, CreateOrUpdateBlueprint($"{speciesFolder}/worker_idle_1.asset", overwriteAssets, false, 0, 0, true)));

        frames.Add(("worker", "walk", 0, CreateOrUpdateBlueprint($"{speciesFolder}/worker_walk_0.asset", overwriteAssets, false, -1, 0, false)));
        frames.Add(("worker", "walk", 1, CreateOrUpdateBlueprint($"{speciesFolder}/worker_walk_1.asset", overwriteAssets, false, 1, 0, false)));
        frames.Add(("worker", "walk", 2, CreateOrUpdateBlueprint($"{speciesFolder}/worker_walk_2.asset", overwriteAssets, false, 0, -1, false)));
        frames.Add(("worker", "walk", 3, CreateOrUpdateBlueprint($"{speciesFolder}/worker_walk_3.asset", overwriteAssets, false, 0, 1, false)));

        frames.Add(("worker", "run", 0, CreateOrUpdateBlueprint($"{speciesFolder}/worker_run_0.asset", overwriteAssets, false, -2, 0, false)));
        frames.Add(("worker", "run", 1, CreateOrUpdateBlueprint($"{speciesFolder}/worker_run_1.asset", overwriteAssets, false, 2, 0, false)));
        frames.Add(("worker", "run", 2, CreateOrUpdateBlueprint($"{speciesFolder}/worker_run_2.asset", overwriteAssets, false, 0, -1, true)));
        frames.Add(("worker", "run", 3, CreateOrUpdateBlueprint($"{speciesFolder}/worker_run_3.asset", overwriteAssets, false, 0, 1, true)));

        var oneFrameClips = new[] { "work", "eat", "fight", "defend", "hurt", "death", "reproduce" };
        foreach (var clipId in oneFrameClips)
        {
            frames.Add(("worker", clipId, 0, CreateOrUpdateBlueprint($"{speciesFolder}/worker_{clipId}_0.asset", overwriteAssets, false, 0, 0, clipId == "fight")));
        }

        var setPath = $"{speciesFolder}/AntSpeciesBlueprintSet.asset";
        var set = AssetDatabase.LoadAssetAtPath<AntSpeciesBlueprintSet>(setPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<AntSpeciesBlueprintSet>();
            AssetDatabase.CreateAsset(set, setPath);
        }

        set.speciesId = speciesId;
        set.displayName = ObjectNames.NicifyVariableName(speciesId);
        set.roles = new List<AntSpeciesBlueprintSet.RoleBlueprints>
        {
            new() { roleId = "worker", basePose = worker },
            new() { roleId = "soldier", basePose = soldier }
        };

        set.clips = BuildClips(frames);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return set;
    }

    private static List<AntSpeciesBlueprintSet.ClipBlueprint> BuildClips(List<(string role, string clip, int index, PixelBlueprint2D blueprint)> frames)
    {
        var fpsByClip = new Dictionary<string, int>
        {
            ["idle"] = 6,
            ["walk"] = 8,
            ["run"] = 12,
            ["work"] = 4,
            ["eat"] = 4,
            ["fight"] = 6,
            ["defend"] = 4,
            ["hurt"] = 4,
            ["death"] = 3,
            ["reproduce"] = 3
        };

        var clips = new List<AntSpeciesBlueprintSet.ClipBlueprint>();
        foreach (var group in frames.GroupBy(f => new { f.role, f.clip }))
        {
            var clip = new AntSpeciesBlueprintSet.ClipBlueprint
            {
                roleId = group.Key.role,
                clipId = group.Key.clip,
                fps = fpsByClip.TryGetValue(group.Key.clip, out var fps) ? fps : 8,
                frames = group.OrderBy(x => x.index).Select(x => new AntSpeciesBlueprintSet.FrameBlueprint { blueprint = x.blueprint }).ToList()
            };
            clips.Add(clip);
        }

        return clips;
    }

    private static PixelBlueprint2D CreateOrUpdateBlueprint(string assetPath, bool overwrite, bool soldier, int legOffsetX, int legOffsetY, bool antennaWide)
    {
        var blueprint = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(assetPath);
        if (blueprint == null)
        {
            blueprint = ScriptableObject.CreateInstance<PixelBlueprint2D>();
            AssetDatabase.CreateAsset(blueprint, assetPath);
            overwrite = true;
        }

        if (overwrite)
        {
            blueprint.width = 32;
            blueprint.height = 32;
            blueprint.layers = new List<PixelBlueprint2D.Layer>();
            blueprint.EnsureLayer("body");
            blueprint.EnsureLayer("stripe");
            blueprint.Clear("body");
            blueprint.Clear("stripe");
            DrawAntBase(blueprint, soldier, legOffsetX, legOffsetY, antennaWide);
            EditorUtility.SetDirty(blueprint);
        }

        return blueprint;
    }

    private static void DrawAntBase(PixelBlueprint2D bp, bool soldier, int legOffsetX, int legOffsetY, bool antennaWide)
    {
        FillEllipse(bp, "body", 10, 16, 7, 5);
        FillEllipse(bp, "body", 17, 16, 5, 4);
        FillEllipse(bp, "body", 24, 15, soldier ? 6 : 5, soldier ? 5 : 4);

        for (var y = 15; y <= 17; y++)
        {
            bp.Set("body", 13, y, 0);
            bp.Set("body", 14, y, 1);
        }

        DrawLine(bp, "body", 18, 13, 9 + legOffsetX, 9 + legOffsetY);
        DrawLine(bp, "body", 18, 14, 8 + legOffsetX, 15 + legOffsetY);
        DrawLine(bp, "body", 18, 18, 9 + legOffsetX, 22 + legOffsetY);

        DrawLine(bp, "body", 19, 13, 26 + legOffsetX, 9 + legOffsetY);
        DrawLine(bp, "body", 19, 16, 27 + legOffsetX, 16 + legOffsetY);
        DrawLine(bp, "body", 19, 19, 26 + legOffsetX, 23 + legOffsetY);

        DrawLine(bp, "body", 27, 13, antennaWide ? 31 : 30, 9);
        DrawLine(bp, "body", 27, 17, antennaWide ? 31 : 30, 21);

        if (soldier)
        {
            DrawLine(bp, "body", 29, 14, 31, 12);
            DrawLine(bp, "body", 29, 16, 31, 18);
        }

        for (var y = 15; y <= 17; y++)
        {
            for (var x = 4; x <= 9; x++)
            {
                if (Mathf.Abs(y - 16) <= 1 && x <= 8)
                {
                    bp.Set("stripe", x, y, 1);
                }
            }
        }
    }

    private static void FillEllipse(PixelBlueprint2D bp, string layer, int cx, int cy, int rx, int ry)
    {
        for (var y = cy - ry; y <= cy + ry; y++)
        {
            for (var x = cx - rx; x <= cx + rx; x++)
            {
                var dx = (x - cx) / (float)Mathf.Max(1, rx);
                var dy = (y - cy) / (float)Mathf.Max(1, ry);
                if ((dx * dx) + (dy * dy) <= 1f)
                {
                    bp.Set(layer, x, y, 1);
                }
            }
        }
    }

    private static void DrawLine(PixelBlueprint2D bp, string layer, int x0, int y0, int x1, int y1)
    {
        var dx = Mathf.Abs(x1 - x0);
        var dy = Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        while (true)
        {
            bp.Set(layer, x0, y0, 1);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static string ToAssetPath(string absolutePath)
    {
        var normalized = absolutePath.Replace('\\', '/');
        var assetsPrefix = Application.dataPath.Replace('\\', '/');
        if (!normalized.StartsWith(assetsPrefix))
        {
            return null;
        }

        return $"Assets{normalized.Substring(assetsPrefix.Length)}";
    }

    private static string MakeSafeId(string raw)
    {
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
#endif
