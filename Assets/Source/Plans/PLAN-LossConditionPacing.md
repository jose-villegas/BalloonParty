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

**External dependency:** the grid-actor per-range mix needs Phase 8.3 (procedural placement,
see `PLAN-GridActorExpansion.md`); everything else uses mechanics that exist today.

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
1. **Phase 3** — level-range difficulty (`LevelRangeConfiguration` + `RangedValue` +
   `LevelDifficultyResolver`/`IActiveLevelParameters`), then allowed-colors (Phase 4). Spec-only
   below; memory `phase3-level-pacing` holds the locked decisions and verified read-sites.
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
