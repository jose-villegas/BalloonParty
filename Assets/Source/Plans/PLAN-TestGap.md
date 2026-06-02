@page plan_test_gap Test Gap — Pre-Bush Audit

# Test Gap — Pre-Bush Audit

> Test coverage gaps identified June 2 2026. Last test addition was May 23.
> Separated into two groups: general gaps to address **before** the Bush plan,
> and Bush-plan-specific gaps to address **during** the Bush plan.
>
> All proposed tests follow `Assets/Tests/README.md`:
> - **"Too simple to break"** tests are excluded (auto-properties, constructor stores,
>   simple forwarding, interface forwarding, ReactiveProperty get/set)
> - **Real objects over mocks** — real `BubbleClusterModel`, `ColorStreakTracker`, etc.
>   NSubstitute only for `IGamePalette`, `IPublisher<T>`, `ISubscriber<T>`
> - **Deterministic over random** — single-candidate scenarios eliminate `Random.Range`;
>   for `ClusterSlotSelectionStrategy`, test structural invariants (count, membership,
>   adjacency) not specific slot choices
> - **EditMode only** — pure C#, no Play Mode

---

## General test gaps (address before Bush)

### High priority

#### `BubbleClusterModelTests.cs`

`BubbleClusterModel` has conditional logic in `ResolveScoreAttribution` (loop count
tied to `HitsRemaining`, `breaksStreak` flag, empty palette guard) and inherits
`EvaluateHit` from `BalloonModelBase`. These are "algorithms" and "conditional
branching" per the guidelines.

Real `BubbleClusterModel` constructed with `BalloonModelConfig` + `NSubstitute.For<IGamePalette>()`.

```
BubbleClusterModel_IsIHasDurability
BubbleClusterModel_IsIHasScoreColor
BubbleClusterModel_EvaluateHit_Survives_ReturnsPassThrough
BubbleClusterModel_EvaluateHit_KillingBlow_ReturnsPop
BubbleClusterModel_ResolveScoreAttribution_EntryCountEqualsHitsRemainingPlusOne
BubbleClusterModel_ResolveScoreAttribution_AllEntriesHaveBreaksStreak
BubbleClusterModel_ResolveScoreAttribution_EachEntryScoresOnePoint
BubbleClusterModel_ResolveScoreAttribution_EmptyPalette_AddsNothing
```

Why each test matters:
- Interface conformance (`is IHasDurability`, `is IHasScoreColor`) — structural contract
  that `ScoreController` and spawner depend on
- `EvaluateHit` — inherited template method; `PassThrough` on survival is the expected
  outcome (not `Deflect` like Tough)
- Attribution count — loop uses `HitsRemaining.Value + 1` which could break on off-by-one
- `BreaksStreak` flag — every attribution must have it; missing it changes streak behaviour
- Empty palette guard — `colors.Count == 0` early return prevents `IndexOutOfRange`

#### `ColorStreakTrackerTests.cs`

`ColorStreakTracker.Record()` is a **streak state machine** — same pattern as
`ScoreController.GetStreak` which the README explicitly lists under "What TO Test".
The `breaksStreak` branch, colour-change reset, and multiplier return are all
conditional logic that could reasonably break.

Real `ColorStreakTracker` with `NSubstitute.For<ISubscriber<ScoreLevelUpMessage>>()`.

```
ColorStreakTracker_Record_FirstPop_ReturnsOne
ColorStreakTracker_Record_ConsecutiveSameColor_IncrementsStreak
ColorStreakTracker_Record_DifferentColor_ResetsToOne
ColorStreakTracker_Record_BreaksStreak_ResetsAndReturnsOne
ColorStreakTracker_Record_AfterBreak_NextSameColor_StartsAtOne
ColorStreakTracker_GetStreak_MatchingColor_ReturnsCurrentStreak
ColorStreakTracker_GetStreak_NonMatchingColor_ReturnsZero
```

#### `ClusterSlotSelectionStrategyTests.cs`

Complex algorithm — greedy hex-neighbor expansion, `maxPerCluster` cap, farthest-seed
bias. This is "Algorithms" and "Boundary/range checks" per the guidelines.

Real `ClusterSlotSelectionStrategy`. Tests verify **structural invariants**, not
specific slot positions (randomness makes positional assertions non-deterministic).

```
ClusterSlotSelection_EmptySlots_ReturnsEmpty
ClusterSlotSelection_CountZero_ReturnsEmpty
ClusterSlotSelection_SingleSlotAvailable_ReturnsThatSlot
ClusterSlotSelection_ResultCount_DoesNotExceedRequestedCount
ClusterSlotSelection_ResultCount_DoesNotExceedAvailableSlots
ClusterSlotSelection_AllResultsAreFromAvailableSet
ClusterSlotSelection_MaxPerCluster_NoClusterExceedsLimit
ClusterSlotSelection_SelectedSlotsAreHexAdjacent_WithinCluster
```

Why these and not positional tests: `PickRandom` and `PickFarthestSeed` use
`Random.Range` internally. The guideline says "deterministic over random — use
single-candidate scenarios" for weighted selection. For the cluster algorithm, we
can't eliminate all randomness — so we test invariants that must hold regardless of
random choices.

#### `WeightedPickTests.cs`

`WeightedPickExtensions.PickRandom` is **weighted selection with caps** — explicitly
listed under "What TO Test". Same pattern as `BalloonsConfigurationTests` which already
exists.

