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

## Bush System

The Bush system renders baked 2D skeletal bushes over groups of adjacent Bush
slots. Branch maps and leaf attachment points are baked offline via the Bush
Baker editor window. At runtime, branches are a static textured quad per slot;
leaves are `DrawMeshInstanced` quads that rotate around their attachment
points for wind sway. Zero GameObjects per leaf, two draw calls per slot.

### View Layer

| File | What it does |
|---|---|
| `BushView` | `ClusterView` subclass. Per-slot rendering via `Graphics.DrawMesh` (branch) and `DrawMeshInstanced` (leaves). Each slot picks a `BushVariantData` by cycling `i % variants.Length`. Disables the base `SpriteRenderer`. Leaf matrices are updated externally by `BushAnimator`. |
| `BushViewController` | `ClusterViewController` subclass. Adds gap-fill circles at midpoints between adjacent bush slots. Wires `IBushSettings` into the view via `SetSettings()`. |
| `BushAnimator` | `ITickable`. Drives idle wind animation on all bush leaves. Per-leaf pivot rotation using sine oscillation + Perlin noise, modulated by depth. Optional scale pulse for flutter. Zero allocations per frame. |
| `BushClusterRegistry` | `SlotClusterRegistry<BushObstacleModel>`. Subscribes to grid changes (no `setupOnly`) because spawner places actors async after `Start()`. |

### Configuration

`BushSettings` (ScriptableObject in `Configuration/`) — `IBushSettings`. Holds:
- **Prefab** — `BushView` prefab reference
- **Branch Map** — `BushVariantData[]` variants, branch + leaf shaders, world size
- **Leaf Atlas** — sprite array for atlas sub-rects
- **Leaf Shadow** — shadow colour, offset, softness, sprite scale
- **Wind** — amplitude, period, noise amplitude, scale pulse

### Baking

Baked assets are generated via **Tools > Bush Baker** (editor window). The bake
shaders and pipeline live in `Assets/Source/Editor/Bush/` and
`Assets/Shaders/BalloonParty/Grid/Editor/`. See `Editor/Bush/README.md`.

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
| `DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 blur + reform toward equilibrium + semi-Lagrangian wind advection + pressure fill + displacement decay. |
| `DisturbanceStamp.shader` | Density field stamp blit — subtractive radial falloff with directional wake for disturbance (single-stamp version). |
| `DisturbanceStampBatched.shader` | Batched stamp blit — processes up to 16 stamps in a single blit for performance. |

