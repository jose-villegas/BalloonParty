# Pool

A generic object pooling system for Unity components. Pools are managed centrally through `PoolManager` and accessed by string key.

## Architecture

- **`IPoolable`** — pure lifecycle interface: `OnSpawned()` (called after activation) and `OnDespawned()` (called before deactivation). Items never reference the pool — they don't know they're pooled.
- **`PoolChannel<TItem>`** — abstract base owning a `Stack<TItem>`. `Get()` pops or calls `Create()`; skips destroyed items in the stack. `Return()` despawns, deactivates, and pushes back. On return, `SetParent(..., worldPositionStays: false)` prevents scale drift from reparenting. Subclasses implement `Create()`.
- **`PoolManager`** — injectable singleton registry keyed by `string`. Channels are registered via `Register()` or lazily via `GetOrRegister()`. Consumers call `Get<TItem>(key)` / `Return(key, item)`. `KeyFor(prefab)` returns a cached pool key for a prefab — `Object.name` allocates a fresh managed string on every access, so hot paths (per-pop VFX) resolve keys through the cache instead.

## Effect Abstraction

Effects (VFX that need to `Play` and `Stop`) have their own hierarchy, separate from raw particle pooling:

- **`IEffect`** — interface with `Play(position, tint)`, `Play(position, rotation, tint)`, and `Stop()`. Not item-specific — any system that wants to abstract a visual effect can use it.
- **`EffectView`** — abstract `MonoBehaviour` implementing `IPoolable` + `IEffect`. Holds an `Action onComplete` callback. Subclasses implement `Play()` and `Stop()`.
- **`ParticleEffectView`** — `EffectView` subclass for particle effects. Stops and clears the particle in `OnSpawned()` (prevents stale color from Play-on-Awake). Detects completion natively: `main.stopAction = ParticleSystemStopAction.Callback` (set once in `Awake()`) drives `OnParticleSystemStopped()` instead of polling `IsAlive()` every frame. Requires the `ParticleSystem` on the *same* GameObject — Unity only sends the callback there. If a child system (sub-emitter) outlives the root's own stop, `OnParticleSystemStopped` falls back to a cheap `IsAlive(true)` check in `Update()` until the children finish too, matching the old default (`IsAlive()` = `withChildren: true`).
- **`AnimatorEffectView`** — `EffectView` subclass for animator-driven effects. Timer-based completion against the first clip's length.
- Effect views are pooled via the generic **`SimplePoolChannel<EffectView>`** (see below) — the prefab must already have the correct `EffectView` subclass attached.

## Particle Pooling (simple effects)

For simple particle effects that don't need the full `EffectView` contract:

- **`PoolableParticle`** — `MonoBehaviour` implementing `IPoolable` and `IEffect`. `OnSpawned()` stops the particle to prevent stale color from Play-on-Awake. The consumer calls `Play(pos, color, onComplete)` and handles the return in the `onComplete` callback. Completion is detected the same way as `ParticleEffectView`: `stopAction = Callback` → `OnParticleSystemStopped()`, with an `IsAlive(true)` fallback in `Update()` for prefabs whose child sub-emitters outlive the root's own stop (e.g. the tough-balloon-pop smoke child).
- **`ParticlePoolChannel`** — `PoolChannel<PoolableParticle>` that takes a `GameObject` prefab and adds `PoolableParticle` via `AddComponent`. Used for balloon pop VFX and shield VFX.

## Return Responsibility

**The consumer that calls `Get()` is responsible for calling `Return()`.**
Items signal completion via callbacks passed to their `Play()` or `Setup()` calls. The consumer bundles the `Return()` into that callback.

| Item | Completion signal | Who returns |
|---|---|---|
| `PoolableParticle` | `Play(pos, color, onComplete)` — caller supplies the callback | Consumer (e.g. `BalloonView`, `ProjectileShieldView`) |
| `EffectView` (any subclass) | `Play(pos, tint, onComplete)` — caller supplies the callback | Consumer (e.g. item handlers) |
| `FlyingTrail` | `Setup(target, color, config, onComplete)` — fires on tween completion | `ScoreTrailService` |
| `ProgressNotice` | `Show(score, onComplete, color?)` — fires from `OnAnimationCompleted` animation event | `ColorProgressBar` |
| `BalloonView` | No completion callback — returned directly on hit | `BalloonController` |
| `ProjectileView` | No completion callback — returned directly on death | `ThrowerController` |

### Fire-and-forget helpers

`PoolManagerExtensions` (`Shared/Extensions/`) wraps the common effect flow into one call: `pool.PlayParticle(prefab, position, tint)` and `pool.PlayEffect(prefab, position, tint)` (plus rotation overloads). Each resolves the key via `KeyFor`, lazily registers a `ParticlePoolChannel` / `SimplePoolChannel<EffectView>`, gets the item, binds it to the pool via `BindPool(pool, key)`, and plays it with the item's own `ReturnToPool` action as the completion callback — the effect returns itself, so the caller wires nothing.

