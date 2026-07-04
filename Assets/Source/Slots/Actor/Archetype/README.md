# Actor Archetype

Concrete grid actor models and the Puff cloud / Bush visual systems. Both visual systems build on the generic cluster infrastructure in `Slots/Actor/Cluster/` (`SlotClusterRegistry<TModel>`, `ClusterView`, `ClusterViewController`).

## Actor Models

| File | What it does |
|---|---|
| `PuffObstacleModel` | Structural obstacle — `IClusterableSlotActor` + `IPassThrough`. Occupies a slot, contributes weight, but allows animation paths to pass through. Gains `ClusterId` linking it to a `SlotCluster`. |
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

## Puff Cloud System

The Puff cloud system renders GPU-driven clouds over groups of adjacent Puff slots. Multiple Puff slots that are hex-adjacent merge into a single continuous cloud body. The system follows MVC via the generic cluster infrastructure in `Slots/Actor/Cluster/`:

- **Model:** `SlotCluster` + `PuffClusterRegistry`
- **View:** `PuffCloudView`
- **Controller:** `PuffCloudViewController`

### Model Layer

| File | What it does |
|---|---|
| `PuffClusterRegistry` | Thin `SlotClusterRegistry<PuffObstacleModel>` subclass. The base registry subscribes to `SlotGrid.OnChanged` (Puff slots can be added/removed at runtime), flood-fills hex adjacency into `SlotCluster`s, handles merging/splitting/resizing, assigns `ClusterId` back to each model, and publishes `SlotClusterChangedEvent` via `OnClusterChanged`. |

### View Layer

| File | What it does |
|---|---|
| `PuffCloudView` | Thin `ClusterView` subclass — a `MonoBehaviour` (`[ExecuteAlways]`) driving the `BalloonParty/Grid/PuffCloud` shader on a `SpriteRenderer` quad. `Configure(positions, count, bounds, settings)` positions/scales the quad over the combined bounds and pushes `_SlotCentersWorld`, `_SlotCount`, and `_AnimationSpeed` once via `MaterialPropertyBlock`. The runtime animation clock is shader-driven (`_Time.y * _AnimationSpeed`) — no per-frame property pushes; `_TimeOffset` is only fed in edit mode, where `_Time` is frozen. Disturbance is not managed here — the shader reads the global `_DisturbanceTex` maintained by `DisturbanceFieldService` (`Shared/Disturbance/`). |

### Controller Layer

| File | What it does |
|---|---|
| `PuffCloudViewController` | Thin `ClusterViewController<PuffObstacleModel, PuffCloudView, IPuffCloudSettings>` subclass. The base controller instantiates **one** view instance and, on any cluster change, collects every slot position across **all** clusters (16-entry buffer cap) and reconfigures it — the single quad spans the bounding box of every cluster on the board. Applies sorting layer and order from `PuffCloudSettings`. |

### Configuration

`PuffCloudSettings` (ScriptableObject in `Configuration/`) — `IPuffCloudSettings`. Holds the `CloudPrefab` reference, animation speed, bounding-box padding, and sorting layer/order. Disturbance-field tuning lives in `DisturbanceFieldSettings`, not here.

### Prefabs

- **`Puff.prefab`** — `GridActorView` + `TweenTracker` only. Invisible grid occupancy marker.
- **`PuffCloud.prefab`** — `SpriteRenderer` (PuffCloud material, no sprite) + `PuffCloudView`. Instantiated once by `PuffCloudViewController`, referenced by `PuffCloudSettings.CloudPrefab`.

### Shaders

All in `Assets/Shaders/BalloonParty/Grid/`:

| Shader | What it does |
|---|---|
| `PuffCloud.shader` | Main cloud shader — three octaves of a tileable baked noise texture (generated via **Tools > BalloonParty > Generate Cloud Noise Texture**), sampled continuously in world space so the field is unbroken across the whole board (no per-cluster seams or seeds); smooth-min union of per-slot falloffs (`_SlotBlend`) for the occupancy mask; early discard before the texture fetches where no slot is near; optional density masking + displacement crossfade (`_DENSITY_ON`, reading the global `_DisturbanceTex`); pseudo-normal lighting from screen-space derivatives of the low-frequency noise; optional shadow (`_SHADOW_ON`) evaluated only where it can be visible. |
| `DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 blur + reform toward equilibrium + semi-Lagrangian wind advection + pressure fill + displacement decay. |
| `DisturbanceStamp.shader` | Density field stamp blit — subtractive radial falloff with directional wake for disturbance (single-stamp version). |
| `DisturbanceStampBatched.shader` | Batched stamp blit — processes up to 16 stamps in a single blit for performance. |

