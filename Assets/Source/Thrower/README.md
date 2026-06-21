# Thrower

The thrower is the player-controlled launcher at the bottom of the screen. It aims at the balloon grid and fires projectiles.

## Contents

| File | What it does |
|---|---|
| `ThrowerController` | Plain C# class (`IStartable`, `ITickable`) — aiming, loading, firing, prediction trace, and reload logic |
| `ThrowerView` | MonoBehaviour — owns the thrower's transform, rotation, entrance animation, prediction trace display, and pointer input (`IsAiming`, `FireReleased`, `TryGetAimDirection` — the only place that touches `Input`/`Camera`) |
| `ThrowerLifetimeScope` | Child `LifetimeScope` on the Thrower GameObject — registers `ThrowerView` and `ThrowerController` |
| `ThrowerSettings` | Holds the `ProjectileLifetimeScope` prefab reference for pool creation |

## Gameplay

The player holds the mouse button to aim — the thrower rotates to face the cursor. Releasing the mouse fires the loaded projectile. Once the projectile is destroyed (shields depleted), the thrower automatically reloads and is ready for the next shot.

## How it works

`ThrowerController` is a plain C# class registered as an entry point in `ThrowerLifetimeScope`. It delegates all visual operations *and pointer input* to `ThrowerView`, keeping the controller free of Unity engine APIs. On start it registers the projectile pool and pre-warms two instances. It subscribes reactively to `Navigation.State` — when the state becomes `Game` (via `NavigationTrigger` on the Launch button), it plays the entrance animation. After the entrance animation completes, input is enabled.

Each frame (`Tick`), only when navigation state is `Game` and the entrance animation is complete, the controller:
- Updates its aim direction from the view's pointer read (`_view.IsAiming` / `TryGetAimDirection`)
- Tells the view to rotate to match that direction
- Eases the loaded projectile into the spawn point position using `Ease.OutBack`
- Updates the prediction trace line while the mouse button is held
- Fires on mouse-up

During `LevelUp` state, `Tick` is a no-op — the thrower cannot aim or fire while the level-up popup is visible.

When a `ProjectileDestroyedMessage` arrives, `ThrowerController` returns the old projectile to the pool and loads a new one immediately. The projectile prefab carries a `ProjectileLifetimeScope`, so instantiation uses `parentScope.CreateChildFromPrefab()` — this wires the parent scope before activation, ensuring all injected dependencies resolve correctly. The pool key is derived from the prefab's name.

## Interactions

- **LifetimeScope** — the parent scope; used to call `CreateChildFromPrefab` for projectile instantiation
- **ProjectileDestroyedMessage** — triggers reload
- **ProjectileLoadedMessage** — published after each load so shield UI can self-bind
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `ProjectileLoadDuration`
- **PredictionTraceCalculator / ThrowerView** — calculates and renders the aim trajectory line while the player holds the mouse button
