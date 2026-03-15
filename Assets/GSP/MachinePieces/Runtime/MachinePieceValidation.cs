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

    private static readonly HashSet<string> SupportedPortProfileShapes = new(StringComparer.Ordinal)
    {
        "slot", "rect", "circle", "tube", "funnel", "mouth"
    };

    private static readonly HashSet<string> StandardCompoundPortIds = new(StringComparer.Ordinal)
    {
        "in_top", "in_bottom", "in_left", "in_right",
        "out_top", "out_bottom", "out_left", "out_right"
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

    public static List<string> ValidateCompoundPieceSpec(CompoundPieceSpec spec)
    {
        var errors = new List<string>();
        if (spec == null)
        {
            errors.Add("CompoundPieceSpec is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(spec.id)) errors.Add("CompoundPieceSpec.id is required.");
        if (spec.version < 1) errors.Add($"CompoundPieceSpec '{spec.id}' version must be >=1.");
        if (string.IsNullOrWhiteSpace(spec.displayName)) errors.Add($"CompoundPieceSpec '{spec.id}' displayName is required.");
        if (string.IsNullOrWhiteSpace(spec.compoundType)) errors.Add($"CompoundPieceSpec '{spec.id}' compoundType is required.");

        ValidateDimensions(spec, errors);
        ValidateCompoundPorts(spec, errors);

        var template = spec.buildTemplate;
        if (template == null || template.pieces == null || template.pieces.Length == 0)
        {
            errors.Add($"CompoundPieceSpec '{spec.id}' buildTemplate.pieces must contain at least one piece entry.");
        }
        else
        {
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in template.pieces)
            {
                if (piece == null)
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' buildTemplate has null piece.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(piece.pieceId))
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' buildTemplate entry has empty pieceId.");
                }

                if (piece.scaleOffset.x <= 0f && piece.scaleNormalized.x <= 0f)
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' template piece '{piece.instanceId}' must define positive X scale expression.");
                }

                if (piece.scaleOffset.y <= 0f && piece.scaleNormalized.y <= 0f)
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' template piece '{piece.instanceId}' must define positive Y scale expression.");
                }

                var key = string.IsNullOrWhiteSpace(piece.instanceId) ? piece.pieceId : piece.instanceId;
                if (!string.IsNullOrWhiteSpace(key) && !instanceIds.Add(key))
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' duplicate template instance id '{key}'.");
                }
            }
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
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateAtomicPieceInstances(recipe.pieces ?? Array.Empty<PieceInstance>(), lib, errors, instanceIds);
        ValidateModules(recipe, lib, errors, instanceIds);
        ValidateConnections(recipe, lib, errors);
        ValidateModuleConnections(recipe, lib, errors);

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

    private static void ValidateModules(MachineRecipe recipe, MachinePieceLibrary lib, List<string> errors, HashSet<string> usedInstanceIds)
    {
        var modules = recipe.modules ?? Array.Empty<CompoundModuleInstance>();
        var moduleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            if (module == null)
            {
                errors.Add("MachineRecipe has null module instance.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(module.instanceId))
            {
                errors.Add("CompoundModuleInstance.instanceId is required.");
            }
            else if (!moduleIds.Add(module.instanceId))
            {
                errors.Add($"Invalid module instance ids: duplicate '{module.instanceId}'.");
            }

            if (string.IsNullOrWhiteSpace(module.compoundPieceId) || !lib.CompoundPieceSpecs.TryGetValue(module.compoundPieceId, out var spec))
            {
                errors.Add($"Unknown compound piece id '{module.compoundPieceId}' in module '{module.instanceId}'.");
                continue;
            }

            var compoundErrors = ValidateCompoundPieceSpec(spec);
            foreach (var e in compoundErrors)
            {
                errors.Add($"Compound '{spec.id}' invalid: {e}");
            }

            foreach (var templatePiece in spec.buildTemplate?.pieces ?? Array.Empty<CompoundTemplatePiece>())
            {
                if (templatePiece == null)
                {
                    continue;
                }

                if (!lib.PieceSpecs.ContainsKey(templatePiece.pieceId ?? string.Empty))
                {
                    errors.Add($"Compound '{spec.id}' buildTemplate references unknown PieceSpec '{templatePiece.pieceId}'.");
                }

                var localId = string.IsNullOrWhiteSpace(templatePiece.instanceId) ? templatePiece.pieceId : templatePiece.instanceId;
                var expandedId = $"{module.instanceId}__{localId}";
                if (!usedInstanceIds.Add(expandedId))
                {
                    errors.Add($"Invalid module instance ids: expanded id collision '{expandedId}'.");
                }
            }

            ValidateParamOverrides(module, spec, errors);
        }
    }

    private static void ValidateParamOverrides(CompoundModuleInstance module, CompoundPieceSpec spec, List<string> errors)
    {
        var overrides = module.paramOverrides?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in spec.paramConstraints ?? Array.Empty<ParamConstraint>())
        {
            if (!overrides.TryGetValue(c.key, out var valueText))
            {
                continue;
            }

            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                errors.Add($"Invalid param override '{c.key}' on module '{module.instanceId}': '{valueText}' is not numeric.");
                continue;
            }

            if (value < c.min || value > c.max)
            {
                errors.Add($"Invalid param override '{c.key}' on module '{module.instanceId}': {value} outside [{c.min}, {c.max}].");
            }
        }

        if (overrides.TryGetValue("width", out var widthText) &&
            (!float.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) || width <= 0f))
        {
            errors.Add($"Invalid dimension values on module '{module.instanceId}': width must be > 0.");
        }

        if (overrides.TryGetValue("height", out var heightText) &&
            (!float.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var height) || height <= 0f))
        {
            errors.Add($"Invalid dimension values on module '{module.instanceId}': height must be > 0.");
        }
    }

    private static void ValidateAtomicPieceInstances(IEnumerable<PieceInstance> pieces, MachinePieceLibrary lib, List<string> errors, HashSet<string> instanceIds)
    {
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

    private static void ValidateModuleConnections(MachineRecipe recipe, MachinePieceLibrary lib, List<string> errors)
    {
        var moduleById = (recipe.modules ?? Array.Empty<CompoundModuleInstance>())
            .Where(m => m != null && !string.IsNullOrWhiteSpace(m.instanceId))
            .ToDictionary(m => m.instanceId, StringComparer.Ordinal);

        var incomingByTarget = new Dictionary<string, List<MachineModuleConnection>>(StringComparer.Ordinal);

        foreach (var c in recipe.moduleConnections ?? Array.Empty<MachineModuleConnection>())
        {
            if (c == null)
            {
                errors.Add("MachineRecipe has null module connection.");
                continue;
            }

            if (!moduleById.TryGetValue(c.fromModuleId ?? string.Empty, out var fromModule))
            {
                errors.Add($"Unknown source module id '{c.fromModuleId}' in module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}'.");
                continue;
            }

            if (!moduleById.TryGetValue(c.toModuleId ?? string.Empty, out var toModule))
            {
                errors.Add($"Unknown target module id '{c.toModuleId}' in module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}'.");
                continue;
            }

            if (!lib.CompoundPieceSpecs.TryGetValue(fromModule.compoundPieceId ?? string.Empty, out var fromSpec))
            {
                errors.Add($"Bad module connection source '{c.fromModuleId}' references unknown compound id '{fromModule.compoundPieceId}'.");
                continue;
            }

            if (!lib.CompoundPieceSpecs.TryGetValue(toModule.compoundPieceId ?? string.Empty, out var toSpec))
            {
                errors.Add($"Bad module connection target '{c.toModuleId}' references unknown compound id '{toModule.compoundPieceId}'.");
                continue;
            }

            var fromPort = (fromSpec.ports ?? Array.Empty<CompoundPort>()).FirstOrDefault(p => p != null && string.Equals(p.id, c.fromPortId, StringComparison.Ordinal));
            if (fromPort == null)
            {
                errors.Add(BuildMissingModulePortMessage(c.fromModuleId, c.fromPortId, fromSpec, "source"));
                continue;
            }

            var toPort = (toSpec.ports ?? Array.Empty<CompoundPort>()).FirstOrDefault(p => p != null && string.Equals(p.id, c.toPortId, StringComparison.Ordinal));
            if (toPort == null)
            {
                errors.Add(BuildMissingModulePortMessage(c.toModuleId, c.toPortId, toSpec, "target"));
                continue;
            }

            if (!AreCompatiblePortKinds(fromPort.kind, toPort.kind))
            {
                errors.Add($"Incompatible port kinds for module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}' ({fromPort.kind} -> {toPort.kind}).");
            }

            if (!string.Equals(fromPort.profile?.shape, toPort.profile?.shape, StringComparison.Ordinal))
            {
                errors.Add($"Incompatible port profiles for module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}' (shape '{fromPort.profile?.shape}' vs '{toPort.profile?.shape}').");
            }
            else if (!IsProfileSizeCompatible(fromPort.profile, toPort.profile))
            {
                errors.Add($"Incompatible port profile sizes for module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}'.");
            }

            var fromSemantics = fromPort.semantics ?? string.Empty;
            var toSemantics = toPort.semantics ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromSemantics) && !string.IsNullOrWhiteSpace(toSemantics) &&
                !string.Equals(fromSemantics, toSemantics, StringComparison.Ordinal))
            {
                errors.Add($"Incompatible port semantics for module connection '{c.fromModuleId}:{c.fromPortId}' -> '{c.toModuleId}:{c.toPortId}' ({fromSemantics} vs {toSemantics}).");
            }

            if (!incomingByTarget.TryGetValue(c.toModuleId, out var incoming))
            {
                incoming = new List<MachineModuleConnection>();
                incomingByTarget[c.toModuleId] = incoming;
            }

            incoming.Add(c);
        }

        foreach (var kv in incomingByTarget)
        {
            var targetId = kv.Key;
            var incoming = kv.Value;
            if (incoming.Count <= 1)
            {
                continue;
            }

            var uniqueSources = incoming
                .Select(x => $"{x.fromModuleId}:{x.fromPortId}->{x.toPortId}")
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (uniqueSources > 1)
            {
                errors.Add($"Multiple conflicting incoming snaps for module '{targetId}'.");
            }
        }
    }

    private static bool AreCompatiblePortKinds(string fromKind, string toKind)
    {
        var source = fromKind ?? string.Empty;
        var target = toKind ?? string.Empty;

        if (string.Equals(source, "flow_out", StringComparison.Ordinal) && string.Equals(target, "flow_in", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(source, "material_out", StringComparison.Ordinal) && string.Equals(target, "material_in", StringComparison.Ordinal))
        {
            return true;
        }

        if (!source.EndsWith("_out", StringComparison.Ordinal) || !target.EndsWith("_in", StringComparison.Ordinal))
        {
            return false;
        }

        var sourcePrefix = source.Substring(0, source.Length - 4);
        var targetPrefix = target.Substring(0, target.Length - 3);
        return string.Equals(sourcePrefix, targetPrefix, StringComparison.Ordinal);
    }

    private static bool IsProfileSizeCompatible(CompoundPortProfile from, CompoundPortProfile to)
    {
        if (from == null || to == null)
        {
            return false;
        }

        if (string.Equals(from.shape, "circle", StringComparison.Ordinal) || string.Equals(from.shape, "tube", StringComparison.Ordinal))
        {
            var tolerance = Mathf.Max(0.001f, from.radius * 0.15f);
            return Mathf.Abs(from.radius - to.radius) <= tolerance;
        }

        var widthTolerance = Mathf.Max(0.001f, from.width * 0.2f);
        var heightTolerance = Mathf.Max(0.001f, from.height * 0.2f);
        return Mathf.Abs(from.width - to.width) <= widthTolerance &&
               Mathf.Abs(from.height - to.height) <= heightTolerance;
    }

    private static bool HasAnchor(PieceSpec spec, string anchorId)
    {
        return (spec.anchors ?? Array.Empty<PieceAnchor>()).Any(a => string.Equals(a.id, anchorId, StringComparison.Ordinal));
    }

    private static void ValidateDimensions(CompoundPieceSpec spec, List<string> errors)
    {
        if (spec.dimensions == null)
        {
            errors.Add($"CompoundPieceSpec '{spec.id}' dimensions are required.");
            return;
        }

        ValidateDimension(spec.id, "width", spec.dimensions.width, errors);
        ValidateDimension(spec.id, "height", spec.dimensions.height, errors);
    }

    private static void ValidateDimension(string id, string key, DimensionConstraint dimension, List<string> errors)
    {
        if (dimension == null)
        {
            errors.Add($"CompoundPieceSpec '{id}' dimensions.{key} is required.");
            return;
        }

        if (dimension.min <= 0f) errors.Add($"CompoundPieceSpec '{id}' dimensions.{key}.min must be > 0.");
        if (dimension.max <= 0f) errors.Add($"CompoundPieceSpec '{id}' dimensions.{key}.max must be > 0.");
        if (dimension.min > dimension.max) errors.Add($"CompoundPieceSpec '{id}' dimensions.{key}.min > max.");
        if (dimension.defaultValue < dimension.min || dimension.defaultValue > dimension.max)
            errors.Add($"CompoundPieceSpec '{id}' dimensions.{key}.defaultValue must be inside [{dimension.min}, {dimension.max}].");
    }

    private static void ValidateCompoundPorts(CompoundPieceSpec spec, List<string> errors)
    {
        var ports = spec.ports ?? Array.Empty<CompoundPort>();
        if (ports.Length == 0)
        {
            errors.Add($"CompoundPieceSpec '{spec.id}' ports are required.");
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in ports)
        {
            if (port == null)
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' has null port.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(port.id)) errors.Add($"CompoundPieceSpec '{spec.id}' has port with empty id.");
            else if (!ids.Add(port.id)) errors.Add($"CompoundPieceSpec '{spec.id}' duplicate port id '{port.id}'.");
            else if (!StandardCompoundPortIds.Contains(port.id))
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' is non-standard. Allowed ids: {string.Join(", ", StandardCompoundPortIds.OrderBy(x => x, StringComparer.Ordinal))}.");
            }

            if (string.IsNullOrWhiteSpace(port.kind)) errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' missing kind.");
            if (port.position == null) errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' missing position.");

            var direction = port.direction.ToVector2();
            if (direction.sqrMagnitude <= 0.000001f)
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' direction must be non-zero.");
            }

            if (port.profile == null)
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' missing profile.");
                continue;
            }

            if (!SupportedPortProfileShapes.Contains(port.profile.shape ?? string.Empty))
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' invalid profile shape '{port.profile.shape}' on port '{port.id}'.");
            }

            if ((port.profile.shape == "circle" || port.profile.shape == "tube") && port.profile.radius <= 0f)
            {
                errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' requires positive profile.radius for shape '{port.profile.shape}'.");
            }

            if (port.profile.shape != "circle" && port.profile.shape != "tube")
            {
                if (port.profile.width <= 0f || port.profile.height <= 0f)
                {
                    errors.Add($"CompoundPieceSpec '{spec.id}' port '{port.id}' requires positive profile.width/profile.height.");
                }
            }
        }
    }

    private static string BuildMissingModulePortMessage(string moduleId, string portId, CompoundPieceSpec spec, string side)
    {
        var validPortIds = (spec.ports ?? Array.Empty<CompoundPort>())
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.id))
            .Select(p => p.id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var validText = validPortIds.Length == 0 ? "none" : string.Join(", ", validPortIds);
        var standardText = string.Join(", ", StandardCompoundPortIds.OrderBy(x => x, StringComparer.Ordinal));
        return $"Unknown {side} port id '{portId}' on module '{moduleId}' (compound '{spec.id}'). Valid ids on this module: [{validText}]. Standard ids: [{standardText}].";
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
