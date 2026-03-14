using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class CompoundPieceBuilder
{
    public MachineRecipe ExpandToRuntimeRecipe(MachineRecipe recipe, MachinePieceLibrary library)
    {
        var expandedPieces = new List<PieceInstance>();
        var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var piece in recipe.pieces ?? Array.Empty<PieceInstance>())
        {
            if (piece == null)
            {
                continue;
            }

            expandedPieces.Add(piece);
            if (!string.IsNullOrWhiteSpace(piece.instanceId))
            {
                usedInstanceIds.Add(piece.instanceId);
            }
        }

        foreach (var module in recipe.modules ?? Array.Empty<CompoundModuleInstance>())
        {
            if (module == null || string.IsNullOrWhiteSpace(module.compoundPieceId))
            {
                continue;
            }

            if (!library.CompoundPieceSpecs.TryGetValue(module.compoundPieceId, out var spec))
            {
                continue;
            }

            var resolved = ResolveDimensions(spec, module);
            var moduleTransform = module.transform ?? new PieceTransform { scale = new SerializableVector2 { x = 1f, y = 1f } };
            var moduleRotation = moduleTransform.rotation;
            var modulePosition = moduleTransform.position.ToVector2();

            foreach (var templatePiece in spec.buildTemplate?.pieces ?? Array.Empty<CompoundTemplatePiece>())
            {
                if (templatePiece == null)
                {
                    continue;
                }

                var baseId = string.IsNullOrWhiteSpace(templatePiece.instanceId)
                    ? templatePiece.pieceId
                    : templatePiece.instanceId;
                var nextId = $"{module.instanceId}__{baseId}";
                if (!usedInstanceIds.Add(nextId))
                {
                    throw new InvalidOperationException($"Module expansion produced duplicate piece instance id '{nextId}'.");
                }

                var localPosition = ResolveVector(templatePiece.positionNormalized, templatePiece.positionOffset, resolved.width, resolved.height);
                var finalPosition = modulePosition + Rotate(localPosition, moduleRotation);

                var scale = ResolveScale(templatePiece.scaleNormalized, templatePiece.scaleOffset, resolved.width, resolved.height, moduleTransform.scale);

                expandedPieces.Add(new PieceInstance
                {
                    instanceId = nextId,
                    pieceId = templatePiece.pieceId,
                    transform = new PieceTransform
                    {
                        position = new SerializableVector2 { x = finalPosition.x, y = finalPosition.y },
                        rotation = moduleRotation + templatePiece.rotation,
                        scale = scale
                    },
                    surfaceOverride = string.IsNullOrWhiteSpace(module.surfaceOverride)
                        ? templatePiece.surfaceOverride
                        : module.surfaceOverride,
                    paramOverrides = templatePiece.paramOverrides,
                    stateDefaults = templatePiece.stateDefaults
                });
            }
        }

        return new MachineRecipe
        {
            id = recipe.id,
            version = recipe.version,
            displayName = recipe.displayName,
            machineType = recipe.machineType,
            bounds = recipe.bounds,
            pieces = expandedPieces.ToArray(),
            modules = recipe.modules,
            moduleConnections = recipe.moduleConnections,
            connections = recipe.connections,
            runtimeDefaults = recipe.runtimeDefaults,
            metadata = recipe.metadata
        };
    }

    private static (float width, float height) ResolveDimensions(CompoundPieceSpec spec, CompoundModuleInstance module)
    {
        var values = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["width"] = spec.dimensions?.width?.defaultValue ?? 1f,
            ["height"] = spec.dimensions?.height?.defaultValue ?? 1f
        };

        var defaults = spec.defaultParams?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in defaults)
        {
            if (float.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                values[kv.Key] = parsed;
            }
        }

        var overrides = module.paramOverrides?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in overrides)
        {
            if (float.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                values[kv.Key] = parsed;
            }
        }

        return (
            Mathf.Max(0.001f, values.TryGetValue("width", out var width) ? width : 1f),
            Mathf.Max(0.001f, values.TryGetValue("height", out var height) ? height : 1f));
    }

    private static Vector2 ResolveVector(SerializableVector2 normalized, SerializableVector2 offset, float width, float height)
    {
        return new Vector2(
            normalized.x * width + offset.x,
            normalized.y * height + offset.y);
    }

    private static SerializableVector2 ResolveScale(SerializableVector2 normalized, SerializableVector2 offset, float width, float height, SerializableVector2 moduleScale)
    {
        var rawX = normalized.x * width + offset.x;
        var rawY = normalized.y * height + offset.y;

        var sx = Mathf.Max(0.001f, rawX * (Mathf.Approximately(moduleScale.x, 0f) ? 1f : Mathf.Abs(moduleScale.x)));
        var sy = Mathf.Max(0.001f, rawY * (Mathf.Approximately(moduleScale.y, 0f) ? 1f : Mathf.Abs(moduleScale.y)));
        return new SerializableVector2 { x = sx, y = sy };
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var c = Mathf.Cos(radians);
        var s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
