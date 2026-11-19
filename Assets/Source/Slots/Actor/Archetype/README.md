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
| `MushroomBumperModel` | A meadow mushroom (`Plans/PLAN-MeadowMushrooms.md`): a permanent bumper. Static, never hit, moved by nothing, and not `IPassThrough`, so it blocks traversal as a bush does. It has no collider and no hit response: the ball bounces off the circle `Slots/Grid/MushroomBoard` builds for it. It is one cap of a wall of caps: `ChainIndex` is its place in the layout the level was dealt, which becomes its bumper's id (the name is from when a chain was planned through single mushrooms), and `Wall` is the wall it stands in, which the chain counts by and the kick never bends toward. See `Slots/README.md`, *Mushroom placement* |
| `GridActorType` | Enum identifying actor types — `Puff`, etc. Assets store the number, so append a value and never reorder. `Mushroom` is the meadow's bumper (`Plans/PLAN-MeadowMushrooms.md`): a meadow level's parameters carry a gate for it, `StaticActorSpawner` places its chain, and `GridActorConfiguration` lists it with the `Mushroom` prefab |

## Grid Actor View & Pool

| File | What it does |
|---|---|
| `GridActorView` | `MonoBehaviour` view for grid actors — `IPoolable` + `ISlotActorView` + `TweenTracker`. Invisible for Puff slots (cloud visual is separate). Its pool hooks are virtual, for a view that has something of its own to reset (`MushroomView`) |
| `GridActorPoolChannel` | Pool channel for `GridActorView` prefabs |
| `MushroomView` | The meadow mushroom's view, a `GridActorView`: the one renderer the `Grid/Mushroom` shader draws the mushroom and its shadow on, and the cap's answer to a hit. On a `MushroomBouncedMessage` for the slot the grid holds it at, it pushes its `MushroomCapSpring` and switches itself on; each frame it hands the shader the lean and the swell through a `MaterialPropertyBlock`; once the spring is still it switches itself off. Back in the pool it is at rest, off and unsubscribed. A lance cuts its cap: on a `MushroomCutMessage` for its slot `Cut(worldPoint, direction)` draws the cap in two pieces that part along the lance's line, fall and fade, and leaves the bare stem (`IsCut` is true and a `Hit` does nothing); on a `MushroomRegrownMessage` `Regrow()` pops the cap back. A blast is not a cut: on a `MushroomCutMessage` whose cause is `Blast`, `BlowOff(away)` throws the cap off whole, away from the blast; and on a `MushroomSwayedMessage` for its slot `Sway(away, strength)` leans a whole cap away from the blast and lets it swing back, a hit with no ball (a bare stem does not sway). `EmitSpores(burst)` lets a burst's specks go from the `Spores` particle system the prefab carries, each with its own way inside the burst's fan, speed, size, life and a colour between the tuned green and blue (rolled from the view's own generator, never the run's); a cap a blast threw trails a few more while it rises. It sleeps on a bare stem as it does on a whole cap, and is whole again from the pool, with no speck in the air. See `Slots/README.md`, *The mushroom on the board* |
| `MushroomCapSpring` | The cap's motion, a plain struct with no engine state: `Hit(incoming, speed)` pushes the lean along the ball's way in, `Sway(away, strength)` pushes it away from a blast by the tuned sway's lean, `Pay()` swells the cap when a bounce pays the lance, `Step(deltaTime)` moves both springs on and answers false once the cap has come to rest (and is then exactly at rest), `Reset()` stops it there. Tuned by `MushroomLeanTuning` (`Slots/Actor/`, authored on `IMushroomSettings.Lean`) |
| `MushroomSporePlanner` / `MushroomSporeBurst` | What a mushroom lets go when something happens to its cap, plain C# told the time: `TryHit` (a cone out of the far rim, the way the ball was going, by its speed; nothing under the least speed or while the mushroom is still quiet from its last puff), `TryCut` (a little more, along the lance's way) and `TryBlast` (a wide burst, held for a delay), each answering a `MushroomSporeBurst`: where it leaves from, which way, the smoke's cone and the count, speed and fan of the specks. It keeps the board's shared budget, which shrinks what is asked when much has just been let go |
| `MushroomSporeRouter` | The spores' glue, an entry point: hears `MushroomBouncedMessage` and `MushroomCutMessage`, asks the planner, stamps the smoke's cone into `SmokeFieldService` as capsules and hands the specks to the `MushroomView` the grid holds at the slot. A blast's burst waits its delay in a fixed list, kept with the view it is for: it is let go only if that view still stands in the slot, and the list is dropped on a run reset (`IRunResettable`) and on a new level |
| `MushroomCutTimeline` | What a cap a lance has cut is doing, a plain struct with no engine state: `Cut()` takes the cap off and starts its two pieces parting, `Regrow()` starts it popping back, `Step(deltaTime, tuning)` moves either on and answers false once nothing moves (the stem bare, or the cap whole), `Reset()` is a whole cap. It holds what the shader draws: `CapGrow`, `PieceFade`, `PieceHeight`, and `Part`, `Carry`, `Turn` and `PieceSize` for a tuning. Every value is worked out from the time since the cut or the regrow began, so it is the same at any frame rate. `Blow()` takes the cap off whole instead: one piece (`IsBlown`), thrown (`Part`), lifted (`Arc`, a larger `PieceSize`, a `ShadowHeight` over 1), turning, then landing and fading, by `MushroomCutTuning.Blown`. Tuned by `MushroomCutTuning` (`Slots/Actor/`, authored on `IMushroomSettings.Cut`) |

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
| `BushRenderDataBuilder` | Assembles `BushRenderData` from variant data + slot centers: per-slot branch material/matrix and leaf tiers, then merges every slot's leaves into \f$\le 1023\f$-instance batches. Each slot picks a `BushVariantData` by cycling `i % variants.Length`; leaf sprites cycle via `slotIndex % sprites.Length`. Leaf matrices are **static** (translation-only) — all animation runs on the GPU vertex shader. |
| `BushMaterialSet` | Owns the bush's materials and their lifetime — two shared leaf materials (differ only by render queue) and one branch material per variant branch map — plus the baked branch gradient texture. Settings→material application is factored into `ApplyLeafProperties`/`ApplyBranchProperties`. Releases them on rebuild and runtime destroy. |
| `BushRustleController` | Spawns rustle VFX when any ball in flight passes near a slot or a reported impact lands within a slot's radius. Driven each frame by `BushView`; injected with `IProjectileRegistry`, `ImpactEventBus`, and `PoolManager`. |
| `BushRenderData` | Render-data value types — `LeafTier`, `LeafBatch`, `SlotRenderData`, and the `BushRenderData` container the builder produces and the view submits. |
| `BushShaderProperties` | Cached `Shader.PropertyToID` ids and the `_RATTLE_ON` keyword, shared by the material set, builder, and view. |
| `BushViewController` | `ClusterViewController` subclass. Adds gap-fill circles at midpoints between adjacent bush slots. Wires `IBushSettings` into the view via `SetSettings()`. |
| `BushClusterRegistry` | `SlotClusterRegistry<BushObstacleModel>`. Subscribes to grid changes (no `setupOnly`) because spawner places actors async after `Start()`. |

