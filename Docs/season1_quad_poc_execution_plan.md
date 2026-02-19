# Season 1 QUAD POC — Execution Plan (Codex + GitHub)

**Last updated:** 2026-02-19  
This is the practical “do this next” plan to execute the Season 1 QUAD POC with small parallel Codex tasks and clean GitHub merges.

---

## 0) One-time preparation (Windows)

### 0.1 Put the Unity project on SSD
- Keep the Unity project repo on your 200GB SSD so Unity `Library/` stays fast.

### 0.2 Put growing data on HDD (recommended)
Create these folders on HDD:
- `Runs/` — run logs + metadata
- `Exports/` — MP4 renders
- `Archives/` — keepers
- `ML/` — future training outputs

Then junction them into the repo (PowerShell; example paths):
```powershell
# HDD targets
mkdir E:\GSP_Data\Runs
mkdir E:\GSP_Data\Exports
mkdir E:\GSP_Data\Archives
mkdir E:\GSP_Data\ML

# repo on SSD
cd D:\GSP

cmd /c mklink /J Runs     E:\GSP_Data\Runs
cmd /c mklink /J Exports  E:\GSP_Data\Exports
cmd /c mklink /J Archives E:\GSP_Data\Archives
cmd /c mklink /J ML       E:\GSP_Data\ML
```

### 0.3 Git hygiene
- Ensure `Runs/`, `Exports/`, `Archives/`, and `ML/` are in `.gitignore`.

---

## 1) Repo structure to create now (minimal)

- `Assets/_Bootstrap/Bootstrap.unity`
- `Assets/SimCore/`
- `Assets/Simulations/AntColonies/`
- `Assets/Simulations/MarbleRace/`
- `Assets/Simulations/RaceCar/`
- `Assets/Simulations/FantasySport/`
- `Assets/Presentation/`
- `Assets/Tools/`
- `Configs/` (repo root)

---

## 2) GitHub workflow (keeps parallel Codex tasks safe)

### Branching
- `main` = always playable
- `feat/<area>-<short>` for each task (small PRs)

### PR rules
- One task per PR
- Must include:
  - “How to test” steps
  - A small demo scenario config if relevant
- Merge only when Unity compiles + Play mode runs the Bootstrap scene.

---

## 3) Codex task format (copy/paste template)

Use this exact structure to get good results from Codex:

**Task title:**  
**Goal:** (one sentence)  
**Scope:** (files/folders touched)  
**Constraints:** (Unity 6000.3 LTS, 2D URP, Pixel Perfect, no scene hand-edit except Bootstrap, etc.)  
**Acceptance tests:** (what you’ll do in Unity to confirm)  
**Non-goals:** (explicitly out of scope)

---

## 4) Parallel task plan (first wave)

### Wave 1 (core foundations — do these first)
**A1. Deterministic RNG + seed plumbing**  
Acceptance: running the same seed produces identical agent spawn positions + obstacle layout.

**A2. Fixed-step SimClock**  
Acceptance: sim ticks at fixed dt; can run faster/slower without changing logic.

**B1. Config loader (scenario/world/simulation/director)**  
Acceptance: Bootstrap can load `Configs/scenario_*.json` and print parsed values.

**B2. WorldGen v1 (bounds/walls/obstacles/zones)**  
Acceptance: config changes regenerate the world without scene edits.

**C1. Event logger + run metadata**  
Acceptance: after play, `Runs/<run_id>/metadata.json` and `events.jsonl` exist.

These five tasks are safe to run in parallel.

---

## 5) Second wave (pipeline unlock)

**C2. Snapshot sampler**  
Acceptance: `states.bin` (or `states.jsonl`) is created and has multiple samples.

**C3. Replay runner**  
Acceptance: you can replay a recorded run and see the same motion paths.

**D3. Director v1 camera follow + hotspot**  
Acceptance: camera follows leader/target and occasionally switches to events.

**D4. Broadcast UI v1**  
Acceptance: minimap + stats + ticker display without clutter.

---

## 6) Third wave (simulation modules)

Start with “thin” versions; expand later.

**E2. MarbleRace module (recommended first)**
- simplest to validate: checkpoints + laps + results

**E1. AntColonies module**
- nests + food + pheromone field + skirmish stub

**E3. RaceCar module**
- time trial baseline + reward hooks

**E4. FantasySport module**
- ball + goals + clock + scoring

---

## 7) Learning wave (RaceCar only, after replay works)

**F1. Brain interface + RuleBrain**
- all sims can run with RuleBrain

**F3. Training harness skeleton**
- headless episode runner that outputs policy checkpoints

**F4. Improvement demo**
- show “episode 1” vs “episode 20” lap time improvement

---

## 8) First end-to-end test run (the pipeline proof)

Once WorldGen + Recording + Replay exist:

1) Create `Configs/scenario_marble_pilot.json`  
2) Run 2–3 minutes and record logs  
3) Replay and verify motion is identical  
4) Render 1080p MP4 (4× upscale) to `Exports/<run_id>/pilot.mp4`

If this works, the platform is real.

---

## 9) What to do if you feel stuck
- Don’t jump to RL early.
- Prioritize: **WorldGen → Record/Replay → Director → then RL**
- A working replay pipeline makes everything (including YouTube) dramatically easier.
