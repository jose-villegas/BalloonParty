# UI/Score

Tracks and displays player progress toward the next level — one bar per balloon color, plus score and level counters.

## Contents

| File | What it does |
|---|---|
| `ScoreUILifetimeScope` | VContainer child scope on the Score UI Canvas root; injects all scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` in `Start()` |
| `ColorProgressBar` | Per-color progress slider placed directly in the scene. Listens for `StreakChangedMessage` (streak notice, streak read from `IColorStreak`), `ScoreTrailArrivedMessage` (slider advancement by `msg.Points`, point notice spawning, trail-hit feedback), `ScoreLevelUpMessage` (stash new max, reset completion state, dismiss all notices as the popup appears), `LevelUpGlowTrailsMessage` (drain slider in sync with glow trail waves), `LevelUpDismissedMessage` (apply stashed max, reset slider to zero), `LevelTransitionCompletedMessage` (animate the bar reveal/hide — deferred to when the board has settled and the player can fire again), and `RunResetMessage` (reset for a fresh run). Registers itself as the `ITrailEndpoint` for its colour with `ScoreTrailService` (which forwards to the shared `TrailEndpointRegistry`) for randomised trail destinations. Delegates notice spawning/dismissal to `ProgressNoticePresenter` and rect-position math to `RectAnchorMath`. Uses `[PaletteColorName]` to select its color from `GamePalette` in the Inspector |
| `ProgressNoticePresenter` | Plain C# helper owning a bar's notice lifecycle: the two `SimplePoolChannel<ProgressNotice>` pools, the active-notice list (point **and** streak notices), and `SpawnPointNotice`/`ShowStreak`/`DismissAllAnimated` (plays each notice's disappear)/`DismissAllNotices` (immediate — snaps straight to completed, safe during the level-up freeze). Constructed by `ColorProgressBar` in `Start()` |
| `RectAnchorMath` | Static `RectTransform` position math (`UI/` root, no Score knowledge): `Center`, `RandomPosition` (for `ITrailEndpoint`), and `WorldToAnchoredPosition` (world → local anchored, for placing pooled notices) |
| `ProgressNotice` | Pooled floating TMP popup at the bar. Hand-ticked rise-in/hold/rise-out on unscaled time (no Animator — with dozens alive during trail storms an Animator would re-write and canvas-dirty every frame even through the hold; the ticker writes nothing once settled). Timings mirror the retired ScoreUp/ScoreDisappear clips; a negative `_holdDuration` holds until `Dismiss()`. Uses `ColorableRenderer` for tinting. Label scale driven by `AnimationCurve`; `_labelOffsetXCurve` compensates for scale-induced horizontal drift. Two prefab variants: streak notices ("x3", hold-until-dismissed) shown immediately on hit, and point notices ("+1") shown on trail arrival |
| `SimplePoolChannel<ProgressNotice>` | separate per-color pools keyed by `StreakNotice_{colorName}` and `PointNotice_{colorName}` (owned by `ProgressNoticePresenter`) |
| `GraphicColorableRenderer` | `ColorableRenderer<Graphic>` — enables `ColorableRenderer`-based tinting for UI `Graphic` components |
| `FlyingTrail` | Pooled orb that flies from balloon world position → bar position via DOTween. Supports single-phase (`Setup`), two-phase burst (`SetupBurst`), and live-target follow (`SetupFollow` — homes on a target provider re-read every frame, so a drifting endpoint is still hit exactly) flight modes. `SetSortingOrder` overrides the default UI sorting order for glow trails. Also doubles as the orbiting "pen" for BigScore shape formations (`Game/Score/Behaviours/`) — `SetRibbonTime`/`SetRibbonEmitting`/`ClearRibbon`/`TransformRibbon`/`FreezeRibbon`/`ThawRibbon` let `ShapeFormationTicker` drive its `TrailRenderer` directly instead of via `DOTween` |
| `SimplePoolChannel<FlyingTrail>` | per-color pool keyed by `ScoreTrail_{colorName}` |
| `ScoreCounterLabel` | Empty marker subclass of `ReactiveCounterLabel`; rendering delegated to a `RollingCounterDisplay` sibling component. Bound to `ScoreController.TotalScore` by the scope |
| `LevelLabel` | Binds level `TMP_Text` to `LevelController.Level`; `_showNextLevel` toggle |

## How it works

Four `ColorProgressBar` instances are placed in the scene under the Score UI Canvas — one per balloon color. `ScoreUILifetimeScope` injects them via `RegisterBuildCallback` so each bar resolves its `[Inject]` dependencies (palette, score controller, subscribers, pool manager, trail service) without singleton conflicts.

Each bar identifies its color through a `[PaletteColorName]` string field, which renders in the Inspector as a popup dropdown with color swatch. On `Start()` the bar resolves its `PaletteEntry`, tints its graphics (preserving existing alpha), sets up the slider, and registers a target provider with `ScoreTrailService` — which also prewarms that color's `ScoreTrail_{color}` pool (see `Game/Score/README.md`). The bar then kicks off its own notice prewarm via `ProgressNoticePresenter.PrewarmAsync`, tied to `destroyCancellationToken`. Both prewarms proceed one `Instantiate` per frame so registering a color at level setup never spikes into a hitch, and both no-op past the first registration so a level restart tops up rather than growing the pools unboundedly.

### Scoring flow

Scoring is deferred to trail arrival — points, persistent score, and level progress all update when a `ScoreTrailArrivedMessage` is received, not immediately on balloon pop. This synchronises the visual trail animation with the actual score state.

1. **Balloon popped** → `ScoreController` publishes one `ScorePointsGroupMessage` per resolved color, carrying the group's total `Points` (no score mutation yet)
2. **`ColorProgressBar.OnStreakChanged`** (driven by `StreakChangedMessage`) → reads the current streak from `ColorStreakTracker` (`IColorStreak.GetStreak`, maintained in `Game/Score/`) and spawns a **streak notice** ("x3") at the bar centre when the streak is above 1
3. **`ScoreTrailService`** → resolves the group to a trail behaviour by its point total. Below the `BigScore` floor, `DefaultScoreTrailBehaviour` spawns one pooled trail orb per point, from the balloon's world position toward the bar. At or above the floor, `BigScoreTrailBehaviour` decomposes the total into a handful of tumbling 3D shapes (see `Game/Score/README.md`) that travel toward the bar together
4. **Trail arrives** → each `ScoreTrailArrivedMessage` carries `msg.Points`: `1` for a default trail, or a shape's whole denomination for a BigScore formation arriving in one go
5. **`ScoreController.OnTrailArrived`** → adds `msg.Points` to `_persistentScore` and `_totalScore`; `LevelController` confirms progress and checks for level-up
6. **`ColorProgressBar.OnTrailArrived`** → advances the slider by `msg.Points`, spawns a **point notice** (showing `msg.Points`) at the trail's arrival world position (converted to local anchored space via `WorldToAnchoredPosition`) using the `_pointNoticePrefab`, triggers "TrailHit" animator feedback

### Level-up reset flow

Bar reset is a two-phase process synchronised with the glow trail ceremony:

1. **`ScoreLevelUpMessage`** → `OnLevelUp` stashes `PointsRequiredForLevel(newLevel)` (the goal for the level just reached) as `_stashedMaxValue`, stops the completion particle, and clears the "Completed" animator flag. It also snaps the slider's *value* up to its current `maxValue` — trails still frozen behind the popup haven't landed yet, so without the snap the bar would read low even though the level requirement was actually met. The value is **not** reset to zero yet; that only happens once dismissed.
2. **`LevelUpGlowTrailsMessage`** → `DrainSliderAsync` gradually drains the slider to zero in sync with glow trail waves spawned by `LevelUpPopUp`.
3. **`LevelUpDismissedMessage`** → `OnDismissed` applies `_stashedMaxValue` as the new `maxValue` and resets the slider value to zero.

### Notices

`ProgressNotice` is a pooled TMP popup with `ColorableRenderer`-based tinting. Two separate prefabs and pools are used:

- **Streak notices** (`_streakNoticePrefab`, pool key `StreakNotice_{color}`) — shown immediately on balloon hit at bar centre, tinted with the bar's color. Tracked by `ProgressNoticePresenter` for dismiss-on-replace
- **Point notices** (`_pointNoticePrefab`, pool key `PointNotice_{color}`) — shown on trail arrival at the trail's world-space landing position (converted to local anchored coordinates). Untracked (returned to pool on animation complete)

Both pools are prewarmed per color to `IGameConfiguration.ProgressNoticePrewarmPerColor` (default 16 each) when the bar starts.

`OnValidate()` provides editor-time color preview — it loads `GamePalette` via `AssetDatabase`, finds the matching entry, and tints `_graphicsToSetColor` while preserving each graphic's existing alpha.

When the slider reaches its maximum, a completion particle plays and the bar enters its "Completed" animator state.

`ScoreCounterLabel` and `LevelLabel` are bound imperatively from `ScoreUILifetimeScope.Start()` — they receive `ScoreController.TotalScore` and `LevelController.Level` respectively and subscribe with UniRx.

## Interactions

- **ScoreController** — source of `ScorePointsGroupMessage` and `TotalScore`; score mutation deferred to `ScoreTrailArrivedMessage`. `Level` and `ScoreLevelUpMessage` belong to `LevelController` instead (`Game/Level/`)
- **ScoreTrailService** — manages trail orb spawning and flight; bar registers itself as an `ITrailEndpoint` and receives `ScoreTrailArrivedMessage` on trail arrival
- **LevelUpPopUp** — publishes `LevelUpGlowTrailsMessage` (drain trigger) and `LevelUpDismissedMessage` (final reset)
- **PoolManager** — separate per-color pools for streak notices, point notices, and `FlyingTrail`; consumer handles return
- **ILevelThresholds** — `PointsRequiredForLevel`, read by `ColorProgressBar` to size the slider and stash the next goal
- **IGameConfiguration** — `ProgressNoticePrewarmPerColor`
- **GamePalette** — resolves color name → `Color` for tinting graphics and trail orbs
- **ScoreUILifetimeScope** — injects all bars via `RegisterBuildCallback`; binds labels in `Start()`