### Level-transition Ascent participation

`ClusterViewController` parents its single view under the shared `ScenarioContentRoot` (`Slots/Actor/`)
so the level-transition Ascent (`Game/Level/LevelTransitionController.cs`) can slide the whole
scenario in by moving that one root. For that to move the visuals — which otherwise render in
absolute world space — the renderers follow their transform: `ClusterView` pushes `_SlotCentersLocal`
(centers relative to the quad origin) and `PuffCloud.shader` masks against `worldPos - objectOrigin`,
divided by how much the quad has been scaled since it was configured (`_RestScale`; byte-identical to the
old world math at rest, since origin == bounds center and the scale is unchanged then); `BushView` offsets
its baked world matrices by how far its transform has moved or scaled from where it was configured (its
live matrix times its rest inverse, a no-op at rest). The sky/ground mist reveal scales the root about the
camera (`ScenarioContentRoot.SetDepth`), so both follow it too, and the bushes also fade with the meadow's
hole through it (`GroundSceneryAt` in `Ground.cginc`, read by `BushLeaf` and `BushBranch`): the meadow and its
bushes leave for the sky, or arrive from it, together. Both capture their rest state when they are
configured, which must happen with the root at rest: unmoved and unscaled. See the plan's Phase 3b notes
for the full rationale.

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
- **Branch Map** — `BushVariantData[]` variants, branch shader, gradient, `BranchColor` (tint × gradient), leaf material, world size
- **Branch Shadow / AO** — ground shadow colour/offset/spread/softness; central occlusion blob (`BranchAOColor`, `BranchAORadius`, `BranchAOSoftness`, `BranchAOIntensity`). Both colours are lit by the scene light like the ground they land on, so they darken and tint with the time of day
- **Leaf** — atlas sprites, `LeafTint` (global foliage tint, applied as `_LeafColor` over the per-instance tint), shadow colour/offset/softness (lit by the scene light, like the branch shadow), sprite scale
- **Wind** — amplitude, period, noise amplitude, scale pulse, pivot offset
- **Rattle** — enabled toggle, amplitude, frequency, damping
- **Editor Preview** (`#if UNITY_EDITOR`) — `LiveTuning` toggle + a `Revision` counter bumped by the SO's `OnValidate`. When on, `BushView` polls `Revision` and **rebuilds** the bush (materials + render data) whenever any field changes, so **all** fields preview live — colours, AO, wind, and structural ones (atlas, gradient, world size, depth split). Editor-only (stripped from builds), but works **in Play mode**: bushes are configured only by the DI controller (an entry point), which runs in play mode, so tune with the editor playing.

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
- **`Mushroom.prefab`** (`Prefabs/Grid/Mushroom/`) — `MushroomView` + `TweenTracker` + a child `Spores` (a particle system on the `Effects` layer with the `MushroomSpore` material, drawn over the balloons, that never plays by itself: the view emits into it) + a `MeshRenderer` on `MushroomQuad` (a plain quad beside the prefab, sized for the mushroom's radius, its furthest lean, its shadow and how far a cut cap's pieces fly) with the `Mushroom` material, on the `Grid` layer and at the bottom of the `Balloons` sorting layer (order -10, under the balloons, their shadows, the ball and the bursts). No collider, no sprite, no shadow child: the shader draws the shadow. Built by `Tools > BalloonParty > Meadow > Build Mushroom Prefab`; do not edit it by hand, run the builder again (after changing the mushroom's radius, its furthest lean or how far a cut's pieces part or are carried too, which the quad is sized from).

### Shaders

All in `Assets/Shaders/BalloonParty/Grid/`:

| Shader | What it does |
|---|---|
| `PuffCloud.shader` | Main cloud shader — three octaves of a tileable baked noise texture (generated via **Tools > BalloonParty > Generate Cloud Noise Texture**), sampled continuously in world space so the field is unbroken across the whole board (no per-cluster seams or seeds); smooth-min union of per-slot falloffs (`_SlotBlend`) for the occupancy mask; early discard before the texture fetches where no slot is near; optional density masking + displacement crossfade (`_DENSITY_ON`, reading the global `_DisturbanceTex`); pseudo-normal lighting from screen-space derivatives of the low-frequency noise; optional shadow (`_SHADOW_ON`) evaluated only where it can be visible. |
| `DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 blur + reform toward equilibrium + semi-Lagrangian wind advection + pressure fill + displacement decay. |
| `DisturbanceStamp.shader` | Density field stamp blit — subtractive radial falloff with directional wake for disturbance (single-stamp version). |
| `DisturbanceStampBatched.shader` | Batched stamp blit — processes up to 16 stamps in a single blit for performance. |

