@page plan_puff_cloud_simulation Puff Cloud Simulation

# Puff Cloud Simulation

> Design plan for turning the Puff grid actor into an interactive cloud with cheap
> GPU-driven gas simulation, adjacent-slot merging, and disturbance/reform behaviour.

---

## Vision

Puff slots currently exist as structural placeholders. The goal is to make them feel
alive — wispy 2D cloud matter that visibly reacts when objects pass through it. When
two or more Puff slots are adjacent, they merge into a single continuous cloud body
that spans those slots, creating organic shapes instead of a grid of identical tiles.

Key behaviours:
1. **Idle** — soft, organic cloud motion driven by layered noise in a shader
2. **Disturbance** — when a projectile, balloon spawn animation, or balance animation
   passes through, the cloud parts around the moving object and reforms behind it
3. **Merging** — hex-adjacent Puff slots (6 neighbors per slot) share a single visual
   entity whose bounds expand to cover all member slots
4. **Reform** — after a disturbance the cloud drifts back to its equilibrium density
   over a configurable duration

---

## Feasibility — Why This Is Cheap

This is NOT a CPU-side Navier-Stokes fluid simulation. The entire effect is driven by
two mechanisms, both GPU-bound:

1. **Procedural noise shader** — layered Simplex/Perlin noise scrolling at different
   speeds gives the organic cloud shape. This is a single fragment shader pass on a
   quad — the same cost class as the existing `SoapBubbleCluster` shader.

2. **Density field with stamp-based disturbance** — a small `RenderTexture`
   (e.g. 32×32 per slot, so a merged 2-slot cloud uses 64×32) stores a density
   scalar per texel. Disturbances are "stamped" as subtractive radial falloff.
   A simple blur/diffusion pass per frame restores density toward equilibrium.
   Two RT ping-pong buffers, one `Blit` per frame — negligible GPU cost.

The cloud shader samples the density RT and multiplies it against the noise output.
Where density is near zero (a fresh disturbance) the cloud is invisible; where density
is 1.0 (equilibrium) the cloud renders at full opacity. The diffusion pass naturally
fills holes back in, producing the "reform" effect for free.

**Performance budget estimate:**
- Noise shader: same cost as SoapBubbleCluster (~0.05ms on mobile GPU per cloud)
- Density RT blit (blur kernel): ~0.02ms per cloud per frame (32×32 texture, 3×3 kernel)
- Total per merged cloud: < 0.1ms — well within budget for 3–5 simultaneous clouds

---

## Architecture Overview

```
PuffObstacleModel (existing)          ← unchanged; IWriteableSlotActor + IPassThrough
    └── gains: PuffClusterId          ← shared ID linking adjacent Puffs

PuffClusterRegistry (new, plain C#)   ← IStartable; subscribes SlotGridChangedEvent
    ├── OnPuffPlaced / OnPuffRemoved  ← hex flood-fill adjacency → assigns cluster IDs
    ├── GetCluster(clusterId)         ← returns PuffCluster (slot list, bounds)
    └── OnClusterChanged              ← observable for view layer

PuffCluster (new, plain C#, Model)
    ├── Slots: List<Vector2Int>       ← member slots
    ├── WorldBounds: Rect             ← union of slot world positions + padding
    └── ClusterId: int

PuffCloudView (new, MonoBehaviour, View)
    ├── Owns the cloud quad + material + density RenderTexture pair
    ├── Subscribes to PuffClusterRegistry.OnClusterChanged → resizes quad + RT
    ├── Receives disturbance stamps via PuffDisturbanceMessage
    ├── Runs the diffusion blit each frame (or on a slower tick)
    └── Drives _TimeOffset for noise animation

PuffCloudShader (new shader)
    ├── Layered Simplex noise for cloud shape
    ├── Samples _DensityTex to mask cloud opacity
    ├── Soft edge falloff at cluster boundaries
    └── Configurable color, opacity, noise scale/speed

PuffDisturbanceMessage (new MessagePipe event)
    ├── WorldPosition: Vector3
    ├── Radius: float                 ← derived from actor size
    ├── Strength: float               ← derived from actor speed
    └── Direction: Vector2            ← travel direction for directional wake

PuffDisturbanceStamper (new, plain C#, Controller)
    ├── Subscribes to projectile movement, spawn animations, balance animations
    ├── Checks if world position overlaps any PuffCluster bounds
    └── Publishes PuffDisturbanceMessage when overlap detected
```

---

## Detailed Design

### 1. Adjacency & Merging — `PuffClusterRegistry`

**When:** After any `SlotGridChangedEvent` where the placed/removed actor is a
`PuffObstacleModel`.

**Algorithm:** Flood-fill / union-find over the grid using the existing hex adjacency
from `SlotGrid.HexNeighborIndices(col, row)`. The grid is hexagonal — each slot has
6 neighbors (left, right, upper-left, upper-right, lower-left, lower-right), with the
diagonal shift depending on row parity (even rows shift left, odd rows shift right).
All hex-connected Puff slots form a single `PuffCluster`.

```
Hex grid state (staggered rows):      Clusters:
Row 0:  . P . . P P                   Cluster A = {(1,0),(1,1)}
Row 1:   . P . . . P                  Cluster B = {(4,0),(5,0),(5,1),(4,2),(5,2)}
Row 2:  . . . . P P
```

Note: `(1,0)` and `(1,1)` are hex-adjacent because row 1 is odd (shifts right), so
`(1,1)`'s neighbors include `(1,0)` via the upper-left diagonal.

Each `PuffCluster` stores:
- `Slots` — sorted list of `Vector2Int` members
- `WorldBounds` — axis-aligned `Rect` computed from the union of slot world positions
  plus half-slot padding on each side
- `ClusterId` — stable int ID (monotonically incrementing; never reused within a session)

When a Puff is added or removed, the registry recomputes only the affected cluster(s)
and publishes `PuffClusterChangedEvent` (new cluster, removed cluster, or resized).

**Model impact:** `PuffObstacleModel` gains a `ClusterId` property (set by the registry,
read by the view layer). No other model changes.

**Edge case — single Puff:** A lone Puff with no adjacent Puffs is its own single-slot
cluster. It still gets the cloud shader and density RT, just at minimum size.

---

### 2. Cloud Shader — `BalloonParty/Grid/PuffCloud`

A fragment shader on a dynamically-sized quad. Renders in the same sorting layer as
other grid actors.

**Noise layer stack (3 octaves, world-space sampling):**

Noise is sampled in **world space**, not UV space. The vertex shader passes the
fragment's world position; the noise functions use `worldPos * noiseScale`. This
ensures the cloud pattern is spatially stable — scaling the quad for merged clusters
(P3) or repositioning it for cloud drift does not distort or swim the noise.

| Octave | Scale | Scroll speed | Weight | Purpose |
|--------|-------|-------------|--------|---------|
| Base   | 2.0   | (0.03, 0.02) | 0.50 | Large billowy shapes |
| Detail | 5.0   | (0.06, -0.04) | 0.30 | Medium turbulence |
| Fine   | 10.0  | (-0.04, 0.08) | 0.20 | Wispy edges |

All three use the same Simplex noise function with different UV scaling and scroll
direction. The weighted sum produces a scalar `noiseValue` in [0, 1].

**Density masking:**
```hlsl
float density = tex2D(_DensityTex, densityUV).r;
float cloud = smoothstep(_EdgeLow, _EdgeHigh, noiseValue) * density;
```

Where `_EdgeLow` / `_EdgeHigh` control how much noise is visible (acts as a density
threshold — lower values = puffier cloud, higher = wispier).

**Boundary falloff & occupancy mask:**
The quad is scaled via `transform.localScale` to cover the cluster's axis-aligned
bounding box (all member slot positions + padding). Because the bounding box may
contain empty slots (e.g. an L-shaped cluster), the shader uses the slot center
array as both a boundary falloff AND an occupancy mask — pixels far from any
occupied slot center get zero cloud coverage, regardless of their position within
the quad.

Because the grid is hexagonal, merged clusters are rarely rectangular — the
slot-center distance approach naturally produces organic, hex-shaped cloud
boundaries that follow the staggered grid layout. For large clusters the slot
center array is small (max ~10–15 entries) — well within uniform buffer limits.
```hlsl
float minDist = 999.0;
for (int i = 0; i < _SlotCount; i++)
    minDist = min(minDist, length(worldPos.xy - _SlotCentersWorld[i].xy));
float borderFade = smoothstep(_SlotRadius + _BorderSoftness, _SlotRadius, minDist);
cloud *= borderFade;
```
Note: slot centers are passed in **world space** (not UV space) so the falloff
calculation is independent of quad scale. `_SlotRadius` is half the slot separation
(~0.5 world units) plus visual padding.

