using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class BeetleArchetypeModule : IArchetypeModule
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Beetle/BeetleSpeciesLibrary.asset";
    public string ArchetypeId => "archetype.beetle";

    public void EnsureLibrariesExist()
    {
        if (AssetDatabase.LoadAssetAtPath<BeetleSpeciesLibrary>(LibraryPath) != null) return;
        var lib = ScriptableObject.CreateInstance<BeetleSpeciesLibrary>();
        lib.profiles = new List<BeetleSpeciesProfile> { new() { speciesId = "ground_beetle_like", displayName = "Ground Beetle-like", grubLike = false }, new() { speciesId = "grub_like", displayName = "Grub-like", grubLike = true } };
        ImportSettingsUtil.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\','/'));
        AssetDatabase.CreateAsset(lib, LibraryPath);
    }

    public List<string> PickSpeciesIds(int seed, int count) => Deterministic.PickN(AssetDatabase.LoadAssetAtPath<BeetleSpeciesLibrary>(LibraryPath).profiles.Select(p => p.speciesId).ToList(), Mathf.Max(1,count), seed);

    public ArchetypeSynthesisResult Synthesize(ArchetypeSynthesisRequest req)
    {
        var lib = AssetDatabase.LoadAssetAtPath<BeetleSpeciesLibrary>(LibraryPath);
        var profile = lib.profiles.FirstOrDefault(p => p.speciesId == req.speciesId) ?? lib.profiles[0];
        var bp = BeetleBlueprintSynthesizer.Build(req, profile.grubLike);
        var outp = new ArchetypeSynthesisResult();
        outp.frames.Add(new SynthesizedFrame { spriteId = $"agent:{req.entity.entityId}:{req.speciesId}:{req.role}:{req.stage}:{req.state}:{req.frameIndex:00}", bodyBlueprint = bp });
        return outp;
    }
}
