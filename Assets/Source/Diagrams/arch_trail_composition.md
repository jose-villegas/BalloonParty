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
- **After** the trail is already in flight (retroactive) — `TrackTrail` detects the
  trail in `_flights`, switches its DOTween tweens to unscaled time, and fires the
  callback immediately

`ScoreTrailService` exposes `TrackTrail(id, callback)`, `PauseTrailsAbove(threshold)`,
`ResumeTrail(id)`, and `ClearTrackedTrail(id)` — the cinematic's vocabulary for
manipulating in-flight trails.

**`FlyingTrail`** — knows nothing about cinematics, pause state, or `TrailId`. It just
flies from A to B on a path and calls `OnComplete`. All interception happens by
externally switching its tweens to `UpdateType.Manual` and advancing them with
`DOTween.ManualUpdate`.

## Guidance

**Adding a new trail consumer (e.g. a new cinematic that intercepts a trail type):**
1. Subscribe to the relevant `ScorePointMessage` variant
2. Call `ScoreTrailService.TrackTrail(id, callback)` — works whether the trail has
   spawned yet or not
3. In the callback, do your cinematic work; call `ResumeTrail(id)` when done
4. Call `ClearTrackedTrail(id)` in your cleanup path to prevent stale registrations

**Why `TrailId` uses `(Color, Score, Level)` not just an int:**
- Two colors can produce the same numeric score simultaneously in a single turn
- After a level-up, score restarts from 1 — in-flight trails from the previous level
  would collide with the first trails of the new level without the `Level` field
- All three fields are needed for global uniqueness across the lifetime of a session

