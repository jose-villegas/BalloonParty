# Game

The entry point that starts and runs the game.

## Contents

| File | What it does |
|---|---|
| `GameLifetimeScope` | VContainer composition root — registers all game services, entry points, MessagePipe brokers, and cheats |
| `GameChildLifetimeScope` | Abstract base for all child scopes — provides `FindParent()` wiring to `GameLifetimeScope` |
| `ScoreController` | Tracks per-color progress and total score; persists via `PlayerPrefs`; drives level progression |

## Architecture

`GameLifetimeScope` is the sole composition root. All other systems — spawner, balancer, thrower, score — are wired here and injected wherever needed.

`GameChildLifetimeScope` is the abstract base that most child scopes extend (`ScoreUILifetimeScope`, `LevelUpLifetimeScope`, `ShieldUILifetimeScope`, `ProjectileLifetimeScope`, `BalloonLifetimeScope`). It overrides `FindParent()` to locate `GameLifetimeScope` automatically, so child scopes inherit all parent services without manual wiring.

**Exception:** `ItemViewScope` extends `LifetimeScope` directly with a custom `FindParent()` that walks the transform hierarchy. This is necessary because `ItemViewScope` is a nested child scope (under `BalloonLifetimeScope`) and needs to parent to its own balloon's root scope rather than the game scope.

`ScoreController` lives here. On each balloon hit it publishes `BalloonScoredMessage` for the UI and checks whether all colors have met the threshold for the next level; when they have it publishes `ScoreLevelUpMessage` and pauses the game. It saves to `PlayerPrefs` on quit and focus-lost.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid whenever a gap is created
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **ScoreUILifetimeScope** — child scope for score HUD; inherits all registrations from here
- **IGameConfiguration** — registered once here as a singleton; consumed everywhere
