# Shared

Types and utilities used across multiple features.

`IGameConfiguration` is the single source of truth for all game data — slot dimensions, balloon colors, timing values, spawn counts, shield counts, score thresholds. Any system that needs game data gets it from here, never from hardcoded values or duplicated fields.

`IReusable` is the contract for pooled objects (score notices, score trails) — a single `IsUsable` bool that pools query before recycling an instance.

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `BalloonController`, `ProjectileView`, `BalloonSpawner` | `BalloonBalancer`, `ShieldCounterAnimation`, `ShieldCounterLabel` |
| `SpawnBalloonLineMessage` | `GameStartButton` | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView` | `BalloonController` |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
