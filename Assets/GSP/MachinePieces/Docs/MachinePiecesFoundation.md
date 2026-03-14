# Machine Pieces Foundation (v1)

## What this is
Shared, reusable, physics-agnostic machine-piece data/runtime foundation for standalone bootstrap simulations.

## Locked JSON contracts (v1)
- PieceSpec
- PieceInstance
- MachineRecipe
- GeneratorPreset
- SurfaceProfile

## Deterministic rebuild rule
Given the same PieceSpec + PieceInstance + SurfaceProfile + MachineRecipe inputs, the runtime machine rebuild is identical every time.

## Physics-agnostic rule
Machine pieces define geometry, anchors, mechanics descriptors, and surface/material hints only.
Simulation-specific physics behavior belongs in simulation harness code.

## Generated vs Prebuilt unification
Both generated and prebuilt machine definitions resolve to the same runtime `MachineRecipe` path consumed by `MachineBuilder`.

## Machine Pieces Lab
Standalone bootstrap inspection lab that:
- loads PieceSpecs + SurfaceProfiles + sample MachineRecipe
- builds a machine
- draws debug visuals for shape/collision parity and anchors

## Piece Test Sim
Standalone bootstrap mechanics verification harness that:
- validates static piece builds
- verifies Gate/Flap state updates collision/shape representation
- exposes Bin capture zones
- reuses the same machine under at least two environment presets

## Adding new pieces safely
1. Add a new PieceSpec JSON file with supported pieceType and shape kind.
2. Add strict param constraints and mechanics payload mode.
3. Validate content through loader/validator before runtime build.
4. Reuse existing SurfaceProfile or add a new profile.
5. Verify in Machine Pieces Lab and Piece Test Sim.
