using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WorldGenLabWindow : EditorWindow, IWorldGenLogger
{
    private IReadOnlyList<IWorldRecipe> recipes;
    private string[] recipeNames;
    private int selectedRecipe;
    private WorldRecipeSettingsSO settings;
    private Editor settingsEditor;

    private int seed = 1337;
    private string mapId = "Map_001";
    private int width = 128;
    private int height = 128;
    private float cellSize = 1f;
    private Vector2 origin;

    private bool livePreview = true;
    private bool showWater = true;
    private bool showWalkable = true;
    private bool showZones = true;
    private bool showSplines = true;
    private bool showScatter = true;

    private double nextLiveGenAt;
    private bool dirtyLiveGen;

    private NoiseDescriptorSet activeNoise = new NoiseDescriptorSet();
    private WorldMap previewMap;
    private Texture2D previewTexture;
    private Vector2 leftPanelScroll;
    private string logs = string.Empty;
    private string hoveredHelp = string.Empty;

    private static bool IsGridValid(WorldGridSpec grid)
    {
        return grid.width > 0 && grid.height > 0;
    }

    [MenuItem("GSP/Generator/WorldGen Lab")]
    public static void Open()
    {
        GetWindow<WorldGenLabWindow>("WorldGen Lab");
    }

    private void OnEnable()
    {
        recipes = WorldRecipeRegistry.GetRecipes();
        recipeNames = recipes.Select(r => r.RecipeId).ToArray();
        RecreateSettings();
        minSize = new Vector2(980f, 700f);
        EditorApplication.update += Tick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (settings != null) DestroyImmediate(settings);
        if (previewTexture != null) DestroyImmediate(previewTexture);
    }

    private void Tick()
    {
        if (livePreview && dirtyLiveGen && EditorApplication.timeSinceStartup >= nextLiveGenAt)
        {
            dirtyLiveGen = false;
            GeneratePreview();
            Repaint();
        }
    }

    private void OnGUI()
    {
        hoveredHelp = string.Empty;
        var hasRecipes = recipes != null && recipes.Count > 0;

        IDisposable horizontalScope = null;
        try
        {
            horizontalScope = new HorizontalScope();
            DrawLeftColumn(hasRecipes);
            DrawMiddleColumn(hasRecipes);
            CaptureHoveredTooltip();
            DrawHelpPanel(hasRecipes);
        }
        catch (Exception ex)
        {
            logs += $"[Error] UI exception: {ex.Message}\n";
            Repaint();
        }
        finally
        {
            horizontalScope?.Dispose();
        }
    }

    private void DrawLeftColumn(bool hasRecipes)
    {
        using (new VerticalScope(GUILayout.Width(360f), GUILayout.ExpandHeight(true)))
        using (var scroll = new ScrollScope(leftPanelScroll))
        {
            leftPanelScroll = scroll.Position;

            if (!hasRecipes)
            {
                EditorGUILayout.HelpBox("No recipes registered.", MessageType.Warning);
                return;
            }

            DrawRecipeAndGridControls();
            DrawSettingsControls();
            DrawActionButtons();
        }
    }

    private void DrawRecipeAndGridControls()
    {
        EditorGUI.BeginChangeCheck();
        selectedRecipe = EditorGUILayout.Popup(new GUIContent("Recipe", "Select a world generation recipe."), selectedRecipe, recipeNames);
        if (EditorGUI.EndChangeCheck())
        {
            RecreateSettings();
            MarkLiveDirty();
        }

        using (new HorizontalScope())
        {
            seed = EditorGUILayout.IntField(new GUIContent("Seed", "Seed used for deterministic world generation."), seed);
            if (GUILayout.Button(new GUIContent("Randomize", "Set seed to current UTC ticks."), GUILayout.Width(100f)))
            {
                seed = (int)DateTime.UtcNow.Ticks;
                MarkLiveDirty();
            }
        }

        mapId = EditorGUILayout.TextField(new GUIContent("Map ID", "Identifier stored with the generated map assets."), mapId);

        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        width = Mathf.Max(8, EditorGUILayout.IntField(new GUIContent("Width", "Grid width in cells."), width));
        height = Mathf.Max(8, EditorGUILayout.IntField(new GUIContent("Height", "Grid height in cells."), height));
        cellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Cell Size", "Size of one world grid cell."), cellSize));
        origin = EditorGUILayout.Vector2Field(new GUIContent("Origin", "World-space origin of the generated grid."), origin);
        livePreview = EditorGUILayout.Toggle(new GUIContent("Live Preview", "Automatically regenerate preview after settings changes."), livePreview);
    }

    private void DrawSettingsControls()
    {
        if (settings == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        if (settings is VoidNeonSettingsSO neon)
        {
            neon.railsCount = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Rails Count", "Number of neon rail splines generated."), neon.railsCount));
            neon.railLengthFactor = EditorGUILayout.FloatField(new GUIContent("Rail Length Factor", "Rail length as a fraction of map size."), neon.railLengthFactor);
            neon.railCurvature = EditorGUILayout.FloatField(new GUIContent("Rail Curvature", "How wavy the rails are (higher = more bends)."), neon.railCurvature);
            neon.railWidth = EditorGUILayout.FloatField(new GUIContent("Rail Width", "Visual width of rails in preview and baked spline width."), neon.railWidth);
            neon.emitterSpacing = EditorGUILayout.FloatField(new GUIContent("Emitter Spacing", "Distance between scatter emitters placed along rails."), neon.emitterSpacing);
            neon.marginCells = EditorGUILayout.IntField(new GUIContent("Margin Cells", "Keeps rails away from edges by this many grid cells."), neon.marginCells);
            neon.glowFalloff = EditorGUILayout.FloatField(new GUIContent("Glow Falloff", "Glow influence radius around rails."), neon.glowFalloff);
            neon.noiseScale = EditorGUILayout.FloatField(new GUIContent("Noise Scale", "Noise frequency applied to glow variation."), neon.noiseScale);
        }
        else
        {
            settingsEditor?.OnInspectorGUI();
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
            MarkLiveDirty();
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space();
        using (new HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Generate", "Generate a preview map with current settings."))) GeneratePreview();
            if (GUILayout.Button(new GUIContent("Bake", "Bake world map assets to the content folder."))) Bake(false);
            if (GUILayout.Button("Bake & Ping Folder")) Bake(true);
        }

        if (GUILayout.Button("Load provenance.json + Rebuild"))
        {
            RebuildFromProvenance();
        }
    }

    private void DrawMiddleColumn(bool hasRecipes)
    {
        using (new VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            DrawPreview(hasRecipes);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            using (new VerticalScope(EditorStyles.helpBox, GUILayout.Height(88f), GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(string.IsNullOrEmpty(logs) ? "No log messages yet." : logs, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            }
        }
    }

    private void DrawHelpPanel(bool hasRecipes)
    {
        using (new VerticalScope(EditorStyles.helpBox, GUILayout.Width(280f), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Help", EditorStyles.boldLabel);
            var helpText = !hasRecipes
                ? "Register a recipe to start using WorldGen Lab."
                : (string.IsNullOrEmpty(hoveredHelp) ? "Hover a setting to see help." : hoveredHelp);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
        }
    }

    private void DrawPreview(bool hasRecipes)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        var previewRect = GUILayoutUtility.GetRect(10f, 380f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(previewRect, new Color(0.08f, 0.08f, 0.08f, 1f));

        if (hasRecipes && previewTexture != null && previewMap != null && IsGridValid(previewMap.grid))
        {
            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, false);
        }

        Handles.BeginGUI();
        if (hasRecipes && previewMap != null)
        {
            if (showSplines && previewMap.splines != null)
            {
                Handles.color = Color.cyan;
                foreach (var spline in previewMap.splines)
                {
                    if (spline?.points == null) continue;
                    for (var i = 1; i < spline.points.Count; i++)
                    {
                        Handles.DrawLine(WorldToRect(previewRect, spline.points[i - 1]), WorldToRect(previewRect, spline.points[i]));
                    }
                }
            }

            if (showScatter && previewMap.scatters != null)
            {
                Handles.color = Color.magenta;
                foreach (var pair in previewMap.scatters)
                {
                    var points = pair.Value?.points;
                    if (points == null) continue;
                    foreach (var pt in points)
                    {
                        Handles.DrawSolidDisc(WorldToRect(previewRect, pt.pos), Vector3.forward, 2f);
                    }
                }
            }
        }
        Handles.EndGUI();

        using (new HorizontalScope())
        {
            showWalkable = EditorGUILayout.ToggleLeft(new GUIContent("Walkable", "Show walkable mask overlay."), showWalkable);
            showWater = EditorGUILayout.ToggleLeft(new GUIContent("Water", "Show water mask overlay."), showWater);
            showZones = EditorGUILayout.ToggleLeft(new GUIContent("Zones", "Show zones mask overlay."), showZones);
            showSplines = EditorGUILayout.ToggleLeft(new GUIContent("Splines", "Show generated spline paths."), showSplines);
            showScatter = EditorGUILayout.ToggleLeft(new GUIContent("Scatter", "Show scatter points."), showScatter);
        }

        EditorGUILayout.LabelField(GetMouseReadout(previewRect));
    }

    private string GetMouseReadout(Rect previewRect)
    {
        if (previewMap == null || !IsGridValid(previewMap.grid) || !previewRect.Contains(Event.current.mousePosition))
            return "Cell: -";

        var uv = new Vector2(
            (Event.current.mousePosition.x - previewRect.x) / Mathf.Max(1f, previewRect.width),
            (Event.current.mousePosition.y - previewRect.y) / Mathf.Max(1f, previewRect.height));
        var cx = Mathf.Clamp(Mathf.FloorToInt(uv.x * previewMap.grid.width), 0, Mathf.Max(0, previewMap.grid.width - 1));
        var cy = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * previewMap.grid.height), 0, Mathf.Max(0, previewMap.grid.height - 1));

        var msg = $"Cell {cx},{cy}";
        if (previewMap.scalars != null)
        {
            foreach (var scalar in previewMap.scalars)
            {
                var field = scalar.Value;
                if (field == null || !IsGridValid(field.grid)) continue;
                if (cx >= field.grid.width || cy >= field.grid.height) continue;
                msg += $" | {scalar.Key}:{scalar.Value[cx, cy]:0.###}";
            }
        }

        return msg;
    }

    private void CaptureHoveredTooltip()
    {
        if (!string.IsNullOrEmpty(GUI.tooltip))
        {
            hoveredHelp = GUI.tooltip;
        }
    }

    

    private Vector2 WorldToRect(Rect rect, Vector2 world)
    {
        var g = previewMap.grid;
        var u = (world.x - g.originWorld.x) / (g.width * g.cellSize);
        var v = (world.y - g.originWorld.y) / (g.height * g.cellSize);
        return new Vector2(rect.x + u * rect.width, rect.y + (1f - v) * rect.height);
    }

    private void RecreateSettings()
    {
        if (settings != null) DestroyImmediate(settings);
        settings = CreateInstance((Type)recipes[selectedRecipe].SettingsType) as WorldRecipeSettingsSO;
        settingsEditor = Editor.CreateEditor(settings);
        MarkLiveDirty();
    }

    private void MarkLiveDirty()
    {
        dirtyLiveGen = true;
        nextLiveGenAt = EditorApplication.timeSinceStartup + 0.2;
    }

    private WorldGridSpec CurrentGrid()
    {
        return new WorldGridSpec { width = width, height = height, cellSize = cellSize, originWorld = origin };
    }

    private void GeneratePreview()
    {
        logs = string.Empty;
        activeNoise = activeNoise ?? new NoiseDescriptorSet();
        var recipe = recipes[selectedRecipe];
        previewMap = recipe.Generate(settings, seed, CurrentGrid(), activeNoise, this);
        if (string.IsNullOrEmpty(previewMap.mapId)) previewMap.mapId = mapId;
        BuildPreviewTexture();
    }

    private void BuildPreviewTexture()
    {
        if (previewMap == null || !IsGridValid(previewMap.grid)) return;
        if (previewTexture != null) DestroyImmediate(previewTexture);
        previewTexture = new Texture2D(previewMap.grid.width, previewMap.grid.height, TextureFormat.RGBA32, false);
        var pixels = new Color[previewMap.grid.width * previewMap.grid.height];

        for (var y = 0; y < previewMap.grid.height; y++)
        for (var x = 0; x < previewMap.grid.width; x++)
        {
            var idx = previewMap.grid.Index(x, y);
            var c = new Color(0.1f, 0.1f, 0.1f, 1f);
            if (previewMap.scalars != null && previewMap.scalars.TryGetValue("height", out var heightField) && heightField != null)
            {
                var h = heightField[x, y];
                c = new Color(h, h, h, 1f);
            }

            if (showWater && previewMap.masks != null && previewMap.masks.TryGetValue("water", out var water) && water != null && water[x, y] > 0)
                c = Color.Lerp(c, new Color(0f, 0.4f, 1f, 1f), 0.7f);
            if (showWalkable && previewMap.masks != null && previewMap.masks.TryGetValue("walkable", out var walkable) && walkable != null && walkable[x, y] == 0)
                c = Color.Lerp(c, new Color(1f, 0f, 0f, 1f), 0.35f);
            if (showZones && previewMap.masks != null && previewMap.masks.TryGetValue("zones", out var zones) && zones != null)
            {
                var z = zones[x, y] % 4;
                var zc = z switch
                {
                    0 => new Color(0.8f, 0.8f, 0.2f, 1f),
                    1 => new Color(0.2f, 0.8f, 0.2f, 1f),
                    2 => new Color(0.2f, 0.8f, 0.8f, 1f),
                    _ => new Color(0.8f, 0.3f, 0.8f, 1f)
                };
                c = Color.Lerp(c, zc, 0.25f);
            }

            pixels[idx] = c;
        }

        previewTexture.SetPixels(pixels);
        previewTexture.Apply();
    }

    private void Bake(bool pingFolder)
    {
        if (previewMap == null) GeneratePreview();
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

        if (!string.IsNullOrEmpty(prov.settingsJson))
        {
            EditorJsonUtility.FromJsonOverwrite(prov.settingsJson, settings);
        }

        seed = prov.seed;
        mapId = prov.mapId;
        width = prov.grid.width;
        height = prov.grid.height;
        cellSize = prov.grid.cellSize;
        origin = prov.grid.originWorld;
        activeNoise = prov.noiseDescriptors ?? new NoiseDescriptorSet();

        GeneratePreview();
        if (previewMap != null)
        {
            Log($"Rebuilt map {previewMap.mapId}. Verify key outputs (spline/scatter/mask coverage) manually.");
        }
    }

    public void Log(string message)
    {
        logs += $"[Info] {message}\n";
    }

    public void Warn(string message)
    {
        logs += $"[Warn] {message}\n";
    }

    private sealed class HorizontalScope : IDisposable
    {
        public HorizontalScope(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginHorizontal(options);
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
}
