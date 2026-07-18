# Score

Score-keeping only: per-color point tallies, streak multipliers, and the visual trail orbs that
fly from popped balloons to the progress bars. **Level progression — per-color progress, the
threshold, and the level-up trigger — lives in `Game/Level/` (`LevelController`/`ILevelProgress`),
not here.** `ScoreController` writes progress into `ILevelProgress` but never decides a level-up.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score)`. Two colors can share the same numeric score, so both are needed. No level component: the level-up is gated by the transition, so a trail is only ever in flight during the one level it belongs to |
| `ScoreController` | `IStartable` — score-keeping only (implements `IRunScore`, exposing `TotalScore`). Hits reach it as `HitPipeline`'s first dispatch stage (`OnActorHit` invoked directly, not bus-subscribed) so the streak tracker is guaranteed current when `Dispatch` returns. It casts the actor to `IHasScoreColor`, calls `ResolveScoreAttribution(context, attributions)`, applies the streak multiplier, then writes each color's points into `ILevelProgress.ClaimProgress` (which caps them at the level threshold) and publishes one `ScorePointsGroupMessage` per resolved color carrying the group's total `Points`. On trail arrival it only tallies lifetime totals (`_persistentScore` + `TotalScore` by `msg.Points`); confirming level progress on arrival is `LevelController`'s job. Run-scoped: totals reset via `IRunResettable.ResetRun()` (see `Game/Run/`) |
| `ColorStreakTracker` | Plain C# singleton — single source of truth for the color streak. `Record(colorId, breaksStreak)` updates state and returns the multiplier to apply. `breaksStreak = true` resets the chain and returns 1 (attribution still scores, no bonus). Auto-resets on `ScoreLevelUpMessage`. Exposed to UI consumers as `GetStreak(colorName)` via the `IColorStreak` interface |
| `ScoreTrailService` | `IStartable` + `IDisposable` + `IRunResettable` — subscribes to `ScorePointsGroupMessage`, resolves the group to an `IScoreTrailBehaviour` (by group total, via `ScoreTrailBehaviourResolver`), builds a `ScoreTrailContext`, and hands the group to the handler's `Begin`. Owns the shared infrastructure the handlers borrow: the per-color `TrailSpawner`s and prewarm, the endpoint/color lookups, the `TrailFlightRegistry<TrailId>` (exposed as `Flights`), and a pooled `IScoreTrailReporter` per group that publishes `ScoreTrailArrivedMessage` (and asserts the handler contract in dev builds). On a run reset it cancels the group-spawn `CancellationToken` so stale groups stop spawning into the next run |
| `Behaviours/` | The choreography seam — see [Trail Behaviour Seam](#trail-behaviour-seam) |

## Streak Multiplier

`ColorStreakTracker` tracks the current color streak — consecutive pops of the same color:

- First pop of a color: streak = 1 (no bonus)
- Second consecutive same-color pop: streak = 2, points doubled
- Third: streak = 3, points tripled
- Popping a different color resets to streak = 1
- `ToughBalloonModel` and `BubbleClusterModel` attributions carry `BreaksStreak = true`, and any mixed-color attribution group breaks the chain too — the streak is reset before crediting points, so scatter pops never benefit from or continue a streak
- Level-up resets the streak automatically (tracker subscribes to `ScoreLevelUpMessage`)

The balloon's `ScoreValue` is multiplied by the current streak before publishing the group's `Points`. More trails spawn, filling the progress bar faster.

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

## Trail Behaviour Seam

`Behaviours/` decouples *what a score group is worth* from *how it flies*. `ScoreTrailService` no longer
spawns trails itself: it resolves each `ScorePointsGroupMessage` to an `IScoreTrailBehaviour` through
`ScoreTrailBehaviourResolver` (highest-`MinPoints` entry the group's total clears wins, table authored in
`IScoreTrailBehaviourConfiguration`), packs the shared infrastructure into a `ScoreTrailContext` (palette
colour, endpoint, spawner, `Flights` registry, a pooled `IScoreTrailReporter`, config, a group-scoped
cancellation token), and calls `Begin`. The handler owns everything from spawn to arrival: it registers and
unregisters its own flights, and reports arrivals through the reporter with true cumulative scores that sum to the
group total. Each handler also nominates its **principal trail** via `GetPrincipalId` — the one the level-up
cinematic tracks — so the cinematic derives the tipping id from the same code (`resolver.PrincipalIdFor(msg)`)
that decides what registers, and the two can never disagree. `DefaultScore` reproduces the pre-seam pipeline
byte-for-byte (one trail per point, scatter fan, `0.02 s` stagger, one point reported per landing, first trail =
principal). `BigScore` is the confluence handler for large awards (see below).

### `BigScore` + `ShapeFormationTicker`

Once a group clears the `BigScore` `MinPoints` (authored `40`), the group flies as **one carrier + n vertex
trails** instead of one trail per point — the 5×-on-a-cluster worst case becomes one arrival, not hundreds.
`BigScoreTrailBehaviour` picks a tier (`IScoreTrailBehaviourConfiguration.BigScoreTiers`, highest `MinPoints`
the total clears), clamps the formation centre inside `WallLimits` (shifting `C` inward so `C ± BaseRadius`
stays on-screen), acquires the carrier (`TrailSpawner.Acquire`) at `C`, registers it in `Flights` under
`(Color, LastScore)` **synchronously in `Begin`** (the cinematic's registry wait depends on it), and hands a
`BigScoreFormationRequest` to `ShapeFormationTicker`. The carrier is the principal and reports **once**
(`LastScore, Points`) — one `+N` notice/slider/score step.

`ShapeFormationTicker` (`ILateTickable`, pooled zero-alloc state, `BalloonMotionTicker`-style swap-remove)
drives every formation closed-form each `LateTick`. Per repetition, n vertex trails **deploy** from the pop
origin to a regular n-gon's vertices, **draw** one chord each simultaneously (`vᵢ → v₍ᵢ₊ₖ₎`, a star polygon
{n/k}), then **collapse** to `C` (optional in-plane spin). Nesting redraws inward `m` times at `r·NestScale`
with `θ₀ += NestRotation` and radius-scaled (accelerating) durations. At the end the vertices **flash** out
(unscaled scale-to-zero) and the carrier launches to the bar on `FlyingTrail`'s normal tween flight.

**Transport bridge** — the carrier's `TrailFlight` handle is the formation's pause/snap/slow-mo interface,
polled per tick: `Paused` freezes the formation (the cinematic owns the carrier transform, vertices hold
position); `Idle` (the pan-in or a `CompleteAll` drove the principal home) **snaps** — report the whole value
now at the carrier's position, fade the live vertices out (unscaled, freeze-safe), release; `Speed` scales the
formation clock (slow-mo). The formation clock is scaled time (matches the trail tweens); the flash/snap fades
are unscaled so they survive the level-up freeze. A group-CTS cancellation (run reset) releases everything
**without** reporting — the reset zeroes the run's score anyway. A `Reported` guard (plus the reporter's own
backstop) keeps the snap and the carrier's tween arrival from double-firing.

## Spawn & Cinematic Interception

The `DefaultScore` handler spawns one trail per point in each `ScorePointsGroupMessage`, unconditionally — nothing gates spawning during cinematics. A single per-group task walks the points in order for stagger delay: the first point spawns immediately, each subsequent point after one more `ScorePointsScatterDelay`. Each flight registers in the `TrailFlightRegistry<TrailId>` (exposed as `Flights`) on spawn and unregisters on arrival.

`RegisterTarget` (called by each `ColorProgressBar` in `Start()`) also prewarms that color's `ScoreTrail_{colorName}` pool via `TrailSpawner.PrewarmAsync`, to `IGameConfiguration.ScoreTrailPrewarmPerColor` (default 64) — one `Instantiate` per frame so registering a color at level setup never spikes into a hitch. A level restart re-registering the same color is a no-op past the first call, so the pool tops up once instead of growing unboundedly.

The level-up cinematic (`LevelUpCinematic` in `Game/Cinematics/`) intercepts through that registry rather than through this service:

1. On a `ScorePointsGroupMessage` where `ILevelProgress.WillLevelUp()` is true, it records `_resolver.PrincipalIdFor(msg)` as the tipping trail and waits for that id to appear in `Flights`. The handler nominates its own principal, so the cinematic never hardcodes the numbering: for `DefaultScore` that resolves to the group's FIRST point (`msg.FirstScore`), which spawns immediately, keeping the bounded wait timeout-safe — later points can spawn seconds later under scatter stagger.
2. It then pauses that single flight (`FlyingTrail.DisableMoveTween()` + `TrailFlight.Pause()`) and puppets its position/scale along the pan-in curve while the camera follows. All other in-flight trails keep flying at normal speed so their progress-bar arrivals confirm naturally.
3. When the tipping trail reaches its bar (or its matching `ScoreTrailArrivedMessage` lands first), the cinematic calls `Flights.CompleteAll()` — every remaining in-flight trail completes instantly so all progress is confirmed before the popup opens.

## Interactions

- **`ScorePointsGroupMessage`** — published by `ScoreController` on pop (one per resolved color, carries the group's total `Points`, `LastScore`, and streak `Multiplier`), consumed by `ScoreTrailService` and `LevelUpCinematic`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival, consumed by `ScoreController` (lifetime tally), `LevelController` (progress confirmation), `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreLevelUpMessage`** — published by `LevelController` on level-up (see `Game/Level/`), consumed by `ColorProgressBar`, `LevelUpPopUp`, `LevelDifficultyResolver`, and `ColorStreakTracker` (auto-reset)
- **`Cinematics/`** — `LevelUpCinematic` intercepts the tipping trail via `ScoreTrailService.Flights` (see Spawn & Cinematic Interception above) and reads the tipping bar's world position via `ScoreTrailService.GetTarget`
- **`ColorProgressBar`** — registers itself as its colour's `ITrailEndpoint` via `ScoreTrailService.RegisterTarget` (forwarded to the shared `TrailEndpointRegistry` in `Shared/Pool`); reads progress from `ILevelProgress`; reads streak via `ColorStreakTracker.GetStreak` for streak notice display
