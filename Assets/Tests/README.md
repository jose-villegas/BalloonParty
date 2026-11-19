# BalloonParty — Testing Standards

> This document is the authoritative reference for unit testing philosophy, conventions, and coverage across `Assets/Tests/`. All new tests must conform to the rules defined here.

---

## Philosophy

Based on [JUnit best practices](https://junit.org/junit4/faq.html#best):

- **"Too simple to break"** — getters, setters, simple forwarding, and auto-properties cannot break unless the compiler is broken. Don't test them.
- **Test what could reasonably break** — conditional logic, algorithms, boundary guards, math formulas, side effects with real consequences.
- **When a bug is reported** — write a failing test that exposes it, then fix it. The test prevents regression.
- **If something is hard to test** — that's a design improvement opportunity, not an excuse to skip testing.
- **Models are too simple to break** — `BalloonModel` and `ProjectileModel` are mostly pure data bags: auto-properties and `ReactiveProperty<T>` field declarations. Exception: `BalloonModel.EvaluateHit` contains hit-outcome decision logic that both `BalloonController` and `ScoreController` depend on — this is tested because it could reasonably break.

---

## What NOT to Test

| Pattern | Example | Why |
|---|---|---|
| Auto-property getters/setters | `model.Speed = 5; Assert(model.Speed == 5)` | Tests the C# compiler |
| ReactiveProperty get/set | `model.Color.Value = "Red"` | Tests UniRx, not our code |
| Explicit interface forwarding | `IBalloonModel.Color => Color` | Just returns the same field |
| Constructor stores field | `_config = config` | Too simple to break |
| Simple delegation | `ViewAt(index) => _views[index]` | Array access, can't break independently |

---

## What TO Test

| Pattern | Example | Why it could break |
|---|---|---|
| Guard clauses with side effects | `Place` double-occupation guard | Protects against silent data corruption |
| Boundary/range checks | `IsEmpty` for out-of-bounds | Off-by-one errors, missing conditions |
| Conditional branching | `IsUnbalanced` even/odd row logic | Wrong shift direction flips behavior |
| Algorithms | `OptimalNextEmptySlot` weight calculation | Recursive logic, tie-breaking rules |
| Math formulas | `IndexToWorldPosition` staggered grid | Wrong offset or sign breaks the grid |
| Reflection/bounce physics | `PredictionTraceCalculator` | Wall detection, direction math |
| Hit routing decisions | `BalloonModel.EvaluateHit` | Damage thresholds, unbreakable flag, `DamageFlags.Piercing` bypass |
| Interface conformance | `BalloonModel is IHasWriteableItemSlot`, `ToughBalloonModel is not` | Structural item-eligibility contract — affects `ItemAssigner` |
| Multi-tier override cascades | `NudgeService.ResolveDistance` | Priority inversion between balloon, publisher, and config defaults |
| Weighted selection with caps | `BalloonsConfiguration.PickRandom` | MaxCount filtering, cumulative weight edge cases |
| Pipeline filtering | `ItemAssigner.OnItemCheck` | Turn modulo, cap enforcement, eligibility gating |
| Neighbor paint targeting | `PaintItemHandler.Activate` | `IHasWriteableColor` filter, same-color skip, empty-color guard |
| Static index generation | `SlotGrid.HexNeighborIndices` | Even/odd shift direction (consumed independently by PaintItemHandler) |
| Streak state machine | `ScoreController.GetStreak` | Reset on color change, reset on level-up, multiplied into published points |

---

## Test Infrastructure

### Stack

| Tool | Role |
|---|---|
| [NUnit](https://nunit.org/) | Test framework — `[TestFixture]`, `[Test]`, `[SetUp]`, `[TearDown]` |
| [NSubstitute](https://nsubstitute.github.io/) | Mocking — `Substitute.For<T>()`, `.Returns()`, `.Received()` |
| Unity Test Runner | EditMode runner — **Window → General → Test Runner → EditMode** |

### Conventions

- **Most tests are EditMode** — pure C#, run in milliseconds. A small **PlayMode** suite (`Assets/Tests/PlayMode/`) covers behaviour EditMode can't drive — the async/pooling/scene paths (see the PlayMode section below). Reach for PlayMode only when a test genuinely needs the player loop or a live scene.
- **Real objects over mocks** — use real `BalloonModel`, `SlotGrid`, etc. when the class is a plain C# type. Reserve NSubstitute for interfaces (`ISlotGridConfig`, `IProjectileFlightConfig`, `IPublisher<T>`, `ISubscriber<T>`) and ScriptableObjects that need reflection setup.
- **MessagePipe subscriber capture** — `ISubscriber<T>.Subscribe(Action<T>)` is an extension method that wraps the action in `AnonymousMessageHandler<T>` and calls the interface's `Subscribe(IMessageHandler<T>, ...)`. Capture the handler via NSubstitute:
  ```csharp
  IMessageHandler<MyMessage> handler;
  subscriber
      .Subscribe(
          Arg.Do<IMessageHandler<MyMessage>>(h => handler = h),
          Arg.Any<MessageHandlerFilter<MyMessage>[]>())
      .Returns(Substitute.For<IDisposable>());
  // After Start():
  handler.Handle(new MyMessage(...));
  ```
- **ScriptableObject + reflection** — for `ScriptableObject` configs with `[SerializeField] private` fields, use `ScriptableObject.CreateInstance<T>()` and set fields via reflection. Destroy in `TearDown`.
- **Deterministic over random** — when testing weighted-selection or random-assignment logic, use single-candidate scenarios to eliminate `Random.Range` non-determinism.
- **PlayerPrefs isolation** — tests that touch `PlayerPrefs` must clean all used keys in both `SetUp` and `TearDown`.
- **`[InternalsVisibleTo]`** — `AssemblyInfo.cs` in `Assets/Source/` exposes internals to `BalloonParty.Tests.EditMode`. Use `internal` visibility for methods that need direct testing but should not be public API.

---

## Current Coverage — 294 tests

> Last updated: **July 2, 2026**

### `SlotGridTests` — 44 tests

Tests the core grid data structure — the most complex pure-logic class in the codebase.

| Area | Tests | What could break |
|---|---|---|
| Place guard | 1 | Double-occupation silently overwrites a balloon |
| IsEmpty bounds | 1 | Out-of-range index throws instead of returning true |
| IsUnbalanced | 14 | Even/odd row shift direction, row-0 edge case, diagonal vs direct support, static actor as structural support |
| OptimalNextEmptySlot | 5 | Weight tie-breaking (`>=`), out-of-bounds candidate, recursive weight, row-0 null |
| BottomEmptySlotPerColumn | 1 | Skipping logic returns wrong row |
| GetNeighbors | 3 | Even/odd diagonal shift direction, boundary filtering |
| HexNeighborIndices | 2 | Even/odd diagonal shift (used independently by PaintItemHandler) |
| IndexToWorldPosition | 2 | Staggered grid formula — even/odd offset |
| IsKind | 3 | Empty slot returns false; occupied slot matches/mismatches kind |
| IsTraversable | 3 | Empty slot, `IPassThrough` actor, blocking actor |
| ComputePath | 5 | Vertical path length, last waypoint, passthrough intermediate, out-of-bounds source, same source+target |
| AllEmptySlots | 2 | Empty grid returns all, partial fill excludes occupied |
| InBounds | 2 | In-grid true (incl. `Vector2Int` overload); off each edge false — shared by PressurePropagation and SlotClusterRegistry |

### `PredictionTraceCalculatorTests` — 7 tests

Tests the trajectory bounce algorithm — pure math with wall reflection.

| Area | Tests | What could break |
|---|---|---|
| Straight shot | 1 | Top wall detection |
| Top wall termination | 1 | `maxBounces = 0` logic |
| Left/right wall bounce | 2 | Reflection point clamped to limit |
| Max bounces | 1 | Loop termination |
| Max steps | 1 | Step exhaustion before wall hit |
| Zig-zag | 1 | Multiple reflections chain correctly |

### `ScoreControllerTests` — 25 tests

Tests the scoring pipeline, level-up logic, streak multiplier, `WillLevelUp` projected-progress check, next-level trail renumbering, and the run-scoped lifecycle (no cross-session persistence; reset via `IRunResettable`) — deferred scoring via trail arrival, multi-map accumulation with an all-colors threshold gate, consecutive same-color pop multiplier, projected vs confirmed progress, `ScorePointsGroupMessage` field correctness, and `IHitable`-based scoring with non-balloon actors.

| Area | Tests | What could break |
|---|---|---|
| Hit that doesn't kill | 1 | Off-by-one on survive check |
| Valid pop publishes scored message | 1 | Message not published or wrong fields |
| Trail arrival accumulates score | 1 | Wrong dictionary key or sum |
| Level-up when all colors meet threshold | 1 | `Any(p < required)` — wrong comparator |
| No level-up when one color is short | 1 | Partial threshold confusion |
| Level-up resets all color progress | 1 | Level-up resets streak to 0 |
| Pop does not increment level progress | 1 | Score mutated on hit instead of trail arrival |
| Streak starts at 1 on first pop | 1 | Off-by-one in initialization |
| Streak increments on consecutive same-color | 1 | Wrong comparison or missing increment |
| Streak resets on different color | 1 | Missing reset branch |
| Streak multiplies points published | 1 | Multiplication not applied or wrong factor |
| Streak × scoreValue compound | 1 | Only one factor applied |
| Streak resets on level-up | 1 | Missing reset in CheckLevelUp |
| `WillLevelUp` — all colors projected | 1 | Wrong dictionary or comparator on projected map |
| `WillLevelUp` — one color short | 1 | Returns true prematurely |
| Group only publishes granted points | 1 | Cap not applied — over-threshold points leak |
| Group numbered from claimed base | 1 | `FirstScore`/`LastScore` off-by-one against the base |
| Group carries total points | 1 | Wrong `Points` breaks slider/notice value |
| One group message per pop | 1 | Multiple messages break the per-color group contract |
| `IHitable` non-balloon actor — `Pop` outcome scores | 1 | Scoring pipeline too narrowly typed to `IBalloonModel` |
| `Absorb` outcome — does not score | 1 | Absorb mis-routed as Pop |
| Run starts at level 1, ignoring persisted level | 1 | Cross-session restore re-introduced — breaks run-based model |
| `ResetRun` resets level to 1 | 1 | Stale level carries into the next run |
| `ResetRun` clears score and all color progress | 1 | Stale progress carries into the next run |
| Run state is not persisted | 1 | Run leaks to `PlayerPrefs` across sessions |

### `RunControllerTests` — 12 tests

Tests the run lifecycle — loss commit/announce/transition, the suppression gates, and ordered reset. Isolated from the static `Navigation` / `Cinematic` via the `INavigation` / `ICinematicState` seams (substituted with NSubstitute).

| Area | Tests | What could break |
|---|---|---|
| `EndRun` records meta with final level/score | 1 | Snapshot reads the wrong source, or commit skipped |
| `EndRun` publishes `GameOverMessage` once | 1 | Duplicate or missing loss announcement |
| `EndRun` transitions to `GameOver` | 1 | Wrong target state |
| `EndRun` suppressed while a **loss-blocking** cinematic plays (`ICinematicState.Has(BlocksLoss)`) | 1 | GameOver overlaps the level-up cinematic — while the heart-drain must let it through |
| `EndRun` suppressed when not in `Game` | 1 | Loss fires from `LevelUp` / `Launch` |
| `EndRun` suppressed when already `GameOver` | 1 | Re-entrant loss double-commits the meta |
| `RestartRun` invokes resettables in ascending `ResetOrder` | 1 | Teardown order wrong — async/grid reset runs after score |
| `RestartRun` passes one incrementing run number to all resettables | 1 | Per-service generation drift — stale async survives a reset |
| `RestartRun` transitions to `Game` | 1 | Stuck on the GameOver screen after restart |
| `RestartRun` does not record meta | 1 | Best score inflated by a restart |

### `RunMetaTests` — 5 tests

Tests the persisted cross-run record — best level / best score max-keeping and `PlayerPrefs` round-trip. Cleans `BestLevel` / `BestScore` keys in `SetUp` and `TearDown`.

| Area | Tests | What could break |
|---|---|---|
| Higher level updates best level | 1 | Comparison wrong or not persisted |
| Lower level keeps best level | 1 | A worse run overwrites the best |
| Level and score tracked independently | 1 | One field clobbers the other |
| Persists across instances | 1 | Not written, or not reloaded on construct |
| No prefs → defaults to zero | 1 | Wrong default best on a fresh install |

### `BalloonModelTests` — 11 tests

Tests `EvaluateHit` outcomes and `IHasDurability` / `IDynamicSlotActor` / `IHitable` interface conformance.

Design note: `EvaluateHit` is defined on `IHitable` and implemented in `BalloonModelBase`. It is **state-mutating** — it decrements `HitsRemaining` (or zeroes it for `Piercing`) and returns the outcome in a single call. `ProjectileView` calls it once and embeds the result in `ActorHitMessage.Outcome`; `BalloonController` reads `msg.Outcome` without calling `EvaluateHit` again. Tests verify that the outcome is correct and the state change is correct in the same call.

`BalloonModel` returns `PassThrough` on survival (projectile continues, crack animation is reactive). `ToughBalloonModel` overrides to return `Deflect` (projectile bounces). Both return `Pop` on death. `DamageFlags.Piercing` forces `Pop` on any model regardless of `HitsRemaining`.

| Area | Tests | What could break |
|---|---|---|
| Survive → `PassThrough` | 1 | Wrong outcome for soft balloon |
| Exact kill → `Pop` | 1 | Boundary mishandled |
| Overkill → `Pop` | 1 | Negative remainder mishandled |
| Exact kill with higher values → `Pop` | 1 | Arithmetic error at larger numbers |
| Intermediate hit decrements `HitsRemaining` | 1 | State mutation skipped or wrong value |
| Killing blow zeroes `HitsRemaining` | 1 | State mutation missing on final hit |
| `Piercing` flag → `Pop` regardless of `HitsRemaining` | 1 | Flag check missing or not zeroing state |
| Implements `IDynamicSlotActor` | 1 | Interface conformance regression |
| Implements `IHitable` | 1 | Interface conformance regression |
| Implements `IHasDurability` | 1 | Interface conformance regression |

### `ItemSlotTests` — 4 tests

Tests `IHasItemSlot` / `IHasWriteableItemSlot` interface conformance on balloon models. Item eligibility is structural — `BalloonModel` implements the interface; `ToughBalloonModel` does not.

| Area | Tests | What could break |
|---|---|---|
| `BalloonModel` implements `IHasItemSlot` | 1 | Interface missing — `ItemAssigner` can't filter eligible balloons |
| `BalloonModel` as `IHasItemSlot` also implements `IHasColor` | 1 | `IHasItemSlot extends IHasColor` contract broken — item tinting breaks |
| `ToughBalloonModel` does NOT implement `IHasItemSlot` | 1 | Type incorrectly made item-eligible |
| `BalloonModel.Item` defaults to `ItemType.None` | 1 | Wrong default — visual glitch on first spawn |

### `HitableTests` — 6 tests

Tests the `IHitable` / `IHasDurability` capability contract using minimal hand-written actor stubs. No NSubstitute needed.

| Area | Tests | What could break |
|---|---|---|
| `HitOutcome.Absorb` value distinct from others | 1 | Enum value collision |
| `IHitable` actor without `IHasDurability` — correct interfaces | 1 | Removal check mis-skipped |
| `IHitable` absorb wall — `EvaluateHit` returns `Absorb` | 1 | Wrong outcome from IHitable-only impl |
| `IHasDurability` non-deflecting actor — always `Pop`, decrements | 1 | State mutation or return value |
| `IHasDurability` non-deflecting actor — `HitsRemaining` zeroed on final hit | 1 | Final state after single hit |
| Removal check skipped when actor is `IHitable` but not `IHasDurability` | 1 | Compiler/type-check regression |

### `StaticActorTests` — 2 tests

| Area | Tests | What could break |
|---|---|---|
| `StaticActorModel.Kind == Static` | 1 | Wrong `Kind` breaks balancer skip logic |
| `StaticActorModel` is not `IDynamicSlotActor` | 1 | Stability read on a static actor would break |

### `StaticActorSpawnerTests` — 3 tests

| Area | Tests | What could break |
|---|---|---|
| Places exact count when grid has enough slots | 1 | Off-by-one or wrong slot source |
| All placed actors have `Kind == Static` | 1 | Wrong model type used |
| Does not exceed available slots | 1 | Missing guard on slot exhaustion |

### `NudgeOverrideResolverTests` — 10 tests

Tests the 3-tier override resolution cascade (balloon → publisher → config default) and flag-based override matching.

Design note: the resolve logic was extracted from `NudgeService` into `NudgeOverrideResolver` — a standalone class with public methods, testable without `[InternalsVisibleTo]`. `NudgeService` injects the resolver.

| Area | Tests | What could break |
|---|---|---|
| ResolveDistance — balloon override | 1 | Wrong priority in cascade |
| ResolveDistance — publisher override only | 1 | Falls through to default incorrectly |
| ResolveDistance — no overrides (config default) | 1 | Missing fallback |
| ResolveDistance — balloon beats publisher | 1 | Priority inversion |
| ResolveDuration — balloon override | 1 | Duration cascade differs from distance |
| ResolveDuration — config default | 1 | Missing fallback |
| ResolveFalloff — override present | 1 | Falloff resolution differs |
| ResolveFalloff — config default | 1 | Missing fallback |
| `NudgeType.All` flag matches any source | 1 | `HasFlag` logic broken |
| Mismatched flag falls through | 1 | Flag filtering skipped |

### `BalloonsConfigurationTests` — 4 tests

Tests `PickRandom` weighted selection with `MaxCount` cap logic.

| Area | Tests | What could break |
|---|---|---|
| All entries at max → returns `null` | 1 | Missing null guard upstream |
| `MaxCount = 0` means no limit | 1 | Wrong zero-check excludes unlimited types |
| Single candidate always selected | 1 | Edge case in cumulative sum |
| Capped entry excluded, other selected | 1 | Cap filtering skips wrong entry |

### `ItemAssignerTests` — 5 tests

Tests the item-assignment pipeline: wave (cadence) filtering, max-cap enforcement via grid scan, `IHasWriteableItemSlot` eligibility gating, and the happy path.

| Area | Tests | What could break |
|---|---|---|
| Empty `NewBalloons` → early return | 1 | Null guard missed |
| Wave not on the item cadence (`ItemCadence`) → skipped | 1 | Modulo check wrong |
| All items at max → no assignment | 1 | Cap off-by-one in `CountBalloonsWithItem` |
| No eligible balloons (do not implement `IHasWriteableItemSlot`) | 1 | Missing interface filter |
| Eligible balloon gets item assigned | 1 | Assignment path broken |

### `LightningItemHandlerTests` — 4 tests

Tests the lightning item's target collection and hit publishing — color matching, self-exclusion, and configured damage.

| Area | Tests | What could break |
|---|---|---|
| No same-color balloons → no hits | 1 | Color comparison wrong or missing |
| Same-color balloons → hit published for each | 1 | Grid scan misses occupied slots |
| Self excluded from targets | 1 | Source balloon hit by own lightning |
| Configured damage applied | 1 | Settings damage ignored or hardcoded |

### `ShieldItemHandlerTests` — 3 tests

Tests the shield item's projectile shield increment and message publishing.

| Area | Tests | What could break |
|---|---|---|
| Active projectile → shield incremented | 1 | Wrong field or no increment |
| No active projectile → no crash | 1 | Null guard missing |
| ShieldGainedMessage published with correct slot | 1 | Wrong slot index in message |

### `ProjectileHitResolverTests` — 3 tests

Tests `ProjectileHitResolver.Resolve` — the absorb path that kills the projectile on contact with an absorbing actor.

| Area | Tests | What could break |
|---|---|---|
| Absorbing balloon → publishes `ProjectileDestroyedMessage` | 1 | Projectile death not signalled — thrower never reloads |
| Absorbing balloon → sets `model.IsFree = false` | 1 | Projectile keeps moving after absorption |
| Absorbing balloon → publishes `ActorHitMessage` with `Absorb` outcome | 1 | Wrong outcome — hit routed as Pop or Deflect |

### `GridSpawnerCoordinatorTests` — 4 tests

Tests `GridSpawnerCoordinator` stage ordering, sequencing, and run-reset re-spawn — isolated with an `ImmediateGate` to remove the `Navigation` static dependency.

| Area | Tests | What could break |
|---|---|---|
| Spawners called in ascending `SpawnStage` order | 1 | Sort direction wrong — high-priority runs first |
| Each stage awaits completion before the next starts | 1 | Stage sequence serialization broken |
| Multiple spawners at the same stage all run | 1 | Same-stage spawner skipped |
| `ResetRun` re-runs the spawners | 1 | Restart leaves an empty board — nothing repopulates |

### `PaintItemHandlerTests` — 4 tests

Tests the paint item's neighbor color conversion — paintability filter, same-color skip, empty-color guard.

| Area | Tests | What could break |
|---|---|---|
| Paints different-color neighbors | 1 | Wrong color assignment or neighbor lookup |
| Skips same-color neighbors | 1 | Missing color comparison |
| Skips non-paintable neighbors | 1 | `IHasWriteableColor` interface check missing — tough balloons get painted |
| Empty color → no action | 1 | Null/empty guard missing |
| No neighbors → no crash | 1 | Out-of-bounds on corner slot |

### `BubbleClusterModelTests` — 8 tests

Tests `BubbleClusterModel.ResolveScoreAttribution` loop logic, `breaksStreak` flag, empty palette guard, and inherited `EvaluateHit` outcome.

| Area | Tests | What could break |
|---|---|---|
| Implements `IHasDurability` | 1 | Interface conformance — spawner and durability subscription depend on it |
| Implements `IHasScoreColor` | 1 | Interface conformance — `ScoreController` won't call `ResolveScoreAttribution` |
| Survive → `PassThrough` | 1 | Inherited template method returns wrong outcome |
| Killing blow → `Pop` | 1 | Boundary mishandled |
| Attribution count = `HitsRemaining + 1` | 1 | Off-by-one in loop bound |
| All attributions have `BreaksStreak` | 1 | Missing flag changes streak multiplier behaviour |
| Each attribution scores 1 point | 1 | Wrong point value |
| Empty palette → no attributions | 1 | Missing guard causes `IndexOutOfRange` |

### `ColorStreakTrackerTests` — 7 tests

Tests the streak state machine — consecutive same-color tracking, reset on color change, `breaksStreak` flag.

| Area | Tests | What could break |
|---|---|---|
| First pop → returns 1 | 1 | Off-by-one initialization |
| Consecutive same color → increments | 1 | Missing increment |
| Different color → resets to 1 | 1 | Missing reset branch |
| `breaksStreak` → resets and returns 1 | 1 | `breaksStreak` flag ignored |
| After break, same color starts fresh | 1 | State not fully reset |
| `GetStreak` matching color | 1 | Wrong comparison |
| `GetStreak` non-matching → 0 | 1 | Returns stale streak for wrong color |

### `ColorStreakAnnouncementTests` — 17 tests

Tests when `ColorStreak`/`ColorStreakTracker` publish `StreakChangedMessage`, which ball it names, and `HighestStreak`.

| Area | Tests | What could break |
|---|---|---|
| Record names its ball (shared streak and untracked ball → null/shared) | 3 | A reader attributes one ball's streak change to another |
| `Reset` publishes only on a real change (nothing to reset → silent; state or banked deferred pops → once) | 3 | Phantom "streak broke" announcements, or a real break going unannounced |
| Load announces the shared streak's reset once; level-up announces one reset for all | 4 | Duplicate or missing resets reaching audio and telemetry |
| Retiring a ball with a streak announces its end once; a ball with none stays silent | 2 | Streak-end never reaches listeners, or fires twice |
| `HighestStreak` takes the best across balls and the shared streak, ignoring landed and retired balls | 5 | The melodic streak sticks to a dead ball's value |

### `ThrowerReloadTests` — 11 tests

Tests `ThrowerReload`, the reload rules (headless, no `GameObject`).

| Area | Tests | What could break |
|---|---|---|
| Ready only when slid out, a slot is free and the phase is `Playing` | 5 | A release fires a ball still sliding, over the cap, or during the level-up |
| `Restart`: `ReloadCooldownSeconds` floors a shorter slide; a longer slide wins | 2 | Reload ends before the cooldown |
| `Advance` holds at the cap and resumes where it stopped when a slot frees | 2 | The slide creeps while capped, or restarts from zero |
| `SlidePosition` never reaches the muzzle before the ball is ready | 1 | The ease overshoots, showing the ball at the muzzle when a release would fire nothing |
| `CanLaunch` skips the reload but not the cap or phase | 1 | Editor tools bypass the cap |

### `ThrowerControllerRapidFireTests` — 10 tests

Tests the held-ball and `FireAt` guards on `ThrowerController` (the reload rules themselves are in `ThrowerReloadTests`).

| Area | Tests | What could break |
|---|---|---|
| `IsReadyToFire`: held and reloaded, cap reached, nothing held | 3 | A release fires with nothing held, or past the cap |
| `EnsureLoaded` never replaces a held ball; does not load before the entrance ends or outside `Playing`; loads into an empty, movable thrower | 4 | A fire or death throws away the ball being aimed, or loads during the level-up |
| `FireAt` fires nothing at the cap, outside `Playing`, or with nothing held | 3 | Shot Solver / Fire-Best-Shot tools exceed the cap |

### `ProjectileDoomedTimeScaleControllerTests` — 9 tests

Tests the doomed slow-mo's lone-ball rule.

| Area | Tests | What could break |
|---|---|---|
| Doomed ball alone claims the slow-mo; with company it does not; others dying hands it the claim | 3 | One ball's last breath slows the game under the others |
| A second ball launching during the slow-mo releases the claim | 1 | Slow-mo outlives its lone-ball condition |
| A ball left alone resumes where its last breath now is, not from the start; the earlier doomed ball gets it when the later dies first | 2 | The slow-mo restarts or the wrong ball's clock is used |
| Doom ending or the ball retiring without an end message releases the claim; an untracked ball is ignored | 3 | A stuck slow-mo after game over or reset |

### `ProjectileRegistryExtensionsTests` — 9 tests

Tests the lone-ball predicates (`FliesAlone`, `DoomedShotFliesAlone`).

| Area | Tests | What could break |
|---|---|---|
| `FliesAlone`: only ball, company, company lands, null, a ball merely held | 5 | Held or null balls counted as flying |
| `DoomedShotFliesAlone`: lone doomed, lone not doomed, doomed with company, nothing in flight | 4 | Line hold, balancer freeze or slow-mo fires with several balls up |

### `LevelControllerTests` — 66 tests

Tests the level-complete ceremony. The table lists only the flight-related areas added with rapid fire; the registry (`IProjectileRegistry.InFlightCount`) is the source of "balls still flying".

| Area | Tests | What could break |
|---|---|---|
| Level-up waits for the last ball to land (registry-driven, no Fired subscription) | covered by existing completion tests | The ceremony presents while a ball still flies |
| Run reset: the thrower cleared the flights, so completion presents immediately | 1 | A stale "in flight" state blocks the level-up after a restart |
| Game over: balls scaled away without `Destroyed` don't hold a late completion | 1 | The level-up never presents after a game over |
| Grace: after the beat plus `LevelCompleteFlightGrace`, remaining balls are force-destroyed once; not before, not while the beat plays, not when grace is 0; counts from the beat end on a late tick; balls dying within the grace present without force-destroy; a later level gets a fresh grace | 7 | Balls linger and stall the level-up, or are popped too early or repeatedly |

### `FlightStatsServiceTests` — 11 tests

Tests per-ball counts and the airborne window. The `IsLoaded` and `FlightIndex` tests were removed with those members (the thrower always holds a ball); `IsAirborne` is covered by `IsAirborne_SpansFiredToDestroyed_NotAiming`.

| Area | Tests | What could break |
|---|---|---|
| Pops/deflects per type, no bleed between types or from non-balloon actors | 3 | Router pitch steps drift |
| Per-ball lifetime: counters start at zero on load, unknown ball reads zero, dead ball readable until retired, recycled record starts clean | 4 | A new ball inherits an old ball's counts |
| `IsAirborne` spans Fired to Destroyed (not aiming); retire closes the window; wall bounces are zero before firing | 3 | Effects gated on `IsAirborne` stick on or start early |
| Hit with no shot behind it counts in no flight | 1 | Cheat pops credited to a random ball |

> `HoldSpeedUpControllerTests` was deleted together with `HoldSpeedUpController` (hold-to-speed-up was removed in rapid fire).

### `WeightedPickTests` — 4 tests

Tests `WeightedPickExtensions.PickRandom` weighted selection with max-count capping. Uses deterministic single-candidate scenarios.

| Area | Tests | What could break |
|---|---|---|
| Single entry → always returned | 1 | Edge case in cumulative sum |
| All at max → `null` | 1 | Missing null guard upstream |
| Capped excluded, uncapped selected | 1 | Cap filtering skips wrong entry |
| `MaxCount = 0` → never capped | 1 | Wrong zero-check excludes unlimited entries |

### `ClusterSlotSelectionStrategyTests` — 8 tests

Tests the greedy hex-neighbor cluster expansion algorithm — structural invariants (count, membership, adjacency, cluster cap). Non-deterministic slot positions are not asserted.

| Area | Tests | What could break |
|---|---|---|
| Empty slots → empty result | 1 | Missing guard |
| Count zero → empty result | 1 | Missing guard |
| Single slot → returns it | 1 | Edge case |
| Result ≤ requested count | 1 | Off-by-one overrun |
| Result ≤ available slots | 1 | Exceeds available set |
| All results from available set | 1 | Selects phantom slots |
| `maxPerCluster` enforced | 1 | Cap logic bypassed |
| Slots within cluster are hex-adjacent | 1 | Greedy expansion selects non-adjacent |

### Meadow mushrooms (`MushroomChainRuleTests`, `XorShift32Tests`, `BumperBounceRuleTests`, `MushroomSettingsTests`, `MushroomWallCatalogueTests`) — 166 tests

The pure rules of `Plans/PLAN-MeadowMushrooms.md`: phase 1's chain rule and kick, restated for walls of caps (*Walls with a kick as built*), and the shape of the baked catalogue. None of them needs the game. `MushroomTestBoard` is the mushroom tests' shared helper: the shipped board, cannon and aim grid written out as numbers, the catalogue's layouts as the ball meets them, and the middle of a layout's widest window of arming angles, which is the shot the flight tests aim. The layout planner's and the planned chain's tests went with the planner.

| Area | Tests | What could break |
|---|---|---|
| Chain truth table (the rule is only asked about a ball that is not a lance: a lance bounces off no mushroom): the third mushroom arms, and hits past it count and pay nothing; over every flight of six bounces the rule answers a bounce that pays nothing, the arming or no bounce, and never a banked charge; the same mushroom twice in a row counts once; A, B, A counts three; repeats between hits never reach the target; a fresh flight counts mushroom 0 | 7 | The lance is paid a hit early or late, a charge banked again, or two mushrooms can never arm |
| A wall resets the count and the last mushroom, and the mushroom hit before it counts again after it; chain after chain, each after a wall, arms again each time (a lance spent at a wall is an ordinary ball again) | 4 | A chain carried across a wall, or a ball that can be armed only once |
| The numbers come from the rules: the target, a target of 0 or less pays nothing, the repeat-hit switch | 4 | A constant left in the rule |
| The cap on bounces: every bounce is tallied, repeats included; past the cap the answer is `PassThrough` and nothing changes; the same mushroom over and over ends at the cap; `IsSpent`; a wall clears the tally; a cap of 0 or less reads as the default; the shipped cap leaves room to be armed | 8 | A ball kept for good by the mushrooms, or a cap that costs a reward |
| Only the bounce and the wall write a flight's state (read off the rule's methods) | 1 | Someone adds a call that resets the count on a pop, a ricochet or a cut cap |
| `XorShift32`: three seeds' sequences pinned number by number (0 and a negative seed among them), `Next` scaled and in range, a range of 1 or less, same seed same sequence | 9 | A device or runtime that lays out another board for a seed |
| Mirror reflection: head-on, glancing, unit length, a ball already leaving | 4 | A ball turned back into the mushroom |
| The landing point: between two mirrored points it is the top of the circle; over 20,000 random pairs, and 20,000 more with one point a hair off the circle, every answer is a true reflection within a tenth of a degree; over 5,000 pairs it is found whenever both points see some of the circle; none when the other point is straight behind or the start is inside | 6 | The kick landing the ball where its next bounce misses the follower, or giving up on a chain that exists |
| The kick's tests kick with the shipped cone and offsets (20 degrees, 0.15 and 0.9), held to the settings' defaults; the offsets are the turn range (163 and 52 degrees), and offsets authored out of range are never the centre or a graze | 3 | A turn range that is not the offsets' |
| The kick bends toward a cap of another wall inside the cone, the smallest angle wins whatever the list order, and it leaves along the reflection outside the cone, with no cap of another wall, with another cap standing across the way to the target, when the kicked line would cross the cap it is leaving, or when the line leaves within a hair of the tangent; a nearest whose way is blocked gives way to the next | 8 | The ball steered into a cap in the way, or re-entering the one it left on a rounding error |
| Looking ahead: the ball lands where the reflection points at the follower; at that landing the next kick carries on to the follower even inside a cone of 1 degree; with two mushrooms the follower is the one just hit and the return stays within twice the arcsine of the smallest offset of the way back (17.3 degrees, which the shipped cone of 20 covers); a follower needing nearly a graze, or lying straight behind, gets the largest offset | 5 | A chain that does not carry itself, or a cone that cannot send the ball back |
| The follower is the nearest cap to the target that is not of the target's wall, whatever the ids; one out of the offsets, or with its way blocked by a cap of the target's own wall, gives way to the next that fits; 3,000 shuffled lists give the same answer as the sorted ones; a contact on a mushroom not in the list is not kicked | 5 | A follower chosen by the ids, or the answer depending on where a cap stands in the list |
| The last kick lands at the smallest offset whatever follows; no kicked ball lands dead centre or as a graze across the whole cone (counts 1 to 3, two and three mushrooms), nor when the reflection points straight at the centre | 5 | Two mushrooms trading the ball forever |
| The kick stops once the count passes the chain target (counts 1 to 5), reads the target from the settings, can be switched off, and is deterministic | 8 | A flight steered on after it has been paid |
| Two mushrooms never keep a ball: with no cap, 4000 random contacts for each of seven tunings (the shipped one among them) leave within 12 bounces and a dead-centre start leaves; under the mirror reflection alone it never does, and the shipped cap ends that at 12 | 10 | An endless flight, since a mushroom bounce spends no shield |
| Caps of one wall count once and a cap of another wall counts (a rattle along one wall, a crossing, and back: armed on the fifth bounce with a count of three, every bounce tallied against the cap); a cap built by itself is its own wall; the cap left of a wall whose other cap a lance cut still counts as that wall | 3 | A rattle along one wall paying the lance, the count following the cap and not the wall, or a wall losing its number with a cap |
| Walls: the kick never bends toward a cap of the wall just hit; the follower is never a cap of the target's own wall; between two facing walls of three caps 1.95 apart, at the shipped cone and offsets, from each cap and five leaving angles, every bounce up to the one that arms is on the other wall; caps of one wall whose circles overlap are no fault, and caps of two walls that overlap are one, said where the checks are compiled in | 19 | A wall handing the ball along itself, a rally that dies between two walls, or every wall of neighbouring caps reported as a fault |
| `MushroomSettings.Validate`: nothing to say of the shipped values; one message each for a cap under what a chain takes, crossed offsets, a cone too narrow to send the ball back, a spacing under two caps, fewest walls over most, fewest caps over most, most caps under the most walls, caps that leave the bushes fewer than 2 slots, a band with its ends crossed, a ceiling that lets no aim angle meet a cap within the cannon's near range, and a count of walls the baked catalogue has no layout for | 12 | Settings that cannot work going unremarked |
| The settings' numbers: the shipped values are the owner's (cone 20, 2 to 4 walls, 4 to 7 caps, spacing 1.9, the bushes keep two slots); the cone is wider than two walls need to send the ball back; `MaxCapsFor` is every wall at its longest and never over the most caps; `MinSeparation` and the spacing never under it; a spacing authored at nothing reads as the shipped one; `Allows` only the slots off the columns and rows kept clear; `WithBottomRows` changes the bottom rows and nothing else | 7 | The spacing lost to an empty field, a cap on a kept-clear row, or the game's limits losing a field on the way to the picker |
| `RegrowBeats` is what was authored (5 as shipped, the owner's five beats) and never read as under 1; the shipped rules arm on the third hit and pay nothing on the fourth | 5 | A cut cap that never grows back, or a charge banked again |
| `Blast.SwayShare` is what was authored (2.4 as shipped: wide enough for the sway to be seen outside the bomb's cloud) and never read as under 0, which turns the sway off | 4 | A negative share read as a reach, or the sway silently off as shipped |
| The band accepts a layout by what it measures: at its two ends of the arming share (36 and 81 of 511 angles), a run one angle short of the window, and a floor of arming angles whatever share that is | 7 | A layout kept for its shape, or a band that accepts anything |
| The cannon's room allows a layout by how it stands to the cannon: the shipped numbers are 3.25, 4 and a fifth; a nearest cap at 3.25 with 102 of 511 angles met is allowed, one a hundredth nearer or one angle broader is not, and a cap in the lowest row (2.66) is not however few angles it takes | 5 | A clearance that lets a wall stand over the cannon, or that refuses every board |
| The catalogue has layouts for every count of walls the settings ask for and no other, 16 or more of each (18 of two walls and 45 of four, which is every board of those counts that qualifies, and 64 of three), no board twice | 4 | A level asking for walls the catalogue cannot deal, or a run seeing the same board over and over |
| Every layout: the walls of its count; caps within the budget, with two slots left to the bushes and no slot twice; every wall one to three caps in a line of neighbouring slots; a wall of one cap only where the board cannot be filled without it (none on two and three walls, one on four); its walls the spacing apart (the nearest any stand is the lattice's next step, 1.98) and off the columns and rows kept clear | 12 | A hand edit or a bad bake: overlapping walls, a cap on an edge, a board over the cap budget |
| A cap packs to one number and comes back the same | 1 | The generated file read as other slots than were baked |

### Meadow mushrooms, generation (in `LevelGeneratorTests`, `LevelGenerationSettingsTests`, `UnbreakableFormationServiceTests`) — 59 cases

Phase 2 of `Plans/PLAN-MeadowMushrooms.md`: what a generated level says about the meadow, and the formation service obeying it. Written with the phase and not yet run in the Editor.

| Area | Cases | What could break |
|---|---|---|
| With every balloon a staple, over 300 levels and three run seeds: Tough and Tougher on every sky level, on no meadow level and on no water level; the unbreakable everywhere | 3 | Toughs back on the meadow, or lost from the sky |
| On the meadow a tough that costs the whole budget leaves it to the unbreakable, and the mushrooms cost nothing; in the sky the same tough still wins the pick about half the time | 5 | A pick that comes out empty, or mushrooms eating the threat budget |
| `MushroomWallsFor` (the gate counts walls of caps): 2 on a stretch of one; 2 to 4 over stretches of 2 to 6 levels, a half rounding up; in range, never shrinking and ending on the most for every length to 40; none off the meadow; the range read from the settings, an inverted or empty one included | 12 | A wall short on a rounding, a divide by zero on a one-level stretch, a constant in place of the setting |
| Generated levels: the pinned meadow 16 to 19 has 2, 3, 3, 4 walls and the sky either side none, resolved counts included; the shipped schedule over 200 levels and three run seeds has mushrooms only on the meadow, 2 on a stretch's first level and 4 on its last; a schedule of one-level meadows has 2 each; an extra-colour level on the meadow has none | 8 | Mushrooms off the meadow, a count that follows the roll instead of the stretch |
| Bushes of 8 to 12 beside 2, 3, 3 and 4 walls under a limit of 9 come out 3, 2, 2 and 2: the limit less the most caps those walls may hold (6, 7, 7, 7), never under two; under limits of 6, 7, 9 and 12 the bush gate takes what the caps leave or is dropped, and the mushrooms keep their walls | 7 | Bushes and caps over the shared limit, a negative bush count, mushrooms giving way |
| A level allows the snake alone on the meadow and every shape elsewhere, resolved or not; a debug preset and the fallback level allow every shape | 4 | The sky losing its lines and covers, or a preset forming nothing |
| A Mushroom entry in the catalogue is never picked, spends no budget, and the validator reports it | 4 | The meadow carrying its mushrooms twice |
| The formation service on a snake-only level forms a snake whatever the roll; allowed every shape it picks the horizontal, each diagonal and the snake by the roll; allowed one line shape it forms that one though the roll lands on the snake | 11 | The level's set ignored, or read against the wrong shape's bit |
| No shape allowed: nothing forms on the cycle or the beat and nothing moves. Past the snake cap on a snake-only level the unbreakables left over stay loose | 2 | A crash with an empty pick, or a cap lifted for the meadow |
| A snake done holding on a snake-only level stays a snake and takes in a loose unbreakable; a line left from a level that allowed it turns into a snake at the end of its hold, or is set loose when no shape is allowed | 3 | A line planned again as the last resort on a level that does not allow it |

`Generate_EveryCataloguePiece_IsInOnItsUnlockLevel...` now holds a piece to its unlock level only where its scenario allows it, and names the one the fixture holds back: Tougher, which unlocks on the first meadow level. The cloud and bush tests that ran on meadow levels pick their own gate out of the level's, since a meadow level now carries a mushroom gate too.

### Meadow mushrooms, the live bounce (`ProjectileMotionResolverBumperTests`, `BumperSweepTests`, `MushroomBoardTests`, `MushroomBumperModelTests`, `WallSlotSelectionStrategyTests`, and cases in `PierceWakeTests`, `SnipeLanceTraceTests`, `LevelPacingValidatorTests`) — 104 cases

Phase 3 of `Plans/PLAN-MeadowMushrooms.md`: the mushroom on the grid, its placement on the real board, and the ball's bounce in `ProjectileMotionResolver.Step`. No view is involved: models go on the grid and the resolver is stepped by hand. Unless a case says otherwise the bounce is the mirror reflection, so every expected point is worked out by hand in the test. Not yet run in the Editor. All but the three validator cases were run outside it, against a copy of the engine assembly with `AnimationCurve.Linear`, the log calls and `Random.Range` stubbed out; nothing in `ProjectileView` is covered.

| Area | Cases | What could break |
|---|---|---|
| A bounce turns the ball as the rule says and flies the rest of the step along the new direction: head on, off centre (the worked reflection), and with a rule that records what it was asked (the chain already counted, every bumper handed over, the exact entry point) | 3 | A bounce from where the step ended instead of where the ball touched, or the rule asked before the count |
| The entry point is exact for a shallow graze and for a ball as fast as a lance whose step starts before the mushroom and ends past it | 2 | A fast ball through a mushroom between two fixed steps |
| No shield is spent and no wall hit counted, with shields left or none | 2 | A mushroom ending a flight, or earning a tap |
| The count rises; the wall that counts its bounce clears it on that step; so does the wall of a pierce discharge | 2 | A chain carried across a wall |
| Two bumpers in one step, each reported in order with where its leg began; at most eight to a stretch, the rest still flown; a step's bounces are gone by the next | 3 | A step that stops at its first bounce, loops, or leaves stale bounces to be announced twice |
| The bumper being left is not read as entered again, on that step or the next, though the ball starts inside its circle; after a turn it bounces the ball again, and the same mushroom twice counts once | 2 | A ball trapped on the mushroom it just left |
| A ball found inside a circle bounces where its line came in, carrying what it flew since | 1 | A ball set down inside a mushroom passing through it |
| A spent stretch flies through unturned and counts nothing; the bounce that spends it is the last until a wall, and a wall makes the mushrooms bounce it again | 2 | A ball kept by the mushrooms, or let through for good |
| A mushroom between the ball and a wall is met first; a bounce and a wall in one step report both, the wall keeping its contact and its shield; a mushroom on the way back off a wall is met in the same step, on a fresh chain | 3 | A shield lost to a wall the mushroom should have kept the ball from, or a mushroom jumped right after a wall |
| An unarmed cruise ends at a mushroom with its taps kept; a bounce starts a new segment; a bumper out of reach changes nothing | 3 | A cruise through a corridor that is not empty |
| Wired to an empty board, 600 steps of one shot are identical to the bit to a resolver built without a bumper field. Both sides are today's code: what holds today's flight to the flight before the mushrooms is the golden test below | 1 | The field's presence alone changing a flight |
| A layout of the catalogue, the kick and the game's stepping at the ball's real speed: the shot into the middle of the layout's widest window, which the simulator says arms the lance, is armed by the game on the hit that brings its count to three, off more than one wall (2, 3 and 4 walls, 8 layouts each) | 24 | The baker's simulator and the game's flight disagreeing on what a layout is worth |
| Whole flights of random shots over layouts of the catalogue, by the kick and by the mirror reflection, from the ball's speed to the speed rail (1.86 a step): no ball comes to rest inside a mushroom it is not leaving, no step flies through one and reports nothing, none ends outside the walls or over the cap, and every flight ends at a wall | 8 | Tunnelling at speed, a ball outside the walls, an endless flight |
| `BumperSweep`: the first entry on a run and its distance; not past the run's end; the earliest of two whatever the list order; the bumper being left skipped; a miss and a bumper behind; from inside, the entry behind the ball, which wins over one ahead; leaving at the edge is not inside; a circle of no size | 9 | The flight and the lookaheads disagreeing on what is in the way |
| `MushroomBoard`: empty without mushrooms; ids in order wherever the caps stand, each its own wall when placed by itself, and each with the wall it was placed with; the slot's centre and the two radii; rebuilt when a mushroom leaves or arrives; the same circles after a balloon comes and goes; the ball's radius taken when it becomes known | 7 | Bumpers left behind by a cleared level, ids out of order, a cap without its wall, a circle the size of the mushroom alone |
| `MushroomBumperModel`: static, and none of the things that move or hit; the balance race leaves it and moves a balloon round it; a pressure shove stops at it; its slot is not traversable | 5 | A mushroom moved off the slot it was dealt |
| `WallSlotSelectionStrategy` on the shipped board: for 2, 3 and 4 walls over 24 seeds the slots are a layout of the catalogue, cap for cap, with each cap's wall, and the seed is what picks it; the same seed gives the same layout; only slots it was handed; a slot of the seed's layout taken picks the next layout that fits; with five rows kept no layout of four walls fits and every seed gets three; never fewer clear rows than the settings keep; none when asked for none or for fewer walls than the fewest; the most when asked for more; none and a warning when no layout fits at all | 11 | A layout that is not one of the baked ones, a cap on a taken slot, or no mushrooms where fewer walls would stand |
| `IsBesideACap`: true for the slots next to a cap and false further off, on every slot of the board; false everywhere with no layout placed | 1 | Bushes against a wall, or kept off the whole board |
| `StaticActorSpawner`: the walls, then the clouds, then the bushes whatever the configuration's order, four clouds and twelve bushes and none beside a cap; a Mushroom gate counts walls and every cap carries its id, its wall and its slot onto the board, walls the spacing apart (2, 3 and 4 walls); none on a level without the gate; a Mushroom entry skipped with no strategy | 6 | A gate read as caps, a cap without its wall, or bushes placed first |
| A retry stands the same walls wherever the clouds land: the same level seed dealt twice, with the engine's random numbers in two states, gives the same caps in the same slots, cap for cap, and other clouds (2, 3 and 4 walls; Editor only, it seeds the engine's generator) | 3 | A retried level with its mushrooms somewhere else, because something placed with unseeded random numbers went before them |
| `PierceWake`: a step that bounced off a mushroom reports the way on from it and no wall blast; the stretch into a mushroom is reported like any other, and an ordinary ball's is not | 2 | A wake cut straight through the turn, or a crater at a mushroom |
| `SnipeLanceTrace`: a run opened on the mushroom that armed the lance is drawn straight from there to its wall (nothing turns a lance's run but a wall) | 1 | A pencil line that starts where the step began and not on the mushroom |
| `LevelPacingValidator`: a preset listing mushrooms is reported though the catalogue has no entry, and though an entry allows them on the sky; a count of 0 is fine | 3 | Mushrooms floating over the sky backdrop on a debug preset |

### Meadow mushrooms, the live bounce, after its review (`ProjectileMotionResolverGoldenTests`, `BumperLookaheadTests`, `MushroomPlacementSourceTests`, `ThrowerOriginProviderTests`, and cases in `ProjectileMotionResolverBumperTests`, `PierceWakeTests`, `WallSlotSelectionStrategyTests`, `LevelDifficultyResolverTests`) — 32 cases

What phase 3's review asked for. Not yet run in the Editor. All but `ThrowerOriginProviderTests` (it needs a `GameObject`) were run outside it, against the stubbed copy of the engine assembly.

**The golden numbers.** `ProjectileMotionResolverGoldenTests` holds 345 rows captured from the resolver as it stood in the commit before the mushrooms (`ProjectileMotionResolver.cs` and `ProjectileStep.cs` taken from git, compiled under another namespace beside today's, and flown through the same scripts). At capture, that resolver, today's with no bumper field and today's wired to an empty board agreed to the bit on every float and counter. The capture ran on .NET with `AnimationCurve.Evaluate` stubbed to the identity, so the test compares floats within 2e-5 and everything else exactly. To extend it, add a script and capture its rows the same way: the numbers must come from the old code, never from today's.

| Area | Cases | What could break |
|---|---|---|
| Five scripted flights step by step against the old resolver's numbers (outcome, position, direction, wall contact, speed, shields, empty wall bounces, taps, wall hits, cruising, piercing): walls alone, a corner, a doomed last-shield glide, a cruise entry, a pierce discharge at a wall. Each flown by a resolver with no bumper field and by one wired to an empty board; and one case that each flight really contains what it is named for | 11 | The mushrooms changing the flight of a level that has none |
| `BumperLookahead`: a mushroom on the run is found at its near edge, whatever the direction's length; none with no mushrooms or a run that stops short; a run starting inside one finds it at no distance | 4 | A glide or a cruise through a mushroom |
| A spent stretch lets the ball through on its own leg and on no leg past a wall; the mushroom being left is out of the way on the ball's own leg only, and back in it after a turn; a traced path (as the cruise check traces it) with a spent chain is blocked by the mushroom it meets after its first wall, and with a fresh chain on any leg | 4 | A cruise entered down a corridor the ball will be bounced out of |
| A corner and then a mushroom on the way back: the bounce is found on the leg off the second wall, flown along that leg, and the ball stops on the wall it is sent back to; the same corner with no mushroom near is the walls' own step to the bit; `WallDirection` equals `Direction` on every step no mushroom turned after a wall, and is the wall's own on one it did | 3 (and one assert) | A mushroom jumped after a corner, or the shield squashed along the mushroom's turn |
| `PierceWake`: a lance past a wall, through whatever caps it cut, leaves a wake of two straight stretches, to the wall and on from it; the step whose bounce armed the ball reports the way on from the mushroom | 1 | A wake bent at a cap the lance cut through |
| `StaticActorSpawner`: mushrooms listed last, after clouds asked to take every slot, still stand their three walls, and the clouds take every slot that is not next to a cap: the mushrooms went first by type and not by their place in the list | 1 | Clouds placed before the walls, taking their slots |
| `MushroomPlacementSource`: the same seed for the same level of the same run; another for another level or run; the seed is asked for the level the active parameters were resolved for; the reserved rows are the balloon spawner's | 4 | A layout seeded for one level and counted for another |
| `LevelDifficultyResolver.Level` is the level `Current` was resolved for: at start, from the level-up message on, and on a retry | 1 | The seed's level and the count's drifting apart |
| `ThrowerOriginProvider`: no origin before the thrower starts; the resting pivot is what it was given while the live pivot follows the cannon through its entrance and a recoil; `Clear` forgets it. Since the walls are dealt from a baked catalogue these tests are the resting pivot's only readers | 3 | A chain planned from a cannon mid entrance |

### Meadow mushrooms, the reward (`MushroomChainControllerTests`, `MushroomSoundRouterTests`, and cases in `SnipeItemHandlerTests`, `GameplayMetricsServiceTests`, `MetricCatalogFoldTests`) — 35 cases

Phase 4 of `Plans/PLAN-MeadowMushrooms.md`: the chain paying the Snipe lance, the sounds, and the counters. Not yet run in the Editor; all of them were run outside it, against the stubbed copy of the engine assembly described above.

| Area | Cases | What could break |
|---|---|---|
| `MushroomChainController` with a recording armer: `Arm` calls `ArmAt` at the bounce point and is announced with the point and the count; every other answer the rule gives (nothing, a pass through) grants nothing and announces nothing; a ball that landed, retired, is still in the thrower or cannot be written to is not paid; after the board is cleared an old ball's reward is dropped and the next run's is paid; two balls in flight are each paid for their own chain | 8 | A lance handed to a dead ball or the wrong one, or state left over from the last run |
| The controller with the real `SnipeItemHandler`: the third bounce arms and starts the pencil line at the bounce, and the arming itself banks no charge | 1 | A charge banked by the chain's own hit |
| Flown by the game's own steps into the controller and the real Snipe handler: a ball traded between two facing mushrooms is armed by its third bounce, and from there bounces off neither, cuts the lower cap on its way down and the cap that armed it once it comes back, is armed once and banks a charge for each of the two caps it cuts (decision K); and the step that arms ends on the mushroom, short of a wall just beyond it, with a shield left or none, so the ball is alive and unspent when it is armed and the wall is the lance's a step later | 3 | A dead ball armed and announced as a lance, or a wall settled as an ordinary ball's in the step that armed it |
| `ISnipeArmer.ArmAt`: it arms a plain lance, with the speed buff, its line starting where it was earned; a pickup after a chain's lance is banked on it | 2 | A second speed buff on a running lance, or a grant swallowed by a ball already piercing |
| `MushroomSoundRouter`: the first mushroom plays the mushrooms' own bump (`MushroomBounce`) at the root, at the point, under the pops; each one counted a whole tone higher; a bounce that did not count repeats its note and a wall starts the climb over; the climb stops at its top note; a lance plays the Snipe's cue at its own pitch; a cap a lance cut plays the stroke (`Strikethrough`) where it was cut, once a cap, and stops no bump; a cap a blast blew off plays nothing, however many go at once, while a lance's cut beside it still plays; a run of bumps is each played and the router stops none, the bump's own entry keeping its voices; the caps a flight cuts climb the stroke a whole tone each, the Snipe's cue rings once for the flight's first charge, a whole tone up, and not for the charges after it, and the stroke's climb stops at its top note | 10 | A chain that cannot be heard climbing, a pitch run off the scale, a bomb that plays a stroke for every cap it takes, or a router still thinning a sound that is its own |
| Telemetry: every bounce counts, the uncounted included; a chain completed and the caps its lance cut; caps cut by a lance no chain armed are counted with no bounce and no chain, and a cap cut again counts again; chain after chain in one flight are each completed once and none broken; a chain cut short by a forced destruction (the level-up cap, an emptied board) is not a broken one; a wall with the count at 1 or 2 breaks a chain and at 2 it was one short, a wall after a paid chain or with nothing counted breaks nothing; a ball dying with a chain short breaks it, and one ball's wall does not end another's chain; a flight open at the game over breaks none and a ball fired again starts at none; nothing counts after the game over; caps a blast blew off are counted apart from the caps a lance cut, and break no chain; every cut that banked a charge is counted as a charge, and a cut no flight made and a blast are not | 8 | The near-miss counter the cone is tuned against reading walls it should not |
| The catalogue: the seven mushroom rows' wire names, units, folds and scopes (`mushroom_caps_cut` at 39, where the banked charges were, `mushroom_caps_blown` appended at 42, `mushroom_charges_banked` back at 43); their ids are 37 to 43 and nothing before them moved | 8 | A renumbered id, or a wire name changed after it shipped |

`GameplayMetricsHarness` gained the two mushroom captures and the mushroom settings the service now takes. The existing Snipe pickup and Sharpshooter tests are unchanged.

### Meadow mushrooms, the step that pays (in `ProjectileMotionResolverBumperTests`) — 5 cases

The phase 4 review's second finding: the bounce that arms ends its step. Run outside the Editor.

| Area | Cases | What could break |
|---|---|---|
| The bounce that arms ends the step on the mushroom, the ball turned and its next leg starting there; the next step flies the ball as the lance it was armed as, down through the facing cap, off the floor and back up through the cap that armed it, cutting both and bouncing off neither; with a wall just beyond the mushroom the wall is left to the next step, with a shield or with none, and the cap that armed the ball is not cut as it leaves; an arming bounce on the stretch flown back off a wall ends the step there, the wall already settled | 5 | A wall, or the ball's death, sharing a step with the arming |

### Meadow mushrooms, the mirror and the aim line (`MushroomMirrorTests`, `MushroomAimGridTests`, `MushroomAimLineTests`, `ShotMushroomFreeBoardTests`; helper `MushroomMirrorHarness`) — 82 cases, and 3 in the controller's fixture

Phase 5 of `Plans/PLAN-MeadowMushrooms.md`. `MushroomMirrorHarness` holds both sides: `MushroomLiveGame` flies a
ball in fixed steps through the real `ProjectileMotionResolver`, hands its bounces to the real
`MushroomChainController`, which pays through the real `SnipeItemHandler` and buff service, and stands in for the
view's physics trigger (a balloon is met when a step's last straight leg enters its contact circle); `MushroomSimGame`
flies the same shot through `ShotSimulator.Simulate` and reads its path back as turns. The two reach every point by
different arithmetic and a curved surface multiplies a rounding error about ten times a bounce, so points are held
to a bound that grows with the flight (`MirrorTolerance`) and a flight is held until the two sides are half a
millimetre apart; kinds, ids, counts and rewards are held exactly. With the board's caps given, a flight is also held only up to a real graze: a ball that passes a cap's edge, or sits in the notch between the two caps the two sides met, by less than fifty times how far apart the two sides already are meets it on one side and not on the other, which is rounding and no rule. That gap is capped at a tenth of a millimetre before it is multiplied, so the reach is never over half a centimetre, and only the cap the two sides differ on is looked at (`MushroomMirror.IsAGraze`; the aim line's test uses the same bound for a cap the flight meets at its very edge). Run outside the Editor.

| Area | Cases | What could break |
|---|---|---|
| Hand-built: two facing mushrooms (the third bounce arms, and the lance cuts the lower cap and then the one that armed it, alike on both sides); a ball loaded as a lance cuts both, bounces off neither and counts no chain; a cap cut by an earlier flight is absent for the next ball, the simulator handed the caps that are whole as a gather would hand them, and with every cap whole again the same ball bounces off it; a wall between bounces resets the count and an ordinary ball cuts nothing; a spent cap of two flies through the third mushroom to the wall behind it, and the wall gives the mushrooms back; a triangle between two mushrooms and the floor arms a chain of two, after which the turns are walls alone and the cap the lance comes to is cut (ordinary and loaded as a lance) | 7 | The simulator counting, capping, paying or cutting differently from the game |
| A layout of the catalogue a lance has cut, then played on: a ball loaded as a lance is flown at the layout's widest window until it cuts a cap, mirrored turn for turn and cut for cut; then ordinary balls from every seventh aim angle are flown over the board as it left it, the simulator handed the caps left: the same flight on both sides, and no bounce off a cut cap (2, 3 and 4 walls over four layouts) | 12 | A cut cap that is gone for the game and still there for the solver, or the kick bent toward one |
| A ricochet off an unbreakable between two bounces off one mushroom keeps the chain and does not count twice | 1 | A deflect resetting the simulated chain |
| Two facing caps told to be one wall trade the ball and pay nothing: each bounce names the cap that was hit, the count stays at one, and the cap on bounces lets the ball through to a wall, on both sides | 1 | The game or the simulator counting by the cap where the other counts by the wall |
| The same contact, handed to both sides: a ball flown straight down from between two facing walls of two caps onto the lower one, at four points of its caps and a little to either side of the notch between them. The game and the simulator count the hit alike, send the ball on to the same cap and land it at the same point (three turns held to a tenth of a millimetre); between the walls that cap is of the other wall and counts, at the notch it is the wall's other cap and counts nothing | 8 | The game and the simulator picking different kick targets from one contact, or handling the notch between two caps of a wall differently |
| A layout of the catalogue flown from the cannon's real pivot with the shield a ball starts with, the shot into the middle of its widest window and three grid angles either side, for 2, 3 and 4 walls over six layouts: the same flight on both sides up to rounding or a real graze, and most of the seven held past the bounce that arms, armed on the third hit by both | 18 | The kick resolved from a different contact, or the lance armed a bounce apart |
| Seeded boards of the shipped grid (a layout of the catalogue, one-hit balloons, one unbreakable), every sixth angle of the aim grid, ordinary and lance: 2044 flights each, every one the harness can place mirrors turn for turn (1943 and 1930; 2421 mushroom bounces by the ordinary balls and none by the lances; 294 armed; 325 with a wall of the board between two mushrooms). The caps cut are held too, in every flight held to its last wall: 398 in 250 of the ordinary flights (the lance a chain made) and 2106 in 971 of the lance's, the same caps on both sides. It must place 85% of its flights and see over 50 flights cut caps; the ordinary run must see over 100 flights armed, over 100 held past the arming bounce and over 300 turns held as the lance, and the lance's run no bounce off a mushroom; the flights left out are logged by cause | 2 | Any rule the two sides call in a different order or with different state |
| The aim grid: every layout of the catalogue (18 of 2 walls, 64 of 3, 45 of 4) flown again from all 511 angles is inside the settings' band, within 4 arming angles and 1 angle of window of what the baker wrote down; logged: 9.7%, 11.6% and 13.2% of angles arm on average, in windows of 3.0, 3.3 and 3.1 degrees, 44 angles at the fewest | 3 | A catalogue baked for another board, cannon or kick than the game's |
| The bake's room is wider than that drift (8 arming angles against 4, 2 angles of window against 1), and every layout as baked is still inside the band with all the drift it is allowed, either way | 3 | A layout so near the band's edge that another runtime's rounding puts it outside: the device's is one no test flies on |
| The cannon keeps its room on every layout: no cap nearer its pivot than the clear distance, and no more of the aim angles than the ceiling meet a cap within the near range, measured from the pivot and muzzle written in the tests (3 cases; logged: the nearest cap of any layout is 3.31 away and the broadest takes 19.8% of the angles) and from the cannon as it ships (1, Editor only: it loads the assets); the wall of the owner's screenshot, three caps along the lowest row straight above the cannon, is 2.7 away, takes over a quarter of the angles and is refused, and the same wall five rows up is not | 5 | A wall blocking the cannon, or a clearance measured from a cannon the game does not have |
| With the kick switched off the same layouts keep under half their arming angles and under a tenth of them stay inside the band | 3 | A band any board passes, so that passing it proves nothing |
| A flight that arms goes on to cut caps and bounces off none as a lance: the arming bounce is the flight's last bounce, over 90 in 100 arming flights cut a cap (measured: all of 891, 3787 and 3038, 1.21, 1.33 and 1.33 caps a flight on average), no flight leaves a board bare, and an ordinary ball cuts nothing | 3 | A rally that dies after its third hit |
| The aim line against the simulated flight on layouts of the catalogue with an unbreakable, ordinary and lance: every corner is where the flight turns, on the skin of what it turned off, and the pierce starts at the corner where the chain arms (1619 lines each, 10,130 and 8,627 corners, 568 lines armed on the way; no corner of a lance's line is on a mushroom; a line is held up to a cap the flight meets at its very edge, by the mirror's bound) | 2 | The line and the ball bending differently |
| A lance's line runs straight through the mushroom and the unbreakable to the wall behind them; the line of a ball its chain arms is a lance's from that corner and runs through the mushrooms after it; a cut cap bends no line (the line over it is the line of a board it never stood on, to the bit); a mushroom spends the reflection budget and a line ending on one says `Bumper`; a line seeded with the ball's count arms on its first mushroom; with no mushrooms the line is the one it was, to the bit, ordinary and lance | 7 | A lance's line bent at a cap it will cut, or a changed line on every other level |
| The Snipe's corridor runs straight from the host to its first wall on layouts of the catalogue, as the simulated lance does, through every cap in its way | 1 | A corridor bent at a mushroom the lance cuts through |
| Boards without mushrooms: 4088 flights folded into one number, held to what the simulator of main `b85c863c2` (before the mushrooms) gave for the same flights, one number for Mono and one for .NET since the two round differently; four mushrooms outside the walls change no flight, to the bit; a sweep over a layout of four walls allocates nothing | 3 | The mushrooms costing or changing a level that has none |
| A simulated cut takes its cap out of the layer and a cut the layer refuses changes nothing and says so; lances with shields to spare, flown wall to wall over the same stems, cut no cap twice and no more caps than the board has, and end at a wall, not at the cap on events | 1 | A simulated flight handed the same cap to cut without end |
| The numbers the catalogue was baked with are the shipped ones: the thrower's place in the Game scene, its entrance and muzzle, the ball's collider, the walls, the aim arc and step, the starting shields, the slots, and every mushroom setting on the Level Pacing asset (Editor only: it loads the assets); and they are the numbers written in the tests, with the settings' own defaults | 2 | A catalogue that holds for a cannon, a board or a kick the game no longer has |
| An `Arm` arms with the Snipe's speed buff and banks nothing, in the game (controller and real Snipe handler) and in the simulator (`PayMushroomReward`) alike; every other answer the rule gives pays nothing in both: no lance, no charge, no speed buff (in `MushroomChainControllerTests`) | 3 | The simulator paying for an answer the game pays nothing for |

### Meadow mushrooms, the lance cuts caps: the rule (`MushroomRegrowControllerTests`, and cases in `MushroomBoardTests`, `ProjectileMotionResolverBumperTests`, `BumperLookaheadTests`) — 45 cases

Decision J of `Plans/PLAN-MeadowMushrooms.md` (*The lance cuts caps: the rule as built*): a lance flies through the mushrooms and cuts their caps, a cut cap is no bumper, and it grows back on the board's beat. The mirror's side of it (the simulator cutting the same caps, a cap cut earlier gone for a later ball) is in the section above; the view's is below. `MushroomBoardTests` and `MushroomRegrowControllerTests` use a plain grid config and a plain shot origin, not substitutes, because they measure allocation and a substitute allocates on every read. All run outside the Editor.

| Area | Cases | What could break |
|---|---|---|
| A lance cuts the cap and flies on unbent: the step only moved, the whole length flown, the direction kept, no shield spent, its pierce, cruise, run of empty bounces and taps kept, and the chain it carried untouched (a cut is no hit and breaks none); the cut names the cap, its slot, a point and the lance's direction, and the cap has left the field | 1 | A lance still bouncing off a mushroom, or a cut that counts toward a chain |
| What a cut's message carries: through the cap off its centre, the point of the path nearest the centre; clipped by the ball's edge alone, a point drawn in to the cap so a sliver comes off; always inside the cap, with the lance's direction | 4 | A cap split along a line that misses it |
| A lance whose fixed step starts before a cap and ends past it still cuts it (the whole stretch is swept); a lance through two caps cuts both, nearest first whatever the list's order, and each once however often it flies back over the stems; one step through two caps of one wall and a cap of another cuts all three, the wall's two first, each named by its own slot though the list shrank under the sweep | 3 | A fast lance through a cap uncut, a cap cut and announced twice, or a cut named by another cap's slot |
| A field that takes no cap out when told to cut one ends the sweep: it is asked once, nothing is cut or announced, and the lance flies its step unbent | 1 | A fixed step that never returns, adding cuts without end |
| A cap cut while another ball is leaving it: that ball still names it, nothing is found in its place, the caps that are whole still bounce and count for it, and it flies through over the stem | 1 | A ball that cannot meet any cap, or meets a stem, because the cap it left is gone |
| A cap regrown round a ball: an ordinary ball inside the circle bounces at the point its line came in, carrying what it flew since, and a lance there cuts the cap again | 2 | A ball left trapped inside a cap that grew back |
| A cut cap is absent for what comes next: an ordinary ball flies through where it stood and counts nothing, while a whole cap still bounces it; the bounce rule is handed the caps that are left; and the kick bends a ball exactly as on a board the cut cap never stood on, for a cap that would be its target and for one that would be the follower it lands the ball for | 4 | A ball bouncing off a stem, or kicked toward a cap that is not there |
| A lance spent at a wall bounces off the mushroom on the way back off it, in the same step, and begins a new chain there; one still armed past the wall cuts that cap and ends where the walls alone would have left it | 2 | A spent lance cutting on, or an armed one bounced |
| `BumperLookahead`: a lance has no mushroom in its way on any leg, from outside one or from inside; a traced path of a lance is clear through a mushroom an ordinary ball's is blocked by | 2 | A lance's last-shield glide, cruise or telegraph stopped at a cap it will cut |
| `MushroomBoard`, cutting: `Cut` takes the cap out of the list a caller already holds, the caps left keep their ids, walls and slots, and the model stays in its slot; it stays cut through a grid change; every cap can be cut; an index that is no cap does nothing | 4 | A cap that bounces a ball while its view shows a stem, or a slot freed under a stem |
| `MushroomBoard`, growing back: a cut cap is due after the settings' beats (5), counted from its own cut, and stays due; `Regrow` puts it back with its id, wall, slot and circle; a cap of a wall cut whole comes back by itself; a cut cap that left the grid is forgotten and the next level's caps are whole; the cut caps are kept in the order they were cut and a beat counted for the first of them leaves the ones cut after it their whole wait | 5 | A cap back early or late, back as another cap, or a cut carried into the next level |
| `MushroomBoard` allocates nothing: asking and counting with nothing cut, and cutting, counting and regrowing | 2 | Garbage on every fixed step of a meadow level |
| `MushroomRegrowController` over the real board: a cut cap grows back on the fifth beat after its cut, not before and not between beats, announced once; the first beat is the first to fall after the frame of the cut; beats that fell while nothing was cut are not counted; two caps cut beats apart each come back on their own fifth; a board cut bare grows every cap back; a beat in the frame of a cut is not that cap's, for the first cut on a whole board and for one cut while another waits alike, and a cap cut while another waits counts from the first beat after its own frame | 7 | A cap that comes back off the beat, or a beat counted for a cap that was not yet cut |
| Not under a ball: a cap that is due waits for the next beat, and the one after, while a ball's centre is inside its circle, and does not pop back off the beat once the ball has gone; a ball outside the circle is not in its way; flown by the game's own step over the real board, a ball over the stem holds the cap back, the cap returns on the beat after the ball has flown on, and the ball bounces off it when it comes back | 3 | A ball left inside a bumper |
| A count that falls (a run started over) is no beat; caps that left the board while cut are not regrown and not announced, and a cap cut on the new board counts its own beats | 2 | A cap of the last level popping back onto this one |
| With no cap cut a tick reads no beat, asks after no ball and allocates nothing; while a cap waits, ticks and beats allocate nothing | 2 | A controller that costs every frame of every level |

### Meadow mushrooms, blasts (`MushroomBlastTests`, `MushroomBlastRuleTests`, `BombBlastPiercesTests`, `MushroomBlastMirrorTests`, `ShippedMushroomSoundsTests`, and cases in `NukeBlastTests`) — 67 cases

Blasts take caps off (`Plans/PLAN-MeadowMushrooms.md`, *Blasts take caps off: the rule as built*): a bomb blows off the caps of the mushrooms next to it, where it would break an unbreakable, and sways the others its blast reaches; a nuke blows off every cap. `MushroomBlastTests` runs `Item/Bomb/MushroomBlast` over the real `MushroomBoard` on the shipped grid's numbers, with the real `MushroomRegrowController` where a cap grows back. The view's side is not built yet and has no tests here. All run outside the Editor but `ShippedMushroomSoundsTests`, which loads the shipped sound bank and so needs one.

| Area | Cases | What could break |
|---|---|---|
| The board the tests stand on is the shipped one: slots 0.75 apart, the second ring at 1.30 and 1.50, the far cap at 2.25, a cap's radius 0.3 | 1 | Distances asserted against a board the game does not play on |
| A bomb blows off the caps in the slots next to it and leaves the others on, and the two are cut caps on the board; each is announced as a blast (`MushroomCutCause.Blast`), at the cap's own centre, with the unit direction from the bomb to the cap | 2 | A cap that goes without the view being told, or thrown toward the bomb |
| (The fixture holds the rule at a share of 1, the blast's own reach, whatever ships.) The caps the blast reaches without taking them sway: the second ring only, pushed away from the bomb, at (1.55 - distance) / 0.8, so 0.31 and 0.06; a cap past the blast neither goes nor sways | 2 | A sway the wrong way, at full strength at the far edge, or sent to a mushroom the blast never reached |
| The sway's share widens the sway (2: three mushrooms), narrows it (0.9: one) or turns it off (0), every strength above 0 and at most 1, and takes no cap more or fewer | 4 | A setting that moves which caps are lost |
| A normal bomb takes by the slot and not the distance (a mushroom two rows off, nearer the blast than its slot, keeps its cap and sways); a rainbow bomb takes whatever has its centre inside the kill radius; on the shipped lattice a rainbow bomb on its slot takes the same six as a normal one, and the twelve of the second ring sway | 3 | Caps lost by another rule than the one the bomb breaks unbreakables by |
| A cap already gone is left as it is: not announced, not swayed; a board without mushrooms hears nothing; a bomb in a corner takes the neighbours that exist | 3 | A stem announced as blown, or a bomb at the board's edge throwing |
| A nuke's `BlowOff`: the cap of the mushroom in that slot goes, blown away from the blast's centre, and nothing sways; a bare stem or a slot without a mushroom does nothing; every mushroom told leaves the board bare, each cap announced once; a blast centred on the cap blows it straight up | 4 | A nuke that leaves caps on, or announces one twice |
| A field that kept its cap when told to cut it is not announced | 1 | A view showing a stem where the ball still bounces |
| What follows is a cut's: a blown cap grows back on the fifth beat and is announced by the regrow; a bomb over a cap a lance had cut does not start that cap's wait again, and each comes back on its own fifth; after a level change nothing is cut and nothing regrows onto the new board | 3 | A cap that never comes back, a wait started over by every blast, or the last level's stems carried into the next |
| `MushroomBlastRule`: the reach is the kill radius times the share and the cap's radius, and nothing at a share of 0 or under, or unauthored; a negative cap radius adds nothing; the strength is 1 at the nearest slot and a straight line to 0 at the reach, 1 nearer than that, all or nothing when the reach ends inside the nearest slot, nothing anywhere with no reach; `Away` is the unit direction from the blast to the cap and straight up when they are one point; the share is never under 0 | 20 | A sway of the wrong size, or a division by a reach of nothing |
| `BombBlast.Pierces`, the bomb's rule for what it breaks outright, asked of one thing: a normal bomb pierces the six slots next to it and no other; a rainbow bomb on its slot the same six, each 0.75 away; a neighbour slot the blast does not reach is not pierced; a rainbow bomb goes by the centre whatever the slot; and over a full board, on the slot and aside of it, normal and rainbow, it names exactly what `BombBlast.Resolve` marks as a piercing hit | 8 | The caps' rule and the unbreakables' rule coming apart |
| `NukeBlast`: the edge blows the cap off a near mushroom before a far one, each told once with the blast's centre, the mushrooms left standing and no balloon popped but the nuke; a level change stops the caps going where the edge stood; a grid actor that is no mushroom is told nothing; and with the real `MushroomBlast` over the real board, a nuke stopped by a level change has blown the near cap alone, the next level's caps are whole, none is blown, nothing of the old board is cut on the new one and no regrow follows | 4 | A nuke that pops a mushroom, takes a cap twice, or goes on baring a board that has gone |
| Both sides, over every board of the catalogue: a bomb in every slot that holds no mushroom, normal and rainbow, takes the same caps by id in the game (`MushroomBlast.Bomb` over the real board) and in the solver (`ShotBumperLayer.BlowOff` over a layer gathered from it), never more than six | 1 | A solver that flies through caps the game still has, or bounces off ones it has lost |
| Simulated flights: the cap next to a bomb is gone before the ball reaches it (no bounce, one cap blown, named in the log) where without the bomb the ball bounces, and the next flight starts from the board as gathered; caps that are not next to the bomb stay; a chain of two bombs takes the caps next to each; a rainbow bomb takes a cap by its kill radius that a normal one leaves; a bomb that clears the board still tells of its caps; a layer without slots loses none; with no mushrooms a bomb flies as before | 8 | A simulated ball bouncing off a cap its own bomb blew, or caps staying gone for the next shot solved |
| The shipped bank: `MushroomBounce` has a clip; its entry keeps four voices, does not loop and does not walk the key (the router sets the note); it sits a little under a pop in priority | 3 | An id appended without its entry, which plays silence |

And in fixtures listed elsewhere: a lance's cut still says its cause is a lance (`ProjectileMotionResolverBumperTests`), the stroke's gate (`MushroomSoundRouterTests`), the counter (`GameplayMetricsServiceTests`, `MetricCatalogFoldTests`) and the setting (`MushroomSettingsTests`).

### Meadow mushrooms, the bank (`MushroomBankTests`, and cases in `ProjectileMotionResolverBumperTests`, `MushroomMirrorTests`) — 16 cases

Decision K of `Plans/PLAN-MeadowMushrooms.md` (*The bank, as built*): every cap a lance cuts banks that shot a lance charge, which keeps it a lance at the wall where its pierce would end. `MushroomBankTests` flies the game's own step through `MushroomLiveGame` with `plowsToughs` on: a lance plows a balloon with hits to spare as the game does (it stands until the lance's next wall), the wall's discharge is told to the real `SnipeItemHandler`, and that spends the charge. Off, which is how the mirror uses the harness, such a balloon is popped at once and no pierce is ever spent, as in the simulator. The sound, the counter and the chain's flown case are in the reward's section above. All run outside the Editor.

| Area | Cases | What could break |
|---|---|---|
| The resolver: every cap a lance cuts banks the flight a plain charge with no limit, and the cut and its message say which of the flight's charges it was; the count is the flight's over its steps and counts cuts, not the charges the shot holds (a Snipe's banked charge is added to); a ball that bounces banks nothing; a cap the field did not cut banks nothing (in the case of the field that cuts nothing) | 3 | A charge for a cap that is still there, or a count that restarts every step |
| A cap cut in the very step that reaches the wall where the pierce is spent has already kept the lance armed there; with no cap on the way the same step ends the pierce | 2 | A charge banked a moment after the wall that needed it |
| Flown by the game: a lance that cut a cap and plowed an unbreakable is still a lance off the wall, the charge spent there, and to its last wall; the same shot with no cap on its line is an ordinary ball off that wall and lives exactly as long; a shot that dies at that wall takes its charge with it; with nothing plowed no charge is ever spent | 4 | A bank that never pays, pays a dead ball, or changes how long a ball lives |
| The loop inside one flight, bound by the shot's life: a lance with 40 shields on a seven-cap board with unbreakables to plow flies exactly 41 legs from every angle tried, cuts no cap twice, banks a charge a cap, never spends more than it banked or more than one at a wall, and the flights do spend charges | 1 | A lance that re-arms itself for ever |
| The other bound, the caps: over the real `MushroomBoard` with `MushroomRegrowController` running, a lance with 60 shields cuts a cap again only once it has grown back, no sooner than four whole beats after its cut, banks one charge a cut, and dies by its shields | 1 | A cap cut twice before it regrew, each time banking a charge |
| A cruise's pierce, pinned as it is: one that cut a cap and plowed an unbreakable stays piercing at the wall that spends it, its cruise ended, with the Snipe's speed buff and at the Snipe's speed, to its last wall; with no cap cut the same wall ends the pierce as before the bank | 2 | The rule for a cruise's pierce changing by accident, either way |
| The speed a banked charge re-arms with is one number on both sides: a layer gathered from an item configuration banks a cut's charge at that configuration's Snipe speed buff (`ShotSimulator.BankCutCharge`), and the game's lance, re-armed by a charge under the same number, flies at it; with no speed buff a charge is banked at base speed; and, in the Editor, the shipped item configuration's Snipe speed is what the layer and the banked charge carry | 3 | A simulated lance re-armed at another speed than the game's |
| The mirror: wherever the caps cut are held equal the charges banked are too, one a cap, on every flight the mirror flies (`MushroomMirror.Difference`); a chain's lance and a loaded one each bank two for the two caps they cut, on both sides | in the mirror's cases | A simulator that banks on other cuts than the game |

### Meadow mushrooms, the cap's reaction (`MushroomCapSpringTests`, `MushroomViewTests`, and a case in `MushroomSettingsTests`) — 27 cases

Phase 6 of `Plans/PLAN-MeadowMushrooms.md`. The spring is a plain struct and needs no engine. The view's tests build a `GameObject` with a renderer and a `MushroomView`, hand it a real `SlotGrid`, the shipped settings and a subscriber that captures the handler, and call the frame themselves (`Tick`), since edit mode runs no `Update`. What the shader draws is read back off the renderer's property block. The look itself has no test: it is judged from staged captures (`Tools/stage`).

| Area | Cases | What could break |
|---|---|---|
| A fresh spring is at rest and says so; the cap's furthest lean lies along the ball's way in, for six directions; the wobble stays on that line and swings back past rest | 8 | A lean off the hit's line, or a cap that creeps home instead of wobbling |
| The furthest lean is the tuned reach for the ball's speed; a faster ball leans further, and neither speed nor six hits at once carry it past the most; a bounce that pays swells the cap about as far as tuned, and a swell is not a lean | 3 | The reach drifting when the frequency or the damping is retuned; a cap carried off its quad |
| The cap comes to rest and is then exactly at rest; it takes the same time at 120 and at 20 frames a second; a two-second hitch throws nothing off; a hit with no direction does nothing; `Reset` stops it; the shipped tuning is underdamped, fits its quad, and a shot off the cannon knocks the cap far enough to uncover the stem (over four tenths of the radius) with room left for a faster one | 6 | A spring that never sleeps, wobbles longer on a slow device, or blows up on a long frame |
| A spawned view is off and drawn at rest with the settings' radius; a bounce off its own slot switches it on and leans the cap the way the ball was going, from the left and from below, and the shader is handed what the spring holds; a bounce off another mushroom leaves it asleep; a bounce that armed swells the cap | 5 | A view answering another mushroom's bounce, or drawing something other than its spring |
| Once the cap is still the view switches itself off and is drawn at rest; a second hit wakes it; back in the pool it is off, unsubscribed, at rest and drawn so; spawned again it listens again from rest | 4 | An `Update` left running on every mushroom, a subscription leaked into the pool, a pooled mushroom drawn mid wobble on the next level |
| `MushroomSettings.Validate` says so when the furthest lean is over half the radius | 1 | A lean authored past the edge of the quad the mushroom is drawn on |

### Meadow mushrooms, the spores (`MushroomSporePlannerTests`, `MushroomSporeRouterTests`, and cases in `MushroomViewTests`) — 26 cases

What a mushroom lets go (`Plans/PLAN-MeadowMushrooms.md`, *A puff of spores*). The planner is plain C# and is told the time. The router that feeds it is built with no smoke field, so only the specks are seen, counted on each mushroom's own particle system, and it is told the time. The look is judged from staged films.

| Area | Cases | What could break |
|---|---|---|
| A hit at the cannon's speed puffs the whole cone, out of the far rim, the way the ball was going, for three directions; a slower hit puffs less and one under the least speed nothing; the hit that pays puffs more of it, not a longer cone; a hit with no direction lets nothing go | 6 | A puff toward the ball, or from the cap's middle; a soft touch that fogs |
| A mushroom that puffed keeps quiet for its refractory time, and only that mushroom; a rally of 25 knocks in two seconds is a few puffs | 2 | A puff a knock |
| A cut puffs a little more than a hit, along the lance's way from the cut; a blast is the biggest, wide, from where the cap sat, and waits; a blast centred on the cap goes straight up; seven caps blown at once share the budget, the first whole, the last small and none nothing; a spent board lets no hit go until its budget is back; the shipped tuning is a small cone and three steps | 6 | A nuke fogging the board; a blast that shows nothing; the three not reading as steps |
| The router: a hit's and a lance's cut's specks go at once, and only from the mushroom they are for; a blast's burst waits its delay and is let go once; it is dropped when its mushroom has left the board, and is not let go to another mushroom that took the slot; a ninth burst goes at once when eight are waiting; a run reset drops what is waiting, and so does a new level (the level the scope came up on does not); a bounce or a blast where no mushroom stands is nothing | 9 | A burst landing on the mushroom a new board put where the old one stood; a burst lost or let go twice |
| The view's specks leave from the burst's origin, inside its fan, greenish to bluish, as many as asked, and go with the mushroom into the pool; of sixty specks most are under the size the settings name and a few well over it; a mushroom with no particle system lets none go and does not throw | 3 | Specks left in the air on the next level's mushroom |

### Meadow mushrooms, a blast's view (cases in `MushroomCutTimelineTests` and `MushroomViewTests`) — 17 cases

What a blast looks like (`Plans/PLAN-MeadowMushrooms.md`, *Blasts take caps off: the view*): a cap blown off whole and thrown, and the sway. Built as the cut's cases are, with a fourth subscriber for `MushroomSwayedMessage`. The rule that says which caps go and which sway is `MushroomBlastTests`'. The look is judged from staged films of a real bomb and a real nuke.

| Area | Cases | What could break |
|---|---|---|
| A blast takes the cap off at once, whole, with nothing moved yet; the cap is thrown further than a cut piece parts, fast at first and never back, and not sideways; it is lifted early (larger, its shadow well off it, whole) and then lands, shrinks and fades | 3 | A cap that jumps on the frame of the blast, or sinks without ever having been thrown |
| The throw is the same at 120, 30 and 12 frames a second, takes the tuned time and ends on a bare stem exactly at rest; a blown cap grows back as a cut one does; the quad's room is the throw's bound (the radius by the lift and the turn, plus the distance), more than a cut's, with the shadow's room for the lift | 5 | A throw that differs on a slow device; a quad the thrown cap or its shadow is cut off by |
| A `Blast` cut throws one piece away from the blast: the shader is told it is one piece, the line lies behind the cap across the throw and clear of its rim (no cut band), then touches the rim once the cap is up; it is thrown further than a cut parts, larger, its shadow further off, tumbling the way of the throw; it lands in the tuned time on a bare stem asleep, and regrows; a blast on another mushroom or on a cap already off does nothing; a blast mid lean throws the cap from where it stood | 4 | A blast drawn as a lance's cut through the middle; a cap thrown toward the blast; a view left ticking |
| A sway leans the cap away from the blast by the tuned lean, on the blast's line, and swings it back past rest toward the blast, with no swell, for three directions; a fainter sway leans less by its strength; another mushroom's sway is ignored; a bare stem does not sway, in the air or after | 5 | A sway the wrong way or off its line; a stem wobbling with no cap; a sway paid like a chain |

### Meadow mushrooms, a lance's cut (`MushroomCutTimelineTests`, and cases in `MushroomViewTests`) — 27 cases

The view's side of a lance cutting caps (`Plans/PLAN-MeadowMushrooms.md`, *The lance cuts caps: the view*). The timeline is a plain struct and needs no engine. The view's cases are built as the reaction's are, with a subscriber for each of the two new messages, and the mushroom stood off the origin so a cut's world point has to be taken into the view's own space. The rule that publishes the messages is not built yet and has no test here. The look has no test: it is judged from staged films (`Tools/stage`, `cut` and `regrow`).

| Area | Cases | What could break |
|---|---|---|
| A fresh timeline is a whole cap at rest; a cut takes the cap off at once with its pieces exactly where it was; the pieces part quickly (two thirds of the way in the first third of the time) and never come back; they open before they drop, then shrink, sink and fade | 4 | A jump on the frame of the cut; pieces that shrink before the slice is seen open |
| The fall takes the tuned time and ends on a bare stem exactly at rest; it is the same at 120, 30 and 12 frames a second; a five-second hitch ends it at rest | 5 | A fall that looks different on a slow device, or a view that never sleeps |
| The cap pops back past its size by about the tuned overshoot and ends exactly whole, in the tuned time; with no overshoot it never passes its size; a regrow of a whole cap does nothing; a cut while it pops back takes it off again; `Reset` is a whole cap; the shipped tuning is a quick fall and a short pop; `RoomBeyond` is the bound the shader boxes the pieces with (the radius times the turn, plus the part and the carry), more than how far a piece's middle truly gets | 7 | A cap left a hair off its size; a quad smaller than the box the shader asks for, so a piece or its shadow is cut off at the mesh's edge |
| A spawned view is whole and hands the shader no pieces; a cut of its own slot takes the cap off, switches it on and hands the shader the line in the view's own space; a cut of another mushroom leaves it whole and asleep; an already cut cap is not cut again | 4 | A view answering another mushroom's cut; a cut drawn through the wrong place on a mushroom away from the origin |
| The pieces part and turn, then the view switches itself off on a bare stem, still cut, with the pieces' work off; a hit on a cut mushroom does nothing; a cut mid lean starts the pieces where the cap stood and leaves the stem straight; a cut as the cap pops back hands the shader the size it had, and the next mushroom's pieces are full size again | 4 | An `Update` left running on every bare stem for eight seconds; a stem that wobbles with no cap; pieces that jump at the cut |
| A regrow of another slot is ignored, and of its own pops the cap back past its size and ends whole and asleep; a regrow of a whole mushroom leaves it asleep; back in the pool mid fall the cap is whole, and the next spawn is whole and listens for a cut again | 3 | A pooled stem dealt as the next level's mushroom |

### Launch balloons (`LaunchBalloonsTests`) — 8 cases

The launch screen's Play and Continue balloons must never cover each other's ribbons. The fixture opens the
shipped `Launcher.unity` in a preview scene, so the numbers are the scene's own (each balloon's size, line,
ribbon, writing and drift), shows Continue, and runs both through `LaunchBalloonView.NewDrift`.

| Test | Guards |
|---|---|
| On every screen all of Play stays above all of Continue (9:21, 9:20, 9:16, 10:16, 3:4) | Two seeds of 40 s each: every frame, the lowest point of Play's body, line and ribbon is at least 12 canvas units above the highest of Continue's. Both must have drifted, and both ribbons must carry their writing, so it can't pass on two still balloons or two stub ribbons. Prints the least room found |
| The upper room and the lower room leave the gap | `BalloonReach.RoomAbove` / `RoomBelow`: the upper balloon at its lowest is exactly the gap over the lower at its highest, each keeps the edge's clearance, and the travel is split evenly |
| Alone, a balloon has the whole height | `RoomAlone` |
| A screen too short for both | Neither room has travel |

### `PauseServiceTests` — 9 tests

Tests reference-counted pause/resume with nested source tracking and MessagePipe publishing.

| Area | Tests | What could break |
|---|---|---|
| Initial state not paused | 1 | Wrong default |
| Pause → `IsAnyPaused` true | 1 | State not set |
| Pause → publishes `PausedMessage` | 1 | Message not published |
| Resume → `IsAnyPaused` false | 1 | State not cleared |
| Resume → publishes `ResumedMessage` | 1 | Message not published |
| Nested same source → stays paused until all resumed | 1 | Reference count wrong |
| Resume without pause → no-op | 1 | Negative count or crash |
| Multiple sources, one resumed → still paused | 1 | Cross-source interference |
| `ResetRun` clears all sources and unpauses | 1 | Stale pause survives a run restart, freezing the new run |

### `VectorMathExtensionsTests` — 12 tests

Tests pure math: centroid, bounding radius/box, 2D proximity, angle→direction, and the 1D framing clamp the cinematic camera uses.

| Area | Tests | What could break |
|---|---|---|
| Centroid returns arithmetic mean | 1 | Division or summation error |
| Bounding radius returns max distance | 1 | Wrong comparator |
| All same point → radius zero | 1 | Edge case |
| `WithinRadius` inside/outside | 1 | `<=` vs `<` boundary, squared-distance error |
| `DirectionFromAngle` cardinal angles | 1 | Cos/Sin swapped or sign flipped |
| `DirectionFromAngle` is unit length | 1 | Non-normalised result skews placement/scaling |
| `Bounds` spans min/max of all points | 1 | Encapsulate misses a point — camera cuts a trail off frame |
| `Bounds` single point is degenerate | 1 | Edge case the rig relies on (min == max → point focus) |
| `ClampToWindow` inside → unchanged | 1 | Over-eager clamp fights the pan |
| `ClampToWindow` outside → clamped to keep span visible | 1 | Wrong lo/hi math loses the tracked object |
| `ClampToWindow` padding tightens window | 1 | Padding sign flipped |
| `ClampToWindow` span wider than window → fallback | 1 | Crossed clamp bounds return garbage instead of the fallback centre |

### `PathHelperTests` — 12 tests

Tests Catmull-Rom path generation, array sampling, prefix sum, and midpoint displacement.

| Area | Tests | What could break |
|---|---|---|
| CatmullRom starts at first waypoint | 1 | First control point offset |
| CatmullRom ends at last waypoint | 1 | Last control point offset |
| Single point → single result | 1 | Edge case |
| Two points → correct subdivision count | 1 | Formula off-by-one |
| CatmullRomLoop closes to first waypoint | 1 | Loop not closed |
| SampleAt integer index → exact value | 1 | Index mapping error |
| SampleAt fractional → interpolates | 1 | Lerp factor wrong |
| SampleAt beyond bounds → clamps | 1 | Missing clamp |
| PrefixSum correct cumulative values | 1 | Accumulation error |
| PrefixSum first element is zero | 1 | Off-by-one in result |
| MidpointDisplacement preserves endpoints | 1 | Endpoints overwritten |
| MidpointDisplacement count ≤ 2 → only endpoints | 1 | Missing early return |

### `SpawnClockTests` — 17 tests

Tests when `SpawnClock` publishes `SpawnBalloonLineMessage`. Drives `Advance(deltaTime)` directly (`Time.deltaTime` isn't injectable) against a stubbed level-progress/pacing config and a real `PauseService` and `ProjectileRegistry`.

| Area | Tests | What could break |
|---|---|---|
| Grace, then one line per interval | 3 | First line arrives early/late, or the interval isn't restarted after a line |
| A huge frame delta lands at most one line, then waits a full interval | 2 | A frame hitch dumps a backlog of lines |
| Level-up → new level's interval after a fresh grace | 1 | Stale interval or no grace after level-up |
| Frozen while paused, level not `Playing`, off the `Game` screen, or a lone doomed ball flies; resumes after | 5 | A line lands in a moment that must hold still |
| Doomed ball with company, or non-doomed ball → keeps running | 2 | One ball's last breath freezes the clock under other balls |
| Run reset / level change mid-window → grace restarts | 2 | A new run inherits the old run's countdown |
| `NextLineProgress` runs 0 → 1 per window, restarts after a line | 2 | Danger-heart countdown jumps or sticks |

### `SpawnClockSettingsTests` — 5 tests

Tests `SpawnClockSettings.IntervalForLevel`: level 1 is the first interval, later levels decay geometrically, deep levels stop at the floor, levels ≤ 0 run at level 1's pace, and a decay of 1 keeps the interval constant.

### Level generation (`LevelGeneratorTests`, `LevelGenerationSettingsTests`, `ShippedEarlyLevelsTests`, plus the updated pacing and resolver tests) — roughly 40 tests

Tests the generated-level pipeline: the same level and seed give the same mix, staples and featured pieces are always in, budgets bound the optional picks, weight ramps and unlock levels behave, the catalogue validates itself, and the resolver rolls counts from a level-seeded rng. A level leaves out what its scenario doesn't allow (`IContentPiece.AllowedOn`): bushes off the sky, clouds, bushes and shot bouncers off the water, toughs off the meadow; the meadow's own additions are under *Meadow mushrooms, generation* above. `ShippedEarlyLevelsTests` pins levels 1–3 of the real asset (the 12 guaranteed Shields and Tough balloons of level 1). `LevelPacingValidatorTests` and `LevelPacingConfigurationTests` cover the debug presets only.

### `BalloonBalancerTests` — 3 tests

Tests the run-reset cancellation guard added so a balance scheduled before a restart is dropped (it would otherwise animate pooled actors against an emptied grid). The frame-deferred timing itself is a PlayMode concern; here the generation guard is exercised synchronously on an empty grid.

| Area | Tests | What could break |
|---|---|---|
| Current generation → balance runs | 1 | Guard rejects a valid scheduled balance |
| Stale generation after reset → no balance | 1 | The reset race regresses — stale balance touches returned actors |
| `ResetRun` bumps the generation | 1 | Pending balances never invalidated |

### `BalancePathHolderTests` — 2 tests

Tests `ResetRun` clearing in-transit state on a restart — killed balance tweens never fire their per-actor `Release`, so transit state must be dropped wholesale.

| Area | Tests | What could break |
|---|---|---|
| `ResetRun` clears transit slots | 1 | Stale in-transit slots block spawn pathing next run |
| `ResetRun` drops per-actor slot list | 1 | Reusing an actor after reset double-counts old slots |

### `PressurePropagationTests` — 11 tests

Tests the directed pressure search (`TryResolve`) — a board-up shove propagating best-first through occupied movables (hop alignment minus heaviness cost), scoring escapes via the shared `MoveWeightEvaluator` and emitting a mover-first move list.

| Area | Tests | What could break |
|---|---|---|
| Straight / bent chains resolve, mover-first ordering | 2 | Chain executes onto occupied cells, or seed never freed |
| Full board / statics / immovable entry / empty column → false | 4 | Reports relief where none exists, or shoves through statics |
| Heaviness routing prefers light chains | 1 | Heavy movers shoved when a lighter route exists |
| Relocation terminals (nearest/farthest) end the chain | 3 | Relocator not vacating, or wrong gap picked |
| Puff at entry seeds the balloon above it | 1 | Traversability seeding regression blocks relief |

### `FormationGlowFieldTests` — 24 tests

The formation glow's field (`Balloon/View/FormationGlowField`): which rings light and how brightly, how a disconnected ring dissolves instead of vanishing, where the shader's rings join, and that they join only within a formation.

| Area | Tests | What could break |
|---|---|---|
| A whole lone ring sits at the ring radius; a dissolved one draws back into its member | 2 | Threshold, shape and shader sum disagree |
| Neighbours dissolve in over the dissolve time at full brightness, a joiner easing into the merge; brightness falls with distance | 3 | Rings pop in or fade up, a merge snaps on, or the closeness curve is inverted |
| A lone member, a far pair and a balloon out of formation don't light | 3 | Lone unbreakables glow, or a non-member counts as a neighbour |
| A member leaving, popping, or the board clearing dissolves the rings over the dissolve time at their brightness, following a leaver and staying where a popped one was | 4 | Rings vanish at once, dim before dissolving, or jump |
| Reconnecting mid-dissolve grows back; stopped time holds a dissolve | 2 | Ring restarts from nothing, or dissolves through a pause |
| A pooled look spawning again starts a fresh ring and leaves its old one dissolving | 1 | Respawn steals the old ring and drags it across the board |
| Bounds cover the rings and their light; capped at the array size | 2 | Quad clips the ring, or the array overruns |
| Neighbours' rings join, two slots apart they don't, and a dissolving neighbour eases the merge off | 3 | Reach default no longer joins a line, or a merge snaps apart |
| Two formations side by side keep an outline each; rings are written formation by formation; a member beside only another formation's lights nothing; a dissolving ring keeps its formation | 4 | Neighbouring formations run into one outline, the shader's per-formation sum is fed out of order, or a popped member's ring dissolves into the formation next door |

### Formation covers: the covered balloon, its ring, the preference for gifts, and the hold (in `FormationGlowHostsTests`, `FormationGlowBeamsTests`, `FormationPlannerTests`, `UnbreakableFormationServiceTests`, `CoveredBombNeedTests`, `BalancePlannerTests`, `PressurePropagationTests`, `BalloonBalancerTests`, `NukeBalloonModelTests`, `ShotSolver/CoveredHostMirrorTests`) — 66 cases

The balloon an item cover stands round (its host), from the planner's choice to the ring the glow draws over it. `FormationGlowHostsTests` runs the view's frame by hand (`FormationGlowHosts.Sync`, then `FormationGlowField.Step`) on the shipped `FormationGlowSettings`, not a substitute, so the ring is the size it is in the game and a frame can be measured for allocations. The hold is tested where each mover lives: the balance race, the pressure shove, the balancer's pulse and the solver's gather each get a balloon a cover holds (`SetHeldByCover`), and the service's fixture checks who is held and when. What the solver's simulation does with an anchored target is `AnchoredTargetFidelityTests`'.

| Area | Cases | What could break |
|---|---|---|
| A cover's plan reports the slot of the balloon it stands round; a line reports none, and so does a cover with no spot after one that had | 2 | A line given the last cover's host |
| The service records a cover's balloon with its formation's id (the one its members carry) and its slot's world centre; a formation of another shape records none | 2 | The ring drawn off the balloon, or in another formation's outline |
| The balloon is kept by a cover planned again round it, and by a cover that lost a member but stands | 2 | The ring restarting at every re-plan, or going at the first pop |
| It is kept, and held, through the hold and freed when the cover changes shape; freed when the cover breaks (it outlives its cover there), when it pops, when it is taken off its slot (the balloon that takes the slot is neither covered nor held), on a run reset, and when formations are switched off | 6 | A ring left over a balloon nothing covers, or a balloon left anchored for good |
| A cover whose hold runs out with no shape to change to (the level allows none) is set loose with no balloon listed, no ring source left in the glow's sources, and the balloon free on its slot | 1 | The list keeping a balloon the cover let go before a plan that then failed: a ring over a balloon nothing covers (found in review) |
| The board cleared, the covered balloon first or its members first: no balloon stays held and no formation stands | 2 | The level's ascent or a nuke leaving a model held, by the order it took the balloons in |
| The covered balloon is held before any member sets off; a three-member cover keeps it between its members through a balance pass, though it has room above to rise into | 2 | The balloon floating up out of an open cover while the members glide in, which is what the owner saw: a cover round nothing |
| A covered nuke is held unarmed, and free again when its cover breaks; held, it is anchored unarmed, and armed it stays anchored once let go | 2 | The cover's hold and the fuse's overwriting each other |
| A snake turns aside from the covered balloon; a held balloon is no balloon to trade places with (the question every one of the service's moves asks) until it is let go | 2 | The service's own snake, joiners or gap-closing taking the balloon out of its cover |
| A cover never stands round a balloon another cover holds; a line plans over free balloons further off sooner than over held ones | 2 | Two covers holding one balloon, so the first to let go frees it under the second; a formation trading a host out |
| A balloon a cover holds keeps an unsupported slot in the balance race whatever its kind (plain, golden, heart, disco ball, nuke), and let go it floats up | 6 | The flag missing on one of the models a cover stands round |
| A held balloon blocks a pressure chain like a static until it is let go, and can't be the seed; over a gap it is no possible move, so the flight pulse does not fire for it, until it is let go | 3 | A new line shoving the balloon out of its cover, or the balancer pulsing for a move that never happens |
| The solver's gathered target is anchored while a cover holds the balloon and not once let go, for each kind | 5 | The solver's predicted pulses floating a covered balloon out of a cover the real board keeps it in |
| The covered balloon's ring comes in as its members arrive: none while the nearest is past the fade distance, dim at 1.5 off, full once they stand beside it | 1 | A ring lit over a balloon whose cover has not arrived, now that it is a host from the plan |
| A covered balloon gets a ring of its formation; its centre is outside the outline without that ring and inside with it, for a full cover and for one of three | 3 | The hole or the notch back in the middle of a cover |
| The ring dissolves in at full brightness; none lights while the members are still far off | 2 | A ring popping in, or lit over a balloon before its cover arrives |
| The balloon gone: its ring dissolves where it was and the members keep theirs. The cover broken: it dissolves with its members. The cover changed shape: its source is taken out | 3 | The ring vanishing at once, or staying with a line that no longer covers it |
| A pooled ring taken up for another balloon leaves the old one dissolving where it was; the same balloon under another formation gets a ring of each | 2 | One ring dragged across the board, or handed from one formation's outline to another's |
| A standing cover keeps the one source and 200 frames allocate nothing | 1 | A per-frame allocation in the view, or a ring restarting each frame |
| The light: a closed cover with its balloon casts one beam across the ring, through the middle; a cover down to two members either side of the balloon is one group through it | 2 | The capsule ending at the middle, or a thinned cover lit as two |
| A cover stands round each of the four reward balloons (golden, heart, disco ball, nuke) and reports it as its host; the service covers a gift and lists it | 5 | A reward type left out, or a gift covered with no ring |
| A gift and an item carrier that cost the same to cover (a board that mirrors itself): with no preference the first found wins, with one the gift does | 2 | The preference not applied, or applied at 0 |
| Three unbreakables standing as a cover round a bomb, a gift 7 slots of travel away: at a preference of 2 for each member (6) they stay, at 3 (9) they go to the gift | 2 | The preference absolute, or counted once and not for each member |
| With a gift's spot cheapest, whichever of the few cheapest spots the dice pick is a gift's; with no preference set the pick may still land on the item carrier | 4 | `SpotChoices` handing the cover to the item carrier whatever the preference, or narrowed when no preference is set |
| On a snake-only level (the meadow) a gift on the board is not covered: the unbreakables form a snake | 1 | The meadow's rule broken by the new host |
| `CoveredBombNeed` reads none for a gift under a full cover | 1 | A pity for a lance building under a balloon that carries no bomb |

### `UnbreakableBalloonModelTests` — 6 tests

`EvaluateHit` for the Unbreakable model: deflects all damage without `Piercing`, pops only with the `Piercing` flag; interface conformance (`IHitable`, not `IHasDurability`).

### `HitableActorTests` — 5 tests

Capability contracts on minimal hand-written stubs: a deflector returns `Deflect`, an absorber returns `Absorb`; neither is `IHasDurability` / `IBalloonModel`.

### `GatekeeperActorTests` — 2 tests

Gatekeeper durability: survives → `Deflect` and decrements hits; killing blow → `Pop` with `HitsRemaining == 0`.

### `StructuralActorTests` — 6 tests

Static obstacle contracts: `PuffObstacleModel` is `Static` + `IPassThrough` + not `IHitable`; `BushObstacleModel` is `Static`, not pass-through, not hitable.

### `GridActorHitControllerTests` — 3 tests

Routing of `ActorHitMessage` for grid actors: balloon models ignored; a Gatekeeper is removed from the grid when its hits reach zero; a deflector is never removed.

### `PlayerHealthControllerTests` — 6 tests

HP loss model: starts at configured hit points; a blocked spawn costs one HP; reaching zero requests `EndRun` exactly once; a blocked spawn at zero doesn't underflow or re-request.

### `SpaceDangerTests` — 5 tests

Danger-level scaling from the next line's overflow vs remaining hearts: none → safe; partial → scales; overflow ≥ hearts (or zero hearts) → clamps to max danger.

### `CinematicsSettingsTests` — 5 tests

Tests the per-state cinematic declarations on a fresh `CinematicsSettings` instance — the field initializers ARE the canonical declarations, so a fresh instance must equal the shipped asset.

| Area | Tests | What could break |
|---|---|---|
| Every `CinematicState` has a declared entry | 1 | A new state ships without traits/tuning and silently behaves trait-less — `EntryOf` throws, this walks the enum |
| Level-up states declare `BlocksLoss \| BlocksShake` | 1 | Game-over fires mid-level-up, or the shake fights the pan-in |
| Heart-drain states + `None` declare no traits | 1 | The 0-HP game-over gets blocked by its own loss cinematic |
| Defaults carry the authored rig values | 1 | Code defaults drift from the shipped tuning (zoom/pan/follow/durations) |
| Level-up tracked trail pulses to 4× mid-flight | 1 | The authored scale pulse lost in a settings migration |

### `TimeScaleServiceTests` — 9 tests

Tests the claim/release time-scale owner — the only legal `Time.timeScale` writer (audit-enforced). Restores `timeScale = 1` in teardown.

| Area | Tests | What could break |
|---|---|---|
| Claim applies its value | 1 | Claims don't reach `Time.timeScale` |
| Lowest active claim wins | 1 | The popup's freeze loses to a cinematic's slow-mo |
| Release falls back to the next claim | 1 | Releasing the popup snaps to 1 instead of the restore ramp |
| Releasing the last claim restores 1 | 1 | The "forgot to set it back" bug class returns |
| Re-claiming a source replaces its value | 1 | Per-tick curve claims accumulate instead of replacing |
| Claim above 1 can't exceed normal speed | 1 | A bad curve fast-forwards the game |
| Negative claim clamps to zero | 1 | Negative timeScale (reverses physics) |
| Release without claim is a no-op | 1 | Spurious release throws or disturbs other claims |
| `ResetRun` clears all claims | 1 | A stale freeze survives a run restart |

### `HeartTrailFocusTests` — 5 tests

Tests the heart-drain camera focus contract: centre on the **oldest** in-flight heart (the one about to land and pop) while the bounds span every trail. Uses real `GameObject`s (destroyed in teardown).

| Area | Tests | What could break |
|---|---|---|
| No trails → no focus | 1 | Rig frames garbage while waiting for the next launch |
| Centre is the oldest trail, not the centroid | 1 | The centroid regression returns — new launches drag the camera off the pop |
| Bounds span all trails | 1 | Newer trails cut off frame when they'd fit |
| Oldest arriving hands focus to the next | 1 | Focus sticks to a landed heart |
| Destroyed-but-tracked trail skipped | 1 | Pooled teardown race throws or zeroes the focus |

### `ListExtensionsTests` — 3 tests

`List<T>.SwapRemoveAt` — moves the last element into the removed slot (order not preserved); last-index just removes; single element empties.

### `SoundHandleTests` — 11 tests

Tests the `readonly struct` equality/validity contract that gates `SfxService.Stop()`.

| Area | Tests | What could break |
|---|---|---|
| `Equals` — same/different voice id, generation, non-`SoundHandle` object | 4 | Struct equality only checks one field, or throws on a type mismatch |
| `IsValid` — zero vs non-zero generation, `None` | 3 | A default/uninitialized handle reads as valid and stops the wrong voice |
| `==`/`!=` operators | 2 | Operator overloads drift from `Equals` |
| `GetHashCode` — equal handles match | 1 | Handles can't be used as dictionary keys |
| `None` equals `default` | 1 | `SoundHandle.None` isn't actually the zero value callers expect |

### `SoundBankConfigurationTests` — 5 tests

Tests `SoundBankConfiguration.TryGet` — the runtime gate between an authored `GameSoundId` and a silent no-op.

| Area | Tests | What could break |
|---|---|---|
| Authored entry with clips → found | 1 | Lookup or ordinal indexing wrong |
| Entry without clips → not found | 1 | An unauthored slot (Unity's default-constructed entry) plays a null clip instead of a no-op |
| Unauthored slot → not found | 1 | Out-of-author-order id incorrectly resolves |
| `None` id → not found | 1 | `GameSoundId.None` accidentally maps to a real entry |
| Out-of-range ordinal → not found, no throw | 1 | A stale/appended enum value indexes past the array |

### `VariationPickerTests` — 17 tests

Tests clip/pitch/volume selection — plain random ranges, the three melodic modes
(`ScaleWalkUp`/`ScaleWalkDown`/`Tension`), the per-`SfxEntry` octave-cap/skip-step knobs,
burst spread, and pan gating.

| Area | Tests | What could break |
|---|---|---|
| Plain mode pitch/volume within configured range | 1 | Random range math out of bounds |
| ScaleWalkUp — streak 0 → root pitch | 1 | Degree-zero mapping wrong |
| ScaleWalkUp — first ascent peaks an octave up | 1 | Octave rollover math wrong |
| ScaleWalkUp — `MelodicSkipSteps = 0` loops within one octave | 1 | Yoyo dip math wrong at the no-climb boundary |
| ScaleWalkUp — default skip net-climbs across cycles | 1 | Skip-step advance not applied per cycle |
| ScaleWalkUp — skip equal to scale length climbs without dipping | 1 | Full-skip special case not treated as a plain climb |
| ScaleWalkUp — `MelodicMaxOctaves` shrinks the ceiling | 1 | Per-entry octave cap ignored or shared globally |
| One shared picker honours each entry's own range | 1 | `MelodicMaxOctaves`/`MelodicSkipSteps` bleed between entries instead of reading per-`SfxEntry` |
| ScaleWalkDown mirrors the up walk below the root | 1 | Mirrored sign flip missing or inverted |
| ScaleWalkUp — runaway streak stays bounded | 1 | Ceiling not enforced past many cycles |
| ScaleWalkUp — mid-streak → correct scale degree | 1 | Modulo/index-into-scale wrong |
| ScaleWalkUp — empty scale → falls back without throwing | 1 | Divide-by-zero on `degree % scale.Count` |
| Tension — adds tension semitones to current semitone | 1 | Tension offset not applied against the live pop degree |
| Burst index > 0 → pitch up, volume down | 1 | Burst-spread math missing or inverted |
| Multi-clip entry never repeats the same clip consecutively | 1 | No-immediate-repeat guard broken |
| `Pan2D = false` → pan forced to zero | 1 | Pan gate bypassed for non-panned entries |
| `Pan2D = true` → pan equals the normalized input | 1 | Pan value dropped or transformed |

### `VoiceLimiterTests` — 8 tests

Tests per-id and global concurrent-voice accounting and the priority steal/drop rule.

| Area | Tests | What could break |
|---|---|---|
| Under caps → distinct slot ids | 1 | Slot allocation collides |
| Per-id cap reached → steals the oldest same-id voice | 1 | Wrong voice stolen, or per-id count not adjusted |
| Global cap, equal priority → steals the lowest-priority slot | 1 | `>=` vs `>` on the steal comparison (an equal-priority request must still win) |
| Global cap, strictly lower priority → dropped, not stolen | 1 | The pop-can't-starve-a-stinger guarantee — low priority steals a high-priority voice |
| `Release` frees the slot and decrements counts | 1 | Slot leaks or double-counts |
| Released slot is reused by the next acquire | 1 | Free-list not replenished |
| `Release` on an invalid id is a no-op | 1 | Negative/out-of-range id corrupts state |
| `Clear` restores full capacity | 1 | Stale slots block every id after a reset |

### Effects clearing (`EffectsClearingStamperTests`, `EffectsClearingControllerTests`, `EffectsClearingSettingsTests`, `ShippedEffectsClearingTests`, `CameraCullingMaskTests`) — 48 tests

The clearing mask's stamps (`Plans/PLAN-EffectsLayerClearing.md`): what each ball in flight stamps, and the wiring that keeps it fed.

| Area | Tests | What could break |
|---|---|---|
| `ClearingView` world → mask UV, lengths in view heights | 3 | A wrong aspect or zoom puts the clearing beside the ball, or the wrong size |
| `ClearingView` reprojection: identity when still, a world point kept in place through a pan, a zoom, both | 4 | The level-up rig's zoom leaves the clearing where the ball was on screen |
| Stamps: none, first sighting a disc, then a capsule from last frame, a disc again after a gap or a retire, balls tracked apart | 6 | A corridor drawn across the screen from a stale position |
| Trail off: a disc where the ball is, no corridor back to last frame's spot | 1 | The no-trail comparison still drawing the trail |
| A ball in flight is a full dome; trail off, a ball gone from flight (its glide over, or retired) shrinks away where it was last seen over the linger time, keeping its radius, while another flies on; trail on, it leaves no stamp | 5 | A dying ball's lens vanishing at once, or one that never closes |
| More balls than the shader's 8 stamps keep the most central | 1 | The cap drops the ball the player is watching |
| Radius by shields: grows evenly from one shield to ten, none clears as one, past ten capped at the max, two balls each their own | 6 | A ball on its last leg stamping nothing, or a stacked ball clearing the whole board |
| Controller binds the mask for the run, unbinds only itself, forgets retired balls, stops listening when disposed | 6 | A new run's source dropped by the old run's teardown, or a disposed run still feeding the mask |
| `EdgeHigh` always above `EdgeLow` | 4 | The composite's `smoothstep` with equal edges |
| Shipped wiring: both shaders on the settings asset, both components on the Main Camera prefab, the settings on the app scope | 3 | A shader only found by name, stripped from a device build |
| Every balloon pop effect, item effect (bar the shield gain) and the nuke cloud draws into the main camera's map, with a material that writes its coverage | 3 | A new effect authored on a board layer, burying the ball's surroundings where the glass can't clear it |
| The main map is HDR, and the `Effects` layer is drawn by the main camera (under post-processing) and by no other camera | 2 | The lightning and the laser clipped to 1 with no bloom, as they were on the HUD camera |
| Culling-mask owner: a claim takes layers out of the base and gives them back, a new base keeps the claim, a release never brings back a layer the base dropped, one owner per camera | 4 | A navigation change while the main map runs drawing the effects twice, or the map going idle leaking them into the launch screen |

### `LayerPresenceTests` — 8 tests

Whether anything is alive on a layer, which decides whether an effects map renders.

| Area | Tests | What could break |
|---|---|---|
| A tracked object counts on its renderers' layer while active, an inactive child's layer included, and not on other layers; without renderers it never counts; tracked twice it counts once | 4 | An effect out of the pool said absent, so it draws outside the glass, or one counted forever, so the map never skips a frame |
| The layer stays present until the last object leaves, deactivated or destroyed; a mask sees any of its layers | 2 | The map going off under a second effect still alive |
| A watched renderer counts only while shown, and not once destroyed | 1 | The nuke cloud missed, or a destroyed one keeping the map on |
| A pooled effect counts from `Get` to `Return` | 1 | The pool's tracking unhooked, so every map skips every frame |

### `SfxThrottleGateTests` — 7 tests

Tests the wall-clock cooldown and burst-coalescing window (deterministic via an injected clock).

| Area | Tests | What could break |
|---|---|---|
| First call passes | 1 | Cooldown wrongly blocks the very first play |
| Within cooldown → blocked | 1 | Cooldown window off by a frame/epsilon |
| After cooldown elapses → passes | 1 | Cooldown never releases |
| Within coalesce window → burst index increments | 1 | Burst counting wrong, breaks the pitch-spread "chord" effect |
| Burst cap reached → overflow dropped | 1 | Unbounded burst spam past the configured max |
| After window elapses → burst index resets | 1 | Stale burst count leaks into the next window |
| `Reset` clears cooldown and burst state | 1 | A run restart inherits cooldown from the previous run |

### `AudioSourceVoiceTests` — 2 tests

Tests only the synchronous null-clip guard; the real `AudioSource.Play()` + timed-return path needs the player loop and is covered by `SfxServiceGenerationGuardPlayModeTests` (PlayMode) instead.

| Area | Tests | What could break |
|---|---|---|
| Null clip → completion callback invoked synchronously | 1 | An unauthored `GameSoundId` silently occupies a voice forever instead of returning immediately |
| Null clip → no return timer scheduled | 1 | A `CancellationTokenSource` leaks for a play that never actually started |

### `MusicalPitchExtensionsTests` — 4 tests

Tests the `Shared/Extensions` semitone ↔ pitch-multiplier helper the melodic pop system and `VariationPicker` share.

| Area | Tests | What could break |
|---|---|---|
| Zero semitones → multiplier 1 | 1 | Formula offset wrong |
| +12 semitones → doubles pitch | 1 | Equal-temperament octave math wrong |
| -12 semitones → halves pitch | 1 | Sign handling wrong |
| `int` overload matches the `float` overload | 1 | Overload drift |

### `CombatSoundRouterTests` — 43 tests

Tests message → `(GameSoundId, position)` translation for hits, shots, reload, and the cruise loop handle, plus the per-ball pitch ramps. The table lists the main areas, not every test.

| Area | Tests | What could break |
|---|---|---|
| `Pop` outcome → `BalloonPop` at world position | 1 | Wrong id or dropped position |
| `Deflect` outcome → `BalloonDeflect`, not `Pop` | 1 | Outcome branch misrouted |
| `Absorb` / `PassThrough` outcomes → `BalloonResist` | 2 | Missing flag check drops a resist sound |
| `Pop` combined with `Deflect` → resolves to `Pop` | 1 | Branch precedence order flips, changing which sound wins on a combined outcome |
| Fired → `ShotFired` at world position | 1 | Position not forwarded |
| `ThrowerReadyMessage` → `ShotReload` with null position | 1 | Non-positional sound wrongly spatialized |
| `ProjectileLoadedMessage` → no reload cue | 1 | Reload sound played when the ball is only loaded, before it can fire (it may still be sliding out or waiting for a free slot) |
| Cruise started then ended → stops the stored handle | 1 | Loop handle not retained across start/end, leaking the cruise loop |
| Two balls in flight: pop, wall-bounce and colour ramps are per ball | 4 | One ball's pops step another ball's pitch |
| Cruise end, destroy or retire stops only that ball's loop | 3 | One ball ending silences another ball's cruise loop, or a retired ball's loop leaks |
| Streak break clears only the named ball's colour ramp (ball-less break clears the shared one); retirement drops a ball's ramp | 5 | A break on one ball resets everyone, or a retired ball's ramp state leaks into the next ball |

### `ItemSoundRouterTests` — 14 tests

Tests per-`ItemType` sound mapping and the item-slot downcast guard.

| Area | Tests | What could break |
|---|---|---|
| Each `ItemType` → its matching `GameSoundId` | 6 | New item type ships without a sound, or maps to the wrong one |
| `ItemType.None` → no play | 1 | An empty item slot plays a spurious activation sound |
| Balloon not `IHasItemSlot` (e.g. `ToughBalloonModel`) → no play, no throw | 1 | Downcast guard missing — router crashes on a non-item-eligible balloon |
| Overflow heart → `HeartDrain` at target position | 1 | Danger-beat sound dropped or mispositioned |
| Spawn blocked → `OverflowThud` at position | 1 | Column-keyed thud not forwarded |
| Pickup pitch ramp is per ball; a retired ball's ramp is dropped; load clears only the shared ramp | 4 | Two balls' pickups step each other's pitch |

### `ProgressionSoundRouterTests` — 4 tests

| Area | Tests | What could break |
|---|---|---|
| Streak changed → forwards to `IMelodicContext.SetStreak` and plays `StreakStep` | 1 | Melodic pop system never learns the current streak, freezing the scale-walk at the root |
| One ball's streak breaks → melody follows `HighestStreak` (the best remaining) | 1 | A break on one ball drops the melody while another ball's streak is still alive |

### `RegisterAudioTests` — 3 tests

Lives in `Assets/Tests/EditMode/Game/` (paired with `GameScopeRegistration`, not the Audio folder). Scoped to the two conditional branches `RegisterAudio` owns — registration existence only, never a full `Build()` (see the fixture's own header comment for why).

| Area | Tests | What could break |
|---|---|---|
| Null voice prefab → registers nothing | 1 | A half-wired scene throws later instead of degrading to "audio disabled" |
| Valid prefab → registers `ISoundPlayer`/`IMelodicContext`/`IAudioMixerRouter` | 1 | Registration graph incomplete |
| Null sound bank → still registers a working fallback bank | 1 | Missing bank null-refs every voice-cap reader instead of degrading to silence |

### `BalloonSwayPlaybackTests` — 9 tests

The idle sway's state machine (`Balloon/Controller/BalloonSwayPlayback`), which stands in for the plain balloon's Animator controller. On straight-line loops, so each loop's angle at each step is arithmetic.

| Area | Tests | What could break |
|---|---|---|
| Two poses mix as the normalised sum of their rotations, pinned against worked values a straight line between the angles misses | 1 | The crossfade is up to a tenth of a degree off the Animator's |
| A fresh playback plays the loop `Stable` asks for whole from its first step, and is entering it for that loop's enter time | 2 | A fade is invented where the Animator had none, or the enter time ends a flip's wait too soon |
| Phase is the time since the loop began and wraps at its length; a stopped clock holds the pose | 2 | The sway drifts, jumps at the wrap, or moves through a pause |
| A switch starts on the step that sees it, at no progress, then crossfades into the new loop from its start while the old one keeps playing, over the entered loop's time | 2 | The crossfade runs a step ahead of the Animator's, resumes a stale phase, or takes the wrong duration |
| A flip during a crossfade or the enter time waits for it to end; the step it ends on starts nothing, the step after starts the crossfade back | 2 | A crossfade is interrupted, or the way back starts a step early |

### `BalloonSwayTickerTests` — 9 tests

`Balloon/Controller/BalloonSwayTicker`'s bookkeeping, through a fake `IBalloonSwayView`.

| Area | Tests | What could break |
|---|---|---|
| Each view sways from its own start; `SetStable` reaches only its view | 2 | Balloons sway in lockstep, or one balloon's move flips another's sway |
| `End` stops writing and leaves the last pose; the others keep their own playback when slots reshuffle; ending a view that isn't swaying does nothing | 3 | A float-away fights the sway for the pivot, or a despawn hands a neighbour the wrong playback |
| Beginning again (a pooled reuse) starts over on the unstable loop from its start, with no crossfade or flag left from before | 2 | A reused balloon resumes its last life's crossfade or stays stable |
| `SetStable` on a view that isn't swaying is ignored and not remembered; more views than the starting capacity all sway | 2 | A suspended balloon throws, or the 65th balloon stands still |

### `ShippedBalloonIdleSwayTests` — 8 tests

Pins the baked idle sway (`BalloonsConfiguration`'s Idle Sway block) to the Animator controller it was baked from. Nothing compiles one against the other, so a clip or transition edited without a new bake would leave the balloons on the old sway.

| Area | Tests | What could break |
|---|---|---|
| The baked block reads what `BalloonIdleSwayBaker.Read` gives for its controller now (same times, same curve at 3001 points); `Bake` writes what it reads | 2 | The controller changed and nobody baked, or the baker writes to a renamed field |
| Each baked curve reads what a real Animator plays in that loop's state, at 320 steps across the loop, within a thousandth of a degree | 2 | The baked curve isn't the one Unity plays (it plays a weighted stretch as straight lines between frames, not as drawn) |
| `AsPlayed` leaves a curve with no weighted keys as drawn, and turns a weighted stretch into a key a frame joined by straight lines, the rest as drawn | 2 | The baker resamples what the Animator plays as drawn, or the reverse |
| Every prefab the ticker sways has the clips' child as its sway pivot, level in the prefab | 1 | The ticker turns a different child than the Animator did |
| Every prefab that keeps its Animator plays a controller the baker refuses | 1 | A variant pays for an Animator that only sways |

### `ShippedGiftBalloonShineTests` — 10 tests

Reads the shipped balloon prefabs for the base balloon's two smooth shine sprites. The golden balloon, the disco ball and the nuke draw their highlight in the body's shader, where it breaks along the plates, the mirrors and the dents; they are variants of the plain balloon's prefab, so a re-saved variant could bring the sprites back.

| Area | Tests | What could break |
|---|---|---|
| On `Golden`, `Disco` and `Nuke` every child of `ShinePivot` is inactive | 3 | A smooth shine lies over the body again: the relief flattens, and on the nuke the counter is hidden when the light comes from below |
| On the same three, `BalloonView`'s sprite layer list holds no renderer under `ShinePivot` | 3 | The view enables, hides and sorts a renderer that never draws |
| `Balloon`, `ToughBalloon` and `Unbreakable` keep their shine sprites on; the heart keeps its own `Shine` child drawing | 4 | The change spreads to a balloon whose body has no highlight of its own |

### `BalloonSwayAnimatorParityTests` — 3 tests

Steps a real Animator on the baked controller by hand beside a `BalloonSwayPlayback` and compares the pivot's angle at every step. The EditMode twin of `BalloonSwayAnimatorParityPlayModeTests`, at 70 steps a second (so no crossfade ends exactly on a step); these report inconclusive if the Animator stays inert when stepped by hand outside Play mode. A failure lists the steps after every spawn and flip, the steps around each change in the Animator's state or transition, and the steps apart, each with the Animator's own account of its states.

| Area | Tests | What could break |
|---|---|---|
| Spawn, flips of `IsStable` settled and mid-crossfade, a loop's wrap, to five thousandths of a degree | 1 | The playback starts a crossfade a step off, mixes the poses differently, or lets a flip interrupt |
| A balloon despawned mid-swing and reused, and one reused after its sway was stopped and its pivot tilted, both start over level | 2 | A last life's tilt shows in the next |

---

## PlayMode tests — 7 tests

`Assets/Tests/PlayMode/` (assembly `BalloonParty.Tests.PlayMode`). For behaviour EditMode can't exercise: the async/pooling/scene paths that only run under the player loop. Uses `[UnityTest]` coroutines.

Most fixtures derive from **`PlayModeGameTest`** — the shared base that loads the real Game scene, resolves services from `GameLifetimeScope.Container`, and waits on conditions with a timeout. Its `[SetUp]` resets the static `Navigation` to `Launch` (PlayMode shares static state — no domain reload between tests; a test ending in `GameOver` would otherwise leave the spawn gate shut for the next one). `SfxServiceGenerationGuardPlayModeTests` is the one exception — it builds its own standalone `PoolManager`/prefab/bank rather than riding the shared Game scene (see its own section below).

### `RunRestartPlayModeTests` — 1 test

| Area | Tests | What could break |
|---|---|---|
| Restart clears and repopulates the board | 1 | The clear → re-spawn loop leaks, throws, or leaves an empty board (caught the prewarm "await twice" regression — a stored single-await `UniTask` re-awaited when a restart re-spawn raced the initial prewarm) |

### `PressureLossPlayModeTests` — 2 tests

Drives the spawn-saturation loss loop in the real Game scene — the pressure-balance / reject / HP path EditMode can't exercise.

| Area | Tests | What could break |
|---|---|---|
| Initial load → health at configured hit points | 1 | HP not initialised from config in a live scene |
| Saturation fills the board then drains HP to GameOver | 1 | Reject→HP→loss loop stalls, never reaches GameOver, or over-drains |

### `DisturbanceFieldPlayModeTests` — 1 test

| Area | Tests | What could break |
|---|---|---|
| Stamps + ticks publish the global texture without error | 1 | The "Stamp before Start" class of bug — a stamp arriving before the field initialises throws or corrupts the texture |

### `BombActivationPlayModeTests` — 1 test

| Area | Tests | What could break |
|---|---|---|
| Bomb activation pops neighbouring balloons | 1 | `Physics2D.OverlapCircle` path (real colliders) — radius, layer mask, or hit routing regress. Settles the board first (overflow hold released + no `BalancePathHolder` in-transit slots): the grid registers a balloon at its slot while its collider is still flying there, so a blast during transit overlaps nothing |

### `SfxServiceGenerationGuardPlayModeTests` — 2 tests

Exercises `SfxService`'s slot-generation guard — the one piece of novel, safety-critical logic in the orchestrator: when the global/per-id voice cap forces a slot to be stolen and replayed in place, a `Stop()` carrying the original (now-stale) `SoundHandle` must no-op rather than tearing down the newer play sitting in the same slot. Reaching the "steal in place" branch needs a real, non-null-clip `AudioSourceVoice.Play()` so the voice doesn't complete synchronously — per `AudioSourceVoiceTests`' own EditMode/PlayMode split, that's PlayMode territory.

| Area | Tests | What could break |
|---|---|---|
| `Stop(stale handle)` after a slot steal does not tear down the reused voice | 1 | The generation check is missing or wrong — a late `Stop` from a stolen-away sound silently kills an unrelated voice |
| `Stop(current handle)` after a slot steal still stops it | 1 | The guard over-corrects and blocks legitimate stops on the voice that actually owns the slot |

### `BalloonSwayAnimatorParityPlayModeTests` — 3 tests

The proof that `BalloonSwayTicker` plays what the plain balloon's Animator played. Standalone (no Game scene, editor only): a real Animator on the baked controller runs in the player loop beside a `BalloonSwayPlayback` stepped in `LateUpdate` with the same delta time, and the pivot's angle is compared every frame to five thousandths of a degree. What's being settled is the engine's: the frame an enabled Animator first advances on, how a transition starts and mixes, and what it shows when its object is activated again. Runs at 70 frames a second, so no crossfade ends exactly on a frame. A failure lists the frames after every spawn and flip, the frames around each change in the Animator's state or transition, and the frames apart, each with the Animator's own account of its states.

| Area | Tests | What could break |
|---|---|---|
| Spawn, flips of `IsStable` settled and mid-crossfade, a loop's wrap, 601 frames | 1 | The playback is a frame off the Animator, mixes the poses differently, or lets a flip interrupt a crossfade |
| A balloon despawned mid-swing and reused starts over level | 1 | A last life's tilt shows in the next |
| A balloon reused after its sway was stopped (a float-away) starts level | 1 | A cinematic's tilt leaks into the next spawn |

### `BalloonSwayPlayModeTests` — 4 tests

The idle sway's wiring on real balloons in the Game scene. Each test takes one plain balloon that has landed in a slot of the grid; none waits for the whole board to be stable, which lines arriving on the spawn clock and a walking formation can put off indefinitely. A wait that times out lists every live balloon (type, on the grid or not, stable, tilt, scale, Animator).

| Area | Tests | What could break |
|---|---|---|
| Plain balloons sway with their Animator disabled; the other types keep theirs enabled | 1 | The Animator cost comes back, a balloon stands still, or a tough loses its animation |
| A reused view is level on its first frame and on the unstable loop from its start on the next; after `SuspendAnimator` the pivot is left alone and the next spawn starts level | 2 | A reuse resumes the old sway, or the sway fights the float-away |
| `CheatState.PerfOff.Animators` spawns a balloon with no sway | 1 | The device measurement of "no sway" no longer measures that |

---

## Deferred Systems

These systems are not tested because they are either too coupled to Unity runtime, too simple, or already covered indirectly.

| System | Why defer |
|---|---|
| `BombItemHandler` | `BlastBalloons` uses `Physics2D.OverlapCircle` — needs real colliders and physics simulation. Shockwave nudge publish is covered indirectly by NudgeService tests. |
| `LaserItemHandler` | `CastCross` uses `Physics2D.CircleCast` — needs real colliders and physics simulation. |
| `BalloonBalancer` | Scan+move loop depends on well-tested `IsUnbalanced`/`OptimalNextEmptySlot` + DOTween animation — still deferred. The run-reset cancellation guard *is* covered (`BalloonBalancerTests`); the frame-deferred `.Forget()` timing is a PlayMode concern. |
| `BalloonSpawner` | Heavy Unity/DI coupling (`PoolManager`, `IObjectResolver`, DOTween). Little pure logic beyond forwarding; *when* lines arrive is `SpawnClock`, which is tested. |
| `ProjectileModel` | Pure data bag — too simple to break |
| `OrthogonalSizeCameraController` | Forwards config lookup to camera — simple delegation |
| `ThrowerView.RotateTo` | Single `AngleAxis` call — too simple |
| `SceneTransition` / `Navigation` | Button handler wiring / static state machine — too simple |
| `NavigationService` / `CinematicStateService` | Thin forwarders to the static `Navigation` / `Cinematic` (`Has` is one table lookup, and the table itself is tested in `CinematicsSettingsTests`). The lifecycle logic that consumes them is tested in `RunControllerTests` |
| `PaintSplashView` | MonoBehaviour with `Update`-driven animation; visual correctness is a Play Mode concern. Core logic (target collection, paintability) tested via `PaintItemHandlerTests` |
| `ScoreTrailService` | Trail spawning depends on `PoolManager` + DOTween flight. Trail arrival message is tested via `ScoreControllerTests` |
| `TrailFlightRegistry<TId>` / `TrailFlight` | Pause/Resume/Complete drive live DOTween tweens. The registry's dict bookkeeping is too simple; the flight control paths are exercised by the level-up cinematic at runtime |
| `CinematicDirector` / `CameraRigCinematic` / producers (`LevelUpCinematic`, `HeartDrainCinematic`) | Orchestration across DOTween, unscaled time, the camera rig, and scene ticking — runtime concerns. The pieces with pure logic ARE tested: traits/tuning (`CinematicsSettingsTests`), time claims (`TimeScaleServiceTests`), focus (`HeartTrailFocusTests`), framing math (`VectorMathExtensionsTests`), loss gating (`RunControllerTests`) |
| `CinematicCameraRig` / `CameraShakeService` | Drive a live `Camera` + DOTween (shake is an additive offset applied in `LateUpdate`) — visual correctness is a playtest concern; the clamp math is tested via `ClampToWindow` |
| `FlyingTrail` (motions, follow mode) / `TrailSpawner` | MonoBehaviour + pooling + DOTween/Update-driven flight — PlayMode candidates if they regress |
| `RejectedBalloonEffect` | Pool-heavy overflow pile choreography; the end-to-end reject→heart→HP→loss loop is covered by `PressureLossPlayModeTests` |
| `PoolChannel.Prewarm` / `PrewarmAsync` | Requires MonoBehaviour instantiation (`Create()` returns `Component`). Straightforward push-to-stack logic |
| Views in general | Bind→Subscribe→SetValue chains are tested by UniRx; visual correctness is a Play Mode concern |

---

## When to Write New Tests

1. **Before new code** — write a failing test that describes the behavior, then make it pass.
2. **On bug reports** — write a test that reproduces the bug, then fix it.
3. **When refactoring** — if a "too simple" method becomes complex, add tests before refactoring.
4. **When a design is hard to test** — improve the design so it becomes testable.

---

## Running Tests

Run **all** Edit Mode tests on every code change. They're pure C# — they take milliseconds.

In Unity: **Window → General → Test Runner → EditMode → Run All**


