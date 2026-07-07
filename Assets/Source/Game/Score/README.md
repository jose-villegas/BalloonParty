# Score

Score-keeping only: per-color point tallies, streak multipliers, and the visual trail orbs that
fly from popped balloons to the progress bars. **Level progression — per-color progress, the
threshold, and the level-up trigger — lives in `Game/Level/` (`LevelController`/`ILevelProgress`),
not here.** `ScoreController` writes progress into `ILevelProgress` but never decides a level-up.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score)`. Provides a convenience constructor from `ScorePointMessage`. Two colors can share the same numeric score, so both are needed. No level component: the level-up is gated by the transition, so a trail is only ever in flight during the one level it belongs to |
| `ScoreController` | `IStartable` — score-keeping only (implements `IRunScore`, exposing `TotalScore`). Hits reach it as `HitPipeline`'s first dispatch stage (`OnActorHit` invoked directly, not bus-subscribed) so the streak tracker is guaranteed current when `Dispatch` returns. It casts the actor to `IHasScoreColor`, calls `ResolveScoreAttribution(context, attributions)`, applies the streak multiplier, then writes each color's points into `ILevelProgress.ClaimProgress` (which caps them at the level threshold) and publishes the granted points as one scatter group sharing `GroupSize`. On trail arrival it only tallies lifetime totals (`_persistentScore` + `TotalScore`); confirming level progress on arrival is `LevelController`'s job. Run-scoped: totals reset via `IRunResettable.ResetRun()` (see `Game/Run/`) |
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

Each trail is identified by a `TrailId(Color, Score)`:

- **Color** — the palette color name (`"Red"`, `"Blue"`, …). Required because progress is per-color, so two colors can produce the same numeric score value simultaneously.
- **Score** — the level progress value this trail represents (1-based within the level). Every pop advances the color's *projected* progress via `ILevelProgress.ClaimProgress`, so each trail from a multi-point balloon gets a unique sequential score.

No level component is needed: the level-up is gated by the transition, so a trail is only ever in flight during the single level it belongs to — `(Color, Score)` never collides across levels.

## Progress lives in `Game/Level/`

`ScoreController` does not track level progress. It computes granted points (attribution × streak,
capped) and calls `ILevelProgress.ClaimProgress`; `LevelController` owns the projected-vs-confirmed
progress, the threshold cap, the post-level-up straggler suppression (via `LevelUpPhase`, not a
latch), `WillLevelUp`/`GetProgress`/`GetRequiredPoints`, and the `ScoreLevelUpMessage` trigger. See
`Game/Level/README.md` for the two-phase commit and how a trail arrival confirms progress.

## Spawn & Cinematic Interception

`ScoreTrailService` spawns one trail per `ScorePointMessage`, unconditionally — nothing gates spawning during cinematics. Multi-point pops use `GroupIndex` for stagger delay: the first point (index 0) spawns immediately, subsequent points are delayed by `GroupIndex × ScorePointsScatterDelay`. Each flight registers in the `TrailFlightRegistry<TrailId>` (exposed as `Flights`) on spawn and unregisters on arrival.

The level-up cinematic (`LevelUpCinematic` in `Game/Cinematics/`) intercepts through that registry rather than through this service:

1. On a `ScorePointMessage` where `ILevelProgress.WillLevelUp()` is true, it records `new TrailId(msg)` as the tipping trail and waits (`UniTask.WaitUntil`) for that id to appear in `Flights` — this covers delayed `groupIndex > 0` spawns as well as already-registered ones.
2. It then pauses that single flight (`FlyingTrail.DisableMoveTween()` + `TrailFlight.Pause()`) and puppets its position/scale along the pan-in curve while the camera follows. All other in-flight trails keep flying at normal speed so their progress-bar arrivals confirm naturally.
3. When the tipping trail reaches its bar (or its matching `ScoreTrailArrivedMessage` lands first), the cinematic calls `Flights.CompleteAll()` — every remaining in-flight trail completes instantly so all progress is confirmed before the popup opens.

## Interactions

- **`ScorePointMessage`** — published by `ScoreController` on pop (one per point × streak, carries pre-computed `Score`, `GroupSize`/`GroupIndex`), consumed by `ScoreTrailService`, `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival, consumed by `ScoreController` (lifetime tally), `LevelController` (progress confirmation), `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreLevelUpMessage`** — published by `LevelController` on level-up (see `Game/Level/`), consumed by `ColorProgressBar`, `LevelUpPopUp`, `LevelDifficultyResolver`, and `ColorStreakTracker` (auto-reset)
- **`Cinematics/`** — `LevelUpCinematic` intercepts the tipping trail via `ScoreTrailService.Flights` (see Spawn & Cinematic Interception above) and reads the tipping bar's world position via `ScoreTrailService.GetTarget`
- **`ColorProgressBar`** — registers itself as its colour's `ITrailEndpoint` via `ScoreTrailService.RegisterTarget` (forwarded to the shared `TrailEndpointRegistry` in `Shared/Pool`); reads progress from `ILevelProgress`; reads streak via `ColorStreakTracker.GetStreak` for streak notice display
