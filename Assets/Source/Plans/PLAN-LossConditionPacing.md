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

**Status:** Phases 1–2 + pressure balance + the early-warning effect + the **danger (heart-drain)
cinematic** are **implemented, committed and playtested**; Phases 3+ (level-range difficulty,
allowed colors) are still spec only — Phase 3 is the recommended next move. See *Current state* below.

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

**External dependency (narrowed 2026-07-05):** only the **richer grid-actor archetypes**
(Deflector/Absorber/Gatekeeper) need Phase 8.3's procedural placement engine — they have no
content yet (`PLAN-GridActorExpansion.md`, 8.3 "blocked on content") and today's
`StaticActorSpawner` doesn't even know how to construct them (`_modelFactories` only maps
`Puff`/`Bush`). **Per-range type gate + density for what's already spawnable (Puff, Bush) needs
no 8.3 dependency at all** — verified 2026-07-05: `GridActorPrefabEntry.Weight` is never actually
read by `StaticActorSpawner` (it rolls `MinCount`..`MaxCount` per catalog entry independently, no
competitive weighted draw), so it's a simpler filter-and-override shape than the balloon bridge,
buildable on the same `LevelPacingConfiguration`/`LevelDifficultyResolver` infrastructure as 3a.
See Phase 3e below. Everything else in this plan uses mechanics that exist today.

---

## Current state (session handoff)

**Done & committed** (on `main`, latest `3171f799`, 2026-07-02):
- **Phase 1** — `GameOver` state, run-scoped save, in-place restart. PlayMode-verified. (`Game/Run/`, `Shared/GameState/`, `UI/GameOver/`.)
- **Phase 2** — player-HP loss from spawn saturation. `IGameConfiguration.StartingHitPoints`; `SpawnBlockedMessage`; rejected-balloon transient that pops **below the grid** (`Balloon/Spawner/RejectedBalloonEffect`); `Game/Health/PlayerHealthController` (publishes `EndRunRequestedMessage` at 0 HP, which `RunController` routes to `EndRun` — a message to avoid a DI cycle); `Display/CameraShakeService`; `UI/Health/HealthCounterLabel` (numeric, bound via `HealthUILifetimeScope` + a binder). See *Phase 2 — detailed breakdown*.
- **Pressure balance** — a blocked balloon isn't lost until the board truly can't take it: `BalloonSpawner.TrySpawnForColumn` does own-entry → re-home to nearest open column → shove the nearest column pressure can open (`PressureCascade`, rays through puffs, relocates Unbreakable/BubbleCluster, starts above puff entries) → reject. See *Phase 2.5 — detailed breakdown*.
- **Early-warning effect** — `Game/Danger/SpaceDanger` (0→1 `Level` from HP + free space) + `UI/Danger/DangerGradientView` (eased gradient tint + Y slide). See `Game/Danger/README.md`.

**Verification reality:** all of the above is `dotnet build`- and `style_audit`-clean, and the pure logic is EditMode-tested (`PlayerHealthControllerTests`, `PressureCascadeTests`, `SpaceDangerTests`). PlayMode (`Tests/PlayMode/PressureLossPlayModeTests`, `RunRestartPlayModeTests`) and all visual/feel behaviour need the **in-editor Test Runner / playtest** — `dotnet` can't run them.

**Phase 2 evolved past the original spec (2026-07-02, all committed + playtested):** the reject
pile is a lingering per-column queue drained by **heart trails** — `RejectedBalloonEffect`
publishes `OverflowHeartRequestedMessage` per ready balloon (serialized), the HP charge +
camera shake fire **at the heart's launch** (`SpawnBlockedMessage` moved there), the balloon
pops when its live-tracked heart lands (`OnHeartArrived`), and the **heart-drain cinematic**
(slow-mo + camera following the landing heart) plays over the drain. That cinematic drove a
full architecture pass — see `PLAN-CinematicsArchitecture.md` (settings SO, `CameraRigCinematic`
runner, `TimeScaleService`, traits) — which also makes the deferred **loss cinematic** cheap:
two states + two settings entries + a small producer over the runner.

**Loss-priority rule (decided 2026-07-02):** *no level-up after a game loss* — when a level-up and
a loss collide, the loss wins. `ILossForecast.LossImminent` (Game/Health: queued overflow charges ≥
remaining HP, true at reject-queue time — the earliest the loss is a certainty) gates the ceremony:
`ScoreController.CheckLevelUp` skips it entirely, `LevelUpCinematic` refuses to start and aborts
mid-pan-in. The loss commit itself keeps its late timing (0-HP at the Nth heart launch) so the
heart-drain presentation still plays. `RunController` defers (never drops) a loss suppressed by the
level-up window, retrying on the LevelUp → Game transition — the 0-HP request is one-shot.

**Next steps (pick up here):**
1. **Phase 3** — level-range difficulty (`LevelPacingConfiguration` + `RangedValue` +
   `LevelDifficultyResolver`/`IActiveLevelParameters`), then allowed-colors (Phase 4). Spec in
   Parts B/C/D below; **implementation-ready task list in *Phase 3 — detailed implementation
   breakdown*** (file:line-verified 2026-07-05).
2. **Loss cinematic** (`GameOverLoss` beat) — build as a runner parameterization per
   `PLAN-CinematicsArchitecture.md` guidance; do NOT write a MonoBehaviour.
3. **Ongoing tuning** — `StartingHitPoints`, lines-per-turn vs pop-rate, danger gradient feel;
   the cinematic/shake/overflow feel has been playtested through 2026-07-02.

**Key gotchas (learned this session — see memory `loss-condition-pacing-plan` for detail):** DI cycle if a loss trigger that is an `IRunResettable` depends on `RunController` (use a message); MonoBehaviour `[Inject]` runs before its `Awake` under the parent scope (bind from a `Start` entry point, not self-inject); static `Navigation` leaks across PlayMode tests (reset to `Launch` in `[SetUp]`); headless `dotnet`/meta caveats; doubled-hex coords for straight rays.

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

> **⚠️ Superseded by the *Phase 2 — detailed breakdown*.** The encroachment framing still holds, but
> the trigger is now **trigger #2 (spawn saturation) feeding a player HP pool** — not the instant
> deadline-row of #1. Each un-spawnable balloon costs 1 HP; loss fires at 0 HP through
> `RunController.EndRun`. The deadline-row option and its settle-wait are dropped. Read this section
> for background; build from Phase 2.

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

