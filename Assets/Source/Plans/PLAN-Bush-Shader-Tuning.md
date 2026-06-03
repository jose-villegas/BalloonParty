@page plan_bush_shader_tuning Bush Shader Visual Tuning

# Bush Shader — Visual Tuning Plan

> Focused plan for the procedural bush canopy shader. Extracted from
> `PLAN-Bush-Implementation.md` Phase 3.5 for full focus during iteration.

---

## Current state (2026-06-03)

The sub-circle approach is live. Each slot generates 4 circles (1 centre +
3 branches at ~120°). Gap-fill midpoints between adjacent slots are injected
from `BushViewController`. Coverage accumulation drives colour from dark → bright.
Per-circle edge darkening is in place.

**Shader file:** `Assets/Shaders/BalloonParty/Grid/Bush.shader`

**Active properties:**

| Property | Default | Purpose |
|---|---|---|
| `_SlotRadius` | 0.40 | Base circle radius per slot |
| `_RadiusJitter` | 0.06 | Per-slot radius variation |
| `_AAWidth` | 0.008 | Anti-aliasing edge width |
| `_BranchSpread` | 0.45 | How far branch circles extend from slot centre |
| `_SubCircleSize` | 0.55 | Circle radius relative to slot radius |
| `_SubCircleSizeVar` | 0.30 | Random size variation between circles |
| `_BaseColor` | dark green | Colour at single-coverage areas |
| `_TopColor` | bright green | Colour at max-coverage areas |
| `_CoverageScale` | 3.0 | How many overlapping circles = fully bright |
| `_CircleEdgeWidth` | 0.06 | Width of darkening band at circle edges |
| `_EdgeDarken` | 0.75 | How dark circle edges get |
| `_WindSpeed` | 0.4 | Wind animation speed |
| `_WindAmount` | 0.015 | Wind displacement magnitude |

**Shader features:** `_SHADOW_ON`, `_DISTURBANCE_ON`

**C# pipeline:**
- `BushViewController` overrides `PopulatePositions` to inject gap-fill midpoints
  (`.w` = 0.65 radius scale). Gap fills use only the centre circle, real slots
  use all 4 sub-circles.
- `ClusterView` passes positions as `_SlotCentersWorld` (Vector4[16]) via MPB.

---

## Identified problems

| # | Issue | Screenshot evidence | Description |
|---|---|---|---|
| V1 | Intersection transparency | Venn-diagram dark zones where circles overlap | Coverage accumulation counts overlapping circles and maps to colour gradient. Where 2+ circles overlap the colour jumps darker because `closestEdge` is small at both circle edges → darkening compounds. Result looks like semi-transparent discs, not opaque stacking. |
| V2 | No inner shadow / crease | Flat colour transitions at circle overlaps | Where a higher leaf clump sits on top of a lower one, there should be a **thin dark crease** at the upper circle's edge — giving the impression of depth between layers. Currently missing entirely. |
| V3 | Branches not visible | No brown structure anywhere | Need an optional branch/trunk skeleton rendered **behind** leaf circles. Thin brown lines radiating from the slot centre, visible only in gaps where leaf circles don't fully cover. |
| V4 | No variety range | All bushes identical density | Need a **fluffy → thin** spectrum per cluster. Fluffy = dense circles overlapping heavily, branches fully hidden. Thin = circles spread out, branches peek through gaps. |
| V5 | Circles too perfectly round | Smooth circle outlines everywhere | Leaf clump silhouettes should have slightly bumpy/scalloped edges — not noise-based, but small circle cutouts at the rim. |

---

## Target visual

Reference images: top-down stylised trees/bushes (attached to conversation).

Key visual qualities:
- **Opaque stacking**: upper circles fully cover lower ones — no venn-diagram blending
- **Inner shadows**: dark crease at the edge of each upper circle where it overlaps
  a lower one, selling depth between leaf clumps
- **Branches**: thin brown lines from centre outward, visible in sparse canopy gaps
- **Scalloped edges**: bumpy circle outlines from subtracted micro-circles
- **Density variety**: fluffy (compact, branches hidden) ↔ thin (spread, branches visible)

---

## Approach

### 1. Opaque painter's-algorithm layering (fixes V1 + V2)

Replace coverage accumulation with **deterministic back-to-front painting**.

Each sub-circle gets a depth index (0 = furthest, 3 = nearest). For a given
fragment, iterate circles back-to-front. When a circle covers the pixel,
**overwrite** the colour (not add). Lower circles get darker colour, upper
circles get brighter colour.

Inner shadow: when an upper circle covers a pixel that was already covered by a
lower circle, darken a thin band near the upper circle's edge:

```
for each circle i from back to front:
    d = SDF(wp, circle_i)
    if d < 0:
        overwrite colour with layer_colour[depth_i]
        if was_already_covered:
            inner_shadow = smoothstep(0, crease_width, -d)
            darken colour by (1 - inner_shadow * shadow_amount)
        was_already_covered = true
```

This naturally produces opaque, layered leaf clumps with crease shadows.

### 2. Branch skeleton (fixes V3)

