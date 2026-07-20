@page arch_turn_pipeline Turn Pipeline

# Turn Pipeline

@image html turn_pipeline.svg "Turn Pipeline — Hit → Balance → Spawn → Post-Balance"

## What this diagram shows

The four sequential phases that make up one game turn — from the moment the projectile
contacts a balloon to the moment the grid is settled and ready for the next shot.

**Phase 1 — Hit**
The projectile flies freely, bouncing off walls. On each balloon contact `ProjectileView`
hands the collision to the plain-C# `ProjectileHitResolver`, which calls `EvaluateHit`
(state-mutating), embeds the outcome in `ActorHitMessage`, and routes it through
`IHitDispatcher` (`Game/HitPipeline`). The pipeline runs the order-dependent
stages synchronously and explicitly — `ScoreController` records streak/score first, then
the owning `BalloonController` (resolved via `BalloonControllerRegistry`) pops/deflects —
and only then broadcasts the message for order-independent observers (`NudgeService`,
`ItemActivator`, `GridActorHitController`, `BalloonSpawner`, `SpeckField`, VFX). A direct
(`DamageFlags.DirectHit`) pop also gives `BalloonSpawner` a chance to roll an extra
"pop-spawn" balloon into the board, independent of the turn's own spawn wave. The board
itself keeps settling during flight too — see "In-flight rebalancing" below.

**Phase 2 — Balance (pre-spawn)**
When the projectile dies it publishes `ProjectileDestroyedMessage`. `BalloonSpawner`
receives it and, once past the level's `FirstSpawnTurn` grace period (a few turns with no
spawns at the start of each level), kicks off its spawn sequence — which opens with a
`Balance(relocateRoamers: true)` pass consolidating existing balloons upward before any new
lines arrive. Transit slots for in-progress balance animations are reserved in
`BalancePathHolder`.

**Phase 3 — Spawn**
New balloon lines are placed with a stagger delay between each. Spawn animations use
`ComputePath` waypoints; the `BalancePathHolder` is consulted to detect conflicts with
still-animating balance moves from Phase 2.

**Phase 4 — Post-spawn Balance**
After all lines are spawned, `BalloonSpawner` publishes `BalanceBalloonsMessage`.
`BalloonBalancer` runs again on the next frame to settle any gaps created by the fresh
spawn (e.g. a balloon placed in a column that needed support from below).

## Guidance

**Where to add behavior triggered by a balloon pop:**
Subscribe to `ActorHitMessage` filtered by `Outcome == Pop`. Your subscriber runs in
Phase 1 — synchronously, before the projectile dies, and *after* the pipeline's ordered
stages (score recording and the owning balloon's reaction) have completed. If your
behavior must run *before* the pop or interleave with scoring, it belongs in
`HitPipeline` as an explicit stage, not on the bus. Never publish `ActorHitMessage`
directly — route hits through `IHitDispatcher`.

**Where to add behavior triggered by turn end (projectile death):**
Subscribe to `ProjectileDestroyedMessage`. This runs between Phase 1 and Phase 2.

**In-flight rebalancing:**
Balance isn't confined to the turn boundary. `BalloonBalancer.Tick` pulses a full
`Balance()` pass at `IBalloonsConfiguration.FlightRebalanceInterval` while a shot is
airborne — gated on the projectile still being free, not on its last-shield glide, not
paused, and only when `HasPossibleMove()` finds an actual gap to close. This keeps a long
flight from leaving the board visibly unsettled for its whole duration, on top of the
pre-spawn and post-spawn passes below.

**Ordering note:** `BalanceBalloonsMessage` is published by both `ProjectileView`
(fallback — in case `BalloonSpawner` never gets to publish it) and `BalloonSpawner`
(authoritative — after all lines are placed). `BalloonBalancer` debounces redundant
publishes via a one-frame `UniTask.Yield` delay.

