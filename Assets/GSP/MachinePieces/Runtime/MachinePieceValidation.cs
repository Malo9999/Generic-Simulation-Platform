using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class MachinePieceValidation
{
    private static readonly HashSet<string> SupportedPieceTypes = new(StringComparer.Ordinal)
    {
        "Wall", "Slope", "Gate", "Flap", "Bin"
    };

    private static readonly HashSet<string> SupportedShapeKinds = new(StringComparer.Ordinal)
    {
        "box", "polygon", "segment"
    };

    public static List<string> ValidatePieceSpec(PieceSpec spec)
    {
        var errors = new List<string>();
        if (spec == null)
        {
            errors.Add("PieceSpec is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(spec.id)) errors.Add("PieceSpec.id is required.");
        if (spec.version < 1) errors.Add($"PieceSpec '{spec.id}' version must be >=1.");
        if (!SupportedPieceTypes.Contains(spec.pieceType ?? string.Empty)) errors.Add($"PieceSpec '{spec.id}' unsupported pieceType '{spec.pieceType}'.");

        var shapeKind = spec.shape?.kind;
        if (!SupportedShapeKinds.Contains(shapeKind ?? string.Empty))
        {
            errors.Add($"PieceSpec '{spec.id}' unsupported shape.kind '{shapeKind}'.");
        }

        ValidateShape(spec.id, spec.shape, errors);
        ValidateMechanics(spec, errors);

        if (spec.anchors == null || spec.anchors.Length == 0)
        {
            errors.Add($"PieceSpec '{spec.id}' requires at least one anchor.");
        }

        if (spec.surface == null || string.IsNullOrWhiteSpace(spec.surface.surfaceProfileId))
        {
            errors.Add($"PieceSpec '{spec.id}' surface.surfaceProfileId is required.");
        }

        return errors;
    }

    public static List<string> ValidateSurfaceProfile(SurfaceProfile profile)
    {
        var errors = new List<string>();
        if (profile == null)
        {
            errors.Add("SurfaceProfile is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(profile.id)) errors.Add("SurfaceProfile.id is required.");
        if (profile.version < 1) errors.Add($"SurfaceProfile '{profile.id}' version must be >=1.");
        if (string.IsNullOrWhiteSpace(profile.shaderKey)) errors.Add($"SurfaceProfile '{profile.id}' shaderKey is required.");
        if (string.IsNullOrWhiteSpace(profile.surfaceType)) errors.Add($"SurfaceProfile '{profile.id}' surfaceType is required.");
        return errors;
    }

    public static List<string> ValidateMachineRecipe(MachineRecipe recipe, MachinePieceLibrary lib)
    {
        var errors = new List<string>();
        if (recipe == null)
        {
            errors.Add("MachineRecipe is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(recipe.id)) errors.Add("MachineRecipe.id is required.");
        var pieces = recipe.pieces ?? Array.Empty<PieceInstance>();

        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in pieces)
        {
            if (instance == null)
            {
                errors.Add("MachineRecipe has null piece instance.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(instance.instanceId)) errors.Add("PieceInstance.instanceId is required.");
            else if (!instanceIds.Add(instance.instanceId)) errors.Add($"Duplicate PieceInstance.instanceId '{instance.instanceId}'.");

            if (string.IsNullOrWhiteSpace(instance.pieceId) || !lib.PieceSpecs.ContainsKey(instance.pieceId))
            {
                errors.Add($"Unknown piece id '{instance.pieceId}' in instance '{instance.instanceId}'.");
                continue;
            }

            var spec = lib.PieceSpecs[instance.pieceId];
            var anchors = spec.anchors ?? Array.Empty<PieceAnchor>();
            if (anchors.Length == 0) errors.Add($"PieceSpec '{spec.id}' used by '{instance.instanceId}' has missing anchors.");

            var constraints = spec.paramConstraints ?? Array.Empty<ParamConstraint>();
            var overrides = instance.paramOverrides?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var c in constraints)
            {
                if (!overrides.TryGetValue(c.key, out var valueText)) continue;
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    errors.Add($"Bad param override '{c.key}' on '{instance.instanceId}': '{valueText}' is not numeric.");
                    continue;
                }

                if (value < c.min || value > c.max)
                {
                    errors.Add($"Bad param override '{c.key}' on '{instance.instanceId}': {value} outside [{c.min}, {c.max}].");
                }
            }

            var surfaceId = string.IsNullOrWhiteSpace(instance.surfaceOverride)
                ? spec.surface?.surfaceProfileId
                : instance.surfaceOverride;
            if (string.IsNullOrWhiteSpace(surfaceId) || !lib.SurfaceProfiles.ContainsKey(surfaceId))
            {
                errors.Add($"Missing surface key for '{instance.instanceId}'. Resolved='{surfaceId}'.");
            }
        }

        ValidateConnections(recipe, lib, errors);
        return errors;
    }

    public static List<string> ValidateGeneratorPreset(GeneratorPreset preset, MachinePieceLibrary lib)
    {
        var errors = new List<string>();
        if (preset == null)
        {
            errors.Add("GeneratorPreset is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(preset.id)) errors.Add("GeneratorPreset.id is required.");

        foreach (var pieceId in preset.allowedPieceIds ?? Array.Empty<string>())
        {
            if (!lib.PieceSpecs.ContainsKey(pieceId))
            {
                errors.Add($"GeneratorPreset '{preset.id}' invalid piece reference '{pieceId}'.");
            }
        }

        foreach (var range in preset.paramRanges ?? Array.Empty<ParamRange>())
        {
            if (string.IsNullOrWhiteSpace(range.key)) errors.Add($"GeneratorPreset '{preset.id}' has paramRange with empty key.");
            if (range.min > range.max) errors.Add($"GeneratorPreset '{preset.id}' paramRange '{range.key}' min > max.");
        }

        return errors;
    }

    private static void ValidateConnections(MachineRecipe recipe, MachinePieceLibrary lib, List<string> errors)
    {
        var byId = (recipe.pieces ?? Array.Empty<PieceInstance>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.instanceId))
            .ToDictionary(x => x.instanceId, StringComparer.Ordinal);

        foreach (var c in recipe.connections ?? Array.Empty<MachineConnection>())
        {
            if (c == null)
            {
                errors.Add("MachineRecipe has null connection.");
                continue;
            }

            if (!byId.TryGetValue(c.fromInstanceId ?? string.Empty, out var fromInstance) ||
                !byId.TryGetValue(c.toInstanceId ?? string.Empty, out var toInstance))
            {
                errors.Add($"Bad machine connection '{c.fromInstanceId}:{c.fromAnchorId}' -> '{c.toInstanceId}:{c.toAnchorId}' references unknown instance.");
                continue;
            }

            if (!lib.PieceSpecs.TryGetValue(fromInstance.pieceId ?? string.Empty, out var fromSpec))
            {
                errors.Add($"Bad machine connection '{c.fromInstanceId}:{c.fromAnchorId}' -> '{c.toInstanceId}:{c.toAnchorId}' references unknown piece id '{fromInstance.pieceId}' on instance '{fromInstance.instanceId}'.");
                continue;
            }

            if (!lib.PieceSpecs.TryGetValue(toInstance.pieceId ?? string.Empty, out var toSpec))
            {
                errors.Add($"Bad machine connection '{c.fromInstanceId}:{c.fromAnchorId}' -> '{c.toInstanceId}:{c.toAnchorId}' references unknown piece id '{toInstance.pieceId}' on instance '{toInstance.instanceId}'.");
                continue;
            }

            if (!HasAnchor(fromSpec, c.fromAnchorId)) errors.Add($"Bad machine connection missing from-anchor '{c.fromAnchorId}' on '{c.fromInstanceId}'.");
            if (!HasAnchor(toSpec, c.toAnchorId)) errors.Add($"Bad machine connection missing to-anchor '{c.toAnchorId}' on '{c.toInstanceId}'.");
        }
    }

    private static bool HasAnchor(PieceSpec spec, string anchorId)
    {
        return (spec.anchors ?? Array.Empty<PieceAnchor>()).Any(a => string.Equals(a.id, anchorId, StringComparison.Ordinal));
    }

    private static void ValidateShape(string id, PieceShape shape, List<string> errors)
    {
        if (shape == null)
        {
            errors.Add($"PieceSpec '{id}' shape is required.");
            return;
        }

        switch (shape.kind)
        {
            case "box":
                if (shape.size.x <= 0f || shape.size.y <= 0f) errors.Add($"PieceSpec '{id}' box shape requires positive size.");
                break;
            case "polygon":
                if (shape.points == null || shape.points.Length < 3) errors.Add($"PieceSpec '{id}' polygon shape requires >=3 points.");
                break;
            case "segment":
                if ((shape.end.ToVector2() - shape.start.ToVector2()).sqrMagnitude <= 0.000001f) errors.Add($"PieceSpec '{id}' segment shape start/end cannot match.");
                break;
        }
    }

    private static void ValidateMechanics(PieceSpec spec, List<string> errors)
    {
        if (spec.mechanics == null)
        {
            errors.Add($"PieceSpec '{spec.id}' mechanics is required.");
            return;
        }

        var mode = spec.mechanics.mode ?? string.Empty;
        if (spec.pieceType == "Gate" && !string.Equals(mode, "slide", StringComparison.Ordinal))
        {
            errors.Add($"PieceSpec '{spec.id}' invalid mechanics payload for Gate; expected mode='slide'.");
        }

        if (spec.pieceType == "Flap" && !string.Equals(mode, "hinge", StringComparison.Ordinal))
        {
            errors.Add($"PieceSpec '{spec.id}' invalid mechanics payload for Flap; expected mode='hinge'.");
        }

        if (spec.pieceType == "Bin" && !string.Equals(mode, "capture", StringComparison.Ordinal))
        {
            errors.Add($"PieceSpec '{spec.id}' invalid mechanics payload for Bin; expected mode='capture'.");
        }

        if ((spec.pieceType == "Wall" || spec.pieceType == "Slope") && !string.Equals(mode, "static", StringComparison.Ordinal))
        {
            errors.Add($"PieceSpec '{spec.id}' invalid mechanics payload for {spec.pieceType}; expected mode='static'.");
        }
    }
}
