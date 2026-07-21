# Projectile

The projectile is the ball fired by the thrower. It travels in a straight line, bounces off walls, and pops balloons on contact.

## Gameplay

Each shot starts loaded on the thrower. When fired it moves freely across the screen, reflecting off the top, left, and right boundaries. Each bounce costs one shield. When shields are exhausted the projectile is destroyed, the grid rebalances, and the thrower reloads.

Cruise and Sweep both feed the projectile's tap system. Cruise still comes from long empty-corridor wall runs; Sweep adds a tap at a wall hit when the current segment popped at least one balloon, every balloon hit on that segment was at exactly 1 HP when struck, and the backward circle-cast to the previous bounce finds the corridor now clear. Sweep reuses Cruise's `CruiseSpeedPerShield` tap value, the same tap-beat easing, and the same piercing threshold.

On contact with a balloon the projectile absorbs that balloon's color. The shared `ColorStreakTracker` (`Game/Score/`) tracks consecutive same-color pops; from the second consecutive same-color pop onward (\f$\text{streak} \ge 2\f$), each pop grants the projectile an additional shield — a long same-color streak keeps awarding shields on every hit until a different color (or a scatter pop) breaks it. Neighboring balloons nudge outward from the impact point using slot-based positions (logical grid positions, not visual transform positions) to ensure consistent nudge direction regardless of in-progress animations.

When a hit returns `HitOutcome.Absorb`, `ProjectileHitResolver` dispatches `ActorHitMessage(Absorb)`, sets `model.IsFree = false`, and returns `ProjectileHitVisual.Destroyed` — the view then calls `DestroyProjectile()`. `Absorb` is produced by `AbsorberActorModel` (always absorbs, killing the projectile) — no balloon model returns it; balloon hits resolve to `Pop`, `Deflect`, or `PassThrough`.

## Turn-based flow

The game follows a turn-based animation pipeline:

1. **Projectile flight** — bounces, pops balloons, nudges neighbors. The board keeps settling during flight too: `BalloonBalancer.Tick` pulses a full rebalance every `IBalloonsConfiguration.FlightRebalanceInterval`, gated on the shot still being free (not gliding its last-shield approach, not paused) and on `HasPossibleMove()` finding an actual gap to close.
2. **Projectile death** — publishes `BalanceBalloonsMessage` (fallback) and `ProjectileDestroyedMessage`.
3. **Spawn + balance** — `BalloonSpawner` opens its spawn sequence with a pre-spawn `Balance(relocateRoamers: true)` pass, spawns new lines, then publishes `BalanceBalloonsMessage` after all lines are placed.

See @ref arch_balance_flow for the full balance algorithm and its entry points.

## Pooling

The projectile is **pooled, not destroyed**. A single instance is created via `ProjectilePoolChannel` on first load and reused across turns. On death the projectile publishes messages but does not self-destruct — `ThrowerController` returns it to the pool and immediately re-gets it.

- **`OnDespawned()`** — nulls model, resets glow, disables trail via `ProjectileTrail.Disable()`, resets shield view
- **`OnSpawned()`** — resets shield-shown flag; trail stays disabled until the projectile is fired

Trail management is handled by `ProjectileTrail`, a child component on the trail GameObject. It is **not** `IPoolable` — the projectile itself is pooled, so its children's lifecycle follows the parent. `ProjectileTrail` exposes `Enable()` / `Disable()`:

- **`Enable()`** — `async UniTaskVoid` that yields one frame (`UniTask.Yield(destroyCancellationToken)`), clears the trail, then re-enables emitting (prevents snap artifact from position change)
- **`Disable()`** — stops emitting and clears immediately

`ProjectileView` calls `Enable()` on the first `FixedUpdate` frame where `IsFree` is true (fired) and `Disable()` on death and despawn.

The shield field is hidden until the projectile is fired. `ProjectileShieldView` starts inactive on `Awake()` and is shown via `Show()` on the first `FixedUpdate` frame where `IsFree` is true.

## Shield Visuals