> **Status: not started (Phase 3).** This + Part C are the spec. The current read-sites the resolver must
> slot between were verified this session — see memory `phase3-level-pacing` for the grounding map (which
> config owns spawn-lines / balloon weights / items / grid actors / colours today, and who reads them).

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
    Unbreakable / BubbleCluster), a `GridActorType` gate (Puff / Bush available now, see 3e;
    Deflector/Absorber/Gatekeeper once 8.3 lands), per-item weights. **No `RangedValue` modes on
    weights** — a weight *is* a
    distribution; the randomness is the per-spawn weighted draw, so a second random roll on the
    weight itself would be redundant. Want the mix to evolve? Author a finer range.
  - **The weighted set IS the type gate** (explicit, 2026-07-02): a balloon type absent from a
    range's set (or weight 0) **cannot spawn in that range** — introduction levels are authored
    by adding the type to the first range that includes it (e.g. Simple-only 1–4, Tough enters
    at 5, Unbreakable at 9). `RejectedBalloonEffect`'s would-be pick draws from the same gate
    (the overflow pile must never show a type the level can't spawn). Catalog `MaxCount` caps
    apply on top. Custom levels gate the same way — an "all-bubble" level is a set of one.
  - `ColorSet AllowedColors` — active palette subset (see **Part C**); changes only at a level
    boundary.
  - `FromLevel`, `ToLevel` (`ToLevel = ∞` for the tail).
- **`RangedValue<T>`** (`RangedInt`, `RangedFloat`) = `{ Min, Max, Mode }`, resolved **once per
  level** (no per-turn re-roll):
  - **Fixed** — constant `Min`.
  - **Linear** — lerp `Min→Max` by the level's position within the range (a ramp).
  - **Random** — pick within `[Min, Max]` once when the level begins; stable for that level.

### The full difficulty surface (inventoried 2026-07-02) — everything is range-authored

The gating principle applies to **every difficulty-relevant knob**, not just balloon types.
`LevelParameters` carries three kinds of parameter, each with its own gating semantics:

| Kind | Semantics | Parameters (verified read-sites; trimmed 2026-07-02) |
|---|---|---|
| **Ranged scalars** (`RangedInt`/`RangedFloat`, Fixed/Linear/Random per level) | "how much / how many" | `SpawnLines` (`BalloonsConfiguration.NewProjectileBalloonLines` → `BalloonSpawner`, `SpaceDanger`) · `BoardLines` (initial fill → the Ascent) · `ItemTurnCadence` (`ItemSettings.TurnCheckEvery` → `ItemAssigner`) · per-type grid-actor `Count` (`StaticActorSpawner` — available now for Puff/Bush, see 3e) |
| **Weighted sets = availability gates** (static per range; absent/0-weight = cannot spawn) | "what exists here, and how often" | `WeightedSet<BalloonType>` · `WeightedSet<ItemType>` (an item absent from the range can't be assigned — item introduction levels, same principle as balloon types) · `GridActorType` gate (available now for Puff/Bush — see 3e; the *richer* archetypes, Deflector/Absorber/Gatekeeper, still need 8.3's placement engine + content) |
| **Per-range catalog overrides** (optional block; falls back to the catalog when absent) | "the same thing, more of it" | per-type `MaxCount` (e.g. cap Unbreakables at 2 early, 6 late) · per-item `MaximumAllowed` |

Plus the **`AllowedColors` set** (Part C).

**Deliberately kept global** (decided 2026-07-02): `LineInterval` (timing/feel, not
difficulty), `ProjectileStartingShields` (a core-feel constant, not a ramp lever),
per-type `HitsToPop` (a balloon's identity doesn't change per level), item damage/flags/VFX
(balance catalog), prediction/physics tuning.

**The level-up threshold: formula × modifier curve** (clarified 2026-07-02): the formula
**stays** (`e² · ln(level^2π) + 25` — logarithmic: L1=25, L5≈100, L20≈164) and composes
**multiplicatively** with one global authored `AnimationCurve`:
`required(level) = round(formula(level) × modifier.Evaluate(level))`. The curve is
dimensionless (y = multiplier, default flat 1.0 = pure formula, zero effect until authored);
"per range" tuning = inserting keys at range-boundary levels (e.g. `(1, 0.8) → (5, 1.0)`
makes the tutorial range ~20% cheaper, blending back to the formula by level 5). Multiplied,
not added (an offset means different things at 25 pts vs 165 pts) and not per-range formula
coefficients (boundary discontinuities; no single place to see the final curve). Guard: an
`OnValidate`/EditMode check that the composed result stays positive and non-decreasing over
the authored domain.

**HP resets per level** (decided 2026-07-02 — resolves the Ascent's open question):
`StartingHitPoints` stays a single global value, and `PlayerHealthController` refills to it
on each level-up (part of the transition). `LossForecast` recomputes naturally off the
refilled pool.

### Two-layer data shape (design refinement, 2026-07-02)

Split *authoring* from *resolution* so ranges and custom levels share one output type:

- **`LevelParameters`** — the **resolved, plain** form: `int SpawnLines`,
  `WeightedSet<BalloonType>`, item weights, an `AllowedColors` bitmask (+ future fields).
  Serializable. This is what the resolver caches, what `IActiveLevelParameters` exposes —
  **and what custom levels author directly** (exact values; a single level has no min/max).
- **`RangedLevelParameters`** — the range-authored form: scalars as
  `RangedInt`/`RangedFloat {Min, Max, Mode}`, weighted sets static. `Resolve(level,
  positionInRange, rng)` → `LevelParameters`. Pure function → EditMode-testable with a seeded
  rng.

This config keeps the field-initializer pattern: initializers are the canonical defaults
(a fresh SO equals the shipped asset, asserted by `LevelPacingConfigurationTests`),
`OnValidate` keeps the data legal, and an EditMode exhaustiveness test resolves levels 1..50
without throwing. (`CinematicsSettings` has since moved off this pattern to editor-only
authoring — see `PLAN-CinematicsArchitecture.md` — but the pacing config retains it.)

### Custom levels — exact-level overlays (new, 2026-07-02)

Bespoke levels interleaved with the ranged procedural ones ("level 10 is special"), designed
as **overlays by specificity**, CSS-style:

- **`CustomLevelEntry { int Level; LevelParameters Parameters; }`** — a second collection on
  the SO beside the ranges. `Resolve(level)`: **exact custom match wins, else the containing
  range**. Ranges stay contiguous and never know customs exist — inserting or deleting a
  custom level never re-splits the range lattice (the rejected alternative, modelling customs
  as ranges-of-one, forces hand-splitting `8–14` into `8–9 / 10 / 11–14` per insert).
- **Full-block authoring (v1)**: a custom level authors the complete `LevelParameters` —
  explicit, cascade-free, trivially testable. If authoring gets repetitive, v2 adds
  per-field inherit-from-range toggles (the `[ShowIf]` pattern) and/or an editor "copy from
  containing range" button; don't build the cascade until the repetition hurts.
- **Validation**: customs must fall inside the authored level space, no duplicate `Level`,
  warn if a custom is adjacent to a range boundary it makes invisible.
- **Extension hooks** (this is *where* future bespoke content plugs in — sketched, not Phase 3
  scope): a `Title/Tagline` for an intro banner (a cheap camera-rig cinematic: one settings
  entry + a small producer), `LevelModifier` flags (`NoItems`, `PuffStorm`, …) surfaced through
  `IActiveLevelParameters` for systems to opt into, and — once Phase 8.3's procedural placement
  lands — an authored `BoardLayout` reference for fixed starting boards.

The SO becomes **`LevelPacingConfiguration`** (ranges + customs is more than "ranges"):
`LevelRangeEntry[] _ranges` + `CustomLevelEntry[] _customLevels`.

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
- **Consumers pull, not pushed** — `BalloonSpawner` (3 read-sites), `RejectedBalloonEffect`
  (the would-be balloon's type pick), **`SpaceDanger`** (its danger denominator is
  `NewProjectileBalloonLines × Columns` — it must read the *resolved* lines or danger
  misreports on ramped levels), `ItemAssigner`, the Phase 8.3 grid spawner, `ScoreController`,
  and the color-bar UI inject `IActiveLevelParameters` and read the live values at spawn /
  level time. The resolver doesn't reference its consumers — looser coupling, and adding a
  consumer never touches the resolver.
- **Re-resolution triggers**: `ScoreLevelUpMessage`, resolve-at-start, **and run reset** — the
  resolver is `IRunResettable` at `RunResetOrder.Derived` (40), so a restart re-resolves level
  1 *before* `GridSpawnerCoordinator` re-spawns at `Respawn` (120) and the spawner reads
  level-1 parameters, not the dead run's.
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

## Part D — Level transition: "the Ascent" (new, 2026-07-02)

Today a level-up changes **nothing on the board** (verified: `BoardClearMessage` fires only on
a run reset; nothing respawns on `ScoreLevelUpMessage`) — play continues on the old layout and
new parameters would only affect future spawn lines. The Ascent makes each level a **discrete
board** and sells the fiction of climbing: after the popup dismisses, the old board pops away
scorelessly, and the camera rides up to the next board waiting above.

**Approach decision (2026-07-02, after checking the scene + pooling):** the world **never
moves — only the camera does**, and the board swap happens behind cover. Investigated and
rejected: a "spawn above + shift everything back" treadmill. The grid's
`IndexToWorldPosition` is pure config math (no transform anchor to slide), and pooled actors
stay parented under their per-key pool containers while active — all under one
**`DontDestroyOnLoad` `[Pool]` root shared with non-board pools** (trails, VFX, projectile,
notices). Literal stacking would need a grid anchor seam + re-parenting against the pool's
contract + a per-actor or per-container re-normalize with in-flight-tween and
disturbance-field edge cases. The camera-only illusion needs none of that, and the respawn
system already exists (the run-restart path).

**Sequence** (extends the current dismissed → restore choreography):
1. **Pop-out** — every remaining board actor pops (all at once or a fast sweep). Scoreless by
   construction: reuse the **`BoardClearMessage` path** (actors return their pooled views and
   vacate slots — it publishes no `ActorHitMessage`, so no score, no streaks). The cosmetic
   layer is added by the transition controller *before* the clear: per-balloon pop VFX + a
   **falling trail** per balloon — `FlyingTrail` with a new **`TrailMotion.Fall`** entry
   (targets scattered to the sides/below the frustum, gravity-ish move curve, gradient fade —
   the per-motion styling system is exactly this tool; no arrival callback → cannot score).
2. **Ascent** — a third **camera-rig cinematic** (`LevelAscend` state + settings entry + a
   small producer over the runner, per `PLAN-CinematicsArchitecture.md`): the camera pans up
   by +H into the **empty sky band** above the (now cleared) play band. A **cloud sweep**
   (existing puff sprites drifting downward past the camera) sells continuous ascent and
   masks what follows.
3. **Covered swap** — while the frame shows only sky/clouds (no reference points), the camera
   **snaps back to base in one frame** — `rig.Restore()` is already instant — imperceptible
   against a uniform background. Nothing else moved, so there is nothing to re-normalize.
4. **Reveal — the scenario first** (refined 2026-07-02): what the camera settles onto is the
   new level's **static actors** — the Puff clouds and Bushes that *are* the scene — placed
   during the covered swap from the new `LevelParameters`. **No balloons yet.** The staged
   spawner system already separates these: `StaticActorSpawner` runs at a lower `SpawnStage`
   than `BalloonSpawner`, so the transition runs the respawn in **two stage-gated passes**
   (`GridSpawnerCoordinator` gains a stage-ranged respawn, or a gate between stages).
5. **Settle, then populate** — camera at base framing, clouds part, and only then the initial
   balloon fill animates in from the top (`BoardLines` from the new `LevelParameters` — this
   is where type gating and line counts become *visible*). The fiction: you arrived at a new
   place, and its balloons drift in. Hand back to `Game` with (or just after) the fill.

**Orchestration**: a `LevelTransitionController` (plain C#) drives clear → ascend → covered
snap + static-stage respawn (the `GridSpawnerCoordinator` path, *without* touching score/HP —
this is NOT a run reset) → reveal/settle → balloon-stage spawn → hand back.

**Kept as the fallback** (only if seeing old and new boards simultaneously ever becomes a
hard requirement): anchor the board to a root (grid anchor seam + spawner re-parenting on
`Get`) and slide that one transform — accept the tween/field caveats above.

**Open design decisions** (settle before building):
- ✅ **HP on level-up** — **resets to `StartingHitPoints`** (decided 2026-07-02): the refill
  is part of the transition (`PlayerHealthController` on `ScoreLevelUpMessage`).
- **Overflow pile mid-transition** — a non-doomed pile can exist when the popup dismisses; the
  transition should wait for the drain (`!IsOverflowActive` — the thrower is already locked).
- **The frozen projectile** — return/reload it during the pop-out (fresh board, fresh shot).
- **Restore choreography** — the Ascent *replaces* the current `LevelUpRestore` camera-return
  (the camera ends at base framing only after re-normalize); reuse that state or append
  `LevelAscend` (one enum line + one settings entry, per the cinematics architecture).

---

## Phasing

1. **`GameOver` state + run-scoped save** — new `NavigationState.GameOver`, freeze input,
   placeholder "you lost" + **in-place restart**; make `ScoreController` run-scoped (reset via an
   ordered `IRunResettable` harness, persist best-level/best-score meta only). Foundation; bakes
   in the run-based decision. **See *Phase 1 — detailed breakdown* below** for the full task
   list, testability seams, and test plan.
2. **Player HP + spawn-saturation damage** — loss works through an HP pool drained by rejected
   balloons (superseded the deadline approach). **✅ Implemented; feel tuning remains in-editor — see
   *Phase 2 — detailed breakdown* below.**
2.5. **Pressure balance** — shove/redistribute balloons so a line fills every reachable+pushable slot
   before any HP loss. **✅ Implemented — see *Phase 2.5 — detailed breakdown* below.**
2.6. **Early-warning effect** — a 0→1 danger signal (HP + free space) driving an eased gradient tint +
   Y slide. **✅ Implemented; gradient/sprite wiring in-editor — see `Game/Danger/README.md`.**
3. **Level pacing system** (split, design refined 2026-07-02):
   - **3a** — `LevelPacingConfiguration` (ranges) + `RangedValue` + `LevelParameters` +
     `LevelDifficultyResolver`/`IActiveLevelParameters`; rewire the pre-8.3 levers:
     spawn-lines (`BalloonSpawner`, `SpaceDanger`) and balloon-type weights — **the type
     gate** (`BalloonSpawner`, `RejectedBalloonEffect`).
   - **3b** — **the Ascent** (Part D): scoreless pop-out + falling trails, `LevelAscend`
     camera-rig cinematic into the sky band, covered snap + **static actors revealed first**,
     balloons fill in after settling (stage-gated respawn; camera-only illusion — the world
     never moves). The payoff moment — each level becomes a visibly new place built from the
     new parameters.
   - **3c** — items lever: `ItemAssigner` reads the resolver.
   - **3d** — **custom levels**: **CUT** — `CustomLevelEntry` + `ILevelPacingConfiguration.CustomLevels`
     were never wired into the resolver (`ResolveFor` only consults ranges), so they were removed as
     dead surface. Revisit via the `CustomLevelLoader`/`LevelDefinition` idea in PLAN-FutureIdeas if
     exact-level overlays are wanted later.
   - **3e** — **grid-actor pacing (Puff/Bush now)**: per-range type gate + count for whatever
     `StaticActorSpawner` can already build — no Phase 8.3 dependency (narrowed 2026-07-05, see
     "External dependency" above). The richer archetypes (Deflector/Absorber/Gatekeeper) stay
     blocked on 8.3's content + placement engine.
4. **Allowed colors** — spawner color filter + `ScoreController`/level-up restriction to the
   active set + dynamic `ColorProgressBar` count (handle the set growing at boundaries).
5. **GameOver UI + meta** — score, best-level/best-score, restart flow.
6. **Tuning playtest** — pop-rate vs. encroachment vs. level-threshold curve vs. the per-range
   sliders.

---

## Phase 1 — detailed breakdown

Phase 1 delivers the `GameOver` state, the run-based save model, and an **in-place restart**,
with the test scaffolding it implies (including the project's **first PlayMode tests**). The
loss *trigger* is Phase 2 — Phase 1 exercises the flow with a `ForceGameOverCheat`. The loss
*cinematic* is a tracked follow-on built after the state core lands.

> **✅ Status: implemented (Blocks A–C) and PlayMode-verified** (run-based loop loss → clear →
> respawn works in-editor). Where it lives: `Game/Run/` (`RunController` with `EndRun`/`RestartRun`,
> `IRunResettable` + `RunResetOrder`, `IRunMeta`/`RunMeta`, `IRunScore`, `BoardClearController`);
> `Shared/GameState/` (`NavigationState.GameOver`, `INavigation`/`ICinematicState` seams);
> `Shared/Messages/` (`GameOverMessage`, `BoardClearMessage`); `UI/GameOver/` (`GameOverScreen` +
> `FormattedLabel`); `Cheats/` (`ForceGameOverCheat`, `ForceRestartCheat`);
> `Tests/PlayMode/RunRestartPlayModeTests`. `ScoreController` is run-scoped (no cross-session save).
> The board-clear uses a **broadcast `BoardClearMessage`** (each actor returns its own pooled view);
> re-spawn re-runs the grid spawners at `RunResetOrder.Respawn`. **Only deferred:** the loss
> cinematic / GameOver entrance animation. The loss *trigger* is Phase 2 (below).

Lifecycle: **trigger → `RunController.EndRun()` → (loss cinematic) → GameOver screen →
[Restart] → `RunController.RestartRun()` (clears board + `ResetRun()`) → `Game`**. Reset
happens on *restart*, not on GameOver entry, so the screen can still show the final score.

### Testability seams (build first)

`RunController` must drive `Navigation.TransitionTo` and read `Cinematic.IsPlaying` — both
**static** and unobservable to the test suite (the reason `GridSpawnerCoordinator` already
routes around static `Navigation`). Two thin injectable seams make the whole run lifecycle
unit-testable; substitute them with NSubstitute in tests:

- **`INavigation`** — wraps `Navigation.TransitionTo` / `Navigation.Current`; concrete
  `NavigationService` forwards to the static. Other call sites may keep using the static.
- **`ICinematicState`** — exposes `IsPlaying`; concrete forwards to static `Cinematic`.

### Block A — core state + run-scoped save (pure C#, headless-verifiable)

| # | Task | Tests |
|---|------|-------|
| 1 | Add `GameOver` to `NavigationState`. Input-freeze is automatic — `ThrowerController` gates on `== Game`. | none (enum) |
| 2 | `GameOverMessage` + MessagePipe broker registration in `GameLifetimeScope`. | — |
| 3 | `RunMeta : IRunMeta` — best level/score, `RecordRun(level, score)`, PlayerPrefs `BestLevel`/`BestScore`, loaded once. | **new** `RunMetaTests`: max-keep (level + score independently), persist-and-reload, no-prefs defaults to 0 |
| 4 | Run-scoped `ScoreController` — drop cross-session restore + run-state `Save()` and the quit/focus-loss hooks; add `ResetRun()`; implement `IRunResettable` (no `GameOverMessage` subscription — reset is restart-driven). | **rewrite** `ScoreControllerTests`: `Start_WithPersistedLevel_StartsAtOne`, `ResetRun_ClearsLevelScoreAndAllColorProgress`, `Save_DoesNotWriteRunState`; drop the vestigial `Level`/`{color}`/`.Progress` PlayerPrefs cleanup; audit existing cases for hidden reliance on restore |
| 5 | `RunController` — `EndRun()` (gated on `ICinematicState.IsPlaying` / `LevelUp`: commit meta → publish `GameOverMessage` → `INavigation.TransitionTo(GameOver)`) and `RestartRun()` (invoke resettables → `TransitionTo(Game)`). | **new** `RunControllerTests`: records meta with final level/score, publishes once, transitions to `GameOver`, defers while cinematic playing, `RestartRun` transitions to `Game` and does **not** record meta |

### Block B — in-place reset harness (built up front)

| # | Task | Tests |
|---|------|-------|
| 6 | `IRunResettable` (with explicit `ResetOrder`) + ordered invocation in `RunController.RestartRun()`. Mirrors the `SpawnStage`-ordered `IGridSpawner` pattern. | **new** ordering test, mirroring `GridSpawnerCoordinatorTests` — fakes record call order, assert sequence |
| 7 | **Balancer cancellation fix** — `BalloonBalancer.BalanceNextFrameAsync()` is `.Forget()` with **no CancellationToken**; add a CTS/epoch so a balance scheduled *before* a reset is dropped (else it animates pooled actors against an emptied grid). The one real correctness change in Phase 1. | **new** `Balance_AfterReset_IsNoOp` (synchronous guard); frame-deferred timing → PlayMode |
| 8 | `SlotGrid.Clear()` (none exists today) + per-service `Reset()` implementing `IRunResettable`: spawner counters (`_activeCounts`/`_turnCount`/`_newlySpawnedBalloons`), `BalancePathHolder` (`_transitSlots`/`_actorSlots`), `SlotClusterRegistry.RebuildAll()`, `ProjectilePositionProvider.Clear()`, `PauseService` stack, `DisturbanceFieldService` stamps, `ScoreTrailService.CompleteAll()`. **Return every actor through its normal despawn path** (kills tweens, disposes per-instance subs) — never bypass with a central `Return()`. | EditMode for the pure-C# clears (`SlotGrid.Clear`, `BalancePathHolder.Reset`, `SlotClusterRegistry.RebuildAll`, `ProjectilePositionProvider.Clear`, add `PauseService.Reset` to existing `PauseServiceTests`); pooled-return + tween-kill → PlayMode; `DisturbanceFieldService` (RenderTexture-backed) → in-editor |
| 9 | Re-trigger the initial grid spawn after a reset. | — |

### Block C — trigger + UI (in-editor)

| # | Task | Tests |
|---|------|-------|
| 10 | `ForceGameOverCheat` → `RunController.EndRun()` (mirrors `TriggerLevelUpCheat`). | thin forwarding — none |
| 11 | Placeholder GameOver screen — final + best score, restart button via `NavigationTrigger` → `RestartRun()`, plus scene/prefab wiring. | in-editor (not `dotnet build`-verifiable) |
| 12 | Docs — `Score`/`GameState`/`Cheats` READMEs; tick this phase. | — |

### Deferred cinematics (loss + danger) — handoff & build recipe

**Status: not started.** Two cinematics are deferred. Read `Game/Cinematics/README.md` first — it documents
the **producer → director → scene** system in full; this section is the loss/danger-specific plan on top.

**The system in one breath:** a producer MonoBehaviour (e.g. `LevelUpTrailEffect`) builds a
`CinematicScene` (value object of `OnBegin/OnTick/OnLateTick/OnEnd` actions) and hands it to the injected
`CinematicDirector`. `director.BeginCinematic(state)` flips the static `Cinematic.Begin(state)` (observable via
`Cinematic.Current`) and `director.PlayScene(scene)` runs the callbacks; the director
ticks the active scene each frame (`ITickable`/`ILateTickable`). `director.EndCinematic()` → `Cinematic.End()`.
A `CinematicEndGate(state)` registered as `IReadyGate` lets UI/flow wait for the cinematic to finish.
`CinematicState` (in `Shared/GameState/`) is the enum — **add new values there** (`None/LevelUpPanIn/LevelUpRestore` today).

**A. Loss cinematic — `GameOverLossEffect`** (mirrors `LevelUpTrailEffect`):
- New `CinematicState.GameOverLoss` (+ maybe `GameOverRestore`).
- Hook point: `RunController.EndRun()` currently snapshots meta → publishes `GameOverMessage` →
  `INavigation.TransitionTo(GameOver)` synchronously. For a cinematic, the effect should subscribe to
  `GameOverMessage` (or `RunController` raises a "begin loss" beat) → `BeginCinematic(GameOverLoss)` +
  `PauseService.Pause(PauseSource.Cinematic)` → camera push-in / slow-mo `Time.timeScale` curve via DOTween
  `.SetUpdate(true)` → `EndCinematic()` → reveal the GameOver screen. **Gate the GameOver screen** behind a
  `CinematicEndGate(GameOverLoss)` registered as `IReadyGate` in a `GameOverLifetimeScope` (mirror
  `LevelUpLifetimeScope`), so the screen waits for the cinematic. Camera ref via `[SerializeField] Camera`
  (Android `Camera.main` fragility — see README). `EndRun` already no-ops during a level-up cinematic, so
  the two never overlap.
- ⚠️ `Time.timeScale`/pause: the GameOver screen + restart must run in **unscaled** time (the level-up
  popup uses `AnimatorUpdateMode.UnscaledTime`); restart (`RunController.RestartRun`) must restore
  `Time.timeScale = 1` and `Resume(Cinematic)`.

**B. Danger cinematic** (optional polish, user flagged as "maybe"): a brief beat on a reject / near-death,
reusing the same director + a new `CinematicState.Danger`. It can read the existing `SpaceDanger.Level`
(early-warning signal already built) to trigger near a threshold rather than re-deriving danger. Keep it
short and **don't** `Pause` the whole game for it unless desired; if it drives the camera it must coordinate
with `CameraShakeService` (which already skips while `Cinematic.IsPlaying`).

**Decisions still open for a fresh session:** exact beats/curves (tuning), whether the danger cinematic
pauses, and whether loss needs a separate restore cinematic. Self-contained work; needs in-editor tuning.

### Test strategy

**EditMode** (NUnit + NSubstitute, mirrors source folders) covers all pure-C# logic: `RunMeta`,
`ScoreController.ResetRun()`, `RunController` (via the seams), the `IRunResettable` ordering, the
balancer cancel *guard*, and the synchronous service resets.

**PlayMode** — *new for this repo* (add `Assets/Tests/PlayMode/BalloonParty.Tests.PlayMode.asmdef`).
Covers the async/frame/pooling behavior EditMode cannot drive:
- a balance scheduled before a reset is dropped (frame-deferred `.Forget()` path);
- board-clear returns pooled actors through their despawn path — tweens killed, per-instance
  subscriptions disposed, no pool leak;
- end-to-end `EndRun → RestartRun` leaves a clean, replayable grid.

**Manual in-editor** (documented checklist, not automated): cinematic feel, GameOver screen
wiring, and the RenderTexture-backed `DisturbanceFieldService` reset.

---

## Phase 2 — detailed breakdown (player health + spawn-saturation damage)

> **✅ Status: core implemented (tasks 1–5, 7), `dotnet build`- and style-audit-clean.** Where it
> lives: `Shared/IGameConfiguration` + `Configuration/GameConfiguration` (`StartingHitPoints`, asset
> set to 5); `Shared/Messages/SpawnBlockedMessage`; `Balloon/Spawner/BalloonSpawner` (rejected-balloon
> transient pop on a saturated column → `SpawnBlockedMessage` at the pop, staggered, cleaned up in
> `ResetRun`); `Game/Health/PlayerHealthController` (HP pool → publishes `EndRunRequestedMessage` at 0, which `RunController` subscribes to and routes through `EndRun` — a message, not a direct call, to avoid a DI cycle since the controller is itself an `IRunResettable`);
> `Display/CameraShakeService`; `UI/Health/HealthCounterLabel` (numeric label, mirrors `ShieldCounterLabel`). EditMode
> `PlayerHealthControllerTests` covers damage → 0 → `EndRun`-once, clamp, and `ResetRun`.
> **Deferred / needs the editor:** scene + prefab wiring (`CameraShakeService` on the camera, the
> hearts prefab + container, `HealthBarView` placement), the heart sprite art, reject-pop / shake
> feel tuning (task 8), the PlayMode end-to-end (saturate → 0 HP → `GameOver`), and the optional
> danger cinematic (task 6). The spawner reject-count assertion is PlayMode-only (async + pooling +
> DOTween), so it isn't an EditMode test.

**Design pivot (decided):** loss is **not** immediate on encroachment.
The player has **hit points**. When the board is so choked that an incoming balloon **can't spawn**,
each un-spawnable balloon costs **1 HP**. At **0 HP** the run ends via `RunController.EndRun()` (the
Phase 1 seam — commits meta, publishes `GameOverMessage`, gates against the cinematic / non-`Game`
state, transitions to `GameOver`). A **hearts bar** shows current HP as a **dynamic count of hearts**
— hearts appear/disappear as HP changes; **no fixed maximum is displayed** (HP is just hard-capped
at 999 internally). This **replaces** the earlier deadline-row trigger — no deadline row or danger line.

### Why this is simpler than the deadline approach

The "settled grid" problem disappears: a blocked spawn is known **synchronously**.
`BalloonBalancer.Balance()` updates the grid **model** immediately (only the *view* tweens lag), and
`BalloonSpawner.SpawnLineInternal` then asks `FindFirstReachableEmptyRow(col)` against that
up-to-date model. A `null` = that column can't accept a balloon = **1 blocked balloon = 1 HP**,
known right there. No `WaitUntil` / board-settled signal needed.

### Damage source — the "rejected balloon" pop (visual feedback)

Per turn, `SpawnLineInternal` tries one balloon per column and currently **silently skips** a column
when `FindFirstReachableEmptyRow` returns `null`. **Don't skip silently** — the would-be balloon must
**appear at the entry, fail to go up, and pop at the line**, so the player sees *why* they took
damage. Each rejected balloon = **1 HP**, applied **at the pop** (the bleed syncs with the visual).
Initial `PopulateInitialGrid` fills an empty grid and never rejects; only the **turn-driven** paths
do. Popping real balloons to free space stops the bleed — the core tension.

**The rejected-balloon pop is a transient** — no grid slot, no `BalloonController`, all reuse:
pick the would-be type/colour via the existing `Entries.PickRandom`, `PoolManager.Get<BalloonView>`
it, position at the column entry, scale-in (reuse `AnimateSpawn`'s `DOScale`),
`view.PlayHitVfxForOutcome(HitOutcome.Pop)` + `DisturbanceField.Stamp(BalloonPop, …)`, then `Return`
to the pool. (`PlayHitVfxForOutcome` needs a colour — bind a minimal coloured model or call
`PoolManager.PlayParticle(DefaultPopVfxPrefab, pos, colour)` directly.)

### Tasks

| # | Task | Touches / creates |
|---|------|-------------------|
| 1 | **Starting-HP config** — `StartingHitPoints` (int) on `IGameConfiguration` + `GameConfiguration` (the value a run begins / resets to). A hard upper cap (`999`) is a `const` in the controller — not config, not displayed. (Phase 3 ranges can vary the start later.) | edit `Shared/IGameConfiguration.cs`, `Configuration/GameConfiguration.cs`; SO asset value |
| 2 | **Rejected-balloon pop + blocked signal** — `SpawnLineInternal`, on a `null` column, spawns the **transient rejected balloon** (appear at entry → pop, all reuse — see *Damage source*); publishes `SpawnBlockedMessage` **at the pop**. `PopulateInitialGrid` doesn't reject. Stagger multiple rejects in a turn so the pops/shake don't stack ugly. | **new** `Shared/Messages/SpawnBlockedMessage.cs` + broker; edit `Balloon/Spawner/BalloonSpawner.cs` (maybe a small `RejectedBalloonEffect` helper) |
| 3 | **`PlayerHealthController`** (`IStartable`, `IRunResettable`, `IDisposable`) — `ReactiveProperty<int> Current` (init `StartingHitPoints`, clamped `[0, 999]`); exposes **`Current` only** (no max). Subscribe to `SpawnBlockedMessage`; `Damage(count)` clamps at 0; **`EndRun()` once on crossing to 0**; `ResetRun → StartingHitPoints`. | **new** `Game/Health/PlayerHealthController.cs` (+README); register `AsSelf().As<IRunResettable>()` |
| 4 | **Camera shake (NEW — none exists)** — a `CameraShakeService`/component doing a DOTween `DOShakePosition` punch-and-restore on the gameplay camera, triggered on the reject pop (subscribe to `SpawnBlockedMessage`, or hook `ImpactEventBus`). **Must not fight the cinematic camera control** — skip or layer it while `Cinematic.IsPlaying`. Camera ref via `[SerializeField] Camera` like `LevelUpTrailEffect`. | **new** `Display/CameraShakeService.cs` (or component); register in `GameLifetimeScope` |
| 5 | **HP counter UI** — `HealthCounterLabel` binds **`Current`** → a numeric label (**no fixed max**), mirroring `ShieldCounterLabel`. *(Decided against a hearts row.)* `[Inject] Construct(PlayerHealthController)` → `Bind(Current)`. Place a TMP label in-scene. | **new** `UI/Health/HealthCounterLabel.cs` (registered via `RegisterComponentInHierarchy`); scene wiring in-editor |
| 6 | *(optional, deferred polish)* **Dramatic "danger" cinematic** — a brief beat on reject (or near-death) reusing `CinematicDirector` + a new `CinematicState` + `CinematicScene` (like `LevelUpTrailEffect`), optionally `PauseService.Pause(Cinematic)`. Build only after the core feel is right; the user flagged it as a "maybe". | new `CinematicState` value + small effect in `Game/Cinematics/` |
| 7 | **Tests** — EditMode: `PlayerHealthControllerTests` (damage → 0 → `EndRun` once via `INavigation`/`ICinematicState` substitutes; `ResetRun → Max`; clamp). Spawner: reject published / block-count correct on a saturated real `SlotGrid`. PlayMode: saturate → HP to 0 → `GameOver`. | **new** `PlayerHealthControllerTests`; spawner test; PlayMode case |
| 8 | **Tuning** — `PlayerMaxHitPoints`, reject-pop feel, shake intensity, lines-per-turn vs pop-rate vs HP drain. | config asset + playtest |

### Integration / safety

- **Death → `EndRun` is the only loss trigger now.** Fire it once when `Current` crosses to 0;
  `EndRun` already no-ops outside `Game` / during the level-up cinematic, so a blocked spawn that
  lands during a cinematic just doesn't end the run (HP stays at 0; the GameOver-state gate prevents
  a double-fire on the next blocked spawn).
- **HP resets on restart** via `IRunResettable` (order `Counters`, before the `Respawn` stage) so a
  new run starts at full HP.
- **No deadline row / danger line** — the hearts bar + the reject pop are the feedback. (A secondary
  instant-death line could return later if tuning wants it, but it's out of scope here.)
- **Feedback layering** — the reject pop reuses `BalloonView`'s pop VFX + a disturbance stamp; the
  camera shake is new (task 4) and must be a quick punch-and-restore that doesn't run while a
  level-up (or the optional danger) cinematic is driving the camera. Cap/stagger when several
  balloons are rejected in one turn so the screen doesn't get seasick.

### Decision (supersedes Open question #2)

Loss trigger = **spawn-saturation damage to an HP pool**, **not** deadline-row and **not** instant.
Damage is **per un-spawnable balloon** (1 blocked balloon = 1 HP). Feedback = the would-be balloon
**appears and pops at the line** + **camera shake**, with a **hearts bar** showing current HP as a
dynamic count (no max displayed; hard-capped at 999); an optional dramatic cinematic is deferred polish.

---

## Phase 2.5 — detailed breakdown (pressure balance — soften the HP bleed)

**Status: core implemented (`dotnet build`- and audit-clean; EditMode `PressureCascadeTests`).** Phase 2
alone is too harsh: because the grid packs *upward*, a column's entry blocks (and costs HP) the moment
that *single* column is solid floor-to-ceiling — even while other columns still have room. Pressure
balance redistributes balloons *across* columns (the sideways/down moves normal balance never makes) so
HP only drops once the grid is genuinely full where it can be.

**Concept (decided):** when an arriving balloon can't fit, it **shoves** the column's bottom occupant,
and the shove propagates neighbour-to-neighbour — snake-like — until it reaches a free slot. A balloon
can be displaced in **any** direction (a bottleneck can force it *down*); there's no per-direction rule.
The per-type personality is the **push response**: a normal balloon steps one cell to a neighbour and
passes the shove along; **relocating** balloons get out of the way to a free slot anywhere, ending the
chain — **BubbleCluster** drifts to the *nearest* gap (stays close), **Unbreakable** barges to the
*farthest* gap (clears right out). That's the custom behaviour the type seam exists for.

### How it works
- **`IPressureMovable` + `PressureResponse`** (`Slots/Capabilities/`) — `ShoveNeighbour` /
  `RelocateNearest` / `RelocateFarthest`. `BalloonModelBase` defaults to `ShoveNeighbour`;
  `BubbleClusterModel` overrides to `RelocateNearest`; `UnbreakableBalloonModel` to `RelocateFarthest`.
  (Simple / Tough inherit the default — the seam to give them their own response later is here.)
- **`PressureCascade.TryFindChain(grid, col, chain)`** (`Balloon/Controller/`, pure & unit-tested) —
  BFS from the column's entry `(col, Rows-1)` through *shovable* neighbours (any direction) to the
  nearest empty slot. A relocating actor reached by the chain short-circuits it: it vacates to its
  preferred free slot (nearest or farthest), so a reachable relocator relieves pressure as long as the
  board has any gap. Returns the shortest chain `[entry, …, destination]`; BFS keeps the squeeze local.
  Each step **rays through `IPassThrough` occupants** (puff clouds) to the first empty/shovable cell
  beyond them — using doubled hex coordinates (`xd = 2·col + row%2`) so the ray travels a true straight
  line; a non-traversable static actor halts that ray. Chain cells may therefore be several apart.
- **`BalloonBalancer.TryRelievePressure(col)`** — runs the cascade; if a chain exists, shifts each
  occupant into the next cell (from the empty end back, so every destination is vacant), reserves
  transit, and animates via the existing `AnimatePaths`. Returns whether room was opened.
- **`BalloonSpawner.TrySpawnForColumn`** — a blocked balloon doesn't stay locked to its column. On
  turn-driven spawns the order is: **own-column entry → re-home to the nearest other column that can
  still accept it (`TryFindNearestOpenColumn`; the line may over-fill a column) → shove open the nearest
  column pressure *can* open, using gaps anywhere on the board (`TryPressureOpenNearestColumn`, which
  runs `TryRelievePressure` per column outward) → reject + HP**. So a line fills every reachable *and*
  pushable slot — including interior pockets that no column's entry reaches directly — before any
  balloon is lost; HP only drops on genuine overflow.
- **Reject pop is below the grid** — the would-be balloon rises from the entry to just beneath the
  bottom row and bursts there (row `Rows` / `Rows+1` world positions), so it never overlaps the packed
  board.

### Remaining (separate steps, as intended)
- **Danger VFX** — a visual cue that pressure balance is happening / the board is near full. Deferred by
  design; hook off `TryRelievePressure` success (or a new message) when built.
- **Per-type tuning** — Simple / Tough currently share the `ShoveNeighbour` default; give them their
  own `PressureResponse` (or a richer strategy) once the feel is tuned. BubbleCluster relocates to the
  nearest gap, Unbreakable to the farthest.
- **PlayMode** — `Tests/PlayMode/PressureLossPlayModeTests` floods the real Game scene with spawn lines
  and asserts the board fills past half (pressure/re-home use every reachable+pushable slot) *before*
  any HP is lost, then drains to 0 → `GameOver`; plus an initial-HP check. Compile-verified here; run in
  the editor Test Runner to confirm it passes (and eyeball the reject feel).

## Phase 3 — detailed implementation breakdown (written 2026-07-05, code-verified)

> **Status: 3a code-complete (2026-07-05)** — all data types, the resolver, the DI wiring, and the
> read-site swaps below are implemented and committed-pending (7 sites, not 5 — the sweep initially
> missed `Cheats/TriggerLevelUpCheat.cs` and `Cheats/NearLevelUpCheat.cs`, both also reading the raw
> formula off `IGameConfiguration`; fixed the same session); `dotnet build` (Runtime, Editor,
> Tests.EditMode, Configuration.Editor) and `style_audit.py` are clean, **committed** (`39edfeac`).
> `Assets/Configuration/LevelPacingConfiguration.asset` exists and is wired into
> `GameLifetimeScope._levelPacingConfiguration` — the former "blocking in-editor step" is done.
> New EditMode tests (`RangedValueTests`, `LevelPacingConfigurationTests`,
> `LevelDifficultyResolverTests`, `GamePaletteTests`) — **`dotnet build` only compiles, it never
> executes tests**; the in-editor Test Runner is the only thing that actually runs them, and it
> caught a real bug `dotnet build` couldn't: `LevelRangeEntry` (a struct wrapping a
> `RangedLevelParameters` class field) had no constructor, so `LevelPacingConfiguration`'s default
> `_ranges = { new() }` silently produced a range with `Parameters == null` — structs zero-init
> class fields on their implicit default constructor, no Unity serialization involved. Fixed
> (2026-07-05) by giving `LevelRangeEntry` an explicit constructor; `CustomLevelEntry` has the
> identical shape and is flagged with the same warning under 3d below since 3d hasn't touched it
> yet. Takeaway for whoever continues this: **treat `dotnet build` green as "compiles," not
> "correct" — always run the actual Test Runner before considering a sub-phase done.**
> `AllowedColors` was changed from `string[]` to a `[PaletteColorMask] int` during implementation
> (2026-07-05) — see item 5 in 3a below. **3c (items), 3e (grid-actor pacing), and Phase 4
> (allowed colors) are also implemented (2026-07-05)** — see their own status notes below. Only
> 3b (the Ascent) and 3d (custom levels) remain spec-only (3e was added 2026-07-05 —
> grid-actor pacing for Puff/Bush was previously miscategorized as blocked on Phase 8.3; only the
> newer archetypes are).

> **Audience: an implementing agent/session.** Every file:line below was verified against HEAD on
> 2026-07-05. Sub-phases 3a → 3b → 3c → 3d are independent commits in that order; 3a is the
> foundation and must land first. Phase 4 (colors) is broken down at the end because it reuses 3a's
> plumbing. Follow `CLAUDE.md` conventions (field/method order, Allman, braces, `internal`-first);
> run `dotnet build BalloonParty.Runtime.csproj -nologo -clp:ErrorsOnly` +
> `python3 Tools/style_audit.py` after each block.
>
> **Cross-cutting mechanics (apply to every new file):**
> - Hand-add each new `.cs` to `BalloonParty.Runtime.csproj` (`<Compile Include=...>` — it lists
>   files explicitly; ~357 entries) and test files to `BalloonParty.Tests.EditMode.csproj`,
>   otherwise `dotnet build` won't see them.
> - Let the open Unity editor generate `.meta` files (don't hand-author).
> - Namespaces mirror folders (`BalloonParty.Configuration`, `BalloonParty.Game.Level`).
> - Config access is via read-only interfaces; consumers never see the concrete SO.

### 3a — ranges + resolver + first two levers (spawn-lines, balloon-type gate)

#### New files — data types (`Assets/Source/Configuration/`)

1. **`RangeMode.cs`** — `internal enum RangeMode { Fixed, Linear, Random }`.
2. **`RangedInt.cs` / `RangedFloat.cs`** — `[Serializable]` structs: `[SerializeField] int _min, _max;
   [SerializeField] RangeMode _mode;` plus a pure
   `int Resolve(float positionInRange, System.Random rng)`:
   - `Fixed` → `_min`; `Linear` → round(lerp(`_min`,`_max`, positionInRange)); `Random` →
     `rng.Next(_min, _max + 1)` (float variant uses `NextDouble`). Pure + seeded rng ⇒ EditMode-testable.
   - `positionInRange` = `(level - FromLevel) / max(1, ToLevel - FromLevel)`, clamped 0..1; the
     open-ended tail resolves Linear as Fixed(`_min`) (no meaningful span) — document in a `///` note.
3. **`BalloonTypeWeight.cs`** — `[Serializable]`: `BalloonType _type; float _weight;
   int _maxCountOverride;` (0 = use catalog `BalloonPrefabEntry.MaxCount`). An array of these is
   the range's `WeightedSet<BalloonType>` — **membership is the type gate** (absent/0-weight
   cannot spawn).
4. **`ItemTypeWeight.cs`** — same shape for `ItemType` (`_maximumAllowedOverride`, 0 = catalog).
   Added now (it's 10 lines and keeps `LevelParameters` stable); consumed in 3c.
5. **`LevelParameters.cs`** — the **resolved, plain, serializable** form (also authored directly by
   custom levels in 3d):
   `int SpawnLines; int BoardLines; BalloonTypeWeight[] BalloonWeights;
   ItemTypeWeight[] ItemWeights; int AllowedColorsMask;`
   Field initializers = canonical defaults matching today's shipped feel: `SpawnLines` = current
   `BalloonsConfiguration._newProjectileBalloonLines` asset value, `BoardLines` = current
   `_gameStartedBalloonLines`, all four types weighted as the current
   `BalloonsConfiguration.asset` entries. (Pattern proven on `CinematicsSettings`: fresh object ==
   shipped asset.)
   **No `ItemCadence` field** — dropped during 3a implementation (2026-07-05): it was scaffolded
   here but nothing reads it until 3c actually wires `ItemAssigner`, so it sat as unused authoring
   surface. Unity's serializer handles an added field with zero migration cost (existing assets
   just get its default), so there's no stability argument for pre-declaring it — add it in 3c,
   next to the wiring that gives it meaning.
   **`AllowedColors` authored as a bitmask, not `string[]`** (changed during 3a implementation,
   2026-07-05): `[PaletteColorMask] int _allowedColorsMask = ~0` — same attribute + drawer
   `ColorableBalloonVariant._allowedColorsMask` already uses (`Configuration/PaletteColorMaskAttribute`
   + `Configuration/Editor/PaletteColorMaskDrawer`), so authoring gets the existing swatch-picker UI
   for free instead of hand-typed, typo-prone color-name strings. All bits set (default) = every
   palette color, replacing the old "empty array means all colors" special case. The mask is also
   what Phase 4's spawn-filter intersection (below) needs — `rangeMask & prefabMask` is a plain `int`
   AND, where two `string[]` sets would've needed an actual set-intersection. `IActiveLevelParameters
   .AllowedColors` keeps its `IReadOnlyList<string>` shape for consumers (`ScoreController` etc. want
   names, not bits) — `LevelDifficultyResolver` converts once per resolve via the new
   `IGamePalette.ColorNamesForMask(int)` (added to the interface + `GamePalette`, same bit-index-i-
   equals-`Colors[i]` convention as the mask attribute), not per read.
6. **`RangedLevelParameters.cs`** — the range-authored form: `RangedInt SpawnLines; RangedInt
   BoardLines;` + the same static `BalloonTypeWeight[]`/`ItemTypeWeight[]`/
   `int AllowedColorsMask` (weighted sets/colors are **static per range** — locked decision).
   One pure method: `LevelParameters Resolve(int level, float positionInRange, System.Random rng)`.
7. **`LevelRangeEntry.cs`** — `[Serializable]`: `int _fromLevel; int _toLevel;` (`_toLevel <= 0` =
   open-ended tail) + `RangedLevelParameters _parameters`.
8. **`CustomLevelEntry.cs`** — `[Serializable]`: `int _level; LevelParameters _parameters;`
   (fields + class now; resolve wiring in 3d).
9. **`ILevelPacingConfiguration.cs` + `LevelPacingConfiguration.cs`** — SO, `[CreateAssetMenu]`
   "Configuration/Level Pacing" (mirror `Configuration/OverflowSettings.cs`, the reference pattern):
   - `LevelRangeEntry[] _ranges;` `CustomLevelEntry[] _customLevels;`
   - `AnimationCurve _thresholdModifier = AnimationCurve.Constant(1, 100, 1f);` — the
     **dimensionless multiplier** over the `PointsRequiredForLevel` formula (see Part B: multiplied,
     not added; keys inserted at range boundaries; default flat 1.0 = pure formula).
   - Interface: `IReadOnlyList<LevelRangeEntry> Ranges`, `IReadOnlyList<CustomLevelEntry>
     CustomLevels`, `float ThresholdModifier(int level)` (curve evaluate, clamped to > 0).
   - `OnValidate` (editor-only guards): auto-sort ranges by `FromLevel`; warn on gaps/overlaps;
     exactly one open-ended tail (the last); every range's weighted set non-empty with at least
     one weight > 0; **threshold monotonicity** — composed
     `round(formula(L) × modifier.Evaluate(L))` must stay positive and non-decreasing for L = 1..
     last authored key (formula: `(int)((Mathf.Exp(2) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI))) + 25f)`,
     from `GameConfiguration.cs:50-53`). Log a warning naming the offending level, don't throw.

#### New files — resolver (`Assets/Source/Game/Level/` — new folder)

10. **`IActiveLevelParameters.cs`** — the single read surface runtime systems inject:
    ```csharp
    internal interface IActiveLevelParameters
    {
        int SpawnLines { get; }
        int BoardLines { get; }
        int PointsRequiredForLevel(int level);
        BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts);
        IReadOnlyList<ItemSettings> Items { get; }          // 3c narrows this
        IReadOnlyList<string> AllowedColors { get; }         // Phase 4 activates this
    }
    ```
11. **`LevelDifficultyResolver.cs`** — plain C#, `IStartable`, `IDisposable`, `IRunResettable`
    (`ResetOrder => RunResetOrder.Derived` /*40*/ — re-resolves level 1 **before**
    `GridSpawnerCoordinator` respawns at `Respawn` 120), implements `IActiveLevelParameters`.
    - Ctor deps, as actually built (2026-07-05): `ILevelPacingConfiguration`,
      `IBalloonsConfiguration` (catalog), `IItemConfiguration` (catalog), `IGameConfiguration` (the
      threshold formula), `IGamePalette` (mask→names for `AllowedColors`), `ISubscriber
      <ScoreLevelUpMessage>`. **No `IRunScore`** — the original sketch had the resolver read
      `Level.Value` at `Start` to know where to resolve from, but a run always starts at level 1
      (score is run-scoped, resets to 1 on both fresh boot and restart), so `Start()` just calls
      `ResolveFor(1)` unconditionally — simpler and one fewer dependency.
    - `Start()`: `ResolveFor(1)`, then subscribe `ScoreLevelUpMessage` → `ResolveFor(msg.NewLevel)`.
      The payload field is `NewLevel` (`Shared/Messages/ScoreLevelUpMessage.cs`), and
      `ScoreController.CheckLevelUp` (`:151-159`) increments `_level.Value` *before* publishing, so
      `msg.NewLevel` is already the level to resolve for — no `+1`.
    - `ResolveFor(int level)`: exact custom match wins (3d) → else the containing range →
      `range.Parameters.Resolve(level, positionInRange, _rng)` → cache the `LevelParameters` +
      rebuild `_pickList`.
    - **The catalog bridge** (`PickBalloonEntry`): the spawner needs a `BalloonPrefabEntry`
      (prefab, pool key, hits-to-pop live in the catalog). **Correction found during implementation
      (2026-07-05):** the catalog has *multiple prefab variants (skins) per `BalloonType`* (verified
      in `BalloonsConfiguration.asset` — 6 entries across 4 types), so "wrap the entry matched by
      type" (singular) was wrong. The resolver instead wraps **every** catalog entry whose type is
      gated in, with effective weight = `catalogEntry.Weight × range.Weight` — the range's weight
      scales the *type's* overall frequency, the catalog entry's own weight still governs which skin
      of that type is picked, and the two compose multiplicatively (same pattern as the threshold
      curve). `MaxCountOverride`, if set, replaces each matching-type entry's `MaxCount`
      individually — it is **not** an aggregate cap across a type's variants; a range wanting a true
      per-type total should give that type exactly one catalog variant. On each resolve, build a
      cached `List<ResolvedBalloonEntry>` — a private class implementing `IWeightedEntry` — then
      reuse the existing `WeightedPickExtensions.PickRandom(entries, activeCounts)`
      (`Shared/Extensions/WeightedPickExtensions.cs:16` — it already excludes entries at `MaxCount`
      via `PoolKey` counts) and return the wrapped catalog entry. One list rebuild per *level*, zero
      alloc per spawn.
    - `PointsRequiredForLevel(level)` =
      `Mathf.RoundToInt(_gameConfig.PointsRequiredForLevel(level) * _pacing.ThresholdModifier(level))`
      — formula stays in `GameConfiguration`, the resolver composes.
    - `ResetRun(int generation)`: reset `_rng` (seed from generation for determinism) +
      `ResolveFor(1)`.

#### Modified files (3a)

- **`Game/GameLifetimeScope.cs`** —
  - Field block (with the other config SOs): `[SerializeField] private LevelPacingConfiguration
    _levelPacingConfiguration;`
  - After line 107's `RegisterInstance` run:
    `builder.RegisterInstance<ILevelPacingConfiguration>(_levelPacingConfiguration);`
  - With the entry points (near line 148):
    `builder.RegisterEntryPoint<LevelDifficultyResolver>().AsSelf().As<IActiveLevelParameters>().As<IRunResettable>();`
  - ⚠️ The new serialized field **must be wired to the asset in-editor** or injection is null
    (same trap as `_overflowSettings`, noted in Phase 2.5).
- **`Balloon/Spawner/BalloonSpawner.cs`** — inject `IActiveLevelParameters _levelParams;` and swap
  three read-sites (keep `_balloonsConfig` for everything else — prefabs, intervals, offsets):
  - `:126` prewarm sizing `_balloonsConfig.GameStartedBalloonLines` → `_levelParams.BoardLines`
    (prewarm runs once pre-resolve is fine — resolver resolves in `Start` before the gate opens;
    if ordering bites, size the pool with the catalog value and note it).
  - `:146` `SpawnLinesWithDelayAsync(_balloonsConfig.NewProjectileBalloonLines, …)` →
    `_levelParams.SpawnLines`.
  - `:163` `PopulateInitialGrid` loop bound `GameStartedBalloonLines` → `_levelParams.BoardLines`.
  - `:183` `_balloonsConfig.Entries.PickRandom(_activeCounts)` →
    `_levelParams.PickBalloonEntry(_activeCounts)`.
- **`Balloon/Spawner/RejectedBalloonEffect.cs:158`** — same swap for the would-be pick
  (`_levelParams.PickBalloonEntry(activeCounts)`); the overflow pile must never show a type the
  level can't spawn.
- **`Game/Danger/SpaceDanger.cs:67`** — `spawnPerTurn = _balloonsConfig.NewProjectileBalloonLines *
  _grid.Columns` → `_levelParams.SpawnLines * _grid.Columns` (danger misreports on ramped levels
  otherwise). Swap the injected dependency; drop `IBalloonsConfiguration` if now unused here.
- **`Game/Score/ScoreController.cs:85-88`** — `GetRequiredPoints()` currently returns
  `_config.PointsRequiredForLevel(Level.Value + 1)`; switch to
  `_levelParams.PointsRequiredForLevel(Level.Value + 1)`. Check for any other
  `PointsRequiredForLevel` call-sites in the file and swap them identically. `CheckLevelUp`
  (`:135-161`, incl. the `LossImminent` gate) is otherwise untouched.
- **`Configuration/BalloonsConfiguration.cs`** — *demotes to catalog*: `GameStartedBalloonLines` /
  `NewProjectileBalloonLines` stay for now as fallback defaults (used to seed `LevelParameters`
  defaults) but every runtime read now goes through the resolver. Add a `///` note on both
  properties: "catalog default — runtime reads the resolved value via IActiveLevelParameters".
  Do NOT delete the fields this phase (the asset keeps its values as the source for range
  authoring defaults).

#### In-editor steps (3a — flag for the user, cannot be done headless)

- Create `Assets/Configuration/LevelPacingConfiguration.asset` (Create → Configuration → Level
  Pacing); author an initial 1-range config replicating today's constants (open-ended tail,
  today's 4-type weights, `SpawnLines`/`BoardLines` Fixed at current asset values, flat 1.0
  threshold curve) so behavior is **identical before tuning**.
