using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PackRecipe", menuName = "GSP/Art/Pack Recipe")]
public sealed class PackRecipe : ScriptableObject
{
    [Serializable]
    public sealed class AnimationPolicy
    {
        public int idleFrames = 2;
        public int walkFrames = 4;
        public int runFrames = 4;
        public int defaultFps = 8;

        public int FramesForState(string stateId)
        {
            if (string.Equals(stateId, "idle", StringComparison.OrdinalIgnoreCase)) return Mathf.Max(1, idleFrames);
            if (string.Equals(stateId, "walk", StringComparison.OrdinalIgnoreCase)) return Mathf.Max(1, walkFrames);
            if (string.Equals(stateId, "run", StringComparison.OrdinalIgnoreCase)) return Mathf.Max(1, runFrames);
            return 1;
        }
    }

    [Serializable]
    public sealed class EntityRequirement
    {
        public string entityId = "entity";
        public string archetypeId = "archetype.default";
        public int speciesCount = 1;
        public List<string> roles = new();
        public List<string> lifeStages = new();
        public List<string> states = new();
        public AnimationPolicy animationPolicy = new();
    }

    [Serializable]
    public sealed class PropRequirement
    {
        public string propId = "prop";
        public string importance = "background";
    }

    [Serializable]
    public sealed class GenerationPolicy
    {
        public bool generateBlueprints = true;
        public bool compileSpritesheets = true;
        public bool renderAntStripeOverlay;
        public bool includeAntMaskSpritesInMainPack;
        public bool exportCompatibilityAntContentPack;
    }

    [Serializable]
    public sealed class ReferenceAssetNeed
    {
        public string assetId;
        public string entityId = "ant";
        public string mappedSpeciesId;
        public int minImages = 1;
        public GenerationMode generationMode = GenerationMode.OutlineDriven;
        public int variantCount = 1;
    }

    public enum GenerationMode
    {
        Procedural = 0,
        OutlineDriven = 1
    }

    public string simulationId = "Simulation";
    public string packId = "Pack";
    public int seed = 12345;
    public string outputFolder = "Assets/Presentation/Packs/Simulation/Pack";
    public int tileSize = 32;
    public int agentSpriteSize = 64;
    public string agentsBuildStyle = "BasicShapes";
    public string environmentId = "env.default";
    public List<EntityRequirement> entities = new();
    public List<PropRequirement> props = new();
    public List<ReferenceAssetNeed> referenceAssets = new();
    public GenerationPolicy generationPolicy = new();
}
