using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SimSettingsBase : ScriptableObject
{
    public string simulationId;
    public SeedPolicy seedPolicy = SeedPolicy.RandomEveryRun;
    public int fixedSeed = 12345;
    public float tickDeltaTime = 1f / 60f;
    public List<GroupDef> groups = new();
    public ArtPolicy artPolicy;

    [Serializable]
    public struct GroupDef
    {
        public string groupId;
        public string displayName;
        public Color color;
    }

    [Serializable]
    public struct ArtPolicy
    {
        public PackSelectionMode packSelectionMode;
        public List<ContentPack> preferredPacks;
        public ContentPack forcedPack;
        public BasicShapeProfile basicShapes;
    }

    public enum PackSelectionMode
    {
        AutoBest,
        ForcePack,
        ForceBasic
    }

    [Serializable]
    public struct BasicShapeProfile
    {
        public BasicShape shape;
        public bool outline;
        public float sizeScale;
        public bool stateStripe;
        public bool directionDot;
        public ColorMode colorMode;
        public Color fixedColor;
    }

    public enum BasicShape
    {
        Circle,
        Square,
        Diamond,
        Capsule,
        Triangle
    }

    public enum ColorMode
    {
        GroupColor,
        SpeciesColor,
        RoleColor,
        Fixed
    }

    public abstract void ApplyTo(ScenarioConfig cfg);
}
