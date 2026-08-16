# Thrower

The thrower is the player-controlled launcher at the bottom of the screen. It aims at the balloon grid and fires projectiles.

## Contents

| File | What it does |
|---|---|
| `ThrowerController` | Plain C# class (`IStartable`, `ITickable`) — aiming, loading, firing, prediction trace, and reload logic |
| `ThrowerView` | MonoBehaviour — owns the thrower's transform, rotation, entrance animation, prediction trace display, and pointer input (`IsAiming`, `FireReleased`, `TryGetAimDirection` — the only place that touches `Input`/`Camera`) |
| `ThrowerLifetimeScope` | Child `LifetimeScope` on the Thrower GameObject — registers `ThrowerView` and `ThrowerController` (`.AsSelf()`, so editor tooling can resolve the concrete controller — see `FireAt` below) |
| `ThrowerSettings` | Holds the `ProjectileView` prefab reference for pool creation (registered in `GameLifetimeScope`) |
| `AimDirectionHistory` | Fixed-capacity ring buffer of (timestamp, direction) aim samples backing the aim latch below |

## Gameplay

The player holds the mouse button to aim — the thrower rotates to face the cursor. Releasing the mouse fires the loaded projectile. Once the projectile is destroyed (shields depleted), the thrower automatically reloads and is ready for the next shot.

