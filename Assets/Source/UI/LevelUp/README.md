# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` and a `CinematicEndGate(LevelCompleteHit)` by concrete type (not `.As<IReadyGate>()`) so the popup names the exact gate it waits on, plus `RegisterMetricLabels(this)` (see `UI/Telemetry/`), which binds any `MetricLabel` under the popup to `ILevelMetricsView` and no-ops when there are none |
| `LevelUpPopUp` | Holds a `PauseService` pause while it waits for the level-complete hit cinematic to end (via an injected `CinematicEndGate`), then freezes time via `TimeScaleService`, shows the popup, spawns glow trails from each `ColorProgressBar` to the glow fill, and publishes `LevelUpDismissedMessage` on Continue |

## How it works

### Sequence

1. **Hit beat ends** — `LevelUpCinematic` ends the level-complete hit phase after the authored `LevelCompleteHit` curve duration elapses (duration-based, not wall-hit triggered), setting `CinematicState` to `None`.
2. **Gate opens** — `CinematicEndGate(LevelCompleteHit)` unblocks: `Cinematic.Current != LevelCompleteHit` is now true.
3. **Popup shows** — `LevelUpPopUp.ShowAfterGateAsync` claims a `PauseService` pause (`PauseSource.LevelUp`) the moment `ScoreLevelUpMessage` arrives, before it even awaits the gate. Once the gate opens it claims `TimeScaleSource.LevelUpPopup = 0` via `TimeScaleService` (effective `Time.timeScale` drops to 0), triggers the `"Appear"` animator, and waits for the appear animation to finish. The level label initially shows the old level.
4. **Glow trails** — After the appear animation completes, `LevelUpPopUp` publishes `LevelUpGlowTrailsMessage` (triggers `ColorProgressBar.DrainSliderAsync` to drain each bar in sync), then spawns decorative `FlyingTrail` orbs from each bar's random position to random offsets around the glow fill centre. Trails fly in unscaled time (`Spawn(..., useUnscaledTime: true)`), staggered across waves (`_glowTrailsPerBar` waves × the message's completed-colors count). As each trail arrives, `_levelGlowFill.fillAmount` advances proportionally; once all trails arrive, the level label updates to the new level.
5. **Player taps Continue** — `OnContinue()` triggers `"Hide"` and calls `Resume()` synchronously (no delay), which publishes `LevelUpDismissedMessage`, releases the popup's `TimeScaleService` claim, and resumes the `PauseService` pause (`PauseSource.LevelUp`).
6. **Level advances (two-phase commit)** — `LevelController` receives `LevelUpDismissedMessage` and *now* advances the `Level` integer to the pending value, resets progress, and flips `LevelUpPhase` from `Pending` to `Transitioning`. The label animated old→new during the popup (step 4), but the authoritative `Level` only changes here — see `Game/Level/README.md`.
7. **Bar reset** — Each `ColorProgressBar` receives `LevelUpDismissedMessage` and applies the stashed new max value, resetting progress to zero.
8. **Ascent + navigate** — the phase flip to `Transitioning` triggers `LevelTransitionController` (the Ascent), which slides the new level in. The camera un-zoom was already started at `EndPanIn` by `LevelUpCinematic` (via `CinematicCameraRig.RestoreCurveDriven`) — the Ascent does not touch the camera. `LevelController` owns the nav return to `Game` once `LevelTransitionCompletedMessage` arrives. There is **no** `LevelCompleteRestore` cinematic state played — it's kept in the enum only for serialized-index stability; its curve is read for the `RestoreCurveDriven` call.

The Animator's `updateMode` is set to `UnscaledTime` in `Start()`, so animations play even while the game is paused.

### Glow trail spawning

`LevelUpPopUp` owns a per-color dictionary of `TrailSpawner` instances (pool key `GlowTrail_{colorName}`, sorting order 3200). For each wave, it iterates the message's completed colors, reads the bar's position via `_scoreTrailService.GetTarget(colorName).RandomPosition()`, picks a random offset within `_glowTargetRadiusMultiplier` of the glow fill radius, and calls `Spawn` with `useUnscaledTime: true`. Arrival increments `_glowTrailArrivedCount` and fills the glow proportionally. Trails reuse the same `SimplePoolChannel<FlyingTrail>` factory as score trails but are pooled under separate keys.

### Gate pattern

`LevelUpLifetimeScope` registers `CinematicEndGate(CinematicState.LevelCompleteHit)` **by concrete type**, and `LevelUpPopUp` injects `CinematicEndGate` directly — not `IReadyGate`. That keeps the dependency explicit and stops the popup from silently resolving the parent scope's `NavigationReadyGate(Game)` if this registration ever went missing. Both are `IReadyGate` implementations over `UniTask.WaitUntil` on a reactive property, just on different state machines — though no current consumer actually injects the interface; every gate, including `GridSpawnerCoordinator`'s `NavigationReadyGate`, is injected by concrete type.

```
CinematicEndGate(LevelCompleteHit) → opens when Cinematic.Current != LevelCompleteHit
NavigationReadyGate(Game)      → opens when Navigation.Current == Game
```

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` animator), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- Registered in `LevelUpLifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.

## Interactions

- **`LevelController`** (`Game/Level/`) — publishes `ScoreLevelUpMessage` (triggers `ShowAfterGateAsync`) and transitions navigation to `LevelUp`; on dismissal advances the level (two-phase commit) and flips `LevelUpPhase` to drive the Ascent
- **`LevelUpCinematic`** — opens the gate by ending the level-complete hit beat; on `LevelUpDismissedMessage` finalizes its own session state (no resume, no navigation — the Ascent handles the camera and `LevelController` owns the nav return to `Game`)
- **`LevelUpLifetimeScope`** — registers this component and provides the `CinematicEndGate` injection
- **`ColorProgressBar`** — receives `LevelUpGlowTrailsMessage` to drain its slider in sync with glow trail waves; receives `LevelUpDismissedMessage` to apply the new max and reset progress
- **`ScoreTrailService`** — provides trail target positions for glow trail origin and the `FlyingTrail` prefab for pool channel creation
- **`PoolManager`** — hosts per-color `GlowTrail_{colorName}` pools created lazily by popup
- **`GamePalette`** — provides palette entries (color names and tints) for iterating glow trails
