using System.Collections.Generic;

public sealed class AntColoniesPreset : IPackPreset
{
    public string PresetId => "AntColonies";

    public PackRecipe CreateDefaultRecipe(string packId, int seed)
    {
        var recipe = UnityEngine.ScriptableObject.CreateInstance<PackRecipe>();
        recipe.simulationId = "AntColonies";
        recipe.packId = packId;
        recipe.seed = seed;
        recipe.outputFolder = $"Assets/Presentation/Packs/AntColonies/{packId}";
        recipe.environmentId = "env.ant.v1";
        recipe.tileSize = 32;
        recipe.agentSpriteSize = 64;
        recipe.entities = new List<PackRecipe.EntityRequirement>
        {
            new()
            {
                entityId = "ant", archetypeId = "archetype.ant", speciesCount = 3,
                roles = new List<string>{"queen","worker","soldier"},
                lifeStages = new List<string>{"egg","larva","pupa","adult"},
                states = new List<string>{"idle","walk","run","work","eat","reproduce","fight","defend","hurt","death"},
                animationPolicy = new PackRecipe.AnimationPolicy { idleFrames = 2, walkFrames = 4, runFrames = 4, defaultFps = 8 }
            },
            new()
            {
                entityId = "spider", archetypeId = "archetype.arachnid", speciesCount = 1,
                roles = new List<string>{"adult"},
                lifeStages = new List<string>{"adult"},
                states = new List<string>{"idle","walk","run","fight","hurt","death"},
                animationPolicy = new PackRecipe.AnimationPolicy { idleFrames = 2, walkFrames = 4, runFrames = 4, defaultFps = 8 }
            },
            new()
            {
                entityId = "beetle", archetypeId = "archetype.beetle", speciesCount = 1,
                roles = new List<string>{"adult"},
                lifeStages = new List<string>{"adult"},
                states = new List<string>{"idle","walk","hurt","death"},
                animationPolicy = new PackRecipe.AnimationPolicy { idleFrames = 2, walkFrames = 2, runFrames = 2, defaultFps = 8 }
            }
        };

        recipe.props = new List<PackRecipe.PropRequirement>
        {
            new() { propId = "nest_entrance", importance = "hero" },
            new() { propId = "food_pile_small", importance = "hero" },
            new() { propId = "food_pile_med", importance = "hero" },
            new() { propId = "food_pile_large", importance = "hero" },
            new() { propId = "egg_cluster", importance = "hero" },
            new() { propId = "foliage_scatter", importance = "background" },
            new() { propId = "rock_scatter", importance = "background" }
        };

        recipe.referenceAssets = new List<PackRecipe.ReferenceAssetNeed>
        {
            new() { assetId = "FireAnt", minImages = 1 },
            new() { assetId = "CarpenterAnt", minImages = 1 },
            new() { assetId = "PharaohAnt", minImages = 1 },
            new() { assetId = "BlackGardenAnt", minImages = 1 },
            new() { assetId = "RedWoodAnt", minImages = 1 }
        };

        recipe.generationPolicy.generateBlueprints = true;
        recipe.generationPolicy.compileSpritesheets = true;
        recipe.generationPolicy.exportCompatibilityAntContentPack = true;
        return recipe;
    }
}
