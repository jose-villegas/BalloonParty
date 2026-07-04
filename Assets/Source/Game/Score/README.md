# Score

Tracks per-color scoring, level progress, streak multipliers, and the visual trail orbs that fly from popped balloons to the progress bars.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score, Level)`. Provides a convenience constructor from `ScorePointMessage`. Two colors can share the same numeric score within a level, and scores restart after level reset, so all three are needed for uniqueness |
| `ScoreController` | `IStartable` — tracks per-color level progress (confirmed on trail arrival), projected progress (advanced immediately on pop). Hits reach it as `HitPipeline`'s first dispatch stage (`OnActorHit` invoked directly, not bus-subscribed) so the streak tracker is guaranteed current when `Dispatch` returns. It casts the actor to `IHasScoreColor` and calls `ResolveScoreAttribution(context, attributions)` — all returned `ScoreAttribution` entries are resolved and published together as one scatter group sharing `GroupSize`. On trail arrival, sets confirmed progress and checks for level-up via `ScoreLevelUpMessage` and `NavigationState.LevelUp` — suppressed once the run is over or the loss is already certain (`ILossForecast.LossImminent`). Run-scoped: level/score start at 1/0 every session (no cross-session persistence) and reset via `IRunResettable.ResetRun()` on restart (see `Game/Run/`). Exposes `IRunScore` (final level/score for the run commit) and `IScoreQuery` (`GetProgress`/`GetRequiredPoints`/`WillLevelUp`) |
| `ColorStreakTracker` | Plain C# singleton — single source of truth for the color streak. `Record(colorId, breaksStreak)` updates state and returns the multiplier to apply. `breaksStreak = true` resets the chain and returns 1 (attribution still scores, no bonus). Auto-resets on `ScoreLevelUpMessage`. Exposed to UI consumers as `GetStreak(colorName)` via the `IColorStreak` interface |
| `ScoreTrailService` | `IStartable` + `IDisposable` — subscribes to `ScorePointMessage`; spawns one pooled `FlyingTrail` orb per message, unconditionally. Composes `TrailFlightRegistry<TrailId>` (exposed as `Flights`) so the cinematic can look up, pause, and complete in-flight trails by id. Uses `GroupIndex`/`GroupSize` for scatter positioning and stagger delay |

## Streak Multiplier

`ColorStreakTracker` tracks the current color streak — consecutive pops of the same color:

- First pop of a color: streak = 1 (no bonus)
- Second consecutive same-color pop: streak = 2, points doubled
- Third: streak = 3, points tripled
- Popping a different color resets to streak = 1
- `ToughBalloonModel` and `BubbleClusterModel` attributions carry `BreaksStreak = true`, and any mixed-color attribution group breaks the chain too — the streak is reset before crediting points, so scatter pops never benefit from or continue a streak
- Level-up resets the streak automatically (tracker subscribes to `ScoreLevelUpMessage`)

The balloon's `ScoreValue` is multiplied by the current streak before publishing `ScorePointMessage`s. More trails spawn, filling the progress bar faster.

`GetStreak(string colorName)` on `ColorStreakTracker` exposes the current streak for a color so views can display a streak notice.

## Trail Identity

Each trail is identified by a `TrailId(Color, Score, Level)`:

- **Color** — the palette color name (`"Red"`, `"Blue"`, …). Required because `_projectedProgress` is per-color, so two colors can produce the same numeric score value simultaneously.
- **Score** — the level progress value this trail represents (1-based within the level). `ScoreController` advances a per-color `_projectedProgress` counter on every pop, so each trail from a multi-point balloon gets a unique sequential score.
- **Level** — the level the trail was spawned during. After level-up, progress resets to 0 and scores restart from 1. Level prevents post-reset collisions with any in-flight trails from the previous level.

## Projected vs Confirmed Progress

Two progress values exist per color:

- **`_projectedProgress`** — advances immediately on balloon pop. Used by `WillLevelUp` and trail score assignment so multi-point balloons get unique, sequential trail identities.
- **`_levelProgress`** — set to the arriving trail's score value on arrival (using `Math.Max` to prevent out-of-order decreases). Represents the highest confirmed progress for that color. Used for the level-up threshold check.

`WillLevelUp` checks `_projectedProgress` for **all** colors (not just the popping color). This ensures the cinematic registers even when multiple colors reach the threshold in close succession — their trails may still be in-flight but will confirm before the paused tipping trail arrives. `CheckLevelUp` uses `_levelProgress` (confirmed) for the final threshold check.

## Next-Level Trail Renumbering

When a multi-point balloon pop produces points that exceed the level-up threshold, `ScoreController` renumbers each post-tipping point into the next level. For example, if `requiredPoints = 10` and a pop creates raw scores `[9, 10, 11, 12]`:

- Score 9 → `ScorePointMessage(Score=9, Level=1)` — current level
- Score 10 → `ScorePointMessage(Score=10, Level=1)` — tipping trail, tracked by the cinematic
- Score 11 → `ScorePointMessage(Score=1, Level=2)` — next level, renumbered
- Score 12 → `ScorePointMessage(Score=2, Level=2)` — next level, renumbered

After the level-up resets progress to 0, these next-level trails arrive with scores that correctly represent their position in the new level's progress.

## Spawn & Cinematic Interception

`ScoreTrailService` spawns one trail per `ScorePointMessage`, unconditionally — nothing gates spawning during cinematics. Multi-point pops use `GroupIndex` for stagger delay: the first point (index 0) spawns immediately, subsequent points are delayed by `GroupIndex × ScorePointsScatterDelay`. Each flight registers in the `TrailFlightRegistry<TrailId>` (exposed as `Flights`) on spawn and unregisters on arrival.

The level-up cinematic (`LevelUpCinematic` in `Game/Cinematics/`) intercepts through that registry rather than through this service:

1. On a `ScorePointMessage` where `ScoreController.WillLevelUp()` is true, it records `new TrailId(msg)` as the tipping trail and waits (`UniTask.WaitUntil`) for that id to appear in `Flights` — this covers delayed `groupIndex > 0` spawns as well as already-registered ones.
2. It then pauses that single flight (`FlyingTrail.DisableMoveTween()` + `TrailFlight.Pause()`) and puppets its position/scale along the pan-in curve while the camera follows. All other in-flight trails keep flying at normal speed so their progress-bar arrivals confirm naturally.
3. When the tipping trail reaches its bar (or its matching `ScoreTrailArrivedMessage` lands first), the cinematic calls `Flights.CompleteAll()` — every remaining in-flight trail (including renumbered next-level ones) completes instantly so all progress is confirmed before the popup opens.

## Interactions

- **`ScorePointMessage`** — published by `ScoreController` on pop (one per point × streak, carries pre-computed `Score`, `Level`, `GroupSize`/`GroupIndex`), consumed by `ScoreTrailService`, `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival (carries `Level`), consumed by `ScoreController`, `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreLevelUpMessage`** — published by `ScoreController` on level-up, consumed by `ColorProgressBar`, `LevelUpPopUp`, and `ColorStreakTracker` (auto-reset)
- **`Cinematics/`** — `LevelUpCinematic` intercepts the tipping trail via `ScoreTrailService.Flights` (see Spawn & Cinematic Interception above) and reads the tipping bar's world position via `ScoreTrailService.GetTarget`
- **`ColorProgressBar`** — registers itself as its colour's `ITrailEndpoint` via `ScoreTrailService.RegisterTarget` (forwarded to the shared `TrailEndpointRegistry` in `Shared/Pool`); reads progress from `ScoreController`; reads streak via `ColorStreakTracker.GetStreak` for streak notice display