- Wire the asset into `GameLifetimeScope._levelPacingConfiguration` in the Game scene.
- Playtest: spawn mix + lines + level thresholds unchanged; then author a second range (e.g.
  Tough enters at level 3) and verify the gate.

#### EditMode tests (3a) — `Assets/Source/Tests/EditMode/` (+ hand-add to `BalloonParty.Tests.EditMode.csproj`)

- `RangedValueTests` — Fixed/Linear/Random resolution incl. clamped position + seeded-rng
  determinism; open-tail Linear degrades to Min.
- `LevelPacingConfigurationTests` — defaults-equal-shipped-asset assertion (CinematicsSettings
  pattern); threshold monotonicity guard catches an authored dip; contiguity warnings.
- `LevelDifficultyResolverTests` — NSubstitute the configs: exhaustive resolve levels 1..50
  without throwing; type absent from range never picked over N seeded draws; MaxCount override
  honored via `activeCounts`; re-resolve on `ScoreLevelUpMessage` (capture the `IMessageHandler`
  like `ScoreControllerTests` does); `ResetRun` re-resolves level 1; threshold composition =
  round(formula × curve) incl. flat-curve == pure formula.
- Update `ScoreControllerTests` construction for the new dependency (substitute
  `IActiveLevelParameters` returning the formula value so existing threshold expectations hold).

