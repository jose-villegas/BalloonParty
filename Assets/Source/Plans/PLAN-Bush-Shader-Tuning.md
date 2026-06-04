@page plan_bush_shader_tuning Bush Shader Visual Tuning

# Bush Shader — Visual Tuning Plan

> Focused plan for the procedural bush canopy shader. Extracted from
> `PLAN-Bush-Implementation.md` Phase 3.5 for full focus during iteration.

---

## Current state (2026-06-03)

### Leaf placement — Douady-Couder phyllotaxis

Each real slot generates **16 leaf clumps** in a golden-angle spiral (≈ 137.5°).
Distance from centre follows `√n` (Douady-Couder model) for even packing.
Per-slot hash rotates the whole spiral — no two slots look alike.

Gap-fill positions (`.w < 1`, injected by `BushViewController`) use a single
round leaf at the midpoint between adjacent slots.

### Leaf shape — elongated ellipse

Each leaf is a **circle-cut bilateral lens** — two equal circles offset
perpendicular to the leaf axis, intersected via `max(d1, d2)`. This is a
simplified Gielis superformula realised as CSG circle cuts. It produces
genuinely pointy tips with bilateral symmetry at minimal cost (2× `length` +
1× `max` — no `atan2`, no `pow`). `_LeafPointiness` controls the offset
between cutting circles: 0 = circle, 0.7 = natural leaf, 2.0 = needle.
Gap fills stay circular (pointiness 0).

### Rendering — painter's algorithm (back to front)

The fragment shader iterates depth layers 0 → LEAF_COUNT-1 across all slots.
Depth 0 = outermost spiral position (painted first, underneath).
Depth 15 = centre position (painted last, on top).

When a leaf covers the pixel, its colour **overwrites** the previous — no
coverage accumulation. Upper leaves fully occlude lower ones.

### Shading pipeline (per covered leaf, in order)

1. **Depth-based base colour** — `lerp(_BaseColor, _TopColor, depthT)`.
   Outer leaves are dark, centre leaves are bright.

2. **Radial dome gradient** — each leaf is brighter at centre, darker at edge
   (`_EdgeShade`). Sells each clump as a 3D dome.

3. **Highlight spot** — `_HighlightColor` blended as a fake specular.
   For real leaves, computed in the leaf's local coordinate frame — elliptical
   to match the lens shape, and shifted along the leaf axis by
   `_HighlightOffset` to simulate light direction. Gap fills keep a circular
   highlight. `_HighlightSize` controls radius.

4. **Lateral veins (secondary venation)** — pinnate fishbone pattern branching
   from the midrib toward the leaf edge, curving toward the tip. Uses a
   diagonal stripe field `stemAxisT × _LateralVeinCount − |perpDist| ×
   _LateralVeinAngle` with `frac` to create repeating vein lines. Each vein
   tapers thinner toward the leaf edge and fades near the base, tip, and
   midrib. Darkened with `_VeinDarken` (shared with midrib). Skipped for gap
   fills. Zero extra SDF cost.

5. **Central vein (midrib)** — dark line along each leaf's radial axis
   (`_VeinWidth`, `_VeinDarken`). Computed from the tangent perpendicular to
   `leafDir`. Masked to fade near centre and edge. Skipped for gap fills.
   Zero extra SDF cost.

6. **Self-shadow** — for each covered leaf at depth D, checks all higher depth
   layers (D+1 … LEAF_COUNT−1) within the same slot. If an upper leaf's SDF
   (evaluated at a shadow-offset position) is near zero, darkens the current
   leaf (`_LeafShadowStrength`, `_LeafShadowSoftness`, offset XY). Average
   cost ~8 extra `PhyllotaxisLeaf` + `LeafSDF` per covered leaf.

7. **Inner shadow crease** — when an upper leaf overlaps an already-covered
   pixel, a dark band near the upper leaf's edge is applied
   (`_CreaseWidth`, `_CreaseDarken`). Sells depth between layers.

