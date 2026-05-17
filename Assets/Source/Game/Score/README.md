# Score

Tracks per-color scoring, level progress, and the visual trail orbs that fly from popped balloons to the progress bars.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score, Level)`. Two colors can share the same numeric score within a level, and scores restart after level reset, so all three are needed for uniqueness |
| `ScoreController` | `IStartable` — tracks per-color level progress (confirmed on trail arrival) and projected progress (advanced immediately on pop for trail identity). On balloon pop, publishes `BalloonScoredMessage` with current projected progress and level. On trail arrival, sets confirmed progress to the trail's score value and checks for level-up; triggers level-ups via `ScoreLevelUpMessage` and `NavigationState.LevelUp` |
| `ScoreTrailService` | `IStartable` + `ICinematicAware` — subscribes to `BalloonScoredMessage`; spawns pooled `ScorePointTrail` orbs from balloon world position to per-color bar targets. Each trail is keyed by `TrailId`. Supports `TrackTrail` / `ClearTrackedTrail` API for external systems to intercept specific trails at spawn. Provides `PauseTrailsAbove` for selective pause of post-tipping trails. Gates new trail spawns during cinematics |

## Trail Identity

Each trail is identified by a `TrailId(Color, Score, Level)`:

- **Color** — the palette color name (`"Red"`, `"Blue"`, …). Required because `_projectedProgress` is per-color, so two colors can produce the same numeric score value simultaneously.
- **Score** — the level progress value this trail represents (1-based within the level). `ScoreController` advances a per-color `_projectedProgress` counter on every pop, so each trail from a multi-point balloon gets a unique sequential score.
- **Level** — the level the trail was spawned during. After level-up, progress resets to 0 and scores restart from 1. Level prevents post-reset collisions with any in-flight trails from the previous level.

## Projected vs Confirmed Progress

Two progress values exist per color:

- **`_projectedProgress`** — advances immediately on balloon pop. Used by `WillLevelUp` and trail score assignment so multi-point balloons get unique, sequential trail identities.
- **`_levelProgress`** — set to the arriving trail's score value on arrival. Represents the highest confirmed progress for that color. Used for the level-up threshold check and persistent save state.

`CheckLevelUp` uses `_levelProgress` (confirmed) for the threshold check. During the cinematic, pre-tipping trails keep flying and their arrivals push `_levelProgress` upward. The tipping trail arrives last (by score value), setting `_levelProgress` to `requiredPoints` and triggering the level-up. Post-tipping trails from the same pop are paused and resume after the cinematic.

## Next-Level Trail Renumbering

When a multi-point balloon pop produces trails that exceed the level-up threshold, `ScoreTrailService` tags those post-tipping trails as next-level trails. For example, if `requiredPoints = 10` and a pop creates scores `[9, 10, 11, 12]`:

- Score 9 → `TrailId(Red, 9, Level=1)` — current level, flies normally
- Score 10 → `TrailId(Red, 10, Level=1)` — tipping trail, tracked by cinematic
- Score 11 → `TrailId(Red, 1, Level=2)` — next level, renumbered `11 - 10 = 1`
- Score 12 → `TrailId(Red, 2, Level=2)` — next level, renumbered `12 - 10 = 2`

After the level-up resets progress to 0, these next-level trails arrive with scores that correctly represent their position in the new level's progress.

## Selective Pause

When the cinematic begins, only next-level trails are paused — trails of the **same color** with `Level > tippingLevel`. Pre-tipping trails (any color, current level) keep flying so their progress bar arrivals complete naturally. `PauseTrailsAbove(TrailId threshold)` handles this. Paused trails resume automatically on `OnCinematicEnd`. New spawns during the cinematic are gated by `Cinematic.IsPlaying` in `SpawnTrailsAsync`.

## Spawn Timing

`SpawnTrailsAsync` yields one frame before spawning the first trail. This ensures all synchronous `BalloonScoredMessage` handlers (including `LevelUpTrailEffect.OnBalloonScored` → `TrackTrail`) finish before any trail is instantiated. Without this yield, subscription ordering (`IStartable` before MonoBehaviour) would cause the trail to spawn before tracking is registered.

## Interactions

- **`BalloonScoredMessage`** — published by `ScoreController` on pop (carries `Level`), consumed by `ScoreTrailService`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival (carries `Level`), consumed by `ScoreController`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreLevelUpMessage`** — published by `ScoreController` on level-up, consumed by `ColorProgressBar` and `LevelUpPopUp`
- **`Cinematics/`** — `LevelUpTrailEffect` uses `TrackTrail` to intercept the tipping trail at spawn, `PauseTrailsAbove` for selective pause, and `ResumeTrail` / `ClearTrackedTrail` for lifecycle management
- **`ColorProgressBar`** — registers target providers via `ScoreTrailService.RegisterTarget`; reads progress from `ScoreController`
