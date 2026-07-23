# Score

Score-keeping only: per-color point tallies, streak multipliers, and the visual trail orbs that
fly from popped balloons to the progress bars. **Level progression — per-color progress, the
threshold, and the level-up trigger — lives in `Game/Level/` (`LevelController`/`ILevelProgress`),
not here.** `ScoreController` writes progress into `ILevelProgress` but never decides a level-up.

## Contents

| File | What it does |
|---|---|
| `TrailId` | Readonly struct — uniquely identifies a score trail by `(Color, Score)`. Two colors can share the same numeric score, so both are needed. No level component: the level-up is gated by the transition, so a trail is only ever in flight during the one level it belongs to |
| `ScoreController` | `IStartable` — score-keeping only (implements `IRunScore`, exposing `TotalScore`). Hits reach it as `HitPipeline`'s first dispatch stage (`OnActorHit` invoked directly, not bus-subscribed) so the streak tracker is guaranteed current when `Dispatch` returns. It casts the actor to `IHasScoreColor`, calls `ResolveScoreAttribution(in context, incompleteColors, attributions)`, applies the streak multiplier, then writes each color's points into `ILevelProgress.ClaimProgress` (which caps them at the level threshold) and publishes one `ScorePointsGroupMessage` per resolved color carrying the group's total `Points`. Granted points also lead a `_projectedTotal` counter (summed at publish time, ahead of what's actually banked); on trail arrival it tallies lifetime totals (`_persistentScore` + `TotalScore` by `msg.Points`) — confirming level progress on arrival is `LevelController`'s job. On level-up it snaps `TotalScore` to `_projectedTotal` (so the popup never shows a stale low number while the last trails sit frozen) and records the gap as `_snapCredit`, which the frozen survivors' later arrivals absorb instead of double-counting. Run-scoped: totals reset via `IRunResettable.ResetRun()` (see `Game/Run/`) |
| `ColorStreakTracker` | Plain C# singleton — single source of truth for the color streak. `Record(colorId, breaksStreak)` updates state and returns the multiplier to apply. `breaksStreak = true` resets the chain and returns 1 (attribution still scores, no bonus). Auto-resets on `ScoreLevelUpMessage` and on `ProjectileLoadedMessage` (a streak never carries across turns). Exposed to UI consumers as `GetStreak(colorName)` via the `IColorStreak` interface |
| `ScoreTrailService` | `IStartable` + `IDisposable` + `IRunResettable` — subscribes to `ScorePointsGroupMessage`, resolves the group to an `IScoreTrailBehaviour` (by group total, via `ScoreTrailBehaviourResolver`), builds a `ScoreTrailContext`, and hands the group to the handler's `Begin`. Owns the shared infrastructure the handlers borrow: the per-color `TrailSpawner`s and prewarm, the endpoint/color lookups, the `TrailFlightRegistry<TrailId>` (exposed as `Flights`), and a fresh (not pooled) `IScoreTrailReporter` per group that publishes `ScoreTrailArrivedMessage` (and asserts the handler contract in dev builds). On a run reset it cancels the group-spawn `CancellationToken` so stale groups stop spawning into the next run |
| `Behaviours/` | The choreography seam — see [Trail Behaviour Seam](#trail-behaviour-seam) |

## Streak Multiplier

`ColorStreakTracker` tracks the current color streak — consecutive pops of the same color:

- First pop of a color: streak = 1 (no bonus)
- Second consecutive same-color pop: streak = 2, points doubled
- Third: streak = 3, points tripled
- Popping a different color resets to streak = 1
- `ToughBalloonModel` and `BubbleClusterModel` attributions carry `BreaksStreak = true`, and any mixed-color attribution group breaks the chain too — the streak is reset before crediting points, so scatter pops never benefit from or continue a streak
- Level-up resets the streak automatically (tracker subscribes to `ScoreLevelUpMessage`), and so does a fresh shot (`ProjectileLoadedMessage`) — a streak never carries across turns

The balloon's `ScoreValue` is multiplied by the current streak before publishing the group's `Points`. More trails spawn, filling the progress bar faster.

`GetStreak(string colorName)` on `ColorStreakTracker` exposes the current streak for a color so views can display a streak notice.

## Trail Identity

Each trail is identified by a `TrailId(Color, Score)`:

- **Color** — the palette color name (`"Red"`, `"Blue"`, …). Required because progress is per-color, so two colors can produce the same numeric score value simultaneously.
- **Score** — the level progress value this trail represents (1-based within the level). Every pop advances the color's *projected* progress via `ILevelProgress.ClaimProgress`, so each trail from a multi-point balloon gets a unique sequential score.

No level component is needed: the level-up is gated by the transition, so a trail is only ever in flight during the single level it belongs to — `(Color, Score)` never collides across levels.

## Progress lives in `Game/Level/`

`ScoreController` does not track level progress. It computes granted points (\f$\text{attribution} \times \text{streak}\f$,
capped) and calls `ILevelProgress.ClaimProgress`; `LevelController` owns the projected-vs-confirmed
progress, the threshold cap, the post-level-up straggler suppression (via `LevelUpPhase`, not a
latch), `WillLevelUp`/`GetProgress`/`GetRequiredPoints`, and the `ScoreLevelUpMessage` trigger. See
`Game/Level/README.md` for the two-phase commit and how a trail arrival confirms progress.

## Trail Behaviour Seam

`Behaviours/` decouples *what a score group is worth* from *how it flies*. `ScoreTrailService` no longer
spawns trails itself: it resolves each `ScorePointsGroupMessage` to an `IScoreTrailBehaviour` through
`ScoreTrailBehaviourResolver` (highest-`MinPoints` entry the group's total clears wins, table authored in
`IScoreTrailBehaviourConfiguration`), packs the shared infrastructure into a `ScoreTrailContext` (palette
colour, endpoint, spawner, `Flights` registry, a fresh (not pooled) `IScoreTrailReporter`, config, a group-scoped
cancellation token), and calls `Begin`. The handler owns everything from spawn to arrival: it registers and
unregisters its own flights, and reports arrivals through the reporter with true cumulative scores that sum to the
group total. Each handler also nominates its **principal trail** via `GetPrincipalId` — the one the level-up
cinematic tracks — so the cinematic derives the tipping id from the same code (`resolver.PrincipalIdFor(msg)`)
that decides what registers, and the two can never disagree. `DefaultScore` reproduces the pre-seam pipeline
byte-for-byte (one trail per point, scatter fan, `0.02 s` stagger, one point reported per landing, first trail =
principal). `BigScore` is the confluence handler for any group past that floor (see below).

### `BigScore` + `ShapeFormationTicker` + `ShapeCatalog`

When a pop scores 2+ points, the system visualises it as spinning 3D wireframe shapes that fly toward the
score bar — rather than spawning hundreds of individual "+1" trails. Each shape is drawn by **pens**: small
glowing dots that orbit along the shape's edges, leaving fading ribbon trails behind them (like sparklers
traced in the dark). The path a pen follows is a **walk** — a closed loop of edges it repeats forever. When
a shape reaches the bar, it **arrives** and banks its points.

