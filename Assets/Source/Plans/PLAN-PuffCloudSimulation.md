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

**Noise layer stack (3 octaves):**

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

**Boundary falloff:**
The quad extends beyond the cluster bounds by `_Padding`. Because the grid is hexagonal,
merged clusters are rarely rectangular — a simple AABB falloff would leave hard edges
on staggered-row boundaries. Instead, the shader receives an array of slot center
positions (in UV space) and computes falloff as the minimum distance to the nearest
slot center, scaled by the slot radius:
```hlsl
float minDist = 999.0;
for (int i = 0; i < _SlotCount; i++)
    minDist = min(minDist, length(uv - _SlotCenters[i]));
float borderFade = smoothstep(_SlotRadius + _BorderSoftness, _SlotRadius, minDist);
cloud *= borderFade;
```
This naturally produces organic, hex-shaped cloud boundaries that follow the staggered
grid layout. For large clusters the slot center array is small (max ~10–15 entries) —
well within uniform buffer limits.

**Properties (via MaterialPropertyBlock per cloud instance):**
- `_DensityTex` — the density RenderTexture (ping-pong pair, current read buffer)
- `_TimeOffset` — driven by C# each frame (same pattern as SoapBubbleCluster)
- `_SlotCenters` — `Vector4[]` array of slot center positions in UV space (up to 16)
- `_SlotCount` — number of active slot entries
- `_SlotRadius` — per-slot cloud radius in UV space (derived from slot separation)
- `_CloudColor` — base tint (white with low alpha for translucent cloud)
- `_EdgeLow` / `_EdgeHigh` — noise threshold window
- `_NoiseScale` — global noise frequency multiplier
- `_BorderSoftness` — edge fade distance
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
│   └── SortingLayer: string      = "Grid"
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

- [ ] Write `BalloonParty/Grid/PuffCloud` shader (3-octave Simplex noise, edge
      threshold, border falloff, color tint, `_TimeOffset`)
- [ ] Create `PuffCloudView` MonoBehaviour — drives `_TimeOffset` via MPB, sizes
      quad to single slot dimensions
- [ ] Create `PuffCloudSettings` SO with noise + visual parameters
- [ ] Wire into a test prefab and validate in-editor at game scale
- [ ] Tune noise parameters until the cloud reads as "soft wispy cloud" at ~0.85×0.85
      world-unit slot size

**Exit criteria:** A single procedural cloud that looks good at game scale, runs at
< 0.1ms on target hardware.

---

### Phase P2 — Density Field & Disturbance

**Goal:** Add the density RenderTexture, diffusion pass, and manual disturbance
stamping. Validate that the disturb→reform cycle looks convincing.

- [ ] Implement density RT ping-pong pair on `PuffCloudView`
- [ ] Write diffusion blit shader (blur + reform-toward-1.0)
- [ ] Write stamp blit shader (subtractive radial + directional wake)
- [ ] Add debug input: click on cloud → stamp disturbance at click position
- [ ] Tune `DiffusionRate`, `ReformSpeed`, stamp radius/strength
- [ ] Validate density preservation across frames (no drift toward 0 or NaN)

**Exit criteria:** Clicking on the cloud creates a visible hole that smoothly
reforms over 1–2 seconds. The cloud remains stable over long sessions.

---

### Phase P3 — Adjacency Merging

**Goal:** Adjacent Puff slots merge into a single visual cloud.

- [ ] Implement `PuffClusterRegistry` with hex-adjacency flood-fill
      (uses `SlotGrid.HexNeighborIndices`)
- [ ] Implement `PuffCluster` model (slot list, world bounds)
- [ ] `PuffCloudViewController` spawns/resizes `PuffCloudView` per cluster
- [ ] Density RT resize with UV-offset copy on cluster growth
- [ ] Cluster split on Puff removal → old view returned, new views spawned
- [ ] Add `ClusterId` to `PuffObstacleModel`

**Exit criteria:** Placing two adjacent Puffs produces one continuous cloud. Removing
one splits the visual correctly.

---

### Phase P4 — Projectile Disturbance Integration

**Goal:** The projectile disturbs clouds as it flies through them.

- [ ] `PuffCloudView` polls projectile position each frame
- [ ] Stamps disturbance when projectile overlaps cloud bounds
- [ ] Disturbance radius and strength driven by `PuffCloudSettings`
- [ ] The projectile trail is visible through the cloud gap
- [ ] Cloud reforms after the projectile passes

**Exit criteria:** Shooting through a cloud creates a visible wake that reforms.

---

### Phase P5 — Animation Disturbance Integration

**Goal:** Balloon spawn and balance animations disturb clouds they pass through.

- [ ] Hook into spawn animation path — stamp disturbance at balloon position each
      frame while the balloon crosses a cloud's bounds
- [ ] Hook into balance animation path — same approach
- [ ] Balloon pop near a cloud → burst stamp (large radius, high strength)
- [ ] Disturbance intensity scales with balloon speed (fast spawn animation = bigger
      disturbance than slow balance drift)

**Exit criteria:** Spawning a balloon through a Puff cloud visibly parts the cloud
along the spawn path. A pop adjacent to a cloud creates a visible shockwave.

---

### Phase P6 — Polish & Performance

**Goal:** Visual polish, edge cases, and performance validation.

- [ ] Shadow pass (optional `_SHADOW_ON` toggle)
- [ ] Color tinting from `GamePalette` (clouds could subtly tint to match the level's
      palette mood)
- [ ] Density RT resolution scaling based on device tier (`QualitySettings`)
- [ ] Diffusion tick rate optimization (skip frames on low-end)
- [ ] Profile on target devices; ensure < 0.5ms total for all active clouds
- [ ] Edge case: cloud at grid boundary — clamp quad and density UVs
- [ ] Edge case: all Puffs in a cluster removed in one frame (balance pass removes
      support) — graceful cleanup

---

## Open Questions

1. **~~Diagonal adjacency~~** — Resolved. The grid is hexagonal; adjacency uses
   `SlotGrid.HexNeighborIndices` (6 neighbors per slot with row-parity stagger).
   No 4-connected vs 8-connected decision needed.

2. **Density RT format** — `RenderTextureFormat.R8` (8-bit single channel) is
   sufficient and minimal. But if we later want directional density (velocity field
   for more advanced fluid effects), `RG16` gives two channels. Start with R8;
   upgrade if needed.

3. **Disturbance source priority** — If a projectile and a spawn animation disturb
   the same cloud in the same frame, should stamps accumulate or should only the
   strongest apply? Accumulation is simpler and probably looks fine.

4. **Cloud sorting order** — Should clouds render behind or in front of balloons
   that occupy adjacent (non-Puff) slots? Behind is more natural (clouds are
   background atmosphere). But in front could create a cool fog-of-war effect where
   balloons are partially obscured. Resolve during P1 visual investigation.

5. **Maximum cluster size** — Should there be a cap on how many Puff slots can merge?
   Very large clusters (6+ slots) may need density RT resolution scaling. The
   `texelsPerSlot` approach handles this naturally, but the RT could exceed reasonable
   sizes if an entire row is Puffs. Consider a max-RT-size clamp (e.g. 256×256).

6. **`IOnPassThrough` timing** — The interface in `PLAN-FutureIdeas §5.5` is
   described as future work. Should Puff cloud disturbance be the forcing function
   that implements it, or should disturbance remain view-side polling? Polling is
   simpler for Phase P4–P5; `IOnPassThrough` is cleaner long-term. Could implement
   polling first, migrate to `IOnPassThrough` later.

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










