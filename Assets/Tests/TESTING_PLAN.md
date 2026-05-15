# BalloonParty — Unit Testing Plan

> Test everything that could reasonably break. Don't test what can't break on its own.

---

## Philosophy

Based on [JUnit best practices](https://junit.org/junit4/faq.html#best):

- **"Too simple to break"** — getters, setters, simple forwarding, and auto-properties cannot break unless the compiler is broken. Don't test them.
- **Test what could reasonably break** — conditional logic, algorithms, boundary guards, math formulas, side effects with real consequences.
- **When a bug is reported** — write a failing test that exposes it, then fix it. The test prevents regression.
- **If something is hard to test** — that's a design improvement opportunity, not an excuse to skip testing.
- **Models are too simple to break** — `BalloonModel` and `ProjectileModel` are pure data bags: auto-properties and `ReactiveProperty<T>` field declarations. Testing that `Color.Value = "Red"` returns `"Red"` is testing UniRx and the C# compiler. Don't.

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
| Hit routing decisions | `BalloonController` deflect vs pop | Damage thresholds, unbreakable flag |

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

## Phase 2 — Ready to Test Now

### `ScoreControllerTests` — ~6 tests (High value)

Tests the scoring pipeline and level-up logic — multi-map accumulation with an all-colors threshold gate.

Setup: mock `ISubscriber<BalloonHitMessage>` with NSubstitute to capture the callback; mock `IGameConfiguration`, `IPublisher<BalloonScoredMessage>`, `IPublisher<ScoreLevelUpMessage>`, and `GamePalette`. `PlayerPrefs` and `Time.timeScale` work in EditMode.

| Area | Tests | What could break |
|---|---|---|
| Unbreakable balloon hit (`-1`) | 1 | Wrong guard skips scoring for valid pops |
| Hit that doesn't kill (`hitsRemaining - damage > 0`) | 1 | Off-by-one on survive check |
| Valid pop — per-color + total accumulation | 1 | Wrong dictionary key or sum |
| Level-up when all colors meet threshold | 1 | `Any(p < required)` — wrong comparator |
| Level-up resets all color progress to 0 | 1 | Missed key in reset loop |
| No level-up when one color is short | 1 | Partial threshold confusion |

### `BalloonControllerTests` — ~6 tests (High value)

Tests the hit subscription's three-branch routing — unbreakable deflect, absorb-and-deflect, and pop — plus the item branch inside `Pop()`.

Setup: mock `BalloonView`, `SlotGrid`, `PoolManager`, all publishers/subscribers with NSubstitute. Capture the `BalloonHitMessage` callback from `_hitSubscriber.Subscribe(...)`.

| Area | Tests | What could break |
|---|---|---|
| Unbreakable (`hitsRemaining == -1`) deflects | 1 | Wrong sentinel value check |
| Absorb damage (`hitsAfterDamage > 0`) deflects | 1 | Off-by-one (`> 0` vs `>= 0`) |
| Exact-kill (`hitsAfterDamage == 0`) pops | 1 | Damage arithmetic wrong |
| Overkill (`hitsAfterDamage < 0`) pops | 1 | Negative remainder mishandled |
| Pop with item — hides and waits for activation | 1 | Item branch skipped, returned to pool early |
| Pop without item — returns to pool immediately | 1 | Missing null/None check |

### `NudgeServiceTests` — ~4 tests (High value)

Tests the 3-tier override resolution cascade and the shockwave exponential falloff formula.

Setup: mock `SlotGrid`, `BalloonsConfiguration`, subscribers. Consider making `ResolveDistance`/`ResolveDuration`/`ResolveFalloff` `internal` with `[InternalsVisibleTo]` for direct testing, or test through `OnNudge`/`OnBalloonHit` via subscriber capture.

| Area | Tests | What could break |
|---|---|---|
| Balloon override present → uses it over config | 1 | Wrong priority in cascade |
| Only publisher override → uses it over config | 1 | Falls through to default incorrectly |
| No overrides → uses config default | 1 | Missing fallback |
| Shockwave exponential falloff math | 1 | Wrong sign, exponent, or distance calc |

---

## Phase 2 — Medium Value (test on change)

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
| `BalloonModel` / `ProjectileModel` | Pure data bags — too simple to break |
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


