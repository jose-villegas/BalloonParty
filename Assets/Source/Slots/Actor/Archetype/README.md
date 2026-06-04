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

The Bush system renders baked sprite bushes over groups of adjacent Bush slots.
Each slot gets a baked canopy sprite (dense centre) and individual leaf sprites
around the outer ring that animate independently. Follows the same cluster MVC
pattern as the Puff cloud system.

### View Layer

| File | What it does |
|---|---|
| `BushView` | `ClusterView` subclass. Spawns per-slot baked canopy `SpriteRenderer` children and pooled `LeafSpriteView` instances at phyllotaxis positions. Scales are driven by `IBushSettings` (canopy diameter, leaf size, slot radius, branch spread). Canopy scales update in `LateUpdate` for live SO tuning; leaf transforms are set once on configure to avoid fighting DOTween. |
| `LeafSpriteView` | `MonoBehaviour` + `IPoolable`. Holds ruffle state (`PhyllotaxisIndex`, `DepthFactor`, `LeafDirection`, `BaseRotation`). `OnDespawned` kills all tweens (wind + ruffle). |
| `LeafSpritePoolChannel` | Pool channel for `LeafSpriteView` prefabs. |
| `BushViewController` | `ClusterViewController` subclass. Adds gap-fill circles at midpoints between adjacent bush slots. Wires `IBushSettings` and the leaf sprite pool into the view via `OnViewCreated`. |
| `BushRuffleController` | `IStartable` + `ITickable`. Tracks the active projectile via `ProjectileLoadedMessage`. Each tick checks leaf proximity — within `RuffleRadius` triggers DOTween punch sequences (rotation, scale, position) with distance-based stagger. Also starts ambient wind oscillation (infinite yoyo rotation) on each leaf. |
| `BushClusterRegistry` | `SlotClusterRegistry<BushObstacleModel>`. Subscribes to grid changes (no `setupOnly`) because spawner places actors async after `Start()`. |

### Configuration

`BushSettings` (ScriptableObject in `Configuration/`) — `IBushSettings`. Holds:
- **Prefab** — `BushView` prefab reference
- **Shape** — slot radius, branch spread (phyllotaxis spiral tightness)
- **Baked Assets** — canopy variant sprites, leaf atlas sprites, canopy diameter, leaf sprite size
- **Ruffle** — leaf count, radius, rotation/scale/position amplitude, duration, stagger
- **Wind** — amplitude, period

### Baking

Baked assets are generated via **Tools > Bush Baker** (editor window). The bake
shaders and venation simulator live in `Assets/Source/Editor/Bush/` and
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

