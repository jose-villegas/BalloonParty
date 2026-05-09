# UI

All HUD and menu elements. Each sub-folder owns one distinct player-facing feature.

Each self-contained UI section has its own VContainer child scope, inheriting all game services from `GameLifetimeScope` while keeping its registrations local. Components that interact tightly with game-layer systems (thrower, projectile) are registered directly in `GameLifetimeScope` instead.

## Scopes

| Scope | GameObject | Registers |
|---|---|---|
| `ScoreUILifetimeScope` | Score UI Canvas root | `ColorProgressBarInstancer` |
| `LevelUpLifetimeScope` | LevelUp popup root | `LevelUpPopUp` |
| `ShieldUILifetimeScope` | Shield HUD root | `ShieldCounterLabel[]`, `ShieldCounterAnimation` |
| `GameLifetimeScope` (direct) | — | `GameStartButton` |

## Feature folders

| Folder | What it owns | Scope |
|---|---|---|
| `Score/` | Progress bars, score trail orbs, floating notices, score/level labels, `ScoreUILifetimeScope` | `ScoreUILifetimeScope` (child of `GameLifetimeScope`) |
| `LevelUp/` | Full-screen level-up ceremony popup (`LevelUpPopUp`) | `LevelUpLifetimeScope` (child of `GameLifetimeScope`) |
| `Shields/` | Shield counter label and bounce animation | `GameLifetimeScope` |
| `GameStart/` | Start-button logic that kicks off the first balloon spawn | `GameLifetimeScope` |

## Interactions

- **ScoreController** — all score UI subscribes to its `TotalScore` / `Level` reactive properties and `BalloonScoredMessage` / `ScoreLevelUpMessage` events
- **SlotGrid** — `LevelUpPopUp` polls `AllBalloonsStable()` before revealing itself
- **ThrowerController** — binds `ShieldCounterAnimation` to the active `ProjectileModel` after each reload
- **IGameConfiguration** — read for point thresholds and trail animation timing
