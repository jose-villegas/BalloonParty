# Projectile

The projectile is the ball fired by the thrower. It travels in a straight line, bounces off walls, and pops balloons on contact.

## Gameplay

Each shot starts loaded on the thrower. When fired it moves freely across the screen, reflecting off the top, left, and right boundaries. Each bounce costs one shield. When shields are exhausted the projectile is destroyed, the grid rebalances, and the thrower reloads.

On contact with a balloon the projectile absorbs that balloon's color. The shared `ColorStreakTracker` (`Game/Score/`) tracks consecutive same-color pops; from the second consecutive same-color pop onward (streak ≥ 2), each pop grants the projectile an additional shield — a long same-color streak keeps awarding shields on every hit until a different color (or a scatter pop) breaks it. Neighboring balloons nudge outward from the impact point using slot-based positions (logical grid positions, not visual transform positions) to ensure consistent nudge direction regardless of in-progress animations.

When a hit returns `HitOutcome.Absorb`, `ProjectileHitResolver` dispatches `ActorHitMessage(Absorb)`, sets `model.IsFree = false`, and returns `ProjectileHitVisual.Destroyed` — the view then calls `DestroyProjectile()`. Unbreakable balloons may return `Absorb` from `EvaluateHit` to absorb the projectile without being destroyed themselves.

## Turn-based flow

The game follows a turn-based animation pipeline:

1. **Projectile flight** — bounces, pops balloons, nudges neighbors. No rebalancing during flight.
2. **Projectile death** — publishes `BalanceBalloonsMessage` (fallback) and `ProjectileDestroyedMessage`.
3. **Spawn + balance** — `BalloonSpawner` spawns new lines, then publishes `BalanceBalloonsMessage` after all lines are placed.

Balance was intentionally moved to post-death because mid-flight rebalancing caused animation conflicts — competing tweens, double-occupation visuals, and stale balance paths.

## Pooling

The projectile is **pooled, not destroyed**. A single instance is created via `ProjectilePoolChannel` on first load and reused across turns. On death the projectile publishes messages but does not self-destruct — `ThrowerController` returns it to the pool and immediately re-gets it.

- **`OnDespawned()`** — nulls model, resets glow, disables trail via `ProjectileTrail.Disable()`, resets shield view
- **`OnSpawned()`** — resets shield-shown flag; trail stays disabled until the projectile is fired

Trail management is handled by `ProjectileTrail`, a child component on the trail GameObject. It is **not** `IPoolable` — the projectile itself is pooled, so its children's lifecycle follows the parent. `ProjectileTrail` exposes `Enable()` / `Disable()`:

- **`Enable()`** — `async UniTaskVoid` that yields one frame (`UniTask.Yield(destroyCancellationToken)`), clears the trail, then re-enables emitting (prevents snap artifact from position change)
- **`Disable()`** — stops emitting and clears immediately

`ProjectileView` calls `Enable()` on the first `FixedUpdate` frame where `IsFree` is true (fired) and `Disable()` on death and despawn.

Shield orbs are hidden until the projectile is fired. `ProjectileShieldView` starts inactive on `Awake()` and is shown via `Show()` on the first `FixedUpdate` frame where `IsFree` is true.

## Shield Visuals

The projectile carries N shield orbs (`SpriteRenderer` children) managed by `ProjectileShieldView`. Each orb represents one remaining shield and is slightly larger than the previous one. On load all orbs start at zero scale and scale up to match the starting shield count. When a shield is lost (wall bounce) the topmost visible orb scales to zero and `PSVFX_ShieldLose` plays. When a shield is gained (2 same-color pops) a new orb scales up and `PSVFX_ShieldGain` plays. Shield orbs tint to the current balloon color via DOTween color lerp. Wall bounces also spawn `PSVFX_ShieldBounce` at the impact point.

All VFX are spawned via `ParticlePoolChannel` as world-space orphans — they are not children of the projectile and survive recycling independently.

## Buffs (`Buffs/`)

A **projectile buff** is a temporary modifier on the active projectile. The seam is deliberately general — buffs are not tied to items, and *when* a buff ends is a pluggable abstraction the buff itself declares, not a central dispatcher switching on a kind. The two abstractions live in `Model/`, the implementations in `Buffs/`:

