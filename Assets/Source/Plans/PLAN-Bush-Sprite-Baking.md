@page plan_bush_sprite_baking Bush — 2D Skeletal Plant System

# Bush — 2D Skeletal Plant System

> Procedural 2D plant skeletons with baked leaf sprites at branch tips.
> Wind drives idle sway; disturbance triggers rattle. Rendered via
> `Graphics.DrawMeshInstanced` for maximum batching — zero GameObjects for
> leaves, one draw call per cluster.

---

## Status & Phase Tracker

| Phase | Description | Status |
|---|---|---|
| **0** | Cluster infrastructure (shared with Puff) | ✅ Done |
| **1** | Leaf baking — Gielis SDF + vein system | 🔨 **Current** |
| **2** | Skeleton generation + branch & leaf `DrawMeshInstanced` rendering | ⬜ Next |
| **3** | Wind animation (idle) | ⬜ Planned |
| **4** | Rattle (disturbed state) | ⬜ Planned |
| **5** | Visual polish (shadows, sorting, bark texture) | ⬜ Planned |

---

## Core Idea

A bush is a **2D branching tree** — not a single texture or SDF. Each branch
is a mathematical node; leaves are instanced quads at tips. The skeleton is
pure math (no GameObjects), rendered via `Graphics.DrawMeshInstanced`.

```
         Root (ground anchor)
           │
       ┌───┼───┐
       │   │   │         ← primary branches (2-4)
      ┌┤  ┌┤  ┌┤
      ││  ││  ││         ← secondary branches (optional)
     🍃🍃 🍃🍃 🍃🍃       ← instanced leaf quads at tips
```

Animation is hierarchical matrix math:
- Rotating a branch rotates all children naturally
- Wind = sine + Perlin rotation per branch per frame
- Rattle = decaying impulse propagated through the tree
- Output = one `Matrix4x4[]` per cluster → one `DrawMeshInstanced` call

---

## Architecture

```
BushSkeletonData (plain C# struct array)
    │  Flat array of BranchNode structs — no GameObjects
    │  Generated once from config + seed per bush slot
    │
    ▼
BushSkeletonAnimator (ITickable)
    │  Each frame: walk the branch array, compute world matrices
    │  Wind: sin(t) + perlin(t) per branch, amplitude × depth
    │  Rattle: decaying angular impulse, blended with wind
    │  Output: Matrix4x4[] for all leaves in the cluster
    │
    ▼
BushView : ClusterView
    │  OnConfigured: generates skeleton data per slot
    │  LateUpdate: calls Graphics.DrawMeshInstanced twice:
    │    1. Branch quads (unit tapered quad × branch matrices)
    │    2. Leaf quads (unit quad × leaf matrices)
    │  Two draw calls render ALL branches + leaves in the cluster
    │
    ▼
No per-leaf or per-branch GameObjects. No Transform hierarchy.
Pure math → GPU instancing.
```

---

## Phase 0 — Cluster Infrastructure ✅ Done

Shared cluster system extracted from Puff. All files exist and work.

| File | Location | Role |
|---|---|---|
| `ClusterView.cs` | `Slots/Actor/Cluster/` | Abstract MonoBehaviour, MPB, slot positions |
| `ClusterViewController.cs` | `Slots/Actor/Cluster/` | Generic controller, subscribes to registry |
| `SlotClusterRegistry.cs` | `Slots/Actor/Cluster/` | Flood-fill clustering |
| `BushView.cs` | `Slots/Actor/Archetype/` | Concrete ClusterView subclass |
| `BushViewController.cs` | `Slots/Actor/Archetype/` | Gap-fill midpoints, wires settings |
| `BushClusterRegistry.cs` | `Slots/Actor/Archetype/` | Bush-specific registry |
| `IBushSettings.cs` | `Configuration/` | Settings interface |

---

## Phase 1 — Leaf Baking 🔨 Current

Procedural Gielis leaf sprites baked offline via an editor window. The shader
renders shape, surface shading, a gradient-driven midrib (adapting to lobe
count), lateral veins, venules, and reticulate fill into a RenderTexture
that is read back to Texture2D for atlas packing.

