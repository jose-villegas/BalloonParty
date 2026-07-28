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
confirmed progress advances only on trail *arrival* (`ScoreTrailArrivedMessage`). Detection
happens at **claim time** — `LevelController.TryBeginCompleting` checks if all colors meet
threshold inside `ClaimProgress`. Once detected, phase transitions to `Completing` immediately.
(`WillLevelUp()` still exists on the interface but has zero callers.) The level-up itself is a
multi-phase commit gated by `LevelUpPhase` (`Playing → Completing → Pending → Transitioning`)
— the level advances on popup dismissal, not on detection.
See @ref arch_cinematics_architecture and `Game/Level/README.md`.

**Cinematic intercept:**
`LevelUpCinematic` (a plain C# producer over the `CameraRigCinematic` runner — see
@ref arch_cinematics_architecture) subscribes to `ILevelProgress.Phase`. When the phase
transitions to `Completing` (detection at claim time via `LevelController.TryBeginCompleting`),
the hit beat begins immediately. The camera follows the **projectile** (via
`ProjectilePositionProvider`) with box-in-box clamped bounds — no trail interception is
involved. `LevelController` drives an exclusive `ITimeScaleClaims.ClaimExclusive` during the
beat; the authored beat curve IS the time-scale warp. The beat ends on a **duration basis**
(authored curve length elapses), not on trail arrival. After the beat ends, a non-exclusive
ramp-up (1→2× speed) accelerates the remaining trail flight. Survivors are not
force-completed yet — they stay frozen once `LevelUpPhase` reaches `Pending`
(`Flights.PauseAll()`, so their shapes hold behind the popup instead of snapping away).
They are only resolved once the level transition runs:
`LevelTransitionController` calls `ScoreTrailService.HoldOutgoing`, which calls
`Flights.CompleteAll()`, banking every survivor's points as outgoing-level content. (Only
the cinematic's own abort path calls `CompleteAll` directly, to resolve everything
immediately if the ceremony is cut short.)

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