The projectile's shield appears as a glowing force field resembling magnetic field lines
wrapping around the ball. Each remaining shield adds a visible concentric layer (up to a
configured maximum). When a shield is gained, it appears with a wipe sweeping from the leading
tip to the tail. When lost, the outermost layer crumbles away (starting from the front).

While the ball is flying, the glow stretches into a comet shape with a tail trailing behind
it. As the ball nears a wall, the glow smoothly tucks into a circle, holds that round shape
through the bounce, then stretches back into the comet as it flies away. On impact, the circle
visually squashes against the surface — compressing along the wall normal and expanding
sideways — then springs back. This works for both flat wall bounces and angled deflector
bounces. The field also ripples faster the quicker the ball moves.

A four-state cycle (Cruising → Closing → Bracing → Opening) drives the shape transition.
The game predicts how far the next wall is; when it falls below a threshold, closing begins.
On bounce, the shield force-snaps to circle regardless of the current state.

### Implementation

`ProjectileShieldView` feeds 10 uniforms to the `EMShieldField.shader` (a single-quad
procedural shader — one draw call). All uniforms — per-layer dissolve/reveal arrays, active
layers, color, velocity factor, noise scroll direction, shape lerp, noise intensity, squash
amount, and squash axis — are written to a single `MaterialPropertyBlock` and pushed through
`WriteAllProperties()`. Both tween callbacks and the per-frame `Update()` call this method,
which writes **every** property before calling `SetPropertyBlock`. This single-push pattern
prevents split-brain overwrites where one code path's push would erase properties written by
another. Configuration lives in `IShieldFieldSettings` (injected, not serialized on the view).

All VFX (gain/lose/bounce particles) are spawned via `ParticlePoolChannel` as world-space
particles — they are not children of the projectile and survive recycling independently.

See @ref plan_em_shield_field for the full shader design and phase status.

## Pierce & Discharge Feel

A piercing shot doesn't pop the tough (`hits > 1`) balloons it plows through on contact — it carries them until it **discharges**. (The plow-then-shatter mechanic itself — driven by cruise piercing and the Snipe item — is documented in `Item/README.md`; this section covers only how the projectile *sells* it.) Two lights carry the beat:

- **Telegraph** — while a tough balloon is ahead on the shot's current straight run, its carried scene light stretches into a capsule reaching that tough, warning of the armored contact a moment before it lands. Once nothing tough is ahead, the light snaps back to a point following the shot (`ProjectileView.TryFindToughAhead` — a forward circle-cast bounded by the next wall, skipping any tough the shot has already plowed this run, so the line always points at the *next* one).
- **Spark** — every tough actually plowed pops a brief Sparks-coloured flash at the strike point (`FlashPierceSpark`), fired synchronously from `OnTriggerEnter2D` — so a tight run of several toughs plowed in one physics step each still get their own flash instead of blurring into one.

The discharge itself plays a shared shockwave-and-slow-mo beat, owned by `Controller/PierceDischargeEffects` off the `PierceDischargedMessage` the discharge publishes: a `DisturbanceField` shockwave stamp (`StampSource.PierceDischarge`) at the shattered line, and a brief real-time slow-mo dip (`TimeScaleSource.PierceDischarge`; duration/scale from `IGameConfiguration.PierceDischargeTimeScale`/`PierceDischargeTimeScaleDuration`) via a serial `CancellationTokenSource` — a discharge landing mid-dip restarts the dip cleanly instead of layering, and only the dip that runs to completion releases the claim. The rainbow colour-bloom that plays on the same message is a separate effect owned by the Snipe item (`SnipeDischargeBloom`), not by the projectile.

The discharge fires a fixed delay (`PierceDischargeDelay`) after the *last* tough plowed in a run, not the first — each new plow re-arms the countdown, so a string of toughs holds the discharge open until the shot is clear of them (`ProjectileMotionResolver.TickPierceDischarge`). Whether a discharge is scheduled is tracked by its own flag (`ProjectileFlightState.DischargeScheduled`) rather than by the countdown reaching zero, so a `PierceDischargeDelay` of `0` still fires on the next tick instead of never firing at all.

## Buffs (`Buffs/`)

A **projectile buff** is a temporary stat modifier on the active projectile following the
industry-standard *Flat / Additive / Multiplicative* stacking pattern:

