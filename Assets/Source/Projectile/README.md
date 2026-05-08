# Projectile

The projectile is the ball fired by the thrower. It travels in a straight line, bounces off walls, and pops balloons on contact.

## Gameplay

Each shot starts loaded on the thrower. When fired it moves freely across the screen, reflecting off the top, left, and right boundaries. Each bounce costs one shield. When shields are exhausted the projectile is destroyed, the grid rebalances, and the thrower reloads.

On contact with a balloon the projectile absorbs that balloon's color. Consecutive hits of the same color increment the pop-count, which feeds into scoring (Phase 6). Neighboring balloons nudge outward from the impact point.

## How it works

- **`ProjectileModel`** — plain C# data object: direction vector, speed, shield count, color tracking, last-hit balloon reference (prevents double-hit in the same frame).
- **`ProjectileView`** — MonoBehaviour on the projectile prefab. Drives manual movement in `FixedUpdate` (direction × speed × fixedDeltaTime), checks bounds against `IGameConfiguration.LimitsClockwise`, reflects direction and clamps position on bounce. Handles `OnTriggerEnter2D` directly — resolves the `BalloonView` and `BalloonModel` from the collider, tracks color, and runs the neighbor nudge sequence. Publishes `ProjectileDestroyedMessage` and `BalanceBalloonsMessage` when shields reach zero.

## Interactions

- **ThrowerController** — instantiates `ProjectileView` and binds it to a fresh `ProjectileModel`; reloads on `ProjectileDestroyedMessage`
- **SlotGrid** — queried for neighbor models to animate the nudge
- **BalloonView / BalloonModel** — collision target; color and stability state updated on hit
- **BalanceBalloonsMessage** — published on projectile death so the grid rebalances
- **ProjectileDestroyedMessage** — published on death to signal the thrower to reload
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `NudgeDistance`, `NudgeDuration`
