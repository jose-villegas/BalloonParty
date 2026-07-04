# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `Rendering/` | Visual rendering utilities — `ColorableRenderer` (abstract base + generic `<T>`), `CompositeColorableRenderer`, `SortingHelper` (sorting order calculations), `GizmoDrawingHelper` (Gizmos API primitives for `OnDrawGizmos`, guarded in `#if UNITY_EDITOR`) |
| `GameState/` | App-wide navigation and cinematic tracking — `Navigation` (static `ReactiveProperty<NavigationState>` with `Current` and `TransitionTo`), `Cinematic` (static `ReactiveProperty<CinematicState>` with `Current`, `IsPlaying`, `Begin`, `End`), `NavigationState` enum, `CinematicState` enum (`None`, `LevelUpPanIn`, `LevelUpRestore`), `IReadyGate` (interface — `UniTask WaitAsync(CancellationToken)`; injectable precondition gate), `NavigationReadyGate(NavigationState)` (waits until `Navigation.Current == targetState`), `CinematicEndGate(CinematicState)` (waits until `Cinematic.Current != awaitedState`), `NavigationTrigger` (button wiring), `SceneTransition` (preload + additive scene loading), `EditorNavigationBootstrap` (editor-only auto-transition) |
| `Animation/` | Tween/animation and math utilities — `TweenTracker` (DOTween sequence composition for nudge → balance chaining), `PathHelper` (Catmull-Rom spline paths + loops, midpoint displacement for fractal lightning, fractional-index array sampling, linear resampling, prefix sums), `VectorMathHelper` (centroid, bounding radius for point sets) |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `SimplePoolChannel<T>`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Disturbance/` | Shared screen-space disturbance field — `DisturbanceFieldService` (plain C# `IStartable` + `ITickable` + `IDisposable`; owns a camera-sized `ARGBHalf` RT pair for density + displacement; runs diffusion/reform/wind-advection blits each tick; exposes `Stamp()` with optional `duration` for any game system to inject disturbances; `PuffCloudView` and future effects sample `FieldTexture` via shader). Configuration lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `PLAN-PuffCloudSimulation.md` P4 |
| `Extensions/` | Extension methods — `ColorableRendererExtensions` (`BindColor` overloads for reactive color subscriptions), `WeightedPickExtensions` (`PickRandom<T>()` for `IWeightedEntry` collections — shared weighted random selection used by balloon, grid actor, and item spawning), `SlotActorExtensions` (actor query helpers), `SceneExtensions` (`SuppressRendering()` / `SceneRenderingHandle.Restore()`), `AnimationCurveExtensions` |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Diagnostics/` | Debug utilities — `FPSCounter`, `FrameRateSettings` |
| `IGameConfiguration` | Read-only interface for core game data — projectile settings, slot grid dimensions, prediction trace params, score trail timing, points formula |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()` |
| `MathUtils` | General-purpose math constants and pure functions not covered by `Mathf` — `GoldenAngle`, `TwoPi`, `Frac` |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `ActorHitMessage` | `HitPipeline` only — producers (`ProjectileHitResolver`, item handlers, cheats) route through `IHitDispatcher.Dispatch`, which runs the score/streak stage first, then broadcasts | `BalloonController`, `NudgeService`, `ItemActivator`, `GridActorHitController` (`ScoreController` is invoked as a pipeline stage, not a subscriber) — carries `ISlotActor Actor`, `WorldPosition`, `ProjectileDirection`, `HitOutcome Outcome`, and a `DamageContext Context` (whose `Damage` defaults to 1); item handlers pass `ItemSettings.Damage`, projectile hits always use 1. Subscribers downcast `Actor` to the specific interface they need (`IBalloonModel`, `IHasColor`, `IHasNudge`, etc.) |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `ScorePointMessage` | `ScoreController` | `ColorProgressBar`, `ScoreTrailService`, `LevelUpCinematic` — one message per individual score point; carries `ColorName`, `WorldPosition`, `Score` (1-based within level), `Level` (pre-computed, including next-level renumbering), `GroupSize` and `GroupIndex` (for scatter positioning) |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp`, `ColorStreakTracker` (auto-resets the streak) |
| `ScoreTrailArrivedMessage` | `ScoreTrailService` | `ScoreController`, `ColorProgressBar`, `LevelUpCinematic` — carries `ColorName`, `Score` (the level progress value this trail represents), `Level` (the level the trail was spawned during), and `WorldPosition` |
| `LevelUpDismissedMessage` | `LevelUpPopUp` | `LevelUpCinematic` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterLabel`, `ShieldCounterAnimation` |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `ItemRotationCapturedMessage` | `BalloonController` | `LaserItemHandler` |
| `ShieldGainedMessage` | `ShieldItemHandler` | `ShieldTrailController` |

> `NudgeMessage` lives in `Nudge/` (not `Messages/`) — it is specific to the nudge system and carries `NudgeType` and `NudgeOverride[]` types from that namespace.
