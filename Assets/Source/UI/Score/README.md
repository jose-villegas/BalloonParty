# UI/Score

Tracks and displays player progress toward the next level — one bar per balloon color, plus score and level counters.

## Contents

| File | What it does |
|---|---|
| `ScoreUILifetimeScope` | VContainer child scope on the Score UI Canvas root; injects all scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` in `Start()` |
| `ColorProgressBar` | Per-color progress slider placed directly in the scene. Listens for `BalloonScoredMessage` (streak counting, slider advancement, notice spawning) and `ScoreTrailArrivedMessage` (trail-hit feedback). Registers a `Func<Vector3>` target provider with `ScoreTrailService` for randomised trail destinations. Uses `[PaletteColorName]` to select its color from `GamePalette` in the Inspector |
| `ScoreNotice` | Pooled floating "+N" TMP popup at the bar; label scale driven by `AnimationCurve`; `_labelOffsetXCurve` compensates for scale-induced horizontal drift; dismisses via `ScoreDisappear` animation when replaced |
| `ScoreNoticePoolChannel` | `PoolChannel<ScoreNotice>` — per-color pool keyed by `ScoreNotice_{colorName}` |
| `ScorePointTrail` | Pooled orb that flies from balloon world position → bar position via DOTween |
| `ScoreTrailPoolChannel` | `PoolChannel<ScorePointTrail>` — per-color pool keyed by `ScoreTrail_{colorName}` |
| `ScoreCounterLabel` | Binds total-score `TMP_Text` to `ScoreController.TotalScore` |
| `LevelLabel` | Binds level `TMP_Text` to `ScoreController.Level`; `_showNextLevel` toggle |

## How it works

Four `ColorProgressBar` instances are placed in the scene under the Score UI Canvas — one per balloon color. `ScoreUILifetimeScope` injects them via `RegisterBuildCallback` so each bar resolves its `[Inject]` dependencies (palette, score controller, subscribers, pool manager, trail service) without singleton conflicts.

Each bar identifies its color through a `[PaletteColorName]` string field, which renders in the Inspector as a popup dropdown with color swatch. On `Start()` the bar resolves its `PaletteEntry`, tints its graphics (preserving existing alpha), sets up the slider, and registers a target provider with `ScoreTrailService`.

When a `BalloonScoredMessage` arrives whose color matches the bar, a per-color streak counter increments and the slider advances. For single-point scores, any fully-shown notice is dismissed and a new `ScoreNotice` spawns at the bar origin. For multi-point scores (e.g. items), multiple untracked notices spawn at random positions within the bar's rect, staggered by `ScorePointsScatterDelay`.

`OnValidate()` provides editor-time color preview — it loads `GamePalette` via `AssetDatabase`, finds the matching entry, and tints `_graphicsToSetColor` while preserving each graphic's existing alpha.

Score trail orbs are managed by `ScoreTrailService` (in `Game/`). When a trail arrives at this bar's target, `ScoreTrailArrivedMessage` triggers the "TrailHit" animator feedback. The bar's target position is randomised each trail via `RandomWorldPositionInRect()`, registering a `Func<Vector3>` with the trail service.

When the slider reaches its maximum, a completion particle plays and the bar enters its "Completed" animator state. On `ScoreLevelUpMessage`, all bars reset their sliders and update their `maxValue` to the points required for the new level.

`ScoreNotice` uses TMP labels (`TMP_Text`). The label scale is driven by an `AnimationCurve` (X = score, Y = scale), and `_labelOffsetXCurve` applies horizontal position compensation so the label stays centred as it scales up. Both the Score and ScoreDisappear animations fire `OnAnimationCompleted`, which invokes the consumer's return callback to send the notice back to the pool.

`ScoreCounterLabel` and `LevelLabel` are bound imperatively from `ScoreUILifetimeScope.Start()` — they receive the `ScoreController`'s reactive properties and subscribe with UniRx.

## Interactions

- **ScoreController** — source of `BalloonScoredMessage`, `ScoreLevelUpMessage`, `TotalScore`, `Level`
- **ScoreTrailService** — manages trail orb spawning and flight; bar registers a `Func<Vector3>` target provider and receives `ScoreTrailArrivedMessage` on trail arrival
- **PoolManager** — per-color pools for `ScoreNotice` and `ScorePointTrail`; consumer handles return
- **IGameConfiguration** — `PointsRequiredForLevel`, `ScorePointTraceDuration`, `ScorePointsScatterDelay`
- **GamePalette** — resolves color name → `Color` for tinting graphics and trail orbs
- **ScoreUILifetimeScope** — injects all bars via `RegisterBuildCallback`; binds labels in `Start()`
