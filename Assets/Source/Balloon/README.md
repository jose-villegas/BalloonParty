# Balloon

Represents balloons in the game — their state, appearance, spawning, and destruction.

## Folder structure

| Folder | What it owns |
|---|---|
| `Model/` | `BalloonModel` — pure C# data class with reactive properties (`Color`, `SlotIndex`, `IsStable`) |
| `View/` | `BalloonView` — MonoBehaviour that binds to a model and renders color, shadow, sorting order, and pop VFX |
| `Controller/` | `BalloonController` — mediator that wires model to view and handles hit destruction; `BalloonBalancer` — rebalances the grid when gaps appear |
| `Spawner/` | `BalloonSpawner` — creates balloon lines at game start and after each projectile death; `BalloonSpawnerSettings` — holds the balloon prefab reference |
| `PowerUps/` | Power-up balloon types (Phase 8) |

## Behaviour

A balloon knows its color, where it sits in the grid, and whether it has settled into position. When any of these change, the view updates automatically via UniRx subscriptions.

When a gap appears in the grid (because a balloon was destroyed), `BalloonBalancer` scans for unbalanced balloons and moves them upward along the best available path, animating smoothly into their new slot. A balloon is marked unstable for the duration of that animation.

When a projectile hits a balloon, `BalloonController` receives the `BalloonHitMessage` (filtered to its own model), plays the pop particle effect via `VfxPoolChannel`, removes the model from the slot grid, destroys the view GameObject, and publishes a `BalanceBalloonsMessage` so the remaining balloons settle.

## Interactions

- **SlotGrid** — balloons occupy positions in it; spawner places, controller removes
- **BalloonBalancer** — moves balloons when gaps appear below
- **ProjectileView** — triggers destruction on collision via `BalloonHitMessage`
- **ScoreController** — records each hit via `BalloonHitMessage`
- **PoolManager** — `BalloonView` uses `VfxPoolChannel` for pop VFX
- **IGameConfiguration** — balloon colors, spawn animation timing, balance timing
