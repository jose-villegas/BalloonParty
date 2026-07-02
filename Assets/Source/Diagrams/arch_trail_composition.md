@page arch_trail_composition Trail Utility Composition

# Trail Utility Composition

@image html trail_composition.svg "Trail Utility Composition"

## What this diagram shows

How `TrailFlightRegistry<TrailId>` composes with `ScoreTrailService` to support
forward registration, retroactive interception, and cinematic pause/resume — without
any of those concerns leaking into `FlyingTrail` itself.

**`TrailFlightRegistry<T>`** — a plain C# generic that tracks which trails are
currently in flight by their `TrailId`. `Register(id, callback)` can be called:
- **Before** the trail spawns (forward registration) — the callback fires when the
  trail is spawned, which immediately pauses the trail
- **After** the trail is already in flight (retroactive) — `LevelUpCinematic` awaits the
  trail in `_flights`, switches its DOTween tweens to unscaled time, and fires the
  callback immediately

`ScoreTrailService` exposes `Flights` (a `TrailFlightRegistry<TrailId>`: `Contains` /
`Get` / `CompleteAll`); each `TrailFlight` handle exposes `Pause` / `Resume` /
`Complete` / `Speed` — the cinematic's vocabulary for manipulating in-flight trails.

**`FlyingTrail`** — knows nothing about cinematics, pause state, or `TrailId`. It just
flies from A to B on a path and calls `OnComplete`. All interception happens by
externally switching its tweens to `UpdateType.Manual` and advancing them with
`DOTween.ManualUpdate`.

## Guidance

**Adding a new trail consumer (e.g. a new cinematic that intercepts a trail type):**
1. Subscribe to the relevant `ScorePointMessage` variant
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