The pointer itself always moves freely — `ThrowerView.TryGetAimDirection` just reports where it is. `ThrowerController` resolves the *angle* that produces through `ClampAndQuantizeAimDirection` before storing it: unlike quantization, the clamp into `[IProjectileFlightConfig.AimAngleMinDegrees, AimAngleMaxDegrees]` is never opt-in — it always applies, so the player can never aim sideways or below the horizontal, whatever the step. On top of the clamp, a positive `AimAngleStepDegrees` also snaps the angle to the nearest multiple of the step, so the aim (and everything derived from it — the thrower's own rotation, the prediction trace) jumps between fixed headings instead of following the cursor continuously; `AimAngleStepDegrees` defaults to 0, meaning continuous aim within the range (still clamped, just not snapped) — today's exact behaviour with the range added.

### Aim range — clamp before snap, on the grid index

`AimAngleMinDegrees`/`AimAngleMaxDegrees` (degrees from +X, matching `ShotBoardGather.DirectionFromDegrees`: 0 = due right, 90 = straight up) default to 5/175. The order the clamp and the snap happen in matters and is easy to get backwards: clamping the raw angle first and then snapping to the step grid can push the result back outside the range (the nearest grid line to a clamped boundary sample can sit past it); snapping first and then clamping the angle can land on a value that isn't a step multiple at all. Neither is acceptable — the reachable set must be exactly the step's multiples that lie within the range.

`ClampAndQuantizeAimDirection` sidesteps both failure modes by delegating to `AimAngleGrid.ClampToReachableAngle` (`Shared/`), which clamps the *rounded grid index* rather than the angle — the result is always both a step multiple and inside the range, by construction. This is the same absolute grid `AimAngleGrid.ResolveSweepAngle` sweeps with (see `Shared/README.md` and `Solver/README.md`), so the thrower's own clamp and every sweep built on that grid — the Fire Best Shot cheat, the Shot Solver window, and the Scene-view aim fan overlay, see `Editor/README.md` — can never disagree about which angles are reachable: one config-sourced range, consumed everywhere by construction rather than by convention.

`FireAt` (editor tooling) and `DirectionOverride` (the auto-fire cheat) both bypass the clamp entirely, same as they already bypass quantization — `FireBestShotCheat` sweeps within the configured range already, so its angles are in range by construction, and `ShotSolverWindow`'s own arc field is deliberately user-editable for experimentation beyond the player's reachable range.

### Aim latch — firing the direction from before release

The shot fires on touch release, but on a touchscreen the act of lifting a finger itself registers as a position change: a big or clumsy finger rolls off the glass and shifts the reported point, so the release-instant sample is the *least* trustworthy one in the whole gesture. Mouse input has no equivalent — a mouse-up doesn't drag the cursor with it — so this is a touch-only precision gap.

`ThrowerController` records every frame's (already-quantized) aim direction into `AimDirectionHistory`, a fixed-capacity ring buffer (`Time.time`, matching the scaled-time convention `_loadElapsed` already uses; capacity 32, chosen to comfortably outlast any sensible latch even at a pessimistic 10 fps). On release, instead of firing the live direction it looks up the newest sample at or before `now - IProjectileFlightConfig.AimLatchSeconds`, falling back to the oldest sample held if the latch reaches further back than the history goes. Because the latch stores the quantized direction — the same value `RotateTo` and the prediction trace already show the player — the fired shot exactly reproduces a heading the player actually saw a moment earlier, rather than re-deriving one from a raw sample. `AimLatchSeconds` defaults to 0, meaning fire the live direction — today's exact behaviour; a useful range to try first is roughly 60-120ms. The history is cleared on every fire, so a fast follow-up shot can't latch onto a direction recorded before the previous one. `FireAt` (editor tooling) and `DirectionOverride` (the auto-fire cheat) both bypass the latch entirely — they set the direction deliberately and fire it as-is.

Because firing now reads a moment in the past, the very last frame's prediction trace (drawn from the live direction) can differ slightly from where the shot actually goes — that's the intended trade: the player aimed at the pre-lift trace, not the post-lift one. A non-zero latch also delays mouse aiming's responsiveness by the same amount, since mouse input has no lift-off to correct for; at 60-120ms that delay is expected to be imperceptible against the reaction-time gains on touch.

## How it works

`ThrowerController` is a plain C# class registered as an entry point in `ThrowerLifetimeScope`. It delegates all visual operations *and pointer input* to `ThrowerView`, keeping the controller free of Unity engine APIs. On start it registers the projectile pool and pre-warms two instances. It subscribes reactively to `Navigation.State` — when the state becomes `Game` (via `NavigationTrigger` on the Launch button), it plays the entrance animation. After the entrance animation completes, input is enabled.

Each frame (`Tick`), only when navigation state is `Game` and the entrance animation is complete, the controller:
- Updates its aim direction from the view's pointer read (`_view.IsAiming` / `TryGetAimDirection`)
- Tells the view to rotate to match that direction
- Eases the loaded projectile into the spawn point position using `Ease.OutBack`
- Updates the prediction trace line while the mouse button is held, mirroring it into `PredictionTraceProvider` (`Prediction/`) so other readers (e.g. a balloon's `TraceHitMarker`) can react to the same-frame trace without depending on the Thrower's view chain
- Fires on mouse-up

`Tick` is a no-op outside the `Game` navigation state, while any `PauseService` source is paused, or while `HoldSpeedUpController.ConsumedInput` is true — the thrower cannot aim or fire during the level-up ceremony, cinematics, the overflow heart-drain lock, or while the player's finger is still down from a speed-up hold. The `ConsumedInput` gate prevents the finger-lift that ended a speed-up from accidentally aiming or firing the next shot; the player must lift and tap fresh.

`FireAt(Vector3 direction)` is an internal entry point bypassing mouse input entirely — it snaps the loaded shot to the spawn point, aims it at the given direction, and fires. It exists for editor tooling (the Shot Solver window and the Fire-Best-Shot cheat), which is why `ThrowerLifetimeScope` also registers the controller `.AsSelf()`.

When a `ProjectileDestroyedMessage` or a `LevelUpDismissedMessage` arrives, `ThrowerController` swaps the active projectile: the spent one plays its scale-away disappear animation and only returns to the pool once that finishes, while a fresh instance loads immediately — so the thrower never hands out a shot still mid-disappear. A `ScoreLevelUpMessage` (the level-up freeze) un-fires a shot that was fired the very same frame, before it ever took a physics step, so the dismissal swap doesn't scale-drift a phantom shot away from the muzzle. A `GameOverMessage` scales the active projectile away without loading a replacement (the thrower only reloads on restart). A `BoardClearMessage` or `RunResetMessage` triggers a synchronous reload — the old projectile returns to the pool immediately and a fresh one loads — so a fresh run or a cleared board starts with a fresh projectile (default shields and position). Projectiles are created through `ProjectilePoolChannel` (an `InjectingPoolChannel` — `[Inject]` fields resolved from the parent container, no child scope on the prefab). The pool key is derived from the prefab's name.

## Interactions

- **PoolManager / ProjectilePoolChannel** — registers and serves the projectile pool (pre-warmed with two instances)
- **ProjectileDestroyedMessage / LevelUpDismissedMessage** — trigger the scale-away swap (spent shot returns to pool once its disappear finishes; a fresh one loads immediately)
- **ScoreLevelUpMessage** — un-fires a shot fired the same frame the level-up freeze lands, before the dismissal swap runs
- **GameOverMessage** — scales the active projectile away with no replacement load
- **BoardClearMessage / RunResetMessage** — trigger a synchronous reload so a cleared board or a fresh run starts with a fresh projectile
- **PauseService** — any paused source blocks `Tick` (aim/fire)
- **HoldSpeedUpController** (`Projectile/Controller/`) — `ConsumedInput` blocks `Tick` while the player's speed-up hold hasn't been released yet
- **ProjectileLoadedMessage** — published after each load so shield UI can self-bind
- **IProjectileFlightConfig** — provides `LimitsClockwise`, `ProjectileSpeed`, `ProjectileStartingShields`, `ProjectileLoadDuration`, `AimAngleMinDegrees`/`AimAngleMaxDegrees` (5/175 by default, always in effect), `AimAngleStepDegrees` (0 = continuous aim, the default), `AimLatchSeconds` (0 = fire the live direction, the default); **IPredictionTraceConfig** — provides `LineColor` plus the optional capsule-light tuning (`LightingEnabled` and friends — see `Prediction/README.md`)
- **PredictionTraceCalculator / ThrowerView** — calculates and renders the aim trajectory line while the player holds the mouse button
- **PredictionTraceProvider** — written each `Tick` alongside the view (set on aim, cleared on fire/release/reload) so non-Thrower readers can find where the trace currently sits
- **PredictionTraceLights** (`Prediction/`) — mirrors the same trace into optional per-leg capsule scene-lights, off by default; see `Prediction/README.md`
- **ThrowerOriginProvider** — set once in `Start()` from the spawn point transform and the prefab's own collider-derived contact radius (`ContactRadius.FromCollider`, read off the prefab since nothing has been fired yet). Same shape as `ProjectilePositionProvider` and `PredictionTraceProvider`: a plain game-scope singleton a view in the thrower's own child scope fills in, so a system registered above `ThrowerLifetimeScope` — `ItemAssigner`, planning a shield chain — can read a launch point it could never inject directly. `IsAvailable` is false until `Start()` runs, and `ItemAssigner` treats that as "no thrower yet" rather than an error: any shield grant requested before the thrower has started falls back to the plain weighted draw instead of a planned chain
