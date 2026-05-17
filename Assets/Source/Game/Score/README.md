# Score

Tracks per-color scoring, level progress, and the visual trail orbs that fly from popped balloons to the progress bars.

## Contents

| File | What it does |
|---|---|
| `ScoreController` | `IStartable` — tracks per-color level progress (confirmed on trail arrival) and projected progress (advanced immediately on pop for trail identity). On balloon pop, publishes `BalloonScoredMessage` with current projected progress. On trail arrival, increments persistent score and level progress; triggers level-ups via `ScoreLevelUpMessage` and `NavigationState.LevelUp` |
| `ScoreTrailService` | `IStartable` + `ICinematicAware` — subscribns to `BalloonScoredMessage`; spawns pooled `ScorePointTrail` orbs from balloon world position to per-color bar targets. Each trail is keyed by its score value for identity. Supports `TrackTrail` / `ClearTrackedTrail` API for external systems to intercept specific trails at spawn. Pauses/resumes all in-flight trails on cinematic state changes. Gates new trail spawns during cinematics |

## Trail Identity

Each trail is identified by its `int score` — the level progress value it represents. Since `ScoreController` advances a single `_projectedProgress` counter on every pop (before trails spawn), each trail gets a unique score regardless of color. `GetTrailColor(score)` provides the reverse lookup when color is needed.

## Projected vs Confirmed Progress

Two progress values exist per color:

- **`_projectedProgress`** — advances immediately on balloon pop. Used by `WillLevelUp` and trail score assignment so multi-point balloons get unique, sequential trail identities.
- **`_levelProgress`** — advances when a trail arrives at its target. Used for persistent save state.

`CheckLevelUp` uses `_projectedProgress` for the threshold check so the tipping trail's single arrival triggers level-up even when earlier trails from the same multi-point pop are still paused.

## Interactions

- **`BalloonScoredMessage`** — published by `ScoreController` on pop, consumed by `ScoreTrailService`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival, consumed by `ScoreController`, `ColorProgressBar`, and `LevelUpTrailEffect`
- **`ScoreLevelUpMessage`** — published by `ScoreController` on level-up, consumed by `ColorProgressBar` and `LevelUpPopUp`
- **`Cinematics/`** — `LevelUpTrailEffect` uses `TrackTrail` to intercept the tipping trail at spawn and begin the cinematic
- **`ColorProgressBar`** — registers target providers via `ScoreTrailService.RegisterTarget`; reads progress from `ScoreController`

