# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` |
| `LevelUpPopUp` | Subscribes to `ScoreLevelUpMessage`; animates glow fill ring; updates TMP level label; publishes `LevelUpDismissedMessage` on Continue |

## How it works

`ScoreController` publishes `ScoreLevelUpMessage` and transitions navigation to `LevelUp` on level-up. `LevelUpPopUp` subscribes to this message. When it arrives, it immediately pauses the game (`Time.timeScale = 0`), triggers the `"Appear"` animator trigger, and kicks off the glow fill animation. The glow fill waits for the configured delay via `UniTask.Delay(ignoreTimeScale: true)`, plays the glow particle system, animates the glow fill ring frame-by-frame using `UniTask.Yield` and `Time.unscaledDeltaTime`, and finally updates the level label to the new level. The Continue button calls `OnContinue()`, which triggers `"Hide"` and publishes `LevelUpDismissedMessage`. Navigation back to `Game` is owned by `LevelUpTrailEffect` — it waits for the camera/timeScale restore tweens to complete before transitioning. All async work uses `destroyCancellationToken` for automatic cleanup on destroy.

The Animator's `updateMode` is set to `UnscaledTime` in code (`Start()`), so animations play even while the game is paused.

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` animator), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- Registered in `LevelUpLifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.

## Interactions

- **ScoreController** — publishes `ScoreLevelUpMessage` and transitions navigation to `LevelUp`
- **LevelUpTrailEffect** — subscribes to `LevelUpDismissedMessage`; restores timeScale, camera, and canvas; transitions navigation to `Game` once restore completes
- **LevelUpLifetimeScope** — registers this component and provides injection
