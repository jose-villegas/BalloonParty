# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` |
| `LevelUpPopUp` | Subscribes to `ScoreLevelUpMessage`; waits for stability; animates glow fill; updates level label |

## How it works

`LevelUpPopUp` subscribes to `ScoreLevelUpMessage`. When it arrives, it waits for `SlotGrid.AllBalloonsStable()` using `UniTask.WaitUntil` before starting — this ensures no balloons are mid-animation when the popup appears. It then triggers the `"Appear"` animator trigger, waits for the configured delay via `UniTask.Delay(ignoreTimeScale: true)`, animates the glow fill ring frame-by-frame using `UniTask.Yield`, updates the level label to the new level, and plays the glow particle system. The Continue button calls `OnContinue()`, which triggers `"Hide"` and restores `Time.timeScale = 1` after a delay. All async work uses `destroyCancellationToken` for automatic cleanup on destroy.

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` controller), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- The `Animator` component **must have Update Mode set to Unscaled Time**. The game is paused (`Time.timeScale = 0`) at the moment the level-up fires, so a Normal-mode Animator will freeze and the popup will never become visible.
- Registered in `LevelUpLifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.

## Interactions

- **ScoreController** — publishes `ScoreLevelUpMessage` which triggers the ceremony
- **SlotGrid** — `AllBalloonsStable()` polled before revealing the popup
- **LevelUpLifetimeScope** — registers this component and provides injection

