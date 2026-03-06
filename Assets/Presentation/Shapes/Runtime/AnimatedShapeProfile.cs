using System;

[Serializable]
public sealed class AnimatedShapeProfile
{
    public ShapeAnimType animType = ShapeAnimType.None;
    public float amplitude = 0.04f;
    public float frequency = 1.6f;
    public float phaseOffset;
    public float scalePulse = 0.02f;
    public float brightnessPulse;
    public bool useUnscaledTime;

    public static AnimatedShapeProfile CreateAmoebaWobble(float amplitude = 0.04f, float frequency = 1.6f, float scalePulse = 0.02f)
    {
        return new AnimatedShapeProfile
        {
            animType = ShapeAnimType.AmoebaWobble,
            amplitude = amplitude,
            frequency = frequency,
            scalePulse = scalePulse,
            brightnessPulse = 0f,
            phaseOffset = 0f,
            useUnscaledTime = false
        };
    }

    public static AnimatedShapeProfile CreateGlowPulse(float frequency = 1.4f, float scalePulse = 0.08f, float brightnessPulse = 0.18f)
    {
        return new AnimatedShapeProfile
        {
            animType = ShapeAnimType.GlowPulse,
            amplitude = 0f,
            frequency = frequency,
            scalePulse = scalePulse,
            brightnessPulse = brightnessPulse,
            phaseOffset = 0f,
            useUnscaledTime = false
        };
    }

    public static AnimatedShapeProfile CreateFilamentWave(float amplitude = 0.03f, float frequency = 1.2f, float scalePulse = 0.01f)
    {
        return new AnimatedShapeProfile
        {
            animType = ShapeAnimType.FilamentWave,
            amplitude = amplitude,
            frequency = frequency,
            scalePulse = scalePulse,
            brightnessPulse = 0f,
            phaseOffset = 0f,
            useUnscaledTime = false
        };
    }
}
