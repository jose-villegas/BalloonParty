# Thrower

The thrower is the player-controlled launcher at the bottom of the screen. It aims at the balloon grid and fires projectiles.

## Contents

| File | What it does |
|---|---|
| `ThrowerController` | Plain C# class (`IStartable`, `ITickable`) — aiming, loading, firing, prediction trace, and reload logic |
| `ThrowerView` | MonoBehaviour — owns the thrower's transform, rotation, entrance animation, and prediction trace display |
| `ThrowerLifetimeScope` | Child `LifetimeScope` on the Thrower GameObject — registers `ThrowerView` and `ThrowerController` |
| `ThrowerSettings` | Holds the `ProjectileLifetimeScope` prefab reference for pool creation |

## Gameplay

The player holds the mouse button to aim — the thrower rotates to face the cursor. Releasing the mouse fires the loaded projectile. Once the projectile is destroyed (shields depleted), the thrower automatically reloads and is ready for the next shot.

## How it works

`ThrowerController` is a plain C# class registered as an entry point in `ThrowerLifetimeScope`. It delegates all visual operations to `ThrowerView`. On start it slides into position from below the screen, then loads a projectile and waits for input. Each frame it:
- Updates its aim direction from the mouse cursor position
- Tells the view to rotate to match that direction
- Eases the loaded projectile into the spawn point position using `Ease.OutBack`
- Updates the prediction trace line while the mouse button is held
- Fires on mouse-up

When a `ProjectileDestroyedMessage` arrives, `ThrowerController` returns the old projectile to the pool and loads a new one immediately. The projectile prefab carries a `ProjectileLifetimeScope`, so instantiation uses `parentScope.CreateChildFromPrefab()` — this wires the parent scope before activation, ensuring all injected dependencies resolve correctly. The pool key is derived from the prefab's name.

## Interactions

- **LifetimeScope** — the parent scope; used to call `CreateChildFromPrefab` for projectile instantiation
- **ProjectileDestroyedMessage** — triggers reload
- **ProjectileLoadedMessage** — published after each load so shield UI can self-bind
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `ProjectileLoadDuration`
- **PredictionTraceCalculator / ThrowerView** — calculates and renders the aim trajectory line while the player holds the mouse button
