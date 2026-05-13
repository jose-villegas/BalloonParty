# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `IGameConfiguration` | Read-only interface for core game data — projectile settings, slot grid dimensions, prediction trace params, score trail timing, points-per-level formula. Concrete implementation in `Configuration/GameConfiguration` |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()`. Not item-specific; used by any system that abstracts a VFX |
| `TweenTracker` | Generic `MonoBehaviour` for DOTween sequence composition — `Append` (chain after current), `Replace` (kill current and start new), `Kill`, `IsPlaying`. Used by balloon views to manage nudge → balance tween chaining without conflicts |
| `SortingHelper` | Static utility for Unity sorting order calculations — `SlotBaseSortingOrder` computes base order from grid position, `ApplySortingOrder` applies sequential orders to renderer arrays. Used by `BalloonView`, `ItemDisplayService`, and `ItemVisualView` |
| `SceneTransition` | MonoBehaviour wired to a button's `onClick` — calls `SceneManager.LoadScene(_sceneName)` and publishes `SpawnBalloonLineMessage` to seed the initial balloon grid |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `EffectPoolChannel`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, item handlers, cheats | `BalloonController`, `ScoreController`, `NudgeService`, `ItemActivator` |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `ItemRotationCapturedMessage` | `BalloonController` | `LaserItemHandler` |
| `ShieldGainedMessage` | `ShieldItemHandler` | `ShieldTrailController` |

> `BalloonNudgeMessage` lives in `Nudge/` (not `Messages/`) — it is specific to the nudge system and carries `NudgeType` and `NudgeOverride[]` types from that namespace.
