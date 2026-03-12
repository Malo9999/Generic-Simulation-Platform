using System;
using UnityEngine;

public static class ReactionDiffusionPresetCatalog
{
    [Serializable]
    public struct Parameters
    {
        public float diffuseA;
        public float diffuseB;
        public float feed;
        public float kill;
        public float dt;
        public int stepsPerFrame;

        public Parameters(float diffuseA, float diffuseB, float feed, float kill, float dt, int stepsPerFrame)
        {
            this.diffuseA = diffuseA;
            this.diffuseB = diffuseB;
            this.feed = feed;
            this.kill = kill;
            this.dt = dt;
            this.stepsPerFrame = stepsPerFrame;
        }
    }

    public static bool TryGet(ReactionDiffusionPreset preset, out Parameters parameters)
    {
        switch (preset)
        {
            case ReactionDiffusionPreset.Default:
                // Calm baseline, good for debugging and comparison.
                parameters = new Parameters(1.0f, 0.5f, 0.0367f, 0.0649f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Mitosis:
                // Slightly denser than Default, but still stable.
                parameters = new Parameters(1.0f, 0.5f, 0.0340f, 0.0620f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Solitons:
                // More isolated moving structures.
                parameters = new Parameters(1.0f, 0.5f, 0.0300f, 0.0620f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Flower:
                // Lower feed/kill pocket for more ornate radial growth.
                parameters = new Parameters(1.0f, 0.5f, 0.0220f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Finger:
                // Good elongated branching baseline.
                parameters = new Parameters(1.0f, 0.5f, 0.0370f, 0.0600f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.USkate:
                // Faster-moving, more aggressive frontier behavior.
                parameters = new Parameters(1.0f, 0.5f, 0.0620f, 0.0610f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Mazes:
                // Strong labyrinth preset.
                parameters = new Parameters(1.0f, 0.5f, 0.0290f, 0.0570f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Spirals:
                // More delicate, lower-parameter regime.
                parameters = new Parameters(1.0f, 0.5f, 0.0180f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Coral:
                // Slightly faster because it benefits from a bit more growth pressure.
                parameters = new Parameters(1.0f, 0.5f, 0.0540f, 0.0620f, 1f, 2);
                return true;

            case ReactionDiffusionPreset.Worms:
                // Distinct higher-feed wormy regime, but kept stable.
                parameters = new Parameters(1.0f, 0.5f, 0.0780f, 0.0610f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Spots:
                // Cleaner spot field.
                parameters = new Parameters(1.0f, 0.5f, 0.0260f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Chaos:
                // Best "living" default for slow drift + micro reseeding.
                parameters = new Parameters(1.0f, 0.5f, 0.0420f, 0.0600f, 1f, 1);
                return true;

            default:
                parameters = default;
                return false;
        }
    }

    public static void ApplyTo(
        ReactionDiffusionPreset preset,
        ref float diffuseA,
        ref float diffuseB,
        ref float feed,
        ref float kill,
        ref float dt,
        ref int stepsPerFrame)
    {
        if (!TryGet(preset, out var values))
        {
            return;
        }

        diffuseA = Mathf.Max(0f, values.diffuseA);
        diffuseB = Mathf.Max(0f, values.diffuseB);
        feed = Mathf.Max(0f, values.feed);
        kill = Mathf.Max(0f, values.kill);
        dt = Mathf.Max(0.0001f, values.dt);
        stepsPerFrame = Mathf.Max(1, values.stepsPerFrame);
    }
}