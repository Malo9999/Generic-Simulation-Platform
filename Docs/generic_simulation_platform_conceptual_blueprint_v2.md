# GENERIC SIMULATION PLATFORM — CONCEPTUAL BLUEPRINT (v2)

**Last updated:** 2026-02-19  
**Project goal:** Build a **reusable Unity-based 2D top‑down simulation platform** for AI‑driven simulations suitable for YouTube content.

This blueprint is the *foundation document*. Items marked **Locked** should not change unless you explicitly decide to.

---

## 0) Core Promise

One shared engine powers many **Simulations** (ecosystems, races, sports, battles, colony sims).  
A Simulation produces a **Run** (truth + logs). Runs can be replayed and rendered into different YouTube “broadcast styles”.

**Core loop:**
**Config + Seed → Sim runs → Logs → Replay + Director → Video**

---

## 1) Locked Principles

### 1.1 Truth first
- **Simulation is the source of truth.**
- Presentation can *interpret* (camera, overlays, highlight selection) but never changes outcomes.

### 1.2 Emergence over scripts
- No hardcoded “evolution paths” or “guaranteed story beats”.
- Interesting outcomes emerge from simple primitives + pressure + time.

### 1.3 Readability at tiny scale
- Many small competitors in big arenas.
- Visual clarity beats realism.
- Prefer stable silhouettes and clean motion over busy textures.

### 1.4 Separation of concerns
- **SimCore** (truth) is independent from:
  - **Presentation** (broadcast, visuals)
  - **Tooling** (record/replay/export)
  - **Learning/Training** (RL, policy updates)

---

## 2) Terminology (use these words consistently)

- **Engine / SimCore:** shared runtime systems (tick loop, movement, spatial queries, recording, replay)
- **Simulation:** a pluggable “package” (rules + entity set + worldgen defaults + theme + director defaults)
- **Scenario:** a specific configuration of a Simulation (configs + seed)
- **Run / Match:** one execution of a Scenario (produces logs + results)
- **Season / Tournament:** orchestration of multiple runs with standings/leaderboards
- **Brain:** agent decision system (rules, scripted, RL policy, neuroevolution)

> Note: “League” is a category label (sports), but the platform term is **Simulation**.

---

## 3) Platform Architecture

### 3.1 Core layers

**A) SimCore (shared)**
- Deterministic tick loop (fixed step)
- World state (bounds, obstacles, zones, fields)
- Agents (state, energy/health, traits/genome, team/faction id optional)
- Interaction primitives (pickup, deposit, collide, contest, score hooks)
- Recording (events + snapshots)
- Replay (deterministic playback)

**B) Simulation Modules (pluggable)**
Each Simulation defines:
- Ruleset (how the primitives become “a game”)
- Entities (agent archetypes + objects)
- WorldGen defaults (arena + obstacles + zones + fields)
- Theme (visual grammar + palettes + identity encoding)
- Director defaults (camera + overlays + highlight heuristics)

**C) Presentation (shared)**
- Pixel‑perfect rendering pipeline
- Archetype renderers (ants, marbles, cars, players, etc.)
- Broadcast UI (minimap, ticker, stats)
- Director (camera + highlight queue + overlays)

**D) Learning/Training (optional but supported)**
- Training harness runs episodes headless/fast
- Produces policy checkpoints
- Evaluation runs are frozen for fairness and recording

### 3.2 Determinism & reproducibility (mandatory)
Every Run must be reproducible from:
- Scenario configs + seed
- engine version/hash
- simulation version/hash
- optional policy checkpoint id

---

## 4) Data & Config Model

### 4.1 Config files (recommended)
- `simulation_<name>.json` — ruleset + entities + defaults
- `world_<name>.json` — arena generation, obstacles, zones, fields
- `director_<profile>.json` — camera + overlays + highlight tuning
- `scenario_<name>.json` — references the above + seed + run parameters

