@page plan_loss_condition_pacing Loss Condition & Pacing

# Loss Condition & Pacing

> Turns the current lossless, content-rich sandbox into a game with **stakes** (a way
> to lose) and a **difficulty ramp** (it gets harder as you climb). Promotes the
> grid-encroachment idea from `PLAN-FutureIdeas.md` §12 into a concrete plan, grounded
> in the mechanics that already exist. No new art is required to start.

---

## Orientation — start here

**What this is:** a fail state (grid-encroachment loss) **plus** a level-range
difficulty/pacing system that turns the endless sandbox into a run-based game.

**Status:** design-complete, **not yet implemented** — this doc is the spec; no code exists.

**Decisions already locked** (don't re-litigate):
- Loss = **grid encroachment** — the board chokes up toward the thrower (balloons enter at
  the bottom, fill upward; saturation pushes the front back down toward the entry).
- Progression = **run-based** — loss resets level/score; only best-level/best-score persist.
- Pacing is authored as **level ranges**; a **`LevelDifficultyResolver` (`IActiveLevelParameters`)**
  is the single source the spawner/score/UI pull from; the existing configs demote to catalogs.
- **Per-level** resolution (no per-turn re-rolls); **weights static per range** (modes apply to
  scalars only); **allowed-color set changes only at level boundaries**.

**Where to look, in order:** Part A (loss) → Part B (level-range config + resolver) →
Part C (allowed colors) → Phasing. **Start implementing at Phase 1** (GameOver state +
run-scoped save) — it has no dependencies.

**External dependency:** the grid-actor per-range mix needs Phase 8.3 (procedural placement,
see `PLAN-GridActorExpansion.md`); everything else uses mechanics that exist today.

---

## Why now

The loop works and there's plenty of content (4 balloon types, Puff, Bush), but the game
**cannot be lost** and **plays identically at level 1 and level 20**. Adding more actors
to a no-stakes sandbox has diminishing returns; adding stakes + ramp is what makes the
existing content matter. Macro pacing (loss + ramp) is *not* blocked on Phase 8.3 — it
uses mechanics already in place.

---

## ⚠️ Decision that gates everything: runs vs. persistent progression

`ScoreController` currently **saves level + per-color score to `PlayerPrefs`** (on quit /
focus-loss) and restores them on launch — i.e. **endless persistent progression**. A loss
condition implies a **run** that can end. These are in tension and must be reconciled
*before* implementation:

| Model | On loss | Persistence | Feel |
|---|---|---|---|
| **Run-based** | reset level/score, start over | high score / best level only | arcade / roguelike |
| **Persistent + setback** | drop a level, or lose progress in current level | full progression saved | forgiving / casual |
| **Endless + soft fail** | "near miss" pushback, never a hard loss | full progression saved | zen / no stakes (status quo) |

**This choice changes the GameOver flow, the save logic, and the difficulty curve.**

**✅ DECIDED: run-based.** A run ends on loss and resets level + per-color score;
`ScoreController.Save()` becomes run-scoped (no cross-session level/score restore). Persist
only a **meta record** — best level and best total score — loaded for display, never fed
back into a run. The launcher starts every session at level 1.

---

## Current mechanics (grounding)

- Balloons **enter from the bottom** (thrower side) and **travel upward**, filling the grid
  from the top down (`row 0` = top / far from thrower; `row Rows-1` = bottom / entry).
  `BalloonSpawner.FindFirstReachableEmptyRow`.
- `BalloonBalancer` packs balloons **upward** (toward row 0) each turn. As the grid
  saturates, the lowest filled row **descends back toward the thrower**.
- Each turn (projectile death) spawns `BalloonsConfiguration.NewProjectileBalloonLines`
  lines; a post-spawn `BalanceBalloonsMessage` settles the grid.
- Level-up is per-color score bars meeting `GameConfiguration.PointsRequiredForLevel(level)`
  (already an exponential ramp). No fail state. Thrower already no-ops outside
  `NavigationState.Game`.

---

## Part A — Loss condition: grid encroachment

**Rule:** the grid chokes up toward the thrower; if balloons reach a deadline near the
entry, the run ends.

Two candidate triggers (pick one or combine):
1. **Deadline row** — after the post-spawn balance *settles*, if any actor occupies
   `row >= DeadlineRow` (a configurable row near the bottom), lose. Readable; can show a
   visible danger line.
2. **Spawn saturation** — a line spawn where `FindFirstReachableEmptyRow` returns null for
   the entry columns (grid genuinely can't accept the line). Organic; no magic row.

**Implementation sketch:**
- New `NavigationState.GameOver` (mirrors `LevelUp`; thrower already gates on `Game`).
- A `BreachDetector` (`IStartable`) subscribing to the **end-of-turn settle** (the existing
  post-spawn `BalanceBalloonsMessage` path, evaluated *after* balance completes, not mid-
  animation — avoids false positives). On breach → `Navigation.TransitionTo(GameOver)`.
- Config: `DeadlineRow` (or rows-from-bottom) on a new `DifficultyConfiguration` SO (Part B).
- **Gate against the cinematic:** do not fire GameOver while a level-up cinematic is
  playing (`Cinematic.IsPlaying` / `Navigation == LevelUp`); defer the check to the next
  settled turn. The cinematic + GameOver must never overlap.

**Tension source:** lines-per-turn vs. pop rate. `Unbreakable`/`Tough` accumulation
(they resist popping) naturally accelerates encroachment.

---

## Part B — Per-level-range difficulty configuration

Pacing is authored as **level ranges**. A range covers `[FromLevel, ToLevel]` and owns a
set of tunable parameters; the **final range is open-ended** (applies forever — the steady
"4 colors / settled difficulty" tail). Within a range, each parameter is a slider that
resolves by one of three modes, so a designer can say "ramp this up across the range" or
"keep it varied" without hand-authoring every level.

### Data model

- **`LevelRangeConfiguration`** (SO) — ordered, contiguous `LevelRange[]`; the last entry is
  open-ended. Injected via `GameLifetimeScope` like the other configs.
- **`LevelRange`** holds two kinds of parameter:
  - **Scalars** — `RangedValue`: `RangedInt SpawnLines` (lines per turn), item frequency,
    deadline pressure, threshold tuning, etc. "How many / how much" — where fixed/linear/random
    is meaningful.
  - **Weighted sets** — *static per range*: `WeightedSet<BalloonType>` (Simple / Tough /
    Unbreakable / BubbleCluster), `WeightedSet<GridActorType>` (Puff / Bush / … — consumed once
    8.3 lands), per-item weights. **No `RangedValue` modes on weights** — a weight *is* a
    distribution; the randomness is the per-spawn weighted draw, so a second random roll on the
    weight itself would be redundant. Want the mix to evolve? Author a finer range.
  - `ColorSet AllowedColors` — active palette subset (see **Part C**); changes only at a level
    boundary.
  - `FromLevel`, `ToLevel` (`ToLevel = ∞` for the tail).
- **`RangedValue<T>`** (`RangedInt`, `RangedFloat`) = `{ Min, Max, Mode }`, resolved **once per
  level** (no per-turn re-roll):
  - **Fixed** — constant `Min`.
  - **Linear** — lerp `Min→Max` by the level's position within the range (a ramp).
  - **Random** — pick within `[Min, Max]` once when the level begins; stable for that level.

### Resolver / mediator — single source of the live mix

The per-range parameters **replace** the weights that currently live in
`BalloonsConfiguration` / `ItemConfiguration` / `GridActorConfiguration`. To avoid two
sources of truth, one mediator owns the *resolved current-level parameters* and is the
**only** thing the runtime systems read for the live mix:

- **`LevelDifficultyResolver`** (`IStartable`, implements **`IActiveLevelParameters`**) —
  subscribes to `ScoreLevelUpMessage` (+ resolves at start). On each level change it finds the
  active `LevelRange`, resolves every `RangedValue` (fixed/linear/random), and caches the
  result. It also **merges with the catalogs** — range weights drive selection while prefab
  refs and caps still come from the base configs (the bridge function). Exposes read-only:
  - `int SpawnLines` (resolved once for the level)
  - `PickBalloonType()` / `PickGridActor()` — weighted draws from the range's static weighted sets, honoring catalog caps
  - `ItemSpawnSettings Items`
  - `IReadOnlyList<string> AllowedColors`
- **Consumers pull, not pushed** — `BalloonSpawner`, `ItemAssigner`, the Phase 8.3 grid
  spawner, `ScoreController`, and the color-bar UI inject `IActiveLevelParameters` and read the
  live values at spawn / level time. The resolver doesn't reference its consumers — looser
  coupling, and adding a consumer never touches the resolver.
- **Base configs demote to catalogs** — `BalloonsConfiguration` etc. keep only what isn't
  range-varied: prefab references, caps, and per-type base tuning (HP, VFX). **All weights,
  spawn-line counts, item frequency, and color sets move to `LevelRangeConfiguration`** and are
  resolved through the mediator — so *all* randomness/interpolation is authored in one config
  and resolved in one service.

```
LevelRangeConfiguration (authored: weights, ranges, colors) ─┐
BalloonsConfiguration / ItemConfiguration /                  ├─► LevelDifficultyResolver
GridActorConfiguration / GamePalette (catalog: prefabs,caps) ─┘     : IActiveLevelParameters
                                                                          ▲ pull
        BalloonSpawner · ItemAssigner · GridSpawner · ScoreController · ColorBar UI
```

### Resolved decisions

1. ✅ **Resolution cadence** — **per level**. Params resolve once when a level begins (random
   modes roll once, then stay fixed for the level); never re-rolled per turn.
2. ✅ **Weights are static per range** — `RangedValue` modes apply only to *scalars* (spawn
   lines, frequency, etc.). The per-spawn weighted draw is the randomness for the mix; evolve
   the mix by authoring finer ranges.
3. ⬜ **Range validation** (impl detail) — ranges must be contiguous, non-overlapping, one
   open-ended tail; enforce via editor `OnValidate` (auto-sort + gap/overlap warnings).

---

## Part C — Allowed colors (tutorialization)

Each range declares its **active color set** (a subset of `GamePalette`). Early ranges use
fewer colors (e.g. 2 → 3); the open-ended tail uses all **4 forever**. This is the most
**cross-cutting** parameter — it reaches into four systems:

- **Spawning** — `BalloonSpawner` must draw balloon colors only from the active set (today it
  uses the full palette).
- **Scoring / level-up** — `ScoreController` tracks per-color progress and requires **all**
  colors at threshold to level up. It must restrict to the active set *and* handle the set
  **growing at a range boundary**: a newly-introduced color's progress starts at 0 and joins
  the level-up requirement from that level onward.
- **UI** — `ColorProgressBar`s must reflect exactly the active colors (dynamic count: 2, then
  3, then 4 bars) rather than a fixed layout.
- **Steady state** — because it settles at 4 forever, the dynamic-count complexity only bites
  during the early tutorial ramp; **4-color is the permanent baseline**, so most of the game
  runs the simple path.

**✅ Resolved:** the color set changes **only at a level boundary** — never mid-level. A new
color enters at the start of its range's first level, its bar fades in at 0, and the level-up
requirement expands to include it. (Resolution is per-level anyway, so the set is stable for
the whole level.)

---

## Phasing

1. **`GameOver` state + run-scoped save** — new `NavigationState.GameOver`, freeze input,
   placeholder "you lost" + restart; make `ScoreController` run-scoped (reset on loss, persist
   best-level/best-score meta only). Foundation; bakes in the run-based decision.
2. **`BreachDetector` + deadline** — encroachment loss works; tune the deadline by hand.
3. **Level-range config system** — `LevelRangeConfiguration` + `RangedValue` (fixed/linear/
   random) + `DifficultyController`. Start with the levers that work pre-8.3: spawn-lines and
   balloon-type weights.
4. **Allowed colors** — spawner color filter + `ScoreController`/level-up restriction to the
   active set + dynamic `ColorProgressBar` count (handle the set growing at boundaries).
5. **Per-range item & actor mix** — wire `ItemAssigner` to the range's item config now; wire
   grid-actor weights when Phase 8.3's grid spawner lands.
6. **GameOver UI + meta** — score, best-level/best-score, restart flow.
7. **Tuning playtest** — pop-rate vs. encroachment vs. level-threshold curve vs. the per-range
   sliders.

---

## Risks & interactions

- **Settle timing:** evaluate the breach only after the post-spawn balance finishes, or a
  mid-flight balloon transiting a low row trips a false loss.
- **Cinematic overlap:** GameOver and the level-up cinematic must be mutually exclusive
  (gate the breach check during `LevelUp`).
- **Persistence:** `ScoreController.Save()` writes level/score to `PlayerPrefs`. Whatever
  the runs decision, GameOver must define what happens to that saved state (reset vs keep).
- **`PointsRequiredForLevel`** is a steep exponential — with real stakes, the early curve
  needs a playtest pass (a `return 12;`-style flat curve was only a debug hack).

---

## Open questions

1. ~~Runs vs. persistent progression~~ — **✅ Resolved: run-based** (see gating decision above).
2. **Deadline row vs. spawn-saturation vs. both** for the loss trigger.
3. **Restart flow** — full scene reload, or in-place grid reset + state clear?
4. **Meta on loss** — best level / best score is the baseline; currency/unlocks TBD.
