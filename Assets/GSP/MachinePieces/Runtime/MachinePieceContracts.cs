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
    public MachineConnection[] connections;
    public SerializableDictionary runtimeDefaults;
    public SerializableDictionary metadata;
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
