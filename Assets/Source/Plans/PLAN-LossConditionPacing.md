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
   placeholder "you lost" + **in-place restart**; make `ScoreController` run-scoped (reset via an
   ordered `IRunResettable` harness, persist best-level/best-score meta only). Foundation; bakes
   in the run-based decision. **See *Phase 1 — detailed breakdown* below** for the full task
   list, testability seams, and test plan.
2. **`BreachDetector` + deadline** — encroachment loss works; tune the deadline by hand.
   **▶ Next — see *Phase 2 — detailed breakdown* below.**
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

### Follow-on (after Block A) — loss cinematic

`GameOverLossEffect` mirroring `LevelUpTrailEffect`: a new `CinematicState` value, `BeginCinematic`
+ `PauseService.Pause(Cinematic)`, a camera push-in / slow-mo `Time.timeScale` curve via DOTween
(`.SetUpdate(true)`), then `EndCinematic` → GameOver screen. Likely a `GameOverLifetimeScope` with
a `CinematicEndGate`, mirroring `LevelUpLifetimeScope`. Self-contained; needs in-editor tuning.

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

## Phase 2 — detailed breakdown

**Status: not started.** Phase 2 makes the loss *real*: a `BreachDetector` watches the settled
grid each turn and ends the run when balloons choke up to a deadline near the thrower. It plugs
into the Phase 1 seam — on breach it calls **`RunController.EndRun()`**, which already commits the
meta record, publishes `GameOverMessage`, gates against the level-up cinematic / non-`Game` state,
and transitions to `GameOver`. `ForceGameOverCheat` stays as a manual trigger; `BreachDetector`
becomes the automatic one. (No new state, save, or UI work — Phase 1 built all of that.)

### Grid orientation (grounding)

`SlotGrid` row **0 = top / far from thrower**; row **`Rows-1` = bottom / entry**
(`IndexToWorldPosition` maps a higher row index to a lower world Y). Balloons enter at the bottom
and pack **upward** toward row 0 (`BalloonBalancer`); as the board saturates, the filled front
descends back toward the entry. So the **deadline is a high row index** near `Rows-1`, and
`Rows = IGameConfiguration.SlotsSize.y`.

### The crux: there is no "board settled" signal (must create the wait)

`BalanceBalloonsMessage` is published **immediately after the balance tweens are *enqueued***
(`BalloonSpawner.SpawnLine` / `SpawnLinesWithDelayAsync`), **not** after they finish — and balance
+ spawn animate over `~IBalloonsConfiguration.TimeForBalloonsBalance`. Evaluating the breach at that
message reads a mid-animation grid and false-triggers (see Risks → *Settle timing*). Each actor
exposes `IsStable` (`ReactiveProperty<bool>` on `BalloonModelBase`): `false` while its spawn/balance
tween runs, `true` on `OnComplete`. **Settle = every dynamic actor has `IsStable == true`.**

**Recommended:** on `BalanceBalloonsMessage`, `BreachDetector` does
`await UniTask.WaitUntil(() => AllDynamicActorsStable())`, then evaluates — no new message type
needed. (Alternative: a dedicated `BoardSettledMessage` published by a tracker once the last actor
stabilises; heavier — defer unless another consumer needs it.)

### Tasks

