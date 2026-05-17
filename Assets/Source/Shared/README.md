# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `Rendering/` | Visual rendering utilities — `ColorableRenderer` (abstract base + generic `<T>`), `CompositeColorableRenderer`, `SortingHelper` (sorting order calculations), `GizmoDrawingHelper` (Gizmos API primitives for `OnDrawGizmos`, guarded in `#if UNITY_EDITOR`) |
| `GameState/` | App-wide navigation and cinematic tracking — `Navigation` (static `ReactiveProperty<NavigationState>` with `Current` and `TransitionTo`), `Cinematic` (static `ReactiveProperty<CinematicState>` with `Current`, `IsPlaying`, `Begin`, `End`; auto-notifies `ICinematicAware` listeners), `ICinematicAware` (interface — `OnCinematicBegin`, `OnCinematicEnd`), `NavigationState` enum, `CinematicState` enum, `NavigationTrigger` (button wiring), `SceneTransition` (preload + additive scene loading), `EditorNavigationBootstrap` (editor-only auto-transition) |
| `Animation/` | Tween/animation and math utilities — `TweenTracker` (DOTween sequence composition for nudge → balance chaining), `PathHelper` (Catmull-Rom spline paths + loops, midpoint displacement for fractal lightning, fractional-index array sampling, linear resampling, prefix sums), `VectorMathHelper` (centroid, bounding radius for point sets) |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `EffectPoolChannel`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Extensions/` | Extension methods — `ColorableRendererExtensions` (`BindColor` overloads for reactive color subscriptions), `SceneExtensions` (`SuppressRendering()` / `SceneRenderingHandle.Restore()`), `RectTransformExtensions` (`WorldDeltaToLocalDelta`, `WorldPointToAnchoredOffset` — convert world-space positions/deltas to RectTransform anchoredPosition values, accounting for anchors, pivot, and CanvasScaler) |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Diagnostics/` | Debug utilities — `FPSCounter`, `FrameRateSettings` |
| `IGameConfiguration` | Read-only interface for core game data — projectile settings, slot grid dimensions, prediction trace params, score trail timing, points formula |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()` |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `BalloonHitMessage` | `ProjectileView`, item handlers, cheats | `BalloonController`, `ScoreController`, `NudgeService`, `ItemActivator` — carries `Damage` (int, default 1); item handlers pass `ItemSettings.Damage`, projectile hits always use 1 |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `BalloonScoredMessage` | `ScoreController` | `ColorProgressBar`, `ScoreTrailService`, `LevelUpTrailEffect` |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp` |
| `ScoreTrailArrivedMessage` | `ScoreTrailService` | `ScoreController`, `ColorProgressBar`, `LevelUpTrailEffect` |
| `LevelUpDismissedMessage` | `LevelUpPopUp` | `LevelUpTrailEffect` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `ItemRotationCapturedMessage` | `BalloonController` | `LaserItemHandler` |
| `ShieldGainedMessage` | `ShieldItemHandler` | `ShieldTrailController` |

> `BalloonNudgeMessage` lives in `Nudge/` (not `Messages/`) — it is specific to the nudge system and carries `NudgeType` and `NudgeOverride[]` types from that namespace.
