using System;
using System.Collections.Generic;

[Serializable]
public struct ZoneDef
{
    public int zoneId;
    public string name;
}

[Serializable]
public class WorldMap
{
    public string recipeId;
    public string mapId;
    public int seed;
    public WorldGridSpec grid;
    public Dictionary<string, ScalarField> scalars = new Dictionary<string, ScalarField>();
    public Dictionary<string, MaskField> masks = new Dictionary<string, MaskField>();
    public List<WorldSpline> splines = new List<WorldSpline>();
    public Dictionary<string, ScatterSet> scatters = new Dictionary<string, ScatterSet>();
    public Dictionary<string, ZoneDef> zones = new Dictionary<string, ZoneDef>();

    public void EnsureRequiredOutputs()
    {
        if (!masks.ContainsKey("walkable"))
        {
            var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
            for (var i = 0; i < walkable.values.Length; i++) walkable.values[i] = 1;
            masks["walkable"] = walkable;
        }

        if (!masks.ContainsKey("zones"))
        {
            var zonesMask = new MaskField("zones", grid, MaskEncoding.Categorical)
            {
                categories = new[] { "default" }
            };
            masks["zones"] = zonesMask;
        }
    }
}
