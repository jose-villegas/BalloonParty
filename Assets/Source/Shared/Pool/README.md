# Pool

A generic object pooling system for Unity components. Pools are managed centrally through `PoolManager` and accessed by string key. Each channel is bound to what it creates at construction — callers just call `Get(key)`.

## Architecture

- **`IPoolChannel`** — non-generic marker interface. Allows `PoolManager` to store heterogeneous channels in a type-safe `Dictionary<string, IPoolChannel>` instead of `Dictionary<string, object>`.
- **`PoolChannel<TItem>`** — abstract base implementing `IPoolChannel`. Owns a `Stack<TItem>` of inactive instances. `Get()` pops or calls `Create()`. `Return()` despawns and pushes back. Subclasses implement `Create()` with their specific instantiation logic.
- **`PoolManager`** — injectable singleton registry. Stores channels in a `Dictionary<string, IPoolChannel>` keyed by `string`. Channels are registered explicitly via `Register()`, then consumers call `Get<TItem>(key)` / `Return(key, item)` without needing to know the channel type.
- **`IPoolable`** — interface on pooled components: `OnSpawned()` (called after activation) and `OnDespawned()` (called before deactivation).

## Channels

| Channel | Key | Item | Creates via |
|---|---|---|---|
| `VfxPoolChannel` | prefab name (e.g. `"PopVfx"`) | `PoolableParticle` | `Object.Instantiate` + `AddComponent<PoolableParticle>` |
| `ProjectilePoolChannel` | prefab name (from `ThrowerSettings.ProjectileScopePrefab.name`) | `ProjectileView` | `CreateChildFromPrefab` (VContainer) |

## Usage

```csharp
// === Registration (once, during setup) ===

// Projectile — explicit register in Start(), key derived from prefab name
_poolManager.Register<ProjectileView>(prefab.name,
    new ProjectilePoolChannel(scope, prefab));

// === Getting / Returning ===

// Projectile — get and return by key
var view = _poolManager.Get<ProjectileView>(prefab.name);
_poolManager.Return(prefab.name, view);

// VFX — register-on-first-use via GetOrRegister (one channel per prefab)
_poolManager.GetOrRegister<PoolableParticle>(prefab.name, () => new VfxPoolChannel(prefab))
    .Play(pos, color);
```

## Adding a new pool

1. Create a class extending `PoolChannel<TItem>`
2. Accept the creation key (prefab, config, etc.) in the constructor
3. Implement `Create()` — instantiate and configure the item
4. Have the pooled component implement `IPoolable` for reset/cleanup
5. Register via `_poolManager.Register<TItem>(prefab.name, new YourChannel(...))`
6. Access via `_poolManager.Get<TItem>(prefab.name)`

## Auto-return

`PoolableParticle` auto-returns itself to the pool when its particle system finishes (`!IsAlive()`). Other pooled items (projectile, balloons) are returned explicitly by their owning controller.
