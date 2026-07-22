@page plan_level_pacing_overhaul Level Pacing Overhaul

# Level Pacing Overhaul — unified scoring curve replacing override+formula dual path

> The level pacing system determines how many points a player needs to clear each level.
> Today it uses two separate mechanisms stitched together: hand-authored overrides for
> early levels and a logarithmic formula for everything after. This plan replaces both
> with a single designer-authored curve that smoothly covers all levels, eliminates the
> discontinuity at the boundary, and accounts for color count at every level.
>
> **No changes until this plan is actioned.**

---

## Problem Statement

The current system has a dual-path design:

1. **Levels 1–10** — `AnimationCurve` overrides (`LevelThresholdOverride`) divide a
   hand-authored value by color count.
2. **Levels 11+** — logarithmic formula `25 + e² × ln(level^(2π))` ignores color count
   entirely.

This creates five concrete problems:

| # | Issue | Impact |
|---|-------|--------|
| 1 | Discontinuity cliff at level 11 | Sudden difficulty spike or drop when exiting override coverage |
| 2 | Formula ignores color count | More colors doesn't mean easier bars — bars stay the same size |
| 3 | `CumulativeScoreForLevel` returns 0 at boundaries | First level of each override absorbs the entire milestone |
| 4 | No editor visualization | Designers can't see the difficulty curve without playing |
| 5 | Logarithmic growth too flat | Long-term engagement suffers — levels feel the same after ~20 |

---

## Solution: Single `LevelScoringCurve`

Replace override+formula with one monotone piecewise-cubic curve. Designers author
**control points** (cumulative total score at specific levels), and the system
interpolates smoothly between them. A tail formula handles levels beyond the last
control point.

### How it works (plain language)

A control point says: "by the time the player finishes level N, they should have earned
X total points across all levels so far." The system draws a smooth curve through these
points. For any given level, it reads the cumulative total at that level and at the
previous level — the difference is how many points that level requires. It then divides
by the number of colors active at that level to get the per-color bar size.

---

## Data Model

```csharp
[Serializable] internal struct LevelScoringCurve
{
    [SerializeField] private ScoringControlPoint[] _controlPoints;
    [SerializeField] private TailGrowthConfig _tailGrowth;

    internal LevelScoringCurve(ScoringControlPoint[] points, TailGrowthConfig tail);
    public float CumulativeMilestone(int level);
    public bool IsEmpty { get; }
}

[Serializable] internal struct ScoringControlPoint
{
    [Min(1)] [SerializeField] private int _level;
    [SerializeField] private float _cumulativeScore;

    public int Level { get; }
    public float CumulativeScore { get; }
    internal ScoringControlPoint(int level, float cumulativeScore);
}

[Serializable] internal struct TailGrowthConfig
{
    [SerializeField] private TailGrowthMode _mode;
    [SerializeField] private float _rate;

    public TailGrowthMode Mode { get; }
    public float Rate { get; }
    internal TailGrowthConfig(TailGrowthMode mode, float rate);
}

internal enum TailGrowthMode { Geometric, Linear }
```

All structs (not classes) — matches existing `LevelRangeEntry`/`LevelThresholdOverride`
patterns in the Configuration layer.

---

## Interpolation Domains

`CumulativeMilestone(level)` returns the total cumulative score at a given level,
evaluated across four domains:

```
level ≤ 0          → 0
level < first CP   → linear ramp from (0, 0) to first control point
level within CPs   → Fritsch-Carlson monotone cubic interpolation
level > last CP    → tail extrapolation (Geometric or Linear)
```

### Why Fritsch-Carlson

Standard cubic Hermite splines can overshoot between control points — the curve might
dip below a previous value, producing negative per-level requirements. Fritsch-Carlson
computes tangents that **guarantee monotonicity**: if the control points never decrease,
the interpolated curve never decreases either. No designer-facing tension parameter is
needed.

### Tail Extrapolation

Beyond the last control point, the curve extends using the increment between the last
two control points as its base step:

- **Geometric**: `cum[N] = cum[last] + Σ(lastIncrement × rate^i)` for each level past.
  Rate > 1 = accelerating growth.
- **Linear**: `cum[N] = cum[last] + Σ(lastIncrement + addend × i)` where addend = rate.
  Constant additive increase each level.

---

## ThresholdForLevel Flow

The revised `LevelPacingConfiguration.ThresholdForLevel(level)`:

```
1. If _scoringCurve.IsEmpty → legacy fallback (migration period only)
2. cum_this  = _scoringCurve.CumulativeMilestone(level)
3. cum_prev  = _scoringCurve.CumulativeMilestone(level - 1)
4. increment = cum_this - cum_prev
5. perColor  = increment / ColorsForLevel(level)
6. perColor  = Max(_minPerColor, perColor)        // floor prevents shrinking bars
7. return Max(1, RoundThreshold(perColor))
```

Key properties:
- More colors at a level → smaller per-bar requirement (automatic).
- `_minPerColor` floor prevents bars from visually shrinking when color count jumps.
- `ILevelPacingConfiguration` interface is **unchanged** — purely internal refactor.

---

## Editor Visualization (V1 — display-only)

A new `LevelPacingCurvePanel` section in the existing `LevelPacingWindow` (IMGUI):

