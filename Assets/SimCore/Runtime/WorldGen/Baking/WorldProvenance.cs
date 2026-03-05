using System;
using System.Collections.Generic;

[Serializable]
public class WorldProvenance
{
    public string recipeId;
    public int version;
    public string mapId;
    public int seed;
    public WorldGridSpec grid;
    public string settingsType;
    public string settingsJson;
    public NoiseSet noiseDescriptors;
    public string bakedAtUtc;
    public string generatorGitCommit;
    public ProvenanceOutputs outputs = new ProvenanceOutputs();
}

[Serializable]
public class ProvenanceOutputs
{
    public List<ProvenanceScalar> scalars = new List<ProvenanceScalar>();
    public List<ProvenanceMask> masks = new List<ProvenanceMask>();
    public List<ProvenanceSpline> splines = new List<ProvenanceSpline>();
    public List<ProvenanceScatter> scatters = new List<ProvenanceScatter>();
}

[Serializable] public class ProvenanceScalar { public string id; public string path; public string format; }
[Serializable] public class ProvenanceMask { public string id; public string path; public string encoding; public string[] categories; }
[Serializable] public class ProvenanceSpline { public string id; public string path; }
[Serializable] public class ProvenanceScatter { public string id; public string path; public string format; }
