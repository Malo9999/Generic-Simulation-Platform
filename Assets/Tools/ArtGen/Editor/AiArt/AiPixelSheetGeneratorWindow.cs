using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class AiPixelSheetGeneratorWindow : EditorWindow
{
    private static readonly string[] Models = { "gpt-image-1", "gpt-image-1.5" };
    private static readonly string[] Roles = { "worker", "soldier", "queen" };

    private SimulationStyleSpec styleSpec;
    private DefaultAsset packFolderAsset;
    private string assetId = "FireAnt";
    private int roleIndex;
    private int modelIndex;
    private int variants = 1;
    private bool strictPixel = true;
    private string apiKeyDraft = string.Empty;
    private int selectedVariantIndex;
    private string status = "Ready.";

    [MenuItem("Tools/Generic Simulation Platform/Art/AI/Generate Pixel Sheet…")]
    public static void OpenWindow()
    {
        var window = GetWindow<AiPixelSheetGeneratorWindow>("AI Pixel Sheet Generator");
        window.minSize = new Vector2(620f, 380f);
    }

    private void OnEnable()
    {
        apiKeyDraft = OpenAIImageClient.GetApiKey();
        if (packFolderAsset == null && styleSpec != null)
        {
            packFolderAsset = FindLatestPackFolder(styleSpec.simulationId);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AI Pixel Sheet Generator", EditorStyles.boldLabel);
        styleSpec = (SimulationStyleSpec)EditorGUILayout.ObjectField("Style Spec", styleSpec, typeof(SimulationStyleSpec), false);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Simulation Id", styleSpec == null ? string.Empty : styleSpec.simulationId);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            packFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Pack Folder", packFolderAsset, typeof(DefaultAsset), false);
            if (GUILayout.Button("Auto", GUILayout.Width(70f)))
            {
                packFolderAsset = FindLatestPackFolder(styleSpec == null ? string.Empty : styleSpec.simulationId);
            }
        }

        assetId = EditorGUILayout.TextField("Asset Id", assetId);
        roleIndex = EditorGUILayout.Popup("Role", Mathf.Clamp(roleIndex, 0, Roles.Length - 1), Roles);
        modelIndex = EditorGUILayout.Popup("Model", Mathf.Clamp(modelIndex, 0, Models.Length - 1), Models);
        variants = EditorGUILayout.IntSlider("Variants", variants, 1, 3);
        strictPixel = EditorGUILayout.ToggleLeft("Strict pixel (no AA, crisp edges, 1px outline)", strictPixel);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("API Key (env OPENAI_API_KEY overrides EditorPrefs)", EditorStyles.miniBoldLabel);
        apiKeyDraft = EditorGUILayout.PasswordField("EditorPrefs Key", apiKeyDraft);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Key To EditorPrefs"))
            {
                OpenAIImageClient.SetEditorPrefsApiKey(apiKeyDraft);
                status = "Saved API key to EditorPrefs.";
            }

            if (GUILayout.Button("Clear Saved Key", GUILayout.Width(140f)))
            {
                apiKeyDraft = string.Empty;
                OpenAIImageClient.SetEditorPrefsApiKey(string.Empty);
                status = "Cleared saved API key from EditorPrefs.";
            }
        }

        selectedVariantIndex = EditorGUILayout.IntSlider("Selected Variant", selectedVariantIndex + 1, 1, variants) - 1;

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Base Sheet", GUILayout.Height(28f)))
            {
                _ = GenerateAsync(GenerateMode.BaseOnly);
            }

            if (GUILayout.Button("Generate Mask Sheet", GUILayout.Height(28f)))
            {
                _ = GenerateAsync(GenerateMode.MaskOnly);
            }

            if (GUILayout.Button("Generate Both", GUILayout.Height(28f)))
            {
                _ = GenerateAsync(GenerateMode.Both);
            }

            if (GUILayout.Button("Slice & Preview", GUILayout.Height(28f)))
            {
                SliceAndPreview();
            }
        }

        EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private async Task GenerateAsync(GenerateMode mode)
    {
        if (!ValidateInput())
        {
            return;
        }

        var outputFolder = GetReferenceOutputFolder();
        ImportSettingsUtil.EnsureFolder(outputFolder);

        try
        {
            status = "Generating with OpenAI...";
            Repaint();

            if (mode is GenerateMode.BaseOnly or GenerateMode.Both)
            {
                await GenerateAndWrite(outputFolder, "base", BuildBasePrompt());
            }

            if (mode is GenerateMode.MaskOnly or GenerateMode.Both)
            {
                await GenerateAndWrite(outputFolder, "mask", BuildMaskPrompt());
            }

            AssetDatabase.Refresh();
            SliceAndPreview();
            status = $"Generated {mode} sheets for {assetId}/{Roles[roleIndex]}.";
        }
        catch (Exception ex)
        {
            status = $"Generation failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task GenerateAndWrite(string outputFolder, string kind, string prompt)
    {
        var model = Models[Mathf.Clamp(modelIndex, 0, Models.Length - 1)];
        var generatedPaths = new List<string>();

        for (var i = 0; i < variants; i++)
        {
            status = $"Generating {kind} variant {i + 1}/{variants}...";
            Repaint();

            var pngBytes = await OpenAIImageClient.GeneratePngBase64(prompt, model, "1024x1024", "transparent", "png");
            var variantPath = $"{outputFolder}/{kind}_v{i + 1:00}.png";
            File.WriteAllBytes(variantPath, pngBytes);
            generatedPaths.Add(variantPath);
            AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceSynchronousImport);
        }

        var selected = generatedPaths[Mathf.Clamp(selectedVariantIndex, 0, generatedPaths.Count - 1)];
        var canonicalPath = $"{outputFolder}/{kind}.png";
        File.Copy(selected, canonicalPath, true);
        AssetDatabase.ImportAsset(canonicalPath, ImportAssetOptions.ForceSynchronousImport);

        CopyIntoPackFolderIfConfigured(canonicalPath, kind);
    }

    private void SliceAndPreview()
    {
        if (!ValidateInput(requireStyleOnly: true))
        {
            return;
        }

        var outputFolder = GetReferenceOutputFolder();
        var basePath = $"{outputFolder}/base.png";
        var maskPath = $"{outputFolder}/mask.png";

        var idsToSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        idsToSprites = SliceSheet(basePath, false, idsToSprites);
        idsToSprites = SliceSheet(maskPath, true, idsToSprites);

        TryRegisterIntoContentPack(idsToSprites);
        AssetDatabase.SaveAssets();
        status = $"Sliced {idsToSprites.Count} sprites and refreshed content pack entries.";
    }

    private Dictionary<string, Sprite> SliceSheet(string texturePath, bool isMask, Dictionary<string, Sprite> idsToSprites)
    {
        if (!File.Exists(texturePath))
        {
            return idsToSprites;
        }

        var rects = BuildSpriteRects(isMask);
        var sprites = ImportSettingsUtil.ConfigureAsPixelArtMultiple(texturePath, styleSpec.frameSize, rects);
        foreach (var sprite in sprites)
        {
            idsToSprites[sprite.name] = sprite;
        }

        return idsToSprites;
    }

    private List<SpriteRect> BuildSpriteRects(bool isMask)
    {
        var rects = new List<SpriteRect>();
        var totalFrames = styleSpec.sheetCols * styleSpec.sheetRows;
        for (var frame = 0; frame < totalFrames; frame++)
        {
            var col = frame % styleSpec.sheetCols;
            var rowTop = frame / styleSpec.sheetCols;
            var x = styleSpec.paddingPx + (col * (styleSpec.frameSize + styleSpec.paddingPx));
            var yFromTop = styleSpec.paddingPx + (rowTop * (styleSpec.frameSize + styleSpec.paddingPx));

            // SpriteRect uses bottom-left origin.
            var y = 1024 - yFromTop - styleSpec.frameSize;
            var id = BuildSpriteId(frame, isMask);
            rects.Add(new SpriteRect
            {
                name = id,
                rect = new Rect(x, y, styleSpec.frameSize, styleSpec.frameSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        return rects;
    }

    private string BuildSpriteId(int frame, bool isMask)
    {
        var role = Roles[Mathf.Clamp(roleIndex, 0, Roles.Length - 1)];
        var (state, localFrame) = MapFrame(frame);
        var suffix = isMask ? "_mask" : string.Empty;
        return $"agent:ant:{assetId}:{role}:adult:{state}:{localFrame}{suffix}";
    }

    private static (string state, int frame) MapFrame(int frame)
    {
        return frame switch
        {
            0 or 1 => ("idle", frame),
            2 or 3 or 4 => ("walk", frame - 2),
            5 or 6 or 7 or 8 => ("run", frame - 5),
            9 => ("fight", 0),
            _ => ("idle", frame)
        };
    }

    private string BuildBasePrompt()
    {
        return BuildPromptCommon(false);
    }

    private string BuildMaskPrompt()
    {
        return BuildPromptCommon(true);
    }

    private string BuildPromptCommon(bool mask)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(styleSpec.promptPrefix))
        {
            sb.AppendLine(styleSpec.promptPrefix.Trim());
        }

        sb.AppendLine($"Style ID: {styleSpec.styleId}. Keep style fully consistent with this simulation.");
        sb.AppendLine($"Sprite sheet: {styleSpec.sheetCols}x{styleSpec.sheetRows} grid, exactly 10 frames total.");
        sb.AppendLine("Use this exact layout contract: frame 0-1 idle, 2-4 walk, 5-8 run, 9 fight.");
        sb.AppendLine($"Each frame is {styleSpec.frameSize}x{styleSpec.frameSize} pixels with {styleSpec.paddingPx}px padding.");
        sb.AppendLine("Canvas target 1024x1024 transparent PNG.");
        sb.AppendLine($"Subject: ant {Roles[Mathf.Clamp(roleIndex, 0, Roles.Length - 1)]} named {assetId}.");
        sb.AppendLine($"Shading rule: {styleSpec.shadingRule}.");

        if (styleSpec.paletteHex != null && styleSpec.paletteHex.Length > 0)
        {
            sb.AppendLine($"Palette lock (hex): {string.Join(", ", styleSpec.paletteHex.Where(x => !string.IsNullOrWhiteSpace(x)))}.");
        }

        if (!string.IsNullOrWhiteSpace(styleSpec.outlineHex))
        {
            sb.AppendLine($"Outline color lock: {styleSpec.outlineHex}.");
        }

        if (strictPixel)
        {
            sb.AppendLine("Strict pixel art constraints: no anti-aliasing, no blur, crisp pixels, clean 1px outline, no subpixel rendering.");
        }

        if (mask)
        {
            sb.AppendLine("Mask output only: white silhouette for target markings on transparent background. No color, no shading, no gradients.");
        }
        else
        {
            sb.AppendLine("Base output only: fully rendered ant sprite frames with clear difference between idle, walk, run, and fight poses.");
        }

        sb.AppendLine("Keep the ant centered and fully inside each frame.");
        return sb.ToString();
    }

    private void CopyIntoPackFolderIfConfigured(string sourceAssetPath, string kind)
    {
        var packFolderPath = GetPackFolderPath();
        if (string.IsNullOrWhiteSpace(packFolderPath))
        {
            return;
        }

        var packId = Path.GetFileName(packFolderPath.TrimEnd('/'));
        var role = Roles[Mathf.Clamp(roleIndex, 0, Roles.Length - 1)];
        var targetFolder = $"{packFolderPath}/Generated";
        ImportSettingsUtil.EnsureFolder(targetFolder);
        var targetPath = $"{targetFolder}/ai_{assetId}_{role}_{kind}.png";

        File.Copy(sourceAssetPath, targetPath, true);
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        ApplyPackTextureImportSettings(sourceAssetPath, targetPath);

        status = $"Copied {kind}.png into pack {packId}.";
    }

    private void ApplyPackTextureImportSettings(string sourceAssetPath, string packAssetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath) == null)
        {
            return;
        }

        var sourceSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(sourceAssetPath).OfType<Sprite>().ToList();
        if (sourceSprites.Count == 0)
        {
            return;
        }

        var rects = sourceSprites.Select(sprite => new SpriteRect
        {
            name = sprite.name,
            rect = sprite.rect,
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        }).ToList();

        ImportSettingsUtil.ConfigureAsPixelArtMultiple(packAssetPath, styleSpec.frameSize, rects);
    }

    private void TryRegisterIntoContentPack(Dictionary<string, Sprite> spriteById)
    {
        var packFolderPath = GetPackFolderPath();
        if (string.IsNullOrWhiteSpace(packFolderPath))
        {
            return;
        }

        var contentPackPath = $"{packFolderPath}/ContentPack.asset";
        var contentPack = AssetDatabase.LoadAssetAtPath<ContentPack>(contentPackPath);
        if (contentPack == null)
        {
            return;
        }

        var textures = contentPack.Textures == null ? new List<ContentPack.TextureEntry>() : contentPack.Textures.ToList();
        var sprites = contentPack.Sprites == null ? new List<ContentPack.SpriteEntry>() : contentPack.Sprites.ToList();

        foreach (var kv in spriteById)
        {
            var idx = sprites.FindIndex(x => x.id == kv.Key);
            var entry = new ContentPack.SpriteEntry
            {
                id = kv.Key,
                category = "agent",
                sprite = kv.Value
            };

            if (idx >= 0)
            {
                sprites[idx] = entry;
            }
            else
            {
                sprites.Add(entry);
            }
        }

        contentPack.SetEntries(textures, sprites);
        EditorUtility.SetDirty(contentPack);
    }

    private string GetReferenceOutputFolder()
    {
        var role = Roles[Mathf.Clamp(roleIndex, 0, Roles.Length - 1)];
        return $"Assets/Presentation/ReferencePacks/{styleSpec.simulationId}/AI/{assetId}/{role}";
    }

    private string GetPackFolderPath()
    {
        if (packFolderAsset == null)
        {
            return string.Empty;
        }

        var path = AssetDatabase.GetAssetPath(packFolderAsset);
        return AssetDatabase.IsValidFolder(path) ? path : string.Empty;
    }

    private bool ValidateInput(bool requireStyleOnly = false)
    {
        if (styleSpec == null)
        {
            status = "Select a SimulationStyleSpec first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            status = "AssetId is required.";
            return false;
        }

        if (!requireStyleOnly && string.IsNullOrWhiteSpace(OpenAIImageClient.GetApiKey()))
        {
            status = "Missing API key. Set OPENAI_API_KEY or save one in EditorPrefs.";
            return false;
        }

        return true;
    }

    private static DefaultAsset FindLatestPackFolder(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        var parent = $"Assets/Presentation/Packs/{simulationId}";
        if (!AssetDatabase.IsValidFolder(parent))
        {
            return null;
        }

        var guids = AssetDatabase.FindAssets("t:DefaultAsset", new[] { parent });
        var candidateFolders = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.IsValidFolder(path) && path != parent)
            .Where(path => Path.GetDirectoryName(path)?.Replace('\\', '/') == parent)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .ToList();

        return candidateFolders.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(candidateFolders[0]);
    }

    private enum GenerateMode
    {
        BaseOnly,
        MaskOnly,
        Both
    }
}
