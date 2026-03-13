using UnityEngine;

public interface IGranularFlowBrain
{
    GranularFlowBrainDecision Decide(in GranularFlowBrainContext context, float dt);
}

public readonly struct GranularFlowBrainContext
{
    public readonly GranularFlowSensors Sensors;
    public readonly GranularRuleBrainSettings Settings;
    public readonly float CurrentGateOpen;
    public readonly float CurrentFlap;
    public readonly float TimeSeconds;

    public GranularFlowBrainContext(GranularFlowSensors sensors, GranularRuleBrainSettings settings, float currentGateOpen, float currentFlap, float timeSeconds)
    {
        Sensors = sensors;
        Settings = settings;
        CurrentGateOpen = currentGateOpen;
        CurrentFlap = currentFlap;
        TimeSeconds = timeSeconds;
    }
}

public readonly struct GranularFlowBrainDecision
{
    public readonly float GateTargetOpen;
    public readonly float FlapTarget;

    public GranularFlowBrainDecision(float gateTargetOpen, float flapTarget)
    {
        GateTargetOpen = Mathf.Clamp01(gateTargetOpen);
        FlapTarget = Mathf.Clamp(flapTarget, -1f, 1f);
    }
}

public sealed class GranularFlowRuleBrain : IGranularFlowBrain
{
    private float pulseTimer;
    private float pulseDuration;
    private bool pulseOpen;

    public GranularFlowBrainDecision Decide(in GranularFlowBrainContext context, float dt)
    {
        var sensors = context.Sensors;
        var settings = context.Settings;

        pulseTimer += Mathf.Max(0f, dt);
        if (pulseTimer >= pulseDuration)
        {
            pulseTimer = 0f;
            pulseOpen = !pulseOpen;
            var jitter = 0.5f + 0.5f * Mathf.PerlinNoise(context.TimeSeconds * 0.63f, 0.1f);
            pulseDuration = Mathf.Lerp(settings.gatePulseMinSeconds, settings.gatePulseMaxSeconds, jitter);
        }

        var jamPressure = Mathf.InverseLerp(settings.jamThreshold, 1f, sensors.throatJamEstimate);
        var upperDemand = Mathf.InverseLerp(0f, settings.upperFillTarget, sensors.upperChamberFill);
        var throughputNeed = 1f - Mathf.Clamp01(sensors.recentThroughputEstimate * 0.12f);

        var gate = pulseOpen ? 0.65f : 0.25f;
        gate += upperDemand * 0.35f;
        gate -= jamPressure * 0.5f;
        gate += throughputNeed * 0.2f;
        if (sensors.leftBinFill > 0.95f || sensors.rightBinFill > 0.95f)
        {
            gate = Mathf.Min(gate, 0.25f);
        }

        var colorBias = (sensors.lowerDominantColorNorm - 0.5f) * 2f;
        var binBias = sensors.leftBinFill - sensors.rightBinFill;
        var flapTarget = Mathf.Clamp(colorBias * settings.flapBiasStrength - binBias * 0.6f, -1f, 1f);
        if (Mathf.Abs(flapTarget) < 0.15f)
        {
            flapTarget = 0f;
        }

        return new GranularFlowBrainDecision(gate, flapTarget);
    }
}