### What exists now

| File | Location | Role |
|---|---|---|
| `BushBakeLeaf.shader` | `Shaders/.../Editor/` | Gielis SDF, dome shading, hue jitter, AA alpha, palmate midribs, lateral veins, venules, reticulate, petiole |
| `BushLeafBaker.cs` | `Source/Editor/Bush/` | Offscreen camera bake pipeline, gradient texture baking, camera sizing for petiole |
| `BushLeafBakeSettings.cs` | `Source/Editor/Bush/` | All bake parameters (shape, surface, midrib, laterals, venules, reticulate, petiole) |
| `BushBakerWindow.cs` | `Source/Editor/Bush/` | Editor window: properties panel + live preview box, export |
| `BushBakerState.cs` | `Source/Editor/Bush/` | Persisted editor state (foldouts, settings, output path, preview seed) |
| `LeafAtlasPacker.cs` | `Source/Editor/Bush/` | Packs leaf variants into sprite atlas |
| `GielisSDF.cginc` | `Shaders/.../Editor/` | Gielis superformula, GielisRadius, HueRotate, JitterGielisParams |
| `LeafVeins.cginc` | `Shaders/.../Editor/` | Fractal vein system (unused — superseded by shader-inline veins) |

### Implemented leaf features

1. ✅ **Gielis SDF shape** — superformula boundary with per-variant jitter
   on m, n1, n2, n3 via seed-driven hash
2. ✅ **Dome shading** — radial darkening at edges, controllable via Edge Shade;
   reduced along midrib axis so the central vein stays bright at the leaf
   base, creating a natural connection to the petiole
3. ✅ **Hue jitter** — per-variant hue rotation in degrees
4. ✅ **AA alpha** — smoothstep anti-aliased edge with configurable width
5. ✅ **Palmate midrib** — gradient-driven central vein(s) adapting to lobe count
   - m < 2.5: single vertical midrib (pinnate venation)
   - m ≥ 2.5: `floor(m)` midribs radiate from centre (palmate venation)
   - Each midrib direction aligns with its Gielis lobe axis
   - Multi-midrib mode clips each midrib to its forward side
   - Width parameter, gradient cross-section profile
   - Gradient baked to a 64×1 texture, sampled in the shader
6. ✅ **Lateral veins** — mirrored pairs branching from each midrib
   - Count: number of vein pairs per midrib (0–8)
   - Angle: min/max range (degrees), randomised per lateral
   - Width Ratio: lateral width as fraction of midrib width
   - Start: where along the midrib the first pair originates (-1 to 0.5)
   - Length: min/max range, randomised per vein; biased by position —
     veins near the base are longer, near the tip shorter
   - Curvature: shape-adaptive bend follows Gielis boundary contour
     (separate from venule curvature); samples boundary at vein position
   - Lateral directions computed relative to their parent midrib direction
   - Smooth fade-out toward tips + fade near leaf edge via SDF distance
   - Primary laterals emerge from midrib at full strength (no fade-in seam)
7. ✅ **Venules** — recursive fractal branching from lateral veins
   - Per Lateral count (0–4), Survival Chance (0–1) via deterministic hash
   - Length: min/max range, randomised per venule; biased by position
   - Sub-veins branch in both directions (±angle from parent lateral)
   - Half-width of parent, smooth fade-in at origin for seamless blending
   - Origins placed on the **curved** parent lateral (not the straight ray)
   - Curvature: independent from lateral curvature, also shape-adaptive
   - Each venule gets an independent random length via unique hash seeds
8. ✅ **Edge fade** — all veins (laterals + venules) fade out when close to
   the leaf boundary, regardless of their configured length; uses SDF
   distance with `smoothstep(0, -0.04, sdfDist)`
9. ✅ **Vein presence tracking** — `inout float veinPresence` accumulates
   proximity to midrib + all laterals + all venules during rendering;
   each vein contributes a soft halo (3× base width) for reticulate suppression
