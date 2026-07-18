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
bar it contributes to. `ScoreController` publishes one `ScorePointsGroupMessage` per
resolved color, carrying the group's total points. `ScoreTrailService` spawns a pooled
`FlyingTrail` orb per point in the group.

**Projected vs confirmed progress (owned by `LevelController`, not `ScoreController`):**
`LevelController` holds two per-color counters. Projected progress advances immediately on
pop (`ScoreController` writes it via `ILevelProgress.ClaimProgress`, capped at the threshold);
confirmed progress advances only on trail *arrival* (`ScoreTrailArrivedMessage`). `WillLevelUp`
reads projected; the confirmed threshold check triggers the level-up only after the visual
feedback has landed. The level-up itself is a two-phase commit gated by `LevelUpPhase`
(`Playing → Pending → Transitioning`) — the level advances on popup dismissal, not on detection.
See @ref arch_cinematics_architecture and `Game/Level/README.md`.

**Cinematic intercept:**
`LevelUpCinematic` (a plain C# producer over the `CameraRigCinematic` runner — see
@ref arch_cinematics_architecture) subscribes to `ScorePointsGroupMessage`. When
`ILevelProgress.WillLevelUp()` is true at publish time it awaits the tipping trail's registration in
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

**No next-level trails — excess is capped, not carried:**
`ILevelProgress.ClaimProgress` caps a color's granted points at that level's threshold, so a
big or high-streak pop brings a color to *at most* the threshold and any excess is dropped —
one level-up per burst. Points never renumber into the next level, so every trail in flight
belongs to the current level (`TrailId` needs no level component).

**Why projected progress leads confirmed progress:**
Without the projection, a multi-point balloon would assign the same `TrailId` to
multiple trails (all at score position N). Each pop advances `LevelController`'s projected
counter per point, so each trail gets a unique `(Color, Score)` key, preventing trail
identity collisions in `TrailFlightRegistry`.

