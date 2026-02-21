using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class GenerateAntReferencePacksMenu : EditorWindow
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Ant/AntSpeciesLibrary.asset";
    private static readonly string[] StateOrder = { "idle", "walk", "run", "fight", "hurt", "death" };

    private string outputRoot = "Assets/Presentation/ReferencePacks/Ants";
    private int spriteSize = 64;
    private bool overwrite;
    private readonly Dictionary<string, bool> enabledStates = new();

    [MenuItem("Tools/Generic Simulation Platform/Reference Packs/Generate Ant Reference Packs…")]
    public static void Open()
    {
        var window = GetWindow<GenerateAntReferencePacksMenu>("Generate Ant Reference Packs");
        window.minSize = new Vector2(520f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        foreach (var state in StateOrder)
        {
            if (!enabledStates.ContainsKey(state))
            {
                enabledStates[state] = true;
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Ant Reference Pack Generator", EditorStyles.boldLabel);
        outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
        spriteSize = Mathf.Max(16, EditorGUILayout.IntField("Sprite Size", spriteSize));
        overwrite = EditorGUILayout.ToggleLeft("Overwrite existing outputs", overwrite);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("States To Export", EditorStyles.boldLabel);
        foreach (var state in StateOrder)
        {
            enabledStates[state] = EditorGUILayout.ToggleLeft(state, enabledStates[state]);
        }

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("Generate", GUILayout.Height(34f)))
        {
            Generate();
        }
    }

    private void Generate()
    {
        if (!outputRoot.StartsWith("Assets/", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Invalid Output Root", "Output Root must be under Assets/.", "OK");
            return;
        }

        var module = new AntArchetypeModule();
        module.EnsureLibrariesExist();
        var library = AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath);
        if (library == null)
        {
            EditorUtility.DisplayDialog("Missing Ant Species Library", $"Could not load {LibraryPath}.", "OK");
            return;
        }

        var selectedStates = StateOrder.Where(state => enabledStates.TryGetValue(state, out var isOn) && isOn).ToList();
        if (selectedStates.Count == 0)
        {
            EditorUtility.DisplayDialog("No States Selected", "Select at least one state to export.", "OK");
            return;
        }

        ImportSettingsUtil.EnsureFolder(outputRoot);

        var generatedCount = 0;
        foreach (var profile in library.profiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.speciesId))
            {
                continue;
            }

            var speciesFolder = $"{outputRoot}/{profile.speciesId}";
            ImportSettingsUtil.EnsureFolder(speciesFolder);

            var sheets = new List<ReferencePack2D.StateSheet>();
            foreach (var stateId in selectedStates)
            {
                var frameCount = GetFrameCount(stateId);
                var fps = GetFps(stateId);
                var texturePath = $"{speciesFolder}/refs_{stateId}.png";

                if (overwrite || !File.Exists(texturePath))
                {
                    var texture = BuildStateSheetTexture(profile, stateId, frameCount);
                    File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                    DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                }

                ConfigureStateTexture(texturePath, frameCount, spriteSize);
                var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                sheets.Add(new ReferencePack2D.StateSheet
                {
                    stateId = stateId,
                    texture = importedTexture,
                    frameCount = frameCount,
                    fps = fps
                });
            }

            var packPath = $"{speciesFolder}/ReferencePack2D.asset";
            var pack = AssetDatabase.LoadAssetAtPath<ReferencePack2D>(packPath);
            if (pack == null)
            {
                pack = CreateInstance<ReferencePack2D>();
                AssetDatabase.CreateAsset(pack, packPath);
            }

            pack.packKind = "ant";
            pack.speciesId = profile.speciesId;
            pack.displayName = string.IsNullOrWhiteSpace(profile.displayName) ? profile.speciesId : profile.displayName;
            pack.sheets = sheets;
            pack.notes = "Generated from AntBlueprintSynthesizer (deterministic, original pixel art).";
            pack.version = "v1";
            EditorUtility.SetDirty(pack);

            profile.referencePack = pack;
            generatedCount++;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Generated {generatedCount} ant reference packs under {outputRoot}.");
        EditorUtility.DisplayDialog("Reference Packs Generated", $"Generated {generatedCount} ant reference pack(s).", "OK");
    }

    private Texture2D BuildStateSheetTexture(AntSpeciesProfile profile, string stateId, int frameCount)
    {
        var width = spriteSize * frameCount;
        var height = spriteSize;
        var pixels = new Color32[width * height];

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var request = new ArchetypeSynthesisRequest
            {
                speciesId = profile.speciesId,
                role = "worker",
                stage = "adult",
                state = stateId,
                frameIndex = frameIndex,
                blueprintPath = null
            };

            var (body, _) = AntBlueprintSynthesizer.Build(profile, request);
            var bodyColor = ResolveBodyColor(profile.baseColorId);
            BlueprintRasterizer.Render(body, "body", spriteSize, frameIndex * spriteSize, 0, bodyColor, pixels, width);
            BlueprintRasterizer.Render(body, "stripe", spriteSize, frameIndex * spriteSize, 0, ResolveStripeColor(bodyColor), pixels, width);
            DestroyImmediate(body);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void ConfigureStateTexture(string texturePath, int frameCount, int spriteSize)
    {
        var rects = new List<SpriteRect>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            rects.Add(new SpriteRect
            {
                name = $"frame_{i:00}",
                rect = new Rect(i * spriteSize, 0, spriteSize, spriteSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        ImportSettingsUtil.ConfigureAsPixelArtMultiple(texturePath, spriteSize, rects);
    }

    private static int GetFrameCount(string stateId)
    {
        return stateId switch
        {
            "idle" => 2,
            "walk" => 4,
            "run" => 4,
            _ => 1
        };
    }

    private static int GetFps(string stateId)
    {
        return stateId switch
        {
            "idle" => 3,
            "walk" => 8,
            "run" => 12,
            _ => 0
        };
    }

    private static Color32 ResolveBodyColor(string baseColorId)
    {
        return baseColorId switch
        {
            "red" => new Color32(156, 67, 52, 255),
            "brown" => new Color32(101, 71, 45, 255),
            "yellow" => new Color32(164, 140, 76, 255),
            "black" => new Color32(58, 58, 61, 255),
            _ => new Color32(82, 67, 55, 255)
        };
    }

    private static Color32 ResolveStripeColor(Color32 bodyColor)
    {
        return new Color32(
            (byte)Mathf.Clamp(bodyColor.r + 28, 0, 255),
            (byte)Mathf.Clamp(bodyColor.g + 24, 0, 255),
            (byte)Mathf.Clamp(bodyColor.b + 20, 0, 255),
            255);
    }
}
