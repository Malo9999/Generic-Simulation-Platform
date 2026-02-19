# Generic Simulation Platform — Season 1 Spec (QUAD POC)

**Last updated:** 2026-02-19  
This Season 1 replaces “Genesis Arena” with an **extensive 4‑way Proof of Concept** to validate the platform, teach the workflow, and explore multiple “brain” approaches (rules + RL).

---

## 0) Season 1 Goal

Build enough hands-on experience and a reliable pipeline to start the **first real YouTube implementation cycle**.

Season 1 deliverable is not “one perfect sim”; it is a **repeatable end‑to‑end pipeline**:
**Scenario → Run (logs) → Highlights → Replay Render → Publish‑ready video**

---

## 1) The Four POC Simulations

### 1.1 Colony Sim (Ants + colony wars)
**Purpose:** validate factions/colonies, fields (pheromones), territory dynamics, war/skirmish primitives.  
**Core features:**
- Multiple colonies (teamId)
- Nest zones + food objects
- Carry/deposit loop (workers) + combat loop (soldiers)
- Pheromone field per colony (deposit/decay/diffuse)
- Simple war trigger (contested resources / border contact)

**Video angle:** “Colony A expands… Colony B adapts… border wars.”

---

### 1.2 Marble Race (solo + tournament)
**Purpose:** validate racing + tournaments + strong identity visuals at scale.  
**Core features:**
- Track/worldgen with checkpoints + laps
- Marbles with distinctive patterns (rim + swirl + stripes/dots)
- Race director modes (pack follow, leader follow, crash hotspots)
- Tournament mode (heats → finals), standings, fastest lap records

**Video angle:** “Heats, upsets, finals, champion marble.”

---

### 1.3 RaceCar (with learning across episodes)
**Purpose:** validate Brain interface + training loop + policy persistence across races.  
**Core features:**
- Time trial first (single car learns the track)
- Then multi-car races (optional) with evaluation runs frozen
- Policy checkpoints saved per “week”
- Season results: wins, poles, fastest laps, points table

**Video angle:** “Week 1 chaos → Week 10 mastery → season champion.”

---

### 1.4 Fantasy Custom Sports Game (fictional)
**Purpose:** validate “game rules as a Simulation module”, team play, scoreboards, draft/season orchestration.  
**Core features:**
- Fictional teams + fictional players (no real IP)
- Field + goals/zones + ball object
- Simple positions/roles (runner, blocker, thrower) as **visual + stats**, not lore
- Match clock, scoring events, basic league table
- Optional: “draft” as a meta-system (team chooses from generated player pools)

**Video angle:** “Invented sport, rival teams, playoffs.”

---

## 2) Shared Platform Deliverables (must work for all 4)

### 2.1 Config-driven bootstrap
- One Bootstrap scene
- Load `scenario_*.json` and seed
- Generate world + spawn entities

### 2.2 Recording & replay (non-negotiable)
- Event log + sampled state snapshots
- Deterministic replay (or stable keyframes)
- Ability to render multiple cuts from the same run

### 2.3 Director + broadcast UI
- Automated camera + minimap
- Event ticker + small stats panel
- Highlight queue (goals, crashes, wars, record laps, etc.)
- Director profiles: at least **2 styles** (data-forward vs story-forward)

### 2.4 Visual system
- Archetype silhouettes (ant / marble / car / player) that read instantly
- Identity encoding: team/colony + individual pattern + role/status glyph
- Scale profiles per Simulation (sports small cast vs biome/ants large cast)

### 2.5 Brain framework
- `RuleBrain` baseline for all sims
- `PolicyBrain` hook for RaceCar training (and future sims)
- Policies are external artifacts (checkpoint id in Run metadata)

---

## 3) Definition of Done (Season 1 POC)

### Platform “Done”
- New Simulation can be added by creating a module folder + configs (no scene editing)
- Runs are reproducible from config + seed
- Recording + replay stable for at least 10+ minute runs
- 1080p MP4 export pipeline working (no PNG sequences by default)

### Content “Done”
For each of the 4 sims:
- At least **one** publishable 60–180s “pilot clip” rendered from logs
- At least **one** highlight type that clearly works (wars, crashes, goals, etc.)

