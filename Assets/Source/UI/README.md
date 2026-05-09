# UI

All HUD and menu elements. Each sub-folder owns one distinct player-facing feature.

`ScoreUILifetimeScope` is a VContainer child scope scoped specifically to the score UI. It lives on the Score UI Canvas root GameObject, inherits all game services from `GameLifetimeScope` via `EnqueueParent`, and registers only score-related components. `ShieldCounterLabel`, `ShieldCounterAnimation`, and `GameStartButton` have no sub-scope — they are registered directly in `GameLifetimeScope` because they interact tightly with game-layer systems (thrower, projectile).

## Feature folders

| Folder | What it owns | Scope |
|---|---|---|
| `Score/` | Progress bars, score trail orbs, floating notices, level labels, level-up popup, `ScoreUILifetimeScope` | `ScoreUILifetimeScope` (child of `GameLifetimeScope`) |
| `Shields/` | Shield counter label and bounce animation | `GameLifetimeScope` |
| `GameStart/` | Start-button logic that kicks off the first balloon spawn | `GameLifetimeScope` |

## Interactions

- **ScoreController** — all score UI subscribes to its `TotalScore` / `Level` reactive properties and `BalloonScoredMessage` / `ScoreLevelUpMessage` events
- **SlotGrid** — `LevelUpPopUp` polls `AllBalloonsStable()` before revealing itself
- **ThrowerController** — binds `ShieldCounterAnimation` to the active `ProjectileModel` after each reload
- **IGameConfiguration** — read for point thresholds and trail animation timing
