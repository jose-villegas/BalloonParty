# UI

All HUD and menu elements. Each sub-folder owns one distinct player-facing feature.

Each self-contained UI section has its own VContainer child scope, inheriting all game services from `GameLifetimeScope` while keeping its registrations local.

## Scopes

| Scope | GameObject | Registers |
|---|---|---|
| `ScoreUILifetimeScope` | Score UI Canvas root | Injects scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` |
| `LevelUpLifetimeScope` | LevelUp popup root | `LevelUpPopUp`, `CinematicEndGate(LevelCompleteHit)` (by concrete type — the popup injects it directly, not `IReadyGate`) |
| `ShieldUILifetimeScope` | Shield HUD root | `ShieldCounterLabel[]`, `ShieldCounterAnimation` |
| `HealthUILifetimeScope` | Hearts HUD root | Binds `HealthCounterLabel`s to `IPlayerHealth.Current`; `HeartTrailController`, the `Heart` trail endpoint |
| `DangerUILifetimeScope` | Danger overlay root | Binds `DangerGradientView`s to `IDangerLevel.Level` |
| `GameOverLifetimeScope` | Game-over screen root | Empty child scope on the screen root (`GameOverScreen` itself is injected by `GameLifetimeScope`) |

## Feature folders

| Folder | What it owns | Scope |
|---|---|---|
| `Score/` | Progress bars, score trail orbs, floating notices, score/level labels, `ScoreUILifetimeScope` | `ScoreUILifetimeScope` (child of `GameLifetimeScope`) |
| `LevelUp/` | Full-screen level-up ceremony popup (`LevelUpPopUp`) | `LevelUpLifetimeScope` (child of `GameLifetimeScope`) |
| `Shields/` | The shields-per-ball counter and its animation | `ShieldUILifetimeScope` (child of `GameLifetimeScope`) |
| `Health/` | Hit-point counter label and heart trails to overflow pops | `HealthUILifetimeScope` (child of `GameLifetimeScope`) |
| `Danger/` | Space-danger gradient overlay (`DangerGradientView`) | `DangerUILifetimeScope` (child of `GameLifetimeScope`) |
| `GameOver/` | Loss screen (`GameOverScreen`) — see `GameOver/README.md` | `GameOverLifetimeScope` (child of `GameLifetimeScope`) |
| `Tooltip/` | The on-screen tooltip (`TooltipBanner`): shows each `TooltipMessage` briefly — see `Tooltip/README.md` | `GameLifetimeScope` (`RegisterComponentInHierarchy`) |
| `HeadsUp/` | Handwritten heads-ups (`PLAN-HeadsUp.md`); the view half: `HeadsUpLayer` draws what `Game/HeadsUp/` lets through as pooled `HandwrittenHeadsUp` lines, packed clear of each other (`HeadsUpPacking`) and written on as if by a pencil (`WriteOnText`) — see `HeadsUp/README.md` | `HeadsUpLayer`: required in the Game scene, `GameLifetimeScope` (`RegisterComponentInHierarchy`, as `IHeadsUpPresenter`) |
| `CardReveal/` | The card reveal on a level-up: a balloon pops and ribbons carry the won card — see `CardReveal/README.md` | `CardRevealView` on the Game scene's `CardRevealCanvas`, injected by `GameScopeRegistration`'s scene-view pass |
| `Telemetry/` | Catalog-driven stats labels — drop `MetricLabel` on a `TMP_Text`, author the format string, and pick a value from a dropdown generated out of `MetricCatalog`; no code needed per label. Any UI scope that wants labels must call `MetricLabelRegistration.RegisterMetricLabels(scope)` — see `Telemetry/README.md` | Already wired into `LevelUpLifetimeScope` and `GameOverLifetimeScope` |
| `Binding/` | Shared reactive binding helpers — `IReactiveBindable<T>`, `ICounterDisplay` (strategy interface for counter rendering), `ReactivePropertyBinder`, and the `RegisterBoundViews` scope extension used by the Health and Danger scopes | — |

Root-level helpers: `ReactiveCounterLabel` (abstract base for counter labels — subscribes a reactive `int` property and delegates rendering to an `ICounterDisplay` sibling resolved via `GetComponent`), `RollingCounterDisplay` (rolling-odometer `ICounterDisplay` backed by `RollingTextAnimator`), `PlainCounterDisplay` (plain thousands-formatted `ICounterDisplay`), `FormattedLabel` (captures a label's authored text as a `{0}` template), `RectAnchorMath` (static `RectTransform` position math), `CameraFittedCanvas` (keeps a World Space canvas over the main camera's view each frame, sized as the `CanvasScaler` sizes a screen canvas, so UI laid out for the screen is drawn in the scene and lit with it; it follows the camera's zoom and pan — the card reveal's canvas, `CardReveal/README.md`), and `CanvasCameraBinder` (binds a Screen Space - Camera canvas's root at runtime to the persistent camera's HUD overlay, `Display/HudCamera`, found under `Camera.main`, so the HUD draws after the scene's tonemapping; that camera lives on a `DontDestroyOnLoad` prefab and can't be wired to a canvas at edit time — place it alongside each HUD's root `Canvas`).

## Game start

Scene loading is handled by `SceneTransition` (in `Shared/`) — a MonoBehaviour wired directly to the start button's `onClick` in the Inspector. No dedicated start-screen component is needed.

## Interactions

- **ScoreController / LevelController** — all score UI subscribes to `TotalScore` / `Level` reactive properties and `ScorePointsGroupMessage` (from `ScoreController`) / `ScoreLevelUpMessage` (from `LevelController`) events; `ColorProgressBar` reads the current streak via `ScoreController.GetStreak` for displaying streak notices
- **ScoreTrailService** — `ColorProgressBar` registers trail target providers and subscribes to `ScoreTrailArrivedMessage`; `LevelUpPopUp` reads target positions for fill trail origins
- **LevelUpPopUp ↔ ColorProgressBar** — popup publishes `LevelUpFillTrailsMessage` to drain bars in sync with fill trails, and `LevelUpDismissedMessage` to apply the new max and reset
- **RunUpgrades** — `IShieldsPerBall`: the shields every new ball starts with. `ShieldCounterAnimation` binds the shield labels to it once; each ball shows its own shields in flight (`ProjectileShieldView`)
- **ILevelThresholds / IScoreTrailConfig** — read for point thresholds and trail animation timing
