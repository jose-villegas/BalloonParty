# Flight

Owns the per-ball boundary and the counts scoped to each ball. A **gameplay** service, not part of
the telemetry subsystem — audio depends on it, so making it telemetry's would put a
shipping feature downstream of metrics.

## Contents

| File | What it does |
|---|---|
| `IFlightScope` | The boundary as one reactive window: `IsAirborne` is true while any ball is in `[Fired, Destroyed)`. It is an "any ball" flag, not one ball's state. (There is no "loaded" flag: the thrower always holds a ball, so it would never be false) |
| `IFlightStats` | What happened to one ball: pops and deflects per `BalloonType`, pierce discharges, wall bounces |
| `IFlightStatsSource` | `Of(ball)` returns that ball's `IFlightStats`. A ball's stats stay readable through its own `ProjectileDestroyedMessage`; an unknown or null ball reads as all zeroes |
| `IFlightStatsWriter` | The write half, segregated so no reader can mutate. Each write names the ball it counts for. `HitPipeline` is the only caller of the hit counts |
| `FlightStatsService` | Implements all four. Plain C#, `IStartable`/`IDisposable` |

Several balls can exist at once (up to `MaxBallsInFlight` fly at once), so a
reader never asks "the flight's" stats. It takes the ball its message carries
(`message.Projectile`, or `DamageContext`'s ball) and calls `Of(ball)`. Stats are created when
the ball loads and dropped when `IProjectileRegistry.Retired` fires for it, which is *after* its
Destroyed dispatch (see the Land/Retire rule in `Projectile/README.md`).

## Which window to gate on

`IsAirborne` is the one to use for effects — ducking, time-scale, anything that should stop
when the last shot dies. A ball's counts start at its load, not at its fire, but the scope
exposes no flag for that: the thrower always holds a ball, so such a flag would be constant.

## The publisher counts, then publishes

Every count is written by whoever publishes the corresponding message, immediately before
that publish:

| Count | Written by |
|---|---|
| pops, deflects | `HitPipeline.Dispatch` |
| wall bounces | `ProjectileView`, at the bounce |
| pierce discharges | `ProjectileHitResolver.DischargePending` |

That is what makes the counts safe to share: every subscriber reads a total that already
includes the event it is reacting to, so **no reader depends on MessagePipe's subscription
order**, which is enforced nowhere. `CombatSoundRouter` relies on this directly, reading the
post-increment count and subtracting one so the ball's first pop of a type lands on the
root.

Only the flight *boundary* (`ProjectileLoadedMessage`, `Fired`, `Destroyed`, and the
registry's `Retired`) is a subscription here, because nothing reads a boundary flag mid-dispatch and expects it to
already have flipped.

If a subscriber ever counts one of these instead, the value each reader sees becomes
order-dependent and the pitch ramps drift by a step — silently, and only audible to someone
listening for it. Don't.

## Cheats feed it too

`AwardScorePopCheat`, `BalloonRemoverCheat` and `ScoreCheatHelper` all dispatch through
`HitPipeline`, so cheat-driven pops land in these counts. That is deliberate — those runs
are cheat-tagged for filtering downstream, and special-casing them here would make the
audio ramps behave differently under cheats than in a real run.

## Design plan

Full specification: @ref plan_gameplay_telemetry
