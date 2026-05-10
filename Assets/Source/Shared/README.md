# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `IGameConfiguration` | Read-only interface for all game data — slot dimensions, balloon colors, timing values, spawn counts, shield counts, power-ups, display settings. Concrete implementation lives in `Configuration/GameConfiguration` |
| `TweenTracker` | Generic `MonoBehaviour` for DOTween sequence composition — `Append` (chain after current), `Replace` (kill current and start new), `Kill`, `IsPlaying`. Used by balloon views to manage nudge → balance tween chaining without conflicts |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `VfxPoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Extensions/` | Extension methods (reserved for future use) |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `GameStartButton` | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, cheats | `BalloonController`, `ScoreController`, `BalloonNudgeHandler` |
| `BalloonNudgeMessage` | `BalloonNudgeHandler` | neighboring `BalloonView`s (nudge animation) |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
