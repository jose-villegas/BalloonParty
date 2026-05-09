# UI/Score

Tracks and displays player progress toward the next level — one bar per balloon color, plus score and level counters.

## Contents

| File | What it does |
|---|---|
| `ScoreUILifetimeScope` | VContainer child scope on the Score UI Canvas root; registers `ColorProgressBarInstancer` |
| `ColorProgressBarInstancer` | Spawns one `ColorProgressBar` at runtime per color in `IGameConfiguration.BalloonColors` |
| `ColorProgressBar` | Per-color slider; owns `ScoreNotice` + `ScorePointTrail` pools; completion VFX |
| `ScoreNotice` | Pooled floating "+N" popup at the bar |
| `ScorePointTrail` | Pooled orb that flies from balloon world position → bar position |
| `ScoreCounterLabel` | Binds total-score `Text` to `ScoreController.TotalScore` |
| `LevelLabel` | Binds level `Text` to `ScoreController.Level`; `_showNextLevel` toggle |

## How it works

`ColorProgressBarInstancer` creates one `ColorProgressBar` at runtime for each color defined in `IGameConfiguration.BalloonColors`. Each bar listens for `BalloonScoredMessage` and advances its slider when the message color matches its own.

When the slider reaches its maximum, a completion particle plays and the bar enters its "Completed" animator state. On `ScoreLevelUpMessage`, all bars reset their sliders and update their `maxValue` to the points required for the new level.

Every balloon score also spawns two pooled objects from the bar's own pools: a `ScoreNotice` (a floating "+N" label that scales with the hit count) and a `ScorePointTrail` (a colored orb that flies from the balloon's world position to the bar). When the orb arrives it triggers a "TrailHit" animator event on the bar.

`ScoreCounterLabel` and `LevelLabel` are bound imperatively from `ScoreUILifetimeScope.Start()` — they receive the `ScoreController`'s reactive properties and subscribe with UniRx.


## Interactions

- **ScoreController** — source of `BalloonScoredMessage`, `ScoreLevelUpMessage`, `TotalScore`, `Level`
- **IGameConfiguration** — `PointsRequiredForLevel`, `ScorePointTraceDuration`
- **ScoreUILifetimeScope** — registers `ColorProgressBarInstancer`; binds labels in `Start()`
