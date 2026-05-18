# Score

Tracks per-color scoring, level progress, and the visual trail orbs that fly from popped balloons to the progress bars.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score, Level)`. Two colors can share the same numeric score within a level, and scores restart after level reset, so all three are needed for uniqueness |
| `ScoreController` | `IStartable` — tracks per-color level progress (confirmed on trail arrival) and projected progress (advanced immediately on pop). On balloon pop, publishes one `ScorePointMessage` per point with pre-computed `Score`, `Level`, and `NextLevel` flag — including next-level renumbering. On trail arrival, sets confirmed progress and checks for level-up via `ScoreLevelUpMessage` and `NavigationState.LevelUp` |
| `ScoreTrailService` | `IStartable` + `ICinematicAware` — subscribes to `ScorePointMessage`; spawns one pooled `ScorePointTrail` orb per message. Uses `GroupIndex`/`GroupSize` for scatter positioning and stagger delay. Supports `TrackTrail` / `ClearTrackedTrail` for external trail interception. `PauseTrailsAbove` selectively pauses next-level trails. `NextLevel` flag gates spawns during cinematics |

## Trail Identity

Each trail is identified by a `TrailId(Color, Score, Level)`:

- **Color** — the palette color name (`"Red"`, `"Blue"`, …). Required because `_projectedProgress` is per-color, so two colors can produce the same numeric score value simultaneously.
- **Score** — the level progress value this trail represents (1-based within the level). `ScoreController` advances a per-color `_projectedProgress` counter on every pop, so each trail from a multi-point balloon gets a unique sequential score.
- **Level** — the level the trail was spawned during. After level-up, progress resets to 0 and scores restart from 1. Level prevents post-reset collisions with any in-flight trails from the previous level.

## Projected vs Confirmed Progress

Two progress values exist per color:

- **`_projectedProgress`** — advances immediately on balloon pop. Used by `WillLevelUp` and trail score assignment so multi-point balloons get unique, sequential trail identities.
- **`_levelProgress`** — set to the arriving trail's score value on arrival (using `Math.Max` to prevent out-of-order decreases). Represents the highest confirmed progress for that color. Used for the level-up threshold check and persistent save state.

`WillLevelUp` checks `_projectedProgress` for **all** colors (not just the popping color). This ensures the cinematic registers even when multiple colors reach the threshold in close succession — their trails may still be in-flight but will confirm before the paused tipping trail arrives. `CheckLevelUp` uses `_levelProgress` (confirmed) for the final threshold check.

## Next-Level Trail Renumbering

When a multi-point balloon pop produces points that exceed the level-up threshold, `ScoreController` tags each post-tipping point as next-level in its `ScorePointMessage`. For example, if `requiredPoints = 10` and a pop creates raw scores `[9, 10, 11, 12]`:

- Score 9 → `ScorePointMessage(Score=9, Level=1, NextLevel=false)` — current level
- Score 10 → `ScorePointMessage(Score=10, Level=1, NextLevel=false)` — tipping trail, tracked by cinematic
- Score 11 → `ScorePointMessage(Score=1, Level=2, NextLevel=true)` — next level, renumbered
- Score 12 → `ScorePointMessage(Score=2, Level=2, NextLevel=true)` — next level, renumbered

After the level-up resets progress to 0, these next-level trails arrive with scores that correctly represent their position in the new level's progress.

## Selective Pause

When the cinematic begins, all next-level in-flight trails are paused — any trail (regardless of color) with `Level > tippingLevel`. Pre-tipping trails (any color, current level) keep flying so their progress bar arrivals complete naturally. `PauseTrailsAbove(TrailId threshold)` handles already in-flight trails. New trail spawns are gated by the `NextLevel` flag on each `ScorePointMessage`; current-level trails spawn freely even during the cinematic so that `CheckLevelUp` can confirm progress for every color.

## Spawn Synchronization

Each `ScorePointMessage` creates a spawn gate (`UniTaskCompletionSource`) that the async spawn task awaits before spawning. `LevelUpTrailEffect` releases the gate via `ReleaseSpawnGate()` after all `TrackTrail` registrations are complete. A one-frame auto-release serves as safety fallback. Multi-point pops use `GroupIndex` for stagger delay — the first point (index 0) spawns immediately, subsequent points are delayed by `GroupIndex × ScorePointsScatterDelay`.


## Interactions

- **`ScorePointMessage`** — published by `ScoreController` on pop (one per point, carries pre-computed `Score`, `Level`, `NextLevel`), consumed by `ScoreTrailService`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival (carries `Level`), consumed by `ScoreController`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreLevelUpMessage`** — published by `ScoreController` on level-up, consumed by `ColorProgressBar` and `LevelUpPopUp`
- **`Cinematics/`** — `LevelUpTrailEffect` uses `TrackTrail` to intercept the tipping trail at spawn, `PauseTrailsAbove` for selective pause, and `ResumeTrail` / `ClearTrackedTrail` for lifecycle management
- **`ColorProgressBar`** — registers target providers via `ScoreTrailService.RegisterTarget`; reads progress from `ScoreController`
