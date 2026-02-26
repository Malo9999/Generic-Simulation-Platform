using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class PackInputManifest
{
    [Serializable]
    public sealed class ImportedFile
    {
        public string state;
        public string canonicalFileName;
        public string sourceHash;
    }

    public string version = "v1";
    public string group = "Agents";
    public string entityId = "entity";
    public int dirSet = 4;
    public int framesPerState = 10;
    public int cellSize = 64;
    public int padding = 2;
    public string layout = "PerStateSheets";
    public List<string> states = new();
    public List<ImportedFile> importedFiles = new();

    public static PackInputManifest Load(string assetRelativePath)
    {
        var fullPath = Path.GetFullPath(assetRelativePath);
        if (!File.Exists(fullPath))
        {
            return new PackInputManifest();
        }

        var json = File.ReadAllText(fullPath);
        var manifest = JsonUtility.FromJson<PackInputManifest>(json);
        manifest ??= new PackInputManifest();
        manifest.states ??= new List<string>();
        manifest.importedFiles ??= new List<ImportedFile>();
        return manifest;
    }

    public static void Save(string assetRelativePath, PackInputManifest manifest)
    {
        manifest ??= new PackInputManifest();
        manifest.states ??= new List<string>();
        manifest.importedFiles ??= new List<ImportedFile>();

        var fullPath = Path.GetFullPath(assetRelativePath);
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(fullPath, json);
    }
}
