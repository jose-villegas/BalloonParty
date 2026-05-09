# Game

The entry point that starts and runs the game.

`GameLifetimeScope` is the VContainer composition root. It registers all game services, entry points, and MessagePipe brokers. All other systems — spawner, balancer, thrower, score — are wired here and injected wherever needed.

It also owns the registrations for HUD components that interact directly with game-layer systems: `ShieldCounterLabel`, `ShieldCounterAnimation`, and `GameStartButton`. These sit in `GameLifetimeScope` rather than a UI child scope because they depend on services (`ThrowerController`, projectile messages) that live at the game level and should not bleed into a UI-scoped container.

`ScoreUILifetimeScope` is a child scope that inherits everything registered here. Other child scopes (`LevelUpLifetimeScope`, `ShieldUILifetimeScope`) follow the same pattern via `GameChildLifetimeScope.FindParent()`.

`ScoreController` lives here. It tracks per-color progress and total score, persists them across sessions via `PlayerPrefs`, and drives level progression. On each balloon hit it publishes `BalloonScoredMessage` for the UI and checks whether all colors have met the threshold for the next level; when they have it publishes `ScoreLevelUpMessage` and pauses the game.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid whenever a gap is created
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **ScoreUILifetimeScope** — child scope for score HUD; inherits all registrations from here
- **IGameConfiguration** — registered once here as a singleton; consumed everywhere
