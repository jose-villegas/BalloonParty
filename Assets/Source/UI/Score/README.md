# UI/Score

Tracks and displays player progress toward the next level — one bar per balloon color, plus score and level counters.

## Contents

| File | What it does |
|---|---|
| `ScoreUILifetimeScope` | VContainer child scope on the Score UI Canvas root; injects all scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` in `Start()` |
| `ColorProgressBar` | Per-color progress slider placed directly in the scene. Listens for `ScorePointMessage` (streak notice on `GroupIndex == 0`, streak read from `IColorStreak`), `ScoreTrailArrivedMessage` (slider advancement, point notice spawning, trail-hit feedback), `ScoreLevelUpMessage` (stash new max, reset completion state, dismiss all notices as the popup appears), `LevelUpGlowTrailsMessage` (drain slider in sync with glow trail waves), `LevelUpDismissedMessage` (apply stashed max, reset slider to zero), `LevelTransitionCompletedMessage` (animate the bar reveal/hide — deferred to when the board has settled and the player can fire again), and `RunResetMessage` (reset for a fresh run). Registers itself as the `ITrailEndpoint` for its colour with `ScoreTrailService` (which forwards to the shared `TrailEndpointRegistry`) for randomised trail destinations. Delegates notice spawning/dismissal to `ProgressNoticePresenter` and rect-position math to `RectAnchorMath`. Uses `[PaletteColorName]` to select its color from `GamePalette` in the Inspector |
| `ProgressNoticePresenter` | Plain C# helper owning a bar's notice lifecycle: the two `SimplePoolChannel<ProgressNotice>` pools, the active-notice list (point **and** streak notices), and `SpawnPointNotice`/`SpawnStreakNotice`/`DismissFullyShownNotices` (animated)/`DismissAllNotices` (immediate — clears even while the Animator is frozen during the level-up freeze). Constructed by `ColorProgressBar` in `Start()` |
| `RectAnchorMath` | Static `RectTransform` position math (`UI/` root, no Score knowledge): `Center`, `RandomPosition` (for `ITrailEndpoint`), and `WorldToAnchoredPosition` (world → local anchored, for placing pooled notices) |
| `ProgressNotice` | Pooled floating TMP popup at the bar. Uses `ColorableRenderer` for tinting. Label scale driven by `AnimationCurve`; `_labelOffsetXCurve` compensates for scale-induced horizontal drift; dismisses via `ScoreDisappear` animation when replaced. Two prefab variants: streak notices ("x3") shown immediately on hit, and point notices ("+1") shown on trail arrival |
| `SimplePoolChannel<ProgressNotice>` | separate per-color pools keyed by `StreakNotice_{colorName}` and `PointNotice_{colorName}` (owned by `ProgressNoticePresenter`) |
| `GraphicColorableRenderer` | `ColorableRenderer<Graphic>` — enables `ColorableRenderer`-based tinting for UI `Graphic` components |
| `FlyingTrail` | Pooled orb that flies from balloon world position → bar position via DOTween. Supports single-phase (`Setup`) and two-phase burst (`SetupBurst`) flight modes. `SetSortingOrder` overrides the default UI sorting order for glow trails |
| `SimplePoolChannel<FlyingTrail>` | per-color pool keyed by `ScoreTrail_{colorName}` |
| `ScoreCounterLabel` | Binds total-score `TMP_Text` to `ScoreController.TotalScore` |
| `LevelLabel` | Binds level `TMP_Text` to `ScoreController.Level`; `_showNextLevel` toggle |

## How it works

Four `ColorProgressBar` instances are placed in the scene under the Score UI Canvas — one per balloon color. `ScoreUILifetimeScope` injects them via `RegisterBuildCallback` so each bar resolves its `[Inject]` dependencies (palette, score controller, subscribers, pool manager, trail service) without singleton conflicts.

Each bar identifies its color through a `[PaletteColorName]` string field, which renders in the Inspector as a popup dropdown with color swatch. On `Start()` the bar resolves its `PaletteEntry`, tints its graphics (preserving existing alpha), sets up the slider, and registers a target provider with `ScoreTrailService`.

### Scoring flow

Scoring is deferred to trail arrival — points, persistent score, and level progress all update when a `ScoreTrailArrivedMessage` is received, not immediately on balloon pop. This synchronises the visual trail animation with the actual score state.

1. **Balloon popped** → `ScoreController` publishes one `ScorePointMessage` per point (no score mutation yet)
2. **`ColorProgressBar.OnScorePoint`** (`GroupIndex == 0` only) → reads the current streak from `ColorStreakTracker` (`IColorStreak.GetStreak`, maintained in `Game/Score/`) and spawns a **streak notice** ("x3") at the bar centre when the streak is above 1
3. **`ScoreTrailService`** → spawns pooled trail orbs from the balloon's world position toward the bar
4. **Trail arrives** → `ScoreTrailService` publishes `ScoreTrailArrivedMessage`
5. **`ScoreController.OnTrailArrived`** → increments `_persistentScore`, `_totalScore`, `_levelProgress`, and checks for level-up
6. **`ColorProgressBar.OnTrailArrived`** → advances the slider by 1, spawns a **point notice** ("+1") at the trail's arrival world position (converted to local anchored space via `WorldToAnchoredPosition`) using the `_pointNoticePrefab`, triggers "TrailHit" animator feedback

### Level-up reset flow

Bar reset is a two-phase process synchronised with the glow trail ceremony:

1. **`ScoreLevelUpMessage`** → `OnLevelUp` stashes `PointsRequiredForLevel(newLevel + 1)` as `_stashedMaxValue`, stops the completion particle, and clears the "Completed" animator flag. The slider value is **not** reset yet.
2. **`LevelUpGlowTrailsMessage`** → `DrainSliderAsync` gradually drains the slider to zero in sync with glow trail waves spawned by `LevelUpPopUp`.
3. **`LevelUpDismissedMessage`** → `OnDismissed` applies `_stashedMaxValue` as the new `maxValue` and resets the slider value to zero.

### Notices

`ProgressNotice` is a pooled TMP popup with `ColorableRenderer`-based tinting. Two separate prefabs and pools are used:

- **Streak notices** (`_streakNoticePrefab`, pool key `StreakNotice_{color}`) — shown immediately on balloon hit at bar centre, tinted with the bar's color. Tracked by `ProgressNoticePresenter` for dismiss-on-replace
- **Point notices** (`_pointNoticePrefab`, pool key `PointNotice_{color}`) — shown on trail arrival at the trail's world-space landing position (converted to local anchored coordinates). Untracked (returned to pool on animation complete)

`OnValidate()` provides editor-time color preview — it loads `GamePalette` via `AssetDatabase`, finds the matching entry, and tints `_graphicsToSetColor` while preserving each graphic's existing alpha.

When the slider reaches its maximum, a completion particle plays and the bar enters its "Completed" animator state.

`ScoreCounterLabel` and `LevelLabel` are bound imperatively from `ScoreUILifetimeScope.Start()` — they receive the `ScoreController`'s reactive properties and subscribe with UniRx.

## Interactions

- **ScoreController** — source of `ScorePointMessage`, `ScoreLevelUpMessage`, `TotalScore`, `Level`; score mutation deferred to `ScoreTrailArrivedMessage`
- **ScoreTrailService** — manages trail orb spawning and flight; bar registers itself as an `ITrailEndpoint` and receives `ScoreTrailArrivedMessage` on trail arrival
- **LevelUpPopUp** — publishes `LevelUpGlowTrailsMessage` (drain trigger) and `LevelUpDismissedMessage` (final reset)
- **PoolManager** — separate per-color pools for streak notices, point notices, and `FlyingTrail`; consumer handles return
- **IGameConfiguration** — `PointsRequiredForLevel`, `ScorePointTraceDuration`
- **GamePalette** — resolves color name → `Color` for tinting graphics and trail orbs
- **ScoreUILifetimeScope** — injects all bars via `RegisterBuildCallback`; binds labels in `Start()`
