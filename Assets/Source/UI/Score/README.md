# UI/Score

Tracks and displays player progress toward the next level — one bar per balloon color, plus score and level counters.

## Contents

| File | What it does |
|---|---|
| `ScoreUILifetimeScope` | VContainer child scope on the Score UI Canvas root; registers `ColorProgressBarInstancer` |
| `ColorProgressBarInstancer` | Spawns one `ColorProgressBar` at runtime per color in `IGameConfiguration.BalloonColors` |
| `ColorProgressBar` | Per-color slider; spawns `ScoreNotice` and `ScorePointTrail` from pool; tracks active notices and dismisses stale ones; completion VFX |
| `ScoreNotice` | Pooled floating "+N" popup at the bar; scale driven by `AnimationCurve`; dismisses via `ScoreDisappear` animation when replaced |
| `ScoreNoticePoolChannel` | `PoolChannel<ScoreNotice>` — per-color pool keyed by `ScoreNotice_{colorName}` |
| `ScorePointTrail` | Pooled orb that flies from balloon world position → bar position via DOTween |
| `ScoreTrailPoolChannel` | `PoolChannel<ScorePointTrail>` — per-color pool keyed by `ScoreTrail_{colorName}` |
| `ScoreCounterLabel` | Binds total-score `Text` to `ScoreController.TotalScore` |
| `LevelLabel` | Binds level `Text` to `ScoreController.Level`; `_showNextLevel` toggle |

## How it works

`ColorProgressBarInstancer` creates one `ColorProgressBar` at runtime for each color defined in `IGameConfiguration.BalloonColors`. Each bar listens for `BalloonScoredMessage` and advances its slider when the message color matches its own. A per-color streak counter (`_localCount`) tracks consecutive same-color hits — it resets to zero whenever a different color scores.

When the slider reaches its maximum, a completion particle plays and the bar enters its "Completed" animator state. On `ScoreLevelUpMessage`, all bars reset their sliders and update their `maxValue` to the points required for the new level.

Every balloon score spawns two pooled objects via `PoolManager`:

- **`ScoreNotice`** — a floating label showing the streak count, scaled by an `AnimationCurve` (X = score, Y = scale). The bar tracks active notices; when a new one spawns, any previous notice that is fully shown (`IsFullyShown`, set by animation event) is dismissed with `ScoreDisappear` (played immediately via `Animator.Play`). Both the Score and ScoreDisappear animations fire `OnAnimationCompleted`, which invokes the consumer's return callback to send the notice back to the pool. Notices reparent themselves under the progress bar via `OnSpawned()`.

- **`ScorePointTrail`** — a colored orb that flies from the balloon's world position to the bar via DOTween. On tween completion, the consumer's callback fires — triggering the "TrailHit" animator event on the bar and returning the trail to the pool.

`ScoreCounterLabel` and `LevelLabel` are bound imperatively from `ScoreUILifetimeScope.Start()` — they receive the `ScoreController`'s reactive properties and subscribe with UniRx.

## Interactions

- **ScoreController** — source of `BalloonScoredMessage`, `ScoreLevelUpMessage`, `TotalScore`, `Level`
- **PoolManager** — per-color pools for `ScoreNotice` and `ScorePointTrail`; consumer handles return
- **IGameConfiguration** — `PointsRequiredForLevel`, `ScorePointTraceDuration`
- **ScoreUILifetimeScope** — registers `ColorProgressBarInstancer`; binds labels in `Start()`
