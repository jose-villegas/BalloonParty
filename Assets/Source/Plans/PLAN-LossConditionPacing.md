@page plan_loss_condition_pacing Loss Condition & Pacing

# Loss Condition & Pacing

> Turns the current lossless, content-rich sandbox into a game with **stakes** (a way
> to lose) and a **difficulty ramp** (it gets harder as you climb). Promotes the
> grid-encroachment idea from `PLAN-FutureIdeas.md` §12 into a concrete plan, grounded
> in the mechanics that already exist. No new art is required to start.

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

## Part B — Difficulty ramp / pacing

Levers available **today** (no Phase 8.3 needed):
- **Lines per turn** — scale `NewProjectileBalloonLines` up with level.
- **Initial fill** — `GameStartedBalloonLines`.
- **Level threshold** — `PointsRequiredForLevel` (already exponential; tune the curve).
- **Type mix** — shift the Simple/Tough weight in `BalloonsConfiguration` toward Tough at
  higher levels (more resistant board → more pressure).
- *(Later, with 8.3:)* obstacle density + the hitable actors (Deflector/Absorber/Gatekeeper).

**Implementation sketch:**
- A `DifficultyConfiguration` SO (curve- or table-based per level): lines/turn, type
  weights, deadline pressure. Injected via `GameLifetimeScope` like the other configs.
- A small `DifficultyController` (`IStartable`) subscribing to `ScoreLevelUpMessage` →
  pushes the level's values into the spawner (replaces the hardcoded
  `NewProjectileBalloonLines` read with a difficulty-driven value).

---

## Phasing

1. **`GameOver` navigation state + minimal flow** — transition, freeze input, a placeholder
   "you lost" + restart. Foundation; resolves the runs-vs-persistence decision in code.
2. **`BreachDetector` + deadline check + `DifficultyConfiguration` (deadline only)** — loss
   actually works; tune the deadline by hand.
3. **Difficulty ramp** — `DifficultyController` scales lines/turn + type mix per level.
4. **GameOver UI** — score, best, restart (and meta persistence per the decision above).
5. **Tuning playtest** — balance pop-rate vs. encroachment vs. level-threshold curve.

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
