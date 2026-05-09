# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `IGameConfiguration` | Single source of truth for all game data — slot dimensions, balloon colors, timing values, spawn counts, shield counts, score thresholds |
| `IReusable` | Contract for pooled UI objects (score notices, score trails) — a single `IsUsable` bool that pools query before recycling |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `VfxPoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Extensions/` | Extension methods (reserved for future use) |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `BalloonController`, `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `GameStartButton` | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, cheats | `BalloonController`, `ScoreController` |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