**Properties (via MaterialPropertyBlock per cloud instance):**
- `_DensityTex` — the density RenderTexture (ping-pong pair, current read buffer)
- `_TimeOffset` — driven by C# each frame (same pattern as SoapBubbleCluster)
- `_SlotCentersWorld` — `Vector4[]` array of occupied slot center positions in world
  space (up to 16); doubles as occupancy mask — only areas near these centers render
- `_SlotCount` — number of active slot entries
- `_SlotRadius` — per-slot cloud radius in world units (half slot separation + padding)
- `_CloudColor` — base tint (white with low alpha for translucent cloud)
- `_EdgeLow` / `_EdgeHigh` — noise threshold window
- `_NoiseScale` — global noise frequency multiplier
- `_BorderSoftness` — edge fade distance in world units
- `_Padding` — extra quad extent beyond cluster bounds

**Shadow pass:** Optional, same pattern as SoapBubbleCluster `_SHADOW_ON` toggle.
Projects cloud silhouette offset in a fixed direction. Deferred — add only if visually
needed after the base cloud looks right.

---

### 3. Density Field — RenderTexture Ping-Pong

Each `PuffCloudView` owns two `RenderTexture` instances (`_densityA`, `_densityB`)
at a resolution proportional to the cluster slot count:

```
Resolution = (slotsWide * texelsPerSlot, slotsTall * texelsPerSlot)
texelsPerSlot = 32  (configurable; 16 for low-end, 64 for high-quality)
```

A single-slot cloud: 32×32. A 3×2 merged cloud: 96×64. These are tiny — even 10
simultaneous clouds total < 0.1 MB of VRAM.

**Initialization:** All texels = 1.0 (full density = undisturbed).

**Per-frame diffusion pass:**
A one-pass compute-free blit with a blur material:
```
Graphics.Blit(_densityRead, _densityWrite, _diffusionMaterial);
swap(_densityRead, _densityWrite);
```

The diffusion material runs a 3×3 Gaussian blur weighted toward 1.0 (equilibrium):
```hlsl
float current = tex2D(_MainTex, uv).r;
float blurred = /* 3x3 weighted average of neighbors */;
float result = lerp(current, blurred, _DiffusionRate);
result = lerp(result, 1.0, _ReformSpeed * _DeltaTime);
```

- `_DiffusionRate` — how quickly the blur spreads (spatial smoothing)
- `_ReformSpeed` — how quickly density trends back toward 1.0 (temporal recovery)

Both are configurable per `PuffCloudSettings` ScriptableObject.

**Optimization — tick rate:**
The diffusion blit does not need to run every frame. Running at 15–20 Hz (every 3–4
frames) is visually indistinguishable for a soft cloud effect and halves the GPU cost.
`PuffCloudView` tracks elapsed time and only blits when the interval elapses.

---

### 4. Disturbance System

#### 4.1 Disturbance Message

```csharp
internal readonly struct PuffDisturbanceMessage
{
    public readonly Vector3 WorldPosition;
    public readonly float Radius;
    public readonly float Strength;
    public readonly Vector2 Direction;
}
```

#### 4.2 Disturbance Sources

| Source | When | Radius | Strength | Direction |
|--------|------|--------|----------|-----------|
| Projectile | Each `FixedUpdate` while flying through a cloud region | Small (≈ 0.3 world units) | High (projectile is fast) | `model.Direction` |
| Balloon spawn animation | Each frame of DOPath while path crosses a Puff slot | Medium (≈ 0.5) | Medium | Path tangent |
| Balance animation | Each frame of DOPath while crossing a Puff slot | Medium (≈ 0.5) | Low (slow drift) | Path tangent |
| Balloon pop | On pop if adjacent to a Puff slot | Large burst (≈ 0.8) | High | Radial outward |

