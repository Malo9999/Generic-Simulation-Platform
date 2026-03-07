using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MotionShaderProfile", menuName = "Presentation/Rendering/Motion Shader Profile")]
public sealed class MotionShaderProfile : ScriptableObject
{
    [SerializeField] private MotionFamilyDefaults organicAmoeba = MotionFamilyDefaults.Create(MotionMotionMode.OrganicAmoeba, 0.015f, 1.1f, 0.007f, 0.52f, new Vector2(0.15f, 0.05f), 0.06f);
    [SerializeField] private MotionFamilyDefaults organicMetaball = MotionFamilyDefaults.Create(MotionMotionMode.OrganicMetaball, 0.011f, 0.72f, 0.006f, 0.33f, new Vector2(0.08f, 0.02f), 0.045f);
    [SerializeField] private MotionFamilyDefaults fieldBlob = MotionFamilyDefaults.Create(MotionMotionMode.FieldBlob, 0.009f, 0.43f, 0.005f, 0.21f, new Vector2(0.2f, 0.09f), 0.04f);
    [SerializeField] private MotionFamilyDefaults filament = MotionFamilyDefaults.Create(MotionMotionMode.Filament, 0.012f, 1.45f, 0.004f, 0.78f, new Vector2(1f, 0.12f), 0.035f);

    private static MotionShaderProfile runtimeFallback;

    public MotionFamilyDefaults GetDefaults(MotionFamily family)
    {
        return family switch
        {
            MotionFamily.OrganicAmoeba => organicAmoeba,
            MotionFamily.OrganicMetaball => organicMetaball,
            MotionFamily.FieldBlob => fieldBlob,
            MotionFamily.Filament => filament,
            _ => organicAmoeba
        };
    }

    public static MotionShaderProfile LoadRuntimeProfile()
    {
        var loaded = Resources.Load<MotionShaderProfile>("MotionShaderProfile");
        if (loaded != null)
        {
            return loaded;
        }

        if (runtimeFallback == null)
        {
            runtimeFallback = CreateInstance<MotionShaderProfile>();
        }

        return runtimeFallback;
    }

    public static bool TryResolveFamily(string shapeId, out MotionFamily family)
    {
        family = MotionFamily.None;
        if (string.IsNullOrWhiteSpace(shapeId))
        {
            return false;
        }

        family = shapeId switch
        {
            ShapeId.OrganicAmoeba => MotionFamily.OrganicAmoeba,
            ShapeId.OrganicMetaball => MotionFamily.OrganicMetaball,
            ShapeId.FieldBlob => MotionFamily.FieldBlob,
            ShapeId.Filament => MotionFamily.Filament,
            _ => MotionFamily.None
        };

        return family != MotionFamily.None;
    }
}

public enum MotionFamily
{
    None = 0,
    OrganicAmoeba = 1,
    OrganicMetaball = 2,
    FieldBlob = 3,
    Filament = 4
}

public enum MotionMotionMode
{
    None = 0,
    OrganicAmoeba = 1,
    OrganicMetaball = 2,
    FieldBlob = 3,
    Filament = 4
}

[Serializable]
public struct MotionFamilyDefaults
{
    public MotionMotionMode mode;
    [Min(0f)] public float amplitude;
    [Min(0f)] public float frequency;
    [Min(0f)] public float secondaryAmplitude;
    [Min(0f)] public float secondaryFrequency;
    public Vector2 flowDirection;
    [Range(0f, 1f)] public float tintStrength;

    public static MotionFamilyDefaults Create(MotionMotionMode mode, float amplitude, float frequency, float secondaryAmplitude, float secondaryFrequency, Vector2 flowDirection, float tintStrength)
    {
        return new MotionFamilyDefaults
        {
            mode = mode,
            amplitude = amplitude,
            frequency = frequency,
            secondaryAmplitude = secondaryAmplitude,
            secondaryFrequency = secondaryFrequency,
            flowDirection = flowDirection,
            tintStrength = tintStrength
        };
    }
}
