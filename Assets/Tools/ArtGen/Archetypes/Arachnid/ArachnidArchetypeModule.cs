using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ArachnidArchetypeModule : IArchetypeModule
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Arachnid/ArachnidSpeciesLibrary.asset";
    public string ArchetypeId => "archetype.arachnid";
    public void EnsureLibrariesExist()
    {
        if (AssetDatabase.LoadAssetAtPath<ArachnidSpeciesLibrary>(LibraryPath) != null) return;
        var lib = ScriptableObject.CreateInstance<ArachnidSpeciesLibrary>();
        lib.profiles = new List<ArachnidSpeciesProfile> { new() { speciesId = "wolf_spider_like", displayName = "Wolf Spider-like" }, new() { speciesId = "orb_weaver_like", displayName = "Orb Weaver-like", bodyScale = 1.1f } };
        ImportSettingsUtil.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\','/'));
        AssetDatabase.CreateAsset(lib, LibraryPath);
    }
    public List<string> PickSpeciesIds(int seed, int count) => Deterministic.PickN(AssetDatabase.LoadAssetAtPath<ArachnidSpeciesLibrary>(LibraryPath).profiles.Select(p => p.speciesId).ToList(), Mathf.Max(1,count), seed);
    public ArchetypeSynthesisResult Synthesize(ArchetypeSynthesisRequest req)
    {
        var bp = ArachnidBlueprintSynthesizer.Build(req);
        var outp = new ArchetypeSynthesisResult();
        outp.frames.Add(new SynthesizedFrame { spriteId = $"agent:{req.entity.entityId}:{req.speciesId}:{req.role}:{req.stage}:{req.state}:{req.frameIndex:00}", bodyBlueprint = bp });
        return outp;
    }
}