- **`IProjectileBuff`** (`Model/`) — the contract requires a buff to provide an `IProjectileBuffEndCondition`. The buff is otherwise just *identity* (its type, for `HasBuff<T>()`) + *effects* (which live elsewhere).
- **`IProjectileBuffEndCondition`** (`Model/`) — the "when it ends" abstraction: exposes only `IReadOnlyReactiveProperty<bool> Expired`. An implementation encapsulates its own lifecycle logic and flips the bool once. `WallBounceEndCondition` (subscribes to `ShieldLostMessage`, ends on the first wall bounce) is the only one today; a `Timer`/`PopCount`/... end-condition is a new implementer — no context, no switch, no change to any buff.
- **`IProjectileBuffs.Apply(IProjectileBuff)`** — the activation seam, injectable anywhere. Just takes the buff; the buff already carries its end-condition, so `_buffs.Apply(new RainbowShieldProjectileBuff(wallBounces))` is the whole call.
- **`ProjectileBuffService`** — `IStartable` owning storage + lifecycle: tracks the active projectile (`ProjectileLoadedMessage`), applies buffs onto it, and drops a buff the first time its `EndCondition.Expired` fires. Knows nothing about any buff's effect or end-condition — it only observes the exposed signal.
- **`RainbowShieldProjectileBuff`** — the first buff (applied by a rainbow-held Shield). It constructs its own `WallBounceEndCondition` from the injected `ISubscriber<ShieldLostMessage>`. Its effects live in the layers that own their dependencies and query `HasBuff<RainbowShieldProjectileBuff>()`: the glow cycle in `ProjectileView`, and in `ProjectileHitResolver` the colour-agnostic streak flag (`DamageFlags.WildcardStreak`), piercing (`DamageFlags.Piercing` — plows through tough/unbreakable balloons instead of one-shotting/deflecting), and the pop-neighbour rainbow conversion.

The model stores buffs in a plain list exposed via `HasBuff<T>()` (read) and `AddBuff`/`RemoveBuff` (write); a fresh `ProjectileModel` per throw resets them for free.

## How it works