## Injecting Pool Channel

For prefabs that have `[Inject]` fields but don't need their own VContainer child scope (all injected dependencies are singletons from an ancestor scope):

- **`InjectingPoolChannel<TItem>`** — generic `PoolChannel<TItem>` that takes an `IObjectResolver` and a prefab. `Create()` calls `resolver.Instantiate(prefab, container)` — VContainer's built-in extension that deactivates the prefab, clones, injects all `[Inject]` fields from the parent container, and reactivates. Much faster than `CreateChildFromPrefab` because it skips container creation, `Configure()`, and `RegisterComponentInHierarchy` traversals. Any `LifetimeScope` components on the prefab should have `autoRun` disabled in the Inspector.
- **`BalloonPoolChannel`** and **`ProjectilePoolChannel`** extend `InjectingPoolChannel<T>` as thin type aliases.

## Simple Pool Channel (no injection)

For prefabs that need **no** VContainer injection — the common case for effects, trails, and HUD notices:

- **`SimplePoolChannel<TItem>`** — generic `PoolChannel<TItem>` whose `Create()` instantiates the prefab under the container, deactivates it, and returns the `TItem` component. Two constructors: one takes the `TItem` component prefab directly, the other takes a `GameObject` prefab and resolves the component via `GetComponent<TItem>()`. This single channel replaces the former per-type `EffectPoolChannel`, `ItemVisualPoolChannel`, `ScoreTrailPoolChannel`, `ShieldTrailPoolChannel`, and `ProgressNoticePoolChannel` shells.

## Channels

| Channel | Key | Item | Creates via |
|---|---|---|---|
| `InjectingPoolChannel<T>` | (varies) | any `IPoolable` | `Object.Instantiate` + `InjectGameObject` |
| `BalloonPoolChannel` | prefab name | `BalloonView` | `InjectingPoolChannel` |
| `ProjectilePoolChannel` | prefab name | `ProjectileView` | `InjectingPoolChannel` |
| `ParticlePoolChannel` | prefab name (e.g. `"PopVfx"`) | `PoolableParticle` | `Object.Instantiate` + `AddComponent<PoolableParticle>` |
| `SimplePoolChannel<T>` | (varies — prefab name, `ScoreTrail_{color}`, `StreakNotice_{color}`, …) | any non-injected `IPoolable` (`EffectView`, `ItemVisualView`, `FlyingTrail`, `ProgressNotice`) | `Object.Instantiate` (+ `GetComponent<T>` for the `GameObject` overload) |

## Usage

```csharp
// === Registration — injecting channel (prefabs with [Inject] fields) ===
_poolManager.Register(prefab.name, new BalloonPoolChannel(resolver, prefab));

// === Registration (once, during setup) ===
_poolManager.Register(prefab.name, new ParticlePoolChannel(prefab));

// === Getting / Returning ===
var view = _poolManager.Get<ProjectileView>(prefab.name);
_poolManager.Return(prefab.name, view);

// === Lazy registration + get — particle ===
var particle = _poolManager.GetOrRegister(prefab.name, () => new ParticlePoolChannel(prefab));
particle.Play(pos, color, () => _poolManager.Return(prefab.name, particle));

// === Lazy registration + get — EffectView ===
var effect = _poolManager.GetOrRegister(prefab.name, () => new SimplePoolChannel<EffectView>(effectPrefab));
effect.Play(pos, tint, () => _poolManager.Return(prefab.name, effect));
```

## Pre-warming

Pre-warming creates pool items ahead of time so the first `Get()` call hits a warm cache instead of calling `Create()`.

- **`Prewarm(int count)`** — synchronous, creates all items in the current frame. Use for lightweight items (simple prefabs, particles).
- **`PrewarmAsync(int count, CancellationToken)`** — spreads creation across frames (one item per `UniTask.Yield`). Use for heavier items (e.g. balloons, projectiles).
- **`PoolManager.PrewarmAllAsync(counts, ct)`** — pre-warms a set of registered channels by key, skipping channels that already have enough items.

Pre-warmed items are created, deactivated, and pushed onto the available stack. They never have `OnSpawned()` called — that only happens on the first `Get()`.

```csharp
// === Synchronous (lightweight items) ===
_poolManager.Register(key, new ParticlePoolChannel(prefab));
_poolManager.Prewarm(key, 4);

// === Async (heavier items — e.g. balloons with injection) ===
_poolManager.Register(key, new BalloonPoolChannel(resolver, prefab));
await _poolManager.PrewarmAsync(key, 36, ct);

// === Batch async — multiple channels by key ===
var counts = new Dictionary<string, int>
{
    { "Balloon", 30 },
    { "ToughBalloon", 3 },
};
await _poolManager.PrewarmAllAsync(counts, ct);
```

