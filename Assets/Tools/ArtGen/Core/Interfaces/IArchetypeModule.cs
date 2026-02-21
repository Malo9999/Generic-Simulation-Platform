using System.Collections.Generic;
using UnityEngine;

public sealed class ArchetypeSynthesisRequest
{
    public PackRecipe recipe;
    public PackRecipe.EntityRequirement entity;
    public string speciesId;
    public string role;
    public string stage;
    public string state;
    public int frameIndex;
    public string blueprintPath;
    public int seed;
}

public sealed class SynthesizedFrame
{
    public string spriteId;
    public PixelBlueprint2D bodyBlueprint;
    public PixelBlueprint2D maskBlueprint;
}

public sealed class ArchetypeSynthesisResult
{
    public readonly List<SynthesizedFrame> frames = new();
}

public interface IArchetypeModule
{
    string ArchetypeId { get; }
    void EnsureLibrariesExist();
    List<string> PickSpeciesIds(int seed, int count);
    ArchetypeSynthesisResult Synthesize(ArchetypeSynthesisRequest req);
}