10. ✅ **Reticulate venation** — fine net-like mesh filling spaces between veins
    - Two sets of parallel lines at ±angle create a diamond mesh
    - Organic distortion via cheap sine noise breaks grid regularity
    - Line width varies along each line for hand-drawn feel
    - Suppressed near all veins via `veinPresence` tracker
    - Fades near leaf edge via SDF distance
    - Controls: Enabled, Density (5–60), Width, Opacity, Angle (°)
11. ✅ **Petiole** — leaf stem extending from the bottommost Gielis boundary
    - Samples `GielisRadius(π)` to find the leaf base point
    - Overlaps slightly into the leaf (2× midrib width) for seamless blending
    - Uses midrib gradient cross-section for colour; darkens toward tip
    - Tapered width: configurable -1 to 1 (flared to pointed)
    - Length, Width, Taper controls in a dedicated Petiole foldout
    - Bake camera auto-expands to fit the petiole when enabled
12. ✅ **Unified vein seed** — `_VeinSeed` uniform derived from the variant
    hash feeds into `VeinHash()`, so randomising the preview seed also
    reshuffles vein angles, lengths, and venule survival patterns
13. ✅ **Preview seed** — 🎲 button in preview box randomises the seed,
    changing shape, hue, and full vein hierarchy per click
14. ✅ **Clickable variant grid** — variant thumbnails in a HelpBox; clicking
    any variant displays it in the live preview box with a `►` selection marker
15. ✅ **Shared MinMaxSlider** — reusable `PropertyDrawerHelper.DrawMinMaxSlider`
    (rect-based) and `DrawMinMaxSliderLayout` (layout-based) in `Source/Editor/`

### Leaf feature backlog (add one at a time)

1. **Highlight** — specular-like dome highlight
2. **Edge darkening** — colour variation at leaf boundary

### Bake pipeline

The shader uses an offscreen camera rendering a quad with the Gielis SDF
material into a RenderTexture, then reads back to Texture2D. The leaf is
centred at origin pointing up (+Y). Per-variant Gielis jitter and hue shift
are applied via a hash derived from the seed and variant index. The same
hash (scaled) is passed as `_VeinSeed` so vein randomisation is coupled
to the variant identity.

The midrib gradient is baked from Unity's `Gradient` to a 64×1 RGBA texture
at bake time, passed as `_MidribGradient`. All vein levels and the petiole
reuse the same texture. Cleaned up after each bake.

When petiole is enabled, the bake camera ortho size and quad scale expand
to fit the stem below the leaf. All loops in the shader use `[loop]`
attributes to prevent Metal from attempting to unroll the triply-nested
vein hierarchy (6 midribs × 8 laterals × 4 venules). Up to 64 variants.

### Editor window layout

The Bush Baker window (`Tools > Bush Baker`) has:
- **Properties panel** (left, 280–420px) with foldable sections:
  Gielis Superformula, Surface, Midrib (including Lateral Veins,
  Venules, and Reticulate sub-sections), Petiole
- **Live preview box** (right, fills remaining space) — rect-based layout
  that occupies all horizontal space right of the properties column,
  matching its full height. Texture is centred and aspect-fitted inside
  a HelpBox-style container. 🎲 button in top-right corner randomises
  the preview seed.
- **Buttons** — "Preview All Variants" and "Export Leaf Atlas"
- **Variant grid** — thumbnails in a HelpBox container; clickable to
  display the selected variant in the live preview. Selection is cleared
  when parameters change and auto-preview re-bakes.

Live preview auto-updates on any parameter change via a settings hash that
includes all fields, gradient colour/alpha keys, and the preview seed.

### Session context for Phase 1

When continuing leaf baking work, read:
- `Assets/Source/Editor/Bush/BushBakerWindow.cs` — editor window
- `Assets/Source/Editor/Bush/BushLeafBaker.cs` — bake pipeline
- `Assets/Source/Editor/Bush/BushLeafBakeSettings.cs` — settings
- `Assets/Source/Editor/Bush/BushBakerState.cs` — persisted editor state
- `Assets/Shaders/BalloonParty/Grid/Editor/BushBakeLeaf.shader` — the shader
- `Assets/Shaders/BalloonParty/Grid/Editor/GielisSDF.cginc` — Gielis math
- `Assets/Source/Editor/PropertyDrawerHelper.cs` — shared MinMaxSlider
- `.github/copilot-instructions.md` — project coding conventions


