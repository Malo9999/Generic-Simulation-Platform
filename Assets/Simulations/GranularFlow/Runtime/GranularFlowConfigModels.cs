using System;
using UnityEngine;

[Serializable]
public class GranularFlowConfig
{
    public string machineArchetype = "SplitterTower";
    public SplitterTowerConfig machine = new();
    public GranularParticleRecipe particles = new();
    public GranularRuleBrainSettings ruleBrain = new();

    public void Normalize()
    {
        machineArchetype = string.IsNullOrWhiteSpace(machineArchetype) ? "SplitterTower" : machineArchetype.Trim();
        machine ??= new SplitterTowerConfig();
        particles ??= new GranularParticleRecipe();
        ruleBrain ??= new GranularRuleBrainSettings();
        machine.Normalize();
        particles.Normalize();
        ruleBrain.Normalize();
    }
}

[Serializable]
public class SplitterTowerConfig
{
    public float feederRatePerSecond = 160f;
    public float gateMaxOpen = 1f;
    public float gateMoveSpeed = 4f;
    public float flapMoveSpeed = 10f;
    public float flapMaxAngle = 34f;
    public float laneSlope = 0.55f;

    public void Normalize()
    {
        feederRatePerSecond = Mathf.Clamp(feederRatePerSecond, 5f, 800f);
        gateMaxOpen = Mathf.Clamp01(gateMaxOpen);
        gateMoveSpeed = Mathf.Clamp(gateMoveSpeed, 0.2f, 20f);
        flapMoveSpeed = Mathf.Clamp(flapMoveSpeed, 0.2f, 40f);
        flapMaxAngle = Mathf.Clamp(flapMaxAngle, 5f, 75f);
        laneSlope = Mathf.Clamp(laneSlope, 0.1f, 1f);
    }
}

[Serializable]
public class GranularParticleRecipe
{
    public int maxParticles = 12000;
    public float radius = 0.16f;
    public float gravity = 18f;
    public float damping = 0.994f;
    public float collisionBounce = 0.35f;
    public float feederJitter = 0.85f;
    public Color[] palette =
    {
        new(1f, 0.3f, 0.2f, 1f),
        new(0.2f, 1f, 0.4f, 1f),
        new(0.2f, 0.7f, 1f, 1f),
        new(1f, 0.9f, 0.2f, 1f)
    };

    public void Normalize()
    {
        maxParticles = Mathf.Clamp(maxParticles, 256, 40000);
        radius = Mathf.Clamp(radius, 0.04f, 0.6f);
        gravity = Mathf.Clamp(gravity, 0f, 80f);
        damping = Mathf.Clamp(damping, 0.9f, 1f);
        collisionBounce = Mathf.Clamp(collisionBounce, 0f, 0.9f);
        feederJitter = Mathf.Clamp(feederJitter, 0f, 3f);
        if (palette == null || palette.Length == 0)
        {
            palette = new[] { Color.yellow, Color.cyan, Color.magenta, Color.green };
        }
    }
}

[Serializable]
public class GranularRuleBrainSettings
{
    public float upperFillTarget = 0.58f;
    public float jamThreshold = 0.62f;
    public float flapBiasStrength = 0.95f;
    public float gatePulseMinSeconds = 0.2f;
    public float gatePulseMaxSeconds = 0.9f;

    public void Normalize()
    {
        upperFillTarget = Mathf.Clamp01(upperFillTarget);
        jamThreshold = Mathf.Clamp01(jamThreshold);
        flapBiasStrength = Mathf.Clamp(flapBiasStrength, 0f, 2f);
        gatePulseMinSeconds = Mathf.Clamp(gatePulseMinSeconds, 0.05f, 2f);
        gatePulseMaxSeconds = Mathf.Clamp(gatePulseMaxSeconds, gatePulseMinSeconds, 3f);
    }
}