### Branches

Capsule SDFs from slot centre toward the outermost spiral positions. Endpoints
are **prebaked on the CPU** by `BushView.ComputeBranchSegments` and pushed via
`_BranchSegments` / `_BranchCount` MPB arrays. The fragment shader loops only
`CapsuleSDF` — no per-pixel `PhyllotaxisLeaf` calls. Rendered in `_BranchColor`
(brown). Only visible where no leaf covers the pixel.

### Shadow

`CanopySDF` traces the actual leaf shapes (all depths × all slots) at a shadow
offset position. Matches the canopy silhouette exactly — each leaf's circle-cut
lens shape is visible in the shadow. Uses `_ShadowSoftness` for soft edges.

### Wind & disturbance

Wind: 1 simplex noise sample per fragment displaces the evaluation position.
Disturbance: `_DISTURBANCE_ON` feature adds world-space displacement from the
global `_DisturbanceTex`.

---

## Shader file

`Assets/Shaders/BalloonParty/Grid/Bush.shader`

### Compile-time constants

| Define | Value | Purpose |
|---|---|---|
| `MAX_SLOTS` | 16 | Maximum slot positions in the uniform array |
| `LEAF_COUNT` | 16 | Leaves per real slot (phyllotaxis spiral) |
| `BRANCH_COUNT` | 5 | Branch capsules per real slot |
| `GOLDEN_ANGLE` | 2.39996323 | 137.508° in radians |
| `MAX_BRANCHES` | MAX_SLOTS × BRANCH_COUNT | Prebaked branch segment capacity (80) |

### Shader features

| Keyword | Purpose |
|---|---|
| `_SHADOW_ON` | Ground shadow pass |
| `_DISTURBANCE_ON` | Disturbance field displacement |

### All properties

| Property | Type | Default | Purpose |
|---|---|---|---|
| **Shape** ||||
| `_SlotRadius` | Float | 0.40 | Base radius per slot |
| `_RadiusJitter` | Range(0, 0.15) | 0.06 | Per-slot radius variation |
| `_AAWidth` | Range(0.001, 0.03) | 0.008 | Anti-aliasing edge width |
| **Canopy** ||||
| `_BranchSpread` | Range(0.1, 0.8) | 0.55 | How far the spiral extends from centre |
| `_SubCircleSize` | Range(0.15, 0.7) | 0.30 | Individual leaf size factor |
| `_SubCircleSizeVar` | Range(0, 0.5) | 0.30 | Hash-based size randomness per leaf |
| `_LeafPointiness` | Range(0.0, 2.0) | 0.7 | Circle-cut offset (0 = circle, 2 = needle) |
| **Surface** ||||
| `_BaseColor` | Color | (0.14, 0.40, 0.10) | Outer / deepest leaf colour |
| `_TopColor` | Color | (0.35, 0.65, 0.20) | Centre / topmost leaf colour |
| `_CreaseWidth` | Range(0.01, 0.12) | 0.07 | Inner shadow band width at overlaps |
| `_CreaseDarken` | Range(0.3, 1.0) | 0.50 | How dark the crease gets |
| **Dome Shading** ||||
| `_HighlightColor` | Color | (0.55, 0.80, 0.35, 0.45) | Bright spot colour + intensity (alpha) |
| `_HighlightSize` | Range(0.1, 0.7) | 0.30 | How large the bright specular spot is |
| `_HighlightOffset` | Range(-0.5, 0.5) | 0.15 | Shift specular along leaf axis (+ = toward tip) |
| `_EdgeShade` | Range(0.5, 1.0) | 0.68 | Edge-to-centre darkness (lower = darker edges) |
| **Leaf Vein** ||||
| `_VeinWidth` | Range(0.01, 0.15) | 0.06 | Midrib line thickness (normalized to leaf radius) |
| `_VeinDarken` | Range(0.5, 1.0) | 0.72 | How dark the vein lines are (midrib + lateral) |
| `_LateralVeinCount` | Range(3, 12) | 6 | Number of lateral vein pairs along the midrib |
| `_LateralVeinAngle` | Range(0.3, 3.0) | 1.2 | Slope of lateral veins (higher = more angled toward tip) |
| **Branches** ||||
| `_BranchThickness` | Range(0.005, 0.05) | 0.014 | Branch capsule half-width |
| `_BranchColor` | Color | (0.35, 0.22, 0.10) | Branch colour |
| **Self Shadow** ||||
| `_LeafShadowStrength` | Range(0, 0.6) | 0.35 | How dark the cast shadow is |
| `_LeafShadowSoftness` | Range(0.01, 0.15) | 0.05 | Shadow edge falloff distance |
| `_LeafShadowOffsetX` | Range(-0.06, 0.06) | 0.02 | Shadow offset X (light direction) |
| `_LeafShadowOffsetY` | Range(-0.06, 0.06) | -0.03 | Shadow offset Y (light direction) |
| **Wind** ||||
| `_WindSpeed` | Range(0, 2) | 0.4 | Wind noise scroll speed |
| `_WindAmount` | Range(0, 0.05) | 0.015 | Wind displacement magnitude |
| **Animation** ||||
| `_TimeOffset` | Float | 0.0 | MPB-driven animation time |
| **Shadow** ||||
| `_ShadowColor` | Color | (0.04, 0.04, 0.08, 0.45) | Ground shadow colour |
| `_ShadowOffsetX` | Range | 0.03 | Shadow X offset |
| `_ShadowOffsetY` | Range | -0.04 | Shadow Y offset |
| `_ShadowSoftness` | Range | 0.04 | Shadow edge softness |
| **Disturbance** ||||
| `_DisplaceWorldScale` | Range(0, 2) | 0.3 | Disturbance displacement strength |