---

## Phase 2 — Skeleton + Branch & Leaf Rendering

### Branch structure (plain C# data)

```csharp
internal struct BranchNode
{
    internal int ParentIndex;           // -1 for root
    internal Vector2 LocalOffset;       // offset from parent tip
    internal float Length;
    internal float BaseAngle;           // rest angle relative to parent
    internal float BaseWidth;           // width at the branch base
    internal float TipWidth;            // width at the branch tip (taper)
    internal int Depth;                 // 0 = trunk, 1 = primary, 2 = secondary
    internal int FirstLeafIndex;        // index into leaf array (-1 if none)
    internal int LeafCount;
    internal float PhaseOffset;         // unique per branch, for wind desync
}

internal struct LeafInstance
{
    internal int BranchIndex;           // which branch this leaf is attached to
    internal Vector2 LocalOffset;       // offset from branch tip
    internal float BaseAngle;           // rest angle relative to branch
    internal float Scale;
    internal int SpriteVariant;         // index into leaf atlas
    internal Color32 Tint;              // per-leaf hue variation
}
```

### Generation rules

- **Trunk** — short, nearly vertical
- **Primary branches** — 2-4, spread ±30°-60° from trunk
- **Secondary branches** — 0-2 per primary (optional, depth budget)
- **Leaves** — 2-3 per terminal branch, small fan arrangement
- **Randomisation** — seed-driven jitter on angles, lengths, counts

### Branch rendering — procedural mesh per cluster

Each branch segment is a **tapered quad** (trapezoid) connecting the parent
tip to the child tip. The four vertices are placed at perpendicular offsets
from the branch line using `BaseWidth` at the base and `TipWidth` at the tip.
All branch quads for a cluster are combined into a **single procedural mesh**,
rebuilt only on skeleton generation (not per frame).

```
          TipWidth
       ┌────────────┐  ← branch tip (toward leaves)
      ╱              ╲
     ╱                ╲    ← tapered quad (2 triangles)
    ╱                  ╲
   └────────────────────┘  ← branch base (toward parent)
          BaseWidth
```

#### Mesh structure

```csharp
// Per branch segment: 4 vertices, 2 triangles (6 indices)
// Vertices in local branch space, transformed to world during generation:
//   v0 = base - perpendicular × baseWidth/2
//   v1 = base + perpendicular × baseWidth/2
//   v2 = tip  + perpendicular × tipWidth/2
//   v3 = tip  - perpendicular × tipWidth/2
//
// UVs: v along branch length (0=base, 1=tip), u across width (0,1)
// Vertex color: brown tint with slight per-branch variation
```

- **One `Mesh`** per cluster — all branch segments baked in
- **One draw call** via `MeshFilter` + `MeshRenderer` on the `BushView`
- **Rebuilt on generation only** — vertex positions are in world space
  at generation time; animation transforms the mesh via per-vertex
  bone-like indices stored in UV2 (branch index) so the animator can
  rewrite vertex positions each frame

#### Animation-time vertex update

During wind/rattle (Phases 3-4), branch angles change each frame. Two
strategies, pick one during implementation:

1. **Matrix approach** — store branch index in UV2, use
   `Mesh.vertices` rewrite each frame from the animator's computed
   branch world matrices. ~4 vertices × ~8 branches = ~32 vertex
   writes per bush. Cheap enough for mobile.

2. **Transform approach** — render branches via `DrawMeshInstanced`
   with a unit tapered-quad mesh and one `Matrix4x4` per branch
   segment. Avoids vertex rewrite but needs a branch material with
   instancing. Slightly more draw calls if branch and leaf materials
   differ (2 per cluster instead of 1).

Recommendation: **option 2** (`DrawMeshInstanced` with unit branch quad)
keeps the same pattern as leaves, avoids CPU vertex manipulation, and
scales cleanly. The extra draw call (branches + leaves = 2 per cluster)
is negligible.