For RaceCar specifically:
- Demonstrate improvement across episodes (checkpointed learning)

---

## 4) Practical Build Order (fastest path to visible results)

### Milestone M0 — Repo & folders (1–2 hours)
- Project on SSD (fast `Library/`)
- `Runs/`, `Exports/`, `Archives/`, `ML/` junctioned to HDD
- GitHub clean: `Runs/` and exports ignored

### Milestone M1 — SimCore skeleton
- Deterministic tick loop + RNG service (seeded)
- WorldState + AgentState + SpatialHash neighbor queries

### Milestone M2 — Config + WorldGen v1
- Load configs, generate bounds/walls/obstacles/zones
- Spawn entities from config

### Milestone M3 — Recording/Replay v1
- Events (JSONL) + snapshots (sampled)
- Replay runner that can render from log

### Milestone M4 — Director/Overlay v1
- Follow target + hotspot camera
- Minimap + ticker + small stats panel
- Highlight queue (minimal)

### Milestone M5 — 4 Simulation modules
- Ants: nests + pheromones + carry + basic combat
- Marbles: checkpoints + laps + results table + tournament runner
- RaceCar: track + time trial + reward shaping hooks
- Fantasy sport: ball + goals + match clock + scoring

### Milestone M6 — POC pilots
- Record one compelling run per Simulation
- Render 1080p MP4 pilots with clean broadcast overlay

---

## 5) Manual Setup Checklist (Unity + repo)

### Unity (minimal manual work)
- 2D URP + Pixel Perfect Camera (reference 480×270, PPU ~32)
- One Bootstrap scene with a single Bootstrapper object
- Ensure Game view can render exact 1080p (4× upscale)

### Repo structure (recommended)
- `Assets/_Bootstrap/`
- `Assets/SimCore/`
- `Assets/Simulations/<Name>/`
- `Assets/Presentation/`
- `Assets/Tools/`
- `Configs/` (repo root)
- `Runs/`, `Exports/`, `Archives/`, `ML/` (gitignored; may be junctions)

---

## 6) Codex Plan (small parallel tasks)

**Rule:** tasks should be independently mergeable and easy to review.

### Track A — Foundations
A1. Deterministic RNG service + seed plumbing  
A2. Fixed-step tick loop (SimClock)  
A3. SpatialHash neighbor query utility  
A4. Data models: WorldState, AgentState, ObjectState

### Track B — Config & WorldGen
B1. Config loader (scenario/world/simulation/director)  
B2. WorldGen v1 (bounds, walls, obstacles, zones)  
B3. Spawner system (entities from config)

### Track C — Recording & Replay
C1. Event logger (JSONL) + metadata writer  
C2. Snapshot sampler (every N ticks)  
C3. Replay runner (playback + render)

### Track D — Presentation
D1. Archetype renderer framework (multi-layer sprites/shapes)  
D2. Identity encoding (team color + pattern + glyph)  
D3. Director v1 (camera follow + hotspot)  
D4. Broadcast UI v1 (minimap, stats, ticker)

### Track E — Simulations
E1. Ants module (carry/deposit + pheromones + combat stub)  
E2. Marble race module (checkpoints + laps + results)  
E3. RaceCar module (time trial + reward hooks + evaluation mode)  
E4. Fantasy sport module (ball + goals + clock + scoring)

### Track F — Learning (RaceCar first)
F1. Brain interface + RuleBrain baseline  
F2. PolicyBrain stub (load policy checkpoint id)  
F3. Training harness skeleton (episode runner + checkpoint save)  
F4. “Improvement across episodes” demo (time trial)

---

## 7) First Test Run (what to do once M3 exists)

1) Create `scenario_marble_pilot.json`  
2) Run for 2–3 minutes, record logs  
3) Replay render to MP4 at 1080p  
4) Confirm: the run replays identically from logs

This is the “pipeline proof”. Once this works, the rest becomes iteration.

---

## 8) Non-goals (Season 1 POC)
- Photorealism / complex lighting
- Huge ML training infrastructure upfront
- Full production seasons; Season 1 is learning and pipeline validation
