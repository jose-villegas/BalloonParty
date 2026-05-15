# BalloonParty — Unit Testing Plan

> Test everything that could reasonably break. Don't test what can't break on its own.

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
| Hit routing decisions | `BalloonModel.EvaluateHit` | Damage thresholds, unbreakable flag |

---

## Current Tests (Phase 1)

### `SlotGridTests` — 17 tests

Tests the core grid data structure — the most complex pure-logic class in the codebase.

| Area | Tests | What could break |
|---|---|---|
| Place guard | 1 | Double-occupation silently overwrites a balloon |
| IsEmpty bounds | 1 | Out-of-range index throws instead of returning true |
| IsUnbalanced | 4 | Even/odd row shift direction, row-0 edge case, diagonal vs direct support |
| OptimalNextEmptySlot | 5 | Weight tie-breaking (`>=`), out-of-bounds candidate, recursive weight, row-0 null |
| BottomEmptySlotPerColumn | 1 | Skipping logic returns wrong row |
| GetNeighbors | 3 | Even/odd diagonal shift direction, boundary filtering |
| IndexToWorldPosition | 2 | Staggered grid formula — even/odd offset |

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

**Total: 24 tests** — each one protecting logic that could reasonably break.

---

## Current Tests (Phase 2)

### `ScoreControllerTests` — 6 tests

Tests the scoring pipeline and level-up logic — multi-map accumulation with an all-colors threshold gate.

Setup: capture `IMessageHandler<BalloonHitMessage>` from NSubstitute subscriber; mock `IGameConfiguration`, publishers; create `GamePalette` + `PaletteEntry` via reflection. `PlayerPrefs` cleaned in SetUp/TearDown.

| Area | Tests | What could break |
|---|---|---|
| Unbreakable balloon hit (`-1`) | 1 | Wrong guard skips scoring for valid pops |
| Hit that doesn't kill | 1 | Off-by-one on survive check |
| Valid pop — per-color + total accumulation | 1 | Wrong dictionary key or sum |
| Level-up when all colors meet threshold | 1 | `Any(p < required)` — wrong comparator |
| No level-up when one color is short | 1 | Partial threshold confusion |
| Level-up resets all color progress | 1 | Missed key in reset loop |

### `BalloonModelTests` — 6 tests

Tests `EvaluateHit` — the hit-outcome decision that both `BalloonController` and `ScoreController` depend on.

Design note: `EvaluateHit` was moved from a static method on `BalloonController` to `IBalloonModel` after tests revealed the same decision logic was duplicated in `ScoreController.OnBalloonHit`. The balloon now owns its hit semantics — both consumers call `model.EvaluateHit(damage)` instead of re-implementing the check.

| Area | Tests | What could break |
|---|---|---|
| Unbreakable (`-1`) | 1 | Wrong sentinel value check |
| Unbreakable with high damage | 1 | Sentinel bypassed by large damage |
| Absorb damage (remaining > damage) | 1 | Off-by-one (`> 0` vs `>= 0`) |
| Exact kill (remaining == damage) | 1 | Boundary mishandled |
| Overkill (damage > remaining) | 1 | Negative remainder mishandled |
| Exact kill with higher values | 1 | Arithmetic error at larger numbers |

### `NudgeServiceTests` — 10 tests

Tests the 3-tier override resolution cascade (balloon → publisher → config default) and flag-based override matching.

Design note: `ResolveDistance`, `ResolveDuration`, `ResolveFalloff` changed from `private` to `internal`. `[InternalsVisibleTo]` added via `AssemblyInfo.cs`.

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

**Phase 2 total: 22 tests · Grand total: 46 tests**

---

## Future Test Targets — Medium Value (test on change)

### `BalloonsConfiguration.PickRandom` — ~4 tests

Testable now — public method on a ScriptableObject. Use `ScriptableObject.CreateInstance<BalloonsConfiguration>()` and set `_entries` via reflection.

| Area | Tests | What could break |
|---|---|---|
| All entries at max → returns `null` | 1 | Missing null guard upstream |
| `MaxCount = 0` means no limit | 1 | Wrong zero-check excludes unlimited types |
| Single candidate always selected | 1 | Edge case in cumulative sum |
| Weight distribution respected | 1 | Off-by-one in `roll < cumulative` |

### `ItemAssigner.OnItemCheck` — ~4 tests

Has real logic: turn-based filtering, max cap via `CountBalloonsWithItem`, weighted selection, `CanHoldItem` filtering. Requires reflection to set `ItemSettings` fields or extraction of logic into testable methods.

| Area | Tests | What could break |
|---|---|---|
| No eligible balloons (`CanHoldItem = false`) → no assignment | 1 | Missing null check |
| All items at max → no assignment | 1 | Cap off-by-one |
| Turn not divisible by `TurnCheckEvery` → skipped | 1 | Modulo check wrong |
| Empty `NewBalloons` → early return | 1 | Null guard missed |

---

## Future Test Targets (write when the code changes or a bug is found)

### Not ready / low value now

| System | Why defer |
|---|---|
| `BalloonBalancer` relocation | Scan+move loop depends on well-tested `IsUnbalanced`/`OptimalNextEmptySlot` + DOTween animation. Test if changes. |
| `BalloonSpawner` | Heavy Unity/DI coupling (`PoolManager`, `LifetimeScope`, DOTween). Little pure logic beyond forwarding. |

### Skip unless they grow complex

| System | Why skip |
|---|---|
| `ProjectileModel` | Pure data bag — too simple to break |
| `OrthogonalSizeCameraController` | Forwards config lookup to camera — simple delegation |
| `ThrowerView.RotateTo` | Single `AngleAxis` call — too simple |
| `SceneTransition` | Button handler wiring — too simple |
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