**`PuffDisturbanceStamper`** (plain C# `ITickable` controller):
- Holds a reference to `PuffClusterRegistry` for fast bounds checking
- Each tick, checks the projectile's current world position against cluster bounds
- For spawn/balance animations: hooks into the existing DOTween `OnUpdate` callback
  (or a per-frame position poll — TBD based on DOTween integration complexity)
- Publishes `PuffDisturbanceMessage` only when an overlap is detected

Alternatively, if per-frame polling of every animation is too invasive, the disturbance
can be driven purely from the **view side**: `PuffCloudView` checks a list of known
"disturbance sources" (projectile transform, animating balloon transforms) each frame
and stamps directly. This avoids MessagePipe overhead for high-frequency events.

**Decision point:** MessagePipe vs direct polling. MessagePipe is cleaner architecturally
but may be too noisy for per-frame projectile updates. Likely best approach: **direct
polling for projectile** (PuffCloudView reads a shared projectile position), **message
for discrete events** (pop burst, spawn entry).

#### 4.3 Stamping onto the Density RT

When a disturbance arrives, `PuffCloudView` converts the world position to density-RT
UV space and draws a subtractive radial stamp:

```csharp
// Convert world pos to density UV
var localPos = worldPos - cloudWorldOrigin;
var uv = new Vector2(localPos.x / cloudWorldWidth, localPos.y / cloudWorldHeight);

// Set stamp material properties
_stampMaterial.SetVector("_StampCenter", uv);
_stampMaterial.SetFloat("_StampRadius", radius / cloudWorldWidth);
_stampMaterial.SetFloat("_StampStrength", strength);
_stampMaterial.SetVector("_StampDirection", direction.normalized);

// Blit stamp onto density RT
Graphics.Blit(_densityRead, _densityWrite, _stampMaterial);
swap(_densityRead, _densityWrite);
```

The stamp shader subtracts a radial falloff from the existing density:
```hlsl
float dist = length(uv - _StampCenter);
float falloff = smoothstep(_StampRadius, 0.0, dist);

// Directional wake — elongate the stamp in the travel direction
float2 toCenter = uv - _StampCenter;
float along = dot(toCenter, _StampDirection);
float wake = smoothstep(_StampRadius * 1.5, 0.0, abs(along)) * step(0.0, along);
falloff = max(falloff, wake * 0.5);

float current = tex2D(_MainTex, uv).r;
return max(0.0, current - falloff * _StampStrength);
```

This produces a tear-drop shaped hole in the cloud density that trails behind the
moving object — the leading edge parts, the wake lingers.

---

### 5. Merging — View-Side Lifecycle

#### 5.1 One `PuffCloudView` Per Cluster

When `PuffClusterRegistry` reports a new cluster, `PuffCloudViewController` (IStartable)
spawns a `PuffCloudView` from a pool:

1. Sizes the quad to cover the cluster's `WorldBounds`
2. Creates (or resizes) the density RT pair at the appropriate resolution
3. Positions the quad at the cluster's world center

When a cluster grows (new Puff placed adjacent):
1. Registry publishes cluster-changed event with updated bounds
2. The existing `PuffCloudView` resizes its quad and reallocates the density RT
3. Old density data is blit-copied into the new larger RT at the correct UV offset
   so existing disturbance holes are preserved during the resize

When a cluster shrinks or splits (Puff removed):
1. Registry detects split → old cluster removed, 1–2 new clusters created
2. Old `PuffCloudView` returned to pool; new views spawned for each sub-cluster
3. Density data from the old RT can optionally be sampled into the new RTs for
   continuity (nice-to-have; not critical — a brief full-density flash is acceptable)

#### 5.2 Individual Puff Slots — Visual Anchors

Each Puff slot still has a `GridActorView` in the grid (required by `SlotGrid`). These
views become **invisible** — no sprite renderer, no visual. They exist only as grid
occupancy markers. The `PuffCloudView` quad is the sole visual, positioned as a sibling
or child of the grid container.

Alternatively, the `GridActorView` for single-slot Puffs could BE the cloud view
(attach `PuffCloudView` as a component). For merged clusters, a separate pooled
GameObject is spawned. **Decision: separate pooled GO is simpler** — avoids
conditional component logic on `GridActorView`.

---

### 6. Configuration — `PuffCloudSettings`

A ScriptableObject holding all tuning knobs:

```
PuffCloudSettings (ScriptableObject)
├── Noise
│   ├── BaseScale: float          = 2.0
│   ├── DetailScale: float        = 5.0
│   ├── FineScale: float          = 10.0
│   ├── ScrollSpeed: Vector2      = (0.03, 0.02)
│   ├── EdgeLow: float            = 0.35
│   └── EdgeHigh: float           = 0.55
├── Density
│   ├── TexelsPerSlot: int        = 32
│   ├── DiffusionRate: float      = 0.15
│   ├── ReformSpeed: float        = 0.4
│   └── DiffusionTickRate: float  = 0.05   (seconds between blit passes)
├── Disturbance
│   ├── ProjectileRadius: float   = 0.3
│   ├── ProjectileStrength: float = 0.8
│   ├── BalloonRadius: float      = 0.5
│   ├── BalloonStrength: float    = 0.4
│   ├── PopBurstRadius: float     = 0.8
│   └── PopBurstStrength: float   = 1.0
├── Visual
│   ├── CloudColor: Color         = (1, 1, 1, 0.6)
│   ├── BorderSoftness: float     = 0.15
│   ├── Padding: float            = 0.3
│   └── SortingLayer: int         (layer ID, Inspector dropdown)
└── Merging
    └── PreserveDensityOnResize: bool = true
```

Injected into `PuffCloudViewController` and `PuffCloudView` via VContainer.

---

### 7. Integration Points

#### 7.1 `IPassThrough` — No Changes

Puff remains `IPassThrough`. The disturbance system is purely visual — it does not
affect traversability or game logic. A Puff slot with zero cloud density is still
structurally occupied and traversable.

#### 7.2 `IOnPassThrough` — Future Hook

The `IOnPassThrough` interface from `PLAN-FutureIdeas §5.5` is a natural fit for
triggering disturbances from spawn/balance animations:

```csharp
public interface IOnPassThrough
{
    void OnActorPassedThrough(ISlotActor passing, Vector3 worldPosition, Vector2 velocity);
}
```

`PuffObstacleModel` would implement `IOnPassThrough` and forward the call to the
`PuffClusterRegistry`, which routes it to the appropriate `PuffCloudView`.

**This is additive** — the disturbance system works without `IOnPassThrough` by using
direct position polling. The interface can be added later when other actors also need
pass-through triggers.

#### 7.3 Projectile Disturbance — Direct Polling

`PuffCloudView` holds an `[Inject]` reference to a `ProjectileTracker` (or reads the
active projectile's transform directly). Each frame, if the projectile is within the
cloud's world bounds, stamp a disturbance at its position.

This is simpler and cheaper than per-frame MessagePipe publishing.

#### 7.4 `SlotGridChangedEvent` — Cluster Recomputation

`PuffClusterRegistry` subscribes to `SlotGrid.OnChanged`. On each event, it checks
whether the affected slot is a Puff and recomputes adjacency if so. This is the only
integration with the existing grid system — no changes to `SlotGrid` are needed.

---

## Phases

### Phase P1 — Cloud Shader Prototype

**Goal:** A single Puff slot renders as an animated procedural cloud on a quad.
No density field, no disturbance, no merging. Pure visual investigation.

- [x] Write `BalloonParty/Grid/PuffCloud` shader (3-octave Simplex noise, edge
      threshold, border falloff, color tint, `_TimeOffset`)
- [x] Create `PuffCloudView` MonoBehaviour — drives `_TimeOffset` via MPB, sizes
      quad to single slot dimensions
- [x] Create `PuffCloudSettings` SO with noise + visual parameters
      → P1 uses `[SerializeField]` on `PuffCloudView` per G10; SO deferred to P3
- [ ] Wire into a test prefab and validate in-editor at game scale
- [ ] Tune noise parameters until the cloud reads as "soft wispy cloud" at ~0.85×0.85
      world-unit slot size

**Exit criteria:** A single procedural cloud that looks good at game scale, runs at
< 0.1ms on target hardware.

#### P1 — Implementation Gaps & Decisions

The following gaps were identified by walking through the implementation path against
the existing codebase. Each must be resolved before or during P1 work.

**G1 — Renderer type: SpriteRenderer with scale**

All existing procedural shaders (`SoapBubbleCluster`, `ToughBalloon`, `UnbreakableBalloon`)
use `SpriteRenderer` on a quad with no assigned sprite. The PuffCloud shader follows
the same pattern.

For merged clusters (P3), the `SpriteRenderer`'s `transform.localScale` is set to
cover the cluster's axis-aligned bounding box (union of all member slot positions +
padding). The shader receives the occupied slot centers as a uniform array and uses
them for two purposes:

1. **Boundary falloff** — cloud opacity fades based on distance to the nearest
   occupied slot center (already in the plan's §2 design).
2. **Occupancy mask** — pixels far from any slot center are discarded, so empty slots
   within the bounding box show no cloud. This handles irregular cluster shapes
   naturally (e.g. an L-shaped cluster spanning 3 columns and 2 rows only renders
   cloud around the occupied slots, not the empty ones).

The noise must sample in **world space** (see G3) so that scaling the quad does not
distort or stretch the cloud pattern. The vertex shader passes world position to the
fragment; noise functions use `worldPos * scale`, not UV. UV space is only used for
density RT sampling (P2+) and boundary falloff.

This means no MeshRenderer migration is needed — `SpriteRenderer` + scale works for
both single-slot (P1) and merged clusters (P3). The slot-center array is the sole
mechanism that controls cloud shape within the scaled quad.

**P1 simplification:** With a single slot, `localScale = Vector3.one` (default quad
covers one slot). The slot-center array has one entry at `(0.5, 0.5)` in UV space.
No scaling logic needed in P1.

**G2 — Simplex noise: no existing implementation in the shader codebase**

The project has no Simplex/Perlin noise function in any shader or `.cginc` include.
`ToughBalloon.shader` uses a simple value-noise hash (`GrainHash` / `GrainNoise`) but
that produces grid-aligned artifacts at the scales needed for cloud shapes.

A 2D Simplex noise function must be written (or a well-known public-domain
implementation adapted). This is ~40 lines of HLSL. It should live in a shared
`.cginc` include file (`Shaders/BalloonParty/Noise/SimplexNoise2D.cginc`) so future
shaders can reuse it.

**Action:** Write `SimplexNoise2D.cginc` as part of P1. The PuffCloud shader
`#include`s it.

**G3 — World-space vs UV-space noise sampling**

The plan says noise uses "different UV scaling and scroll direction". But UV space is
local to the quad — if the quad moves (or if P3 resizes it), the noise pattern shifts
relative to the world, creating visible swimming.

Cloud noise should sample in **world space**: the vertex shader passes world position
to the fragment shader, and noise functions use `worldPos * noiseScale + scrollOffset`
instead of `uv * noiseScale`. This guarantees:
- The noise pattern is spatially stable (cloud doesn't swim when the quad is
  repositioned)
- Merged clusters (P3) share a continuous noise field regardless of quad bounds
- Moving clouds (future vertical drift) reveal new noise rather than sliding existing
  noise

**Action:** Shader `v2f` struct needs a `float2 worldPos : TEXCOORD1` output. The
fragment shader samples noise using `worldPos`, not `texcoord`. UV-space is still used
for boundary falloff and density RT sampling.

**G4 — `PuffCloudView` ownership: who spawns it?**

In P1 there is no `PuffClusterRegistry` yet (that's P3). Someone needs to create the
`PuffCloudView` when a Puff is spawned. Options:

- **A) `StaticActorSpawner` creates it alongside the `GridActorView`:** After placing
  the model, spawn a `PuffCloudView` from a pool and position it at the slot. This
  couples the spawner to the cloud view.
- **B) `PuffCloudView` is a component on the `StaticActorView`/`GridActorView` prefab
  itself:** The cloud visual lives on the same GameObject as the grid occupancy marker.
  Simplest for P1 — no extra spawning logic. The existing `StaticActorSpawner` already
  positions the view at the slot. The downside: P3 merging requires a separate pooled
  GO, so this component would need to be migrated off the per-slot prefab.
- **C) Standalone controller (`PuffCloudSpawner : IStartable`):** Subscribes to
  `SlotGrid.OnChanged`, spawns a `PuffCloudView` per Puff. This is basically a
  simplified P3 `PuffClusterRegistry` that creates one view per slot (no merging).
  Cleanest architecturally but more code for P1.

**Decision needed.** Option B is recommended for P1 — component on the Puff prefab,
no extra spawning logic. Migrate to standalone pooled GO in P3.

**G5 — Prefab: `Puff.prefab` already exists**

`Assets/Prefabs/Grid/Puff.prefab` is already in the project (`StaticTest.prefab` has
been retired). `StaticActorSpawner` uses `_staticActorPrefab` (serialized on
`GameLifetimeScope` as a `StaticActorView` reference). This reference should already
point to `Puff.prefab`.

For P1, add a `PuffCloudView` component to `Puff.prefab` alongside the existing
`StaticActorView`. The `SpriteRenderer` on the prefab gets the PuffCloud material
(no sprite assigned — procedural quad). No spawner changes needed.

**Note:** The spawner uses `StaticActorView`, not `GridActorView`. Both are nearly
identical (`IPoolable` + `ISlotActorView` + `TweenTracker`). Decide whether to keep
using `StaticActorView` or switch to `GridActorView` — they're functionally
interchangeable.

**G6 — `_TimeOffset` driving pattern: `[ExecuteAlways]` needed?**

