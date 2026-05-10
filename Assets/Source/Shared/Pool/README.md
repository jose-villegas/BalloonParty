# Pool

A generic object pooling system for Unity components. Pools are managed centrally through `PoolManager` and accessed by string key. Each channel is bound to what it creates at construction — callers just call `Get(key)`.

## Architecture

- **`IPoolChannel`** — non-generic marker interface with `SetParent(Transform)`. Allows `PoolManager` to store heterogeneous channels in a type-safe `Dictionary<string, IPoolChannel>`.
- **`PoolChannel<TItem>`** — abstract base implementing `IPoolChannel`. Owns a `Stack<TItem>` of inactive instances. `Get()` pops or calls `Create()`; skips destroyed items in the stack. `Return()` despawns, deactivates, and pushes back. Subclasses implement `Create()` with their specific instantiation logic.
- **`PoolManager`** — injectable singleton registry. Stores channels in a `Dictionary<string, IPoolChannel>` keyed by `string`. Channels are registered explicitly via `Register()`, then consumers call `Get<TItem>(key)` / `Return(key, item)`. `GetOrRegister()` combines registration and retrieval for lazy-init channels.
- **`IPoolable`** — pure lifecycle interface on pooled components: `OnSpawned()` (called after activation) and `OnDespawned()` (called before deactivation). Items never reference the pool — they don't know they're pooled.

## Return Responsibility

**The consumer that calls `Get()` is responsible for calling `Return()`.** Pooled items signal completion via their own public API callbacks (e.g. `Action onComplete` parameters), and the consumer bundles the `Return()` call into that callback. Items never self-return.

| Item | Completion signal | Who returns |
|---|---|---|
| `PoolableParticle` | `Play(pos, color, onComplete)` — fires when `!IsAlive()` | Consumer (e.g. `BalloonView`, `ProjectileShieldView`) |
| `ScorePointTrail` | `Setup(target, color, config, onComplete)` — fires on tween completion | `ColorProgressBar` |
| `ScoreNotice` | `Show(score, color, onComplete)` — fires from `OnAnimationCompleted` animation event | `ColorProgressBar` |
| `BalloonView` | No completion callback — returned directly on hit | `BalloonController` |
| `ProjectileView` | No completion callback — returned directly on death | `ThrowerController` |

## Channels

| Channel | Key | Item | Creates via |
|---|---|---|---|
| `VfxPoolChannel` | prefab name (e.g. `"PopVfx"`) | `PoolableParticle` | `Object.Instantiate` + `AddComponent<PoolableParticle>` |
| `ProjectilePoolChannel` | prefab name | `ProjectileView` | `CreateChildFromPrefab` (VContainer) |
| `BalloonPoolChannel` | `"Balloon"` | `BalloonView` | `IObjectResolver.Instantiate` |
| `ScoreTrailPoolChannel` | `ScoreTrail_{color}` | `ScorePointTrail` | `Object.Instantiate` |
| `ScoreNoticePoolChannel` | `ScoreNotice_{color}` | `ScoreNotice` | `Object.Instantiate` |

## Usage

```csharp
// === Registration (once, during setup) ===
_poolManager.Register(prefab.name, new ProjectilePoolChannel(scope, prefab));

// === Getting / Returning ===
var view = _poolManager.Get<ProjectileView>(prefab.name);
_poolManager.Return(prefab.name, view);

// === Lazy registration + get (one channel per key) ===
var vfx = _poolManager.GetOrRegister(prefab.name, () => new VfxPoolChannel(prefab));
vfx.Play(pos, color, () => _poolManager.Return(prefab.name, vfx));
```

## Adding a new pool

1. Create a class extending `PoolChannel<TItem>`
2. Accept the creation key (prefab, config, etc.) in the constructor
3. Implement `Create()` — instantiate and configure the item; start it deactivated
4. Have the pooled component implement `IPoolable` for lifecycle hooks
5. Register via `_poolManager.Register(key, new YourChannel(...))`
6. On `Get()`, the consumer passes a return callback through the item's public API
7. The callback calls `_poolManager.Return(key, item)` when the item signals completion
