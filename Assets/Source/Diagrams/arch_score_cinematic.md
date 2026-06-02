@page arch_score_cinematic Score & Cinematic Pipeline

# Score & Cinematic Pipeline

@image html cinematic_flow.svg "Score & Cinematic Pipeline"

## What this diagram shows

The full journey from a balloon pop to a confirmed level-up, including the cinematic
intercept that pauses the tipping trail mid-flight.

**Attribution → trails:**
`ScoreController` receives `ActorHitMessage`, casts the actor to `IHasScoreColor`, and
calls `ResolveScoreAttribution` — the actor appends one `ScoreAttribution` per color
bar it contributes to. `ScoreController` publishes one `ScorePointMessage` per
individual point × streak multiplier. `ScoreTrailService` spawns a pooled `FlyingTrail`
orb per message.

**Projected vs confirmed progress:**
`_projectedProgress` advances immediately on pop (used for trail score identity and
`WillLevelUp` checks). `_levelProgress` only advances on trail *arrival* (`ScoreTrailArrivedMessage`)
— the confirmed threshold check uses `_levelProgress` so the level-up only triggers
after visual feedback has landed.

**Cinematic intercept:**
`LevelUpTrailEffect` subscribes to `ScorePointMessage`. When `WillLevelUp` is true at
publish time, the tipping trail is registered via `TrackTrail`. On registration the
trail's tweens switch to unscaled time, the camera pan-in begins, and gameplay pauses
(`PauseSource.Cinematic`). Next-level trails spawned after the tip are paused until the
popup is dismissed.

## Guidance

**Adding a new score source (new actor type or item):**
Implement `IHasScoreColor` on the model — `ResolveScoreAttribution` appends
`ScoreAttribution(colorId, points, breaksStreak)` entries. `ScoreController` calls it
automatically on any `Pop` or `PassThrough` hit. No changes to `ScoreController` needed.

**Understanding `NextLevel` trails:**
When a multi-point pop straddles a level boundary, points above the threshold are
published with `NextLevel = true` and a renumbered `Score` (starting from 1 in the
new level). These trails are paused during the cinematic. After the popup dismisses,
`ScoreTrailService` resumes them — they fly to the reset progress bars.

**Why projected progress leads confirmed progress:**
Without the projection, a multi-point balloon would assign the same `TrailId` to
multiple trails (all at score position N). The projection increments a counter per
point so each trail gets a unique `(Color, Score, Level)` key, preventing trail
identity collisions in `TrailFlightRegistry`.

