# Projectile

The projectile is the ball fired by the thrower. It travels in a straight line, bounces off walls, and pops balloons on contact.

## Gameplay

Each shot starts loaded on the thrower. When fired it moves freely across the screen, reflecting off the top, left, and right boundaries. Each bounce costs one shield. When shields are exhausted the projectile is destroyed, the grid rebalances, and the thrower reloads.

On contact with a balloon the projectile absorbs that balloon's color. Consecutive hits of the same color increment the pop-count. After 3 consecutive same-color pops the projectile gains a shield and the counter resets. Neighboring balloons nudge outward from the impact point.

## Shield Visuals

The projectile carries N shield orbs (`SpriteRenderer` children) managed by `ProjectileShieldView`. Each orb represents one remaining shield and is slightly larger than the previous one. On load all orbs start at zero scale and scale up to match the starting shield count. When a shield is lost (wall bounce) the topmost visible orb scales to zero and `PSVFX_ShieldLose` plays. When a shield is gained (3 same-color pops) a new orb scales up and `PSVFX_ShieldGain` plays. Shield orbs tint to the current balloon color via DOTween color lerp. Wall bounces also spawn `PSVFX_ShieldBounce` at the impact point.

## How it works

- **`ProjectileLifetimeScope`** — child `GameChildLifetimeScope` on the projectile prefab root. Registers `ProjectileView` and `ProjectileShieldView` via `RegisterComponentInHierarchy`. Instantiated by `ThrowerController` via `parentScope.CreateChildFromPrefab()`, which wires the parent scope explicitly before activation — ensuring all parent services (messages, config, grid) are available.
- **`ProjectileModel`** — plain C# data object: direction vector, speed, `ReactiveProperty<int> ShieldsRemaining`, `ReactiveProperty<string> ColorName`, pop-count, last-hit balloon reference.
- **`ProjectileView`** — MonoBehaviour on the projectile prefab. Drives manual movement in `FixedUpdate`, checks bounds against `IGameConfiguration.LimitsClockwise`, reflects direction and clamps position on bounce. Handles `OnTriggerEnter2D` — resolves the `BalloonView` and `BalloonModel` from the collider, tracks color, triggers shield gain on 3 consecutive same-color pops, and runs the neighbor nudge sequence. Publishes `ProjectileDestroyedMessage` and `BalanceBalloonsMessage` when shields reach zero.
- **`ProjectileShieldView`** — MonoBehaviour on the projectile prefab. Subscribes to `ShieldsRemaining` and `ColorName` via UniRx. Scales shield orb sprites, tints them to the current color, and spawns gain/lose/bounce VFX.


## Interactions

- **ThrowerController** — instantiates `ProjectileView` and binds it to a fresh `ProjectileModel`; reloads on `ProjectileDestroyedMessage`
- **SlotGrid** — queried for neighbor models to animate the nudge
- **BalloonView / BalloonModel** — collision target; color and stability state updated on hit
- **BalanceBalloonsMessage** — published on projectile death so the grid rebalances
- **ProjectileDestroyedMessage** — published on death to signal the thrower to reload
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `NudgeDistance`, `NudgeDuration`