#### Unit branch quad mesh

A shared unit quad with built-in taper, scaled per branch via its
`Matrix4x4`:

```csharp
// Unit branch quad: height = 1 (along local Y), width tapers from
// BaseWidth at y=0 to TipWidth at y=1. Since width varies per branch,
// encode taper as a uniform or use two draws (base-heavy vs tip-heavy).
//
// Simpler: use a unit rectangle (1×1) and let the matrix handle
// width + length. Taper is applied by scaling x differently at
// base vs tip — requires a 2-bone or shader-driven approach.
```

Practical solution: **generate a shared tapered quad mesh at startup**
with a configurable taper ratio. Since all branches share a similar
taper profile (controlled by depth), 2-3 mesh variants (trunk, primary,
secondary) suffice.

```csharp
// BranchQuadFactory — creates a unit tapered quad mesh
internal static Mesh CreateBranchQuad(float tipWidthRatio)
{
    // tipWidthRatio: 0 = pointed, 1 = rectangle
    // height = 1 along Y, base width = 1, tip width = tipWidthRatio
    // 4 verts, 2 tris
}
```

#### Branch material

- Simple unlit shader: solid colour from vertex color, optional subtle
  bark texture
- GPU instancing **enabled** — no MPB needed, colour comes from
  vertex colour baked into the mesh or instancing buffer
- Same sorting layer as leaves, but lower sorting order so branches
  render behind leaves

#### Branch visual parameters (in `IBushSettings`)

```csharp
float BranchTrunkWidth { get; }         // world-space width of the trunk
float BranchTaperRatio { get; }         // tip/base width ratio (0.3–0.7)
Color BranchColor { get; }              // base bark colour
float BranchColorVariation { get; }     // per-branch hue/value jitter
```

### Leaf rendering via DrawMeshInstanced

```csharp
// In BushView.LateUpdate or a dedicated render callback:
Graphics.DrawMeshInstanced(
    _quadMesh,           // shared unit quad
    submeshIndex: 0,
    _leafMaterial,       // shared leaf atlas material (GPU instancing enabled)
    _leafMatrices,       // Matrix4x4[] — computed per frame by animator
    _leafCount,
    _leafMPB             // per-instance color via instancing buffer
);
```

- **Zero GameObjects** for leaves — just a `Matrix4x4[]` array
- **One draw call** per cluster (all leaves share the same atlas material)
- **Per-instance color** for hue variation via instancing buffer
- Skeleton hierarchy exists only as index relationships in the `BranchNode[]`

### Draw call budget (updated)

| Element | Method | Calls per cluster |
|---|---|---|
| Branches | `DrawMeshInstanced` (unit branch quads) | 1 |
| Leaves | `DrawMeshInstanced` (unit leaf quads) | 1 |
| **Total** | | **2** |

### Matrix computation (per frame)

```
For each branch (depth-first, parent before children):
    branchWorldMatrix = parentWorldMatrix × TRS(offset, currentAngle, 1)
    branchRenderMatrix = branchWorldMatrix × TRS(0, 0, (length, width, 1))
    branchMatrices[branchIndex] = branchRenderMatrix

    For each leaf on this branch:
        leafWorldMatrix = branchWorldMatrix × TRS(leafOffset, leafAngle, leafScale)
        leafMatrices[leafIndex] = leafWorldMatrix
```

~12-18 matrix multiplies per bush. Trivially cheap.

### Render order & sorting

- Branches render **behind** leaves within the same sorting layer
- Branch sorting order = base offset (from cluster slot)
- Leaf sorting order = base offset + depth within the branch
- Both use the sorting layer from `IBushSettings.SortingLayerId`

### Session context for Phase 2

When implementing skeleton + rendering, read:
- `Assets/Source/Slots/Actor/Archetype/BushView.cs` — current view (refactor)
- `Assets/Source/Slots/Actor/Cluster/ClusterView.cs` — base class
- `Assets/Source/Slots/Actor/Archetype/BushViewController.cs` — controller
- `Assets/Source/Configuration/IBushSettings.cs` — settings interface
- `Assets/Shaders/BalloonParty/README.md` — GPU instancing patterns
- `Assets/Materials/README.md` — instancing policy

