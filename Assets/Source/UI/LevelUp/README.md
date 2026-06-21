# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` and a `CinematicEndGate(LevelUpPanIn)` as `IReadyGate` |
| `LevelUpPopUp` | Waits for the pan-in cinematic to end (via `IReadyGate`), freezes time, shows the popup, spawns glow trails from each `ColorProgressBar` to the glow fill, and publishes `LevelUpDismissedMessage` on Continue |

## How it works

### Sequence

1. **Pan-in ends** — `LevelUpTrailEffect` calls `EndCinematic()` after the tipping trail arrives, setting `CinematicState` to `None`.
2. **Gate opens** — `CinematicEndGate(LevelUpPanIn)` unblocks: `Cinematic.Current != LevelUpPanIn` is now true.
3. **Popup shows** — `LevelUpPopUp.ShowAfterGateAsync` sets `Time.timeScale = 0f`, triggers the `"Appear"` animator, and waits for the appear animation to finish. The level label initially shows the old level.
4. **Glow trails** — After the appear animation completes, `LevelUpPopUp` publishes `LevelUpGlowTrailsMessage` (triggers `ColorProgressBar.DrainSliderAsync` to drain each bar in sync), then spawns decorative `FlyingTrail` orbs from each bar's random position to random offsets around the glow fill centre. Trails fly in unscaled time (`SpawnUnscaled`), staggered across waves (`_glowTrailsPerBar` waves × palette color count). As each trail arrives, `_levelGlowFill.fillAmount` advances proportionally; once all trails arrive, the level label updates to the new level.
5. **Player taps Continue** — `OnContinue()` triggers `"Hide"`, publishes `LevelUpDismissedMessage`, and starts `ResumeAfterDelayAsync` (a configurable settle delay).
6. **Bar reset** — Each `ColorProgressBar` receives `LevelUpDismissedMessage` and applies the stashed new max value, resetting progress to zero.
7. **Restore cinematic** — `LevelUpTrailEffect` receives `LevelUpDismissedMessage` and starts `CinematicState.LevelUpRestore` — tweens `Time.timeScale` back to 1 and camera back to its base position/size.
8. **Navigate** — once restore completes, `LevelUpTrailEffect` calls `Navigation.TransitionTo(Game)`.

The Animator's `updateMode` is set to `UnscaledTime` in `Start()`, so animations play even while the game is paused.

### Glow trail spawning

`LevelUpPopUp` owns a per-color dictionary of `TrailSpawner` instances (pool key `GlowTrail_{colorName}`, sorting order 3200). For each wave, it iterates every palette color, reads the bar's position via `_scoreTrailService.GetTarget(colorName).RandomPosition()`, picks a random offset within `_glowTargetRadiusMultiplier` of the glow fill radius, and calls `SpawnUnscaled`. Arrival increments `_glowTrailArrivedCount` and fills the glow proportionally. Trails reuse the same `SimplePoolChannel<FlyingTrail>` factory as score trails but are pooled under separate keys.

### Gate pattern

`LevelUpLifetimeScope` registers `new CinematicEndGate(CinematicState.LevelUpPanIn)` as `IReadyGate`. This mirrors how `GameLifetimeScope` registers `NavigationReadyGate(NavigationState.Game)` — both use `UniTask.WaitUntil` on a reactive property, just on different state machines.

```
CinematicEndGate(LevelUpPanIn) → opens when Cinematic.Current != LevelUpPanIn
NavigationReadyGate(Game)      → opens when Navigation.Current == Game
```

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` animator), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- Registered in `LevelUpLifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.

## Interactions

- **`ScoreController`** — publishes `ScoreLevelUpMessage` (triggers `ShowAfterGateAsync`) and transitions navigation to `LevelUp`
- **`LevelUpTrailEffect`** — owns both cinematics; opens the gate by ending the pan-in cinematic; starts restore on `LevelUpDismissedMessage`; navigates to `Game` once restore completes
- **`LevelUpLifetimeScope`** — registers this component and provides the `IReadyGate` injection
- **`ColorProgressBar`** — receives `LevelUpGlowTrailsMessage` to drain its slider in sync with glow trail waves; receives `LevelUpDismissedMessage` to apply the new max and reset progress
- **`ScoreTrailService`** — provides trail target positions for glow trail origin and the `FlyingTrail` prefab for pool channel creation
- **`PoolManager`** — hosts per-color `GlowTrail_{colorName}` pools created lazily by popup
- **`GamePalette`** — provides palette entries (color names and tints) for iterating glow trails