| # | Task | Touches / creates |
|---|------|-------------------|
| 1 | **Deadline config** — add `BreachDeadlineRowsFromBottom` (int, grid-height-independent) to `IGameConfiguration` + `GameConfiguration`; absolute deadline = `Rows - rowsFromBottom`. Phase 3's `LevelRangeConfiguration` can vary it per range later; a single field is enough now. | edit `Shared/IGameConfiguration.cs`, `Configuration/GameConfiguration.cs`; set the value on the SO asset (in-editor) |
| 2 | **`BreachDetector`** (`IStartable`, `IRunResettable`) — subscribe to `BalanceBalloonsMessage`; per turn, `await` settle, then if any actor occupies `row >= deadline` call `RunController.EndRun()`. **Generation guard** (mirror `BalloonBalancer`/`BalloonSpawner`) so a settle-check from a pre-restart turn is dropped; `ResetRun` at `RunResetOrder.Quiesce`. | **new** `Game/Run/BreachDetector.cs`; injects `SlotGrid`, `IGameConfiguration`, `ISubscriber<BalanceBalloonsMessage>`, `RunController`; register in `GameLifetimeScope` (`RegisterEntryPoint…AsSelf().As<IRunResettable>()`) |
| 3 | **Settle + occupancy helpers** — `AllDynamicActorsStable()` (iterate grid, check `IsStable` on `IWriteableDynamicSlotActor`s) and `RowOccupied(row)` (`!IsEmpty(col,row)` across columns). | small helpers in `BreachDetector` (or `SlotGrid` if reused elsewhere) |
| 4 | **Test trigger** — keep `ForceGameOverCheat`; optionally add a `FillToDeadlineCheat` (spawn lines until breach) to exercise `BreachDetector` in-editor without grinding. | optional new `Cheats/` cheat |
| 5 | *(optional, secondary trigger)* **Spawn saturation** — `SpawnLineInternal` silently skips a column when `FindFirstReachableEmptyRow` returns null; expose "no column could place" and treat as a breach via `EndRun`. Organic, no magic row — but redundant if the deadline sits near the entry. Decide during tuning. | edit `Balloon/Spawner/BalloonSpawner.cs` |
| 6 | **Deadline visual** — danger-line gizmo in `SlotGridView.OnDrawGizmos` at the deadline row's world Y (editor, for tuning); a runtime danger line is later polish. | edit `Slots/Grid/SlotGridView.cs` (`#if UNITY_EDITOR`) |
| 7 | **Tests** — EditMode: breach-eval logic on a real `SlotGrid` with placed actors (actor at/below deadline → `EndRun`; above → none), driving `RunController` via the `INavigation`/`ICinematicState` substitutes (as `RunControllerTests` does). PlayMode: fill the board to the deadline → assert `GameOver` fires (extend the `RunRestartPlayModeTests` style). | **new** `BreachDetectorTests` (EditMode) + a PlayMode case |
| 8 | **Tuning** — set `BreachDeadlineRowsFromBottom`, then playtest lines-per-turn vs pop-rate vs the level-threshold curve. | config asset + playtest |

### Settle-eval timing & safety

- **Evaluate only after settle** (task 2) — the `WaitUntil` avoids the mid-animation false trigger.
- **Cinematic / restart safety** — `EndRun` already no-ops during the level-up cinematic and outside
  `Game`; a deferred breach simply re-evaluates on the next settled turn. The generation guard drops
  a settle-check whose turn predates a restart, so a stale breach can't fire `GameOver` right after
  `RestartRun`.
- **Initial spawn** — initial lines fill from the top; with the deadline near the bottom they
  shouldn't trip it. Confirm during tuning rather than special-casing.

### Decision to make (Open question #2)

**Recommended: deadline-row as the primary trigger** — readable, hand-tunable, and visualizable
(the danger line). Treat **spawn-saturation as an optional safety net** (task 5), added only if
tuning shows balloons can fully saturate without crossing the deadline. Don't build both up front.

---

## Risks & interactions

- **Settle timing:** evaluate the breach only after the post-spawn balance finishes, or a
  mid-flight balloon transiting a low row trips a false loss.
- **Cinematic overlap:** GameOver and the level-up cinematic must be mutually exclusive
  (gate the breach check during `LevelUp`).
- ~~**Persistence:**~~ **Resolved in Phase 1** — `ScoreController` is run-scoped (no cross-session
  level/score save); only best-level/best-score persist via `IRunMeta`, and `ResetRun` clears run
  state on restart.
- **`PointsRequiredForLevel`** is a steep exponential — with real stakes, the early curve
  needs a playtest pass (a `return 12;`-style flat curve was only a debug hack).

---

## Open questions

1. ~~Runs vs. persistent progression~~ — **✅ Resolved: run-based** (see gating decision above).
2. **Deadline row vs. spawn-saturation vs. both** for the loss trigger — **recommendation in
   *Phase 2 — detailed breakdown*: deadline-row primary, spawn-saturation as an optional safety net.**
3. ~~Restart flow~~ — **✅ Resolved: in-place** grid reset + state clear, via an ordered
   `IRunResettable` harness (scene reload rejected — avoids a visible reload). See *Phase 1 —
   detailed breakdown*.
4. **Meta on loss** — best level / best score is the baseline; currency/unlocks TBD.
