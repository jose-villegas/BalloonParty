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
- Orbits the loaded projectile around the spawn point to preview the trajectory
- Updates the prediction trace line (via `PredictionTraceCalculator` and `ThrowerView`) showing the projected path with wall bounces
- Fires on mouse-up when all balloons have settled (unstable balloons block firing); clears the prediction trace on fire

When a `ProjectileDestroyedMessage` arrives, `ThrowerController` creates a new projectile automatically. The projectile prefab carries a `ProjectileLifetimeScope`, so instantiation uses `parentScope.CreateChildFromPrefab()` — this wires the parent scope before activation, ensuring all injected dependencies resolve correctly. The pool key is derived from the prefab's name (`_settings.ProjectileScopePrefab.name`), keeping it consistent with the VFX pooling convention.

## Interactions

- **SlotGrid** — queried to check balloon stability before allowing a shot
- **LifetimeScope** — the parent scope injected into `ThrowerController`; used to call `CreateChildFromPrefab` for projectile instantiation
- **ProjectileDestroyedMessage** — triggers reload
- **ProjectileLoadedMessage** — published after each load so shield UI can self-bind
- **IGameConfiguration** — provides `ThrowerSpawnPoint`, `ProjectileSpawnPoint`, `ProjectileSpeed`, `ProjectileStartingShields`
- **PredictionTraceCalculator / PredictionTraceView** — calculates and renders the aim trajectory line while the player holds the mouse button
