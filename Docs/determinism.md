# Deterministic RNG in SimCore

SimCore runtime randomness flows through `RngService` and `IRng`.

## Rules
- Seed the run once via `RngService.SetGlobal(seed)` (done in bootstrap).
- Never use `UnityEngine.Random` or `System.Random` in simulation/worldgen logic.
- Use stable substreams with `RngService.Fork("<SALT>")`.
- Do not share one stream between unrelated systems.

## Recommended salt naming
- `WORLD:OBSTACLES`
- `WORLD:GROUND_TILES`
- `WORLD:ZONES`
- `SPAWN:<entityId>`
- `DECOR:GRASS`
- `DECOR:ROCKS`
- `SIM:<simulationId>:<subsystem>`

## Adding a new RNG consumer
1. Pick a stable salt string that is specific to the subsystem.
2. Create an RNG with `var rng = RngService.Fork("...")`.
3. Keep all random calls in that subsystem on that stream.
4. If you need additional independent randomness, fork another explicit salt.

## Quick self-check
Use `RngService.BuildSignature(seed)` and compare output between runs:
- same seed => same signature
- different seed => different signature
