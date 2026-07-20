# Thrower

The thrower is the player-controlled launcher at the bottom of the screen. It aims at the balloon grid and fires projectiles.

## Contents

| File | What it does |
|---|---|
| `ThrowerController` | Plain C# class (`IStartable`, `ITickable`) — aiming, loading, firing, prediction trace, and reload logic |
| `ThrowerView` | MonoBehaviour — owns the thrower's transform, rotation, entrance animation, prediction trace display, and pointer input (`IsAiming`, `FireReleased`, `TryGetAimDirection` — the only place that touches `Input`/`Camera`) |
| `ThrowerLifetimeScope` | Child `LifetimeScope` on the Thrower GameObject — registers `ThrowerView` and `ThrowerController` (`.AsSelf()`, so editor tooling can resolve the concrete controller — see `FireAt` below) |
| `ThrowerSettings` | Holds the `ProjectileView` prefab reference for pool creation (registered in `GameLifetimeScope`) |

## Gameplay

The player holds the mouse button to aim — the thrower rotates to face the cursor. Releasing the mouse fires the loaded projectile. Once the projectile is destroyed (shields depleted), the thrower automatically reloads and is ready for the next shot.

## How it works

`ThrowerController` is a plain C# class registered as an entry point in `ThrowerLifetimeScope`. It delegates all visual operations *and pointer input* to `ThrowerView`, keeping the controller free of Unity engine APIs. On start it registers the projectile pool and pre-warms two instances. It subscribes reactively to `Navigation.State` — when the state becomes `Game` (via `NavigationTrigger` on the Launch button), it plays the entrance animation. After the entrance animation completes, input is enabled.

Each frame (`Tick`), only when navigation state is `Game` and the entrance animation is complete, the controller:
- Updates its aim direction from the view's pointer read (`_view.IsAiming` / `TryGetAimDirection`)
- Tells the view to rotate to match that direction
- Eases the loaded projectile into the spawn point position using `Ease.OutBack`
- Updates the prediction trace line while the mouse button is held, mirroring it into `PredictionTraceProvider` (`Prediction/`) so other readers (e.g. a balloon's `TraceHitMarker`) can react to the same-frame trace without depending on the Thrower's view chain
- Fires on mouse-up

`Tick` is a no-op outside the `Game` navigation state or while any `PauseService` source is paused — the thrower cannot aim or fire during the level-up ceremony, cinematics, or the overflow heart-drain lock.

`FireAt(Vector3 direction)` is an internal entry point bypassing mouse input entirely — it snaps the loaded shot to the spawn point, aims it at the given direction, and fires. It exists for editor tooling (the Shot Solver window and the Fire-Best-Shot cheat), which is why `ThrowerLifetimeScope` also registers the controller `.AsSelf()`.

When a `ProjectileDestroyedMessage` or a `LevelUpDismissedMessage` arrives, `ThrowerController` swaps the active projectile: the spent one plays its scale-away disappear animation and only returns to the pool once that finishes, while a fresh instance loads immediately — so the thrower never hands out a shot still mid-disappear. A `ScoreLevelUpMessage` (the level-up freeze) un-fires a shot that was fired the very same frame, before it ever took a physics step, so the dismissal swap doesn't scale-drift a phantom shot away from the muzzle. A `GameOverMessage` scales the active projectile away without loading a replacement (the thrower only reloads on restart). A `BoardClearMessage` or `RunResetMessage` triggers a synchronous reload — the old projectile returns to the pool immediately and a fresh one loads — so a fresh run or a cleared board starts with a fresh projectile (default shields and position). Projectiles are created through `ProjectilePoolChannel` (an `InjectingPoolChannel` — `[Inject]` fields resolved from the parent container, no child scope on the prefab). The pool key is derived from the prefab's name.

## Interactions

- **PoolManager / ProjectilePoolChannel** — registers and serves the projectile pool (pre-warmed with two instances)
- **ProjectileDestroyedMessage / LevelUpDismissedMessage** — trigger the scale-away swap (spent shot returns to pool once its disappear finishes; a fresh one loads immediately)
- **ScoreLevelUpMessage** — un-fires a shot fired the same frame the level-up freeze lands, before the dismissal swap runs
- **GameOverMessage** — scales the active projectile away with no replacement load
- **BoardClearMessage / RunResetMessage** — trigger a synchronous reload so a cleared board or a fresh run starts with a fresh projectile
- **PauseService** — any paused source blocks `Tick` (aim/fire)
- **ProjectileLoadedMessage** — published after each load so shield UI can self-bind
- **IGameConfiguration** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `ProjectileLoadDuration`, `PredictionTraceColor`
- **PredictionTraceCalculator / ThrowerView** — calculates and renders the aim trajectory line while the player holds the mouse button
- **PredictionTraceProvider** — written each `Tick` alongside the view (set on aim, cleared on fire/release/reload) so non-Thrower readers can find where the trace currently sits
