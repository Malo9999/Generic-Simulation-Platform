using UnityEditor;
using UnityEngine;

public sealed class AntPackGeneratorWindow : EditorWindow
{
    private string packName = "AntPack";
    private int seed = 12345;
    private int tileSize = 32;
    private int antSpriteSize = 64;
    private AntPalettePreset palette = AntPalettePreset.Classic;
    private string outputFolder = "Assets/Presentation/Packs/Ants/AntPack";
    private bool generateTiles = true;
    private bool generateAnts = true;
    private bool generateProps = true;
    private bool overwrite;
    private float previewZoom = 3f;
    private Texture2D antsPreviewTexture;
    private string summary = string.Empty;

    [MenuItem("Tools/Generic Simulation Platform/Art/Generate Ant Pack…")]
    public static void OpenWindow()
    {
        var window = GetWindow<AntPackGeneratorWindow>("Generate Ant Pack");
        window.minSize = new Vector2(460f, 360f);
    }

    public static void OpenTilesOnly(string preferredPackName, int preferredSeed)
    {
        var window = GetWindow<AntPackGeneratorWindow>("Generate Ant Pack");
        window.packName = preferredPackName;
        window.seed = preferredSeed;
        window.generateTiles = true;
        window.generateAnts = false;
        window.generateProps = false;
        window.UpdateOutputFolderFromPackName();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Ant Content Pack", EditorStyles.boldLabel);
        packName = EditorGUILayout.TextField("Pack Name", packName);
        seed = EditorGUILayout.IntField("Seed", seed);
        tileSize = EditorGUILayout.IntField("Tile Size", tileSize);
        antSpriteSize = EditorGUILayout.IntField("Ant Sprite Size", antSpriteSize);
        antSpriteSize = Mathf.Max(32, antSpriteSize);
        antSpriteSize = Mathf.RoundToInt(antSpriteSize / 8f) * 8;
        palette = (AntPalettePreset)EditorGUILayout.EnumPopup("Palette Preset", palette);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("Default", GUILayout.Width(70f)))
            {
                UpdateOutputFolderFromPackName();
            }
        }

        EditorGUILayout.Space(4f);
        generateTiles = EditorGUILayout.ToggleLeft("Generate Tiles", generateTiles);
        generateAnts = EditorGUILayout.ToggleLeft("Generate Ants", generateAnts);
        generateProps = EditorGUILayout.ToggleLeft("Generate Props", generateProps);
        overwrite = EditorGUILayout.ToggleLeft("Regenerate / overwrite existing assets", overwrite);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(30f)))
            {
                Generate();
            }

            if (GUILayout.Button("Preview", GUILayout.Height(30f), GUILayout.Width(100f)))
            {
                LoadPreviewTexture();
            }
        }

        if (!string.IsNullOrEmpty(summary))
        {
            EditorGUILayout.HelpBox(summary, MessageType.Info);
        }

        DrawPreview();
    }

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(outputFolder) || outputFolder == AntPackGenerator.DefaultBaseFolder)
        {
            UpdateOutputFolderFromPackName();
        }

        var request = new AntPackGenerator.Request
        {
            PackName = packName,
            Seed = seed,
            TileSize = tileSize,
            AntSpriteSize = antSpriteSize,
            Palette = palette,
            OutputFolder = outputFolder,
            GenerateTiles = generateTiles,
            GenerateAnts = generateAnts,
            GenerateProps = generateProps,
            Overwrite = overwrite
        };

        try
        {
            summary = AntPackGenerator.Generate(request);
            LoadPreviewTexture();
        }
        catch (System.Exception ex)
        {
            summary = $"Generation failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void UpdateOutputFolderFromPackName()
    {
        var safe = string.IsNullOrWhiteSpace(packName) ? "AntPack" : packName.Trim();
        outputFolder = $"{AntPackGenerator.DefaultBaseFolder}/{safe}";
    }

    private void LoadPreviewTexture()
    {
        var path = $"{outputFolder}/ants.png";
        antsPreviewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        summary = antsPreviewTexture == null ? $"{summary}\nNo ants.png found at {path}" : summary;
        Repaint();
    }

    private void DrawPreview()
    {
        if (antsPreviewTexture == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Ants Preview (worker, worker_mask, soldier, soldier_mask)", EditorStyles.boldLabel);
        DrawSheetRow(1f);

        previewZoom = EditorGUILayout.Slider("Zoom", previewZoom, 2f, 4f);
        DrawSheetRow(previewZoom);
    }

    private void DrawSheetRow(float zoom)
    {
        var cellSize = antsPreviewTexture.width / 4f;
        var drawSize = cellSize * zoom;
        var uvWidth = 0.25f;
        var labels = new[] { "Worker", "Worker Mask", "Soldier", "Soldier Mask" };

        using (new EditorGUILayout.HorizontalScope())
        {
            for (var i = 0; i < 4; i++)
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(drawSize + 8f)))
                {
                    var rect = GUILayoutUtility.GetRect(drawSize, drawSize, GUILayout.Width(drawSize), GUILayout.Height(drawSize));
                    var uv = new Rect(i * uvWidth, 0f, uvWidth, 1f);
                    GUI.DrawTextureWithTexCoords(rect, antsPreviewTexture, uv, true);
                    EditorGUILayout.LabelField(labels[i], EditorStyles.miniLabel, GUILayout.Width(drawSize));
                }
            }
        }
    }
}
