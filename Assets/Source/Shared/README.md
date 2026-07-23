# Shared

Types and utilities used across multiple features.

## Contents

| File / Folder | What it provides |
|---|---|
| `Rendering/` | Visual rendering utilities — `ColorableRenderer` (abstract base + generic `<T>`), `CompositeColorableRenderer`, `SortingHelper` (sorting order calculations), `GizmoDrawingHelper` (Gizmos API primitives for `OnDrawGizmos`, guarded in `#if UNITY_EDITOR`) |
| `GameState/` | App-wide navigation and cinematic tracking — `Navigation` (static `ReactiveProperty<NavigationState>` with `Current` and `TransitionTo`), `Cinematic` (static `ReactiveProperty<CinematicState>` with `Current`, `IsPlaying`, `Begin`, `End`), `NavigationState` enum, `CinematicState` enum (`None`, `LevelUpPanIn`, `LevelUpRestore`), `IReadyGate` (interface — `UniTask WaitAsync(CancellationToken)`; the shared gate contract implemented by the gates below — injected by concrete type, not polymorphically), `NavigationReadyGate(INavigation, NavigationState)` (waits until `Current == targetState`; takes an injectable `INavigation` so it's testable without the static `Navigation`), `CinematicEndGate(CinematicState)` (waits until `Cinematic.Current != awaitedState`), `NavigationTrigger` (button wiring), `SceneTransition` (preload + additive scene loading), `EditorNavigationBootstrap` (editor-only auto-transition), `LaunchAscend` (static hand-off for the launch-screen cloud scroll — `Begin`/`IsActive`/`StartTime`/`Duration`/`Scroll`, set by `Game/LaunchPlayTrigger` and read by `Scenario/CloudFieldService`, no cross-scene DI needed) |
| `Animation/` | Tween/animation and math utilities — `TweenTracker` (DOTween sequence composition for nudge → balance chaining), `PathHelper` (Catmull-Rom spline paths + loops, midpoint displacement for fractal lightning, fractional-index array sampling, linear resampling, prefix sums), `VectorMathHelper` (centroid, bounding radius for point sets) |
| `Math/` | Lightweight value-type springs — `DampedSpring2D` (second-order damped spring tracking a 2D target), `DampedSpring1D` (scalar variant). Pure structs, no Unity lifecycle, no allocations. Used for physically-driven animation (shield field deformation, squash-on-impact) |
| `Pool/` | Generic object pooling system — `PoolManager`, `PoolChannel<T>`, `IPoolable`, `EffectView`, `SimplePoolChannel<T>`, `ParticlePoolChannel`, `PoolableParticle` (see `Pool/README.md`) |
| `Pause/` | Broadcast pause coordination and time-scale ownership — `PauseService` (reference-counted per `PauseSource`; live systems gate on its `IsAnyPaused` reactive property), `TimeScaleService` (sole legal writer of `Time.timeScale`; claim/release by `TimeScaleSource`, lowest active claim wins) (see `Pause/README.md`) |
| `Disturbance/` | Shared screen-space disturbance field — `DisturbanceFieldService` (plain C# `IStartable` + `ITickable` + `IDisposable`; owns a camera-sized `ARGBHalf` RT pair for density + displacement; runs diffusion/reform/wind-advection blits each tick; exposes `Stamp()` with optional `duration` for any game system to inject disturbances; `PuffCloudView` and future effects sample `FieldTexture` via shader). Configuration lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `PLAN-PuffCloudSimulation.md` P4 |
| `SceneLight/` | Shared light field — the disturbance-field architecture applied to light: `SceneLightFieldService` (owns the field RT + the ambient globals `_SceneLightDir`/`_SceneLightColor`/`_SceneLightIntensity`), `Light` (reactive per-light model; `RegisterLight`/dispose turns it on/off). Consumers sample it through `SceneLight.cginc`. See `SceneLight/README.md` |
| `Extensions/` | Extension methods — `ColorableRendererExtensions` (`BindColor` overloads for reactive color subscriptions), `WeightedPickExtensions` (`PickRandom<T>()` for `IWeightedEntry` collections — shared weighted random selection used for balloon and item spawning, over the resolver's resolved pick lists), `SlotActorExtensions` (actor query helpers), `SceneExtensions` (`SuppressRendering()` / `SceneRenderingHandle.Restore()`), `AnimationCurveExtensions` |
| `Messages/` | MessagePipe signal structs that decouple systems from one another |
| `Diagnostics/` | Debug utilities — `FPSCounter`, `FrameRateSettings` |
| `IProjectileFlightConfig` / `ISlotGridConfig` / `IPredictionTraceConfig` / `IRunConfig` / `IScoreTrailConfig` | The focused read-only config contracts consumed across assemblies — projectile flight tuning, slot-grid layout, prediction-trace params, run rules, and score-trail timing respectively. Backed by the `ProjectileFlightConfig` / `SlotGridConfig` / `PredictionTraceConfig` / `RunConfig` / `ScoreTrailBehaviourConfiguration` SOs (they replaced the former single `IGameConfiguration` umbrella) |
| `IEffect` | Interface for poolable visual effects — `Play(position, tint)`, `Play(position, rotation, tint)`, `Stop()` |
| `EditorAssetCache<T>` | Editor-only lazy cache for a config `ScriptableObject` asset by type — lives in the `com.balloonparty.editorui` package (`EditorUI.Utilities`). Editor config lookups go through this instead of inlining `FindAssets` + `LoadAssetAtPath` |
| `ImpactEventBus` | Frame-scoped list of impact events (position + radius); written via `Report`, cleared every `LateTick` |
| `PathTrace` | Shared skeleton for tracing a projectile's wall-reflected path ahead (`IsClearAhead`) — reflects off each wall via `WallLimits`, leaving the per-segment occupancy test to the caller |
| `WallLimits` | The four play-area walls unpacked from `IProjectileFlightConfig.LimitsClockwise` — wall-crossing (`TryFindCrossing`), billiard mirror-reflect (`Reflect`), in-bounds clamp (`ClampInside`) |
| `EnumIndexedAttribute` | `PropertyAttribute` that labels a serialized array indexed by enum ordinal with the enum value's name instead of "Element N" |

This table covers the folders and the types other features reach for most; a few small editor-only attributes living alongside these files aren't broken out separately.

## Messages

Messages are the signals that decouple systems from one another. A publisher fires a message; any number of subscribers react independently.

| Message | Published by | Consumed by |
|---|---|---|
| `BalanceBalloonsMessage` | `ProjectileView`, `BalloonSpawner` | `BalloonBalancer` |
| `SpawnBalloonLineMessage` | `SceneTransition` (game start), cheats | `BalloonSpawner` |
| `ActorHitMessage` | `HitPipeline` only — producers (`ProjectileHitResolver`, item handlers, cheats) route through `IHitDispatcher.Dispatch`, which runs the score/streak stage first, then broadcasts | `BalloonController`, `NudgeService`, `ItemActivator`, `GridActorHitController` (`ScoreController` is invoked as a pipeline stage, not a subscriber) — carries `ISlotActor Actor`, `WorldPosition`, `ProjectileDirection`, `HitOutcome Outcome`, and a `DamageContext Context` (whose `Damage` defaults to 1); item handlers pass `ItemSettings.Damage`, projectile hits always use 1. Subscribers downcast `Actor` to the specific interface they need (`IBalloonModel`, `IHasColor`, `IHasNudge`, etc.) |
| `BalloonDeflectedMessage` | `BalloonController` (on deflect) | `ProjectileView` (color-tracking on deflect hit) |
| `ScorePointsGroupMessage` | `ScoreController` | `ScoreTrailService`, `LevelUpCinematic` — one message per resolved attribution color; carries `ColorName`, `WorldPosition`, `Points` (group total, post-multiplier post-cap), `LastScore` (cumulative per-color number of the group's last point), `Multiplier` (the streak multiplier, data only), and a `FirstScore` convenience property |
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