\f[
\text{final} = \big(\text{base} + \sum \text{flat}\big) \times \big(1 + \sum \text{additive}\big) \times \prod \text{multiplicative}
\f]

Each buff carries four fields:
- **`ProjectileBuffId`** — which stat (`Speed`, `RainbowShield`, …).
- **`float Value`** — the numeric contribution.
- **`BuffModifierOp`** — `Flat`, `Additive`, or `Multiplicative` (determines aggregation lane).
- **`IProjectileBuffEndCondition`** — pluggable lifecycle (flips `Expired` when done).

Multiple buffs targeting the same stat stack correctly:
- All `Flat` values sum → added to base.
- All `Additive` values sum → applied as \f$\times (1 + \text{sum})\f$.
- All `Multiplicative` values multiply independently.

The two abstractions live in `Model/`, the service in `Buffs/`:

- **`ProjectileBuff`** (`Model/IProjectileBuff.cs`) — sealed class. Carries Id, Value, Op, EndCondition.
- **`BuffModifierOp`** (`Model/BuffModifierOp.cs`) — enum: `Flat`, `Additive`, `Multiplicative`.
- **`IProjectileBuffEndCondition`** (`Model/`) — the "when it ends" abstraction: exposes only `IReadOnlyReactiveProperty<bool> Expired`. An implementation encapsulates its own lifecycle logic and flips the bool once. `WallBounceEndCondition` (subscribes to `ShieldLostMessage`, ends on the first wall bounce) is the only one today; a `Timer`/`PopCount`/... end-condition is a new implementer — no context, no switch, no change to any buff.
- **`IProjectileBuffs.Apply(ProjectileBuff)`** — the activation seam, injectable anywhere. Just takes the buff; the buff already carries its end-condition.
- **`ProjectileBuffService`** — `IStartable` owning storage + lifecycle: tracks the active projectile (`ProjectileLoadedMessage`), applies buffs onto it, and drops a buff the first time its `EndCondition.Expired` fires. Knows nothing about any buff's effect or end-condition — it only observes the exposed signal.

The model stores buffs in a plain list exposed via `HasBuff(ProjectileBuffId)` (read), `ComputeBuffedValue(id, baseValue)` (aggregated stat query), and `AddBuff`/`RemoveBuff` (write); a fresh `ProjectileModel` per throw resets them for free.

## How it works

