using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AntArchetypeModule : IArchetypeModule
{
    private const string LibraryPath = "Assets/Tools/ArtGen/Archetypes/Ant/AntSpeciesLibrary.asset";
    public string ArchetypeId => "archetype.ant";

    public void EnsureLibrariesExist()
    {
        if (AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath) != null) return;
        var library = ScriptableObject.CreateInstance<AntSpeciesLibrary>();
        library.profiles = new List<AntSpeciesProfile>
        {
            new() { speciesId = "lasius_niger_like", displayName = "Lasius niger-like", baseColorId = "black", headScale = 0.9f, thoraxScale = 0.9f, abdomenScale = 0.95f, legLengthScale = 1f, antennaLengthScale = 1f, petiolePinchStrength = 1.2f },
            new() { speciesId = "camponotus_like", displayName = "Camponotus-like", baseColorId = "brown", headScale = 1.15f, thoraxScale = 1.1f, abdomenScale = 1.2f, legLengthScale = 1f, antennaLengthScale = 1f, petiolePinchStrength = 1f },
            new() { speciesId = "solenopsis_like", displayName = "Solenopsis-like", baseColorId = "red", headScale = 1.0f, thoraxScale = 0.95f, abdomenScale = 0.9f, legLengthScale = 0.9f, antennaLengthScale = 1.1f, petiolePinchStrength = 1.3f },
            new() { speciesId = "formica_like", displayName = "Formica-like", baseColorId = "yellow", headScale = 1.0f, thoraxScale = 1f, abdomenScale = 1.3f, legLengthScale = 1.1f, antennaLengthScale = 1f, petiolePinchStrength = 1f }
        };

        var folder = Path.GetDirectoryName(LibraryPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(folder)) ImportSettingsUtil.EnsureFolder(folder);
        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();
    }

    public List<string> PickSpeciesIds(int seed, int count)
    {
        var lib = AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath);
        var ids = lib.profiles.Select(p => p.speciesId).ToList();
        return Deterministic.PickN(ids, Mathf.Max(1, count), seed);
    }

    public ArchetypeSynthesisResult Synthesize(ArchetypeSynthesisRequest req)
    {
        var lib = AssetDatabase.LoadAssetAtPath<AntSpeciesLibrary>(LibraryPath);
        var profile = lib.profiles.FirstOrDefault(p => p.speciesId == req.speciesId) ?? lib.profiles[0];
        var (body, stripe) = AntBlueprintSynthesizer.Build(profile, req);
        var result = new ArchetypeSynthesisResult();
        var id = $"agent:{req.entity.entityId}:{req.speciesId}:{req.role}:{req.stage}:{req.state}:{req.frameIndex:00}";
        result.frames.Add(new SynthesizedFrame { spriteId = id, bodyBlueprint = body, maskBlueprint = stripe });
        return result;
    }
}
