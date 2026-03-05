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
    private Vector2 previewScroll;
    private string logs = string.Empty;

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
        if (recipes == null || recipes.Count == 0)
        {
            EditorGUILayout.HelpBox("No recipes registered.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        selectedRecipe = EditorGUILayout.Popup("Recipe", selectedRecipe, recipeNames);
        if (EditorGUI.EndChangeCheck())
        {
            RecreateSettings();
            MarkLiveDirty();
        }

        EditorGUILayout.BeginHorizontal();
        seed = EditorGUILayout.IntField("Seed", seed);
        if (GUILayout.Button("Randomize", GUILayout.Width(100)))
        {
            seed = (int)DateTime.UtcNow.Ticks;
            MarkLiveDirty();
        }
        EditorGUILayout.EndHorizontal();

        mapId = EditorGUILayout.TextField("Map ID", mapId);

        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        width = Mathf.Max(8, EditorGUILayout.IntField("Width", width));
        height = Mathf.Max(8, EditorGUILayout.IntField("Height", height));
        cellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cell Size", cellSize));
        origin = EditorGUILayout.Vector2Field("Origin", origin);

        livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);

        if (settingsEditor != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            settingsEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) MarkLiveDirty();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate")) GeneratePreview();
        if (GUILayout.Button("Bake")) Bake(false);
        if (GUILayout.Button("Bake & Ping Folder")) Bake(true);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Load provenance.json + Rebuild"))
        {
            RebuildFromProvenance();
        }

        DrawPreview();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Log");
        EditorGUILayout.HelpBox(logs, MessageType.None);
    }

    private void DrawPreview()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        showWalkable = EditorGUILayout.Toggle("Overlay Walkable", showWalkable);
        showWater = EditorGUILayout.Toggle("Overlay Water", showWater);
        showZones = EditorGUILayout.Toggle("Overlay Zones", showZones);
        showSplines = EditorGUILayout.Toggle("Overlay Splines", showSplines);
        showScatter = EditorGUILayout.Toggle("Overlay Scatter", showScatter);

        var rect = GUILayoutUtility.GetRect(position.width - 20, position.width - 20, 128, 512);
        EditorGUI.DrawRect(rect, Color.black);

        if (previewTexture != null)
        {
            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);
        }

        if (previewMap != null)
        {
            Handles.BeginGUI();
            if (showSplines)
            {
                Handles.color = Color.cyan;
                foreach (var spline in previewMap.splines)
                {
                    for (var i = 1; i < spline.points.Count; i++)
                    {
                        Handles.DrawLine(WorldToRect(rect, spline.points[i - 1]), WorldToRect(rect, spline.points[i]));
                    }
                }
            }

            if (showScatter)
            {
                Handles.color = Color.magenta;
                foreach (var pair in previewMap.scatters)
                {
                    foreach (var pt in pair.Value.points)
                    {
                        Handles.DrawSolidDisc(WorldToRect(rect, pt.pos), Vector3.forward, 2f);
                    }
                }
            }
            Handles.EndGUI();

            var evt = Event.current;
            if (rect.Contains(evt.mousePosition))
            {
                var uv = new Vector2((evt.mousePosition.x - rect.x) / rect.width, (evt.mousePosition.y - rect.y) / rect.height);
                var cx = Mathf.Clamp(Mathf.FloorToInt(uv.x * previewMap.grid.width), 0, previewMap.grid.width - 1);
                var cy = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * previewMap.grid.height), 0, previewMap.grid.height - 1);
                var msg = $"Cell {cx},{cy}";
                foreach (var s in previewMap.scalars)
                {
                    msg += $" | {s.Key}:{s.Value[cx, cy]:0.###}";
                }
                EditorGUILayout.LabelField(msg);
            }
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
        if (previewMap == null) return;
        if (previewTexture != null) DestroyImmediate(previewTexture);
        previewTexture = new Texture2D(previewMap.grid.width, previewMap.grid.height, TextureFormat.RGBA32, false);
        var pixels = new Color[previewMap.grid.width * previewMap.grid.height];

        for (var y = 0; y < previewMap.grid.height; y++)
        for (var x = 0; x < previewMap.grid.width; x++)
        {
            var idx = previewMap.grid.Index(x, y);
            var c = new Color(0.1f, 0.1f, 0.1f, 1f);
            if (previewMap.scalars.TryGetValue("height", out var heightField))
            {
                var h = heightField[x, y];
                c = new Color(h, h, h, 1f);
            }

            if (showWater && previewMap.masks.TryGetValue("water", out var water) && water[x, y] > 0)
                c = Color.Lerp(c, new Color(0f, 0.4f, 1f, 1f), 0.7f);
            if (showWalkable && previewMap.masks.TryGetValue("walkable", out var walkable) && walkable[x, y] == 0)
                c = Color.Lerp(c, new Color(1f, 0f, 0f, 1f), 0.35f);
            if (showZones && previewMap.masks.TryGetValue("zones", out var zones))
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
}
