# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `ColorableRenderer` | Abstract `MonoBehaviour` base implementing `IColorableRenderer` — required so Unity can serialise `ColorableRenderer[]` fields. Generic `ColorableRenderer<T>` subclass lazy-fetches the renderer component. Concrete subclasses (`SpriteColorableRenderer`, `ParticleColorableRenderer`) override `SetColor` with type-specific logic |
| `CompositeColorableRenderer` | `ColorableRenderer` that holds a serialised `ColorableRenderer[]` and forwards `SetColor` to all of them — composites multiple renderers under a single colorable |
| `CurveUtility` | Static math utilities for `AnimationCurve`-driven interpolation — `LerpWithVerticalCurve` (position along an arc) and `SampleMultiplied` (curve-scaled value). Used by `PaintSplashView` and its editor preview |
| `IGameConfiguration` | Read-only interface for core game data — projectile settings, slot grid dimensions, prediction trace params, score trail timing, score points scatter delay, points-per-level formula. Concrete implementation in `Configuration/GameConfiguration` |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()`. Not item-specific; used by any system that abstracts a VFX |
| `TweenTracker` | Generic `MonoBehaviour` for DOTween sequence composition — `Append` (chain after current), `Replace` (kill current and start new), `Kill`, `IsPlaying`. Used by balloon views to manage nudge → balance tween chaining without conflicts |
| `SortingHelper` | Static utility for Unity sorting order calculations — `SlotBaseSortingOrder` computes base order from grid position, `ApplySortingOrder` applies sequential orders to renderer arrays. Used by `BalloonView`, `ItemDisplayService`, and `ItemVisualView` |
| `SceneTransition` | MonoBehaviour wired to a button's `onClick`. When `_preload` is enabled, loads the target scene additively on `Start` with rendering suppressed (cameras, canvases, audio listeners, event systems disabled via `SceneExtensions`). `Load()` restores rendering, sets the preloaded scene as active, and unloads the current scene. Without preload, falls back to `SceneManager.LoadScene` |
| `Navigation` | Static app-wide navigation state — `ReactiveProperty<NavigationState>` with `TransitionTo()`. Accessible from any scene without DI |
| `NavigationState` | Enum: `Launch`, `Game`, `LevelUp` |
| `NavigationTrigger` | MonoBehaviour with `[SerializeField] NavigationState _targetState`. Wire `Transition()` to a button's onClick to change navigation state from the Inspector |
| `EditorNavigationBootstrap` | Editor-only MonoBehaviour — auto-transitions to `_targetState` on `Awake` when playing a scene directly (skipping the full navigation path). Only fires when the scene is the active scene, so it is inert during additive preloading |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `EffectPoolChannel`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Extensions/` | Extension methods — `ColorableRendererExtensions` provides `BindColor` overloads for reactive color subscriptions. `SceneExtensions` provides `SuppressRendering()` / `SceneRenderingHandle.Restore()` for disabling and restoring cameras, canvases, audio listeners, and event systems on a loaded scene |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, item handlers, cheats | `BalloonController`, `ScoreController`, `NudgeService`, `ItemActivator` — carries `Damage` (int, default 1); item handlers pass `ItemSettings.Damage`, projectile hits always use 1 |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar`, `ScoreTrailService` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ScoreTrailArrivedMessage` | `ScoreTrailService` | `ColorProgressBar` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `ItemRotationCapturedMessage` | `BalloonController` | `LaserItemHandler` |
| `ShieldGainedMessage` | `ShieldItemHandler` | `ShieldTrailController` |

> `BalloonNudgeMessage` lives in `Nudge/` (not `Messages/`) — it is specific to the nudge system and carries `NudgeType` and `NudgeOverride[]` types from that namespace.
