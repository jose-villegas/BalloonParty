# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `IGameConfiguration` | Read-only interface for all game data — slot dimensions, balloon colors, timing values, spawn counts, shield counts, items, display settings. Concrete implementation lives in `Configuration/GameConfiguration` |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()`. Not item-specific; used by any system that abstracts a VFX |
| `TweenTracker` | Generic `MonoBehaviour` for DOTween sequence composition — `Append` (chain after current), `Replace` (kill current and start new), `Kill`, `IsPlaying`. Used by balloon views to manage nudge → balance tween chaining without conflicts |
| `SortingHelper` | Static utility for Unity sorting order calculations — `SlotBaseSortingOrder` computes base order from grid position, `ApplySortingOrder` applies sequential orders to renderer arrays. Used by `BalloonView`, `ItemDisplayService`, and `ItemVisualView` |
| `SceneTransition` | MonoBehaviour wired to a button's `onClick` — calls `SceneManager.LoadScene(_sceneName)`. Attach to the start button and set the Game scene name in the Inspector |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `EffectPoolChannel`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Extensions/` | Extension methods (reserved for future use) |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, item handlers, cheats | `BalloonController`, `ScoreController`, `BalloonNudgeHandler`, `ItemActivator` |
| `BalloonNudgeMessage` | `BalloonNudgeHandler`, item handlers | neighboring `BalloonView`s (nudge animation) |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `ItemRotationCapturedMessage` | `BalloonController` | `LaserItemHandler` |
