using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class WorldGenLabWindow : EditorWindow, IWorldGenLogger
{
    private const float LeftMinWidth = 360f;
    private const float LeftDefaultWidth = 420f;
    private const float RightPanelWidth = 280f;
    private const float SplitterWidth = 5f;

    private enum PreviewMode
    {
        Beauty,
        Height,
        Wetness,
        Walkable,
        Water,
        Zones,
        Scatter,
        Splines
    }

    private IReadOnlyList<IWorldRecipe> recipes;
    private string[] recipeNames = Array.Empty<string>();
    private int selectedRecipe;

    private WorldRecipeSettingsSO settings;
    private Editor settingsEditor;

    private List<WorldRecipePresetSO> availablePresets = new List<WorldRecipePresetSO>();
    private string[] presetNames = Array.Empty<string>();
    private int selectedPreset = -1;
    private bool presetUseCurrentGrid = true;

    private int seed = 1337;
    private bool treatSeedAsUnsigned;
    private string mapId = "Map_001";
    private int width = 128;
    private int height = 128;
    private float cellSize = 1f;
    private Vector2 origin;

    private bool autoFitPreview = true;
    private bool showNodeAnchors = true;
    private bool showLaneAnchors = true;

    private float leftPanelWidth = LeftDefaultWidth;
    private bool draggingSplitter;
    private Vector2 leftPanelScroll;
    private Vector2 logsScroll;

    private PreviewMode previewMode = PreviewMode.Beauty;
    private float heightContrast = 1.6f;
    private float heightGamma = 1f;
    private float previewHeightMin = 0f;
    private float previewHeightMax = 1f;

    private PreviewTransform previewTransform;
    private Texture2D previewTexture;
    private WorldMap previewMap;
    private NoiseSet activeNoise = new NoiseSet();

    private string logs = string.Empty;
    private string hoveredHelp = string.Empty;
    private string pinnedHelp = string.Empty;

    [MenuItem("GSP/Generator/WorldGen Lab")]
    public static void Open()
    {
        GetWindow<WorldGenLabWindow>("WorldGen Lab");
    }

    private void OnEnable()
    {
        recipes = WorldRecipeRegistry.GetRecipes();
        recipeNames = recipes?.Select(r => r.RecipeId).ToArray() ?? Array.Empty<string>();
        if (recipeNames.Length > 0)
        {
            selectedRecipe = Mathf.Clamp(selectedRecipe, 0, recipeNames.Length - 1);
            RecreateSettings();
            RefreshPresets();
        }

        minSize = new Vector2(1150f, 720f);
    }

    private void OnDisable()
    {
        if (settings != null) DestroyImmediate(settings);
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        if (previewTexture != null) DestroyImmediate(previewTexture);
    }

    private void OnGUI()
    {
        hoveredHelp = string.Empty;
        var hasRecipes = recipes != null && recipes.Count > 0;
        var fullRect = new Rect(0f, 0f, position.width, position.height);

        var minMiddleWidth = 360f;
        var maxLeftWidth = Mathf.Max(LeftMinWidth, fullRect.width - RightPanelWidth - minMiddleWidth - SplitterWidth * 2f);
        leftPanelWidth = Mathf.Clamp(leftPanelWidth, LeftMinWidth, maxLeftWidth);

        var leftRect = new Rect(fullRect.x, fullRect.y, leftPanelWidth, fullRect.height);
        var splitterRect = new Rect(leftRect.xMax, fullRect.y, SplitterWidth, fullRect.height);
        var rightRect = new Rect(fullRect.xMax - RightPanelWidth, fullRect.y, RightPanelWidth, fullRect.height);
        var middleRect = new Rect(splitterRect.xMax, fullRect.y, rightRect.xMin - splitterRect.xMax, fullRect.height);

        DrawSplitter(splitterRect, maxLeftWidth);

        using (new GUILayoutAreaScope(leftRect)) DrawLeftColumn(hasRecipes);
        using (new GUILayoutAreaScope(middleRect)) DrawMiddleColumn(hasRecipes);
        CaptureHoveredTooltip();
        using (new GUILayoutAreaScope(rightRect)) DrawHelpPanel(hasRecipes);
    }

    private void DrawSplitter(Rect splitterRect, float maxLeftWidth)
    {
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        EditorGUI.DrawRect(splitterRect, new Color(0.16f, 0.16f, 0.16f, 1f));

        var e = Event.current;
        if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition) && e.button == 0)
        {
            draggingSplitter = true;
            e.Use();
        }

        if (draggingSplitter && e.type == EventType.MouseDrag)
        {
            leftPanelWidth = Mathf.Clamp(e.mousePosition.x, LeftMinWidth, maxLeftWidth);
            Repaint();
            e.Use();
        }

        if (draggingSplitter && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
        {
            draggingSplitter = false;
            e.Use();
        }
    }

    private void DrawLeftColumn(bool hasRecipes)
    {
        using (new VerticalScope())
        {
            using (var scroll = new ScrollScope(leftPanelScroll, GUILayout.ExpandHeight(true)))
            {
                leftPanelScroll = scroll.Position;
                if (!hasRecipes)
                {
                    EditorGUILayout.HelpBox("No recipes registered.", MessageType.Warning);
                }
                else
                {
                    DrawRecipeAndGridControls();
                    DrawSettingsControls();
                    DrawActionButtons();
                }
            }
        }
    }

    private void DrawMiddleColumn(bool hasRecipes)
    {
        var previewHeaderHeight = 54f;
        var overlayHeight = 64f;
        var logHeight = 120f;
        var availableHeight = Mathf.Max(180f, position.height - previewHeaderHeight - overlayHeight - logHeight - 24f);

        using (new VerticalScope())
        {
            DrawPreviewHeader();
            var previewRect = GUILayoutUtility.GetRect(10f, availableHeight, GUILayout.ExpandWidth(true), GUILayout.Height(availableHeight));
            DrawPreviewCanvas(previewRect, hasRecipes);
            DrawOverlayRow();
            DrawLogPanel(logHeight);
        }
    }

    private void DrawPreviewHeader()
    {
        using (new HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel, GUILayout.Width(56f));

            if (previewMode == PreviewMode.Height)
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(new GUIContent("Contrast", "Expands/reduces visible height variation."), GUILayout.Width(54f));
                heightContrast = EditorGUILayout.Slider(heightContrast, 0.25f, 4f);

                EditorGUILayout.LabelField(new GUIContent("Gamma", "Brightness curve in Height preview."), GUILayout.Width(44f));
                heightGamma = EditorGUILayout.Slider(heightGamma, 0.5f, 2.4f);

                GUILayout.Space(6f);
                EditorGUILayout.LabelField($"Min {previewHeightMin:0.###} / Max {previewHeightMax:0.###}", GUILayout.Width(150f));
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawPreviewCanvas(Rect previewRect, bool hasRecipes)
    {
        EditorGUI.DrawRect(previewRect, new Color(0.08f, 0.08f, 0.08f, 1f));

        if (hasRecipes && previewMap != null && IsGridValid(previewMap.grid))
        {
            if (autoFitPreview || !previewTransform.IsValid)
                previewTransform = PreviewTransform.Create(previewRect, ComputeWorldRect(previewMap.grid), 8f);

            if (previewTexture != null)
                GUI.DrawTexture(previewTransform.PixelRect, previewTexture, ScaleMode.StretchToFill, false);

            DrawOverlays();
        }
        else
        {
            GUI.Label(new Rect(previewRect.x + 12f, previewRect.y + 8f, previewRect.width - 24f, 24f), "Generate a preview to visualize the selected recipe.");
        }

        GUI.Label(new Rect(previewRect.x + 8f, previewRect.yMax - 22f, previewRect.width - 16f, 20f), GetMouseReadout());
    }

    private void DrawOverlays()
    {
        if (previewMap == null || !previewTransform.IsValid) return;

        Handles.BeginGUI();
        DrawPreviewBounds();

        if ((previewMode == PreviewMode.Beauty || previewMode == PreviewMode.Splines) && previewMap.splines != null)
        {
            foreach (var spline in previewMap.splines)
            {
                if (spline?.points == null || spline.points.Count < 2) continue;
                var thickness = Mathf.Max(1f, spline.baseWidth / Mathf.Max(0.001f, previewMap.grid.cellSize));
                Handles.color = Color.cyan;
                for (var i = 1; i < spline.points.Count; i++)
                    Handles.DrawAAPolyLine(thickness, WorldToRect(spline.points[i - 1]), WorldToRect(spline.points[i]));

                if (previewMode == PreviewMode.Splines)
                {
                    Handles.color = new Color(1f, 0.75f, 0.25f, 0.95f);
                    foreach (var point in spline.points)
                        Handles.DrawSolidDisc(WorldToRect(point), Vector3.forward, 2.2f);
                }
            }
        }

        if ((previewMode == PreviewMode.Beauty || previewMode == PreviewMode.Scatter) && previewMap.scatters != null)
        {
            foreach (var scatter in previewMap.scatters.Values)
            {
                if (scatter?.points == null) continue;
                Handles.color = new Color(0.35f, 1f, 0.35f, 0.85f);
                foreach (var point in scatter.points)
                    Handles.DrawSolidDisc(WorldToRect(point.pos), Vector3.forward, 1.4f);
            }

            if (showLaneAnchors && previewMap.scatters.TryGetValue("anchors_lane", out var laneAnchors) && laneAnchors?.points != null)
            {
                Handles.color = new Color(1f, 0.3f, 1f, 0.95f);
                foreach (var point in laneAnchors.points) Handles.DrawSolidDisc(WorldToRect(point.pos), Vector3.forward, 1.8f);
            }

            if (showNodeAnchors && previewMap.scatters.TryGetValue("anchors_nodes", out var nodeAnchors) && nodeAnchors?.points != null)
            {
                Handles.color = new Color(1f, 0.95f, 0.2f, 0.95f);
                foreach (var point in nodeAnchors.points) Handles.DrawSolidDisc(WorldToRect(point.pos), Vector3.forward, 3f);
            }
        }

        Handles.EndGUI();
    }

    private void DrawOverlayRow()
    {
        using (new VerticalScope(EditorStyles.helpBox))
        {
            using (new HorizontalScope())
            {
                EditorGUILayout.LabelField("Mode", GUILayout.Width(34f));
                previewMode = (PreviewMode)EditorGUILayout.EnumPopup(previewMode, GUILayout.Width(130f));
                autoFitPreview = GUILayout.Toggle(autoFitPreview, new GUIContent("Auto-fit", "Fit map bounds into preview area."), EditorStyles.toolbarButton, GUILayout.Width(70f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Rebuild Texture", "Recompute preview visualization from current map."), GUILayout.Width(115f))) RebuildTextureOnly();
            }
        }
    }

    private void DrawLogPanel(float logHeight)
    {
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        using (new VerticalScope(EditorStyles.helpBox, GUILayout.Height(logHeight), GUILayout.ExpandWidth(true)))
        using (var scroll = new ScrollScope(logsScroll, GUILayout.ExpandHeight(true)))
        {
            logsScroll = scroll.Position;
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(logs) ? "No log messages yet." : logs, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        }
    }

    private void DrawHelpPanel(bool hasRecipes)
    {
        using (new VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Help", EditorStyles.boldLabel);
            if (!hasRecipes)
            {
                EditorGUILayout.HelpBox("Register a recipe to start using WorldGen Lab.", MessageType.Info);
                return;
            }

            var message = !string.IsNullOrEmpty(hoveredHelp)
                ? hoveredHelp
                : (!string.IsNullOrEmpty(pinnedHelp) ? pinnedHelp : DefaultRecipeHelp());
            EditorGUILayout.HelpBox(message, MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recommended preset values", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Natural River\n- RiverWidth: 4.5\n- MeanderAmp: 0.14\n- MeanderFreq: 1.8\n- FloodplainWidth: 13\n- HeightNoise amplitude: 0.45\n\nBig Meanders\n- RiverWidth: 7.0\n- MeanderAmp: 0.28\n- MeanderFreq: 1.2\n- RiverWarpAmplitude: 7.5\n- RiverWarpFrequency: 0.02", MessageType.None);
        }
    }

    private string DefaultRecipeHelp()
    {
        if (selectedRecipe < 0 || selectedRecipe >= recipeNames.Length) return "Hover a setting to see help.";
        if (recipeNames[selectedRecipe] != "SavannaRiver") return "Hover a setting to see help.";

        return "SavannaRiver tip: seed affects all registered noises and warp offset. River shape is strongly driven by MeanderAmp/Freq and RiverWarp settings; if shape changes feel subtle, increase WarpAmplitude or MeanderAmp.";
    }

    private void DrawRecipeAndGridControls()
    {
        EditorGUI.BeginChangeCheck();
        selectedRecipe = EditorGUILayout.Popup(new GUIContent("Recipe", "Select a world generation recipe."), selectedRecipe, recipeNames);
        if (EditorGUI.EndChangeCheck())
        {
            RecreateSettings();
            RefreshPresets();
        }

        EditorGUI.BeginChangeCheck();
        DrawPresetControls();

        using (new HorizontalScope())
        {
            seed = EditorGUILayout.IntField(new GUIContent("Seed", "Signed int seed. Negative values are valid and deterministic."), seed);
            if (GUILayout.Button(new GUIContent("Randomize", "Use a random 32-bit seed across full int range."), GUILayout.Width(96f)))
            {
                seed = Guid.NewGuid().GetHashCode();
            }
        }

        treatSeedAsUnsigned = EditorGUILayout.Toggle(new GUIContent("Treat seed as unsigned", "Display and edit seed via uint representation while preserving exact bits."), treatSeedAsUnsigned);
        if (treatSeedAsUnsigned)
        {
            var seedUnsigned = unchecked((uint)seed);
            var nextUnsigned = (uint)EditorGUILayout.LongField(new GUIContent("Seed (uint)", "Unsigned 32-bit view of the seed."), seedUnsigned);
            if (nextUnsigned != seedUnsigned)
            {
                seed = unchecked((int)nextUnsigned);
            }
        }
        else EditorGUILayout.LabelField(new GUIContent("Seed (uint)", "Unsigned 32-bit view of the same seed bits."), new GUIContent(unchecked((uint)seed).ToString()), EditorStyles.miniLabel);

        mapId = EditorGUILayout.TextField(new GUIContent("Map ID", "Identifier stored with generated map assets."), mapId);

        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        width = Mathf.Max(8, EditorGUILayout.IntField(new GUIContent("Width", "Grid width in cells."), width));
        height = Mathf.Max(8, EditorGUILayout.IntField(new GUIContent("Height", "Grid height in cells."), height));
        cellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Cell Size", "Size of one world grid cell."), cellSize));
        origin = EditorGUILayout.Vector2Field(new GUIContent("Origin", "World-space origin of the generated grid."), origin);
        EditorGUI.EndChangeCheck();
    }

    private void DrawPresetControls()
    {
        using (new HorizontalScope())
        {
            EditorGUI.BeginDisabledGroup(presetNames.Length == 0);
            selectedPreset = EditorGUILayout.Popup(new GUIContent("Preset", "Select a saved preset for current recipe."), Mathf.Clamp(selectedPreset, 0, Mathf.Max(0, presetNames.Length - 1)), presetNames.Length == 0 ? new[] { "(none)" } : presetNames);
            if (GUILayout.Button(new GUIContent("Apply", "Apply selected preset to grid and settings."), GUILayout.Width(58f))) ApplySelectedPreset();
            EditorGUI.EndDisabledGroup();

            presetUseCurrentGrid = EditorGUILayout.ToggleLeft(new GUIContent("Use current grid", "If enabled, applying preset keeps the current grid values."), presetUseCurrentGrid, GUILayout.Width(110f));
            if (GUILayout.Button(new GUIContent("Save", "Save current settings as a preset asset."), GUILayout.Width(58f))) SavePreset();
        }
    }

    private void DrawSettingsControls()
    {
        if (settings == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        settingsEditor?.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
        }

        DrawSavannaKeyHelpHints();
    }

    private void DrawSavannaKeyHelpHints()
    {
        if (!(settings is SavannaRiverSettingsSO)) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SavannaRiver quick help", EditorStyles.miniBoldLabel);
        DrawHelpLine("RiverWidth", "Channel width of main river spline.");
        DrawHelpLine("MeanderAmp", "How far river oscillates from centerline.");
        DrawHelpLine("MeanderFreq", "How many meander cycles across map.");
        DrawHelpLine("FloodplainWidth", "Distance where floodplain carving/wetness fades.");
        DrawHelpLine("BankWidth", "Transition width around river edge.");
        DrawHelpLine("WaterLevel", "Biome threshold for high/low elevation decisions.");
        DrawHelpLine("CarveStrength", "Depth/strength of river carving in height field.");
        DrawHelpLine("HeightNoise", "Noise type/frequency/octaves/lacunarity/gain/amplitude blended into terrain height.");
        DrawHelpLine("RiverWarpAmplitude", "Amount of warp applied to spline points.");
        DrawHelpLine("RiverWarpFrequency", "Frequency for warp noise sampling.");
    }

    private void DrawHelpLine(string label, string help)
    {
        EditorGUILayout.LabelField(new GUIContent(label, help), EditorStyles.miniLabel);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Manual mode: preview updates only when Generate is clicked.",
            MessageType.None);
        using (new HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Generate", "Generate preview map with current settings."))) GenerateMapAndPreview();
            if (GUILayout.Button(new GUIContent("Bake", "Bake world map assets to content folder."))) Bake(false);
            if (GUILayout.Button(new GUIContent("Bake & Ping", "Bake and ping output folder."))) Bake(true);
        }

        if (GUILayout.Button(new GUIContent("Load provenance.json + Rebuild", "Load world provenance json and regenerate that world.")))
            RebuildFromProvenance();
    }

    private void RefreshPresets()
    {
        availablePresets.Clear();
        presetNames = Array.Empty<string>();
        selectedPreset = -1;
        if (recipes == null || recipes.Count == 0) return;

        var recipeId = recipes[selectedRecipe].RecipeId;
        foreach (var guid in AssetDatabase.FindAssets("t:WorldRecipePresetSO"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var preset = AssetDatabase.LoadAssetAtPath<WorldRecipePresetSO>(path);
            if (preset == null || !string.Equals(preset.recipeId, recipeId, StringComparison.Ordinal)) continue;
            availablePresets.Add(preset);
        }

        availablePresets = availablePresets.OrderBy(p => p.presetName, StringComparer.OrdinalIgnoreCase).ToList();
        presetNames = availablePresets.Select(p => p.presetName).ToArray();
        selectedPreset = presetNames.Length > 0 ? 0 : -1;
    }

    private void ApplySelectedPreset()
    {
        if (selectedPreset < 0 || selectedPreset >= availablePresets.Count || settings == null) return;

        var preset = availablePresets[selectedPreset];
        if (!preset.useCurrentGrid && IsGridValid(preset.gridDefaults))
        {
            width = preset.gridDefaults.width;
            height = preset.gridDefaults.height;
            cellSize = preset.gridDefaults.cellSize;
            origin = preset.gridDefaults.originWorld;
        }

        if (!string.IsNullOrWhiteSpace(preset.settingsJson))
            WorldPresetIO.ApplySettingsJson(settings, preset.settingsJson);

        EditorUtility.SetDirty(settings);
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        settingsEditor = Editor.CreateEditor(settings);

        Log($"Applied preset '{preset.presetName}'.");
        Repaint();
    }

    private void SavePreset()
    {
        if (settings == null || recipes == null || recipes.Count == 0) return;

        var recipeId = recipes[selectedRecipe].RecipeId;
        var presetName = EditorUtility.SaveFilePanelInProject(
            "Save World Recipe Preset",
            $"{recipeId}_Preset",
            "asset",
            "Enter a preset name and save location under recipe presets.",
            $"Assets/Content/Worlds/_Presets/{recipeId}");

        if (string.IsNullOrWhiteSpace(presetName)) return;

        var fileName = Path.GetFileNameWithoutExtension(presetName);
        var forcedFolder = $"Assets/Content/Worlds/_Presets/{recipeId}";
        EnsureFolder(forcedFolder);
        var fullPath = $"{forcedFolder}/{fileName}.asset";

        var preset = WorldRecipePresetSO.Create(
            recipeId,
            fileName,
            CurrentGrid(),
            presetUseCurrentGrid,
            WorldPresetIO.CaptureSettingsJson(settings),
            CaptureNoiseJson(),
            settings.GetType().AssemblyQualifiedName);

        var existing = AssetDatabase.LoadAssetAtPath<WorldRecipePresetSO>(fullPath);
        if (existing != null) AssetDatabase.DeleteAsset(fullPath);
        AssetDatabase.CreateAsset(preset, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshPresets();
        selectedPreset = Mathf.Max(0, availablePresets.FindIndex(p => p == preset));
        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
        Log($"Saved preset '{fileName}' at {fullPath}.");
    }

    private string CaptureNoiseJson()
    {
        var snapshot = new NoiseSet();
        if (settings == null) return string.Empty;

        foreach (var field in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType != typeof(NoiseDescriptor)) continue;
            var descriptor = (NoiseDescriptor)field.GetValue(settings);
            snapshot.Register(descriptor, seed);
        }

        return JsonUtility.ToJson(snapshot, true);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        var leaf = Path.GetFileName(folder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, leaf);
    }

    private void GenerateMapAndPreview()
    {
        var mapStopwatch = Stopwatch.StartNew();
        if (!GenerateWorldMap()) return;
        mapStopwatch.Stop();
        Log($"Generation took {mapStopwatch.ElapsedMilliseconds} ms.");

        var textureStopwatch = Stopwatch.StartNew();
        BuildPreviewTexture();
        textureStopwatch.Stop();
        Log($"Texture build took {textureStopwatch.ElapsedMilliseconds} ms.");
    }

    private bool GenerateWorldMap()
    {
        if (recipes == null || recipes.Count == 0 || settings == null) return false;

        logs = string.Empty;
        activeNoise = activeNoise ?? new NoiseSet();
        activeNoise.descriptors.Clear();

        var recipe = recipes[selectedRecipe];
        previewMap = recipe.Generate(settings, seed, CurrentGrid(), activeNoise, this);
        if (previewMap != null && string.IsNullOrEmpty(previewMap.mapId)) previewMap.mapId = mapId;
        return previewMap != null;
    }

    private void RebuildTextureOnly()
    {
        if (previewMap == null || !IsGridValid(previewMap.grid)) return;

        var stopwatch = Stopwatch.StartNew();
        BuildPreviewTexture();
        stopwatch.Stop();
        Log($"Texture build took {stopwatch.ElapsedMilliseconds} ms.");
    }

    private void BuildPreviewTexture()
    {
        if (previewMap == null || !IsGridValid(previewMap.grid)) return;

        if (previewTexture != null) DestroyImmediate(previewTexture);
        previewTexture = new Texture2D(previewMap.grid.width, previewMap.grid.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[previewMap.grid.width * previewMap.grid.height];
        var hasHeight = TryGetScalar("height", out var heightField);
        var hasWalkable = TryGetMask("walkable", out var walkableField);
        var hasWater = TryGetMask("water", out var waterField);
        var hasZones = TryGetMask("zones", out var zonesField);
        var hasWetness = TryGetScalar("wetness", out var wetnessField);

        previewHeightMin = 0f;
        previewHeightMax = 1f;
        if (hasHeight && heightField.values != null && heightField.values.Length > 0)
        {
            previewHeightMin = float.MaxValue;
            previewHeightMax = float.MinValue;
            foreach (var value in heightField.values)
            {
                if (value < previewHeightMin) previewHeightMin = value;
                if (value > previewHeightMax) previewHeightMax = value;
            }

            if (previewHeightMin > previewHeightMax)
            {
                previewHeightMin = 0f;
                previewHeightMax = 1f;
            }
        }

        for (var y = 0; y < previewMap.grid.height; y++)
        for (var x = 0; x < previewMap.grid.width; x++)
        {
            var idx = previewMap.grid.Index(x, y);
            var heightValue = hasHeight ? heightField[x, y] : 0f;
            Color color;

            switch (previewMode)
            {
                case PreviewMode.Height:
                    color = HeightColor(heightValue);
                    break;
                case PreviewMode.Wetness:
                    var wetValue = hasWetness ? wetnessField[x, y] : 0f;
                    color = new Color(wetValue, wetValue, wetValue, 1f);
                    break;
                case PreviewMode.Walkable:
                    color = hasWalkable && walkableField[x, y] > 0 ? Color.white : Color.black;
                    break;
                case PreviewMode.Water:
                    color = hasWater && waterField[x, y] > 0 ? new Color(0.12f, 0.45f, 1f, 1f) : new Color(0.06f, 0.06f, 0.06f, 1f);
                    break;
                case PreviewMode.Zones:
                    color = hasZones ? ZoneColor(zonesField[x, y]) : new Color(0.1f, 0.1f, 0.1f, 1f);
                    break;
                case PreviewMode.Scatter:
                case PreviewMode.Splines:
                    color = HeightColor(heightValue);
                    break;
                default:
                    color = BeautyColor(x, y, hasHeight ? heightField : null, hasWalkable ? walkableField : null, hasWater ? waterField : null, hasZones ? zonesField : null);
                    break;
            }

            pixels[idx] = color;
        }

        previewTexture.SetPixels(pixels);
        previewTexture.Apply();
    }

    private Color BeautyColor(int x, int y, ScalarField heightField, MaskField walkable, MaskField water, MaskField zones)
    {
        var baseColor = heightField != null ? HeightColor(heightField[x, y]) : new Color(0.2f, 0.2f, 0.2f, 1f);
        if (water != null && water[x, y] > 0) baseColor = Color.Lerp(baseColor, new Color(0.12f, 0.45f, 1f, 1f), 0.8f);
        if (walkable != null && walkable[x, y] == 0) baseColor = Color.Lerp(baseColor, Color.red, 0.3f);
        if (zones != null) baseColor = Color.Lerp(baseColor, ZoneColor(zones[x, y]), 0.25f);
        return baseColor;
    }

    private Color HeightColor(float h)
    {
        var normalized = Mathf.InverseLerp(previewHeightMin, previewHeightMax, h);
        var contrasted = Mathf.Clamp01((normalized - 0.5f) * heightContrast + 0.5f);
        var gammaAdjusted = Mathf.Pow(contrasted, heightGamma);
        return new Color(gammaAdjusted, gammaAdjusted, gammaAdjusted, 1f);
    }

    private static Color ZoneColor(int zone)
    {
        switch (zone % 6)
        {
            case 0: return new Color(0.85f, 0.75f, 0.3f, 1f);
            case 1: return new Color(0.3f, 0.85f, 0.35f, 1f);
            case 2: return new Color(0.26f, 0.72f, 0.9f, 1f);
            case 3: return new Color(0.82f, 0.44f, 0.85f, 1f);
            case 4: return new Color(0.95f, 0.5f, 0.2f, 1f);
            default: return new Color(0.85f, 0.85f, 0.85f, 1f);
        }
    }

    private bool TryGetScalar(string id, out ScalarField field)
    {
        field = null;
        return previewMap != null && previewMap.scalars != null && previewMap.scalars.TryGetValue(id, out field) && field != null;
    }

    private bool TryGetMask(string id, out MaskField field)
    {
        field = null;
        return previewMap != null && previewMap.masks != null && previewMap.masks.TryGetValue(id, out field) && field != null;
    }

    private string GetMouseReadout()
    {
        if (previewMap == null || !IsGridValid(previewMap.grid) || !previewTransform.IsValid || !previewTransform.PixelRect.Contains(Event.current.mousePosition))
            return "Cell: -";

        var worldPos = previewTransform.PixelToWorld(Event.current.mousePosition);
        var localX = (worldPos.x - previewMap.grid.originWorld.x) / Mathf.Max(0.0001f, previewMap.grid.cellSize);
        var localY = (worldPos.y - previewMap.grid.originWorld.y) / Mathf.Max(0.0001f, previewMap.grid.cellSize);
        var cx = Mathf.Clamp(Mathf.FloorToInt(localX), 0, Mathf.Max(0, previewMap.grid.width - 1));
        var cy = Mathf.Clamp(Mathf.FloorToInt(localY), 0, Mathf.Max(0, previewMap.grid.height - 1));

        var msg = $"world {worldPos.x:0.##},{worldPos.y:0.##} | cell {cx},{cy}";
        if (previewMap.scalars != null)
        {
            foreach (var scalar in previewMap.scalars)
            {
                var field = scalar.Value;
                if (field == null || !IsGridValid(field.grid) || cx >= field.grid.width || cy >= field.grid.height) continue;
                msg += $" | {scalar.Key}:{field[cx, cy]:0.###}";
            }
        }

        return msg;
    }

    private void CaptureHoveredTooltip()
    {
        if (!string.IsNullOrEmpty(GUI.tooltip)) hoveredHelp = GUI.tooltip;
        if (Event.current.type == EventType.MouseDown && !string.IsNullOrEmpty(hoveredHelp)) pinnedHelp = hoveredHelp;
    }

    private void DrawPreviewBounds()
    {
        var min = previewTransform.WorldRect.min;
        var max = previewTransform.WorldRect.max;

        var p0 = WorldToRect(new Vector2(min.x, min.y));
        var p1 = WorldToRect(new Vector2(max.x, min.y));
        var p2 = WorldToRect(new Vector2(max.x, max.y));
        var p3 = WorldToRect(new Vector2(min.x, max.y));

        Handles.color = new Color(1f, 0.55f, 0f, 1f);
        Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
    }

    private Vector2 WorldToRect(Vector2 world)
    {
        return previewTransform.WorldToPixel(world);
    }

    private static Rect ComputeWorldRect(WorldGridSpec grid)
    {
        var min = grid.originWorld;
        var max = grid.originWorld + new Vector2(grid.width * grid.cellSize, grid.height * grid.cellSize);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool IsGridValid(WorldGridSpec grid)
    {
        return grid.width > 0 && grid.height > 0;
    }

    private void RecreateSettings()
    {
        if (recipes == null || recipes.Count == 0 || selectedRecipe < 0 || selectedRecipe >= recipes.Count) return;

        if (settings != null) DestroyImmediate(settings);
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        settings = CreateInstance((Type)recipes[selectedRecipe].SettingsType) as WorldRecipeSettingsSO;
        settingsEditor = settings != null ? Editor.CreateEditor(settings) : null;
    }

    private WorldGridSpec CurrentGrid()
    {
        return new WorldGridSpec { width = width, height = height, cellSize = cellSize, originWorld = origin };
    }

    private void Bake(bool pingFolder)
    {
        if (previewMap == null) GenerateMapAndPreview();
        if (previewMap == null) return;

        previewMap.mapId = mapId;
        var outDir = $"Assets/Content/Worlds/{recipes[selectedRecipe].RecipeId}/{mapId}";
        WorldMapBaker.Bake(previewMap, recipes[selectedRecipe], settings, activeNoise, outDir);
        AssetDatabase.Refresh();

        if (pingFolder)
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outDir);
            if (folder != null) EditorGUIUtility.PingObject(folder);
        }

        Log($"Baked to {outDir}");
    }

    private void RebuildFromProvenance()
    {
        var path = EditorUtility.OpenFilePanel("Select provenance.json", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        var json = File.ReadAllText(path);
        var prov = JsonUtility.FromJson<WorldProvenance>(json);
        if (prov == null)
        {
            Warn("Failed to parse provenance file.");
            return;
        }

        var recipeIndex = Array.FindIndex(recipeNames, n => n == prov.recipeId);
        if (recipeIndex < 0)
        {
            Warn($"Recipe '{prov.recipeId}' not found in registry.");
            return;
        }

        selectedRecipe = recipeIndex;
        RecreateSettings();
        RefreshPresets();

        if (!string.IsNullOrEmpty(prov.settingsJson)) EditorJsonUtility.FromJsonOverwrite(prov.settingsJson, settings);

        seed = prov.seed;
        mapId = prov.mapId;
        width = prov.grid.width;
        height = prov.grid.height;
        cellSize = prov.grid.cellSize;
        origin = prov.grid.originWorld;
        activeNoise = prov.noiseDescriptors ?? new NoiseSet();

        GenerateMapAndPreview();
        if (previewMap != null) Log($"Rebuilt map {previewMap.mapId}.");
    }

    public void Log(string message)
    {
        logs += $"[Info] {message}\n";
    }

    public void Warn(string message)
    {
        logs += $"[Warn] {message}\n";
    }

    private sealed class GUILayoutAreaScope : IDisposable
    {
        public GUILayoutAreaScope(Rect rect)
        {
            GUILayout.BeginArea(rect);
        }

        public void Dispose()
        {
            GUILayout.EndArea();
        }
    }

    private sealed class HorizontalScope : IDisposable
    {
        public HorizontalScope(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginHorizontal(options);
        }

        public HorizontalScope(GUIStyle style, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginHorizontal(style, options);
        }

        public void Dispose()
        {
            EditorGUILayout.EndHorizontal();
        }
    }

    private sealed class VerticalScope : IDisposable
    {
        public VerticalScope(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(options);
        }

        public VerticalScope(GUIStyle style, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(style, options);
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
        }
    }

    private sealed class ScrollScope : IDisposable
    {
        public Vector2 Position { get; }

        public ScrollScope(Vector2 position, params GUILayoutOption[] options)
        {
            Position = EditorGUILayout.BeginScrollView(position, options);
        }

        public void Dispose()
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private readonly struct PreviewTransform
    {
        public readonly Rect WorldRect;
        public readonly Rect PixelRect;
        public readonly bool IsValid;

        private PreviewTransform(Rect worldRect, Rect pixelRect, bool isValid)
        {
            WorldRect = worldRect;
            PixelRect = pixelRect;
            IsValid = isValid;
        }

        public static PreviewTransform Create(Rect canvasRect, Rect worldRect, float padding)
        {
            var padded = new Rect(
                canvasRect.x + padding,
                canvasRect.y + padding,
                Mathf.Max(1f, canvasRect.width - padding * 2f),
                Mathf.Max(1f, canvasRect.height - padding * 2f));

            var worldWidth = Mathf.Max(0.0001f, worldRect.width);
            var worldHeight = Mathf.Max(0.0001f, worldRect.height);
            var scale = Mathf.Min(padded.width / worldWidth, padded.height / worldHeight);

            var fitW = worldWidth * scale;
            var fitH = worldHeight * scale;
            var pixelRect = new Rect(
                padded.x + (padded.width - fitW) * 0.5f,
                padded.y + (padded.height - fitH) * 0.5f,
                fitW,
                fitH);

            return new PreviewTransform(worldRect, pixelRect, worldRect.width > 0f && worldRect.height > 0f && pixelRect.width > 0f && pixelRect.height > 0f);
        }

        public Vector2 WorldToPixel(Vector2 world)
        {
            var u = (world.x - WorldRect.xMin) / Mathf.Max(0.0001f, WorldRect.width);
            var v = (world.y - WorldRect.yMin) / Mathf.Max(0.0001f, WorldRect.height);
            return new Vector2(PixelRect.x + u * PixelRect.width, PixelRect.y + (1f - v) * PixelRect.height);
        }

        public Vector2 PixelToWorld(Vector2 pixel)
        {
            var u = (pixel.x - PixelRect.x) / Mathf.Max(0.0001f, PixelRect.width);
            var v = 1f - ((pixel.y - PixelRect.y) / Mathf.Max(0.0001f, PixelRect.height));
            return new Vector2(WorldRect.xMin + u * WorldRect.width, WorldRect.yMin + v * WorldRect.height);
        }
    }
}
