# Flight

Owns the per-shot boundary and the counts scoped to it. A **gameplay** service, not part of
the telemetry subsystem — audio depends on it, so making it telemetry's would put a
shipping feature downstream of metrics.

## Contents

| File | What it does |
|---|---|
| `IFlightScope` | The shot's boundary as two reactive windows plus `FlightIndex`. `IsLoaded` spans `[Loaded, Destroyed)` — the whole shot including aiming; `IsAirborne` spans `[Fired, Destroyed)` |
| `IFlightStats` | What happened this flight: pops and deflects per `BalloonType`, pierce discharges, wall bounces. Zeroed when the next projectile loads |
| `IFlightStatsWriter` | The write half, segregated so no reader can mutate. `HitPipeline` is the only caller |
| `FlightStatsService` | Implements all three. Plain C#, `IStartable`/`IDisposable` |

## Which window to gate on

`IsAirborne` is almost always the one you want for effects — ducking, time-scale, anything
that should stop when the shot dies. `IsLoaded` stays true while the player is *aiming*, so
gating a duck on it holds the effect permanently between shots. `IsLoaded` exists because
per-flight counts are scoped to it: they reset on load, not on fire.

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
post-increment count and subtracting one so the flight's first pop of a type lands on the
root.

Only the flight *boundary* (`ProjectileLoadedMessage`, `Fired`, `Destroyed`) is a
subscription here, because nothing reads a boundary flag mid-dispatch and expects it to
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
