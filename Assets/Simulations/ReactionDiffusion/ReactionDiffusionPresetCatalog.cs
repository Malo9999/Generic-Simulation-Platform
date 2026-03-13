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
                parameters = new Parameters(1.0f, 0.50f, 0.0367f, 0.0649f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Mitosis:
                parameters = new Parameters(1.0f, 0.50f, 0.0340f, 0.0620f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Solitons:
                parameters = new Parameters(1.0f, 0.50f, 0.0300f, 0.0620f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Flower:
                parameters = new Parameters(1.0f, 0.50f, 0.0220f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Finger:
                parameters = new Parameters(1.0f, 0.50f, 0.0370f, 0.0600f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.USkate:
                parameters = new Parameters(1.0f, 0.50f, 0.0620f, 0.0610f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Mazes:
                parameters = new Parameters(1.0f, 0.53f, 0.0290f, 0.0570f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Spirals:
                parameters = new Parameters(1.0f, 0.50f, 0.0180f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Coral:
                parameters = new Parameters(1.0f, 0.50f, 0.0540f, 0.0620f, 1f, 2);
                return true;

            case ReactionDiffusionPreset.Worms:
                parameters = new Parameters(1.0f, 0.57f, 0.0780f, 0.0610f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Spots:
                parameters = new Parameters(1.0f, 0.56f, 0.0260f, 0.0510f, 1f, 1);
                return true;

            case ReactionDiffusionPreset.Chaos:
                parameters = new Parameters(1.0f, 0.55f, 0.0420f, 0.0600f, 1f, 1);
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