---

## C# pipeline

| File | Role |
|---|---|
| `ClusterView.cs` | Base MonoBehaviour. Pushes `_SlotCentersWorld`, `_SlotCount`, `_TimeOffset` via MPB. Buffer size: 16. |
| `ClusterViewController.cs` | Abstract controller. `PopulatePositions` virtual method — default fills from cluster slots with `.w = 1`. Positions buffer: 16. |
| `BushViewController.cs` | Overrides `PopulatePositions` to add gap-fill midpoints between adjacent bush slots (`.w = 0.65`). Uses `HashSet<(Vector2Int, Vector2Int)>` for edge deduplication. |
| `BushClusterRegistry.cs` | `SlotClusterRegistry<BushObstacleModel>`. Subscribes to grid changes (no `setupOnly`) because spawner places actors async after `Start()`. |
| `BushView.cs` | `ClusterView` subclass. Overrides `OnConfigured` to prebake branch capsule endpoints from material properties (`_SlotRadius`, `_RadiusJitter`, `_BranchSpread`) and slot centres. Pushes `_BranchSegments` (Vector4[48]) and `_BranchCount` via MPB. |
| `IBushSettings.cs` | Interface: `IClusterViewSettings` + `BushView BushPrefab`. |
| `BushView.cs` | `ClusterView` subclass. Overrides `OnConfigured` to prebake branch capsule endpoints from material properties (`_SlotRadius`, `_RadiusJitter`, `_BranchSpread`) and slot centres. Pushes `_BranchSegments` (Vector4[80]) and `_BranchCount` via MPB. |

---

## Performance profile (per fragment)

