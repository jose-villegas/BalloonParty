# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `Rendering/` | Visual rendering utilities — `ColorableRenderer` (abstract base + generic `<T>`), `CompositeColorableRenderer`, `SortingHelper` (sorting order calculations), `GizmoDrawingHelper` (Gizmos API primitives for `OnDrawGizmos`, guarded in `#if UNITY_EDITOR`), `GradientTextureHelper` (gradient → baked `Texture2D`), `MeshHelper` (procedural quad meshes), `SpriteShadowBaker` and `SpriteLayerCombiner` (data-only authoring components for baked shadows / flattened sprite layers; bake logic lives in `Editor/ShadowBake/` and `Editor/SpriteCombine/`) |
| `GameState/` | App-wide navigation and cinematic tracking — `Navigation` (static `ReactiveProperty<NavigationState>` with `Current` and `TransitionTo`) + `NavigationService`/`INavigation` (injectable seam over it), `Cinematic` (static `ReactiveProperty<CinematicState>` with `Current`, `IsPlaying`, `Begin`, `End`; auto-notifies `ICinematicAware` listeners) + `CinematicStateService`/`ICinematicState` (injectable seam; answers `Has(CinematicTraits)` from `CinematicsSettings`), `ICinematicAware` (interface — `OnCinematicBegin`, `OnCinematicEnd`), `NavigationState` enum, `CinematicState` enum (`None`, `LevelUpPanIn`, `LevelUpRestore`, `HeartDrain`, `HeartDrainRestore`), `CinematicTraits` flags, `NavigationReadyGate(NavigationState)` (waits until `Navigation.Current == targetState`), `CinematicEndGate(CinematicState)` (waits until `Cinematic.Current != awaitedState`), `NavigationTrigger` (button wiring), `SceneTransition` (preload + additive scene loading), `EditorNavigationBootstrap` (editor-only auto-transition) |
| `Animation/` | Tween/animation utilities — `TweenTracker` (DOTween sequence composition for nudge → balance chaining), `PathHelper` (Catmull-Rom spline paths + loops, midpoint displacement for fractal lightning, fractional-index array sampling, linear resampling, prefix sums) |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `SimplePoolChannel<T>`, `ParticlePoolChannel`, `PoolableParticle`, plus the trail-flight system (`TrailSpawner`, `TrailFlight`, endpoint registries) — see `Pool/README.md` |
| `Pause/` | Pause broadcast + time-scale claim system — `PauseService`, `PausedMessage`/`ResumedMessage`, `PauseResumedGate`, `TimeScaleService`/`TimeScaleSource` (the only legal writer of `Time.timeScale`) — see `Pause/README.md` |
| `Disturbance/` | Shared screen-space disturbance field — `DisturbanceFieldService` (plain C# `IStartable` + `ITickable` + `IDisposable`; drives diffusion/reform/wind-advection blits and exposes `Stamp()` for any game system to inject disturbances; `PuffCloudView`, `BushLeaf.shader`, and future effects sample the global `_DisturbanceTex`). Configuration lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `Disturbance/README.md` |
| `Extensions/` | Extension methods — `ColorableRendererExtensions` (`BindColor` overloads for reactive color subscriptions), `WeightedPickExtensions` (`PickRandom<T>()` for `IWeightedEntry` collections — shared weighted random selection used by balloon, grid actor, and item spawning), `SlotActorExtensions` (actor query helpers), `SceneExtensions` (`SuppressRendering()` / `SceneRenderingHandle.Restore()`), `AnimationCurveExtensions`, `DisturbanceTweenExtensions` (`StampDisturbanceAlongPath` — stamps the disturbance field from a tween's `OnUpdate`), `VectorMathExtensions` (centroid, bounding radius, 2D distance helpers), plus balloon/color/list/pool/renderer/transform/vector helpers |
| `Messages/` | MessagePipe signal structs that decouple systems from one another, plus `IHitDispatcher` (the entry point for actor hits — implemented by `Game/HitPipeline`) |
| `Diagnostics/` | Debug utilities — `FPSCounter`, `FrameRateSettings` |
| `ConfigAssetCache<T>` | Editor-side lazy-cached config asset lookup (guarded `#if UNITY_EDITOR`); required by the `config-asset-cache` audit rule |
| `EnumIndexedAttribute` | Marks an array as indexed by an enum's ordinals; drawn with enum-name labels by `EnumIndexedDrawer` (in `Editor/`) |
| `IGameConfiguration` | Read-only interface for core game data — projectile settings, slot grid dimensions, prediction trace params, score trail timing, points formula |
| `IReadyGate` | Interface — `UniTask WaitAsync(CancellationToken)`; injectable precondition gate (implementations in `GameState/` and `Pause/`) |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()` |
| `ImpactEventBus` | Frame-scoped one-shot impact events — any system `Report()`s a position + radius (every disturbance stamp does), visual consumers (e.g. bush rustle) read `Pending` the same frame; cleared on `LateTick` |
| `WallLimits` | Play-field wall boundary math shared by projectile bounce and prediction |

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner`, cheats | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | cheats (`SpawnBalloonLineCheat`) | `BalloonSpawner` |
| `ActorHitMessage` | `HitPipeline` only — producers (`ProjectileHitResolver`, item handlers, cheats) route through `IHitDispatcher.Dispatch`, which runs the score/streak stage, routes the owning balloon's reaction via `BalloonControllerRegistry`, then broadcasts | `NudgeService`, `ItemActivator`, `GridActorHitController` (the hit balloon's `BalloonController` and `ScoreController` are invoked as pipeline stages, not subscribers) — carries `ISlotActor Actor`, `WorldPosition`, `ProjectileDirection`, `HitOutcome Outcome`, and a `DamageContext Context` (`Damage` defaults to 1, plus `DamageFlags` and `SourceColorId`); item handlers pass `ItemSettings.Damage`, projectile hits always use 1. Subscribers downcast `Actor` to the specific interface they need (`IBalloonModel`, `IHasColor`, `IHasNudge`, etc.) |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `BoardClearMessage` | `BoardClearController` | `BalloonControllerRegistry`, `StaticActorSpawner` |
| `ScorePointMessage` | `ScoreController` | `ColorProgressBar`, `ScoreTrailService`, `LevelUpCinematic` — one message per individual score point; carries `ColorName`, `WorldPosition`, `Score` (1-based within level), `Level` (pre-computed, including next-level renumbering), `GroupSize` and `GroupIndex` (for scatter positioning) |
| `ScoreLevelUpMessage` | `ScoreController` | `ColorProgressBar`, `LevelUpPopUp`, `ColorStreakTracker` (auto-resets the streak) |
| `ScoreTrailArrivedMessage` | `ScoreTrailService` | `ScoreController`, `ColorProgressBar`, `LevelUpCinematic` — carries `ColorName`, `Score` (the level progress value this trail represents), `Level` (the level the trail was spawned during), and `WorldPosition` |
| `LevelUpDismissedMessage` | `LevelUpPopUp` | `LevelUpCinematic`, `ColorProgressBar` |
| `LevelUpGlowTrailsMessage` | `LevelUpPopUp` | `ColorProgressBar` |
| `ProjectileDestroyedMessage` | `ProjectileView` | `ThrowerController`, `BalloonSpawner`, `ShieldCounterAnimation` |
| `ProjectileLoadedMessage` | `ThrowerController` | `ShieldCounterAnimation`, `ShieldItemHandler`, cheats |
| `ItemCheckMessage` | `BalloonSpawner` | `ItemAssigner` |
| `ItemActivatedMessage` | `ItemActivator` | `BalloonController` |
| `TransformCapturedMessage` | `BalloonController` (snapshots the hit balloon's `ITransformCapture` child, e.g. the rotating laser cross) | `LaserItemHandler` |
| `ShieldGainedMessage` | `ShieldItemHandler`, `ProjectileHitResolver` (color-streak shield rule) | `ShieldTrailController` |
| `ShieldLostMessage` | `ProjectileView` (a shield absorbed a wall bounce) | `ShieldTrailController` |
| `SpawnBlockedMessage` | `RejectedBalloonEffect` (a spawn line couldn't fit) | `PlayerHealthController`, `CameraShakeService` |
| `OverflowHeartRequestedMessage` | `RejectedBalloonEffect` | `HeartTrailController`, `HeartDrainCinematic` |
| `EndRunRequestedMessage` | `PlayerHealthController` (health reached 0) | `RunController` |
| `GameOverMessage` | `RunController` | `GameOverScreen` |
| `RunResetMessage` | `RunController` (broadcast after every `IRunResettable` service has reset, on restart) | `ColorProgressBar`, `ThrowerController` |

> `NudgeMessage` lives in `Nudge/` (not `Messages/`) — it is specific to the nudge system and carries `NudgeType` and `NudgeOverride[]` types from that namespace.