- **`ProjectilePoolChannel`** — `InjectingPoolChannel<ProjectileView>` that creates projectiles via `IObjectResolver` injection from the parent container (no child scope on the prefab). Accessed through `PoolManager`.
- **`Controller/ProjectileHitResolver`** — plain C# singleton owning the hit rules: calls `balloon.EvaluateHit(new DamageContext(1, Normal, projectileColor))` for the pre-computed outcome, applies the colour-steal (projectile absorbs a popped balloon's color) and the streak-shield rule (reads `ColorStreakTracker` immediately after dispatch — `IHitDispatcher` guarantees the score stage already ran), dispatches the `ActorHitMessage` through `IHitDispatcher`, and returns a `ProjectileHitVisual` for the view to play. When the projectile carries a `RainbowShieldProjectileBuff` it also flags the hit `WildcardStreak | Piercing` (colour-agnostic scoring + plows through tough/unbreakable balloons) and rainbow-converts the popped balloon's hex neighbours via the injected `SlotGrid`.
- **`Controller/ProjectileHitVisual`** — enum result of a resolve (`None`, `Recolored`, `Destroyed`) so the view knows which feedback to play without re-deriving the rules.
- **`Controller/ProjectileMotionResolver`** — plain C# singleton owning the flight rules: advances one fixed step, wall-bounces via `WallLimits`, decrements shields, and decides destroy-vs-continue, mutating the model's direction/shields. `ProjectileView.MoveAndBounce` just applies the returned `ProjectileStep` (transform, bounce VFX, `ShieldLostMessage`, disturbance stamp); `Deflect` reflects off a balloon surface normal. Headless-testable — see `ProjectileMotionResolverTests`.
- **`Controller/ProjectileStep`** — result of one advance (`Moved` / `Bounced` / `Destroyed` + resulting position and direction) that the view presents without re-deriving the rules.
- **`IProjectileModel`** — read-only interface exposing `IReadOnlyReactiveProperty<string> ColorName`, `IReadOnlyReactiveProperty<int> ShieldsRemaining`, read-only plain properties (`Direction`, `Speed`, `IsFree`, `LastHitBalloon`), and `HasBuff<T>()` (see [Buffs](#buffs-buffs)). Used by shield UI and views that only observe state.
- **`IWriteableProjectileModel`** — mutable interface extending `IProjectileModel`; re-declares reactive properties as `ReactiveProperty<T>` (via `new` keyword), adds setters, and adds `AddBuff`/`RemoveBuff`. Used by `ProjectileView`, `ThrowerController`, the buff service, and cheats that mutate state.
- **`ProjectileModel`** — concrete class implementing `IWriteableProjectileModel`. Only referenced at creation sites (`ThrowerController.LoadProjectile`).
- **`ProjectilePositionProvider`** — singleton holding the live projectile transform for systems that need its position without a reference to the view (set on load, cleared on reload).
- **`ProjectileView`** — MonoBehaviour implementing `IPoolable`. Drives manual movement in `FixedUpdate` (skipped while `PauseService.IsAnyPaused`), checks bounds against `IGameConfiguration.LimitsClockwise`, reflects direction and clamps position on bounce. Handles `OnTriggerEnter2D` — resolves the `BalloonView` via `GetComponent<BalloonView>()` on the collider (O(1) when the collider lives on the same GameObject as `BalloonView`) and hands the collision to `ProjectileHitResolver`, playing the returned `ProjectileHitVisual`. Publishes `ProjectileDestroyedMessage` and `BalanceBalloonsMessage` when shields reach zero, and `ShieldLostMessage` on each shield-spending wall bounce. Neighbor nudging happens on the balloon side via `NudgeService`.
- **`ProjectileTrail`** — child MonoBehaviour on the trail GameObject. `Enable()`/`Disable()` manage `TrailRenderer` emitting state using `async UniTaskVoid` with `destroyCancellationToken`. Not `IPoolable` — lifecycle follows the pooled projectile parent.
- **`ProjectileShieldView`** — MonoBehaviour on the projectile prefab. Subscribes to `ShieldsRemaining` and `ColorName` via UniRx. Scales shield orb sprites, tints them to the current color, and spawns gain/lose/bounce VFX via `ParticlePoolChannel`.

## Interactions

- **ThrowerController** — gets/returns projectile via `PoolManager` + `ProjectilePoolChannel`; binds to a fresh `ProjectileModel`; reloads on `ProjectileDestroyedMessage`
- **SlotGrid** — queried for neighbor models to animate the nudge
- **BalloonView / BalloonModel** — collision target; color and stability state updated on hit
- **BalanceBalloonsMessage** — published on projectile death so the grid rebalances
- **ProjectileDestroyedMessage** — published on death to signal the thrower to reload
- **PoolManager** — provides `ParticlePoolChannel` for VFX and `ProjectilePoolChannel` for projectile lifecycle
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `NudgeDistance`, `NudgeDuration`
- **DisturbanceFieldService** — `ProjectileView` injects the shared disturbance field and calls `Stamp()` in `MoveAndBounce()` after position update, using the `Projectile` stamp profile from `DisturbanceFieldSettings`. On the first free frame it also emits the muzzle-exit force (`EmitFireBurst`): a cone of `ProjectileFire` stamps marched along the fire heading — count = that profile's `Interval` (repurposed), spaced by `Spacing` (length ≈ Spacing × count; 0 = Radius), with the radius growing (`RadiusGrowth`) and strength fading (`StrengthFalloff`) toward the far end (`0/0` = a uniform line) — tagged the reserved `Projectile` palette colour, with specks seeded along the same line first (`SpeckSpawnRequestMessage`, `SpeckSource.ProjectileFire`) so the stamps agitate them. Only the muzzle stamp reports impact (one bush-rustle per shot). It also publishes `ProjectileFiredMessage`. Creates visible wakes through Puff clouds

- **SceneLightFieldService** — `ProjectileView` registers a small `Light` (radius/intensity from the `Scene Light` serialized fields) in `Bind`, updates its `Position` to the transform each `Update`, and disposes the registration in `OnDespawned`. Its `PaletteIndex` follows the shot's colour (`UpdateGlowColor` sets it via `IGamePalette.IndexOfColor`), falling back to the `Sparks` palette entry while colourless — so the bullet casts a coloured point light into the scene-light field. First gameplay consumer of the field (@ref plan_lighting "Milestone 3").