## Adding a new pool

1. Create a class extending `PoolChannel<TItem>` — or use `InjectingPoolChannel<TItem>` if the prefab has `[Inject]` fields
2. Accept the creation key (prefab, config, etc.) in the constructor
3. Implement `Create()` — instantiate and configure the item; start it deactivated
4. Have the pooled component implement `IPoolable` for lifecycle hooks
5. Register via `_poolManager.Register(key, new YourChannel(...))`
6. The consumer passes a return callback through the item's public API; the callback calls `Return(key, item)` on completion

## Trail Utilities

Composable plain-C# helpers for trail orb services. Pick the level that matches your feature's complexity:

### `TrailSpawner`

Spawn-and-forget: handles pool get → position → setup → return on arrival. Accepts an optional `sortingOrder` to override the trail's sorting layer (used by glow trails at `3200`).

```csharp
var spawner = new TrailSpawner(poolManager, "MyTrail", prefab);                       // common case: pools via SimplePoolChannel
var spawner = new TrailSpawner(poolManager, "MyTrail", () => new MyPoolChannel(prefab)); // custom channel (e.g. injected prefabs)
spawner.Spawn(from, to, duration, color, onArrived);
spawner.Spawn(from, to, duration, color, onArrived, useUnscaledTime: true);  // unscaled time
spawner.SpawnBurst(center, burstTo, target, burstDur, traceDur, color, onArrived);  // two-phase: scatter then trace
spawner.SpawnFollow(from, () => balloon.position, duration, onArrived);  // curve-eased, homes on a moving target
```

`SpawnFollow` (backed by `FlyingTrail.SetupFollow`) eases from its launch point to a **live-updating** target over `duration` along the move curve, instead of tweening to a fixed point — used by `HeartTrailController` so an overflow heart lands on its balloon even while the pile compacts up under it. It honours `TrailMotion` like the fixed-point flights.

Used by `ScoreTrailService` for score trail spawning, `ShieldTrailController` for shield trails, `HeartTrailController` for overflow heart trails, and `LevelUpPopUp` for glow trails.

### `TrailEndpointRegistry` + `ITrailEndpoint`

The fixed anchor a trail flies **to** or **from**, resolved by key instead of each feature wiring its own position provider. A view registers under a key; a controller resolves it when spawning:

```csharp
registry.Register(TrailEndpointKeys.Heart, new TransformTrailEndpoint(anchor));  // scope build / view Start
if (registry.TryGet(TrailEndpointKeys.Heart, out var e)) { spawner.Spawn(e.Center, popPos, dur); }
```

`ITrailEndpoint` exposes `Center` and `RandomPosition()` — point anchors return `Center` for both; an area (a progress bar) spreads arrivals. Implementations: `TransformTrailEndpoint` (wraps a fixed anchor `Transform`; used for shield target / heart source) and `ColorProgressBar` (registers per palette-colour name via `ScoreTrailService.RegisterTarget`). The registry is a `GameLifetimeScope` singleton so child UI scopes resolve the same instance; registrations are long-lived (not reset per run). Well-known single-anchor keys live in `TrailEndpointKeys`; score endpoints key on colour name.

### `TrailFlightRegistry<TId>`

Identity-based flight registry with per-trail control and bulk operations. Each trail is wrapped in a `TrailFlight` value object exposing transport-style commands: pause, resume, stop, complete, and speed control. The registry does **not** own spawning — the service spawns and calls `Register`/`Unregister`.

#### `TrailFlight`

Per-trail controller wrapping DOTween operations on a `Transform`. Tracks `FlightPhase` (`Idle`, `InFlight`, `Paused`), exposes `Origin`, `Speed`, and `Transform`.

```csharp
flight.Pause();          // DOPause, phase → Paused
flight.Resume();         // DOPlay, phase → InFlight
flight.Stop();           // DOKill, snap to origin
flight.Complete();       // DOComplete, fires onComplete callbacks
flight.SetSpeed(0.5f);   // timeScale on all tweens
flight.SetUnscaledTime(true); // ignore Time.timeScale
```

#### Registry API

```csharp
var registry = new TrailFlightRegistry<TrailId>();

// After spawning:
var flight = registry.Register(id, trail.transform, origin);

// On arrival:
registry.Unregister(id);

// Lookup:
if (registry.TryGet(id, out var f)) { /* use f */ }

// Bulk operations (snapshot-safe — CompleteAll clears before iterating):
registry.PauseAll();
registry.ResumeAll();
registry.CompleteAll();
registry.StopAll();

// Speed control:
registry.SetSpeedAll(0.5f);
```

Used by `ScoreTrailService` for trail identity tracking and cinematic integration with `LevelUpCinematic`.