### 4.2 Run artifacts (gitignored)
- `Runs/<run_id>/metadata.json` (seed, configs used, hashes, duration, stats)
- `Runs/<run_id>/events.jsonl` (or binary) — births, deaths, scores, collisions, etc.
- `Runs/<run_id>/states.bin` (sampled snapshots)
- `Runs/<run_id>/results.json` — standings, champions, records
- `Exports/<run_id>/*.mp4` — rendered cuts

---

## 5) Agent “Brains” and Improvement

### 5.1 Brain interface (platform requirement)
Agents choose actions through a Brain abstraction:
- **RuleBrain:** hand-authored, simple heuristics (great for baseline + POC)
- **ScriptBrain:** deterministic behaviors for testing
- **PolicyBrain:** ML policy (future: ONNX/inference, or in‑process)
- **HybridBrain:** rules for safety + policy for strategy

### 5.2 Two improvement mechanisms (both supported)
- **Evolution:** mutates traits/genomes across reproduction
- **Learning (RL/other):** improves policy across episodes (checkpoints)

They can be used separately or together:
- Evolution changes “bodies/traits”
- Learning changes “brains/policies”

### 5.3 Training across episodes (season learning)
Typical pattern:
- Episode/run → collect rewards → update policy → next run
- For content: show “Week 1 chaos → Week 10 mastery”
- For fairness: evaluation races/matches run with learning **disabled** (policies frozen)

---

## 6) Visual System (tiny-scale, but recognizable)

### 6.1 Pixel pipeline (recommended)
- Internal reference: **480×270** with Pixel Perfect Camera
- Export: **1080p = 4× integer upscale** (1920×1080)
- Optional: 4K exports for better YouTube compression

### 6.2 Visual grammar (two-layer model)
1) **Archetype silhouette** (what it is): ant vs marble vs car vs player
2) **Identity encoding** (who it is): team/colony + individual pattern + role/status glyph

Rules:
- Each archetype gets 2–3 unique silhouette cues (no ambiguity)
- Identity uses 2 channels minimum (color + pattern/glyph) for compression/colorblind safety
- Avoid busy micro-textures; prefer stable shapes

### 6.3 Scale profiles per Simulation
Each Simulation declares a ScaleProfile:
- target on‑screen count (sports: small, biome: huge)
- base agent pixel size (hero vs crowd tiers)
- camera zoom range
- overlay policy (scoreboard vs heatmaps)
- LOD tiers (hero/crowd/aggregate)

---

## 7) World Generation (code-driven)

Scenes are thin bootstraps; worlds are generated from configs + seed.

WorldGen inputs:
- arena size/bounds/walls
- obstacle generation (density, clusters, corridors)
- zones (goals, nests, spawn areas)
- fields (food, friction, hazard, pheromones per colony, etc.)
- optional schedules (seasons/shocks) that pressure, not script winners

---

## 8) YouTube Content Pipeline (repeatable)

1) **Pitch** (what viewers will see)
2) **Scenario config** (seed + world + simulation + director profiles)
3) **Run** (record logs)
4) **Summarize** (stats + highlight candidates)
5) **Replay render** (Channel profiles / broadcast styles)
6) **Edit/publish** (titles, chapters, thumbnails)

Key unlock: **Log-first** workflow (store truth, re-render later).

---

## 9) Storage & Performance (practical)

### 9.1 SSD vs HDD (recommended)
- SSD: Unity project + `Library/` (speed)
- HDD: `Runs/`, `Exports/`, `Archives/`, `ML/` outputs (space)

Use junctions on Windows to keep repo clean while storing big folders on HDD.

### 9.2 Avoid disk blowups
- Default export: **MP4**, not PNG sequences
- Keep only “best-of” exports locally; logs are cheap

---

## 10) Roadmap Strategy

Start with a multi-simulation POC that validates:
- modular Simulation packages
- config-driven worldgen
- record/replay
- director + broadcast UI
- at least one “learning across episodes” demo (RL or other)

Then commit to the first “real” YouTube season once the pipeline is proven.
