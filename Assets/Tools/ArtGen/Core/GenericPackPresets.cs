using System.Collections.Generic;
using UnityEngine;

public sealed class AntColoniesPackPreset : IPackPreset
{
    public string PresetId => "AntColoniesPreset";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "AntColonies";
        recipe.packId = string.IsNullOrWhiteSpace(packId) ? "AntPack_Auto" : packId;
        recipe.seed = seed;
        recipe.environmentId = "env.ant.v1";
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "ant",
                archetypeId = "archetype.ant",
                speciesCount = 5,
                roles = new List<string> { "worker", "soldier" },
                lifeStages = new List<string> { "adult" },
                states = new List<string> { "idle", "walk" }
            }
        };
        recipe.props = new List<PackRecipe.PropRequirement>
        {
            new() { propId = "prop_nest_entrance_medium", importance = "critical" },
            new() { propId = "prop_food_large", importance = "critical" }
        };
        recipe.referenceAssets = new List<PackRecipe.ReferenceAssetNeed>
        {
            new() { assetId = "FireAnt", entityId = "ant", mappedSpeciesId = "FireAnt", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 },
            new() { assetId = "CarpenterAnt", entityId = "ant", mappedSpeciesId = "CarpenterAnt", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 },
            new() { assetId = "PharaohAnt", entityId = "ant", mappedSpeciesId = "PharaohAnt", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 },
            new() { assetId = "WeaverAnt", entityId = "ant", mappedSpeciesId = "WeaverAnt", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 },
            new() { assetId = "ArmyAnt", entityId = "ant", mappedSpeciesId = "ArmyAnt", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 },
            new() { assetId = "prop_nest_entrance_medium", entityId = "ant", mappedSpeciesId = "default", minImages = 1, generationMode = PackRecipe.GenerationMode.Procedural, variantCount = 1 },
            new() { assetId = "prop_food_large", entityId = "ant", mappedSpeciesId = "default", minImages = 1, generationMode = PackRecipe.GenerationMode.Procedural, variantCount = 1 }
        };
        return recipe;
    }
}

public sealed class GenericPackPreset : IPackPreset
{
    public string PresetId => "GenericPreset";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "Generic";
        recipe.packId = string.IsNullOrWhiteSpace(packId) ? "GenericPack_Auto" : packId;
        recipe.seed = seed;
        recipe.environmentId = "env.ant.v1";
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "agent",
                archetypeId = "archetype.ant",
                speciesCount = 2,
                roles = new List<string> { "worker" },
                lifeStages = new List<string> { "adult" },
                states = new List<string> { "idle" }
            }
        };
        recipe.referenceAssets = new List<PackRecipe.ReferenceAssetNeed>
        {
            new() { assetId = "agent_base", entityId = "agent", mappedSpeciesId = "default", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 1 }
        };
        return recipe;
    }
}


public sealed class MarbleRacePackPreset : IPackPreset
{
    public string PresetId => "MarbleRacePreset";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "MarbleRace";
        recipe.packId = string.IsNullOrWhiteSpace(packId) ? "MarbleRacePack_Auto" : packId;
        recipe.seed = seed;
        recipe.environmentId = "env.ant.v1";
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "marble",
                archetypeId = "archetype.beetle",
                speciesCount = 2,
                roles = new List<string> { "racer" },
                lifeStages = new List<string> { "adult" },
                states = new List<string> { "idle", "roll", "run" }
            }
        };
        return recipe;
    }
}

public sealed class FantasySportPackPreset : IPackPreset
{
    public string PresetId => "FantasySportPreset";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "FantasySport";
        recipe.packId = string.IsNullOrWhiteSpace(packId) ? "FantasySportPack_Auto" : packId;
        recipe.seed = seed;
        recipe.environmentId = "env.ant.v1";
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "player",
                archetypeId = "archetype.arachnid",
                speciesCount = 2,
                roles = new List<string> { "striker" },
                lifeStages = new List<string> { "adult" },
                states = new List<string> { "idle", "run", "attack" }
            }
        };
        return recipe;
    }
}

public sealed class RaceCarPackPreset : IPackPreset
{
    public string PresetId => "RaceCarPreset";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "RaceCar";
        recipe.packId = string.IsNullOrWhiteSpace(packId) ? "RaceCarPack_Auto" : packId;
        recipe.seed = seed;
        recipe.environmentId = "env.ant.v1";
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "car",
                archetypeId = "archetype.beetle",
                speciesCount = 2,
                roles = new List<string> { "driver" },
                lifeStages = new List<string> { "adult" },
                states = new List<string> { "idle", "drive", "turn" }
            }
        };
        return recipe;
    }
}
