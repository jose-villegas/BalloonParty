# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` and a `CinematicEndGate(LevelUpPanIn)` as `IReadyGate` |
| `LevelUpPopUp` | Waits for the pan-in cinematic to end (via `IReadyGate`), then freezes time and shows the popup; publishes `LevelUpDismissedMessage` on Continue |

## How it works

### Sequence

1. **Pan-in ends** — `LevelUpTrailEffect` calls `EndCinematic()` after the tipping trail arrives, setting `CinematicState` to `None`.
2. **Gate opens** — `CinematicEndGate(LevelUpPanIn)` unblocks: `Cinematic.Current != LevelUpPanIn` is now true.
3. **Popup shows** — `LevelUpPopUp.ShowAfterGateAsync` sets `Time.timeScale = 0f`, triggers the `"Appear"` animator, and kicks off the glow fill animation. The level label initially shows the old level; the `LevelGlowFillAsync` animation updates it to the new level after the fill completes.
4. **Player taps Continue** — `OnContinue()` triggers `"Hide"`, publishes `LevelUpDismissedMessage`, and starts `ResumeAfterDelayAsync` (a configurable settle delay).
5. **Restore cinematic** — `LevelUpTrailEffect` receives `LevelUpDismissedMessage` and starts `CinematicState.LevelUpRestore` — tweens `Time.timeScale` back to 1 and camera back to its base position/size.
6. **Navigate** — once restore completes, `LevelUpTrailEffect` calls `Navigation.TransitionTo(Game)`.

The Animator's `updateMode` is set to `UnscaledTime` in `Start()`, so animations play even while the game is paused.

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
