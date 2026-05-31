# Actor Archetype

Concrete grid actor models and the Puff cloud visual system.

## Actor Models

| File | What it does |
|---|---|
| `PuffObstacleModel` | Structural obstacle — `IWriteableSlotActor` + `IPassThrough`. Occupies a slot, contributes weight, but allows animation paths to pass through. Gains `ClusterId` linking it to a `PuffCluster`. |
| `BushObstacleModel` | Structural obstacle — blocks traversal, no hit response |
| `DeflectorActorModel` | Indestructible hitable — always `Deflect`, no `HitsRemaining` |
| `AbsorberActorModel` | Indestructible hitable — always `Absorb`, kills the projectile |
| `GatekeeperActorModel` | Durability-based column blocker — `Deflect` on survival, `Pop` on death |
| `GridActorType` | Enum identifying actor types — `Puff`, etc. |

## Grid Actor View & Pool

| File | What it does |
|---|---|
| `GridActorView` | `MonoBehaviour` view for grid actors — `IPoolable` + `ISlotActorView` + `TweenTracker`. Invisible for Puff slots (cloud visual is separate). |
| `GridActorPoolChannel` | Pool channel for `GridActorView` prefabs |

## Puff Cloud System

The Puff cloud system renders procedural GPU-driven clouds over groups of adjacent Puff slots. Multiple Puff slots that are hex-adjacent merge into a single continuous cloud body. The system follows MVC:

- **Model:** `PuffCluster` + `PuffClusterRegistry`
- **View:** `PuffCloudView`
- **Controller:** `PuffCloudViewController`

See `Plans/PLAN-PuffCloudSimulation.md` for the full design plan.

### Model Layer

| File | What it does |
|---|---|
| `PuffCluster` | Plain C# model — a group of hex-adjacent Puff slots. Stores `Slots` (list of grid indices), `WorldBounds` (AABB covering all member slots + padding), and `ClusterId`. |
| `PuffClusterChangedEvent` | Event struct published by `PuffClusterRegistry` — carries `ClusterId`, `ChangeType` (`Created` / `Resized` / `Removed`), and the affected `PuffCluster`. |
| `PuffClusterRegistry` | `IStartable` + `IDisposable` — subscribes to `SlotGrid.OnChanged`, flood-fills hex adjacency via `SlotGrid.HexNeighborIndices` to form clusters, and publishes `PuffClusterChangedEvent` via `OnClusterChanged`. Also calls `RebuildAll()` on `Start()` to pick up pre-existing Puffs. Handles cluster merging (new Puff bridges two clusters), splitting (removed Puff was bridging), and resize (cluster gains/loses a slot). |

### View Layer

| File | What it does |
|---|---|
| `PuffCloudView` | `MonoBehaviour` (`[ExecuteAlways]`, `IPoolable`) — drives the `BalloonParty/Grid/PuffCloud` shader on a `SpriteRenderer` quad. Manages density `RenderTexture` ping-pong pair for disturbance/reform. Exposes `Configure(positions, bounds, settings)` for cluster configuration and `StampDisturbance(...)` for punching holes in the cloud. Pushes `_TimeOffset`, `_SlotCentersWorld`, `_SlotCount`, and `_DensityTex` via `MaterialPropertyBlock` each frame. |
| `PuffCloudPoolChannel` | Pool channel for `PuffCloudView` prefabs |

### Controller Layer

| File | What it does |
|---|---|
| `PuffCloudViewController` | `IStartable` — subscribes to `PuffClusterRegistry.OnClusterChanged` and manages pooled `PuffCloudView` lifecycle. Spawns a view on `Created`, reconfigures on `Resized`, returns to pool on `Removed`. Applies sorting layer and order from `PuffCloudSettings`. |

### Configuration

`PuffCloudSettings` (ScriptableObject in `Configuration/`) holds all tuning knobs — noise animation speed, density field resolution and timing, wind parameters, displacement, visual padding, sorting layer, and disturbance radii/strengths. Also holds the `CloudPrefab` reference.

### Prefabs

- **`Puff.prefab`** — `GridActorView` + `TweenTracker` only. Invisible grid occupancy marker.
- **`PuffCloud.prefab`** — `SpriteRenderer` (PuffCloud material, no sprite) + `PuffCloudView`. Pooled by `PuffCloudPoolChannel`, referenced by `PuffCloudSettings.CloudPrefab`.

### Shaders

All in `Assets/Shaders/BalloonParty/Grid/`:

| Shader | What it does |
|---|---|
| `PuffCloud.shader` | Main cloud shader — 3-octave Simplex noise in world space, slot-center boundary falloff, optional density masking (`_DENSITY_ON`), displacement crossfade, pseudo-normal lighting, optional shadow (`_SHADOW_ON`). |
| `PuffCloudDiffusion.shader` | Density field diffusion blit — 3×3 blur + reform toward equilibrium + semi-Lagrangian wind advection + pressure fill + displacement decay. |
| `PuffCloudStamp.shader` | Density field stamp blit — subtractive radial falloff with directional wake for disturbance. |

