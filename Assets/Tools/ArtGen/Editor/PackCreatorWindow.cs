using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class PackCreatorWindow : EditorWindow
{
    private static readonly string[] Tabs = { "Agents", "World", "UI" };
    private static readonly Regex StateGuessRegex = new("idle|walk|run|fight|drive|turn|work|attack", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, PackCreatorSimConfig> SimConfigs = new(StringComparer.Ordinal)
    {
        ["AntColonies"] = new("AntColonies", "ant", new[] { "idle", "walk", "run", "fight" }),
        ["MarbleRace"] = new("MarbleRace", "marble", new[] { "idle", "roll", "run" }),
        ["FantasySport"] = new("FantasySport", "player", new[] { "idle", "run", "attack" }),
        ["RaceCar"] = new("RaceCar", "car", new[] { "idle", "drive", "turn" })
    };

    private readonly Dictionary<PackCreatorAssetGroup, PackCreatorBuildStyle> buildStyles = new()
    {
        [PackCreatorAssetGroup.Agents] = PackCreatorBuildStyle.BasicShapes,
        [PackCreatorAssetGroup.World] = PackCreatorBuildStyle.BasicShapes,
        [PackCreatorAssetGroup.UI] = PackCreatorBuildStyle.BasicShapes
    };

    private readonly Dictionary<PackCreatorAssetGroup, string> jsonInputs = new()
    {
        [PackCreatorAssetGroup.Agents] = string.Empty,
        [PackCreatorAssetGroup.World] = string.Empty,
        [PackCreatorAssetGroup.UI] = string.Empty
    };

    private readonly Dictionary<PackCreatorAssetGroup, Vector2> groupScroll = new();
    private readonly List<PackCreatorSheetImportRow> stagedRows = new();

    private int selectedSimIndex;
    private string packId = "pack.v1";
    private int seed = 123;
    private int selectedTab;
    private Vector2 rowsScroll;
    private string statusLine = "Ready.";
    private bool overwriteBuild = true;

    [MenuItem("Tools/GSP/Pack Creator")]
    public static void Open()
    {
        var window = GetWindow<PackCreatorWindow>("Pack Creator");
        window.minSize = new Vector2(780f, 540f);
    }

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space();

        selectedTab = GUILayout.Toolbar(selectedTab, Tabs);
        var group = (PackCreatorAssetGroup)selectedTab;
        groupScroll[group] = EditorGUILayout.BeginScrollView(groupScroll.TryGetValue(group, out var existing) ? existing : Vector2.zero);
        DrawGroupPanel(group);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(statusLine, MessageType.Info);
    }

    private void DrawHeader()
    {
        var simulationIds = SimConfigs.Keys.ToArray();
        selectedSimIndex = EditorGUILayout.Popup("Simulation", selectedSimIndex, simulationIds);
        packId = EditorGUILayout.TextField("Pack Id", packId);
        seed = EditorGUILayout.IntField("Seed", seed);

        EditorGUILayout.LabelField("Output", BuildPackRoot(), EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Pack Folder", GUILayout.Width(160f)))
            {
                EnsurePackFolders();
                EditorUtility.RevealInFinder(Path.GetFullPath(BuildPackRoot()));
            }

            if (GUILayout.Button("Create/Update Recipe", GUILayout.Width(180f)))
            {
                CreateOrUpdateRecipe();
            }

            overwriteBuild = EditorGUILayout.ToggleLeft("Overwrite", overwriteBuild, GUILayout.Width(90f));
            if (GUILayout.Button("Build Pack", GUILayout.Width(120f)))
            {
                BuildPack();
            }
        }
    }

    private void DrawGroupPanel(PackCreatorAssetGroup group)
    {
        buildStyles[group] = (PackCreatorBuildStyle)EditorGUILayout.EnumPopup("Build Style", buildStyles[group]);
        EditorGUILayout.LabelField("Status", GetStatus(group));

        if (buildStyles[group] == PackCreatorBuildStyle.JsonBlueprint)
        {
            DrawJsonPanel(group);
            return;
        }

        if (buildStyles[group] == PackCreatorBuildStyle.SheetIso4Dir || buildStyles[group] == PackCreatorBuildStyle.SheetIso8Dir)
        {
            DrawSheetPanel(group);
            return;
        }

        EditorGUILayout.HelpBox("No additional input required for BasicShapes in v1.", MessageType.None);
    }

    private void DrawJsonPanel(PackCreatorAssetGroup group)
    {
        jsonInputs[group] = EditorGUILayout.TextArea(jsonInputs[group], GUILayout.MinHeight(220f));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save JSON", GUILayout.Width(120f)))
            {
                SaveJson(group);
            }

            if (GUILayout.Button("Validate JSON", GUILayout.Width(120f)))
            {
                ValidateJson(group);
            }
        }
    }

    private void DrawSheetPanel(PackCreatorAssetGroup group)
    {
        DrawDragDropBox(group);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Files...", GUILayout.Width(140f)))
            {
                SelectSheetFiles(group);
            }

            if (GUILayout.Button("Import + Rename", GUILayout.Width(140f)))
            {
                ImportAndRename(group);
            }

            if (GUILayout.Button("Validate Inputs", GUILayout.Width(130f)))
            {
                ValidateSheetInputs(group);
            }

            if (GUILayout.Button("Open Sheets Folder", GUILayout.Width(140f)))
            {
                EnsurePackFolders();
                EditorUtility.RevealInFinder(Path.GetFullPath(GetSheetsFolder(group)));
            }
        }

        DrawStagedTable();
    }

    private void DrawDragDropBox(PackCreatorAssetGroup group)
    {
        var dropRect = GUILayoutUtility.GetRect(0f, 64f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drag .png files here");
        var evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }

        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            AddFiles(group, DragAndDrop.paths);
            evt.Use();
        }
    }

    private void DrawStagedTable()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Staged Imports", EditorStyles.boldLabel);

        if (stagedRows.Count == 0)
        {
            EditorGUILayout.HelpBox("No files staged.", MessageType.None);
            return;
        }

        rowsScroll = EditorGUILayout.BeginScrollView(rowsScroll, GUILayout.MinHeight(180f));
        var simConfig = CurrentSimConfig();
        var entities = new[] { simConfig.defaultEntityId };
        var states = ResolveRequiredStates(simConfig.defaultEntityId);

        for (var i = 0; i < stagedRows.Count; i++)
        {
            var row = stagedRows[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.sourceFileName, GUILayout.Width(190f));
                EditorGUILayout.LabelField(row.guessedState, GUILayout.Width(90f));
                row.entityId = DrawPopup(row.entityId, entities, GUILayout.Width(120f));
                row.state = DrawPopup(row.state, states, GUILayout.Width(120f));
                EditorGUILayout.LabelField(row.dirSet + "dir", GUILayout.Width(60f));
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    stagedRows.RemoveAt(i);
                    i--;
                    continue;
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static string DrawPopup(string current, IReadOnlyList<string> options, params GUILayoutOption[] layout)
    {
        if (options == null || options.Count == 0)
        {
            return EditorGUILayout.TextField(current, layout);
        }

        var optionArray = options.ToArray();
        var currentIndex = Mathf.Max(0, Array.IndexOf(optionArray, current));
        var newIndex = EditorGUILayout.Popup(currentIndex, optionArray, layout);
        return optionArray[Mathf.Clamp(newIndex, 0, optionArray.Length - 1)];
    }

    private void SelectSheetFiles(PackCreatorAssetGroup group)
    {
        var selected = EditorUtility.OpenFilePanelWithFilters("Select PNG sheets", Application.dataPath, new[] { "PNG files", "png" });
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        AddFiles(group, new[] { selected });
    }

    private void AddFiles(PackCreatorAssetGroup group, IEnumerable<string> paths)
    {
        var dir = buildStyles[group] == PackCreatorBuildStyle.SheetIso8Dir ? 8 : 4;
        var defaults = CurrentSimConfig();
        var requiredStates = ResolveRequiredStates(defaults.defaultEntityId);

        foreach (var path in paths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(fullPath);
            var guessed = GuessState(fileName);
            var chosenState = requiredStates.Contains(guessed) ? guessed : requiredStates.FirstOrDefault() ?? guessed;

            stagedRows.Add(new PackCreatorSheetImportRow
            {
                sourcePath = fullPath,
                sourceFileName = fileName,
                guessedState = guessed,
                entityId = defaults.defaultEntityId,
                state = chosenState,
                dirSet = dir
            });
        }

        statusLine = $"Staged {stagedRows.Count} sheet file(s).";
    }

    private void ImportAndRename(PackCreatorAssetGroup group)
    {
        EnsurePackFolders();
        var sheetsFolder = GetSheetsFolder(group);
        var manifestPath = GetManifestPath(group);
        var manifest = PackInputManifest.Load(manifestPath);
        manifest.group = group.ToString();
        manifest.dirSet = buildStyles[group] == PackCreatorBuildStyle.SheetIso8Dir ? 8 : 4;
        manifest.framesPerState = 10;
        manifest.cellSize = 64;
        manifest.padding = 2;
        manifest.layout = "PerStateSheets";
        manifest.entityId = CurrentSimConfig().defaultEntityId;
        manifest.states = ResolveRequiredStates(manifest.entityId);

        foreach (var row in stagedRows)
        {
            var canonical = BuildCanonicalAgentSheetName(row.entityId, row.state, row.dirSet);
            var targetPath = $"{sheetsFolder}/{canonical}";
            if (File.Exists(Path.GetFullPath(targetPath)))
            {
                Debug.LogWarning($"[PackCreator] Overwriting existing file: {targetPath}");
            }

            File.Copy(row.sourcePath, Path.GetFullPath(targetPath), true);
            var existing = manifest.importedFiles.FirstOrDefault(x => string.Equals(x.state, row.state, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new PackInputManifest.ImportedFile();
                manifest.importedFiles.Add(existing);
            }

            existing.state = row.state;
            existing.canonicalFileName = canonical;
            existing.sourceHash = ComputeSha1(row.sourcePath);
        }

        PackInputManifest.Save(manifestPath, manifest);
        AssetDatabase.Refresh();
        statusLine = $"Imported {stagedRows.Count} sheet file(s) and wrote manifest.";
    }

    private void ValidateSheetInputs(PackCreatorAssetGroup group)
    {
        EnsurePackFolders();
        var sim = CurrentSimConfig();
        var entity = sim.defaultEntityId;
        var states = ResolveRequiredStates(entity);
        var dir = buildStyles[group] == PackCreatorBuildStyle.SheetIso8Dir ? 8 : 4;
        var missing = new List<string>();
        var warnings = new List<string>();

        foreach (var state in states)
        {
            var canonical = BuildCanonicalAgentSheetName(entity, state, dir);
            var path = $"{GetSheetsFolder(group)}/{canonical}";
            if (!File.Exists(Path.GetFullPath(path)))
            {
                missing.Add(canonical);
                continue;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                warnings.Add($"Unreadable texture: {path}");
                continue;
            }

            if (texture.width <= 0 || texture.height <= 0)
            {
                warnings.Add($"Invalid size: {path}");
            }
        }

        if (missing.Count == 0)
        {
            statusLine = warnings.Count == 0
                ? "Validation OK."
                : $"Validation OK with {warnings.Count} warning(s).";
        }
        else
        {
            statusLine = "Missing: " + string.Join(", ", missing);
        }

        foreach (var warning in warnings)
        {
            Debug.LogWarning("[PackCreator] " + warning);
        }
    }

    private void SaveJson(PackCreatorAssetGroup group)
    {
        EnsurePackFolders();
        var json = jsonInputs[group] ?? string.Empty;
        var target = GetJsonPath(group);
        File.WriteAllText(Path.GetFullPath(target), json);
        AssetDatabase.Refresh();
        statusLine = $"Saved JSON to {target}";
    }

    private void ValidateJson(PackCreatorAssetGroup group)
    {
        try
        {
            var parsed = JsonUtility.FromJson<PackInputManifest>(jsonInputs[group]);
            statusLine = parsed == null ? "JSON validation warning: JsonUtility returned null." : "JSON validation passed (syntax check via JsonUtility).";
        }
        catch (Exception ex)
        {
            statusLine = "JSON validation failed: " + ex.Message;
        }
    }

    private void CreateOrUpdateRecipe()
    {
        EnsurePackFolders();
        var recipePath = BuildRecipePath();
        var existing = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath);
        var recipe = CreateRecipeFromPresetOrFallback();
        recipe.packId = packId;
        recipe.seed = seed;
        recipe.simulationId = CurrentSimId();
        recipe.outputFolder = BuildPackRoot();

        if (existing == null)
        {
            AssetDatabase.CreateAsset(recipe, recipePath);
        }
        else
        {
            EditorUtility.CopySerialized(recipe, existing);
            DestroyImmediate(recipe);
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        statusLine = $"Recipe saved at {recipePath}";
    }

    private void BuildPack()
    {
        var recipePath = BuildRecipePath();
        var recipe = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath);
        if (recipe == null)
        {
            CreateOrUpdateRecipe();
            recipe = AssetDatabase.LoadAssetAtPath<PackRecipe>(recipePath);
        }

        if (recipe == null)
        {
            statusLine = "Build failed: unable to create/load recipe.";
            return;
        }

        var report = PackBuildPipeline.Build(recipe, overwriteBuild);
        statusLine = $"Build done. Sprites={report.spriteCount}, Textures={report.textureCount}, Warnings={report.warnings.Count}";
        if (report.warnings.Count > 0)
        {
            Debug.LogWarning("[PackCreator] Build warnings:\n" + string.Join("\n", report.warnings));
        }
    }

    private PackRecipe CreateRecipeFromPresetOrFallback()
    {
        var presets = ModuleRegistry.ListPresets();
        var simId = CurrentSimId();
        foreach (var preset in presets)
        {
            var recipe = preset.CreateDefaultRecipe(packId, seed);
            if (string.Equals(recipe.simulationId, simId, StringComparison.OrdinalIgnoreCase))
            {
                return recipe;
            }

            DestroyImmediate(recipe);
        }

        Debug.LogWarning($"[PackCreator] No preset found for '{simId}', using fallback recipe.");
        var fallback = ScriptableObject.CreateInstance<PackRecipe>();
        fallback.simulationId = simId;
        fallback.packId = packId;
        fallback.seed = seed;
        fallback.environmentId = "env.ant.v1";
        var entityId = CurrentSimConfig().defaultEntityId;
        fallback.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = entityId,
                archetypeId = "archetype.ant",
                speciesCount = 1,
                roles = new List<string> { "worker" },
                lifeStages = new List<string> { "adult" },
                states = ResolveRequiredStates(entityId)
            }
        };
        return fallback;
    }

    private string GetStatus(PackCreatorAssetGroup group)
    {
        var rootExists = AssetDatabase.IsValidFolder(BuildPackRoot());
        return rootExists ? "OK" : "Missing: pack folder not created yet";
    }

    private List<string> ResolveRequiredStates(string entityId)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<PackRecipe>(BuildRecipePath());
        var states = recipe?.entities?.FirstOrDefault(x => string.Equals(x.entityId, entityId, StringComparison.OrdinalIgnoreCase))?.states;
        if (states != null && states.Count > 0)
        {
            return new List<string>(states);
        }

        return new List<string>(CurrentSimConfig().defaultStates);
    }

    private static string GuessState(string fileName)
    {
        var match = StateGuessRegex.Match(fileName ?? string.Empty);
        return match.Success ? match.Value.ToLowerInvariant() : "idle";
    }

    private void EnsurePackFolders()
    {
        var root = BuildPackRoot();
        ImportSettingsUtil.EnsureFolder("Assets/Presentation");
        ImportSettingsUtil.EnsureFolder("Assets/Presentation/Packs");
        ImportSettingsUtil.EnsureFolder($"Assets/Presentation/Packs/{CurrentSimId()}");
        ImportSettingsUtil.EnsureFolder(root);

        foreach (PackCreatorAssetGroup group in Enum.GetValues(typeof(PackCreatorAssetGroup)))
        {
            ImportSettingsUtil.EnsureFolder($"{root}/Inputs/{group}");
            ImportSettingsUtil.EnsureFolder(GetJsonFolder(group));
            ImportSettingsUtil.EnsureFolder(GetSheetsFolder(group));
        }
    }

    private static string BuildCanonicalAgentSheetName(string entity, string state, int dirSet)
        => $"agent_{SanitizeToken(entity)}_{SanitizeToken(state)}_{dirSet}dir.png";

    private static string SanitizeToken(string value)
    {
        var token = (value ?? string.Empty).Trim().ToLowerInvariant();
        token = Regex.Replace(token, "[^a-z0-9_]+", "_");
        return token.Trim('_');
    }

    private static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private PackCreatorSimConfig CurrentSimConfig() => SimConfigs[CurrentSimId()];
    private string CurrentSimId() => SimConfigs.Keys.ElementAt(Mathf.Clamp(selectedSimIndex, 0, SimConfigs.Count - 1));
    private string BuildPackRoot() => $"Assets/Presentation/Packs/{CurrentSimId()}/{packId}";
    private string BuildRecipePath() => $"{BuildPackRoot()}/PackRecipe.asset";
    private string GetJsonFolder(PackCreatorAssetGroup group) => $"{BuildPackRoot()}/Inputs/{group}/Json";
    private string GetSheetsFolder(PackCreatorAssetGroup group) => $"{BuildPackRoot()}/Inputs/{group}/Sheets";
    private string GetManifestPath(PackCreatorAssetGroup group) => $"{GetSheetsFolder(group)}/manifest.json";
    private string GetJsonPath(PackCreatorAssetGroup group) => $"{GetJsonFolder(group)}/{group.ToString().ToLowerInvariant()}.json";
}