Uses a minimal `TestWeightedEntry : IWeightedEntry` stub (same approach as
`BalloonsConfigurationTests`).

```
WeightedPick_SingleEntry_AlwaysReturnsIt
WeightedPick_AllEntriesAtMax_ReturnsNull
WeightedPick_CappedEntry_IsExcluded_UncappedSelected
WeightedPick_ZeroMaxCount_NeverCapped
```

Deterministic: single-candidate scenarios eliminate `Random.Range` non-determinism.

#### `PauseServiceTests.cs`

Reference-counted pause/resume with nested source tracking. The nesting logic is
"conditional branching with side effects" — resuming one source while another is
still paused must not unpause the game.

Real `PauseService` with `NSubstitute.For<IPublisher<PausedMessage>>()` and
`NSubstitute.For<IPublisher<ResumedMessage>>()`.

```
PauseService_InitialState_IsNotPaused
PauseService_Pause_SetsIsAnyPausedTrue
PauseService_Pause_PublishesPausedMessage
PauseService_Resume_AfterSinglePause_SetsIsAnyPausedFalse
PauseService_Resume_PublishesResumedMessage
PauseService_NestedPause_SameSource_StaysPausedUntilAllResumed
PauseService_Resume_WithoutPriorPause_DoesNothing
PauseService_MultipleSources_OneResumed_StillPaused
```

### Medium priority

#### `VectorMathHelperTests.cs`

Pure **math formulas** — `Centroid` and `BoundingRadius`. Explicitly listed under
"What TO Test".

```
VectorMathHelper_Centroid_ReturnsArithmeticMean
VectorMathHelper_BoundingRadius_ReturnsMaxDistance
VectorMathHelper_BoundingRadius_AllSamePoint_ReturnsZero
```

Excluded: `Centroid_SinglePoint_ReturnsThatPoint` — "too simple to break" (divides by 1).

#### `PathHelperTests.cs`

Pure **math formulas** and **algorithms** — Catmull-Rom interpolation, midpoint
displacement, prefix sum.

```
PathHelper_CatmullRomPath_StartsAtFirstWaypoint
PathHelper_CatmullRomPath_EndsAtLastWaypoint
PathHelper_CatmullRomPath_SinglePoint_ReturnsSinglePoint
PathHelper_CatmullRomPath_TwoPoints_ReturnsCorrectSubdivisionCount
PathHelper_CatmullRomLoop_EndsAtFirstWaypoint
PathHelper_SampleAt_IntegerIndex_ReturnsExactValue
PathHelper_SampleAt_FractionalIndex_Interpolates
PathHelper_SampleAt_BeyondBounds_Clamps
PathHelper_PrefixSum_CorrectCumulativeValues
PathHelper_PrefixSum_FirstElementIsZero
PathHelper_MidpointDisplacement_PreservesEndpoints
PathHelper_MidpointDisplacement_CountTwoOrLess_OnlyEndpoints
```

### Low priority (defer)

| File | Reason to defer |
|---|---|
| `DisturbanceFieldService.cs` | Heavy Unity dependency (`RenderTexture`, `Graphics.Blit`, `Shader`). Per guidelines: "deferred systems too coupled to Unity runtime". |
| `ItemActivator.cs` | Coordinates item handlers via MessagePipe. Full DI mock setup for low ROI. |
| `NudgeOverrideResolver.cs` | Already has 10 tests. Modified in 8.2c nudge decoupling but existing tests should still cover — verify, don't rewrite. |

---

## Bush-plan-specific test gaps (address during Bush plan)

| Phase | Tests | Notes |
|---|---|---|
| Phase 1 (Cluster infrastructure) | Migrate existing `PuffClusterRegistryTests` to `SlotClusterRegistryTests<PuffObstacleModel>`. Add: `Registry_SetupOnly_DoesNotSubscribeToGridChanges`, `Registry_SetupOnly_RebuildAllStillRuns` | Tests the `setupOnly` flag — conditional logic that could break. |
| Phase 3 (Bush C#) | `BushObstacleModel_IsIClusterableSlotActor` in `StructuralActorTests` | Interface conformance — structural contract for cluster registry. |
| Phase 4 (Disturbance) | Defer `BushDisturbanceController` — depends on `DisturbanceFieldService` (Unity runtime) and projectile position stream. Same deferral rationale as `DisturbanceFieldService` itself. |

---

## Execution order

```
1. [x] BubbleClusterModelTests          — 8 tests (June 2 2026)
2. [x] ColorStreakTrackerTests           — 7 tests (June 2 2026)
3. [x] WeightedPickTests                — 4 tests (June 2 2026)
4. [x] ClusterSlotSelectionStrategyTests — 8 tests (June 2 2026)
5. [x] PauseServiceTests                — 8 tests (June 2 2026)
6. [x] VectorMathHelperTests            — 3 tests (June 2 2026)
7. [x] PathHelperTests                  — 12 tests (June 2 2026)
--- Bush plan begins ---
8. [ ] SlotClusterRegistryTests (Phase 1 — migrate + setupOnly)
9. [ ] StructuralActorTests additions (Phase 3 — IClusterableSlotActor)
```

---

## Tests README update

After implementing the above, update `Assets/Tests/README.md`:
- Add fixture descriptions to the "Current Coverage" section
- Update the test count (currently 133)
- Add `BubbleClusterModel`, `ColorStreakTracker`, `WeightedPickExtensions`,
  `ClusterSlotSelectionStrategy`, `PauseService`, `VectorMathHelper`, `PathHelper`
  to the coverage table