`SoapBubbleClusterVariant` uses `[ExecuteAlways]` + manual delta time tracking +
`EditorApplication.timeSinceStartup` so the animation runs in edit mode. `PuffCloudView`
needs the same pattern if we want to see the cloud animate in the Scene view without
entering Play mode.

**Action:** `PuffCloudView` should follow the `SoapBubbleClusterVariant` pattern:
- `[ExecuteAlways]` attribute
- `Update()` computes `_TimeOffset` from either `Time.time` (play) or
  `EditorApplication.timeSinceStartup` (edit)
- Guard editor-only code with `#if UNITY_EDITOR`
- `SceneView.RepaintAll()` in edit mode to keep the animation live

**G7 — GPU instancing must be disabled**

Per the shader README: "Materials using `MaterialPropertyBlock` for per-instance shader
properties must have GPU instancing **disabled**". `PuffCloudView` drives `_TimeOffset`
via MPB, so the cloud material must have instancing off. Document this in the shader
file header.

**G8 — Sorting order integration**

The plan mentions `SortingLayer: string = "Grid"` in `PuffCloudSettings`, but the
existing codebase uses `SortingHelper.SlotBaseSortingOrder` for per-slot ordering and
`_baseSortingLayer` on `BalloonView`. Clouds should render **behind** balloons in the
same slot region.

For P1 (single-slot, component on prefab), the cloud's `SpriteRenderer` can use the
same sorting layer as other grid actors but with a lower `sortingOrder`. The exact
value depends on whether the Puff prefab has one renderer (cloud only — no sprite for
the puff itself since it IS the cloud) or multiple.

**Action:** P1 prefab has a single `SpriteRenderer` for the cloud. Sorting order is
set relative to the slot position via `SortingHelper.SlotBaseSortingOrder` with an
offset that places it behind balloons. This is handled in `StaticActorSpawner` or in
the view's `OnSpawned()`.

**G9 — Slot dimensions at game scale**

From `SlotGridTests`: `SlotSeparation = (1.0, 0.85)`. The cloud quad needs to be
sized to cover one slot with padding. At `_SpriteScale = 0.8` (matching
`SoapBubbleCluster`), the effective cloud radius is ~0.4 world units — which may be
too small for a wispy cloud effect. May need `_SpriteScale = 0.6` or lower (larger
content area) to let cloud edges extend further. Padding parameter needs tuning
against these real dimensions.

**Action:** Document `SlotSeparation` in the shader header so tuning is grounded in
actual measurements. P1 tuning pass must validate cloud size against neighboring
balloons.

**G10 — P1 scope: `[SerializeField]` fields, not SO**

~~Decision needed.~~ Resolved: P1 uses `[SerializeField]` fields directly on
`PuffCloudView` for all noise and visual parameters. This avoids premature config
infrastructure (SO creation, `GameLifetimeScope` registration, VContainer injection)
during pure visual investigation. Extract to `PuffCloudSettings` SO in P3 when
multiple views need shared config.

**G11 — Boundary falloff: same mechanism in P1 and P3**

The slot-center-array falloff works identically for single-slot (P1) and merged
clusters (P3) — the only difference is the array length. In P1, the array has one
entry at the quad center. The shader loop runs once and produces a radial falloff
from that single center. No special-case code path needed.

This means the shader's boundary falloff code is **write-once** — P1 implements the
full slot-center-array loop, P3 just passes more entries and scales the quad.

**G12 — Shader folder location**

Existing shaders live under `Assets/Shaders/BalloonParty/Balloon/`. The plan says
`BalloonParty/Grid/PuffCloud` — this implies a new folder
`Assets/Shaders/BalloonParty/Grid/`. This is consistent with the prefab folder
structure (`Assets/Prefabs/Grid/`).

**Action:** Create `Assets/Shaders/BalloonParty/Grid/` folder. Add
`PuffCloud.shader` and the shared `Noise/SimplexNoise2D.cginc` (or put the noise
include in `Assets/Shaders/BalloonParty/Noise/`).

---

### Phase P2 — Density Field & Disturbance

**Goal:** Add the density RenderTexture, diffusion pass, and manual disturbance
stamping. Validate that the disturb→reform cycle looks convincing.

- [x] Implement density RT ping-pong pair on `PuffCloudView`
- [x] Write diffusion blit shader (blur + reform-toward-1.0)
- [x] Write stamp blit shader (subtractive radial + directional wake)
- [x] Add debug input: click on cloud → stamp disturbance at click position
- [ ] Tune `DiffusionRate`, `ReformSpeed`, stamp radius/strength
- [ ] Validate density preservation across frames (no drift toward 0 or NaN)

**Exit criteria:** Clicking on the cloud creates a visible hole that smoothly
reforms over 1–2 seconds. The cloud remains stable over long sessions.

#### P2 — Implementation Notes (Completed)

The following describes what was actually built, which diverges from the original P2
description in several ways. Reference this for accurate context in future sessions.

**RT format — packed density + displacement (not R8)**

The density RenderTexture uses `ARGBHalf` (not the originally planned `R8`):
- **R** = density (1.0 = full cloud, 0.0 = cleared)
- **G** = displacement X (0.5 = zero, biased ±0.5 range)
- **B** = displacement Y (0.5 = zero, biased ±0.5 range)

Equilibrium clear color: `(1.0, 0.5, 0.5, 1.0)`.

**Displacement field — cloud deformation, not just opacity**

The stamp shader writes displacement vectors alongside density subtraction. The cloud
shader reads displacement and offsets noise sampling coordinates, visibly warping the
cloud shape around disturbances. Direction comes from the drag/projectile velocity.

**Crossfade reformation — no stretching artifacts**

The cloud shader computes noise at BOTH the original and displaced positions, then
crossfades between them based on `disturbance` intensity (displacement magnitude).
As displacement decays, the cloud smoothly reveals undisturbed noise rather than
rubber-banding stretched noise back to rest. The boundary falloff uses the original
(undisplaced) world position so edges stay anchored.

**Diffusion shader — advection + pressure + displacement decay**

Three forces drive the field each tick:
1. **Advection** — semi-Lagrangian wind shifts sample origin (direction from last
   disturbance, opposite to drag, smoothed + decaying)
2. **Pressure** — `max(neighbor density) - current density` pushes high→low, filling
   holes from edges directionally
3. **Displacement decay** — GB channels lerp toward 0.5 at `_DisplaceDecay` rate
   (faster than density reform so shape snaps back before opacity returns)

**Wind direction — dynamic from disturbance**

Wind is not a static ambient value. It is set to `-direction` on each stamp (opposite
to the drag/projectile velocity), smoothed via `_windSmoothing`, and decays toward
zero via `_windDecay`. This makes the reform flow from behind the moving object.

**Pseudo-normal lighting**

The cloud shader derives pseudo-normals from the noise gradient via 4-tap central
differences, then applies half-Lambert directional lighting with configurable light
direction, highlight color, and shadow tint. This gives volume/depth to the flat cloud.

**Debug drag interaction**

`HandleDebugClick` uses `Input.GetMouseButton(0)` (continuous hold, not single click).
Tracks previous mouse world position to compute drag direction each frame. Direction
is passed to `StampDisturbance` which both stamps the density/displacement field AND
sets the wind target for directional reform.

**Files created/modified in P2:**

| File | Role |
|---|---|
| `Shaders/BalloonParty/Grid/DisturbanceDiffusion.shader` | Diffusion + advection + pressure + displacement decay blit |
| `Shaders/BalloonParty/Grid/DisturbanceStamp.shader` | Density subtraction + displacement push + directional wake |
| `Shaders/BalloonParty/Grid/PuffCloud.shader` | Added `_DENSITY_ON`, `_DensityTex`, displacement crossfade, lighting |
| `Source/Slots/Actor/Archetype/PuffCloudView.cs` | Density RT ping-pong, stamp API, diffusion tick, wind state, debug drag |
| `Shaders/BalloonParty/Grid/PuffCloud_Shadertoy.glsl` | Standalone Shadertoy port for external testing |

**Current `PuffCloudView` SerializeField inventory (P2 state):**

```
[Header("Animation")]
_animationSpeed: float = 0.8

[Header("Density Field")]
_texelsPerSlot: int = 32
_diffusionRate: float = 0.3  (was 0.15 on prefab — prefab may be stale)
_reformSpeed: float = 0.05   (was 0.4 on prefab — prefab may be stale)
_diffusionTickInterval: float = 0.05

[Header("Wind")]
_windSpeed: float = 1.0
_windSmoothing: float = 6.0
_windDecay: float = 2.0
_pressureStrength: float = 0.4

[Header("Displacement")]
_displaceAmount: float = 0.3
_displaceDecay: float = 1.5

[Header("Debug")]
_debugClickToStamp: bool
_debugStampRadius: float = 0.05
_debugStampStrength: float = 0.8
```

