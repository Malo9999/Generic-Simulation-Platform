#if UNITY_EDITOR && GSP_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ArtGenPipelineInspectorWindow : EditorWindow
{
    private const int PreviewSize = 64;

    private sealed class FrameEntry
    {
        public string spriteId;
        public string prefix;
        public int frame;
        public string assetPath;
        public DateTime modifiedUtc;
        public PixelBlueprint2D blueprint;
        public Texture2D compositePreview;
        public int pixelDiffFromPrevious;
        public bool hasPrevious;
        public bool identicalToPrevious;
    }

    private PackRecipe recipe;
    private ContentPack contentPack;
    private string outputFolderOverride = "Assets/Presentation/Packs/Ants/AntPack";
    private string generatedRoot = string.Empty;
    private readonly List<string> spriteIds = new();
    private readonly Dictionary<string, FrameEntry> frameBySpriteId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> layerPreviewTextures = new(StringComparer.OrdinalIgnoreCase);
    private string selectedSpriteId = string.Empty;
    private Vector2 spriteListScroll;
    private Vector2 previewScroll;
    private string status = "Load a recipe or output folder, then click Refresh.";

    [MenuItem("GSP/Tooling/Art/ArtGen Pipeline Inspector")]
    public static void OpenWindow()
    {
        var window = GetWindow<ArtGenPipelineInspectorWindow>("ArtGen Pipeline Inspector");
        window.minSize = new Vector2(980f, 560f);
    }

    private void OnDisable()
    {
        ClearPreviewCache();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSpriteIdList();
            DrawSelectedSpriteDetails();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.LabelField("Inspect generated blueprint frames", EditorStyles.boldLabel);
        recipe = (PackRecipe)EditorGUILayout.ObjectField("Pack Recipe", recipe, typeof(PackRecipe), false);
        contentPack = (ContentPack)EditorGUILayout.ObjectField("Content Pack (optional)", contentPack, typeof(ContentPack), false);

        outputFolderOverride = EditorGUILayout.TextField("Output Folder", GetActiveOutputFolder());

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Recipe Output", GUILayout.Width(140f)))
            {
                if (recipe != null)
                {
                    outputFolderOverride = recipe.outputFolder;
                }
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
            {
                RefreshData();
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            EditorGUILayout.HelpBox(status, MessageType.Info);
        }
    }

    private void DrawSpriteIdList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(420f)))
        {
            EditorGUILayout.LabelField($"Sprite IDs ({spriteIds.Count})", EditorStyles.boldLabel);
            spriteListScroll = EditorGUILayout.BeginScrollView(spriteListScroll, GUILayout.ExpandHeight(true));
            foreach (var id in spriteIds)
            {
                var exists = frameBySpriteId.ContainsKey(id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(string.Equals(selectedSpriteId, id, StringComparison.Ordinal), id, "Button"))
                    {
                        if (!string.Equals(selectedSpriteId, id, StringComparison.Ordinal))
                        {
                            selectedSpriteId = id;
                            BuildSelectedPreviews();
                        }
                    }

                    var badge = exists ? "BP" : "—";
                    GUILayout.Label(badge, EditorStyles.miniLabel, GUILayout.Width(22f));
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSelectedSpriteDetails()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            if (string.IsNullOrWhiteSpace(selectedSpriteId))
            {
                EditorGUILayout.HelpBox("Select a spriteId to inspect its generated blueprint and previews.", MessageType.None);
                return;
            }

            EditorGUILayout.SelectableLabel(selectedSpriteId, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (!frameBySpriteId.TryGetValue(selectedSpriteId, out var entry) || entry.blueprint == null)
            {
                EditorGUILayout.HelpBox("No generated blueprint was found for this spriteId. Try scanning a different output folder.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Asset: {entry.assetPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Modified UTC: {entry.modifiedUtc:u}", EditorStyles.miniLabel);
            if (entry.hasPrevious)
            {
                var msg = $"Pixel diff vs previous frame: {entry.pixelDiffFromPrevious}";
                if (entry.identicalToPrevious)
                {
                    EditorGUILayout.HelpBox(msg + " (IDENTICAL to previous frame)", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(msg, MessageType.Info);
                }
            }

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.ExpandHeight(true));
            DrawLayerPreviews(entry);
            DrawCompositePreview(entry);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawLayerPreviews(FrameEntry entry)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Layer previews (BlueprintRasterizer @64x64)", EditorStyles.boldLabel);

        if (entry.blueprint.layers == null || entry.blueprint.layers.Count == 0)
        {
            EditorGUILayout.HelpBox("Blueprint has no layers.", MessageType.Warning);
            return;
        }

        foreach (var layer in entry.blueprint.layers.Where(l => l != null && !string.IsNullOrWhiteSpace(l.name)))
        {
            var key = BuildLayerKey(entry.spriteId, layer.name);
            if (!layerPreviewTextures.TryGetValue(key, out var tex) || tex == null)
            {
                tex = RenderLayer(entry.blueprint, layer.name);
                layerPreviewTextures[key] = tex;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(layer.name, GUILayout.Width(120f));
                var rect = GUILayoutUtility.GetRect(PreviewSize * 2f, PreviewSize * 2f, GUILayout.Width(PreviewSize * 2f), GUILayout.Height(PreviewSize * 2f));
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }
        }
    }

    private void DrawCompositePreview(FrameEntry entry)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Composited preview (body + stripe)", EditorStyles.boldLabel);

        if (entry.compositePreview == null)
        {
            entry.compositePreview = RenderComposite(entry.spriteId, entry.blueprint);
        }

        var rect = GUILayoutUtility.GetRect(PreviewSize * 3f, PreviewSize * 3f, GUILayout.Width(PreviewSize * 3f), GUILayout.Height(PreviewSize * 3f));
        GUI.DrawTexture(rect, entry.compositePreview, ScaleMode.ScaleToFit, true);
    }

    private void RefreshData()
    {
        ClearPreviewCache();
        spriteIds.Clear();
        frameBySpriteId.Clear();
        selectedSpriteId = string.Empty;

        var outputFolder = GetActiveOutputFolder();
        generatedRoot = string.IsNullOrWhiteSpace(outputFolder)
            ? string.Empty
            : $"{outputFolder.TrimEnd('/')}/Blueprints/Generated";

        var discovered = new HashSet<string>(StringComparer.Ordinal);
        if (contentPack != null)
        {
            foreach (var id in contentPack.GetAllSpriteIds())
            {
                if (string.IsNullOrWhiteSpace(id) || id.EndsWith("_mask", StringComparison.Ordinal))
                {
                    continue;
                }

                discovered.Add(id);
            }
        }

        if (AssetDatabase.IsValidFolder(generatedRoot))
        {
            var guids = AssetDatabase.FindAssets("t:PixelBlueprint2D", new[] { generatedRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!TryBuildSpriteIdFromPath(path, generatedRoot, out var spriteId, out var frameIndex, out var prefix))
                {
                    continue;
                }

                var bp = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(path);
                if (bp == null)
                {
                    continue;
                }

                discovered.Add(spriteId);
                var candidate = new FrameEntry
                {
                    spriteId = spriteId,
                    prefix = prefix,
                    frame = frameIndex,
                    assetPath = path,
                    blueprint = bp,
                    modifiedUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue
                };

                if (!frameBySpriteId.TryGetValue(spriteId, out var existing) || candidate.modifiedUtc >= existing.modifiedUtc)
                {
                    frameBySpriteId[spriteId] = candidate;
                }
            }
        }

        spriteIds.AddRange(discovered.OrderBy(s => s, StringComparer.Ordinal));
        ComputeFrameDiffWarnings();

        status = $"Loaded {frameBySpriteId.Count} generated blueprints from '{generatedRoot}'. Sprite IDs listed: {spriteIds.Count}.";
        if (spriteIds.Count > 0)
        {
            selectedSpriteId = spriteIds[0];
            BuildSelectedPreviews();
        }
        Repaint();
    }

    private void BuildSelectedPreviews()
    {
        previewScroll = Vector2.zero;
        if (!frameBySpriteId.TryGetValue(selectedSpriteId, out var entry) || entry.blueprint == null)
        {
            return;
        }

        foreach (var layer in entry.blueprint.layers.Where(l => l != null && !string.IsNullOrWhiteSpace(l.name)))
        {
            var key = BuildLayerKey(entry.spriteId, layer.name);
            if (!layerPreviewTextures.ContainsKey(key))
            {
                layerPreviewTextures[key] = RenderLayer(entry.blueprint, layer.name);
            }
        }

        entry.compositePreview ??= RenderComposite(entry.spriteId, entry.blueprint);
    }

    private void ComputeFrameDiffWarnings()
    {
        var byPrefix = frameBySpriteId.Values
            .Where(v => v != null && !string.IsNullOrWhiteSpace(v.prefix))
            .GroupBy(v => v.prefix, StringComparer.Ordinal);

        foreach (var group in byPrefix)
        {
            var ordered = group.OrderBy(v => v.frame).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].hasPrevious = i > 0;
                ordered[i].pixelDiffFromPrevious = 0;
                ordered[i].identicalToPrevious = false;
                if (i == 0)
                {
                    continue;
                }

                var prevPixels = GetCompositePixels(ordered[i - 1]);
                var currPixels = GetCompositePixels(ordered[i]);
                var diff = ComputePixelDiff(prevPixels, currPixels);
                ordered[i].pixelDiffFromPrevious = diff;
                ordered[i].identicalToPrevious = diff == 0;
            }
        }
    }

    private static int ComputePixelDiff(Color32[] a, Color32[] b)
    {
        if (a == null || b == null)
        {
            return int.MaxValue;
        }

        var length = Mathf.Min(a.Length, b.Length);
        var diff = 0;
        for (var i = 0; i < length; i++)
        {
            if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a)
            {
                diff++;
            }
        }

        diff += Math.Abs(a.Length - b.Length);
        return diff;
    }

    private static Color32[] GetCompositePixels(FrameEntry entry)
    {
        entry.compositePreview ??= RenderComposite(entry.spriteId, entry.blueprint);
        return entry.compositePreview != null ? entry.compositePreview.GetPixels32() : null;
    }

    private static Texture2D RenderLayer(PixelBlueprint2D blueprint, string layerName)
    {
        var pixels = new Color32[PreviewSize * PreviewSize];
        var layerColor = ResolveLayerColor(layerName);
        BlueprintRasterizer.Render(blueprint, layerName, PreviewSize, 0, 0, layerColor, pixels, PreviewSize);

        var tex = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D RenderComposite(string spriteId, PixelBlueprint2D blueprint)
    {
        var pixels = new Color32[PreviewSize * PreviewSize];
        var bodyColor = new Color32(56, 44, 31, 255);

        if (TryParseAntSpeciesFromSpriteId(spriteId, out var speciesId))
        {
            bodyColor = ReferenceColorSampler.SampleOrFallback("", speciesId, bodyColor);
            RenderAntComposite(blueprint, PreviewSize, pixels, bodyColor);
        }
        else
        {
            BlueprintRasterizer.Render(blueprint, "body", PreviewSize, 0, 0, bodyColor, pixels, PreviewSize);
            BlueprintRasterizer.Render(blueprint, "stripe", PreviewSize, 0, 0, new Color32(245, 238, 210, 255), pixels, PreviewSize);
        }

        var tex = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static void RenderAntComposite(PixelBlueprint2D blueprint, int size, Color32[] outPixels, Color32 bodyColor)
    {
        var outline = new Color32(20, 16, 12, 255);
        var bodyRamp = BuildToneRamp(bodyColor, outline);
        var legsRamp = BuildToneRamp(ScaleColor(bodyColor, 0.82f), outline);
        var mandibleRamp = BuildToneRamp(ScaleColor(bodyColor, 0.78f), outline);
        var eyeRamp = new BlueprintRasterizer.ToneRamp(new Color32(232, 220, 156, 255), new Color32(171, 149, 92, 255), new Color32(255, 236, 176, 255), outline);

        BlueprintRasterizer.RenderLayers(
            blueprint,
            size,
            0,
            0,
            outPixels,
            size,
            new BlueprintRasterizer.LayerStyle("body", bodyRamp, true),
            new BlueprintRasterizer.LayerStyle("legs", legsRamp, false),
            new BlueprintRasterizer.LayerStyle("antennae", legsRamp, false),
            new BlueprintRasterizer.LayerStyle("mandibles", mandibleRamp, false),
            new BlueprintRasterizer.LayerStyle("eyes", eyeRamp, false));
        BlueprintRasterizer.Render(blueprint, "stripe", size, 0, 0, new Color32(245, 238, 210, 255), outPixels, size);
    }

    private static BlueprintRasterizer.ToneRamp BuildToneRamp(Color32 baseColor, Color32 outline)
    {
        return new BlueprintRasterizer.ToneRamp(baseColor, ScaleColor(baseColor, 0.72f), ScaleColor(baseColor, 1.18f), outline);
    }

    private static Color32 ScaleColor(Color32 color, float factor)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255),
            255);
    }

    private static bool TryParseAntSpeciesFromSpriteId(string spriteId, out string speciesId)
    {
        speciesId = string.Empty;
        if (string.IsNullOrWhiteSpace(spriteId))
        {
            return false;
        }

        var tokens = spriteId.Split(':');
        if (tokens.Length < 4 || !string.Equals(tokens[0], "agent", StringComparison.OrdinalIgnoreCase) || !string.Equals(tokens[1], "ant", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        speciesId = tokens[2];
        return !string.IsNullOrWhiteSpace(speciesId);
    }

    private static Color32 ResolveLayerColor(string layerName)
    {
        if (string.Equals(layerName, "body", StringComparison.OrdinalIgnoreCase)) return new Color32(119, 90, 62, 255);
        if (string.Equals(layerName, "stripe", StringComparison.OrdinalIgnoreCase)) return new Color32(245, 238, 210, 255);
        if (string.Equals(layerName, "eyes", StringComparison.OrdinalIgnoreCase)) return new Color32(255, 229, 156, 255);
        return new Color32(168, 196, 222, 255);
    }

    private static string BuildLayerKey(string spriteId, string layerName) => spriteId + "::" + layerName;

    private string GetActiveOutputFolder()
    {
        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.outputFolder))
        {
            return recipe.outputFolder;
        }

        return outputFolderOverride;
    }

    private static bool TryBuildSpriteIdFromPath(string assetPath, string rootPath, out string spriteId, out int frameIndex, out string prefix)
    {
        spriteId = string.Empty;
        frameIndex = -1;
        prefix = string.Empty;

        var normalizedPath = assetPath.Replace('\\', '/');
        var normalizedRoot = rootPath.Replace('\\', '/').TrimEnd('/');
        if (!normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = normalizedPath.Substring(normalizedRoot.Length + 1);
        var parts = relative.Split('/');
        if (parts.Length < 6)
        {
            return false;
        }

        var entity = parts[0];
        var species = parts[1];
        var role = parts[2];
        var stage = parts[3];
        var state = parts[4];
        var frameToken = Path.GetFileNameWithoutExtension(parts[5]);
        if (!int.TryParse(frameToken, out frameIndex))
        {
            return false;
        }

        prefix = $"agent:{entity}:{species}:{role}:{stage}:{state}";
        spriteId = $"{prefix}:{frameIndex:00}";
        return true;
    }

    private void ClearPreviewCache()
    {
        foreach (var tex in layerPreviewTextures.Values)
        {
            if (tex != null)
            {
                DestroyImmediate(tex);
            }
        }

        layerPreviewTextures.Clear();
        foreach (var frame in frameBySpriteId.Values)
        {
            if (frame?.compositePreview != null)
            {
                DestroyImmediate(frame.compositePreview);
            }
        }
    }
}
#endif
