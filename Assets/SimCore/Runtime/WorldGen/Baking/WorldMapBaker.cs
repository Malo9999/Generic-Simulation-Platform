using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class WorldMapBaker
{
#if UNITY_EDITOR
    public static WorldMapAsset Bake(WorldMap map, IWorldRecipe recipe, WorldRecipeSettingsSO settings, NoiseDescriptorSet noise, string outDirAssetPath)
    {
        map.EnsureRequiredOutputs();
        EnsureFolder(outDirAssetPath);
        EnsureFolder($"{outDirAssetPath}/Fields");
        EnsureFolder($"{outDirAssetPath}/Masks");
        EnsureFolder($"{outDirAssetPath}/Splines");
        EnsureFolder($"{outDirAssetPath}/Scatter");

        var worldAssetPath = $"{outDirAssetPath}/world.asset";
        var provenancePath = $"{outDirAssetPath}/provenance.json";

        var worldAsset = AssetDatabase.LoadAssetAtPath<WorldMapAsset>(worldAssetPath);
        if (worldAsset == null)
        {
            worldAsset = ScriptableObject.CreateInstance<WorldMapAsset>();
            AssetDatabase.CreateAsset(worldAsset, worldAssetPath);
        }

        var provenance = new WorldProvenance
        {
            recipeId = map.recipeId,
            version = recipe.Version,
            mapId = map.mapId,
            seed = map.seed,
            grid = map.grid,
            settingsType = settings.GetType().AssemblyQualifiedName,
            settingsJson = EditorJsonUtility.ToJson(settings, true),
            noiseDescriptors = noise,
            bakedAtUtc = DateTime.UtcNow.ToString("o"),
            generatorGitCommit = string.Empty,
            outputs = new ProvenanceOutputs()
        };

        worldAsset.recipeId = map.recipeId;
        worldAsset.mapId = map.mapId;
        worldAsset.seed = map.seed;
        worldAsset.grid = map.grid;
        worldAsset.scalarRefs = new List<FieldRef>();
        worldAsset.maskRefs = new List<MaskRef>();
        worldAsset.splineRefs = new List<SplineRef>();
        worldAsset.scatterRefs = new List<ScatterRef>();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pair in map.scalars)
            {
                var path = $"{outDirAssetPath}/Fields/{pair.Key}.exr";
                WriteScalarExr(pair.Value, path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                worldAsset.scalarRefs.Add(new FieldRef { id = pair.Key, asset = tex, format = "exr-rfloat" });
                provenance.outputs.scalars.Add(new ProvenanceScalar { id = pair.Key, path = path, format = "exr-rfloat" });
            }

            foreach (var pair in map.masks)
            {
                var path = $"{outDirAssetPath}/Masks/{pair.Key}.png";
                WriteMaskPng(pair.Value, path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                worldAsset.maskRefs.Add(new MaskRef { id = pair.Key, asset = tex, encoding = pair.Value.encoding, categories = pair.Value.categories });
                provenance.outputs.masks.Add(new ProvenanceMask { id = pair.Key, path = path, encoding = pair.Value.encoding.ToString(), categories = pair.Value.categories });
            }

            foreach (var spline in map.splines)
            {
                var path = $"{outDirAssetPath}/Splines/{spline.id}.json";
                File.WriteAllText(path, JsonUtility.ToJson(spline, true));
                AssetDatabase.ImportAsset(path);
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                worldAsset.splineRefs.Add(new SplineRef { id = spline.id, asset = ta });
                provenance.outputs.splines.Add(new ProvenanceSpline { id = spline.id, path = path });
            }

            foreach (var scatter in map.scatters)
            {
                var path = $"{outDirAssetPath}/Scatter/{scatter.Key}.json";
                File.WriteAllText(path, JsonUtility.ToJson(scatter.Value, true));
                AssetDatabase.ImportAsset(path);
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                worldAsset.scatterRefs.Add(new ScatterRef { id = scatter.Key, asset = ta, format = "json" });
                provenance.outputs.scatters.Add(new ProvenanceScatter { id = scatter.Key, path = path, format = "json" });
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        File.WriteAllText(provenancePath, JsonUtility.ToJson(provenance, true));
        AssetDatabase.ImportAsset(provenancePath);
        worldAsset.provenanceJson = AssetDatabase.LoadAssetAtPath<TextAsset>(provenancePath);

        EditorUtility.SetDirty(worldAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return worldAsset;
    }

    private static void WriteScalarExr(ScalarField field, string path)
    {
        var tex = new Texture2D(field.grid.width, field.grid.height, TextureFormat.RFloat, false, true);
        var pixels = new Color[field.values.Length];
        for (var i = 0; i < field.values.Length; i++)
        {
            var v = field.values[i];
            pixels[i] = new Color(v, 0f, 0f, 1f);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(path, tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureImporter(path, true);
    }

    private static void WriteMaskPng(MaskField field, string path)
    {
        var tex = new Texture2D(field.grid.width, field.grid.height, TextureFormat.R8, false, true);
        var pixels = new Color32[field.values.Length];
        for (var i = 0; i < field.values.Length; i++)
        {
            var v = field.values[i];
            pixels[i] = new Color32(v, 0, 0, 255);
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureImporter(path, false);
    }

    private static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                EnsureFolder(parent);
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    private static void ConfigureImporter(string assetPath, bool hdr)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = hdr ? FilterMode.Bilinear : FilterMode.Point;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
    }
#endif
}