**Note:** The serialized values on `Puff.prefab` may be stale (from before wind/
displacement fields were added). Re-serialize after confirming tuning in-editor.

---

### Phase P3 — Adjacency Merging ✅

**Goal:** Adjacent Puff slots merge into a single visual cloud.

- [x] Implement `PuffClusterRegistry` with hex-adjacency flood-fill
      (uses `SlotGrid.HexNeighborIndices`)
- [x] Implement `PuffCluster` model (slot list, world bounds)
- [x] `PuffCloudViewController` spawns/resizes `PuffCloudView` per cluster
- [x] Density RT resize with UV-offset copy on cluster growth
- [x] Cluster split on Puff removal → old view returned, new views spawned
- [x] Add `ClusterId` to `PuffObstacleModel`

**Exit criteria:** Placing two adjacent Puffs produces one continuous cloud. Removing
one splits the visual correctly. ✅

#### P3 — Implementation Notes

**What was built:**

| File | Role |
|---|---|
| `Configuration/PuffCloudSettings.cs` | SO with all tuning knobs. `CloudPrefab` field for the pooled cloud view prefab. |
| `Slots/Actor/Archetype/PuffCluster.cs` | Model — slot list, world bounds, cluster ID |
| `Slots/Actor/Archetype/PuffClusterChangedEvent.cs` | Event struct (Created/Resized/Removed) |
| `Slots/Actor/Archetype/PuffClusterRegistry.cs` | `IStartable` + `IDisposable` — subscribes `SlotGrid.OnChanged`, flood-fills hex adjacency, publishes cluster events. Calls `RebuildAll()` on `Start()`. |
| `Slots/Actor/Archetype/PuffCloudViewController.cs` | `IStartable` — manages pooled `PuffCloudView` lifecycle per cluster. Gets cloud prefab from `PuffCloudSettings.CloudPrefab`. |
| `Slots/Actor/Archetype/PuffCloudPoolChannel.cs` | Pool channel for `PuffCloudView` prefabs |
| `PuffCloudView.cs` | Added `IPoolable`, `Configure()` API, density RT resize, `OnSpawned`/`OnDespawned` |
| `PuffObstacleModel.cs` | Added `ClusterId` property |

**Architecture changes made during P3 (diverging from original plan):**

1. **`StaticActorSettings` / `StaticActorView` / `StaticActorPoolChannel` — deleted.**
   The spawner now uses `GridActorView` and `GridActorPoolChannel` directly. The Puff
   slot prefab is found from `GridActorConfiguration.Entries` (the entry with
   `GridActorType.Puff`).

2. **`PuffCloudViewSettings` — deleted.** The cloud prefab lives on `PuffCloudSettings`
   as `CloudPrefab`. No separate settings wrapper needed.

3. **`StaticActorSpawner` — refactored to be generic per actor type.**
   - No longer depends on `IGameConfiguration` for `MinStaticActors`/`MaxStaticActors`.
   - Spawn counts come from per-entry `MinCount`/`MaxCount` on `GridActorPrefabEntry`.
   - Iterates `GridActorConfiguration.Entries`, picks count per entry, uses
     `ISlotSelectionStrategy` per entry's `SlotPlacementMode`.
   - `CreateModel(GridActorType)` switch creates the appropriate model per type.

4. **`ISlotSelectionStrategy` — new interface for slot placement logic.**
   - `RandomSlotSelectionStrategy` — shuffles and picks (default for most actors).
   - `ClusterSlotSelectionStrategy` — greedy hex-neighbor expansion. Each cluster
     is capped at `MaxPerCluster` slots. New seeds are biased toward the opposite
     side of the grid from existing cluster centroids, producing spatially
     distributed, facing clusters.
   - `SlotPlacementMode` enum on `GridActorPrefabEntry` — selectable in Inspector.
   - Strategies are lazily cached in a static dictionary.

5. **`GridActorPrefabEntry` — gains `MinCount`, `MaxPerCluster`, `SlotPlacementMode`,
   implements `IWeightedEntry`.** `MaxPerCluster` caps individual cluster size for
   the `Cluster` placement strategy (default 3).

6. **`IWeightedEntry` + `WeightedPickExtensions.PickRandom<T>()` — new shared
   weighted random selection.** Replaces duplicate logic in `BalloonsConfiguration`,
   `GridActorConfiguration`, and `ItemAssigner`. Both `BalloonPrefabEntry` and
   `GridActorPrefabEntry` implement `IWeightedEntry`. `ItemSettings` uses explicit
   interface implementation mapping `MaximumAllowed` → `MaxCount` and
   `Type.ToString()` → `PoolKey`.

7. **`MinStaticActors` / `MaxStaticActors` removed from `IGameConfiguration` and
   `GameConfiguration`.** Spawn counts are fully per-entry now.

8. **GPU instancing removed from PuffCloud shader.** Per G7, MPB-driven properties
   require instancing disabled.

9. **`Puff.prefab` — Cloud child removed.** The prefab now only has `GridActorView` +
   `TweenTracker` (invisible grid occupancy marker). The cloud visual is entirely
   managed by the pooled `PuffCloud.prefab`.

10. **`PuffCloud.prefab` — new standalone prefab.** `SpriteRenderer` (with sprite +
    PuffCloud material) + `PuffCloudView` component. Referenced by
    `PuffCloudSettings.CloudPrefab`. Pooled by `PuffCloudPoolChannel`.

**Prefab state:**
- `Puff.prefab` — `GridActorView` + `TweenTracker` only (no visuals)
- `PuffCloud.prefab` — `SpriteRenderer` + `PuffCloudView` (with sprite + PuffCloud
  material assigned). Referenced by `PuffCloudSettings.CloudPrefab`.
- `PuffCloudSettings.asset` — has tuned values from P2 prefab. Sorting layer
  configured via `[SortingLayer]` dropdown.
- `GridActorConfiguration.asset` — Puff entry with `MinCount`/`MaxCount`,
  `PlacementMode = Cluster`, `GridActorType = Puff`.

---

### Phase P4 — Shared Disturbance Field & Projectile Integration ✅

**Goal:** Replace per-cluster density RTs with a single screen-space disturbance field
service. Any system (projectile, balloons, items) stamps into the shared field.
The cloud shader samples from it. Other future effects can also consume it.

- [x] Create `DisturbanceFieldService` — owns a camera-sized RT pair, runs diffusion
- [x] Migrate `PuffCloudView` to sample from the shared RT instead of per-cluster RTs
- [x] `ProjectileView` stamps the shared field each `FixedUpdate` while flying
- [x] Cloud reforms after the projectile passes (diffusion handles this)
- [x] Projectile trail visible through the cloud gap

**Exit criteria:** Shooting through a cloud creates a visible wake that reforms.
The disturbance field is a standalone service usable by any future consumer. ✅

#### P4 — Architecture

**Core idea:** One screen-space `RenderTexture` pair (density + displacement) that
maps 1:1 to the camera's orthographic viewport. Any game system stamps into it by
calling `DisturbanceFieldService.Stamp(worldPos, radius, strength, direction)`.
The cloud shader (and any future effect) samples from the RT using a simple
world-to-screen-UV transform.

```
DisturbanceFieldService (new, plain C#, IStartable + ITickable + IDisposable)
    ├── Owns RT pair (_fieldA, _fieldB) — ARGBHalf, camera-sized
    ├── Runs diffusion blit each tick (reform + blur + displacement decay)
    ├── Stamp(worldPos, radius, strength, direction) — public API
    ├── Stamp(worldPos, radius, strength, direction, duration) — unified stamp API
    ├── Exposes FieldTexture (current read RT) for shader sampling
    ├── Exposes WorldToFieldUV(worldPos) for coordinate conversion
    └── Computes RT bounds from GameDisplayConfiguration orthographic size + aspect ratio

PuffCloudView (modified)
    ├── Removes per-cluster density RT pair (_densityA, _densityB)
    ├── Removes stamp material, diffusion material, diffusion tick
    ├── Removes StampDisturbance(), TickDiffusion(), density resize logic
    ├── Samples _DisturbanceTex (global) in the shader via MPB
    └── Pushes field UV bounds so shader can map world→field UV

ProjectileView (modified)
    ├── Injects DisturbanceFieldService + DisturbanceFieldSettings
    ├── Calls Stamp() at end of MoveAndBounce()
    └── No knowledge of clouds — stamps the shared field

BalloonSpawner (modified — P5)
    ├── Injects DisturbanceFieldService
    ├── Calls Stamp() each frame during spawn path animation
    └── Uses StampProfile from DisturbanceFieldSettings (BalloonPath source)

BalloonBalancer (modified — P5)
    ├── Injects DisturbanceFieldService
    ├── Calls Stamp() during balance animation path
    └── Uses StampProfile from DisturbanceFieldSettings (BalloonPath source)

BalloonController (modified — P5)
    ├── Injects DisturbanceFieldService
    ├── Calls Stamp() on balloon pop
    └── Uses StampProfile from DisturbanceFieldSettings (BalloonPop source)

BombItemHandler (modified — P5)
    ├── Calls Stamp() with large radius on detonation
    └── Uses StampProfile from DisturbanceFieldSettings (Bomb source)

LaserItemHandler (modified — P5)
    ├── Calls Stamp() along each beam segment
    └── Uses StampProfile from DisturbanceFieldSettings (Laser source)

PaintItemHandler (modified — P5)
    ├── Calls Stamp() on neighbor hits and splash landing
    └── Uses StampProfile from DisturbanceFieldSettings (Paint source)

PuffCloud.shader (modified)
    ├── Replaces _DensityTex (per-cluster UV) with _DisturbanceTex (screen UV)
    ├── Converts world position to field UV using _FieldBoundsMin/_FieldBoundsSize
    ├── Samples density + displacement from the shared field
    └── Rest of shader unchanged (noise, falloff, lighting)
```

