# UI

All HUD and menu elements. Each sub-folder owns one distinct player-facing feature.

Each self-contained UI section has its own VContainer child scope, inheriting all game services from `GameLifetimeScope` while keeping its registrations local.

## Scopes

| Scope | GameObject | Registers |
|---|---|---|
| `ScoreUILifetimeScope` | Score UI Canvas root | Injects scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` |
| `LevelUpLifetimeScope` | LevelUp popup root | `LevelUpPopUp`, `CinematicEndGate(LevelUpPanIn)` (by concrete type — the popup injects it directly, not `IReadyGate`) |
| `ShieldUILifetimeScope` | Shield HUD root | `ShieldCounterLabel[]`, `ShieldCounterAnimation`, `ShieldTrailController`, the `Shield` trail endpoint |
| `HealthUILifetimeScope` | Hearts HUD root | Binds `HealthCounterLabel`s to `IPlayerHealth.Current`; `HeartTrailController`, the `Heart` trail endpoint |
| `DangerUILifetimeScope` | Danger overlay root | Binds `DangerGradientView`s to `IDangerLevel.Level` |
| `GameOverLifetimeScope` | Game-over screen root | Empty child scope on the screen root (`GameOverScreen` itself is injected by `GameLifetimeScope`) |

## Feature folders

| Folder | What it owns | Scope |
|---|---|---|
| `Score/` | Progress bars, score trail orbs, floating notices, score/level labels, `ScoreUILifetimeScope` | `ScoreUILifetimeScope` (child of `GameLifetimeScope`) |
| `LevelUp/` | Full-screen level-up ceremony popup (`LevelUpPopUp`) | `LevelUpLifetimeScope` (child of `GameLifetimeScope`) |
| `Shields/` | Shield counter label, bounce animation, and shield trails | `ShieldUILifetimeScope` (child of `GameLifetimeScope`) |
| `Health/` | Hit-point counter label and heart trails to overflow pops | `HealthUILifetimeScope` (child of `GameLifetimeScope`) |
| `Danger/` | Space-danger gradient overlay (`DangerGradientView`) | `DangerUILifetimeScope` (child of `GameLifetimeScope`) |
| `GameOver/` | Loss screen (`GameOverScreen`) — see `GameOver/README.md` | `GameOverLifetimeScope` (child of `GameLifetimeScope`) |
| `Binding/` | Shared reactive binding helpers — `IReactiveBindable<T>`, `ReactivePropertyBinder`, and the `RegisterBoundViews` scope extension used by the Health and Danger scopes | — |

Root-level helpers: `ReactiveCounterLabel` (base for TMP counter labels bound to an `int` reactive property), `FormattedLabel` (captures a label's authored text as a `{0}` template), and `RectAnchorMath` (static `RectTransform` position math).

## Game start

Scene loading is handled by `SceneTransition` (in `Shared/`) — a MonoBehaviour wired directly to the start button's `onClick` in the Inspector. No dedicated start-screen component is needed.

## Interactions

- **ScoreController** — all score UI subscribes to its `TotalScore` / `Level` reactive properties and `ScorePointMessage` / `ScoreLevelUpMessage` events; `ColorProgressBar` reads the current streak via `GetStreak` for displaying streak notices
- **ScoreTrailService** — `ColorProgressBar` registers trail target providers and subscribes to `ScoreTrailArrivedMessage`; `LevelUpPopUp` reads target positions for glow trail origins
- **LevelUpPopUp ↔ ColorProgressBar** — popup publishes `LevelUpGlowTrailsMessage` to drain bars in sync with glow trails, and `LevelUpDismissedMessage` to apply the new max and reset
- **ThrowerController** — binds `ShieldCounterAnimation` to the active `ProjectileModel` after each reload
- **IGameConfiguration** — read for point thresholds and trail animation timing
