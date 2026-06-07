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
| **2** | Skeleton generation + `DrawMeshInstanced` rendering | ⬜ Next |
| **3** | Wind animation (idle) | ⬜ Planned |
| **4** | Rattle (disturbed state) | ⬜ Planned |
| **5** | Visual polish (branches, shadows, sorting) | ⬜ Planned |

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
    │  LateUpdate: calls Graphics.DrawMeshInstanced(quadMesh, leafMaterial, matrices)
    │  One draw call renders ALL leaves in the cluster
    │
    ▼
No per-leaf GameObjects. No Transform hierarchy. Pure math → GPU instancing.
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
renders shape, surface shading, a gradient-driven midrib, and diagonal lateral
veins into a RenderTexture that is read back to Texture2D for atlas packing.

### What exists now

| File | Location | Role |
|---|---|---|
| `BushBakeLeaf.shader` | `Shaders/.../Editor/` | Gielis SDF, dome shading, hue jitter, AA alpha, midrib + lateral veins |
| `BushLeafBaker.cs` | `Source/Editor/Bush/` | Offscreen camera bake pipeline, gradient texture baking |
| `BushLeafBakeSettings.cs` | `Source/Editor/Bush/` | All bake parameters (shape, surface, midrib, laterals) |
| `BushBakerWindow.cs` | `Source/Editor/Bush/` | Editor window: properties panel + live preview box, export |
| `BushBakerState.cs` | `Source/Editor/Bush/` | Persisted editor state (foldouts, settings, output path) |
| `LeafAtlasPacker.cs` | `Source/Editor/Bush/` | Packs leaf variants into sprite atlas |
| `GielisSDF.cginc` | `Shaders/.../Editor/` | Gielis superformula, HueRotate, JitterGielisParams |
| `LeafVeins.cginc` | `Shaders/.../Editor/` | Fractal vein system (unused — superseded by shader-inline veins) |

### Implemented leaf features

1. ✅ **Gielis SDF shape** — superformula boundary with per-variant jitter
   on m, n1, n2, n3 via seed-driven hash
2. ✅ **Dome shading** — radial darkening at edges, controllable via Edge Shade
3. ✅ **Hue jitter** — per-variant hue rotation in degrees
4. ✅ **AA alpha** — smoothstep anti-aliased edge with configurable width
5. ✅ **Midrib** — gradient-driven central vein across its width
   - Width parameter controls vein thickness
   - Gradient maps left-to-right across the vein (0% = left edge, 50% = centre,
     100% = right edge); colour defines tint, alpha defines blend strength
   - Gradient baked to a 64×1 texture, sampled in the shader
6. ✅ **Lateral veins** — mirrored pairs branching diagonally from the midrib
   - Count: number of vein pairs (0–8)
   - Angle: diagonal angle from the midrib axis (degrees)
   - Width Ratio: lateral width as fraction of midrib width
   - Start: where along the midrib the first pair originates (-1 to 0.5)
   - Reuses the midrib gradient for cross-section profile
   - Each lateral is a ray from its origin on the midrib; only the forward
     side renders (no backward bleed)
   - Linear fade toward vein tips (70% of radius) for natural tapering

### Leaf feature backlog (add one at a time)

1. **Highlight** — specular-like dome highlight
2. **Edge darkening** — colour variation at leaf boundary

### Bake pipeline

The shader uses an offscreen camera rendering a quad with the Gielis SDF
material into a RenderTexture, then reads back to Texture2D. The leaf is
centred at origin pointing up (+Y). Per-variant Gielis jitter and hue shift
are applied via a hash derived from the seed and variant index.

The midrib gradient is baked from Unity's `Gradient` to a 64×1 RGBA texture
at bake time, passed as `_MidribGradient`. Lateral veins reuse the same
texture. Both are cleaned up after each bake.

### Editor window layout

The Bush Baker window (`Tools > Bush Baker`) has:
- **Properties panel** (left, 280–420px) with foldable sections:
  Gielis Superformula, Surface, Midrib (including Lateral Veins sub-section)
- **Live preview box** (right, fills remaining space) — rect-based layout
  that occupies all horizontal space right of the properties column,
  matching its full height. Texture is centred and aspect-fitted inside
  a HelpBox-style container.
- **Buttons** — "Preview All Variants" and "Export Leaf Atlas"
- **Variant grid** — thumbnails of all baked variants

Live preview auto-updates on any parameter change via a settings hash that
includes all fields plus gradient colour/alpha keys.

### Session context for Phase 1

When continuing leaf baking work, read:
- `Assets/Source/Editor/Bush/BushBakerWindow.cs` — editor window
- `Assets/Source/Editor/Bush/BushLeafBaker.cs` — bake pipeline
- `Assets/Source/Editor/Bush/BushLeafBakeSettings.cs` — settings
- `Assets/Shaders/BalloonParty/Grid/Editor/BushBakeLeaf.shader` — the shader
- `Assets/Shaders/BalloonParty/Grid/Editor/GielisSDF.cginc` — Gielis math
- `.github/copilot-instructions.md` — project coding conventions


---

## Phase 2 — Skeleton + DrawMeshInstanced Rendering

### Branch structure (plain C# data)

```csharp
internal struct BranchNode
{
    internal int ParentIndex;           // -1 for root
    internal Vector2 LocalOffset;       // offset from parent tip
    internal float Length;
    internal float BaseAngle;           // rest angle relative to parent
    internal float Width;               // visual thickness (for branch rendering)
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

### Rendering via DrawMeshInstanced

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

### Matrix computation (per frame)

```
For each branch (depth-first, parent before children):
    branchWorldMatrix = parentWorldMatrix × TRS(offset, currentAngle, 1)

    For each leaf on this branch:
        leafWorldMatrix = branchWorldMatrix × TRS(leafOffset, leafAngle, leafScale)
        matrices[leafIndex] = leafWorldMatrix
```

~12-18 matrix multiplies per bush. Trivially cheap.

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

- **Branch lines** — optional thin quads via `DrawMeshInstanced`
- **Ground shadow** — simple dark ellipse sprite under root
- **Sorting** — leaf matrix order controls painter's algorithm
- **Leaf overlap** — randomise sort within tip clusters

---

## Performance Budget

| Metric | Per bush | Per cluster (4 slots) | 3 clusters |
|---|---|---|---|
| GameObjects | 0 leaves | 0 leaves | 0 leaves |
| Draw calls | 1 | 1 | 3 |
| Matrices | ~15 | ~60 | ~180 |
| Per-frame math | ~15 sin + mul | ~60 sin + mul | ~180 sin + mul |

Comparison with previous approaches:

| | SDF shader | SpriteRenderer/leaf | DrawMeshInstanced |
|---|---|---|---|
| Fragment cost | ~100 SDF evals | 1 tex fetch | 1 tex fetch |
| Draw calls/cluster | 1 (heavy) | ~8-12 | **1** |
| Leaf GameObjects | 0 | ~24-45 | **0** |
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

