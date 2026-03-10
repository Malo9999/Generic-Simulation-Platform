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
                parameters = new Parameters(1.0f, 0.5f, 0.0367f, 0.0649f, 1f, 6);
                return true;
            case ReactionDiffusionPreset.Mitosis:
                parameters = new Parameters(1.0f, 0.5f, 0.0367f, 0.0649f, 1f, 8);
                return true;
            case ReactionDiffusionPreset.Solitons:
                parameters = new Parameters(1.0f, 0.5f, 0.030f, 0.062f, 1f, 6);
                return true;
            case ReactionDiffusionPreset.Flower:
                parameters = new Parameters(1.0f, 0.5f, 0.024f, 0.055f, 1f, 7);
                return true;
            case ReactionDiffusionPreset.Finger:
                parameters = new Parameters(1.0f, 0.5f, 0.037f, 0.060f, 1f, 6);
                return true;
            case ReactionDiffusionPreset.USkate:
                parameters = new Parameters(1.0f, 0.5f, 0.062f, 0.061f, 1f, 4);
                return true;
            case ReactionDiffusionPreset.Mazes:
                parameters = new Parameters(1.0f, 0.5f, 0.029f, 0.057f, 1f, 8);
                return true;
            case ReactionDiffusionPreset.Spirals:
                parameters = new Parameters(1.0f, 0.5f, 0.018f, 0.051f, 1f, 8);
                return true;
            case ReactionDiffusionPreset.Coral:
                parameters = new Parameters(1.0f, 0.5f, 0.054f, 0.062f, 1f, 5);
                return true;
            case ReactionDiffusionPreset.Worms:
                parameters = new Parameters(1.0f, 0.5f, 0.078f, 0.061f, 1f, 5);
                return true;
            case ReactionDiffusionPreset.Spots:
                parameters = new Parameters(1.0f, 0.5f, 0.026f, 0.051f, 1f, 7);
                return true;
            case ReactionDiffusionPreset.Chaos:
                parameters = new Parameters(1.0f, 0.5f, 0.042f, 0.060f, 1f, 10);
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
