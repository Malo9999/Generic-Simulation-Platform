using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Serializable]
public class SerengetiMapSpec
{
    [JsonProperty("mapId")] public string mapId;
    [JsonProperty("version")] public int version;
    [JsonProperty("arena")] public ArenaSpec arena = new();
    [JsonProperty("regions")] public List<RegionSpec> regions = new();
    [JsonProperty("water")] public WaterSpec water = new();
    [JsonProperty("landmarks")] public LandmarkSpec landmarks = new();
    [JsonProperty("calendar")] public CalendarSpec calendar = new();
    [JsonProperty("legend")] public LegendSpec legend = new();
    [JsonProperty("spawnHints")] public SpawnHintsSpec spawnHints = new();
}

[Serializable]
public class ArenaSpec
{
    [JsonProperty("width")] public int width;
    [JsonProperty("height")] public int height;
}

[Serializable]
public class RegionSpec
{
    [JsonProperty("id")] public string id;
    [JsonProperty("name")] public string name;
    [JsonProperty("shape")] public ShapeSpec shape = new();
    [JsonProperty("biome")] public string biome;
    [JsonProperty("baseGreenness")] public float baseGreenness;
    [JsonProperty("rainResponse")] public float rainResponse;
    [JsonProperty("cover")] public float cover;
}

[Serializable]
public class ShapeSpec
{
    [JsonProperty("type")] public string type;
    [JsonProperty("xMin")] public float xMin;
    [JsonProperty("xMax")] public float xMax;
    [JsonProperty("yMin")] public float yMin;
    [JsonProperty("yMax")] public float yMax;
}

[Serializable]
public class WaterSpec
{
    [JsonProperty("mainRiver")] public RiverSpec mainRiver = new();
    [JsonProperty("grumeti")] public RiverSpec grumeti = new();
    [JsonProperty("pools")] public List<PoolSpec> pools = new();
    [JsonProperty("seasonal")] public SeasonalSpec seasonal = new();
}

[Serializable]
public class RiverSpec
{
    [JsonProperty("id")] public string id;
    [JsonProperty("centerline")] public List<PointSpec> centerline = new();
    [JsonProperty("widthNorth")] public float widthNorth;
    [JsonProperty("widthSouth")] public float widthSouth;
    [JsonProperty("floodplainExtra")] public float floodplainExtra;
    [JsonProperty("bankExtra")] public float bankExtra;
    [JsonProperty("width")] public float width;
    [JsonProperty("crossings")] public List<CrossingSpec> crossings = new();
}

[Serializable]
public class PointSpec
{
    [JsonProperty("x")] public float x;
    [JsonProperty("y")] public float y;
}

[Serializable]
public class CrossingSpec
{
    [JsonProperty("id")] public string id;
    [JsonProperty("x")] public float x;
    [JsonProperty("y")] public float y;
    [JsonProperty("radius")] public float radius;
    [JsonProperty("crocRisk")] public float crocRisk;
}

[Serializable]
public class PoolSpec
{
    [JsonProperty("id")] public string id;
    [JsonProperty("x")] public float x;
    [JsonProperty("y")] public float y;
    [JsonProperty("radius")] public float radius;
    [JsonProperty("permanent")] public bool permanent;
}

[Serializable]
public class SeasonalSpec
{
    [JsonProperty("maxVeins")] public int maxVeins;
    [JsonProperty("maxPonds")] public int maxPonds;
    [JsonProperty("presenceByMonth")] public Dictionary<string, float> presenceByMonth = new();
}

[Serializable]
public class LandmarkSpec
{
    [JsonProperty("kopjes")] public List<LandmarkNodeSpec> kopjes = new();
    [JsonProperty("wetlands")] public List<LandmarkNodeSpec> wetlands = new();
}

[Serializable]
public class LandmarkNodeSpec
{
    [JsonProperty("id")] public string id;
    [JsonProperty("x")] public float x;
    [JsonProperty("y")] public float y;
    [JsonProperty("radius")] public float radius;
    [JsonProperty("cover")] public float cover;
}

[Serializable]
public class CalendarSpec
{
    [JsonProperty("ticksPerMonth")] public int ticksPerMonth;
    [JsonProperty("migrationStages")] public List<StageSpec> migrationStages = new();
}

[Serializable]
public class StageSpec
{
    [JsonProperty("months")] public List<int> months = new();
    [JsonProperty("bias")] public Dictionary<string, float> bias = new();
    [JsonProperty("crossingPressure")] public Dictionary<string, float> crossingPressure = new();
}

[Serializable]
public class LegendSpec
{
    [JsonProperty("sexAgeRules")] public LegendRules sexAgeRules = new();
    [JsonProperty("species")] public Dictionary<string, LegendSpeciesEntry> species = new();
}

[Serializable]
public class LegendRules
{
    [JsonProperty("maleScale")] public float maleScale;
    [JsonProperty("femaleScale")] public float femaleScale;
    [JsonProperty("childScale")] public float childScale;
    [JsonProperty("maleDarken")] public float maleDarken;
    [JsonProperty("childLighten")] public float childLighten;
}

[Serializable]
public class LegendSpeciesEntry
{
    [JsonProperty("shape")] public string shape;
    [JsonProperty("fill")] public string fill;
    [JsonProperty("outline")] public string outline;
}

[Serializable]
public class SpawnHintsSpec
{
    [JsonExtensionData] public IDictionary<string, JToken> entries = new Dictionary<string, JToken>();
}
