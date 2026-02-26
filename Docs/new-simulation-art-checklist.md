# New Simulation Art Checklist

## VisualKey contract
- `entityId` is the stable entity **type** for art lookup (for example: `"ant"`, `"marble"`, `"car"`, `"athlete"`).
- `instanceId` is the unique per-entity id within the run. It is for identity/debugging and should not drive content lookup.
- `variantSeed` controls visual variation (species/skin/style). If omitted, set it to `instanceId`.
- `kind` and `state` select clips/poses within the type.

## Recommended creation path
Use `VisualKeyBuilder.Create(...)` to keep defaults consistent (`state = "idle"`, `variantSeed = instanceId`).

### Example 1: car entity
```csharp
var key = VisualKeyBuilder.Create(
    simulationId: "RaceCar",
    entityType: "car",
    instanceId: identity.entityId,
    kind: identity.role,
    state: "drive");
```

### Example 2: ant entity with explicit variant override
```csharp
var key = VisualKeyBuilder.Create(
    simulationId: "AntColonies",
    entityType: "ant",
    instanceId: ant.id,
    kind: ant.identity.role,
    state: "walk",
    variantSeed: ant.speciesId);
```