| Pass | Work | Typical count |
|---|---|---|
| Early bounds | `dot` per slot (squared distance, no sqrt) | 16 |
| Wind | 1 simplex noise | 1 |
| Precomputation | hash + radius + reach per slot (once, not per depth) | 8 |
| Per-slot spatial skip | `dot` per (depth × slot) — skips PhyllotaxisLeaf + LeafSDF for far slots | ~50% skipped |
| Leaf SDF | `LeafSDF` per depth × slot (gap fills skip depth > 0) | ~45-50 |
| Dome shading | per covered leaf: radial gradient + highlight | ~3-5 |
| Lateral veins | per covered real leaf: 1 `frac` + 2 `smoothstep` | ~3-5 |
| Vein | per covered real leaf: tangent dot + smoothstep | ~3-5 |
| Self-shadow | per covered real leaf: circle check (not full LeafSDF) × SELF_SHADOW_LAYERS (4) | ~16 |
| Branches | CapsuleSDF — **deferred**, only when no leaf covers pixel | ~0 (leaf-covered) / ~25 (gaps) |
| Shadow | `CanopySDF`: slot-outer loop with per-slot distance skip | ~12-18 |
| **Total SDF evals** | | **~90-110** |

### Optimizations applied (Iteration 8)

| # | Optimization | Savings |
|---|---|---|
| O1 | **Deferred branches** — branch CapsuleSDF loop only runs when `!anyCovered`. ~80% of bush fragments are leaf-covered, skipping ~25 CapsuleSDF calls. | ~20 SDF/frag avg |
| O2 | **Per-slot precomputation** — hash, baseRadius, reach computed once per slot instead of per (depth × slot). Eliminates ~75 redundant `sin()` calls. | ~75 sin() |
| O3 | **Per-slot spatial skip** — squared distance check skips PhyllotaxisLeaf + LeafSDF for far slots. Saves ~40-60% of SDF evaluations depending on cluster layout. | ~40 SDF/frag avg |
| O4 | **Circle self-shadow** — uses `length(p - center) - radius` instead of full `LeafSDF` for self-shadow checks. Sufficient for soft darkening effect. | ~50% cheaper per check |
| O5 | **CanopySDF slot-outer loop** — hash computed once per slot. Per-slot distance skip culls far slots. Fewer iterations than depth-outer ordering. | ~75% fewer hash calls |
| O6 | **Squared distance bounds** — early bounds check uses `dot` instead of `length`, avoiding 16 `sqrt` calls. | 16 sqrt |
| O7 | **Compile-time constant** — `PHYLLO_MAX_R` replaces `sqrt(LEAF_COUNT - 0.5)` computed every `PhyllotaxisLeaf` call. | minor |
| O8 | **Merged conditionals** — highlight, veins, self-shadow under one `if (!isGapFill)` branch instead of three. Less divergence. | minor |

---

## Iteration history

### Iteration 1 — Noise-based SDF (Phase 2 / initial)

Single smooth-min merged SDF per slot with multi-octave simplex noise for edge
bumps and leaf colour modulation. Full lighting system (half-Lambert from noise
gradient), rim highlight, centre shadow.

**Problem:** Blobby, amorphous shape. Noise looked organic but not cartoony.
Visually clashed with the game's flat art style.

### Iteration 2 — Stacked circle layers

Three depth layers (base, mid, top) with per-layer offset/shrink. Smooth-min
merging within each layer. Leaf noise for colour variation.

**Problem:** Still noise-dependent. Layers didn't read as distinct — looked like
tinted blobs, not leaf clumps.

### Iteration 3 — Coverage accumulation

Replaced layers with per-circle coverage counting. More overlapping circles =
brighter colour. Per-circle edge darkening for individual circle visibility.

**Problem:** Venn-diagram blending at circle overlaps (V1). No depth ordering —
intersections looked like transparency. Missing inner detail.

### Iteration 4 — Painter's algorithm + sub-circles

Fixed-angle sub-circles (4 per slot at 90° apart). Painter's algorithm for
back-to-front opaque painting. Inner shadow crease at overlaps. Branch capsules
behind leaves. Scalloped edges via subtracted micro-circles.

**Problem:** Fixed-angle placement looked mechanical/repetitive. Scalloping was
expensive (5 extra SDF checks per leaf). Still missing inner canopy detail.

### Iteration 5 — Phyllotaxis + leaf shapes + dome shading

