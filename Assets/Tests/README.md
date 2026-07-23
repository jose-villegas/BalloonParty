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

Tests the item-assignment pipeline: turn filtering, max-cap enforcement via grid scan, `IHasWriteableItemSlot` eligibility gating, and the happy path.

| Area | Tests | What could break |
|---|---|---|
| Empty `NewBalloons` → early return | 1 | Null guard missed |
| Turn not on the item cadence (`ItemCadence`) → skipped | 1 | Modulo check wrong |
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

Danger-level scaling from overflow vs remaining hearts: none → safe; partial → scales; overflow ≥ hearts (or zero hearts) → clamps to max danger.

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

---

## PlayMode tests — 5 tests

`Assets/Tests/PlayMode/` (assembly `BalloonParty.Tests.PlayMode`). For behaviour EditMode can't exercise: the async/pooling/scene paths that only run under the player loop. Uses `[UnityTest]` coroutines.

All fixtures derive from **`PlayModeGameTest`** — the shared base that loads the real Game scene, resolves services from `GameLifetimeScope.Container`, and waits on conditions with a timeout. Its `[SetUp]` resets the static `Navigation` to `Launch` (PlayMode shares static state — no domain reload between tests; a test ending in `GameOver` would otherwise leave the spawn gate shut for the next one).

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

---

## Deferred Systems

These systems are not tested because they are either too coupled to Unity runtime, too simple, or already covered indirectly.

| System | Why defer |
|---|---|
| `BombItemHandler` | `BlastBalloons` uses `Physics2D.OverlapCircle` — needs real colliders and physics simulation. Shockwave nudge publish is covered indirectly by NudgeService tests. |
| `LaserItemHandler` | `CastCross` uses `Physics2D.CircleCast` — needs real colliders and physics simulation. |
| `BalloonBalancer` | Scan+move loop depends on well-tested `IsUnbalanced`/`OptimalNextEmptySlot` + DOTween animation — still deferred. The run-reset cancellation guard *is* covered (`BalloonBalancerTests`); the frame-deferred `.Forget()` timing is a PlayMode concern. |
| `BalloonSpawner` | Heavy Unity/DI coupling (`PoolManager`, `IObjectResolver`, DOTween). Little pure logic beyond forwarding. |
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


