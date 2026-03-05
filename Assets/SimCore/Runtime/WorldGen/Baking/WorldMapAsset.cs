using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/WorldGen/World Map Asset", fileName = "world")]
public class WorldMapAsset : ScriptableObject
{
    public string recipeId;
    public string mapId;
    public int seed;
    public WorldGridSpec grid;
    public List<FieldRef> scalarRefs = new List<FieldRef>();
    public List<MaskRef> maskRefs = new List<MaskRef>();
    public List<SplineRef> splineRefs = new List<SplineRef>();
    public List<ScatterRef> scatterRefs = new List<ScatterRef>();
    public TextAsset provenanceJson;
}

[Serializable]
public class FieldRef
{
    public string id;
    public UnityEngine.Object asset;
    public string format;
}

[Serializable]
public class MaskRef
{
    public string id;
    public UnityEngine.Object asset;
    public MaskEncoding encoding;
    public string[] categories;
}

[Serializable]
public class SplineRef
{
    public string id;
    public UnityEngine.Object asset;
}

[Serializable]
public class ScatterRef
{
    public string id;
    public UnityEngine.Object asset;
    public string format;
}
