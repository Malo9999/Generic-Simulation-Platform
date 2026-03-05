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
    private bool showWalkable = true;
    private bool showLanes = true;
    private bool showZones = true;
    private bool showSplines = true;
    private bool showScatter = true;
    private bool showNodeAnchors = true;
    private bool showLaneAnchors = true;
    private bool autoFitPreview = true;

    private PreviewTransform previewTransform;

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
            neon.nodeCount = Mathf.Max(8, EditorGUILayout.IntField(new GUIContent("Node Count", "Maximum Poisson graph nodes."), neon.nodeCount));
            neon.nodeMinDist = Mathf.Max(1f, EditorGUILayout.FloatField(new GUIContent("Node Min Dist", "Minimum node spacing in cells."), neon.nodeMinDist));
            neon.kNearest = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("K Nearest", "Nearest neighbors used for edge connections."), neon.kNearest), 2, 5);
            neon.edgeWidthMin = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Edge Width Min", "Minimum spline width for generated edges."), neon.edgeWidthMin));
            neon.edgeWidthMax = Mathf.Max(neon.edgeWidthMin, EditorGUILayout.FloatField(new GUIContent("Edge Width Max", "Maximum spline width for generated edges."), neon.edgeWidthMax));
            neon.organicJitter = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Organic Jitter", "Per-edge bending amount before smoothing."), neon.organicJitter));
            neon.smoothIterations = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Smooth Iterations", "Chaikin smoothing iterations for each edge spline."), neon.smoothIterations), 0, 3);
            neon.marginCells = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Margin Cells", "Keeps generated nodes away from borders."), neon.marginCells));
            neon.anchorSpacing = Mathf.Max(0.5f, EditorGUILayout.FloatField(new GUIContent("Anchor Spacing", "Spacing used to sample anchors along lane splines."), neon.anchorSpacing));
            neon.glowFalloff = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Glow Falloff", "Glow influence radius around generated lanes."), neon.glowFalloff));
            neon.noiseScale = Mathf.Max(0.0001f, EditorGUILayout.FloatField(new GUIContent("Noise Scale", "Noise frequency applied to glow variation."), neon.noiseScale));
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

        if (hasRecipes && previewMap != null && IsGridValid(previewMap.grid))
        {
            if (autoFitPreview || !previewTransform.IsValid)
            {
                previewTransform = PreviewTransform.Create(previewRect, ComputeWorldRect(previewMap.grid), 8f);
            }

            if (previewTexture != null)
            {
                GUI.DrawTexture(previewTransform.PixelRect, previewTexture, ScaleMode.StretchToFill, false);
            }
        }

        Handles.BeginGUI();
        if (hasRecipes && previewMap != null && previewTransform.IsValid)
        {
            DrawPreviewBounds();

            if (showSplines && previewMap.splines != null)
            {
                foreach (var spline in previewMap.splines)
                {
                    if (spline?.points == null) continue;
                    var thickness = Mathf.Max(1f, spline.baseWidth / Mathf.Max(0.001f, previewMap.grid.cellSize));
                    Handles.color = Color.cyan;
                    for (var i = 1; i < spline.points.Count; i++)
                    {
                        Handles.DrawAAPolyLine(thickness, WorldToRect(spline.points[i - 1]), WorldToRect(spline.points[i]));
                    }
                }
            }

            if (showScatter && previewMap.scatters != null)
            {
                if (showLaneAnchors && previewMap.scatters.TryGetValue("anchors_lane", out var laneAnchors) && laneAnchors?.points != null)
                {
                    Handles.color = new Color(1f, 0.3f, 1f, 0.95f);
                    foreach (var pt in laneAnchors.points)
                    {
                        Handles.DrawSolidDisc(WorldToRect(pt.pos), Vector3.forward, 1.8f);
                    }
                }

                if (showNodeAnchors && previewMap.scatters.TryGetValue("anchors_nodes", out var nodeAnchors) && nodeAnchors?.points != null)
                {
                    Handles.color = new Color(1f, 0.95f, 0.2f, 0.95f);
                    foreach (var pt in nodeAnchors.points)
                    {
                        Handles.DrawSolidDisc(WorldToRect(pt.pos), Vector3.forward, 3.8f);
                    }
                }
            }
        }
        Handles.EndGUI();

        using (new HorizontalScope())
        {
            autoFitPreview = EditorGUILayout.ToggleLeft(new GUIContent("Auto-fit", "Fit map bounds into the preview area with a small padding."), autoFitPreview);
            showWalkable = EditorGUILayout.ToggleLeft(new GUIContent("Walkable", "Show walkable mask overlay."), showWalkable);
            showLanes = EditorGUILayout.ToggleLeft(new GUIContent("Lanes", "Show lane mask overlay."), showLanes);
            showZones = EditorGUILayout.ToggleLeft(new GUIContent("Zones", "Show zones mask overlay."), showZones);
            showSplines = EditorGUILayout.ToggleLeft(new GUIContent("Splines", "Show generated spline paths."), showSplines);
            showScatter = EditorGUILayout.ToggleLeft(new GUIContent("Scatter", "Show scatter points."), showScatter);
            showNodeAnchors = EditorGUILayout.ToggleLeft(new GUIContent("Node Anchors", "Show anchors_nodes points."), showNodeAnchors);
            showLaneAnchors = EditorGUILayout.ToggleLeft(new GUIContent("Lane Anchors", "Show anchors_lane points."), showLaneAnchors);
        }

        EditorGUILayout.LabelField(GetMouseReadout());
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
        msg += $" | walkable:{WorldMapQuery.IsWalkable(previewMap, worldPos)}";

        var nearestSpline = WorldMapQuery.GetNearestSpline(previewMap, worldPos);
        if (nearestSpline != null)
        {
            var nearestPoint = WorldMapQuery.GetNearestPointOnSpline(nearestSpline, worldPos);
            msg += $" | spline:{nearestSpline.id} d={Vector2.Distance(worldPos, nearestPoint):0.##}";
        }

        if (previewMap.scatters != null && previewMap.scatters.TryGetValue("anchors_nodes", out var nodes) && nodes?.points != null && nodes.points.Count > 0)
        {
            var nearestNodeIndex = -1;
            var bestD2 = float.MaxValue;
            for (var i = 0; i < nodes.points.Count; i++)
            {
                var d2 = (nodes.points[i].pos - worldPos).sqrMagnitude;
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                nearestNodeIndex = i;
            }

            if (nearestNodeIndex >= 0) msg += $" | nearestNode:{nearestNodeIndex} d={Mathf.Sqrt(bestD2):0.##}";
        }

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

            if (showLanes && previewMap.masks != null && previewMap.masks.TryGetValue("lanes", out var lanes) && lanes != null && lanes[x, y] > 0)
                c = Color.Lerp(c, new Color(0f, 0.8f, 1f, 1f), 0.65f);
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
            return new Vector2(
                PixelRect.x + u * PixelRect.width,
                PixelRect.y + (1f - v) * PixelRect.height);
        }

        public Vector2 PixelToWorld(Vector2 pixel)
        {
            var u = (pixel.x - PixelRect.x) / Mathf.Max(0.0001f, PixelRect.width);
            var v = 1f - ((pixel.y - PixelRect.y) / Mathf.Max(0.0001f, PixelRect.height));
            return new Vector2(
                WorldRect.xMin + u * WorldRect.width,
                WorldRect.yMin + v * WorldRect.height);
        }
    }
}