### 3b — the Ascent (level transition; spec = Part D above)

**Status: mechanical backbone implemented (2026-07-05).** `dotnet build` (all 4 csproj) +
`style_audit.py` clean; not yet committed, not yet run through the Test Runner or playtested.
Done: `TrailMotion.Fall` enum member (data only — no curve styling wired yet, see below);
`CinematicState.LevelAscend` + `CinematicsSettings` entry; `PauseSource.LevelTransition`;
`GridSpawnerCoordinator.RunStagesAsync(predicate, ct)` (generalized past a single-stage filter so
it stays correct if a `DynamicActors`-priority spawner is ever added); `ThrowerController` reloads
on `BoardClearMessage` too (not a new public method — kept `Reload()` private and had the
controller react to the same message the Ascent already publishes, avoiding a cross-`LifetimeScope`
DI reach into `ThrowerLifetimeScope`); `PlayerHealthController` refills HP on `ScoreLevelUpMessage`;
new `Game/Cinematics/LevelAscendCinematic.cs` (drives `CinematicCameraRig` directly —
`PreparePanIn`/`Restore()` — bypassing `CameraRigCinematic`'s wrapper, whose restore is always
tweened and can't do the instant covered-swap snap); new `Game/Level/LevelTransitionController.cs`
orchestrating pause → overflow drain → ascend → `BoardClearMessage` → statics stage → instant
restore → balloon stage → resume.

**Fixed after first playtest report (2026-07-05):** the Ascent replaces `LevelUpRestore` per this
plan's own "Restore choreography" decision above — the first pass didn't actually do that (it left
`LevelUpCinematic.OnDismissed` calling `_cinematic.TryBeginRestore()`, so two producers fought over
the same shared `CinematicCameraRig` at once: the Ascent's `PreparePanIn` killed the restore's
in-flight camera tween and corrupted `CinematicDirector`'s active-cinematic bookkeeping, which left
`PauseSource.LevelTransition` stuck and the thrower permanently locked). Fix: `LevelUpCinematic.
OnDismissed` no longer calls `TryBeginRestore` — it just resumes `PauseSource.Cinematic` and hands
off to `NavigationState.Game` directly; `LevelAscendCinematic.PreparePanIn` (called moments later)
does the "snap to base then zoom out" itself, since `_hasBaseState` is still true from the pan-in.
`CinematicState.LevelUpRestore` and its `CinematicsSettings` entry stay in the enum/array (removing
them would shift every later ordinal's index in the hand-authored `CinematicsSettings.asset`) but
nothing plays that state anymore. `LevelTransitionController.TransitionAsync` also now: waits for
`!Cinematic.IsPlaying` before touching the rig (defense in depth if some other cinematic is
active), wraps the whole sequence in `try/finally` so `PauseSource.LevelTransition` always resumes
even if a step throws, and `LevelAscendCinematic` tracks whether it actually won
`TryBeginCinematic` so `EndAscend`/the rig calls no-op instead of firing blind when it didn't.

**Redesigned per feedback after the fix above (2026-07-05):** the original zoom-and-snap read as
just "it snaps back into position" — not a real transition. Replaced with a literal vertical camera
translate, and a visible pop for the outgoing balloons instead of a silent clear:
- `BoardClearMessage` gained a `PlayPopVfx` bool (default `false`, so the ordinary run-restart clear
  is unchanged). `BalloonControllerRegistry.OnBoardClear` threads it into
  `BalloonController.HandleBoardClear(bool)`, which now plays `PlayHitVfxForOutcome(HitOutcome.Pop)`
  + the disturbance-field stamp (mirroring `Pop()`'s own effects, minus the item-activation deferral
  — a level transition doesn't need that) when the flag is set. The Ascent publishes
  `new BoardClearMessage(playPopVfx: true)`; every remaining balloon pops in the same frame the
  board clears — "almost at the same time" falls out of the registry's existing single-frame loop,
  no staggering needed.
- `CinematicCameraRig` gained `PrepareAscend()` (captures base state, no tween — mirrors
  `PreparePanIn` minus the zoom) and `TranslateAscend(float offsetY)` (sets the camera to base
  position + a vertical offset). `LevelAscendCinematic.PlayAsync` drives these itself in a per-frame
  `UniTask` loop (not a DOTween — needed a mid-flight callback hook, which DOTween's `OnUpdate` can
  do too, but the existing curve-sampling pattern from `LevelUpCinematic.PanInTick` was the more
  consistent fit) rather than `BeginAscend`/`EndAscend`'s prior zoom-tween-then-snap shape.
- `CameraRigCinematicSettings`' generic fields are **reinterpreted for this one state** (documented
  in the `CinematicsSettings.cs` entry comment, not the class itself — the fields stay generic):
  `TimeScaleCurve`'s VALUE is a 0→1→0 height fraction (not a timeScale multiplier — gameplay is
  paused via `PauseSource.LevelTransition`, not `TimeScaleService`), its duration is still the
  segment length (1.2s authored); `ZoomAmount` is the ascend height in world units (8, placeholder);
  `PanWeight` is the fraction of the total duration at which the new level's balloon spawn cue fires
  (0.75 — partway through the descent, so balloons are already mid-`BalloonFactory.AnimateSpawn` by
  arrival, not popping in only after); `FollowSpeed` is the descent-speed multiplier — the loop
  advances the curve by `unscaledDeltaTime * FollowSpeed`, so real descent time = duration /
  FollowSpeed (0.5 authored → the 1.2s curve plays over ~2.4s; 0 falls back to the curve's own pace).
  This mirrors how `LevelUpCinematic` already reinterprets the same curve's value as a trail-speed
  multiplier instead of a timeScale one — reusing the uniform segment shape per-producer is the
  established pattern, not a one-off hack.
- `LevelTransitionController.TransitionAsync`: pop-burst + `BoardClearMessage` → statics stage
  (hidden, placed before the ascend even starts moving) → `_ascendCinematic.PlayAsync(cue, ct)`
  where `cue` triggers the balloon stage. No more `BeginAscend`/`EndAscend`/`UniTask.Delay` split.

**Fixed after second playtest report (2026-07-05):** the redesign above shipped with zero visible
camera motion — "no literal translation... it just appears." Root cause was NOT the camera code:
`Assets/Configuration/CinematicsSettings.asset` on disk still had only 5 `_states` entries (None
through HeartDrainRestore) — adding `CinematicState.LevelAscend` and its field-initializer entry in
`CinematicsSettings.cs` only affects a FRESH instance (e.g. what EditMode tests construct via
`CreateInstance`), not the real serialized asset, which self-heals its array length only when Unity
actually touches it (`OnValidate`) — which hadn't happened in this headless session. So
`EntryOf(CinematicState.LevelAscend)` threw `ArgumentOutOfRangeException` before `PrepareAscend()`
ever ran, silently swallowed as an unobserved `UniTaskVoid` exception. Fixed by hand-appending the
6th `_states` YAML block directly to the `.asset` file, matching the code's authored values exactly
(diff is purely additive — verified via `git diff`). This class of bug is invisible to both
`dotnet build` and the EditMode tests, since neither loads the real `.asset` file from disk.

**Redesigned again — moving the scenario instead of the camera (2026-07-05):** the fixed camera
translate WAS visible, but read badly — "it just jumps up and down." Replaced with: the camera never
moves at all; instead, `StaticActorSpawner` gained a staging-parent capability
(`SpawnStaticActorsInto(Transform stagingParent)`, alongside the unchanged no-arg
`SpawnStaticActors()`) — each newly placed static's view is positioned at its real grid position
offset by the staging parent's *current* world position, then reparented onto it
(`SetParent(stagingParent, worldPositionStays: true)`), so its `localPosition` under that parent
permanently equals its correct final grid position regardless of the parent's current offset.
`LevelTransitionController` owns a persistent `AscentStagingRoot` transform (created once in
`Start()`, repositioned per-transition, never destroyed — destroying it mid-transition would take
its parented children down with it): it sets the root to `(0, height, 0)` before spawning statics
into it, then `LevelAscendCinematic.PlayAsync` animates the root's position back to `Vector3.zero`
over the curve — every static slides from "above the board" down to its correct slot in one shared
tween, no per-actor tweening needed. `LevelAscendCinematic` no longer takes a `CinematicCameraRig`
at all; `CinematicCameraRig.PrepareAscend`/`TranslateAscend` (added for the previous camera-based
attempt) were removed as dead code. `StaticActorSpawner` needed `.AsSelf()` added to its
`GameLifetimeScope` registration (it was `IGridSpawner`-only) so `LevelTransitionController` could
inject the concrete type directly — `GridSpawnerCoordinator.RunStagesAsync` only exposes the
generic `IGridSpawner.SpawnAsync(ct)` interface, which can't pass a staging parent through, so the
statics stage is now spawned via a direct call, bypassing the coordinator for this one case.
**Known scoping gap:** if a future `SpawnStage.DynamicActors`-priority spawner is ever added, it
won't participate in the staged reveal unless this direct-call pattern is extended to include it too
(today nothing is registered at that priority, so this is currently moot).

**Redesigned again — shared root + local-space rendering (2026-07-05).** Two earlier attempts (a
single staging root the controller moved; then a `DescendTarget` list that reached into the cluster
view controllers) both failed or leaked: moving a transform does nothing for Puff/Bush because
BOTH render in absolute world space, ignoring their GameObject transform — Puff via a
`_SlotCentersWorld` shader uniform compared to `mul(unity_ObjectToWorld, vertex)` world fragments,
Bush via `Graphics.DrawMesh` with per-leaf/branch world `Matrix4x4`s. And having the controller
know about `PuffCloudViewController.View`/`BushViewController.View` was a leaky abstraction (user
feedback: "why would we need to know about puffview or bushview... they should be able themselves to
be parented to the ascended transform, which should be the only thing the controller cares about").
Final design (user chose "render in local space"):
- **`ScenarioContentRoot`** (`Slots/Actor/`, DI singleton) — one scene transform, normally at the
  origin, that every piece of the scenario's static content parents ITSELF under: cluster views
  (in `ClusterViewController.Start`) and per-slot markers (in `StaticActorSpawner.PlaceActor`).
  `LevelTransitionController` injects only this root — no view types, no `.View`. It spawns the new
  statics while the root is at the origin (so cluster views `Configure` at true grid positions), then
  `LevelAscendCinematic.PlayAsync(root, ...)` lifts the root on frame 1 and slides it back to zero;
  the content rides along.
- **Local-space rendering** so a moved transform actually moves the visuals: `ClusterView` now also
  pushes `_SlotCentersLocal` (centers relative to the quad origin) alongside the existing world
  array; `PuffCloud.shader` evaluates its occupancy mask (`SlotFalloff`) against
  `worldPos - objectOrigin` vs `_SlotCentersLocal`. **At rest the object origin equals the bounds
  center, so this is byte-identical to the old world math — normal play is unchanged; only a
  displaced quad moves the silhouette.** Noise + the global disturbance field stay world-anchored.
  `BushView` keeps its baked world matrices but, when its transform is displaced from the position it
  was configured at, offsets every drawn matrix by that delta (a copy-free no-op at rest). No shader
  edit needed for Bush; `Bush.shader`/`BushBake.shader` are editor bake shaders, untouched.
- **Monotonic curve** — the descent curve is now `(0,1)→(1.2,0)` (a 1→0 height fraction). It was
  briefly `(0,0)→(0.6,1)→(1.2,0)` (a hill, leftover from the camera-swoop attempt), which would have
  snapped every target to rest on frame 1 (value 0 at t=0) then moved the wrong way. Fixed in both
  `CinematicsSettings.cs` and the `.asset`.

**⚠️ Needs in-editor + shader verification (untestable via `dotnet build`):** the `PuffCloud.shader`
edit and `BushView` offset are unverified here. The rest-identical claim for puff is by construction
(origin==center at rest) but should be eyeballed.

**Rest-frame sampling fix (2026-07-06):** the flagged flicker was confirmed in-editor — while lifted,
the puff sampled the noise + `_DENSITY_ON` disturbance field at its elevated world position, so the
field UV left `_FieldBoundsMin/Size` and shimmered, and the noise scrolled through the cloud. Fixed by
reconstructing each fragment's REST-frame world position in the shader: `ClusterView` pushes
`_RestOrigin` (the quad's world origin at configure, no lift) once; the shader computes
`wpRest = wpLocal + _RestOrigin` and samples noise/field/shadow at `wpRest` instead of the live world
position, so the whole cloud reads as one rigid object during the slide and the field UV stays in
bounds. `wpRest == wpOrig` at rest, so normal play is byte-identical (only `pixelWorld`'s derivative
still reads `wpOrig` — identical since the two differ by a per-quad constant). The occupancy mask was
already object-relative (`wpLocal`).

**Camera-zoom-left-on fix (2026-07-05, after the down-slide was confirmed working):** the level-up
pan-in zooms the camera onto the tipping trail (and disables `OrthogonalSizeCameraController`), and
`EndPanIn` leaves it there. When the old `LevelUpRestore` beat was removed — on the assumption the
Ascent would take the camera — nothing was left to un-zoom, since the Ascent moves the *scenario*,
not the camera; the camera stayed zoomed after every level-up. Fixed via a new
`CinematicCameraRig.RestoreTweened(duration)` (tweens position + ortho size back to base in unscaled
time, re-enabling the ortho controller on completion; no-ops if `!_hasBaseState`, so it's safe to
call even when the pan-in never ran). **Lesson: deleting a "restore"/cleanup beat because a successor
is *expected* to handle it requires verifying the successor actually does — here the successor was
later redesigned to not touch the camera at all, silently orphaning the un-zoom.**

**Camera un-zoom synced to the pop wave (2026-07-06):** the un-zoom initially fired from
`LevelUpCinematic.OnDismissed` (at dismiss), but the pop wave runs *after* the overflow-drain wait and
over slow-mo — so the camera could finish un-zooming before the balloons even started. Moved the
trigger to `LevelTransitionController`: it calls `_cameraRig.RestoreTweened(EstimatePopWaveSeconds())`
right as the pop wave begins, with a duration matched to the wave's wall-clock length
(`steps × PopWaveBandSeconds / slowMo`), so the un-zoom and the pop read as one beat.
`LevelUpCinematic.OnDismissed` now only resumes + hands back to Game.

**Slow-mo diagonal pop wave (2026-07-06):** the old level's balloons no longer pop all in one frame.
`LevelTransitionController.PopBalloonsInWaveAsync` claims a slow-mo timescale
(`TimeScaleService.Claim(TimeScaleSource.LevelTransition, 0.35)` — new enum value; timescale composes
by MIN, and the popup's `0` freeze is already released by dismiss so `0.35` wins) and pops balloons
band-by-band along anti-diagonals `band = col + row`, advancing from BOTH far corners inward —
top-left (`band 0`) and bottom-right (`band max = (Columns-1)+(Rows-1)`) — so the two fronts meet and
finish at the centre. (Coordinate check: `IndexToWorldPosition` maps `col 0`→left, `row 0`→top since
`y = -row·sep + offset`, so `(0,0)` is visually top-left.) Band cadence is a scaled `UniTask.Delay`
(so slow-mo also stretches the wave). Each pop routes through a new
`BalloonControllerRegistry.TryPopSingle(model)` (resolve → `Unregister` → `HandleBoardClear(true)`) —
the side-effect-free teardown, NOT the projectile-hit `Pop()` path, so no balance/nudge/score fires
(and gameplay is paused anyway). Balloons are snapshotted into bands up front, then popped from the
snapshot as the grid mutates. `0.35` slow-mo and `0.11s` band interval are placeholder tuning like the
ascent values.

**Overlapped the descent with the pop + decoupled the clears (2026-07-06):** the transition used to be
strictly sequential (pop → clear → spawn statics → descend, with balloons cued mid-descent). Per feel
feedback the descent now runs CONCURRENTLY with the pop, and the new balloons spawn once every balloon
has popped (not mid-descent). This required splitting the single `BoardClearMessage` (which cleared
statics + balloons + reloaded the projectile at once — incompatible with a timed wave):
- `StaticActorSpawner.ClearStaticActors()` (extracted from `OnBoardClear`) — static-only clear the
  controller calls directly at pop-start, so new statics can spawn + descend while the wave pops
  balloons in parallel. `BalloonControllerRegistry.ClearAll(bool)` (extracted likewise) — the
  controller sweeps stragglers after the wave.
- The transition NO LONGER publishes `BoardClearMessage` (it clears the board on its own beats).
  `BoardClearMessage` now fires only on run-restart. Projectile reload was decoupled: `ThrowerController`
  chains `Reload` off the dismiss disappear (`OnLevelUpDismissed` → `PlayDisappear(Reload)`) instead of
  reacting to the transition's board-clear.
- `TransitionAsync`: `popTask = PopBalloonsInWaveAsync(ct)` (concurrent) → `ClearStaticActors` +
  spawn statics at origin + `descentTask = PlayAsync(...)` (concurrent, no balloon cue) →
  `await popTask` → `ClearAll(false)` straggler sweep + spawn balloons → `await descentTask`. Descent
  (~2.4s unscaled) overlaps the pop (~2s slow-mo); balloons spawn at pop-end and animate in during the
  last of the descent.

**Missing level-up popup on 2→3 — ROOT CAUSE FOUND + FIXED (2026-07-06).** User's decisive repro: 1→2
popup shows, then level 2 completes ALMOST INSTANTLY and a transition fires with no popup, consistently.
Root cause = **score carry-over auto-completing the next level.** `PublishPoints` renumbered points past
the threshold into `_level+1`, so a big/high-streak pop on level 1 carried its excess into level 2 —
level 2 arrived pre-filled and auto-completed with no player throw. That fired the 2→3 level-up from
trail ARRIVALS with no accompanying new `ScorePointMessage`, so the cinematic never armed and the
level-up landed in a state where the popup was lost → transition without a popup. Fix at the scoring
SOURCE (matching the "cap one level-up per burst, excess lost" choice): `ResolveAttributions` clamps each
color's added points at `required − baseProgress` (a pop can reach at most the threshold, never carry
over); `PublishPoints` renumbering removed. The earlier `OnTrailArrived` value-clamp was reverted (wrong
layer — it capped the value but not the carried trails filling the next level). `ScorePoint_AboveThreshold_*`
test updated from "renumbered" to "excess dropped". **The disproven-hypothesis guards remain in as a
temporary soft-lock backstop — remove once the user confirms 2→3 now shows the popup.** Disproven
hypotheses (don't re-chase): concurrent double-popup (blocked by `CheckLevelUp`'s `_navigation` guard);
pan-in hang (self-terminates via `PanInTick`); `TrailId` mismatch (same `ScorePointMessage` both sides).

**Whole old level slides out during the transition (2026-07-06).** Emptying the grid blinked the old
statics out, and separately left the old balloons pinned while the new scenario descended — both read
wrong. Fix: `ITransitionOutgoingContent` (`Slots/Actor/`) — a GENERAL seam,
`HoldOutgoing(Transform outgoingRoot, float exitDrop)` / `ReleaseOutgoing()` ("keep your outgoing visuals
on screen and slide them out, then drop them"); not cluster-specific, so a future per-slot-rendered actor
plugs in the same way. Implementers: `ClusterViewController` (snapshots a throwaway view from the still-full
registry before the clear, since the single live cluster view can't show old+new) and
`BalloonControllerRegistry` (reparents every live balloon view via `BalloonController.RideOutgoing`; the
pop wave pops them band-by-band as they slide, pool-return reparents each away, so `ReleaseOutgoing` is a
no-op). Everything is offset one `exitDrop` (= `LevelAscend` lift height) BELOW the incoming content on
the SAME scenario root, so as the root descends (new content +exitDrop→rest) the old content rides
rest→-exitDrop and exits the bottom in lockstep. `LevelTransitionController` injects
`IReadOnlyList<ITransitionOutgoingContent>` (decoupled), sets the root to origin + holds BEFORE the pop
wave and the clear, releases in the `finally` after the descent. New balloons (spawned at the pop-end cue)
are not reparented, so they appear at rest while the old slide out.

**Deliberately deferred (art/in-editor dependent, not blocking):** the scenario root's starting
height/descent-duration/spawn-cue-fraction (8 world units, 1.2s, 0.75) are placeholder guesses —
needs an in-editor pass once visible (in particular: is 8 units actually enough to clear the visible
frame at the board's real world scale?). Part D's "cloud sweep VFX" is still unbuilt — today the
statics/clusters just slide down against whatever's rendered above the board (likely plain
background). `TrailMotion.Fall` has no curve pair authored on `FlyingTrail`/`TrailSpawner` yet and
nothing uses it — the "cosmetic falling trails" mentioned in Part D for the pop-out are not built;
outgoing balloons play their normal pop VFX (`HitOutcome.Pop`) in the slow-mo diagonal wave above,
a real improvement over the original silent/instant clear but not the bespoke "falling" effect
originally specified. New balloons are NOT staged/parented — they still spawn directly at
their final position via the existing `BalloonFactory.AnimateSpawn` scale/path tween, triggered
partway through the descent. No EditMode test for `LevelTransitionController`'s sequencing yet (only
`PlayerHealthControllerTests.LevelUp_RestoresStartingHitPoints` was added) — it's UniTask-async and
message-driven throughout, so it's testable with substituted seams, just not done in this pass.

Smaller code surface but cinematic + in-editor heavy. Files:

- **`UI/Score/TrailMotion.cs`** — add `Fall` member; style it in the per-motion styling switch
  wherever `FlyingTrail` (`UI/Score/FlyingTrail.cs`) / `Shared/Pool/TrailSpawner.cs` branch on
  motion (scattered below-frustum targets, gravity-ish curve, gradient fade, **no arrival
  callback** — cannot score by construction).
- **`Shared/GameState/CinematicState.cs`** — add `LevelAscend`.
- **`Configuration/CinematicsSettings.cs`** — add the `LevelAscend` settings entry (traits +
  camera-rig segments: pan up +H into the sky band; restore is an ordinary segment). Follow the
  per-state pattern already in the SO; defaults in field initializers.
- **New `Game/Cinematics/LevelAscendCinematic.cs`** — plain-C# producer over the
  `CameraRigCinematic` runner (mirror `Game/Cinematics/LevelUpCinematic.cs`, registered like
  `GameLifetimeScope.cs:176`). **Do NOT write a MonoBehaviour; do NOT touch `Time.timeScale`**
  (`TimeScaleService` only — enforced by the `timescale-writes` audit rule).
- **New `Game/Level/LevelTransitionController.cs`** — plain C# orchestrator (see Part D
  "Orchestration"): wait for overflow drain (`RejectedBalloonEffect.IsOverflowActive` false) →
  return/reload the frozen projectile → cosmetic falling trails + pop VFX → `BoardClearMessage`
  path (scoreless by construction) → `LevelAscend` pan-in → covered `rig.Restore()` snap →
  stage-gated respawn: statics first, then reveal, then balloon fill.
- **`Slots/Spawner/GridSpawnerCoordinator.cs`** — add a stage-ranged respawn entry point (run
  only `SpawnStage.StaticActors`, later `BalloonActors`) — `RunGroupsAsync` (`:54-64`) already
  groups by stage; expose a filtered variant instead of duplicating it. This is **not** a run
  reset: score/HP untouched.
- **`Game/Health/PlayerHealthController.cs`** — subscribe `ScoreLevelUpMessage` → refill to
  `StartingHitPoints` (locked decision; part of the transition). `LossForecast`
  (`Game/Health/LossForecast.cs`) recomputes naturally off the refilled pool — verify, don't
  change it.

3b test surface is mostly PlayMode/in-editor (flag it); EditMode covers `LevelTransitionController`
sequencing with substituted seams if it's built message/interface-driven.

**Settings split out (2026-07-07).** The `LevelAscend` `CinematicsSettings` entry above (a
camera-rig segment with defaults in field initializers) no longer holds the Ascent's tuning.
Since the Ascent is a transform-descent, not a camera move, its tuning moved to a top-level
`LevelAscendSettings` (`CinematicsSettings.LevelAscend`: descent curve / height / spawn cue /
speed, plus the pop-wave slow-mo tuning that had been `const`s in `LevelTransitionController`).
The `LevelAscend` `_states` entry now keeps only `CinematicTraits`. In the same pass all code
defaults/ctors were stripped from the serialized cinematics types (asset is the sole source of
truth) and `CinematicsSettingsTests` was removed — see `PLAN-CinematicsArchitecture.md`.

### 3c — items lever

**Status: implemented (2026-07-05).** `dotnet build` (all 4 csproj) + `style_audit.py` clean; not
yet committed, not yet run through the actual Test Runner (remember: `dotnet build` only compiles
— see the 3a status note above for why that distinction matters here specifically). One
implementation-time refinement: `Items` stayed `IReadOnlyList<ItemSettings>` (type-gated catalog
entries, for `ItemAssigner` to iterate when building live active-counts) rather than becoming the
"wrapped resolved list" the line below originally proposed — the actual pick happens through a new
`PickItemEntry(activeCounts)` on `IActiveLevelParameters`, mirroring `PickBalloonEntry` exactly
(internal `ResolvedItemEntry : IWeightedEntry` wrapper, not exposed). This keeps `ItemAssigner`'s
live per-board active-count computation (which the resolver can't own — it's board state, not a
per-level constant) cleanly separated from the resolver's per-level weight/cap resolution. Also:
`ItemTypeWeight` had no constructor (only `BalloonTypeWeight` got one in 3a) — added one to match.

- **Re-add `ItemCadence`** to `LevelParameters`/`RangedLevelParameters` (dropped in 3a as unused
  scaffolding — see the note on item 5 above) and expose it on `IActiveLevelParameters`, alongside
  the wiring below that actually gives it meaning.
- **`Item/ItemAssigner.cs`** — inject `IActiveLevelParameters`; `CollectCandidates` (`:80-84`)
  iterates `_itemConfig.Items` and gates on `item.TurnCheckEvery` — switch the source list to the
  resolver's resolved item view: candidates = catalog `ItemSettings` whose `ItemType` is in the
  range's `ItemTypeWeight[]` (weight > 0), cadence check uses the resolved `ItemCadence` scalar
  (one per level, replacing per-item `TurnCheckEvery` in the check), pick keeps
  `PickRandom(_activeCountsBuffer)` (`:60`) with a `ResolvedItemEntry : IWeightedEntry` wrapper
  (range weight; `MaximumAllowed` override or catalog fallback) — same bridge shape as balloons.
- **`LevelDifficultyResolver`** — narrow `Items` to the wrapped resolved list;
  `ItemConfiguration`'s per-item `TurnCheckEvery`/`Weight` demote to catalog defaults (annotate,
  don't delete).
- Tests: extend `LevelDifficultyResolverTests` (item absent from range never assigned; override
  vs catalog fallback); update `ItemAssigner` tests if present.

### 3d — custom levels (exact-level overlays)

- **`LevelDifficultyResolver.ResolveFor`** — add the specificity step: exact `CustomLevelEntry`
  match wins, else containing range (customs are full `LevelParameters` blocks — no cascade, v1
  locked decision).
- **`LevelPacingConfiguration.OnValidate`** — customs inside the authored level space; no
  duplicate `Level`; warn when a custom sits adjacent to a range boundary it makes invisible.
- Tests: overlay specificity (custom at 10 inside range 8–14 wins at 10 only); validation
  warnings.
- **⚠️ Watch for the same bug 3a hit (found 2026-07-05 via the actual Test Runner, not `dotnet
  build` — which only compiles, never executes):** `CustomLevelEntry` is a struct wrapping a
  `LevelParameters` class field with **no constructor**. A struct's implicit default constructor
  zero-initializes every field including class-typed ones, so any `new CustomLevelEntry()` written
  in test code or a field initializer (not authored through the Unity Inspector, which does
  materialize a real nested instance) leaves `Parameters` **null** — exactly what happened to
  `LevelRangeEntry` before it got an explicit constructor (`Configuration/LevelRangeEntry.cs`).
  Give `CustomLevelEntry` the same treatment (a constructor taking `int level, LevelParameters
  parameters`) before writing any test that does `new CustomLevelEntry()` directly.

### 3e — grid-actor pacing (Puff/Bush now; richer archetypes still 8.3-blocked)

**Status: implemented (2026-07-05).** `dotnet build` (all 4 csproj) + `style_audit.py` clean;
not yet committed. One correction made during implementation: the plan below describes a single
`GridActorTypeGate` struct carrying a `RangedInt` used in *both* the authored and resolved forms —
that doesn't actually work, because `RangedInt` is unresolved (min/max/mode) and `LevelParameters`
(the resolved form) needs a plain already-rolled `int`, exactly like `SpawnLines`/`BoardLines` are
`RangedInt` in `RangedLevelParameters` but plain `int` in `LevelParameters`. Built two types
instead: `GridActorTypeGate` (`GridActorType` + `RangedInt`, authored form, lives only on
`RangedLevelParameters`) and `ResolvedGridActorGate` (`GridActorType` + plain `int`, resolved
form, lives only on `LevelParameters`) — `RangedLevelParameters.Resolve` converts one array to the
other, same relationship as the top-level scalars.

**Scope note:** only what `StaticActorSpawner` can already build — `Puff`/`Bush` (its
`_modelFactories` dict has no entries for `Deflector`/`Absorber`/`Gatekeeper`, and those have no
art/prefabs yet per `PLAN-GridActorExpansion.md`'s Phase 8.3 status). Adding a new archetype to
the factory dict is 8.2-series/8.3 work, unrelated to this sub-phase — 3e only makes the *existing*
two types range-aware. **Shape is deliberately not a weighted-pick bridge** like balloons/items:
`GridActorPrefabEntry.Weight` (`Configuration/GridActorPrefabEntry.cs`) is declared but never read
by `StaticActorSpawner.SpawnStaticActors` (`Slots/Actor/StaticActorSpawner.cs:73-101`) — the
algorithm rolls `Random.Range(entry.MinCount, max+1)` **per catalog entry independently**, not a
competitive draw between types, so there's nothing to multiply a range weight against. Don't add
an unused `Weight` field to the new type mirroring `BalloonTypeWeight`'s shape — that field would
sit dead until 8.3's real `GridSpawner.PickEntry` replaces this algorithm (the `ItemCadence`
lesson from 3a: don't pre-scaffold fields nothing reads).

- **New `Configuration/GridActorTypeGate.cs`** — `[Serializable] struct`: `GridActorType _type;
  RangedInt _count;` (mirrors `BalloonTypeWeight`'s membership-is-the-gate idea, but carries a
  `RangedInt` instead of a `float _weight` — presence in the range's array is the gate, absence
  means the type doesn't spawn at all this level; the `RangedInt` replaces both `MinCount` and
  `MaxCount` with the single resolved-once-per-level value the codebase's "resolve per level, not
  per turn" convention expects — resolved once, not independently re-rolled every board
  population like today's `Random.Range` call).
- **`RangedLevelParameters`/`LevelParameters`** gain `GridActorTypeGate[] _gridActorGates`
  (default: `{ new GridActorTypeGate(GridActorType.Puff, new RangedInt(3, 6, Random)) }` — a
  plausible non-empty baseline, same "always-valid minimal default" convention as
  `_balloonWeights`). `RangedLevelParameters.Resolve` resolves each gate's `RangedInt` into a
  plain `(GridActorType Type, int Count)` pair the same way it resolves `SpawnLines`/`BoardLines`.
- **`IActiveLevelParameters`** gains `bool TryGetGridActorCount(GridActorType type, out int
  count)` — false if the type isn't gated in this level (matches the balloon type-gate's
  absent-means-excluded semantics; deliberately not a `PickEntry`-style method since there's no
  competitive draw to bridge).
- **`Slots/Actor/StaticActorSpawner.cs:77`** — `foreach (var entry in _gridActorConfig.Entries)`
  gains a guard: `if (!_levelParams.TryGetGridActorCount(entry.ActorType, out var levelCount))
  continue;` (skip types the level doesn't gate in), then replace the per-entry
  `Random.Range(entry.MinCount, max + 1)` roll (`:84-87`) with
  `Mathf.Min(levelCount, emptySlots.Count)` — the resolved count *is* the roll, resolved once at
  the level's start rather than independently re-rolled at every board population. `MaxPerCluster`/
  `PlacementMode`/`HitsToPop`/prefab stay catalog-only (unchanged) — same catalog/range split as
  balloons. Inject `IActiveLevelParameters` alongside the existing `IGridActorConfiguration`
  (catalog stays for `Entries` iteration order, `PoolKey`, `Prefab`, placement mode).
- **`OnBoardClear`/`RegisterPools`** (`:124-138`, `:167-185`) — unchanged, they iterate the full
  catalog regardless of gating (pools must exist for every type any range might ever use; teardown
  must find and return every possible spawned type).
- Tests: extend `LevelDifficultyResolverTests` (`TryGetGridActorCount` false for a gated-out
  type; resolved count matches the authored `RangedInt` across Fixed/Linear/Random); a
  `StaticActorSpawnerTests` case (if a fixture doesn't exist yet, this is a good excuse to add
  one) verifying a gated-out type places zero and a gated-in type places exactly the resolved
  count, using the existing internal test-only constructor (`StaticActorSpawner(SlotGrid,
  IGridActorConfiguration)` at `:55-59` — extend it to also take `IActiveLevelParameters` for the
  test double).
- **Open question for whoever picks this up:** should `MaxCount`/`MinCount` stay on
  `GridActorPrefabEntry` as catalog fallbacks (used only when a level range doesn't gate the type
  in explicitly — but that can't happen once every range has an explicit gate) or be deleted the
  same way `IBalloonsConfiguration`'s dead spawn-line properties are slated for deletion once the
  pacing asset is trusted? Lean toward keeping them until `LevelPacingConfiguration` covers every
  authored level (same sequencing reasoning as the balloon catalog cleanup).

### Phase 4 — allowed colors (breakdown; reuses 3a plumbing)

**Status: implemented (2026-07-05).** `dotnet build` (all 4 csproj) + `style_audit.py` clean; not
yet run through the actual Test Runner. Implemented essentially as spec'd below, plus three gaps
found only by tracing every consumer of `_palette.Colors`/the raw formula, none of which were
called out in the original breakdown:
- `IActiveLevelParameters` needed a new `int AllowedColorsMask` alongside the existing
  `IReadOnlyList<string> AllowedColors` — the spawn filter needs the raw bits to intersect against
  the prefab mask, `ScoreController`/UI need names. Both are resolver-cached, not recomputed.
- **`ToughBalloonModel`/`BubbleClusterModel` bypassed the gate entirely** — unlike plain `BalloonModel`
  (which scores via its own already-gated `Color.Value`), these two distribute score attribution
  across a *randomly chosen palette color* on pop (`_palette.Colors[Random.Range(...)]`), with no
  color of their own. Threaded `IActiveLevelParameters.AllowedColors` through
  `BalloonModelFactory.Create` (new optional param, default null so every existing test call site
  compiles unchanged) into both models' constructors; `ResolveScoreAttribution` now draws from the
  active set when provided, falling back to the full palette only when not (tests).
- **`LevelUpPopUp`** spawns one glow trail per palette color per level-up (`_palette.Colors` in
  `ShowAfterGateAsync`/`SpawnGlowTrailsAsync`) and sizes `_glowTrailTotalCount` off the full
  palette — both switched to `_levelParams.AllowedColors`, otherwise the ceremony would fire glow
  trails from hidden/inactive bars and the arrival-count gate would never reach 100% during the
  tutorial ramp.
- `TriggerLevelUpCheat`/`NearLevelUpCheat` also filled every palette color, not just active ones —
  switched to `_levelParams.AllowedColors` for consistency (debug-only, but a cheat that no longer
  matches the real level-up requirement is worse than useless for testing it).
- Also fixed in passing: `ColorProgressBar.OnLevelUp` read `IGameConfiguration.PointsRequiredForLevel`
  (the raw formula) instead of `IActiveLevelParameters.PointsRequiredForLevel` (the resolved,
  composed value) — same class of bug as the two cheats fixed in 3a, found by grepping for every
  remaining `PointsRequiredForLevel`/`_config.` call site while touching this file.
- **Dynamic bar count — fixed after in-editor feedback (2026-07-05):** the bars sit inside a
  `HorizontalLayoutGroup`, so the initial `CanvasGroup`-only fade (alpha/interactable/blocksRaycasts)
  left a visible gap — the layout group still reserved space for a hidden bar. Added a
  `LayoutElement` alongside the `CanvasGroup`, toggling `ignoreLayout` in lockstep with the fade;
  its setter marks the layout dirty automatically, so the row reflows on the next layout pass with
  no manual rebuild call needed. Both `CanvasGroup` and `LayoutElement` self-fetch/self-add in
  `Awake()` if not manually assigned, so a bar prefab predating either field degrades gracefully
  instead of throwing `UnassignedReferenceException` (hit once in-editor before this fallback
  existed).
- **Ceremony color-set race — fixed after in-editor feedback (2026-07-05):** `ScoreLevelUpMessage`
  has two independent subscribers — `LevelDifficultyResolver` (re-resolves to the new level) and
  `LevelUpPopUp`/`ColorProgressBar` (celebrate the level that just completed) — with subscriber
  order unenforced. Reading the live `IActiveLevelParameters.AllowedColors` from either UI
  consumer could therefore already reflect the *new* level's set, bursting glow trails from (and
  revealing) bars that never actually contributed to the just-completed level. Fixed by having
  `ScoreController.CheckLevelUp` snapshot `AllowedColors` **before** publishing (safe: the
  resolver replaces its list wholesale on resolve, never mutates the one already handed out) and
  carrying it on the message as `ScoreLevelUpMessage.CompletedColors` (2-arg ctor, 1-arg overload
  kept for existing call sites). `LevelUpPopUp`'s glow-trail spawn now takes `msg.CompletedColors`
  as a parameter instead of reading live state. `ColorProgressBar.ApplyVisibility()` moved from
  `OnLevelUp` to `OnDismissed` — a newly-active color's bar now fades in only after the ceremony
  is fully over, not mid-celebration of a level it had no part in.

- **Spawn filter**: `Balloon/Type/ColorableBalloonVariant.PickColor` (`:24-47`) draws from the
  full palette masked by the per-prefab `[PaletteColorMask] _allowedColorsMask` (`:12`). Since
  `LevelParameters.AllowedColorsMask` is now the same `int` bitmask shape (changed in 3a,
  2026-07-05 — see item 5 above), the active-set filter is a plain `_allowedColorsMask &
  levelParams.AllowedColorsMask` instead of a set-intersection over two `string[]`s — thread the
  resolved mask via the spawner (`Bind`/`Initialize` path) rather than injecting
  `IActiveLevelParameters` into the variant; resolve the interaction: prefab mask = static
  capability, active set = level gate; empty intersection ⇒ `OnValidate`/runtime warning + fall
  back to the prefab mask alone. `IActiveLevelParameters.AllowedColors` (the `IReadOnlyList<string>`
  the resolver already exposes) is for consumers that want names, not bits — use the mask directly
  here instead of round-tripping through names.
- **Scoring**: `ScoreController` — per-color progress is `Dictionary<string,int> _levelProgress`
  (`:21`) + `_colorKeys` (`:32`); level-up requires all colors at threshold. Restrict the
  requirement to `_levelParams.AllowedColors` (the name list — this consumer wants names, matching
  `_levelProgress`'s string keys); on a boundary where the set **grows**, the new color joins at
  progress 0 from that level onward (per-level resolution makes the set stable mid-level — locked).
- **UI**: `UI/Score/ColorProgressBar` is one instance per color with a serialized
  `[PaletteColorName] string _colorName` (`:24`) — bars for inactive colors must hide/fade
  (`CanvasGroup`, not `SetActive` — remember the GameOver-panel Animator gotcha), driven by a
  binder reading `AllowedColors` on level change (`ScoreLevelUpMessage` subscriber list today:
  `LevelUpPopUp.cs:40`, `ColorProgressBar.cs:39`, `ColorStreakTracker.cs:14`).
- Steady state = 4 colors forever, so the dynamic path only runs during the tutorial ramp.

### Suggested commit sequence

1. 3a data types + SO + validation + tests (no consumers touched — pure addition, green build).
2. 3a resolver + DI + the read-site swaps + test updates (behavior identical under the replicating
   asset) — remember the cheats (`TriggerLevelUpCheat`, `NearLevelUpCheat`) alongside the spawner/
   danger/score sites; both read `PointsRequiredForLevel` too and are easy to miss in a grep for
   "Configuration" since they already imported it for other reasons.
3. 3b Ascent (own commit; heavy in-editor verification).
4. 3c items → 5. 3d customs → 6. **3e grid-actor pacing (Puff/Bush) — independent of 3b/3c/3d,
   can land any time after 3a** → 7. Phase 4 colors (spawner → score → UI as separate commits).

### Testability & default-removal follow-ups (raised 2026-07-05, deferred — not blocking 3b+)

Four small items surfaced discussing 3a after it landed. None block later sub-phases; do them
whenever they're convenient.

- **A "jump to level N" cheat** — the actual ask that started this: today, testing level 50's mix
  means grinding through 49 level-ups (`TriggerLevelUpCheat` only advances one level at a time via
  `ScoreCheatHelper.FillColor`). Add a `JumpToLevelCheat : ICheat` (mirror
  `Cheats/TriggerLevelUpCheat.cs`) that takes a target level, calls
  `ScoreController`/`LevelDifficultyResolver` directly rather than looping the fill-and-check path
  (looping `required` levels would still be O(N) work and re-trigger every level-up side effect N
  times — cinematics, HP refill, etc. — which is both slow and not what "just show me level 50"
  wants). Needs a small new seam: `ScoreController` has no public "set level directly" — either add
  an internal `JumpToLevel(int level)` that sets `_level.Value` and publishes
  `ScoreLevelUpMessage` once (skipping the ceremony) or thread it through `RunController`. Surface
  the target level via the existing `CheatConsoleView` (check whether it supports a parameterized/
  text-entry cheat, or start at a handful of preset jump targets like "Jump to 10/25/50" if it
  doesn't). **Not built — first ask when picking this up.**
- **Extract a shared validator.** `LevelPacingConfiguration.OnValidate` (editor-only, hand-written
  warnings) and the recommended "resolve levels 1..50 without throwing" EditMode test currently
  check the same invariants two different ways and can drift. Extract a plain
  `LevelPacingValidator.Validate(ILevelPacingConfiguration, IGameConfiguration) : IReadOnlyList<string>`
  (issue messages, no Unity/editor dependency) that both call — `OnValidate` logs each issue,
  the EditMode test asserts the list is empty. This is also the seam a future "test a hypothetical
  config" editor tool would call before a designer commits changes, and it's what actually answers
  "test any possible configuration" in the general sense (a jump-to-level cheat answers it for the
  in-game/visual sense — the two are complementary, not redundant).
- **Escalate `LevelDifficultyResolver.FallbackParameters` from a silent default to a hard failure.**
  It exists for "no authored range contains this level," which the extracted validator (above) is
  specifically meant to make unreachable in anything that passed validation. Per the project's
  don't-defend-against-things-that-can't-happen stance, once the validator is trusted this should
  throw (or at minimum `Debug.LogError`, not `LogWarning` + a silent substitute) — a shipped gap in
  the level ranges should be loud, not quietly patched over with a Simple-only fallback a player
  might not even notice.
- **Delete (not just annotate) the dead catalog properties** — `IBalloonsConfiguration
  .GameStartedBalloonLines`/`NewProjectileBalloonLines` are unread by any code as of 3a (verified:
  only `LevelDifficultyResolver`/`IActiveLevelParameters` are read now); they're kept today purely
  as the numbers to copy into the first authored range. Once
  `Assets/Configuration/LevelPacingConfiguration.asset` exists, is wired, and is confirmed to
  reproduce today's feel, delete both properties from `IBalloonsConfiguration`/`BalloonsConfiguration`
  so there's exactly one place that claims to know the spawn rate. Doing this before the asset
  exists would remove the only reference for what to author — sequence it after, not before.

---

## Risks & interactions

- ~~**Settle timing:**~~ N/A under the HP model — a blocked spawn is determined **synchronously**
  against the grid *model* (the balancer updates the model before the view tweens), so there's no
  mid-animation false trigger to guard against.
- **Cinematic overlap:** GameOver and the level-up cinematic must be mutually exclusive — handled,
  since loss goes through `RunController.EndRun`, which already no-ops during the cinematic.
- ~~**Persistence:**~~ **Resolved in Phase 1** — `ScoreController` is run-scoped (no cross-session
  level/score save); only best-level/best-score persist via `IRunMeta`, and `ResetRun` clears run
  state on restart.
- **`PointsRequiredForLevel`** is a steep exponential — with real stakes, the early curve
  needs a playtest pass (a `return 12;`-style flat curve was only a debug hack).

---

## Open questions

1. ~~Runs vs. persistent progression~~ — **✅ Resolved: run-based** (see gating decision above).
2. ~~Deadline row vs. spawn-saturation vs. both~~ — **✅ Resolved: spawn-saturation damage to a
   player HP pool** (1 HP per un-spawnable balloon; loss at 0; hearts-bar UI). No deadline row. See
   *Phase 2 — detailed breakdown*.
3. ~~Restart flow~~ — **✅ Resolved: in-place** grid reset + state clear, via an ordered
   `IRunResettable` harness (scene reload rejected — avoids a visible reload). See *Phase 1 —
   detailed breakdown*.
4. **Meta on loss** — best level / best score is the baseline; currency/unlocks TBD.
