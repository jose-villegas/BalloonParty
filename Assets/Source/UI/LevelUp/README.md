# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## How it works

`LevelUpPopUp` subscribes to `ScoreLevelUpMessage`. When it arrives, it waits for `SlotGrid.AllBalloonsStable()` before starting — this ensures no balloons are mid-animation when the popup appears. It then triggers the `"Appear"` animator trigger, animates the glow fill ring over the configured delay, updates the level label to the new level, and plays the glow particle system. The Continue button calls `OnContinue()`, which restores `Time.timeScale = 1` and triggers `"Hide"`.

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` controller), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- The `Animator` component **must have Update Mode set to Unscaled Time**. The game is paused (`Time.timeScale = 0`) at the moment the level-up fires, so a Normal-mode Animator will freeze and the popup will never become visible.
- Registered in `ScoreUILifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.

## Interactions

- **ScoreController** — publishes `ScoreLevelUpMessage` which triggers the ceremony
- **SlotGrid** — `AllBalloonsStable()` polled before revealing the popup
- **ScoreUILifetimeScope** — registers this component and provides injection