#### P4 — Implementation Gaps & Decisions

**G22 — RT sizing: camera-derived world bounds**

The RT maps to the camera's orthographic viewport. The service reads `Camera.main`
(or injects the camera reference) to compute world-space bounds:

```
halfHeight = camera.orthographicSize
halfWidth  = halfHeight * camera.aspect
fieldBounds = Rect(cam.x - halfWidth, cam.y - halfHeight, halfWidth*2, halfHeight*2)
```

RT resolution: `referenceWorldWidth * texelsPerUnit` × `referenceWorldHeight * texelsPerUnit`.
With `texelsPerUnit = 8`, a 10×16 world → 80×128 RT. This is tiny and covers the
entire play area. A single RT replaces all per-cluster RTs.

The texels-per-unit is configurable on `PuffCloudSettings` (or a new
`DisturbanceFieldSettings` SO if the service becomes broadly used).

**G23 — World-to-field UV mapping**

Any world position converts to field UV:

```csharp
float2 uv = (worldPos.xy - fieldBounds.min) / fieldBounds.size
```

This is a simple linear transform. The service exposes `WorldToFieldUV(Vector3)` for
C# callers (stamp positioning). The shader receives `_FieldBoundsMin` (float2) and
`_FieldBoundsSize` (float2) as global shader properties so every shader can compute
the UV without per-material setup.

