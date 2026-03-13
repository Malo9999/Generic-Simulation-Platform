# GranularFlow v1 Experimental Vertical Slice

## Purpose
GranularFlow is an **experimental**, isolated simulation module that demonstrates a satisfying top-down granular sorting machine loop using a GPU particle sim and CPU machine control.

## Architecture split
- **GPU**: particle positions, velocities, color ids, radius, gravity/damping integration, v1 machine collider response, render-source data.
- **CPU**: SplitterTower machine recipe/layout, actuator states (SlidingGate + DiverterFlap), sensor summaries, RuleBrain decisions, score/bin counts, config loading, run metadata/event hooks.

## v1 scope
- One hand-authored machine archetype: `SplitterTower`.
- One pilot scenario using config + seed startup.
- Two bins with visible routing behavior.
- One sim-local brain seam for future expansion:
  - `IGranularFlowBrain`
  - `GranularFlowBrainContext`
  - `GranularFlowBrainDecision`
  - `GranularFlowRuleBrain`

## Non-goals
- No platform-wide generic brain framework.
- No HybridBrain/PolicyBrain implementation.
- No second machine archetype.
- No training harness or cross-sim abstractions.

## How to test
1. Open Unity project and allow script compile.
2. Ensure `GranularFlow` is selected in launcher/bootstrap simulation picker.
3. Run scene and observe:
   - feeder spawns bright particles,
   - upper chamber accumulation,
   - sliding gate cadence,
   - diverter flap routing,
   - left/right bin fill trends.
4. Run twice with same seed and compare high-level behavior reproducibility.
