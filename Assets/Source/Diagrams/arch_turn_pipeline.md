@page arch_turn_pipeline Hit, Spawn and Balance Pipeline

# Hit, Spawn and Balance Pipeline

@image html turn_pipeline.svg "Hit, Spawn and Balance Pipeline"

## What this diagram shows

Two independent flows that share the grid: balls hitting balloons, and a timer bringing
new balloon lines in. There is no "turn" any more: a line never waits for a shot to end,
and several balls can be in flight at once.

**Hit**
Any ball in flight bounces off walls. On each balloon contact `ProjectileView`
hands the collision to the plain-C# `ProjectileHitResolver`, which calls `EvaluateHit`
(state-mutating), embeds the outcome in `ActorHitMessage`, and routes it through
`IHitDispatcher` (`Game/HitPipeline`). The pipeline runs the order-dependent
stages synchronously and explicitly: `ScoreController` records streak/score first, then
the owning `BalloonController` (resolved via `BalloonControllerRegistry`) pops/deflects,
and only then broadcasts the message for order-independent observers (`NudgeService`,
`ItemActivator`, `GridActorHitController`, `BalloonSpawner`, `SpeckField`, VFX). A direct
(`DamageFlags.DirectHit`) pop also gives `BalloonSpawner` a chance to roll an extra
"pop-spawn" balloon into the board, independent of the clock's lines. A ball's death
(`ProjectileDestroyedMessage`) spawns nothing.

**Spawn clock**
`SpawnClock` counts down scaled game time (2 s at level 1, shrinking per level to a floor,
after a grace at each level start) and publishes `SpawnBalloonLineMessage(1)`. It holds while
paused, off the `Game` screen, during a level-up, and while a lone doomed ball glides its
last breath. One line per tick, however long the frame. `BalloonSpawner` handles each message
with the synchronous `SpawnLine()`:

1. **Pre-line balance** — `Balance()` consolidates existing balloons upward. On a wave's
   first line it is `Balance(atWaveStart: true)`, which also relocates roamers and refreshes
   the heavy-step budget. Transit slots for in-progress balance animations are reserved in
   `BalancePathHolder`.
2. **Spawn** — one line is placed. Spawn animations use `ComputePath` waypoints; the
   `BalancePathHolder` is consulted to detect conflicts with still-animating balance moves.
3. **Post-line balance** — `BalloonSpawner` publishes `BalanceBalloonsMessage`;
   `BalloonBalancer` runs again on the next frame to settle gaps the fresh line created.

A **wave** is `LinesPerWave` (3) consecutive lines. The wave's last line publishes
`ItemCheckMessage`, which is what the item cadence counts.

**Rebalance pulse**
Independently of lines, `BalloonBalancer.Tick` pulses a `Balance()` every
`FlightRebalanceInterval` all through play, so hits that open gaps get settled without
waiting for the next line.

## Guidance

**Where to add behavior triggered by a balloon pop:**
Subscribe to `ActorHitMessage` filtered by `Outcome == Pop`. Your subscriber runs
synchronously, before the projectile dies, and *after* the pipeline's ordered
stages (score recording and the owning balloon's reaction) have completed. If your
behavior must run *before* the pop or interleave with scoring, it belongs in
`HitPipeline` as an explicit stage, not on the bus. Never publish `ActorHitMessage`
directly — route hits through `IHitDispatcher`.

**Where to add behavior triggered by a ball's death:**
Subscribe to `ProjectileDestroyedMessage`. It says nothing about the board: lines come from
the clock, so do not hang spawning or board-settling work on it.

**Where to add behavior triggered by a new line:**
Subscribe to `BalanceBalloonsMessage` for "the grid just got a line and needs settling", or
to `ItemCheckMessage` for once-per-wave work over the new balloons.

**Rebalancing:**
Balance isn't tied to a line. `BalloonBalancer.Tick` pulses a full `Balance()` pass at
`IBalloonsConfiguration.FlightRebalanceInterval` all through play — not paused, not while a
lone doomed ball glides, and only when `HasPossibleMove()` finds an actual gap to close —
on top of the pre-line and post-line passes above. `SecondsUntilNextPulse` exposes the
pulse timer to the shot solver.

**Ordering note:** `BalanceBalloonsMessage` is published by both `ProjectileView`
(fallback — in case `BalloonSpawner` never gets to publish it) and `BalloonSpawner`
(authoritative — after each line is placed). `BalloonBalancer` debounces redundant
publishes via a one-frame `UniTask.Yield` delay.