Since the camera is static (no panning), the bounds are computed once on `Start()`
and only recomputed if the camera moves or resizes (which it doesn't during gameplay).

**G24 — Stamp API: participants stamp themselves**

The service exposes one method:

```csharp
internal void Stamp(Vector3 worldPosition, float radius, float strength, Vector2 direction)
```

- `worldPosition` — where in the world the disturbance occurs
- `radius` — world-space radius of the disturbance
- `strength` — 0–1 intensity (how much density to subtract)
- `direction` — velocity direction for directional wake shape

The service converts world position/radius to field UV space internally, sets stamp
material properties, and blits. Callers don't need to know about UVs or RTs.

Each caller decides when and how often to stamp:
- **Projectile** — every `FixedUpdate` in `MoveAndBounce()`, after position update
- **Balloon spawn/balance** — every frame during DOTween `OnUpdate`, while path
  crosses the field bounds (future P5)
- **Bomb** — single stamp on detonation (future)
- **Pop** — single stamp at pop position (future P5)

**G25 — Diffusion: service runs it, not the view**

The diffusion pass (blur + reform + wind advection + displacement decay) moves from
`PuffCloudView.TickDiffusion()` to `DisturbanceFieldService.Tick()`. The service
runs one diffusion blit per tick on the single shared RT — replacing N per-cluster
diffusion blits with one.

Wind state (direction, smoothing, decay) also moves to the service. This means all
clouds share wind state — which is correct for a screen-space field. The wind is a
global atmospheric effect, not per-cluster.

The diffusion tick interval is configurable (same `_diffusionTickInterval` as before).

**G26 — PuffCloudView simplification**

`PuffCloudView` sheds significant complexity:

**Removed:**
- `_densityA`, `_densityB` RT pair and ping-pong logic
- `_diffusionMaterial`, `_stampMaterial` and their creation/destruction
- `_diffusionTimer`, `_readFromA`, `_densityInitialized`
- `_windTarget`, `_windCurrent`, `_windSpeed`, `_windSmoothing`, `_windDecay`,
  `_pressureStrength`
- `InitDensityField()`, `ResizeDensityField()`, `ReleaseDensityField()`
- `TickDiffusion()`, `PushDensityTexture()`, `StampDisturbance()`
- `EnsureDiffusionMaterial()`, `EnsureStampMaterial()`
- `WorldToDensityUV()`, `WorldRadiusToDensityUV()`
- `HandleDebugClick()` and debug stamp fields

**Retained:**
- Noise animation (`_TimeOffset` via MPB)
- Slot-center array + falloff (`_SlotCentersWorld`, `_SlotCount`)
- `Configure()` for cluster positioning/scaling
- `SpriteRenderer` + `MaterialPropertyBlock`

**Added:**
- Receives `DisturbanceFieldService.FieldTexture` reference and pushes it as
  `_DisturbanceTex` via MPB each frame
- Pushes `_FieldBoundsMin` and `_FieldBoundsSize` via MPB so the shader can compute
  field UVs from world position

**G27 — Shader changes: screen-space UV sampling**

The `PuffCloud.shader` fragment shader currently samples density like this:
```hlsl
float3 field = tex2D(_DensityTex, IN.texcoord).rgb;  // per-cluster UV
```

This changes to:
```hlsl
float2 fieldUV = (IN.worldPos - _FieldBoundsMin) / _FieldBoundsSize;
float3 field = tex2D(_DisturbanceTex, fieldUV).rgb;   // screen-space UV
```

`IN.worldPos` is already computed in the vertex shader (from P1). The field UV
is a simple linear remap. Everything else (noise, falloff, lighting, shadow)
stays unchanged.

Edge case: fragments outside the field bounds (clouds at screen edge) produce
UVs outside [0,1]. The RT uses `WrapMode.Clamp`, so edge texels are sampled —
which will be equilibrium (1.0, 0.5, 0.5) since no stamps occur off-screen.
This is correct behaviour.

**G28 — Per-cluster density fields: backward compatibility**

The per-cluster density fields served two purposes:
1. **Disturbance stamping** — replaced by the shared field
2. **Density initialization on cluster creation** — the cluster started at full
   density and the diffusion pass maintained it

With the shared field, new clouds instantly sample from whatever the field contains
at their location — which is equilibrium (full density) if nothing has disturbed
that area. This is the same visual result. No special initialization needed.

Cluster resize (adding/removing slots) no longer needs RT reallocation — the field
is always screen-sized. This eliminates `ResizeDensityField()` and the
`PreserveDensityOnResize` setting entirely.

**G29 — Debug stamping: moves to service**

The debug click-to-stamp functionality moves from `PuffCloudView` to the service.
The service can expose a `DebugStampAtScreenPosition()` method, or a simple
MonoBehaviour debug tool can call `Stamp()` directly.

**G30 — Performance comparison**

| Metric | Before (per-cluster) | After (shared field) |
|---|---|---|
| RT count | 2 per cluster (6–10 total) | 2 total |
| Diffusion blits/frame | 1 per cluster (3–5) | 1 total |
| Stamp blits/frame | 1 per overlapping cluster per source | 1 per source |
| Material instances | 2 per view (stamp + diffusion) | 2 total (on service) |
| Memory | 80 KB (10 × 8 KB) | ~160 KB (2 × 80×128 ARGBHalf) |
| Shader complexity | Per-cluster UV mapping | Screen UV mapping (simpler) |
| CPU overhead | Per-cluster bounds checks | None (stamp is global) |

Total GPU cost drops from ~5 diffusion blits + N stamp blits per frame to
1 diffusion blit + N stamp blits per frame. Memory is comparable. The main win
is architectural simplicity — no per-cluster RT lifecycle, no resize logic, no
bounds-check-before-stamp.

**G31 — Future consumers**

The shared field enables effects beyond clouds:
- **Heat haze / refraction** — a post-process shader samples `_DisturbanceTex`
  displacement to distort the screen image near disturbance points
- **Grass/foliage sway** — ground-plane vegetation bends away from disturbances
- **Particle emission** — spawn particles at high-disturbance regions
- **Water ripples** — if water tiles are added, they sample the same field

All these consumers just sample `_DisturbanceTex` with `WorldToFieldUV` — no
additional stamping infrastructure needed.

**G32 — Service location and registration**

`DisturbanceFieldService` is a plain C# class (not MonoBehaviour) registered in
`GameLifetimeScope`:

```
builder.Register<DisturbanceFieldService>(Lifetime.Singleton)
       .AsImplementedInterfaces().AsSelf();
```

It implements `IStartable` (create RTs, compute bounds), `ITickable` (diffusion),
and `IDisposable` (release RTs).

It lives in `Shared/` (not `Slots/`) because it's game-wide infrastructure, not
specific to the Puff cloud feature. Namespace: `BalloonParty.Shared.Disturbance`.

Config fields (`texelsPerUnit`, `diffusionRate`, `reformSpeed`, `diffusionTickInterval`,
`windSpeed`, `windSmoothing`, `windDecay`, `pressureStrength`, `displaceDecay`) can
stay on `PuffCloudSettings` for now (the service injects it), or be extracted to a
dedicated `DisturbanceFieldSettings` SO if/when other consumers need independent
tuning. For P4, reuse `PuffCloudSettings` to avoid premature extraction.

**G33 — ProjectileView changes**

Minimal:
- Inject `DisturbanceFieldService`
- At end of `MoveAndBounce()`, after `transform.position = pos`:
  `_disturbanceField.Stamp(pos, _settings.ProjectileRadius, _settings.ProjectileStrength, _model.Direction)`
- The settings values come from `PuffCloudSettings` (already has `ProjectileRadius`
  and `ProjectileStrength` fields). Inject `PuffCloudSettings` into `ProjectileView`,
  or pass radius/strength via `ProjectileModel` fields set at launch time.

Preferred: inject `PuffCloudSettings` directly — it's a singleton SO, cheap to inject.
`ProjectileView` already injects `IGameConfiguration` and `GamePalette`, so injecting
one more SO is consistent with existing patterns.

**G34 — Stamp and diffusion shaders: reuse or modify?**

The existing `DisturbanceStamp.shader` and `DisturbanceDiffusion.shader` work on UV-space
coordinates. They are agnostic to RT size — the stamp center and radius are passed in
UV space. The service converts world coords to field UVs before setting material
properties, same as `PuffCloudView` did with `WorldToDensityUV()`.

**No shader changes needed for stamp/diffusion.** Only the main `PuffCloud.shader`
changes (G27).

#### P4 — Recommended Implementation Order

1. Create `DisturbanceFieldService` in `Shared/Disturbance/` **(G22, G25, G32)**
   - RT pair creation (camera-derived bounds)
   - `Stamp()` API with world→UV conversion
   - Diffusion tick (move logic from `PuffCloudView.TickDiffusion()`)
   - `FieldTexture` and bounds properties
2. Register in `GameLifetimeScope`
3. Modify `PuffCloud.shader` — replace per-cluster `_DensityTex` sampling with
   screen-space `_DisturbanceTex` + `_FieldBoundsMin/_FieldBoundsSize` **(G27)**
4. Simplify `PuffCloudView` — remove all density RT, stamp, diffusion, wind code;
   push `_DisturbanceTex` + field bounds via MPB each frame **(G26)**
5. Inject `DisturbanceFieldService` into `ProjectileView`, stamp in
   `MoveAndBounce()` **(G33)**
6. Test: fire projectile through a cloud → visible wake
7. Tune `ProjectileRadius` and `ProjectileStrength` on `PuffCloudSettings`
8. Add debug stamp tool (optional) **(G29)**

#### P4 — Quick Context Recovery

> **Read this first when starting a P4 implementation session.**

**What P4 does:** Creates a shared screen-space `DisturbanceFieldService` that owns
one camera-sized RT pair (density + displacement). Any game system stamps into it.
The `PuffCloud.shader` samples from it. Per-cluster density RTs are removed from
`PuffCloudView`.

**Files to read before starting (in order):**

| File | Why |
|---|---|
| `Shared/Disturbance/` | Does not exist yet — create this folder |
| `Slots/Actor/Archetype/PuffCloudView.cs` | Current density RT owner — most code being removed. Read `TickDiffusion()`, `StampDisturbance()`, `InitDensityField()` to understand what moves to the service |
| `Shaders/BalloonParty/Grid/PuffCloud.shader` | Lines 229–254: `_DENSITY_ON` block — replace `tex2D(_DensityTex, IN.texcoord)` with screen-space UV sampling |
| `Shaders/BalloonParty/Grid/DisturbanceDiffusion.shader` | Diffusion blit — reused as-is by the service |
| `Shaders/BalloonParty/Grid/DisturbanceStamp.shader` | Stamp blit — reused as-is by the service |
| `Configuration/PuffCloudSettings.cs` | Has `ProjectileRadius`, `ProjectileStrength`, and diffusion/wind fields the service will read |
| `Projectile/View/ProjectileView.cs` | `MoveAndBounce()` at line 211 — add `Stamp()` call after position update. `DestroyProjectile()` and `OnDespawned()` — no clear needed (shared field persists) |
| `Display/OrthogonalSizeCameraController.cs` | Camera sizing — `GameDisplayConfiguration` has `_referenceWorldWidth=10`, `_referenceWorldHeight=16`. Service derives RT bounds from these. |
| `Game/GameLifetimeScope.cs` | Lines 103–104: register service here, near `PuffClusterRegistry` and `PuffCloudViewController` |

**Key architectural rules:**
- Service is plain C# (`IStartable` + `ITickable` + `IDisposable`), NOT MonoBehaviour
- Service namespace: `BalloonParty.Shared.Disturbance`
- RT format: `ARGBHalf` (R=density, GB=displacement XY), equilibrium=(1, 0.5, 0.5)
- RT resolution: `referenceWorldWidth * texelsPerUnit` × `referenceWorldHeight * texelsPerUnit`
- Stamp/diffusion shaders unchanged — service sets UV-space properties, same as view did
- `PuffCloudView` becomes thinner: only noise animation + slot falloff + MPB pushes
- `ProjectileView` injects `DisturbanceFieldService` + `PuffCloudSettings` directly
- The `_DENSITY_ON` shader keyword stays but its sampling changes from per-cluster UV
  (`IN.texcoord`) to screen-space UV (`(worldPos - _FieldBoundsMin) / _FieldBoundsSize`)

**What NOT to change:**
- `PuffClusterRegistry`, `PuffCluster`, `PuffCloudViewController` — untouched
- `DisturbanceStamp.shader`, `DisturbanceDiffusion.shader` — reused as-is
- Noise, slot falloff, lighting, shadow in `PuffCloud.shader` — untouched
- `GridActorConfiguration`, `StaticActorSpawner`, `ClusterSlotSelectionStrategy` — untouched

#### P4 — Implementation Notes (Completed)

**What was built — divergences from original plan:**

1. **`DisturbanceFieldSettings` SO extracted immediately (not deferred to P6).**
   The service injects `DisturbanceFieldSettings` directly, not `PuffCloudSettings`.
   All diffusion/wind/displacement/performance fields live on the dedicated SO. This
   was the right call — the service has no knowledge of clouds.

2. **Constructor injection, not `[Inject]` field injection.** The service uses
   constructor injection for `DisturbanceFieldSettings` and `GameDisplayConfiguration`
   (not `[Inject]` on fields), consistent with the project style guide preference.

3. **Unified `Stamp()` API with optional `duration` parameter.** A single method
   handles both instant stamps (`duration = 0`, the default) and lerp stamps
   (`duration > 0`, which ramp strength over multiple frames). All consumers pass
   `StampProfile.Duration` as the last argument. When duration is zero the stamp
   is queued for immediate flush; when positive a `LerpStamp` is added that ticks
   each diffusion pass.

4. **Batched stamp shader (`DisturbanceStampBatched.shader`) replaces single-stamp
   shader in the service.** Processes up to 16 stamps per blit pass via uniform arrays
   (`_StampCenters[]`, `_StampRadii[]`, etc.). The original `DisturbanceStamp.shader`
   remains for reference but is unused by the service.

5. **`StampProfile` + `StampSource` system added to `DisturbanceFieldSettings`.**
   Per-source radius/strength/duration tuning via a `[Flags]` enum and serialized
   profile array. Consumers call `_settings.GetProfile(StampSource.X)`. This was
   built alongside P4 rather than deferred to P5, since the settings SO was already
   extracted.

6. **`DisturbanceStampCheat` added to `Cheats/`.** MonoBehaviour cheat for debug
   stamping — toggle on, mouse-drag to stamp. Uses `Stamp()` directly (not
   `Stamp()`).

7. **`StampProfileDrawer` added to `Configuration/Editor/`.** Custom `PropertyDrawer`
   for `StampProfile` — foldout with `StampSource` flags label.

8. **Camera bounds from `GameDisplayConfiguration`, not `Camera.main`.** The service
   injects `GameDisplayConfiguration` and calls `GetOrthogonalSize()` + `Screen.width/
   height` to compute field bounds. No camera reference needed.

**Files created/modified in P4:**

| File | Role |
|---|---|
| `Shared/Disturbance/DisturbanceFieldService.cs` | Shared screen-space disturbance field — RT pair, stamp API, diffusion tick, lerp stamps |
| `Configuration/DisturbanceFieldSettings.cs` | SO with all field tuning + `StampProfile[]` + shader references |
| `Configuration/Editor/StampProfileDrawer.cs` | Custom drawer for `StampProfile` |
| `Cheats/DisturbanceStampCheat.cs` | Debug click-to-stamp cheat |
| `Shaders/BalloonParty/Grid/DisturbanceStampBatched.shader` | Batched stamp blit (up to 16 per pass) |
| `Shaders/BalloonParty/Grid/PuffCloud.shader` | Changed `_DensityTex` → `_DisturbanceTex` + screen UV |
| `Slots/Actor/Archetype/PuffCloudView.cs` | Simplified — removed all density RT/stamp/diffusion code; pushes shared field via MPB |
| `Projectile/View/ProjectileView.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; stamps in `MoveAndBounce()` |
| `Game/GameLifetimeScope.cs` | Registers `DisturbanceFieldService` as singleton |

---

### Phase P5 — Animation & Item Disturbance Integration ✅

**Goal:** Balloon spawn animations, balance animations, pops, and item effects
stamp the shared disturbance field.

- [x] Spawn animation — `BalloonSpawner` injects `DisturbanceFieldService`, calls
  `Stamp()` each frame during DOTween spawn path
- [x] Balance animation — `BalloonBalancer` injects `DisturbanceFieldService`, calls
  `Stamp()` during balance DOPath
- [x] Balloon pop — `BalloonController` calls `Stamp()` at pop position
  with `BalloonPop` stamp profile from `DisturbanceFieldSettings`
- [x] Bomb item — `BombItemHandler` calls `Stamp()` with `Bomb` profile
- [x] Laser item — `LaserItemHandler` calls `Stamp()` along beam segments
  with `Laser` profile
- [x] Paint item — `PaintItemHandler` calls `Stamp()` on neighbor hits and
  splash landing with `Paint` profile
- [x] Disturbance intensity scales via per-source `StampProfile` on `DisturbanceFieldSettings`

**Exit criteria:** Spawning a balloon through a Puff cloud visibly parts the cloud
along the spawn path. A pop adjacent to a cloud creates a visible shockwave. ✅

#### P5 — Implementation Notes

**`StampProfile` system — per-source tuning**

Rather than separate radius/strength fields per source, all disturbance sources are
configured via `StampProfile[]` on `DisturbanceFieldSettings`. Each `StampProfile` has:
- `Sources` — `[Flags] StampSource` enum (Projectile, BalloonPath, BalloonPop, Bomb,
  Laser, Paint)
- `Radius` / `Strength` — per-source tuning
- `Duration` — ramp-up duration for `Stamp()` (0 = instant)

Consumers call `_disturbanceSettings.GetProfile(StampSource.X)` to get their profile
and pass it to `Stamp()`. Multiple sources can share a profile by setting
multiple flags.

**Lerp stamps — gradual ramp-up**

`Stamp()` accepts an optional `duration` parameter. When `duration > 0`, the service
internally tracks a `LerpStamp` struct that ramps from 0 to full strength over that
many seconds. Each tick, the delta strength since the last frame is stamped, spreading
the effect across multiple frames for a smooth shockwave instead of a single-frame pop.

**Files modified in P5:**

| File | Change |
|---|---|
| `Balloon/Spawner/BalloonSpawner.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; calls `Stamp()` in spawn path `OnUpdate` |
| `Balloon/Controller/BalloonBalancer.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; calls `Stamp()` in balance path `OnUpdate` |
| `Balloon/Controller/BalloonController.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; calls `Stamp()` on pop |
| `Item/Bomb/BombItemHandler.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; stamps on detonation |
| `Item/Laser/LaserItemHandler.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; stamps along beam segments |
| `Item/Paint/PaintItemHandler.cs` | Injects `DisturbanceFieldService` + `DisturbanceFieldSettings`; stamps on neighbor hits and splash |

---

### Phase P6 — Polish & Performance

**Goal:** Visual polish, edge cases, and performance validation.

- [ ] Shadow pass (optional `_SHADOW_ON` toggle)
- [ ] Color tinting from `GamePalette` (clouds could subtly tint to match the level's
      palette mood)
- [ ] Disturbance field resolution scaling based on device tier (`QualitySettings`)
- [ ] Diffusion tick rate optimization (skip frames on low-end)
- [ ] Profile on target devices; ensure < 0.5ms total for all active clouds
- [ ] Edge case: cloud at grid boundary — clamp quad
- [ ] Edge case: all Puffs in a cluster removed in one frame (balance pass removes
      support) — graceful cleanup
- [x] Extract `DisturbanceFieldSettings` SO — extracted during P4; consumers inject it
      independently of `PuffCloudSettings`

---

## Open Questions

1. **~~Diagonal adjacency~~** — Resolved. The grid is hexagonal; adjacency uses
   `SlotGrid.HexNeighborIndices` (6 neighbors per slot with row-parity stagger).
   No 4-connected vs 8-connected decision needed.

2. **~~Density RT format~~** — Resolved. Uses `ARGBHalf` — R = density,
   GB = displacement XY. See P2 implementation notes.

3. **~~Disturbance source priority~~** — Resolved. Stamps accumulate naturally —
   the stamp shader is subtractive, so multiple stamps in one frame just deepen
   the hole. No priority logic needed.

4. **~~Cloud sorting order~~** — Resolved. Clouds render on a dedicated sorting layer
   above balloons (configurable via `PuffCloudSettings` `[SortingLayer]` dropdown).

5. **~~Maximum cluster size~~** — Resolved. `MaxPerCluster` on `GridActorPrefabEntry`
   caps individual cluster growth during placement (default 3).

6. **~~`IOnPassThrough` timing~~** — Resolved. P4/P5 use a shared
   `DisturbanceFieldService` — participants (projectile, balloons, items) stamp
   directly via `Stamp()`. No polling or `IOnPassThrough` needed for
   disturbance. Each source uses a `StampProfile` from `DisturbanceFieldSettings`.

7. **Cloud during Puff placement animation** — When a new Puff is spawned, should
   the cloud fade in or appear instantly? A fade-in (density starts at 0, reforms
   to 1.0) would look natural and reuse the existing reform mechanic.

8. **Cloud fade-out on Puff removal** — When a Puff slot is removed (balance pass
   collapses support, or a future mechanic destroys it), should the cloud vanish
   instantly or dissolve? A dissolve (density drains to 0 over ~0.5s, then the view
   is returned to the pool) would feel organic and mirror the fade-in from Q7.
   The diffusion pass could be repurposed: instead of trending toward 1.0, trend
   toward 0.0 for the removed slot region, letting the cloud visually evaporate.

---

## Future — Vertical Cloud Drift

> Not part of the current plan. Captured here for when the feature is prioritised.

As the game progresses (score increases, levels advance), Puff clouds could **drift
vertically** through the grid — slowly migrating upward row by row. This would make
the sky feel alive and give the grid a sense of weather or atmosphere that evolves
over the session.

**Concept:**
- On a configurable interval (e.g. every N turns, or tied to `DifficultyService`
  level transitions), each Puff slot relocates one row upward (or downward)
- The `PuffClusterRegistry` handles the grid-level move: `SlotGrid.Remove` old slot,
  `SlotGrid.Place` at new slot — same pattern as `BalloonBalancer` relocations
- The `PuffCloudView` animates the transition: quad position lerps vertically,
  density RT is preserved across the move so disturbance state carries over
- Puffs that drift off the top of the grid are removed; new Puffs can spawn at the
  bottom to replace them, creating a conveyor-belt effect

**Why it works with the current architecture:**
- `PuffClusterRegistry` already recomputes adjacency on every `SlotGridChangedEvent`,
  so a Puff moving from row 3 to row 2 naturally triggers cluster recomputation —
  it might merge with a new neighbor or split from an old one
- The density RT resize/copy logic (Phase P3) handles cluster shape changes
- The cloud view lifecycle (spawn/return from pool) handles cluster creation/destruction
- `IPassThrough` traversability is per-slot, not per-cloud, so moving a Puff slot
  correctly updates path computation without any extra work

**Gameplay angle:**
- Clouds drifting through the grid change which columns are visually obscured (if
  clouds render in front) or which columns have structural support (Puffs occupy slots)
- Combined with the procedural spawner (Phase 8.3), cloud drift could be a difficulty
  knob: faster drift at higher levels creates a more dynamic, harder-to-read grid
- Pairs well with `IOnPassThrough` triggers — a drifting Puff cloud that tints or
  buffs balloons it passes through would create emergent gameplay patterns







