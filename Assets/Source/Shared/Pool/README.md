# Pool

A generic object pooling system for Unity components. Pools are managed centrally through `PoolManager` and accessed by string key.

## Architecture

- **`IPoolable`** — pure lifecycle interface: `OnSpawned()` (called after activation) and `OnDespawned()` (called before deactivation). Items never reference the pool — they don't know they're pooled.
- **`PoolChannel<TItem>`** — abstract base owning a `Stack<TItem>`. `Get()` pops or calls `Create()`; skips destroyed items in the stack. `Return()` despawns, deactivates, and pushes back. On return, `SetParent(..., worldPositionStays: false)` prevents scale drift from reparenting. Subclasses implement `Create()`.
- **`PoolManager`** — injectable singleton registry keyed by `string`. Channels are registered via `Register()` or lazily via `GetOrRegister()`. Consumers call `Get<TItem>(key)` / `Return(key, item)`.

## Effect Abstraction

Effects (VFX that need to `Play` and `Stop`) have their own hierarchy, separate from raw particle pooling:

- **`IEffect`** — interface with `Play(position, tint)`, `Play(position, rotation, tint)`, and `Stop()`. Not item-specific — any system that wants to abstract a visual effect can use it.
- **`EffectView`** — abstract `MonoBehaviour` implementing `IPoolable` + `IEffect`. Holds an `Action onComplete` callback. Subclasses implement `Play()` and `Stop()`.
- **`ParticleEffectView`** — `EffectView` subclass for particle effects. Stops and clears the particle in `OnSpawned()` (prevents stale color from Play-on-Awake). Detects completion via `!_particle.IsAlive()` in `Update()`.
- **`AnimatorEffectView`** — `EffectView` subclass for animator-driven effects. Timer-based completion against the first clip's length.
- **`EffectPoolChannel`** — `PoolChannel<EffectView>` that takes an `EffectView` prefab directly. No auto-detection — the prefab must already have the correct `EffectView` subclass attached.

## Particle Pooling (simple effects)

For simple particle effects that don't need the full `EffectView` contract:

- **`PoolableParticle`** — `MonoBehaviour` implementing `IPoolable` and `IEffect`. `OnSpawned()` stops the particle to prevent stale color from Play-on-Awake. The consumer calls `Play(pos, color, onComplete)` and handles the return in the `onComplete` callback.
- **`ParticlePoolChannel`** — `PoolChannel<PoolableParticle>` that takes a `GameObject` prefab and adds `PoolableParticle` via `AddComponent`. Used for balloon pop VFX and shield VFX.

## Return Responsibility

**The consumer that calls `Get()` is responsible for calling `Return()`.**
Items signal completion via callbacks passed to their `Play()` or `Setup()` calls. The consumer bundles the `Return()` into that callback.

| Item | Completion signal | Who returns |
|---|---|---|
| `PoolableParticle` | `Play(pos, color, onComplete)` — caller supplies the callback | Consumer (e.g. `BalloonView`, `ProjectileShieldView`) |
| `EffectView` (any subclass) | `Play(pos, tint, onComplete)` — caller supplies the callback | Consumer (e.g. item handlers) |
| `ScorePointTrail` | `Setup(target, color, config, onComplete)` — fires on tween completion | `ScoreTrailService` |
| `ScoreNotice` | `Show(score, color, onComplete)` — fires from `OnAnimationCompleted` animation event | `ColorProgressBar` |
| `BalloonView` | No completion callback — returned directly on hit | `BalloonController` |
| `ProjectileView` | No completion callback — returned directly on death | `ThrowerController` |

## Channels

| Channel | Key | Item | Creates via |
|---|---|---|---|
| `ParticlePoolChannel` | prefab name (e.g. `"PopVfx"`) | `PoolableParticle` | `Object.Instantiate` + `AddComponent<PoolableParticle>` |
| `EffectPoolChannel` | prefab name | `EffectView` (subclass) | `Object.Instantiate` |
| `ProjectilePoolChannel` | prefab name | `ProjectileView` | `CreateChildFromPrefab` (VContainer) |
| `BalloonPoolChannel` | `"Balloon"` | `BalloonView` | `CreateChildFromPrefab` (VContainer) |
| `ItemVisualPoolChannel` | prefab name | `ItemVisualView` | `Object.Instantiate` |
| `ScoreTrailPoolChannel` | `ScoreTrail_{color}` | `ScorePointTrail` | `Object.Instantiate` |
| `ScoreNoticePoolChannel` | `ScoreNotice_{color}` | `ScoreNotice` | `Object.Instantiate` |

## Usage

```csharp
// === Registration (once, during setup) ===
_poolManager.Register(prefab.name, new ProjectilePoolChannel(scope, prefab));

// === Getting / Returning ===
var view = _poolManager.Get<ProjectileView>(prefab.name);
_poolManager.Return(prefab.name, view);

// === Lazy registration + get — particle ===
var particle = _poolManager.GetOrRegister(prefab.name, () => new ParticlePoolChannel(prefab));
particle.Play(pos, color, () => _poolManager.Return(prefab.name, particle));

// === Lazy registration + get — EffectView ===
var effect = _poolManager.GetOrRegister(prefab.name, () => new EffectPoolChannel(effectPrefab));
effect.Play(pos, tint, () => _poolManager.Return(prefab.name, effect));
```

## Adding a new pool

1. Create a class extending `PoolChannel<TItem>`
2. Accept the creation key (prefab, config, etc.) in the constructor
3. Implement `Create()` — instantiate and configure the item; start it deactivated
4. Have the pooled component implement `IPoolable` for lifecycle hooks
5. Register via `_poolManager.Register(key, new YourChannel(...))`
6. The consumer passes a return callback through the item's public API; the callback calls `Return(key, item)` on completion
