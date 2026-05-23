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

- **All tests are EditMode** — pure C#, no Play Mode. They run in milliseconds.
- **Real objects over mocks** — use real `BalloonModel`, `SlotGrid`, etc. when the class is a plain C# type. Reserve NSubstitute for interfaces (`IGameConfiguration`, `IPublisher<T>`, `ISubscriber<T>`) and ScriptableObjects that need reflection setup.
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

## Current Coverage — 133 tests

### `SlotGridTests` — 42 tests

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

### `ScoreControllerTests` — 22 tests

Tests the scoring pipeline, level-up logic, streak multiplier, `WillLevelUp` projected-progress check, and next-level trail renumbering — deferred scoring via trail arrival, multi-map accumulation with an all-colors threshold gate, consecutive same-color pop multiplier, projected vs confirmed progress, `ScorePointMessage` field correctness, and `IHitable`-based scoring with non-balloon actors.

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
| `ScorePointMessage` below threshold | 1 | Wrong `Level` or spurious `NextLevel` flag |
| `ScorePointMessage` at tipping point | 1 | Off-by-one — `>` vs `>=` makes tipping point next-level |
| `ScorePointMessage` above threshold renumbered | 1 | Score not renumbered, level not incremented, or flag missing |
| `GroupSize` equals points published | 1 | Wrong group size breaks stagger timing in `ScoreTrailService` |
| `GroupIndex` sequential | 1 | Non-sequential indices break trail scatter delay order |
| `IHitable` non-balloon actor — `Pop` outcome scores | 1 | Scoring pipeline too narrowly typed to `IBalloonModel` |
| `Absorb` outcome — does not score | 1 | Absorb mis-routed as Pop |

### `BalloonModelTests` — 10 tests

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
| Turn not divisible by `TurnCheckEvery` → skipped | 1 | Modulo check wrong |
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

### `ProjectileViewAbsorbTests` — 3 tests

Tests `ProjectileView.OnAbsorb` — the absorb path that kills the projectile on contact with an absorbing actor.

| Area | Tests | What could break |
|---|---|---|
| `OnAbsorb` publishes `ProjectileDestroyedMessage` | 1 | Projectile death not signalled — thrower never reloads |
| `OnAbsorb` sets `model.IsFree = false` | 1 | Projectile keeps moving after absorption |
| `OnAbsorb` publishes `ActorHitMessage` with `Absorb` outcome | 1 | Wrong outcome — hit routed as Pop or Deflect |

### `GridSpawnerCoordinatorTests` — 3 tests

Tests `GridSpawnerCoordinator` stage ordering and sequencing — isolated with an `ImmediateGate` to remove the `Navigation` static dependency.

| Area | Tests | What could break |
|---|---|---|
| Spawners called in ascending `SpawnStage` order | 1 | Sort direction wrong — high-priority runs first |
| Each stage awaits completion before the next starts | 1 | Stage sequence serialization broken |
| Multiple spawners at the same stage all run | 1 | Same-stage spawner skipped |

### `PaintItemHandlerTests` — 5 tests

Tests the paint item's neighbor color conversion — paintability filter, same-color skip, empty-color guard.

| Area | Tests | What could break |
|---|---|---|
| Paints different-color neighbors | 1 | Wrong color assignment or neighbor lookup |
| Skips same-color neighbors | 1 | Missing color comparison |
| Skips non-paintable neighbors | 1 | `IHasWriteableColor` interface check missing — tough balloons get painted |
| Empty color → no action | 1 | Null/empty guard missing |
| No neighbors → no crash | 1 | Out-of-bounds on corner slot |


---

## Deferred Systems

These systems are not tested because they are either too coupled to Unity runtime, too simple, or already covered indirectly.

| System | Why defer |
|---|---|
| `BombItemHandler` | `BlastBalloons` uses `Physics2D.OverlapCircle` — needs real colliders and physics simulation. Shockwave nudge publish is covered indirectly by NudgeService tests. |
| `LaserItemHandler` | `CastCross` uses `Physics2D.CircleCast` — needs real colliders and physics simulation. |
| `BalloonBalancer` | Scan+move loop depends on well-tested `IsUnbalanced`/`OptimalNextEmptySlot` + DOTween animation. Test if it changes. |
| `BalloonSpawner` | Heavy Unity/DI coupling (`PoolManager`, `IObjectResolver`, DOTween). Little pure logic beyond forwarding. |
| `ProjectileModel` | Pure data bag — too simple to break |
| `OrthogonalSizeCameraController` | Forwards config lookup to camera — simple delegation |
| `ThrowerView.RotateTo` | Single `AngleAxis` call — too simple |
| `SceneTransition` / `Navigation` | Button handler wiring / static state machine — too simple |
| `PaintSplashView` | MonoBehaviour with `Update`-driven animation; visual correctness is a Play Mode concern. Core logic (target collection, paintability) tested via `PaintItemHandlerTests` |
| `ScoreTrailService` | Trail spawning depends on `PoolManager` + DOTween flight. Trail arrival message is tested via `ScoreControllerTests` |
| `TrailTracker<TId>` | `PauseWhere`, `ResumeAll`, and `TrackTrail` (retroactive path) call `DOPause`/`DOPlay`/`DOTween.TweensByTarget` — require live tweens. The forward `IsTracked` path is too simple (dict lookup + callback store). |
| `CinematicDirector` / `LevelUpTrailEffect` | Orchestration across DOTween, unscaled time, camera, and `ScoreTrailService` — all runtime concerns. Covered indirectly via `ScoreControllerTests` |
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


