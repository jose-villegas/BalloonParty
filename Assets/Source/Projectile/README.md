# Projectile

The projectile is the ball fired by the thrower. It travels in a straight line, bounces off walls, and pops balloons on contact.

## Gameplay

Each shot starts loaded on the thrower. When fired it moves freely across the screen, reflecting off the top, left, and right boundaries. Each bounce costs one shield. When shields are exhausted the projectile is destroyed, the grid rebalances, and the thrower reloads.

On contact with a balloon the projectile absorbs that balloon's color. Consecutive hits of the same color increment the pop-count. From the second consecutive same-color hit onward (count ≥ 2), each hit grants the projectile an additional shield — the counter is not reset, so a long same-color streak continues awarding shields on every hit. Neighboring balloons nudge outward from the impact point using slot-based positions (logical grid positions, not visual transform positions) to ensure consistent nudge direction regardless of in-progress animations.

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

## How it works

- **`ProjectileLifetimeScope`** — child `GameChildLifetimeScope` on the projectile prefab root. Registers `ProjectileView` and `ProjectileShieldView` via `RegisterComponentInHierarchy`. Created by `ProjectilePoolChannel` via `parentScope.CreateChildFromPrefab()`.
- **`ProjectilePoolChannel`** — `PoolChannel<ProjectileView>` that creates projectiles via `CreateChildFromPrefab`. Accessed through `PoolManager`.
- **`IProjectileModel`** — read-only interface exposing `IReadOnlyReactiveProperty<string> ColorName`, `IReadOnlyReactiveProperty<int> ShieldsRemaining`, and read-only plain properties (`Direction`, `Speed`, `IsFree`, `ColorPopCount`, `LastHitBalloon`). Used by shield UI and views that only observe state.
- **`IWriteableProjectileModel`** — mutable interface extending `IProjectileModel`; re-declares reactive properties as `ReactiveProperty<T>` (via `new` keyword) and adds setters. Used by `ProjectileView`, `ThrowerController`, and cheats that mutate state.
- **`ProjectileModel`** — concrete class implementing `IWriteableProjectileModel`. Only referenced at creation sites (`ThrowerController.LoadProjectile`).
- **`ProjectileView`** — MonoBehaviour implementing `IPoolable`. Drives manual movement in `FixedUpdate`, checks bounds against `IGameConfiguration.LimitsClockwise`, reflects direction and clamps position on bounce. Handles `OnTriggerEnter2D` — resolves the `BalloonView` from the collider, tracks absorbed color, triggers shield gain on 2 consecutive same-color pops, and publishes `BalloonHitMessage`. Publishes `ProjectileDestroyedMessage` and `BalanceBalloonsMessage` when shields reach zero. Neighbor nudging is handled by `BalloonNudgeHandler` on the balloon side.
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
