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
    private bool generateLegacyTilesets;
    private bool overwrite;
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
        generateLegacyTilesets = EditorGUILayout.ToggleLeft("Also generate legacy ant tilesets (surface + underground) into Assets/Generated/Tilesets/Ants", generateLegacyTilesets);
        overwrite = EditorGUILayout.ToggleLeft("Regenerate / overwrite existing assets", overwrite);

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Generate", GUILayout.Height(30f)))
        {
            Generate();
        }

        if (!string.IsNullOrEmpty(summary))
        {
            EditorGUILayout.HelpBox(summary, MessageType.Info);
        }
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
            GenerateLegacyTilesets = generateLegacyTilesets,
            Overwrite = overwrite
        };

        try
        {
            summary = AntPackGenerator.Generate(request);
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
}