Key instancing rule: materials using `MaterialPropertyBlock` for per-instance
properties must have GPU instancing **disabled**. For `DrawMeshInstanced`,
use the instancing buffer for per-leaf color, not MPB.

---

## Phase 3 — Wind Animation (Idle)

Procedural math in `BushSkeletonAnimator.Tick()`. No DOTween for wind.

### Per-branch angle each frame

```
windAngle = sin(time × frequency + phaseOffset) × amplitude × depthFactor
          + Mathf.PerlinNoise(time × 0.3f, branchId) × noiseAmplitude
currentAngle = baseAngle + windAngle
```

- **Frequency** — global wind speed
- **Phase offset** — unique per branch (prevents sync)
- **Amplitude** — increases with depth: trunk barely moves, tips sway
- **Perlin** — slow organic drift

### Per-leaf flutter

Each leaf adds on top of inherited branch motion:
- Fast small rotation oscillation
- Subtle scale pulse (0.95–1.05)

---

## Phase 4 — Rattle (Disturbed State)

Triggered by projectile proximity via `DisturbanceFieldService`.

1. Impact detected → find nearest branch
2. Apply angular impulse to that branch
3. Propagate to children with ×0.6 attenuation per depth
4. Each branch has `rattleAngle` + `rattleVelocity` (damped spring)
5. Spring physics per frame: `vel += -stiffness × angle - damping × vel`
6. Blend: `finalAngle = baseAngle + windAngle + rattleAngle`
7. Rattle decays to zero → pure wind resumes

No DOTween needed — simple spring physics in the animator tick.

---

## Phase 5 — Visual Polish

- **Ground shadow** — simple dark ellipse sprite under root
- **Sorting refinement** — leaf matrix order controls painter's algorithm
- **Leaf overlap** — randomise sort within tip clusters
- **Branch texture** — optional bark texture on branch material

---

## Performance Budget

| Metric | Per bush | Per cluster (4 slots) | 3 clusters |
|---|---|---|---|
| GameObjects | 0 leaves, 0 branches | 0 leaves, 0 branches | 0 |
| Draw calls | 2 (branches + leaves) | 2 | 6 |
| Matrices | ~15 | ~60 | ~180 |
| Per-frame math | ~15 sin + mul | ~60 sin + mul | ~180 sin + mul |

Comparison with previous approaches:

| | SDF shader | SpriteRenderer/leaf | DrawMeshInstanced |
|---|---|---|---|
| Fragment cost | ~100 SDF evals | 1 tex fetch | 1 tex fetch |
| Draw calls/cluster | 1 (heavy) | ~8-12 | **2** |
| Leaf GameObjects | 0 | ~24-45 | **0** |
| Branch GameObjects | 0 | N/A | **0** |
| Animation | SDF warp | DOTween tweens | **Pure math** |

---

## What we salvage / discard

### Salvage

| Asset | Reused for |
|---|---|
| Leaf baker (`BushBakeLeaf.shader`, `BushLeafBaker.cs`) | Baked leaf sprites with veins |
| Gielis SDF (`GielisSDF.cginc`) | Leaf shape variety |
| Cluster system (`ClusterView`, `ClusterViewController`) | Slot management |
| `DisturbanceFieldService` | Rattle triggers |
| GPU instancing pattern | Leaf material |

### Discard

| Asset | Reason |
|---|---|
| Canopy baker (`BushBake.shader`, deleted) | Skeleton replaces canopy |
| Phyllotaxis layout | Branching tree replaces it |
| Runions venation (`LeafVenationSimulator.cs`) | Shader-inline veins replace it |
| `LeafVeins.cginc` | Superseded by inline midrib + lateral veins in `BushBakeLeaf.shader` |
| Runtime SDF (`Bush.shader`) | Pivot away from SDF |
| Per-leaf SpriteRenderer + LeafSpriteView | `DrawMeshInstanced` replaces |
| DOTween wind (BushRuffleController) | Spring physics replaces |