The score **decomposes** into shapes like making change with coins: 47 points might become a 30-shape + a
12-shape + a 5-shape, all launched simultaneously from the pop position. The `DefaultScore` handler (simple
single-trail fly-in) only fires for 1-point groups.

`ShapeCatalog` (`internal static`, built once, zero-alloc lookup) is hand-authored 3D shape data. A denomination
maps to a shape whose vertex count equals it: `2` line, `3` triangle, `4` tetrahedron, `5` square pyramid, `6`
triangular prism, `7` pentagonal dipyramid (J13), `8` cube, `9` triangular cupola, `10` elongated square
dipyramid (J15), `12` hexagonal prism, `15` pentagonal cupola, `20` dodecahedron, `30` a ball of six 5-point
outline stars (pens trace each star's silhouette without crossing the interior; five pens per star),
`50` parabidiminished rhombicosidodecahedron (J80, face-walked), and `100` a waving sphere (sine-wave vertex
displacement gives it an organic pulsing look) — the upper tiers favour a readable **silhouette** over vertex
density. Every shape partitions its edges into closed **walks** a pen orbits forever — face-tracing loops (the
pen goes around each flat face) for the Johnson solids, one outline per star, latitude/longitude rings for the
sphere; some walks trace curves (spherical arcs), others trace straight edges. Each shape also carries a
`SpinScale` (complex shapes tumble more slowly for readability), per-shape `DisplacementScale`/
`DisplacementSpeed` multipliers for animated shapes, and an optional hit-aligned start (the line tilts to match
the shot direction). `Denominations` is the decomposition **ladder**
`{100, 50, 30, 20, 15, 12, 10, 9, 8, 7, 6, 5, 4, 3, 2}`. `BigScoreTrailBehaviour.Decompose`
is an optimal coin-change split over that ladder (fewest pieces, largest-first). With both `2` and `3` on the
ladder, every total decomposes remainder-free — `AssertNoRemainder` guards this invariant.

`BigScoreTrailBehaviour.Begin` decomposes `context.Points`, fits every shape's radius inside `WallLimits`, spreads
the formations' sub-centres around the pop (golden-angle phyllotaxis, each wall-clamped), and launches them
simultaneously. The **largest** formation is the **principal**: its sub-centre is the pop, it takes the top score
range (carrying `LastScore`), and `ShapeFormationTicker` registers **its** bare pooled **anchor Transform** in
`Flights` under `(Color, LastScore)` **synchronously in `Begin`** (the cinematic's registry wait depends on it).
Every formation reports **once** — `(itsRangeLast, itsValue)` — so the reports sum to the group total.

`ShapeFormationTicker` (`ILateTickable`, pooled zero-alloc state + groups + anchors, `BalloonMotionTicker`-style
swap-remove) drives every formation closed-form each `LateTick`. A formation lives **one Travel phase**: with the
shape scale driven by the settings' `ScaleOverTravel` curve (its last key time is the duration `D`), a pen's world
position is \f$C(t) + Q(t) \cdot \big(\text{radius} \cdot \text{scale}(t) \cdot \text{local}_p(t)\big)\f$:

- \f$C(t) = \mathrm{Lerp}\big(\text{origin}, \text{liveTarget}, \mathrm{SmoothStep}(t/D)\big)\f$ — blooms at the sub-centre, travels to the bar. `liveTarget`
  re-reads the endpoint centre every tick (plus a launch-sampled offset), so a drifting UI bar can never leave the
  landing stale (the `SetupFollow` moving-target principle).
- `scale(t)` — the curve: `0 → bloom → hold → 0`, so the shape grows from a point and tapers back to one at the
  bar. Pens are **pen-down from t = 0** (no deploy phase, no deploy spokes).
- `Q(t)` — a fixed random tilt spun from t = 0, about the **projectile hit direction**
  (`Cross(Vector3.back, HitDirection)`), so the whole constellation rolls head-over-heels like momentum from the
  shot (all formations share the axis; a directionless pop falls back to a per-shape random axis).
- \f$\text{local}_p(t)\f$ — the pen's position on its closed walk, orbiting continuously at `PenSpeed` **world units/second**
  (parameterized by arc length): the first lap draws the wireframe, later laps re-ink it; `k` pens tile a
  period-`P` walk in \f$P/k\f$. `Coverage` (a style dial, Range 0.2–2) sets each pen's ribbon time — \f$\ge 1\f$ solid
  wireframe, \f$<1\f$ chasing comet heads, \f$\ll 1\f$ orbiting pearls.

The whole figure is **rigid in formation space**: each tick every live pen ribbon is re-framed through
`FlyingTrail.TransformRibbon(oldCenter, newCenter, delta, scaleRatio)` (\f$p' = \text{newCenter} + \text{delta} \cdot (p - \text{oldCenter}) \cdot \text{scaleRatio}\f$;
pure translation at scale 1 is the identity-delta fast path). The ribbon **is** scale-corrected — old
ink shrinks with the shape as it tapers toward the bar, which matters most on the long-ribbon shapes (few pens
sharing a long walk) where unscaled old ink would outlive the taper and hold the big silhouette.

**Transport bridge** — the group's anchor `TrailFlight` handle is the pause/snap/slow-mo interface, shared by
every formation in the group (so a cinematic pause/completion fans out to the whole constellation), polled per
tick: `Paused` freezes the formation and inflates its ribbon time so the drawn figure survives the freeze; `Idle`
(the pan-in or a `CompleteAll` drove the principal home) **snaps** — report the value now at the anchor's
position, fade the live pens out (unscaled, freeze-safe), release; `Speed` scales the formation clock (slow-mo).
The flight stays registered (InFlight) through the group's whole life and is unregistered only once the last
formation finishes, so a principal that lands first never falsely snaps the others. A group-CTS cancellation (run
reset) releases everything **without** reporting. A `Reported` guard (plus the reporter's own backstop) keeps a
snap and a normal arrival from double-firing.

## Spawn & Cinematic Interception

The `DefaultScore` handler spawns one trail per point in each `ScorePointsGroupMessage`, unconditionally — nothing gates spawning during cinematics. A single per-group task walks the points in order for stagger delay: the first point spawns immediately, each subsequent point after one more `ScorePointsScatterDelay`. Each flight registers in the `TrailFlightRegistry<TrailId>` (exposed as `Flights`) on spawn and unregisters on arrival.

`RegisterTarget` (called by each `ColorProgressBar` in `Start()`) also prewarms that color's `ScoreTrail_{colorName}` pool via `TrailSpawner.PrewarmAsync`, to `IScoreTrailConfig.ScoreTrailPrewarmPerColor` (default 64) — one `Instantiate` per frame so registering a color at level setup never spikes into a hitch. A level restart re-registering the same color is a no-op past the first call, so the pool tops up once instead of growing unboundedly.

The level-up cinematic (`LevelUpCinematic` in `Game/Cinematics/`) intercepts through that registry rather than through this service:

1. On a `ScorePointsGroupMessage` where `ILevelProgress.WillLevelUp()` is true, it records `_resolver.PrincipalIdFor(msg)` as the tipping trail and waits for that id to appear in `Flights`. The handler nominates its own principal, so the cinematic never hardcodes the numbering: for `DefaultScore` that resolves to the group's FIRST point (`msg.FirstScore`), which spawns immediately, keeping the bounded wait timeout-safe — later points can spawn seconds later under scatter stagger.
2. It then pauses that single flight (`FlyingTrail.DisableMoveTween()` + `TrailFlight.Pause()`) and puppets its position/scale along the pan-in curve while the camera follows. All other in-flight trails keep flying at normal speed so their progress-bar arrivals confirm naturally.
3. When the tipping trail reaches its bar (or its matching `ScoreTrailArrivedMessage` lands first) the pan-in ends — but the survivors are **not** completed there. They stay airborne (confirming progress normally) until `LevelUpPhase` reaches `Pending` (the popup is up), at which point `Flights.PauseAll()` freezes whatever is still in flight behind the popup. Those frozen trails are only resolved later, as outgoing-level content: `LevelTransitionController` calls `ScoreTrailService.HoldOutgoing`, which calls `Flights.CompleteAll()` — every survivor reports its arrival (banking its points) and, for shape formations, snap-fades out. Only the cinematic's own abort path calls `CompleteAll` directly, to resolve everything immediately when the ceremony is cut short.

## Interactions

- **`ScorePointsGroupMessage`** — published by `ScoreController` on pop (one per resolved color, carries the group's total `Points`, `LastScore`, streak `Multiplier`, and the shot's `HitDirection` — BigScore rolls its shapes about it), consumed by `ScoreTrailService` and `LevelUpCinematic`
- **`ScoreTrailArrivedMessage`** — published by `ScoreTrailService` on trail arrival, consumed by `ScoreController` (lifetime tally), `LevelController` (progress confirmation), `ColorProgressBar`, and `LevelUpCinematic`
- **`ScoreLevelUpMessage`** — published by `LevelController` on level-up (see `Game/Level/`), consumed by `ColorProgressBar`, `LevelUpPopUp`, `LevelDifficultyResolver`, and `ColorStreakTracker` (auto-reset)
- **`Cinematics/`** — `LevelUpCinematic` intercepts the tipping trail via `ScoreTrailService.Flights` (see Spawn & Cinematic Interception above) and reads the tipping bar's world position via `ScoreTrailService.GetTarget`
- **`ColorProgressBar`** — registers itself as its colour's `ITrailEndpoint` via `ScoreTrailService.RegisterTarget` (forwarded to the shared `TrailEndpointRegistry` in `Shared/Pool`); reads progress from `ILevelProgress`; reads streak via `ColorStreakTracker.GetStreak` for streak notice display
