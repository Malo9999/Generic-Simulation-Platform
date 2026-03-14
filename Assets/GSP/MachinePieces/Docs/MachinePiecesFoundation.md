# Machine Pieces Foundation (v1)

## What this is
Shared, reusable, physics-agnostic machine-piece data/runtime foundation for standalone bootstrap simulations.

## Locked JSON contracts (v1)
- PieceSpec
- PieceInstance
- **CompoundPieceSpec**
- **CompoundModuleInstance**
- MachineRecipe
- GeneratorPreset
- SurfaceProfile

## Why CompoundPieces exist
Compound pieces sit between high-level machine recipes and low-level atomic piece specs.
They enable reusable, dimensionable machine modules (hopper/chamber/splitter) so recipes can describe coherent machines without hand-placing every atomic wall/slope piece.

## PieceSpecs vs CompoundPieces
- **PieceSpec**: atomic, directly buildable geometry + anchors + mechanics descriptor.
- **CompoundPieceSpec**: reusable module schema with dimensions, typed ports, and deterministic build template that expands into multiple PieceInstance entries.

## Ports vs anchors
- **Anchors** belong to atomic PieceSpecs and represent local attachment points.
- **Ports** belong to CompoundPieceSpecs and represent typed, dimension-aware interfaces.
- Ports in v1 use locked profile shapes: `slot`, `rect`, `circle`, `tube`, `funnel`, `mouth`.

## Deterministic rebuild rule
Given the same inputs, output is identical every rebuild:
- same PieceSpec + PieceInstance + SurfaceProfile + MachineRecipe
- same CompoundPieceSpec + module params + module transform

Compound expansion has no hidden randomness. Module expansion order, template order, and generated instance IDs are deterministic.

## MachineRecipe usage (updated)
Recipes now support both:
- `pieces[]` for explicit atomic entries (existing path)
- `modules[]` for compound module instances (new path)

At build time:
1. Validate recipe + modules + ports + references.
2. Expand modules to atomic PieceInstances.
3. Merge expanded atomic pieces with explicit `pieces[]`.
4. Feed the resulting runtime recipe through the same PieceBuilder/MachineBuilder path.

## Future builder/generator use
The CompoundPieces layer is the contract boundary for future machine authoring tools and procedural generators:
- authoring tools will place and connect modules via ports
- generators will emit module graphs first, then resolve through deterministic expansion
- both prebuilt and generated machines converge on the same runtime atomic recipe path

## Machine Pieces Lab
Standalone bootstrap inspection lab that:
- loads PieceSpecs + SurfaceProfiles + CompoundPieceSpecs + sample MachineRecipe
- builds a machine
- draws debug visuals for shape/collision parity and anchors

## Piece Test Sim
Standalone bootstrap mechanics verification harness that:
- validates static piece builds
- verifies Gate/Flap state updates collision/shape representation
- exposes Bin capture zones
- reuses the same machine under at least two environment presets

## Adding new pieces safely
1. Add/extend atomic PieceSpec JSON files using supported pieceType and shape kind.
2. Add CompoundPieceSpec JSON that references valid atomic PieceSpec ids in `buildTemplate`.
3. Define typed ports with valid profile shapes and dimension-aware positions.
4. Add strict param constraints and deterministic buildTemplate transforms.
5. Validate through loader/validator before runtime build.
6. Verify in Machine Pieces Lab and Piece Test Sim.
