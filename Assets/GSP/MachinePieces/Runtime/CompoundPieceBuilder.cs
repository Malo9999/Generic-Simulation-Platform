using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class CompoundPieceBuilder
{
    private const float TransformEpsilon = 0.0001f;
    private const string LogPrefix = "[MachinePieces][CompoundSnap]";

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

        var resolvedModules = ResolveSnappedModules(recipe, library);

        foreach (var module in resolvedModules)
        {
            if (module == null)
            {
                continue;
            }

            foreach (var templatePiece in module.Spec.buildTemplate?.pieces ?? Array.Empty<CompoundTemplatePiece>())
            {
                if (templatePiece == null)
                {
                    continue;
                }

                var baseId = string.IsNullOrWhiteSpace(templatePiece.instanceId)
                    ? templatePiece.pieceId
                    : templatePiece.instanceId;
                var nextId = $"{module.Module.instanceId}__{baseId}";
                if (!usedInstanceIds.Add(nextId))
                {
                    throw new InvalidOperationException($"Module expansion produced duplicate piece instance id '{nextId}'.");
                }

                var localPosition = ResolveVector(templatePiece.positionNormalized, templatePiece.positionOffset, module.Width, module.Height);
                var scaledLocalPosition = ScaleVector(localPosition, module.Transform.scale);
                var finalPosition = module.Transform.position.ToVector2() + Rotate(scaledLocalPosition, module.Transform.rotation);

                var scale = ResolveScale(templatePiece.scaleNormalized, templatePiece.scaleOffset, module.Width, module.Height, module.Transform.scale);

                expandedPieces.Add(new PieceInstance
                {
                    instanceId = nextId,
                    pieceId = templatePiece.pieceId,
                    transform = new PieceTransform
                    {
                        position = new SerializableVector2 { x = finalPosition.x, y = finalPosition.y },
                        rotation = module.Transform.rotation + templatePiece.rotation,
                        scale = scale
                    },
                    surfaceOverride = string.IsNullOrWhiteSpace(module.Module.surfaceOverride)
                        ? templatePiece.surfaceOverride
                        : module.Module.surfaceOverride,
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
            modules = resolvedModules.Select(x => new CompoundModuleInstance
            {
                instanceId = x.Module.instanceId,
                compoundPieceId = x.Module.compoundPieceId,
                transform = CloneTransform(x.Transform),
                paramOverrides = x.Module.paramOverrides,
                surfaceOverride = x.Module.surfaceOverride,
                metadata = x.Module.metadata
            }).ToArray(),
            moduleConnections = recipe.moduleConnections,
            connections = recipe.connections,
            runtimeDefaults = recipe.runtimeDefaults,
            metadata = recipe.metadata
        };
    }

    private static List<ResolvedModule> ResolveSnappedModules(MachineRecipe recipe, MachinePieceLibrary library)
    {
        var modules = recipe.modules ?? Array.Empty<CompoundModuleInstance>();
        var moduleById = new Dictionary<string, ResolvedModule>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.instanceId) || string.IsNullOrWhiteSpace(module.compoundPieceId))
            {
                continue;
            }

            if (!library.CompoundPieceSpecs.TryGetValue(module.compoundPieceId, out var spec))
            {
                continue;
            }

            var resolvedDimensions = ResolveDimensions(spec, module);
            var transform = module.transform ?? new PieceTransform { scale = new SerializableVector2 { x = 1f, y = 1f } };
            transform.scale = EnsureScale(transform.scale);

            var resolved = new ResolvedModule(module, spec, transform, resolvedDimensions.width, resolvedDimensions.height);
            moduleById[module.instanceId] = resolved;

            Debug.Log($"{LogPrefix} module '{module.instanceId}' dims resolved width={resolved.Width:F3}, height={resolved.Height:F3}, authoredPos={resolved.Transform.position.x:F3},{resolved.Transform.position.y:F3}, rot={resolved.Transform.rotation:F2}");
        }

        if (moduleById.Count == 0)
        {
            return new List<ResolvedModule>();
        }

        var connectionsByTarget = BuildConnectionsByTarget(recipe.moduleConnections ?? Array.Empty<MachineModuleConnection>(), moduleById);

        foreach (var kv in connectionsByTarget)
        {
            var target = kv.Key;
            var incoming = kv.Value;
            if (incoming.Count == 1)
            {
                continue;
            }

            var candidate = ComputeCandidateTransform(moduleById, incoming[0]);
            for (var i = 1; i < incoming.Count; i++)
            {
                var other = ComputeCandidateTransform(moduleById, incoming[i]);
                if (!AreEquivalent(candidate, other))
                {
                    throw new InvalidOperationException($"Multiple conflicting incoming snaps for module '{target}'.");
                }
            }

            moduleById[target].Transform = candidate;
            Debug.Log($"{LogPrefix} module '{target}' has {incoming.Count} consistent incoming snaps; resolved to shared transform.");
        }

        var unresolved = new HashSet<string>(moduleById.Keys, StringComparer.Ordinal);
        foreach (var moduleId in moduleById.Keys)
        {
            if (!connectionsByTarget.ContainsKey(moduleId))
            {
                unresolved.Remove(moduleId);
            }
        }

        var stabilized = true;
        while (unresolved.Count > 0 && stabilized)
        {
            stabilized = false;
            foreach (var targetId in unresolved.OrderBy(x => x, StringComparer.Ordinal).ToArray())
            {
                var incoming = connectionsByTarget[targetId];
                var sourceId = incoming[0].fromModuleId;
                if (unresolved.Contains(sourceId))
                {
                    continue;
                }

                var snapped = ComputeCandidateTransform(moduleById, incoming[0]);
                moduleById[targetId].Transform = snapped;
                unresolved.Remove(targetId);
                stabilized = true;

                Debug.Log($"{LogPrefix} snapped '{incoming[0].toModuleId}:{incoming[0].toPortId}' to '{incoming[0].fromModuleId}:{incoming[0].fromPortId}' => pos={snapped.position.x:F3},{snapped.position.y:F3}, rot={snapped.rotation:F2}");
            }
        }

        if (unresolved.Count > 0)
        {
            throw new InvalidOperationException($"Unable to resolve deterministic module snapping. Cyclic or unresolved dependencies: {string.Join(", ", unresolved.OrderBy(x => x, StringComparer.Ordinal))}.");
        }

        return modules
            .Where(m => m != null && !string.IsNullOrWhiteSpace(m.instanceId) && moduleById.ContainsKey(m.instanceId))
            .Select(m => moduleById[m.instanceId])
            .ToList();
    }

    private static Dictionary<string, List<MachineModuleConnection>> BuildConnectionsByTarget(
        IEnumerable<MachineModuleConnection> connections,
        IReadOnlyDictionary<string, ResolvedModule> moduleById)
    {
        var result = new Dictionary<string, List<MachineModuleConnection>>(StringComparer.Ordinal);

        foreach (var c in (connections ?? Array.Empty<MachineModuleConnection>())
                     .Where(x => x != null)
                     .OrderBy(x => x.toModuleId, StringComparer.Ordinal)
                     .ThenBy(x => x.toPortId, StringComparer.Ordinal)
                     .ThenBy(x => x.fromModuleId, StringComparer.Ordinal)
                     .ThenBy(x => x.fromPortId, StringComparer.Ordinal))
        {
            if (!moduleById.TryGetValue(c.fromModuleId ?? string.Empty, out var fromModule) ||
                !moduleById.TryGetValue(c.toModuleId ?? string.Empty, out var toModule))
            {
                continue;
            }

            var fromPort = GetPortOrThrow(fromModule, c.fromPortId, "source");
            var toPort = GetPortOrThrow(toModule, c.toPortId, "target");
            EnsurePortCompatibility(c, fromPort, toPort);

            if (!result.TryGetValue(c.toModuleId, out var incoming))
            {
                incoming = new List<MachineModuleConnection>();
                result[c.toModuleId] = incoming;
            }

            incoming.Add(c);
        }

        foreach (var kv in result)
        {
            Debug.Log($"{LogPrefix} target module '{kv.Key}' has {kv.Value.Count} incoming module connection(s).");
        }

        return result;
    }

    private static PieceTransform ComputeCandidateTransform(IReadOnlyDictionary<string, ResolvedModule> moduleById, MachineModuleConnection connection)
    {
        var fromModule = moduleById[connection.fromModuleId];
        var toModule = moduleById[connection.toModuleId];

        var fromPort = GetPortOrThrow(fromModule, connection.fromPortId, "source");
        var toPort = GetPortOrThrow(toModule, connection.toPortId, "target");

        var sourceWorld = ResolvePortWorld(fromModule, fromPort);
        var targetLocal = ResolvePortLocal(toModule, toPort);

        var desiredTargetDirection = -sourceWorld.Direction;
        if (desiredTargetDirection.sqrMagnitude <= TransformEpsilon)
        {
            desiredTargetDirection = targetLocal.Direction;
        }

        var targetRotatedDirection = Rotate(targetLocal.Direction, toModule.Transform.rotation);
        var deltaRotation = SignedAngleDegrees(targetRotatedDirection, desiredTargetDirection);
        var finalRotation = NormalizeAngle(toModule.Transform.rotation + deltaRotation);

        var rotatedTargetLocal = Rotate(targetLocal.Position, finalRotation);
        var finalPosition = sourceWorld.Position - rotatedTargetLocal;

        return new PieceTransform
        {
            position = new SerializableVector2 { x = finalPosition.x, y = finalPosition.y },
            rotation = finalRotation,
            scale = toModule.Transform.scale
        };
    }

    private static ResolvedPortWorld ResolvePortWorld(ResolvedModule module, CompoundPort port)
    {
        var local = ResolvePortLocal(module, port);
        var worldPosition = module.Transform.position.ToVector2() + Rotate(local.Position, module.Transform.rotation);
        var worldDirection = Rotate(local.Direction, module.Transform.rotation).normalized;

        Debug.Log($"{LogPrefix} port world '{module.Module.instanceId}:{port.id}' pos={worldPosition.x:F3},{worldPosition.y:F3} dir={worldDirection.x:F3},{worldDirection.y:F3} profile={port.profile?.shape}");

        return new ResolvedPortWorld(worldPosition, worldDirection, port.profile);
    }

    private static ResolvedPortLocal ResolvePortLocal(ResolvedModule module, CompoundPort port)
    {
        var position = ResolveVector(port.position?.normalized ?? default, port.position?.offset ?? default, module.Width, module.Height);
        position = ScaleVector(position, module.Transform.scale);

        var direction = port.direction.ToVector2();
        if (direction.sqrMagnitude <= TransformEpsilon)
        {
            throw new InvalidOperationException($"Port direction is zero for '{module.Module.instanceId}:{port.id}'.");
        }

        var scaledDirection = ScaleVector(direction, module.Transform.scale).normalized;
        return new ResolvedPortLocal(position, scaledDirection);
    }

    private static CompoundPort GetPortOrThrow(ResolvedModule module, string portId, string role)
    {
        var port = (module.Spec.ports ?? Array.Empty<CompoundPort>())
            .FirstOrDefault(p => p != null && string.Equals(p.id, portId, StringComparison.Ordinal));

        if (port == null)
        {
            throw new InvalidOperationException($"Unknown {role} port id '{portId}' on module '{module.Module.instanceId}'.");
        }

        return port;
    }

    private static void EnsurePortCompatibility(MachineModuleConnection connection, CompoundPort fromPort, CompoundPort toPort)
    {
        if (!IsCompatibleKind(fromPort.kind, toPort.kind))
        {
            throw new InvalidOperationException($"Incompatible port kinds for module connection '{connection.fromModuleId}:{connection.fromPortId}' -> '{connection.toModuleId}:{connection.toPortId}'.");
        }

        var fromShape = fromPort.profile?.shape ?? string.Empty;
        var toShape = toPort.profile?.shape ?? string.Empty;
        if (!string.Equals(fromShape, toShape, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Incompatible port profiles for module connection '{connection.fromModuleId}:{connection.fromPortId}' -> '{connection.toModuleId}:{connection.toPortId}' (shape '{fromShape}' vs '{toShape}').");
        }

        if (!AreProfilesSizeCompatible(fromPort.profile, toPort.profile))
        {
            throw new InvalidOperationException($"Incompatible port profile sizes for module connection '{connection.fromModuleId}:{connection.fromPortId}' -> '{connection.toModuleId}:{connection.toPortId}'.");
        }

        var fromSemantics = fromPort.semantics ?? string.Empty;
        var toSemantics = toPort.semantics ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fromSemantics) && !string.IsNullOrWhiteSpace(toSemantics) && !string.Equals(fromSemantics, toSemantics, StringComparison.Ordinal))
        {
            Debug.Log($"{LogPrefix} semantics mismatch tolerated for '{connection.fromModuleId}:{connection.fromPortId}' -> '{connection.toModuleId}:{connection.toPortId}' ({fromSemantics} vs {toSemantics}).");
        }
    }

    private static bool IsCompatibleKind(string fromKind, string toKind)
    {
        var src = fromKind ?? string.Empty;
        var dst = toKind ?? string.Empty;

        if (string.Equals(src, "flow_out", StringComparison.Ordinal) && string.Equals(dst, "flow_in", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(src, "material_out", StringComparison.Ordinal) && string.Equals(dst, "material_in", StringComparison.Ordinal))
        {
            return true;
        }

        if (!src.EndsWith("_out", StringComparison.Ordinal) || !dst.EndsWith("_in", StringComparison.Ordinal))
        {
            return false;
        }

        var srcPrefix = src.Substring(0, src.Length - 4);
        var dstPrefix = dst.Substring(0, dst.Length - 3);
        return string.Equals(srcPrefix, dstPrefix, StringComparison.Ordinal);
    }

    private static bool AreProfilesSizeCompatible(CompoundPortProfile from, CompoundPortProfile to)
    {
        if (from == null || to == null)
        {
            return false;
        }

        if (string.Equals(from.shape, "circle", StringComparison.Ordinal) || string.Equals(from.shape, "tube", StringComparison.Ordinal))
        {
            return Mathf.Abs(from.radius - to.radius) <= Mathf.Max(0.001f, from.radius * 0.15f);
        }

        var widthMatch = Mathf.Abs(from.width - to.width) <= Mathf.Max(0.001f, from.width * 0.2f);
        var heightMatch = Mathf.Abs(from.height - to.height) <= Mathf.Max(0.001f, from.height * 0.2f);
        return widthMatch && heightMatch;
    }

    private static bool AreEquivalent(PieceTransform a, PieceTransform b)
    {
        var deltaPos = (a.position.ToVector2() - b.position.ToVector2()).magnitude;
        var deltaRot = Mathf.Abs(Mathf.DeltaAngle(a.rotation, b.rotation));
        var deltaScale = (a.scale.ToVector2() - b.scale.ToVector2()).magnitude;
        return deltaPos <= 0.01f && deltaRot <= 0.1f && deltaScale <= 0.001f;
    }

    private static float SignedAngleDegrees(Vector2 from, Vector2 to)
    {
        var safeFrom = from.sqrMagnitude <= TransformEpsilon ? Vector2.up : from.normalized;
        var safeTo = to.sqrMagnitude <= TransformEpsilon ? Vector2.up : to.normalized;
        return Vector2.SignedAngle(safeFrom, safeTo);
    }

    private static float NormalizeAngle(float degrees)
    {
        var angle = degrees % 360f;
        return angle < 0f ? angle + 360f : angle;
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

    private static Vector2 ScaleVector(Vector2 value, SerializableVector2 scale)
    {
        var sx = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);
        var sy = Mathf.Approximately(scale.y, 0f) ? 1f : Mathf.Abs(scale.y);
        return new Vector2(value.x * sx, value.y * sy);
    }

    private static SerializableVector2 ResolveScale(SerializableVector2 normalized, SerializableVector2 offset, float width, float height, SerializableVector2 moduleScale)
    {
        var rawX = normalized.x * width + offset.x;
        var rawY = normalized.y * height + offset.y;

        var sx = Mathf.Max(0.001f, rawX * (Mathf.Approximately(moduleScale.x, 0f) ? 1f : Mathf.Abs(moduleScale.x)));
        var sy = Mathf.Max(0.001f, rawY * (Mathf.Approximately(moduleScale.y, 0f) ? 1f : Mathf.Abs(moduleScale.y)));
        return new SerializableVector2 { x = sx, y = sy };
    }

    private static SerializableVector2 EnsureScale(SerializableVector2 scale)
    {
        return new SerializableVector2
        {
            x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y
        };
    }

    private static PieceTransform CloneTransform(PieceTransform transform)
    {
        var safe = transform ?? new PieceTransform();
        return new PieceTransform
        {
            position = safe.position,
            rotation = safe.rotation,
            scale = EnsureScale(safe.scale)
        };
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var c = Mathf.Cos(radians);
        var s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private sealed class ResolvedModule
    {
        public ResolvedModule(CompoundModuleInstance module, CompoundPieceSpec spec, PieceTransform transform, float width, float height)
        {
            Module = module;
            Spec = spec;
            Transform = CloneTransform(transform);
            Width = width;
            Height = height;
        }

        public CompoundModuleInstance Module { get; }
        public CompoundPieceSpec Spec { get; }
        public float Width { get; }
        public float Height { get; }
        public PieceTransform Transform { get; set; }
    }

    private readonly struct ResolvedPortLocal
    {
        public ResolvedPortLocal(Vector2 position, Vector2 direction)
        {
            Position = position;
            Direction = direction;
        }

        public Vector2 Position { get; }
        public Vector2 Direction { get; }
    }

    private readonly struct ResolvedPortWorld
    {
        public ResolvedPortWorld(Vector2 position, Vector2 direction, CompoundPortProfile profile)
        {
            Position = position;
            Direction = direction;
            Profile = profile;
        }

        public Vector2 Position { get; }
        public Vector2 Direction { get; }
        public CompoundPortProfile Profile { get; }
    }
}