Douady-Couder golden-angle spiral placement (8 leaves). Elongated ellipse SDF
for leaf shapes. Per-leaf dome shading (radial gradient + highlight spot).
Removed scalloping — leaf shapes provide edge variety.

**Problem:** Inner detail still too flat — each leaf was a smooth gradient blob.
Needed more visual density within each leaf clump.

### Iteration 6 — Rosette + grain inner detail (historical)

Added angular rosette petal pattern (two harmonics of `sin(angle × N)`) for
sub-lobe structure within each leaf. Added world-space hash grain for micro-
texture. Both are pure colour modulation — zero extra SDF evaluations.

**Problem:** The narrow circle-cut leaf shape clips most radial lobes — only
1–2 highlights visible per leaf, reading as random blobs rather than structured
detail. Replaced by lateral veins in Iteration 8.

### Iteration 7 — Dense canopy + branch prebake + vein + self-shadow + circle-cut leaf (historical)

Doubled leaf count (8 → 16) and increased branches (3 → 5) to match a dense
top-down tree canopy reference. Branch capsule endpoints prebaked on CPU
(`BushView.ComputeBranchSegments`) and pushed via `_BranchSegments` /
`_BranchCount` MPB arrays — eliminates all per-pixel `PhyllotaxisLeaf` calls
from the branch pass. Added **central vein (midrib)** — a dark line along each
leaf's radial axis for botanical detail. Added **self-shadow** — each leaf
checks the next 2 depth layers in the same slot and darkens where an upper leaf
would sit on top. Replaced elongated ellipse SDF with **circle-cut bilateral
lens** (`_LeafPointiness` 0.7) — two equal circles intersected via CSG to form
genuinely pointy leaf tips with bilateral symmetry. Inspired by Gielis
superformula research. Tuned defaults: smaller leaves (0.30), wider spread
(0.55), stronger rosettes (0.35, 7 lobes), deeper creases (0.50), thinner
branches (0.014). Shared math constants moved to `MathUtils`.

**Status:** Superseded by Iteration 8.

### Iteration 8 — Lateral veins + optimization pass (current)

Replaced the angular rosette petal pattern with **lateral veins** (pinnate
venation). Removed world-space grain. Made highlight leaf-shape-aware and
positionable via `_HighlightOffset`.

**Optimization pass** — reduced per-fragment cost from ~165 to ~90-110 SDF
evaluations:
- **Deferred branches**: CapsuleSDF loop only runs for uncovered pixels (~20%
  of bush fragments). Saves ~25 SDF/frag on average.
- **Per-slot precomputation**: hash + baseRadius + reach computed once per slot
  (not per depth × slot). Eliminates ~75 redundant `sin()` calls.
- **Per-slot spatial skip**: squared distance culls far slots, skipping ~40-60%
  of PhyllotaxisLeaf + LeafSDF evaluations.
- **Circle self-shadow**: `length(p - center) - radius` replaces full `LeafSDF`
  for self-shadow checks. Sufficient for the soft darkening effect.
- **CanopySDF restructured**: slot-outer loop computes hash once per slot and
  adds per-slot distance culling.
- **Squared distance bounds**: early bounds check uses `dot` instead of
  `length`, avoiding 16 `sqrt` calls.
- **Merged conditionals**: highlight, veins, self-shadow under one
  `if (!isGapFill)` branch instead of three separate checks.

**Status:** Active iteration. Evaluating visual result at game scale.

---

## Branch offloading — options analysis (2026-06-03)

### Problem

The fragment shader recomputes branch capsule endpoints every fragment.
`PhyllotaxisLeaf` (cos, sin, sqrt, hash) is called `BRANCH_COUNT × real_slots`
times per fragment, even though the endpoints are purely deterministic from the
slot centre array. Only the `CapsuleSDF` hit-test actually needs the fragment
position.

At 5 real slots × 3 branches = 15 `PhyllotaxisLeaf` + 15 `CapsuleSDF` calls
per fragment. The `PhyllotaxisLeaf` half (~10 ops each, trig-heavy) is 100%
redundant work — the same result for every pixel.