Before the leaf circle loop, evaluate a branch SDF:
- For each real slot (not gap fills), generate 2–3 capsule SDFs (line segments
  with thickness) from the slot centre outward along the branch directions
  (same angles used for sub-circles).
- If inside a branch capsule and NOT covered by any leaf circle → render brown.
- If inside both a branch and a leaf circle → leaf wins (branches are behind).

Branch thickness property: `_BranchThickness` (Range 0.01–0.05).
Branch colour property: `_BranchColor` (brown).

Implementation: evaluate branch SDF once, store result. After the leaf loop,
if no leaf circle covered the pixel but the branch did → branch colour. If leaf
covered → leaf colour. This means branches show through gaps.

### 3. Density variation (fixes V4)

Per-cluster seed (already in `.z`) drives a density factor.

```
float density = frac(seed * 7.31); // 0–1 per cluster
float spreadMul = lerp(1.3, 0.7, density);  // thin = wide spread
float sizeMul   = lerp(0.7, 1.2, density);  // thin = smaller circles
```

Apply to branch spread and circle sizes per-slot. Denser clusters hide branches
naturally because circles overlap more.

Optional: expose `_DensityMin` / `_DensityMax` to clamp the range.

### 4. Edge scalloping (fixes V5)

For each circle, subtract N tiny circles placed along its circumference:

```
float scallop = 0;
for (int s = 0; s < SCALLOP_COUNT; s++)
{
    float angle = hash * 6.28 + s * (6.28 / SCALLOP_COUNT);
    float2 notchCenter = circleCenter + float2(cos(angle), sin(angle))
                       * (circleRadius + _ScallopDepth * 0.5);
    scallop = max(scallop, _ScallopDepth - length(wp - notchCenter));
}
d = max(d, scallop); // subtract notches from circle
```

`SCALLOP_COUNT` = 5–7 (hardcoded for perf).
Properties: `_ScallopDepth` (Range 0–0.04), `_ScallopSize` (Range 0.01–0.05).

---

## Task breakdown

```
T1  [ ] Refactor fragment loop — deterministic depth-ordered painting per
        sub-circle (back-to-front), overwrite colour instead of accumulate.
        Remove _CoverageScale, _CircleEdgeWidth, _EdgeDarken.
T2  [ ] Inner shadow / crease — darken thin band near upper circle edge when
        overlapping a lower circle. Add _CreaseWidth, _CreaseDarken properties.
T3  [ ] Branch skeleton — SDF capsule layer behind leaves. Add _BranchThickness,
        _BranchColor. Render only in leaf gaps.
T4  [ ] Density variation — derive from cluster seed. Scale spread + size per
        slot. Add _DensityMin, _DensityMax properties.
T5  [ ] Edge scalloping — subtracted micro-circles at circle rims.
        Add _ScallopDepth, _ScallopSize properties.
T6  [ ] Property cleanup — remove dead properties from previous iterations
        that no longer apply.
T7  [ ] Tuning pass — colours, radii, branch thickness, density range at game
        scale with balloons + Puff visible.
T8  [ ] Validation — 1, 2, 3+ slot clusters; side-by-side with Puff; check
        perf (no regression from sub-circle loop).
```

---

## Properties after rework (expected)

| Property | Type | Purpose |
|---|---|---|
| `_SlotRadius` | Float | Base circle radius |
| `_RadiusJitter` | Range | Per-slot radius variation |
| `_AAWidth` | Range | Anti-aliasing |
| `_BranchSpread` | Range | Sub-circle offset from centre |
| `_SubCircleSize` | Range | Sub-circle radius factor |
| `_SubCircleSizeVar` | Range | Size variation |
| `_BaseColor` | Color | Deepest / lowest circle colour |
| `_TopColor` | Color | Highest / nearest circle colour |
| `_CreaseWidth` | Range | Inner shadow band width |
| `_CreaseDarken` | Range | Inner shadow darkness |
| `_BranchThickness` | Range | Branch capsule half-width |
| `_BranchColor` | Color | Branch colour (brown) |
| `_DensityMin` | Range | Minimum canopy density |
| `_DensityMax` | Range | Maximum canopy density |
| `_ScallopDepth` | Range | Edge bump depth |
| `_ScallopSize` | Range | Edge bump circle radius |
| `_WindSpeed` | Range | Wind animation speed |
| `_WindAmount` | Range | Wind displacement |
| `_TimeOffset` | Float | MPB-driven animation time |
| `_ShadowColor` | Color | Ground shadow |
| `_ShadowOffsetX/Y` | Range | Shadow offset |
| `_ShadowSoftness` | Range | Shadow edge softness |
| `_DisplaceWorldScale` | Range | Disturbance displacement |

---

## Exit criteria

- Bush canopy reads as **layered, opaque leaf clumps** — no venn-diagram blending.
- **Inner shadows** darken at overlap seams, selling depth between layers.
- **Branches** peek through on thinner variants; fully hidden on fluffy ones.
- **Silhouette edges** are bumpy and organic without noise.
- **Visual variety** from cluster to cluster (density spectrum).
- Clearly **differentiated from Puff** clouds.
- No performance regression at game scale.

