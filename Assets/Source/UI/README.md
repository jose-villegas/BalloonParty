# UI

All HUD and menu elements. Each sub-folder owns one distinct player-facing feature.

Each self-contained UI section has its own VContainer child scope, inheriting all game services from `GameLifetimeScope` while keeping its registrations local.

## Scopes

| Scope | GameObject | Registers |
|---|---|---|
| `ScoreUILifetimeScope` | Score UI Canvas root | Injects scene-placed `ColorProgressBar` instances via `RegisterBuildCallback`; binds `ScoreCounterLabel` and `LevelLabel` |
| `LevelUpLifetimeScope` | LevelUp popup root | `LevelUpPopUp` |
| `ShieldUILifetimeScope` | Shield HUD root | `ShieldCounterLabel[]`, `ShieldCounterAnimation` |

## Feature folders

| Folder | What it owns | Scope |
|---|---|---|
| `Score/` | Progress bars, score trail orbs, floating notices, score/level labels, `ScoreUILifetimeScope` | `ScoreUILifetimeScope` (child of `GameLifetimeScope`) |
| `LevelUp/` | Full-screen level-up ceremony popup (`LevelUpPopUp`) | `LevelUpLifetimeScope` (child of `GameLifetimeScope`) |
| `Shields/` | Shield counter label and bounce animation | `ShieldUILifetimeScope` (child of `GameLifetimeScope`) |

## Game start

Scene loading is handled by `SceneTransition` (in `Shared/`) — a MonoBehaviour wired directly to the start button's `onClick` in the Inspector. No dedicated start-screen component is needed.

## Interactions

- **ScoreController** — all score UI subscribes to its `TotalScore` / `Level` reactive properties and `ScorePointMessage` / `ScoreLevelUpMessage` events; `ColorProgressBar` reads the current streak via `GetStreak` for displaying streak notices
- **ScoreTrailService** — `ColorProgressBar` registers trail target providers and subscribes to `ScoreTrailArrivedMessage`; `LevelUpPopUp` reads target positions for glow trail origins
- **LevelUpPopUp ↔ ColorProgressBar** — popup publishes `LevelUpGlowTrailsMessage` to drain bars in sync with glow trails, and `LevelUpDismissedMessage` to apply the new max and reset
- **ThrowerController** — binds `ShieldCounterAnimation` to the active `ProjectileModel` after each reload
- **IGameConfiguration** — read for point thresholds and trail animation timing
