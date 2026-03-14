using System;
using System.Collections.Generic;
using UnityEngine;

public enum MachinePieceType
{
    Wall,
    Slope,
    Gate,
    Flap,
    Bin
}

public enum MachineShapeKind
{
    box,
    polygon,
    segment
}

[Serializable]
public sealed class PieceSpec
{
    public string id;
    public int version;
    public string pieceType;
    public string displayName;
    public string[] tags;
    public PieceShape shape;
    public PieceCollision collision;
    public PieceAnchor[] anchors;
    public PieceSurfaceRef surface;
    public SerializableDictionary defaultParams;
    public ParamConstraint[] paramConstraints;
    public PieceMechanics mechanics;
}

[Serializable]
public sealed class PieceInstance
{
    public string instanceId;
    public string pieceId;
    public PieceTransform transform;
    public SerializableDictionary paramOverrides;
    public string surfaceOverride;
    public SerializableDictionary stateDefaults;
}

[Serializable]
public sealed class MachineRecipe
{
    public string id;
    public int version;
    public string displayName;
    public string machineType;
    public RecipeBounds bounds;
    public PieceInstance[] pieces;
    public CompoundModuleInstance[] modules;
    public MachineModuleConnection[] moduleConnections;
    public MachineConnection[] connections;
    public SerializableDictionary runtimeDefaults;
    public SerializableDictionary metadata;
}


[Serializable]
public sealed class CompoundPieceSpec
{
    public string id;
    public int version;
    public string displayName;
    public string compoundType;
    public CompoundDimensions dimensions;
    public SerializableDictionary defaultParams;
    public ParamConstraint[] paramConstraints;
    public CompoundPort[] ports;
    public CompoundBuildTemplate buildTemplate;
    public SerializableDictionary surfaceDefaults;
    public SerializableDictionary metadata;
}

[Serializable]
public sealed class CompoundModuleInstance
{
    public string instanceId;
    public string compoundPieceId;
    public PieceTransform transform;
    public SerializableDictionary paramOverrides;
    public string surfaceOverride;
    public SerializableDictionary metadata;
}

[Serializable]
public sealed class MachineModuleConnection
{
    public string fromModuleId;
    public string fromPortId;
    public string toModuleId;
    public string toPortId;
}

[Serializable]
public sealed class CompoundDimensions
{
    public DimensionConstraint width;
    public DimensionConstraint height;
}

[Serializable]
public sealed class DimensionConstraint
{
    public float min;
    public float max;
    public float defaultValue;
}

[Serializable]
public sealed class CompoundPort
{
    public string id;
    public string kind;
    public CompoundPortVector position;
    public SerializableVector2 direction;
    public CompoundPortProfile profile;
    public string semantics;
}

[Serializable]
public sealed class CompoundPortVector
{
    public SerializableVector2 normalized;
    public SerializableVector2 offset;
}

[Serializable]
public sealed class CompoundPortProfile
{
    public string shape;
    public float width;
    public float height;
    public float radius;
}

[Serializable]
public sealed class CompoundBuildTemplate
{
    public CompoundTemplatePiece[] pieces;
}

[Serializable]
public sealed class CompoundTemplatePiece
{
    public string instanceId;
    public string pieceId;
    public SerializableVector2 positionNormalized;
    public SerializableVector2 positionOffset;
    public float rotation;
    public SerializableVector2 scaleNormalized;
    public SerializableVector2 scaleOffset;
    public string surfaceOverride;
    public SerializableDictionary paramOverrides;
    public SerializableDictionary stateDefaults;
}

[Serializable]
public sealed class GeneratorPreset
{
    public string id;
    public int version;
    public string displayName;
    public string machineType;
    public string[] allowedPieceIds;
    public SerializableDictionary requiredTopology;
    public SerializableDictionary generationRules;
    public ParamRange[] paramRanges;
    public SerializableDictionary validationRules;
}

[Serializable]
public sealed class SurfaceProfile
{
    public string id;
    public int version;
    public string displayName;
    public string shaderKey;
    public string textureKey;
    public SerializableColor baseColor;
    public SerializableColor accentColor;
    public string finish;
    public string[] overlayKeys;
    public string surfaceType;
    public SerializableDictionary physicalHints;
}

[Serializable]
public sealed class PieceShape
{
    public string kind;
    public SerializableVector2 size;
    public SerializableVector2[] points;
    public SerializableVector2 start;
    public SerializableVector2 end;
}

[Serializable]
public sealed class PieceCollision
{
    public bool enabled = true;
    public bool isTrigger;
    public string mode;
    public SerializableDictionary payload;
}

[Serializable]
public sealed class PieceAnchor
{
    public string id;
    public SerializableVector2 position;
    public float angle;
}

[Serializable]
public sealed class PieceSurfaceRef
{
    public string surfaceProfileId;
}

[Serializable]
public sealed class PieceMechanics
{
    public string mode;
    public SerializableDictionary payload;
}

[Serializable]
public sealed class ParamConstraint
{
    public string key;
    public float min;
    public float max;
}

[Serializable]
public sealed class ParamRange
{
    public string key;
    public float min;
    public float max;
}

[Serializable]
public sealed class RecipeBounds
{
    public SerializableVector2 min;
    public SerializableVector2 max;
}

[Serializable]
public sealed class MachineConnection
{
    public string fromInstanceId;
    public string fromAnchorId;
    public string toInstanceId;
    public string toAnchorId;
}

[Serializable]
public sealed class PieceTransform
{
    public SerializableVector2 position;
    public float rotation;
    public SerializableVector2 scale;
}

[Serializable]
public struct SerializableVector2
{
    public float x;
    public float y;

    public Vector2 ToVector2() => new(x, y);
}

[Serializable]
public struct SerializableColor
{
    public float r;
    public float g;
    public float b;
    public float a;

    public Color ToColor() => new(r, g, b, a <= 0f ? 1f : a);
}

[Serializable]
public sealed class SerializableDictionary
{
    public SerializableKeyValue[] entries = Array.Empty<SerializableKeyValue>();

    public Dictionary<string, string> ToDictionary()
    {
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entries == null)
        {
            return output;
        }

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            output[entry.key] = entry.value ?? string.Empty;
        }

        return output;
    }
}

[Serializable]
public sealed class SerializableKeyValue
{
    public string key;
    public string value;
}
