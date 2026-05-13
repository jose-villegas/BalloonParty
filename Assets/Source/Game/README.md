# Game

The entry point that starts and runs the game.

## Contents

| File | What it does |
|---|---|
| `GameLifetimeScope` | VContainer composition root — registers all game services, entry points, MessagePipe brokers, configuration assets, and (in dev builds) cheats |
| `GameChildLifetimeScope` | Abstract base for all child scopes — `FindParent()` resolves to `GameLifetimeScope` automatically |
| `ScoreController` | Tracks per-color level progress and total score; persists via `PlayerPrefs`; triggers level-ups and pauses the game when all color thresholds are met |

## Architecture

`GameLifetimeScope` is the sole composition root. All systems — spawner, balancer, nudge, thrower, score, items — are wired here and inherit into child scopes automatically.

`GameChildLifetimeScope` is the abstract base extended by: `ScoreUILifetimeScope`, `LevelUpLifetimeScope`, `ShieldUILifetimeScope`, `ProjectileLifetimeScope`, `BalloonLifetimeScope`, and `ThrowerLifetimeScope`. It overrides `FindParent()` to locate `GameLifetimeScope` in the scene.

**Exception:** `ItemViewScope` extends `LifetimeScope` directly with a custom `FindParent()` that walks the transform hierarchy — so each pooled balloon's item scope parents to its own balloon's root scope rather than a random one.

`ScoreController` subscribes to `BalloonHitMessage`. It only scores actual pops — deflections (`HitsRemaining > 1` or `-1`) are ignored. On each qualifying hit it increments per-color `_levelProgress` and `_persistentScore`, publishes `BalloonScoredMessage`, and checks whether all colors have met the level threshold. When they have, it increments the level, resets per-color progress, publishes `ScoreLevelUpMessage`, and calls `Time.timeScale = 0`. It saves to `PlayerPrefs` on quit and focus-lost.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid after spawn
- **NudgeService** — handles all balloon nudge animations
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **GameLifetimeScope config** — `GameConfiguration`, `BalloonsConfiguration`, `GamePalette`, `GameDisplayConfiguration`, `ItemConfiguration` all registered here as singletons
