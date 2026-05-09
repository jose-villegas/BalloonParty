# Pool

A generic object pooling system for Unity components. Pools are managed centrally through `PoolManager` and accessed by channel type. Each channel is bound to what it creates at construction — callers just call `Get()`.

## Architecture

- **`PoolChannel<TItem>`** — abstract base. Owns a `Stack<TItem>` of inactive instances. `Get()` pops or calls `Create()`. `Return()` despawns and pushes back. Subclasses implement `Create()` with their specific instantiation logic.
- **`PoolManager`** — injectable singleton registry. Stores channels keyed by `(Type, object)` so multiple channels of the same type can coexist (e.g. one `VfxPoolChannel` per particle prefab). Channels are created lazily via a factory on first access and cached for subsequent calls.
- **`IPoolable`** — interface on pooled components: `OnSpawned()` (called after activation) and `OnDespawned()` (called before deactivation).

## Channels

| Channel | Key | Item | Creates via |
|---|---|---|---|
| `VfxPoolChannel` | `ParticleSystem` prefab | `PoolableParticle` | `Object.Instantiate` + `AddComponent<PoolableParticle>` |
| `ProjectilePoolChannel` | `ProjectileLifetimeScope` prefab | `ProjectileView` | `CreateChildFromPrefab` (VContainer) |

## Usage

```csharp
// VFX — channel per prefab, auto-returns after particle lifetime
_poolManager.Channel(prefab, () => new VfxPoolChannel(prefab)).Get().Play(pos, color);

// Projectile — single channel, manual return
var view = _poolManager.Channel(() => new ProjectilePoolChannel(scope, prefab)).Get();
_poolManager.Channel<ProjectilePoolChannel>().Return(view);
```

## Adding a new pool

1. Create a class extending `PoolChannel<TItem>`
2. Accept the creation key (prefab, config, etc.) in the constructor
3. Implement `Create()` — instantiate and configure the item
4. Have the pooled component implement `IPoolable` for reset/cleanup
5. Access via `_poolManager.Channel(key, () => new YourChannel(key)).Get()`

## Auto-return

`PoolableParticle` auto-returns itself to the pool when its particle system finishes (`!IsAlive()`). Other pooled items (projectile, balloons) are returned explicitly by their owning controller.