- **`ProjectilePoolChannel`** — `InjectingPoolChannel<ProjectileView>` that creates projectiles via `IObjectResolver` injection from the parent container (no child scope on the prefab). Accessed through `PoolManager`.
- **`Controller/ProjectileHitResolver`** — plain C# singleton owning the hit rules: calls `balloon.EvaluateHit(context)` for the pre-computed outcome, applies the colour-steal (projectile absorbs a popped balloon's color), increments the current-segment pop count for Sweep, tracks whether every segment contact was a 1HP one-shot, and applies the streak-shield rule (reads `ColorStreakTracker` immediately after dispatch — `IHitDispatcher` guarantees the score stage already ran). It dispatches the `ActorHitMessage` through `IHitDispatcher` and returns a `ProjectileHitVisual` for the view to play. Every contact carries `DamageFlags.DirectHit` — this is what marks the hit as projectile-struck (vs. AOE items) for `BalloonSpawner`'s pop-spawn roll — plus `Piercing` while the shot is piercing. When the projectile carries a `ProjectileBuff` with `ProjectileBuffId.RainbowShield` it also flags the hit `WildcardStreak | Piercing` (colour-agnostic scoring + plows through tough/unbreakable balloons) and rainbow-converts the popped balloon's hex neighbours via the injected `SlotGrid`.
- **`Controller/ProjectileHitVisual`** — enum result of a resolve (`None`, `Recolored`, `Destroyed`) so the view knows which feedback to play without re-deriving the rules.
- **`Controller/ProjectileMotionResolver`** — plain C# singleton owning the flight rules: advances one fixed step, wall-bounces via `WallLimits`, decrements shields, and decides destroy-vs-continue, mutating the model's direction/shields. `ProjectileView.MoveAndBounce` just applies the returned `ProjectileStep` (transform, bounce VFX, `ShieldLostMessage`, disturbance stamp); `Deflect` reflects off the balloon's ANALYTIC contact normal — the travel ray is backtracked to its exact entry into the combined-radius contact circle (`TryComputeContactNormal`), since the trigger fires at a discrete fixed step up to ~0.16 wu inside the balloon and a radial normal there would displace the reflection by up to ~30° (the balloon's world collider radius rides `BalloonDeflectedMessage.SurfaceRadius`). `TickPierceDischarge` also runs here each step, debouncing the pierce discharge (see [Pierce & Discharge Feel](#pierce--discharge-feel)). Headless-testable — see `ProjectileMotionResolverTests`.
- **`Controller/PierceDischargeEffects`** — plain C# `IStartable`/`IDisposable` singleton subscribing to `PierceDischargedMessage`; plays the shockwave stamp and slow-mo dip described in [Pierce & Discharge Feel](#pierce--discharge-feel).
- **`Controller/ProjectileStep`** — result of one advance (`Moved` / `Bounced` / `Destroyed` + resulting position and direction) that the view presents without re-deriving the rules.
- **`IProjectileModel`** — read-only interface exposing `IReadOnlyReactiveProperty<string> ColorName`, `IReadOnlyReactiveProperty<int> ShieldsRemaining`, read-only plain properties (`Direction`, `Speed`, `IsFree`, `LastHitBalloon`), `HasBuff(ProjectileBuffId)`, and `ComputeBuffedValue(id, baseValue)` (see [Buffs](#buffs-buffs)). Used by shield UI and views that only observe state.
- **`IWriteableProjectileModel`** — mutable interface extending `IProjectileModel`; re-declares reactive properties as `ReactiveProperty<T>` (via `new` keyword), adds setters, and adds `AddBuff`/`RemoveBuff`. Used by `ProjectileView`, `ThrowerController`, the buff service, and cheats that mutate state.
- **`ProjectileModel`** — concrete class implementing `IWriteableProjectileModel`. Only referenced at creation sites (`ThrowerController.LoadProjectile`).
- **`ProjectilePositionProvider`** — singleton holding the live projectile transform for systems that need its position without a reference to the view (set on load, cleared on reload).
- **`ProjectileView`** — MonoBehaviour implementing `IPoolable`. Drives manual movement in `FixedUpdate` (skipped while `PauseService.IsAnyPaused`), checks bounds against `IGameConfiguration.LimitsClockwise`, reflects direction and clamps position on bounce. Handles `OnTriggerEnter2D` — resolves the `BalloonView` via `GetComponent<BalloonView>()` on the collider (O(1) when the collider lives on the same GameObject as `BalloonView`) and hands the collision to `ProjectileHitResolver`, playing the returned `ProjectileHitVisual`. On each surviving wall hit it also evaluates Sweep beside the existing Cruise entry check: if the segment popped at least one balloon, never touched a >1HP balloon on that leg, and the backward circle-cast to `LastBouncePosition` is now clear, it awards a Sweep tap using the shared Cruise tap value and restarts the same tap-beat ease, then resets the segment state for the next leg. Publishes `ProjectileDestroyedMessage` and `BalanceBalloonsMessage` when shields reach zero, and `ShieldLostMessage` on each shield-spending wall bounce. Calls `_shieldView.OnBounce(oldDir, newDir, speed)` on wall bounces and balloon deflects to drive the shield field's squash dynamics. Neighbor nudging happens on the balloon side via `NudgeService`.
- **`ProjectileTrail`** — child MonoBehaviour on the trail GameObject. `Enable()`/`Disable()` manage `TrailRenderer` emitting state using `async UniTaskVoid` with `destroyCancellationToken`. Not `IPoolable` — lifecycle follows the pooled projectile parent.
- **`ProjectileShieldView`** — MonoBehaviour on the projectile prefab. Drives the `EMShieldField` shader via `MaterialPropertyBlock`: subscribes to `ShieldsRemaining` (reveal wipe on gain, noise dissolve on loss) and `ColorName` (tint) via UniRx. Per-frame `Update` steps the noise-scroll spring (`DampedSpring2D`) and squash spring (`DampedSpring1D`), runs the morph FSM, computes velocity-driven uniforms, and pushes all properties through the unified `WriteAllProperties()` method; tween callbacks use the same method, ensuring every `SetPropertyBlock` call writes the full property set. `OnBounce(oldDir, newDir, speed)` force-snaps to Bracing (circle), computes the impact normal as `normalize(newDir - oldDir)`, transforms it to local UV space, and injects a speed-scaled impulse into the squash spring. Spawns gain/lose/bounce VFX via `ParticlePoolChannel`.

## Interactions

- **ThrowerController** — gets/returns projectile via `PoolManager` + `ProjectilePoolChannel`; binds to a fresh `ProjectileModel`; reloads on `ProjectileDestroyedMessage`
- **SlotGrid** — queried for neighbor models to animate the nudge
- **BalloonView / BalloonModel** — collision target; color and stability state updated on hit
- **BalanceBalloonsMessage** — published on projectile death so the grid rebalances
- **ProjectileDestroyedMessage** — published on death to signal the thrower to reload
- **PoolManager** — provides `ParticlePoolChannel` for VFX and `ProjectilePoolChannel` for projectile lifecycle
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `NudgeDistance`, `NudgeDuration`
- **DisturbanceFieldService** — `ProjectileView` injects the shared disturbance field and calls `Stamp()` in `MoveAndBounce()` after position update, using the `Projectile` stamp profile from `DisturbanceFieldSettings`. On the first free frame it also emits the muzzle-exit force (`EmitFireBurst`): a cone of `ProjectileFire` stamps marched along the fire heading — count = that profile's `Interval` (repurposed), spaced by `Spacing` (\f$\text{length} \approx \text{Spacing} \times \text{count}\f$; 0 = Radius), with the radius growing (`RadiusGrowth`) and strength fading (`StrengthFalloff`) toward the far end (`0/0` = a uniform line) — tagged the reserved `Projectile` palette colour, with specks seeded along the same line first (`SpeckSpawnRequestMessage`, `SpeckSource.ProjectileFire`) so the stamps agitate them. Only the muzzle stamp reports impact (one bush-rustle per shot). It also publishes `ProjectileFiredMessage`. Creates visible wakes through Puff clouds. `Controller/PierceDischargeEffects` stamps the same field with `StampSource.PierceDischarge` when a piercing shot's plowed toughs discharge (see [Pierce & Discharge Feel](#pierce--discharge-feel))
- **TimeScaleService** (`Shared/Pause/`) — `Controller/PierceDischargeEffects` claims a brief real-time slow-mo dip under `TimeScaleSource.PierceDischarge` on each pierce discharge

- **SceneLightFieldService** — `ProjectileView` registers a small `Light` (radius/intensity from the `Scene Light` serialized fields) on the **first free frame** (when the shot fires, alongside the muzzle burst — it's dark while still held at the thrower), updates its `Position` to the transform each `Update`, and disposes the registration in `OnDespawned`. Its `PaletteIndex` follows the shot's colour (`UpdateGlowColor` sets it via `IGamePalette.IndexOfColor`), falling back to the `Sparks` palette entry while colourless — so the bullet casts a coloured point light into the scene-light field. This same light is the one that stretches into the pierce telegraph capsule (see [Pierce & Discharge Feel](#pierce--discharge-feel)); the shield-loss and pierce-spark flashes register short-lived lights of their own.

## Editor Gizmos

- **Sweep counting gizmo** (`ProjectileView`, `#if UNITY_EDITOR`) — visualizes the sweep warm-up progress toward `SweepTapThreshold`. Each time a sweep condition passes, a wire sphere is drawn at the wall-hit position; successive markers are linked by a line whose alpha ramps from dim (first) to bright (threshold). While counting, markers are orange; once the threshold is reached they turn green and remain visible for the rest of the shot (or until the next counting session begins). A faint line also connects the first marker back to `LastBouncePosition` to show the segment origin. Resets on despawn/spawn (new shot).
