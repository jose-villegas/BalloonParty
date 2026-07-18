@page arch_trail_composition Trail Utility Composition

# Trail Utility Composition

@image html trail_composition.svg "Trail Utility Composition"

## What this diagram shows

How `TrailFlightRegistry<TrailId>` composes with `ScoreTrailService` to support
per-trail lookup, cinematic interception, and bulk transport commands — without any
of those concerns leaking into `FlyingTrail` itself.

**`TrailFlightRegistry<T>`** — a plain C# generic that tracks which trails are
currently in flight by their `TrailId`. `ScoreTrailService` calls
`Register(id, transform, origin)` when it spawns a trail and `Unregister(id)` on
arrival. A consumer that wants a trail that may not have spawned yet simply awaits
`Contains(id)` (this is what `LevelUpCinematic` does for the tipping trail), then
takes the `TrailFlight` handle via `Get(id)`.

`ScoreTrailService` exposes `Flights` (a `TrailFlightRegistry<TrailId>`: `Contains` /
`Get` / `CompleteAll` plus bulk pause/resume/stop/speed); each `TrailFlight` handle
exposes `Pause` / `Resume` / `Stop` / `Complete` / `SetSpeed` / `SetUnscaledTime` —
transport-style commands over the trail's DOTween tweens.

**`FlyingTrail`** — knows nothing about cinematics, pause state, or `TrailId`. It just
flies from A to B on a path and calls `OnComplete`. Interception is external: the
cinematic calls `FlyingTrail.DisableMoveTween()` to take the move tween out of play,
then puppets the trail's transform directly (a curve-driven lerp from origin to
target) and calls `Complete()` on the handle when it lands.

## Guidance

**Adding a new trail consumer (e.g. a new cinematic that intercepts a trail type):**
1. Subscribe to the relevant `ScorePointsGroupMessage` variant
2. Await `Flights.Contains(id)` then `Flights.Get(id)` — works whether the trail has
   spawned yet or not
3. Drive the `TrailFlight` handle (`Pause` / `Resume` / `Complete`); use
   `Flights.CompleteAll()` to flush stragglers when your sequence ends
4. Registrations clear on arrival (`Unregister` in the arrival callback) — kill any
   handle you hold in your cleanup path

**Why `TrailId` uses `(Color, Score, Level)` not just an int:**
- Two colors can produce the same numeric score simultaneously in a single turn
- After a level-up, score restarts from 1 — in-flight trails from the previous level
  would collide with the first trails of the new level without the `Level` field
- All three fields are needed for global uniqueness across the lifetime of a session