- **Cumulative curve**: `Handles.DrawAAPolyLine` in a `GUILayoutUtility.GetRect` area.
- **Per-color bars**: derived bar heights overlaid below the curve.
- **Color count bands**: colored regions showing where color count changes.
- **Hover tooltip**: exact per-color threshold at any level.
- **"Compare Curves" button**: old vs new side-by-side for levels 1–50 (migration aid).

---

## Dependency Graph

```
GameLifetimeScope
    └── LevelPacingConfiguration (registered as ILevelPacingConfiguration)
            │
            ├── contains LevelScoringCurve
            │       └── uses Fritsch-Carlson algorithm + TailGrowthConfig
            │
            └── consumed by:
                    LevelDifficultyResolver → .ThresholdForLevel()
                    LevelController → ILevelThresholds.PointsRequiredForLevel()

LevelPacingWindow (Editor only)
    └── draws LevelPacingCurvePanel
```

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Author cumulative milestones, derive per-color | One curve serves all color counts; adding colors automatically lowers per-bar |
| Fritsch-Carlson over plain Hermite | Guaranteed monotonic without designer-facing parameters |
| `_minPerColor` floor | Prevents bars from shrinking when color count increases mid-game |
| Struct, not class | Matches `LevelRangeEntry`/`LevelThresholdOverride` value-type patterns |
| Difficulty spikes need 3 CPs (rise, peak, return) | Document for designers — a single high CP creates a plateau, not a spike |
| Interface unchanged | No downstream changes needed; purely internal refactor |

---

## Migration Plan

### Phase 1 — Add alongside old (non-breaking)

- Add `LevelScoringCurve`, `ScoringControlPoint`, `TailGrowthConfig`, `TailGrowthMode`
- Add `[SerializeField] private LevelScoringCurve _scoringCurve` to
  `LevelPacingConfiguration`
- Add `[SerializeField] private int _minPerColor` (default = `_thresholdRounding`)
- `ThresholdForLevel` checks `_scoringCurve.IsEmpty` → old path; else new path
- Write `[MenuItem]` migration tool converting existing overrides+formula into CPs

### Phase 2 — Validate + Editor

- Compare Curves validator (old vs new for levels 1–50)
- Tune tail growth to fix level 11 discontinuity
- Add `LevelPacingCurvePanel` to editor window
- Run full test suite (29 cases)

### Phase 3 — Remove old system

- Delete `_thresholdOverrides`, `_baseValue`, formula branch, `LevelThresholdOverride`
- `OnValidate` validates curve monotonicity only
- Update READMEs

---

## Test Plan (29 cases)

### `LevelScoringCurveTests.cs` (14 tests)

| Area | Cases |
|------|-------|
| Edge cases | level 0 → 0, single CP, empty CPs array, duplicate levels |
| Pre-first-CP | linear ramp from origin to first CP |
| Fritsch-Carlson | monotonicity, no overshoots, exact values on CPs |
| Tail geometric | grows beyond last CP at configured rate |
| Tail linear | constant additive growth |
| Tail edge | rate = 0 (flat), negative rate (clamped) |
| Property | monotonic over 200 levels for multiple curve shapes |

### `LevelPacingConfigurationTests.cs` (9 tests, extend existing)

| Area | Cases |
|------|-------|
| Fallback | empty curve → legacy path unchanged |
| Positive | non-decreasing for 50 levels |
| Division | per-color division correct |
| Floor | `_minPerColor` floor applied |
| Never invalid | never zero or negative |
| Rounding | matches existing rounding behavior |
| Level 1 | derived from first CP alone |

### `LevelPacingMigrationTests.cs` (3 tests)

- Levels 1–10: migrated curve matches old overrides within rounding tolerance
- Levels 11+: migrated curve matches old formula within tolerance
- `ColorsForLevel` unchanged by migration

### Property / Stress (3 tests)

- Monotonic for various random curve shapes
- Tail never produces negative increment
- No overflow at level 500

---

## Future: Run-Cumulative Numbering

`CumulativeMilestone(level)` is designed to serve directly as the moving threshold in a
future run-cumulative system (@ref plan_future_ideas §15). When that ships,
`LevelController` compares cumulative color progress against `CumulativeMilestone(level)`
instead of resetting each level. The curve structure doesn't change — only the
controller's comparison logic.

---

## File Structure

```
Assets/Source/
├── Configuration/Level/
│   ├── LevelPacingConfiguration.cs       (modified — adds _scoringCurve field)
│   ├── LevelScoringCurve.cs              (new)
│   ├── ScoringControlPoint.cs            (new)
│   ├── TailGrowthConfig.cs               (new)
│   └── TailGrowthMode.cs                 (new)
├── Game/Level/
│   ├── LevelController.cs                (unchanged)
│   └── LevelDifficultyResolver.cs        (unchanged)
└── Editor/Configuration/
    └── LevelPacingCurvePanel.cs          (new — editor visualization)
```

---

## Notes

- `dotnet build` can validate all new structs and logic — no shader involvement.
- The editor panel uses IMGUI (`Handles.DrawAAPolyLine`) to match the existing
  `LevelPacingWindow` style.
- Fritsch-Carlson is a well-known algorithm (~30 LOC); no third-party dependency needed.
- Removing `LevelThresholdOverride` in Phase 3 is a breaking serialization change —
  requires asset migration before that phase ships.
