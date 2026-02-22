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
                speciesCount = 4,
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
            new() { assetId = "ant_base", entityId = "ant", mappedSpeciesId = "default", minImages = 1, generationMode = PackRecipe.GenerationMode.OutlineDriven, variantCount = 4 },
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