### Target platform constraints

| Platform | Graphics API | Compute support |
|---|---|---|
| Android | Vulkan (explicit, min SDK 25) | ✅ Yes |
| iOS | Metal (automatic) | ✅ Yes |
| Editor | DX12 / Metal / Vulkan | ✅ Yes |

Both shipping APIs support compute shaders, structured buffers, and
`RenderTexture` with `enableRandomWrite`. No ES 2.0 / ES 3.0-only fallback
is needed.

### Option A — CPU prebake branch endpoints → MPB uniform array

Compute all branch capsule `(start, end)` positions on the CPU inside
`BushViewController.PopulatePositions` (or a new `OnConfigured` override in
`BushView`). Pass as a `_BranchSegments` `Vector4[]` via MPB, where each
`Vector4` is `(startX, startY, endX, endY)`. Pass `_BranchCount` alongside.

The fragment shader replaces the current branch loop with:

```hlsl
for (int bi = 0; bi < _BranchCount; bi++)
{
    float4 seg = _BranchSegments[bi];
    branchDist = min(branchDist, CapsuleSDF(wpEval, seg.xy, seg.zw, _BranchThickness));
}
```

No `PhyllotaxisLeaf`, no hash, no skip-gap-fill branching — just CapsuleSDF.

| | |
|---|---|
| **Saves** | All trig/sqrt in `PhyllotaxisLeaf` per fragment (~150 ops at 5 real slots) |
| **Keeps** | CapsuleSDF loop — still O(branches) per fragment (~8 ops × 15) |
| **Cost** | CPU: one-time per reconfigure (~microseconds). MPB: +15 Vector4s (~240 bytes) |
| **Compatibility** | ✅ 100% — plain uniforms, works on every GPU |
| **Complexity** | Low — port `PhyllotaxisLeaf` + hash to C#, add one MPB array |
| **Risk** | Minimal — same visual output, deterministic port |

### Option B — Branch mask texture via CommandBuffer / Blit

Render branch shapes into a small `RenderTexture` (e.g. 128×128) using a
one-shot `CommandBuffer` with a simple capsule-drawing shader. The main bush
shader samples `_BranchMask` in O(1) per fragment.

Steps:
1. CPU computes branch endpoints (same as Option A).
2. A lightweight shader draws oriented quads for each capsule into the RT.
3. The RT covers the cluster's combined bounds (UV = `(wp - boundsMin) / boundsSize`).
4. Bush fragment shader: `tex2D(_BranchMask, uv).r > 0.5 && !anyCovered`.
5. Wind: apply wind displacement to UV before sampling → branches sway.
6. Re-rendered only when clusters reconfigure (not per frame).

| | |
|---|---|
| **Saves** | Entire branch loop — O(1) texture fetch replaces all CapsuleSDF calls |
| **Cost** | One RT (128×128 R8 ≈ 16 KB), one extra draw call on reconfigure |
| **Compatibility** | ✅ 100% — `CommandBuffer` + `Blit` is universal |
| **Complexity** | Medium — needs capsule-drawing shader, RT lifecycle, UV mapping |
| **Risk** | Resolution-dependent quality at close zoom; RT must match bounds |

### Option C — Compute shader branch mask

Same output as Option B (small RT with branch mask), but generated by a compute
kernel instead of a CommandBuffer draw call. A single dispatch evaluates capsule
SDFs for every texel of the RT.

```hlsl
[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    float2 wp = BoundsMin + (id.xy + 0.5) / TexSize * BoundsSize;
    float d = 999.0;
    for (int i = 0; i < _BranchCount; i++)
    {
        float4 seg = _BranchSegments[i];
        d = min(d, CapsuleSDF(wp, seg.xy, seg.zw, _BranchThickness));
    }
    Result[id.xy] = d < 0 ? 1 : 0;
}
```

