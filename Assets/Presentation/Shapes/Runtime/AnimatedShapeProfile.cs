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
    public float rotationSpeedDeg;
    public float orbitRadius = 0.08f;
    public bool useUnscaledTime;
    public bool enableAutoPhase = true;

    public static AnimatedShapeProfile CreateForShapeId(string shapeId)
    {
        if (string.IsNullOrWhiteSpace(shapeId))
        {
            return CreateDefault(ShapeAnimType.None);
        }

        return shapeId switch
        {
            ShapeId.DotCore => CreateDefault(ShapeAnimType.Breathe),
            ShapeId.DotGlow => CreateDefault(ShapeAnimType.GlowPulse),
            ShapeId.DotGlowSmall => CreateDefault(ShapeAnimType.Flicker),
            ShapeId.RingPing => CreateDefault(ShapeAnimType.Ripple),
            ShapeId.PulseRing => CreateDefault(ShapeAnimType.Ripple),
            ShapeId.OrganicAmoeba => CreateDefault(ShapeAnimType.AmoebaWobble),
            ShapeId.OrganicMetaball => CreateDefault(ShapeAnimType.AmoebaWobble, amplitude: 0.02f),
            ShapeId.NoiseBlob => CreateDefault(ShapeAnimType.Drift),
            ShapeId.FieldBlob => CreateDefault(ShapeAnimType.GlowPulse),
            ShapeId.TriangleAgent => CreateDefault(ShapeAnimType.Spin),
            ShapeId.DiamondAgent => CreateDefault(ShapeAnimType.Spin),
            ShapeId.ArrowAgent => CreateDefault(ShapeAnimType.Spin),
            ShapeId.LineSegment => CreateDefault(ShapeAnimType.None),
            ShapeId.StrokeScribble => CreateDefault(ShapeAnimType.FilamentWave),
            ShapeId.Filament => CreateDefault(ShapeAnimType.FilamentWave),
            ShapeId.CrossMarker => CreateDefault(ShapeAnimType.Flicker),
            ShapeId.ArcSector => CreateDefault(ShapeAnimType.Drift),
            _ => CreateDefault(ShapeAnimType.None)
        };
    }

    public static AnimatedShapeProfile CreateDefault(ShapeAnimType animType, float amplitude = -1f)
    {
        var profile = new AnimatedShapeProfile { animType = animType, enableAutoPhase = true };

        switch (animType)
        {
            case ShapeAnimType.None:
                profile.amplitude = 0f;
                profile.frequency = 1f;
                profile.scalePulse = 0f;
                profile.brightnessPulse = 0f;
                profile.rotationSpeedDeg = 0f;
                profile.orbitRadius = 0.08f;
                break;
            case ShapeAnimType.AmoebaWobble:
                profile.amplitude = amplitude >= 0f ? amplitude : 0.04f;
                profile.frequency = 1.6f;
                profile.scalePulse = 0.02f;
                profile.brightnessPulse = 0f;
                break;
            case ShapeAnimType.GlowPulse:
                profile.amplitude = 0f;
                profile.frequency = 1.4f;
                profile.scalePulse = 0.08f;
                profile.brightnessPulse = 0.18f;
                break;
            case ShapeAnimType.FilamentWave:
                profile.amplitude = 0.03f;
                profile.frequency = 1.2f;
                profile.scalePulse = 0.01f;
                profile.brightnessPulse = 0f;
                break;
            case ShapeAnimType.Spin:
                profile.amplitude = 0f;
                profile.frequency = 0.8f;
                profile.rotationSpeedDeg = 18f;
                break;
            case ShapeAnimType.Drift:
                profile.amplitude = 0.025f;
                profile.frequency = 0.9f;
                break;
            case ShapeAnimType.Flicker:
                profile.frequency = 3f;
                profile.brightnessPulse = 0.12f;
                break;
            case ShapeAnimType.Breathe:
                profile.frequency = 1.1f;
                profile.scalePulse = 0.035f;
                break;
            case ShapeAnimType.Ripple:
                profile.frequency = 1.8f;
                profile.scalePulse = 0.14f;
                profile.brightnessPulse = 0.10f;
                break;
            case ShapeAnimType.Orbit:
                profile.frequency = 0.7f;
                profile.orbitRadius = 0.09f;
                break;
        }

        return profile;
    }

    public static AnimatedShapeProfile CreateAmoebaWobble(float amplitude = 0.04f, float frequency = 1.6f, float scalePulse = 0.02f)
    {
        var profile = CreateDefault(ShapeAnimType.AmoebaWobble, amplitude);
        profile.frequency = frequency;
        profile.scalePulse = scalePulse;
        return profile;
    }

    public static AnimatedShapeProfile CreateGlowPulse(float frequency = 1.4f, float scalePulse = 0.08f, float brightnessPulse = 0.18f)
    {
        var profile = CreateDefault(ShapeAnimType.GlowPulse);
        profile.frequency = frequency;
        profile.scalePulse = scalePulse;
        profile.brightnessPulse = brightnessPulse;
        return profile;
    }

    public static AnimatedShapeProfile CreateFilamentWave(float amplitude = 0.03f, float frequency = 1.2f, float scalePulse = 0.01f)
    {
        var profile = CreateDefault(ShapeAnimType.FilamentWave);
        profile.amplitude = amplitude;
        profile.frequency = frequency;
        profile.scalePulse = scalePulse;
        return profile;
    }
}
