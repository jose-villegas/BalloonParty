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
leaves are `DrawMeshInstanced` quads with GPU-driven wind and rattle animation.
Zero GameObjects per leaf, zero CPU animation cost. Every slot's leaves are merged
into one instanced draw per depth tier (inner/outer), so a whole bush submits two
leaf draws plus one branch draw per slot, with materials shared (two leaf
materials; one branch material per variant).

### View Layer

| File | What it does |
|---|---|
| `BushView` | `ClusterView` subclass — the renderer. On configure, wires its collaborators and asks the builder for render data; each frame submits the merged leaf batches (`DrawMeshInstanced`) and per-slot branch quads (`Graphics.DrawMesh`), falling back to per-leaf `DrawMesh` when instancing is unavailable. Disables the base `SpriteRenderer`. Owns the editor gizmos. |
| `BushRenderDataBuilder` | Assembles `BushRenderData` from variant data + slot centers: per-slot branch material/matrix and leaf tiers, then merges every slot's leaves into ≤1023-instance batches. Each slot picks a `BushVariantData` by cycling `i % variants.Length`; leaf sprites cycle via `slotIndex % sprites.Length`. Leaf matrices are **static** (translation-only) — all animation runs on the GPU vertex shader. |
| `BushMaterialSet` | Owns the bush's materials and their lifetime — two shared leaf materials (differ only by render queue) and one branch material per variant branch map — plus the baked branch gradient texture. Releases them on rebuild and runtime destroy. |
| `BushRustleController` | Spawns rustle VFX when the projectile passes near a slot or a reported impact lands within a slot's radius. Driven each frame by `BushView`; injected with `ProjectilePositionProvider`, `ImpactEventBus`, and `PoolManager`. |
| `BushRenderData` | Render-data value types — `LeafTier`, `LeafBatch`, `SlotRenderData`, and the `BushRenderData` container the builder produces and the view submits. |
| `BushShaderProperties` | Cached `Shader.PropertyToID` ids and the `_RATTLE_ON` keyword, shared by the material set, builder, and view. |
| `BushViewController` | `ClusterViewController` subclass. Adds gap-fill circles at midpoints between adjacent bush slots. Wires `IBushSettings` into the view via `SetSettings()`. |
| `BushClusterRegistry` | `SlotClusterRegistry<BushObstacleModel>`. Subscribes to grid changes (no `setupOnly`) because spawner places actors async after `Start()`. |

### GPU Animation (BushLeaf.shader)

All leaf animation runs in the **vertex shader** — no CPU-side `ITickable` or
per-frame matrix updates:

- **Wind idle** — per-instance sine oscillation + dual-sine organic noise,
  modulated by leaf depth. Scale pulse at 2× frequency for flutter.
- **Rattle** — samples the global `_DisturbanceTex` (from `DisturbanceFieldService`)
  at the leaf's world position. Displacement vector is crossed with the leaf
  direction for signed rotation, modulated by configurable damping power curve.
  The disturbance field's own diffusion/reform handles the settle decay.
- **Per-instance data** — `_LeafWind` float4 (phase, depth, baseAngle, scale)
  packed into `MaterialPropertyBlock` at setup, never updated.

### Disturbance Field Interaction

Bush leaves react to the same disturbance field as Puff clouds. Any event that
stamps the field (projectile wake, balloon pop, bomb detonation, paint splash)
causes nearby leaves to rattle. The interaction is automatic — the shader reads
global `_DisturbanceTex` without any C# wiring. Toggle via `_RATTLE_ON` keyword.

### Configuration

`BushSettings` (ScriptableObject in `Configuration/`) — `IBushSettings`. Holds:
- **Prefab** — `BushView` prefab reference
- **Branch Map** — `BushVariantData[]` variants, branch shader, leaf material, world size
- **Leaf Atlas** — sprite array for atlas sub-rects
- **Leaf Shadow** — shadow colour, offset, softness, sprite scale
- **Wind** — amplitude, period, noise amplitude, scale pulse, pivot offset
- **Rattle** — enabled toggle, amplitude, frequency, damping

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

