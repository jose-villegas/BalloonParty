@page arch_score_cinematic Score & Cinematic Pipeline

# Score & Cinematic Pipeline

@image html cinematic_flow.svg "Score & Cinematic Pipeline"

## What this diagram shows

The full journey from a balloon pop to a confirmed level-up, including the cinematic
intercept that pauses the tipping trail mid-flight.

**Attribution → trails:**
`ScoreController.OnActorHit` runs as the first explicit `HitPipeline` stage (before the
owning balloon reacts and before the `ActorHitMessage` broadcast — see
@ref arch_turn_pipeline); it casts the actor to `IHasScoreColor` and
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
`LevelUpCinematic` (a plain C# producer over the `CameraRigCinematic` runner — see
@ref arch_cinematics_architecture) subscribes to `ScorePointMessage`. When `WillLevelUp`
is true at publish time it awaits the tipping trail's registration in
`TrailFlightRegistry`, then intercepts it: the move tween is killed and the trail is
puppeted manually along the pan-in segment's `TimeScaleCurve` while the camera pans in
and gameplay pauses (`PauseSource.Cinematic`). `Time.timeScale` stays untouched during
the pan-in — other trails fly at normal speed. When the tipping trail arrives, the
pan-in ends with `Flights.CompleteAll()`, instantly confirming every remaining
in-flight trail before the popup opens.

## Guidance

**Adding a new score source (new actor type or item):**
Implement `IHasScoreColor` on the model — `ResolveScoreAttribution` appends
`ScoreAttribution(colorId, points, breaksStreak)` entries. `ScoreController` calls it
automatically on any `Pop` or `PassThrough` hit. No changes to `ScoreController` needed.

**Understanding next-level trails:**
When a multi-point pop straddles a level boundary, points above the threshold are
published with a renumbered `Score` (starting from 1) and `Level + 1`. These trails
fly normally during the cinematic; when the tipping trail arrives, `LevelUpCinematic`
completes every remaining in-flight trail (including next-level ones) via
`Flights.CompleteAll()`, so all progress is confirmed before the popup opens.

**Why projected progress leads confirmed progress:**
Without the projection, a multi-point balloon would assign the same `TrailId` to
multiple trails (all at score position N). The projection increments a counter per
point so each trail gets a unique `(Color, Score, Level)` key, preventing trail
identity collisions in `TrailFlightRegistry`.