| | |
|---|---|
| **Saves** | Same as B — O(1) branch lookup in fragment shader |
| **Cost** | One RT + one compute dispatch on reconfigure |
| **Compatibility** | ✅ Vulkan + Metal — both target APIs support compute |
| **Complexity** | Medium — compute shader file, dispatch management, RT lifecycle |
| **Risk** | Same resolution concern as B. Compute dispatch overhead for a small RT may be higher than a simple Blit on some mobile GPUs |

### Option D — Hybrid: CPU prebake + optional compute mask

Implement Option A as the baseline (always available). Add Option C as an
optional enhancement behind a `SystemInfo.supportsComputeShaders` check.
Fallback to Option A if compute is unavailable (future-proofs against
hypothetical low-end targets).

### Recommendation

**Start with Option A.** It removes ~60% of the per-fragment branch cost with
minimal code change, zero new assets, and universal compatibility. The port is
deterministic — visual output is identical.

**Evaluate Option B or C later** only if profiling on target devices shows the
remaining CapsuleSDF loop (now ~120 ops per fragment) is a bottleneck. Given
that branches are already the cheapest pass (~15 vs ~47 leaf SDFs), the
remaining capsule loop is unlikely to be the bottleneck.

If a branch mask RT is needed later, **Option B (CommandBuffer) is preferred**
over Option C (compute) because:
- The work is tiny (15 capsules into a 128×128 RT) — compute dispatch overhead
  may exceed the kernel itself on mobile.
- CommandBuffer + DrawMesh is simpler to debug and profile.
- Compute adds a `.compute` asset and dispatch management for negligible gain.

---

## Remaining work

```
T1  [x] Painter's algorithm — opaque back-to-front painting
T2  [x] Inner shadow crease
T3  [x] Branch skeleton
T4  [ ] Density variation — per-cluster fluffy ↔ thin spectrum
T5  [x] Leaf shape detail (replaced scalloping with elongated ellipse + rosette)
T6  [x] Property cleanup
T7  [ ] Tuning pass — colours, sizes, all parameters at game scale
T8  [ ] Validation — multi-slot clusters, side-by-side with Puff, perf check
T9  [x] Branch offloading — CPU prebake endpoints (Option A)
T10 [ ] Branch offloading — mask texture (Option B/C, if needed after profiling)
```

### Open items

| # | Item | Notes |
|---|---|---|
| T4 | Density variation | Per-cluster seed → spread/size multiplier. Fluffy bushes hide branches, thin bushes expose them. Not yet implemented. |
| T7 | Tuning pass | All defaults are placeholder. Need final tuning with balloons, Puff clouds, and game background visible. |
| T8 | Validation | Multi-slot cluster merging, gap-fill coverage, performance at target device resolution. |
| T9 | Branch offloading (A) | Port `PhyllotaxisLeaf` + hash to C#. Add `_BranchSegments` Vector4[] and `_BranchCount` to MPB. Simplify shader branch loop. |
| T10 | Branch offloading (B/C) | Only if T8 profiling shows CapsuleSDF loop is a bottleneck. Prefer CommandBuffer (B) over compute (C). |
| — | `LEAF_COUNT` tuning | Currently 8. Could increase for denser canopy or decrease for performance. Rosette + grain provide density without needing more leaves. |
| — | Wind per-leaf variation | Currently global wind offset. Could add per-leaf sway (outer leaves sway more) at negligible cost by scaling wind by depth index. |

---

## Exit criteria

- Bush canopy reads as **layered, opaque leaf clumps** with visible inner detail.
- **Inner shadows** darken at overlap seams, selling depth between layers.
- **Lateral veins** provide small-scale leaf texture within each clump.
- **Branches** peek through on thinner variants; fully hidden on fluffy ones.
- **Leaf shapes** are elongated and varied — not uniform circles.
- **Visual variety** from cluster to cluster (density spectrum).
- Clearly **differentiated from Puff** clouds.
- No performance regression at game scale.

