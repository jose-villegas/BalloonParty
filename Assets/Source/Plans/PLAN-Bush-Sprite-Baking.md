@page plan_bush_sprite_baking Bush Sprite Baking & Ruffle Animation

# Bush — Sprite Baking & Ruffle Animation

> Pivoting the bush rendering from a per-fragment SDF shader to baked sprites
> with layered ruffle animation. The procedural shader becomes an offline baker;
> runtime rendering uses standard sprites with DOTween-driven leaf animation.

---

## Motivation

The procedural SDF approach (`Bush.shader`, ~600 lines, ~90-110 SDF evals per
fragment after optimization) delivers high visual quality but has fundamental
problems:

1. **GPU cost** — even after heavy optimization, every fragment evaluates
   phyllotaxis, leaf SDFs, venation, self-shadow, and ground shadow. On mobile
   this is expensive for a background obstacle.

2. **Inflexible interaction** — the disturbance field warps the SDF evaluation
   coordinates, which *deforms* the canopy shape. This looks like rubber
   stretching, not leaves rustling. Real bushes don't deform — individual
   leaves shake, bounce, and settle.

3. **No per-leaf animation** — the SDF approach treats the entire canopy as one
   implicit surface. There's no concept of individual leaf sprites that can
   independently rotate, scale, or translate in response to gameplay events.

4. **Diminishing returns** — adding more visual detail (venation, specular,
   lateral veins) increases fragment cost linearly. A baked texture captures
   arbitrarily complex detail at zero runtime cost.

---

## What we salvage

| Asset | How it's reused |
|---|---|
| **Phyllotaxis math** (`BushView.PhyllotaxisCenter`, `MathUtils`) | Places leaf sprites at runtime AND drives the baker's leaf layout |
| **Leaf SDF + shading pipeline** (`Bush.shader`) | The baker renders the SDF shader into a RenderTexture to produce baked canopy sprites |
| **ClusterView / ClusterViewController** | The clustering, gap-fill, bounds, and slot management infrastructure stays. `BushView` subclass changes what it manages (sprites instead of a single SDF quad) |
| **BushClusterRegistry** | Unchanged — still groups adjacent bush slots |
| **BushViewController + gap-fill logic** | Unchanged — still adds midpoint positions for continuous coverage |
| **IBushSettings / BushSettings SO** | Extended with new fields for ruffle animation and baked asset references |
| **DisturbanceFieldService** | Still stamps projectile positions. Instead of warping SDF coordinates, the stamp drives per-leaf ruffle intensity |
| **Pool system** | Leaf sprites are pooled via `PoolChannel<LeafSprite>` |
| **DOTween** | All ruffle animation (shake, rotate, scale bounce) |

---

## Architecture overview

```
BushBaker (Editor tool)
    │  Renders Bush.shader into RenderTexture at high resolution
    │  Exports Texture2D assets per bush variant (canopy + individual leaves)
    │  Previews variants with different seeds / slot counts
    │
    ▼
BushView : ClusterView
    │  On Configure: spawns layered leaf sprites from pool
    │  Base layer: baked canopy sprite (SpriteRenderer, static)
    │  Leaf layers: individual leaf SpriteRenderers at phyllotaxis positions
    │  Each leaf has a sorting order based on depth layer
    │
    ▼
BushRuffleController (ITickable)
    │  Reads DisturbanceFieldService or direct projectile distance
    │  Computes per-leaf ruffle intensity based on proximity + direction
    │  Drives DOTween sequences: rotation shake, scale bounce, position offset
    │  Inner leaves move less, outer leaves move more
    │
    ▼
Leaf sprites animate individually — rustling, not deforming
```

---

## Baking pipeline

### What the baker produces

For each bush **variant** (seed + configuration):

| Asset | Content | Resolution | Purpose |
|---|---|---|---|
| **Canopy base** | Full bush canopy — Gielis leaf shapes, SSS, full-depth soft shadows, AO, hierarchical veins, dome shading, all baked | 256×256 or 512×512 | Static background layer — the dense, detailed centre |
| **Leaf atlas** | Individual Gielis leaf shapes at various sizes, with SSS edge glow, hierarchical veins, dome shading, per-leaf colour variation | 512×512 atlas | Runtime leaf sprites for the outer ring — these ruffle |
| **Branch overlay** | Branch skeleton rendered separately | 128×128 | Optional: visible through gaps in thinner variants |

### Baker workflow

1. **Editor window** (`BushBakerWindow`) — set variant count, seed range,
   resolution. Preview grid shows all variants side by side.
2. **Render step** — for each variant, set up a temporary `SpriteRenderer` with
   the Bush material, configure slot positions, render via `Camera.Render()`
   into a RenderTexture, read back to `Texture2D`.
3. **Export** — save as PNG or embedded asset. Leaf atlas uses tight sprite
   packing.
4. **Configuration** — `BushSettings` SO references the baked sprite array +
   leaf atlas. Runtime randomly selects a variant per cluster.

### Individual leaf baking

The leaf baker renders each leaf in isolation using the enhanced baking shader:

1. Set up a 1-slot configuration with a single leaf at depth 0.
2. Evaluate the full Gielis superformula SDF with per-leaf parameter variation.
3. Render the complete shading pipeline — SSS edge glow, hierarchical veins,
   dome shading, specular highlight, edge browning.
4. Apply per-leaf hue jitter and colour variation.
5. Read back the alpha-premultiplied result.
6. Pack multiple leaf shapes/sizes into a sprite atlas.

This gives us high-quality leaf sprites with all the procedural detail (lateral
veins, midrib, dome shading, specular) baked in — zero runtime shading cost.

---

## Baking quality — unlocked by offline rendering

Since the baker runs once in the editor with no frame budget, we can push far
beyond what the real-time SDF shader could afford. Every quality upgrade below
adds zero runtime cost — it's all captured in the baked pixels.

### Full Gielis superformula leaf shapes

The real-time shader uses a **simplified CSG approximation** — two circles
intersected via `max(d1, d2)`. This produces a symmetric lens but can't
express asymmetric leaf forms, serrated edges, or lobed shapes.

The full Gielis superformula in polar coordinates:

```
r(θ) = ( |cos(m₁θ/4)/a|^n₂ + |sin(m₂θ/4)/b|^n₃ )^(-1/n₁)
```

Where `m₁, m₂` control lobe count, `n₁, n₂, n₃` control curvature, and
`a, b` control aspect ratio. This single formula can produce:

| Parameters | Shape |
|---|---|
| m=2, n₁=n₂=n₃=1 | Ellipse |
| m=3, n₁=4, n₂=n₃=8 | Rounded triangle (leaf-like) |
| m=1, n₁=0.5, n₂=n₃=0.5 | Asymmetric teardrop |
| m=5, n₁=2, n₂=n₃=7 | Pentagonal flower petal |
| m=0, n₁=n₂=n₃=1 | Circle |

**Baker implementation:** evaluate `r(θ)` in polar coordinates at each texel.
Convert to an SDF by computing signed distance from the superformula boundary.
The baker can afford the `pow()` and `atan2()` calls that were too expensive
per-fragment at runtime.

**Leaf variety:** by varying `m`, `n₁`, `n₂`, `n₃` per leaf, the atlas can
contain genuinely different leaf shapes — ovate, lanceolate, cordate, lobed —
not just scaled copies of the same lens.

### Soft self-shadow on all layers

The real-time shader only checks `SELF_SHADOW_LAYERS = 4` upper depths per
covered leaf, and uses a cheap circle check (not the actual leaf SDF). The
baker has no such constraints.

**Full-depth shadow accumulation:**
- For each leaf at depth D, check ALL higher leaves (D+1 … LEAF_COUNT-1).
- Use the actual Gielis leaf SDF (not a circle approximation).
- Soft shadow with configurable penumbra (Gaussian falloff from shadow edge).
- Multiple shadow offset samples (4-8 jittered offsets) for contact-hardening
  soft shadows — shadow is crisp near the casting leaf, soft far away.

**Ambient occlusion:**
- For each visible pixel, count how many leaves are above it across all slots.
- More coverage = darker ambient occlusion.
- This sells depth even without explicit light direction — the dense centre
  of the canopy is naturally darker.

### Subsurface scattering (SSS)

Real leaves are translucent — light passes through, creating a bright glow at
thin edges and a warm colour shift. This is impossible in the real-time SDF
shader (would require tracing through leaf thickness per fragment).

**Baker SSS approach:**

1. **Thickness map** — for each visible leaf pixel, compute the leaf's local
   thickness (distance from the pixel to both edges of the Gielis SDF along
   the light direction). Thin edges near the leaf tips have low thickness;
   the centre has high thickness.

2. **Transmittance** — `transmittance = exp(-thickness × _SSSAbsorption)`.
   Thin edges transmit more light. The exponential falloff matches real leaf
   optics (Beer-Lambert law).

3. **Colour shift** — transmitted light shifts toward warm yellow-green
   (`_SSSColor`). Real chlorophyll absorbs blue/red and transmits green.
   `finalColor = lerp(baseColor, _SSSColor, transmittance × _SSSStrength)`.

4. **Back-lighting** — leaves that face away from the "light" (based on
   `leafDir` vs `_LightDir`) get extra transmittance, simulating light
   arriving from behind. Only upper leaves (closer to the "sun") contribute
   significant back-light.

**Visual effect:** leaf edges glow warmly. Inner leaves are darker (occluded).
The canopy reads as a living, translucent volume — not flat painted shapes.

### Enhanced venation

Since `atan2` and `pow` are free in the baker, and we can run CPU-side
simulations, the venation system can move from the diagonal-stripe
approximation to a biologically accurate model.

**Runions auxin-based venation** (Runions et al. 2005, see `PLAN-Bush-Research.md`
§3.1) — scatter auxin source points across the leaf; vein nodes grow toward
them; sources are consumed on contact. This produces realistic pinnate
venation with natural branching, curvature, and hierarchy. ~200 simulation
nodes per leaf, ~50 lines of C#. The result is rasterised as anti-aliased
lines with thickness hierarchy into the baked leaf texture.

- **Hierarchical branching** — primary veins branch into secondary veins, which
  branch into tertiary veins. Each level uses the parent vein's position as
  origin. Real pinnate venation has 3-4 levels of branching.

- **Curved veins** — lateral veins follow a quadratic or cubic curve toward the
  leaf tip, not the straight diagonal stripes of the real-time shader. Computed
  via parametric Bézier evaluation along each vein path.

- **Vein thickness variation** — primary midrib is thick (2-3 px at bake
  resolution), secondary veins are thinner (1-2 px), tertiary veins are
  sub-pixel (anti-aliased). Creates a natural hierarchy.

- **Vein bump / parallax** — veins are slightly raised on the leaf surface.
  The baker can compute a simple normal map per leaf (vein ridges create
  directional highlights). Stored as a second channel or separate atlas if the
  runtime sprite shader supports normal-mapped lighting.

### Per-leaf colour variation

- **Hue jitter** — each leaf's base green shifts slightly in hue (±10°),
  simulating natural chlorophyll variation. Hash-driven per leaf.

- **Age gradient** — outer (older) leaves are slightly yellower or darker than
  inner (younger) leaves. Driven by `depthT`.

- **Edge browning** — a subtle warm-brown tint near the leaf edges, especially
  at the tips. Computed from the Gielis SDF distance field — pixels near the
  boundary get a brown blend.

### Ground shadow

The baked ground shadow can use the full leaf SDF at all depths (not just
`SHADOW_DEPTH_LAYERS = 4`). With the Gielis formula, shadow silhouettes show
realistic serrated/lobed edges. The baker can also add soft penumbra via
multi-sample offset averaging.

### Quality settings for the baker

| Setting | Default | Purpose |
|---|---|---|
| `_BakeResolution` | 512 | Texels per canopy sprite |
| `_LeafResolution` | 64 | Texels per individual leaf in the atlas |
| `_ShadowSamples` | 8 | Jittered shadow offset count |
| `_SSSAbsorption` | 3.0 | Subsurface scattering falloff rate |
| `_SSSStrength` | 0.25 | How much SSS affects the final colour |
| `_SSSColor` | (0.6, 0.8, 0.2) | Transmitted light colour |
| `_VeinLevels` | 3 | Hierarchical vein branching depth |
| `_AOMul` | 0.4 | Ambient occlusion strength |
| `_GielisM` | 2.0 | Superformula lobe count |
| `_GielisN1` | 1.0 | Superformula curvature |
| `_GielisN2` | 1.5 | Superformula lateral curvature |
| `_GielisN3` | 1.5 | Superformula lateral curvature |
| `_HueJitter` | 10.0 | Per-leaf hue variation in degrees |
| `_EdgeBrowning` | 0.15 | Warm brown tint at leaf edges |

---

## Runtime rendering

### Layer structure (per cluster)

| Layer | Sort order | Content | Animates? |
|---|---|---|---|
| **Shadow** | -1 | Baked or simple ellipse shadow | No (static offset) |
| **Base canopy** | 0 | Baked canopy sprite — dense centre | Subtle scale pulse on ruffle |
| **Mid leaves** | 1-3 | 4-6 leaf sprites from atlas, mid-ring phyllotaxis positions | Medium ruffle |
| **Top leaves** | 4-6 | 4-6 leaf sprites from atlas, outer-ring phyllotaxis positions | Strong ruffle |

The exact layer count and leaf count per layer are configurable in
`IBushSettings`. Fewer layers = cheaper. More layers = richer animation.

### Leaf placement (runtime)

The phyllotaxis math (`BushView.PhyllotaxisCenter`) places leaf sprites at
the same positions the SDF shader used. But only the **outer N leaves** get
individual sprites — the inner canopy is covered by the baked base sprite.

```
Outer ring (depth 0-3):  individual leaf sprites — these ruffle
Inner ring (depth 4-15): baked into the base canopy sprite — static
```

This means at most ~4-8 leaf sprites per real slot. For a 5-slot cluster:
20-40 leaf sprites. Each is a simple `SpriteRenderer` with a sprite from the
atlas — no custom shader needed. Standard sprite rendering, fully batched.

### Visual variety

- **Variant selection** — each cluster randomly picks a baked canopy variant
  from the array (seeded by cluster position hash).
- **Leaf atlas randomization** — each leaf sprite picks a random sub-sprite
  from the atlas (different sizes, vein patterns).
- **Tint variation** — per-leaf colour tint via `SpriteRenderer.color` or MPB.
- **Rotation** — each leaf sprite's initial rotation is set by the phyllotaxis
  angle, plus a small random offset.

---

## Ruffle animation

### Trigger: projectile proximity

Two detection approaches (not mutually exclusive):

**A. Disturbance field sampling (existing system)**
- `BushRuffleController` samples `DisturbanceFieldService.FieldTexture` at the
  cluster's world position each tick.
- The displacement channels (GB) indicate recent projectile activity nearby.
- Magnitude of displacement → ruffle intensity.
- Direction of displacement → ruffle direction (leaves push away from the
  projectile path).
- **Pro:** reuses existing system, automatically handles multiple projectiles.
- **Con:** field resolution may be too coarse for precise per-leaf response.

**B. Direct projectile distance check**
- Subscribe to projectile position updates (MessagePipe event or direct query).
- For each active projectile, compute distance to cluster centre.
- If within ruffle radius, compute intensity = 1 - (dist / ruffleRadius).
- Direction = normalize(clusterCenter - projectilePos).
- **Pro:** precise, per-frame, no texture sampling.
- **Con:** need to iterate active projectiles; doesn't get the diffusion/reform
  behaviour for free.

**Recommendation:** Start with **B** (direct distance) for precision, but keep
the disturbance field keyword on the base canopy sprite shader for the subtle
UV-shift effect on the baked texture (much cheaper now since it's one sprite,
not 600 lines of SDF).

### Animation per leaf

When ruffle triggers (intensity > 0):

```
Each leaf sprite:
  1. Rotation shake — DOTween.Punch on Z rotation
     - Amplitude: ±15° × intensity × depthFactor
     - Duration: 0.3-0.5s
     - depthFactor: outer leaves (1.0) → inner leaves (0.3)

  2. Scale bounce — DOTween.Punch on localScale
     - Amplitude: 0.1 × intensity
     - Duration: 0.2-0.4s
     - Slight delay per leaf (stagger by phyllotaxis index)

  3. Position offset — DOTween.Punch on localPosition
     - Direction: away from projectile path
     - Amplitude: 0.02-0.05 world units × intensity
     - Springs back via DOTween ease

  4. Settle — all tweens use Ease.OutElastic or OutBack for natural spring-back
```

Staggering by phyllotaxis index creates a wave effect — the leaf closest to
the projectile path reacts first, then adjacent leaves follow.

### Wind (ambient)

Separate from ruffle. A slow continuous animation on all leaf sprites:

- Gentle rotation oscillation: `DOTween.To` on Z rotation, ±3°, ~2s period
- Per-leaf phase offset from phyllotaxis index
- Driven by `_TimeOffset` (same as the current wind system)
- Much cheaper than per-fragment simplex noise

---

## Performance comparison

| Metric | SDF shader (current) | Baked sprites (proposed) |
|---|---|---|
| Fragment cost | ~90-110 SDF evals | Standard sprite sampling (1 texture fetch) |
| Draw calls | 1 per cluster (but heavy fragment) | ~10-20 per cluster (but trivially cheap, SRP batchable) |
| Animation | Per-fragment simplex noise + disturbance warp | DOTween sequences on transforms (CPU, negligible) |
| Interaction | SDF deformation (looks like rubber) | Per-leaf ruffle (looks like real bushes) |
| Visual detail | Computed per-pixel (expensive but sharp at any zoom) | Baked at fixed resolution (free at runtime, but has a resolution ceiling) |
| Memory | Zero (procedural) | ~64-256 KB per variant sprite + leaf atlas |
| Authoring | Shader code changes | Editor tool generates variants; artists can preview |

### Resolution concern

At the game's target resolution and camera zoom, a 256×256 canopy sprite covers
roughly the same pixel density as the procedural shader. The baked texture can
be higher-res (512×512) for retina displays with negligible memory cost.

Individual leaf sprites from the atlas are small (32×32 to 64×64 per leaf) —
they only need to look good at their actual screen size.

---

## Implementation tasks

### Phase 0 — Enhanced baking shader

```
B0a [ ] BushBake.shader — offline-only shader with full Gielis superformula SDF
B0b [ ] Subsurface scattering pass — thickness map + Beer-Lambert transmittance
B0c [ ] Full-depth self-shadow — all layers, actual leaf SDF, multi-sample penumbra
B0d [ ] Ambient occlusion — per-pixel coverage accumulation across all layers
B0e [ ] Runions venation simulation — auxin-based vein growth (CPU, ~200 nodes/leaf)
B0f [ ] Per-leaf colour variation — hue jitter, age gradient, edge browning
B0g [ ] Enhanced ground shadow — full-depth Gielis SDF with soft penumbra
```

### Phase 1 — Leaf atlas baking (Editor tool)

```
B1  [ ] BushLeafBaker — renders individual leaves via Bush.shader into RT
B2  [ ] Leaf atlas packer — packs multiple leaf variants into a sprite atlas
B3  [ ] BushBakerWindow — EditorWindow to configure and preview variants
```

### Phase 2 — Baked canopy sprite

```
B4  [ ] BushCanopyBaker — renders full canopy (all slots) into RT
B5  [ ] Variant system — generate N variants with different seeds
B6  [ ] BushSettings extension — reference baked sprites + atlas
```

### Phase 3 — Runtime leaf sprites

```
B7  [ ] LeafSprite component — SpriteRenderer + ruffle state
B8  [ ] LeafSpritePoolChannel — pooling for leaf sprites
B9  [ ] BushView refactor — spawn base canopy + leaf sprites on Configure
B10 [ ] Phyllotaxis placement — reuse math to position outer leaf sprites
```

### Phase 4 — Ruffle animation

```
B11 [ ] BushRuffleController — ITickable, detects projectile proximity
B12 [ ] Per-leaf DOTween sequences — rotation, scale, position punch
B13 [ ] Stagger system — wave delay based on phyllotaxis index + distance
B14 [ ] Ambient wind — slow continuous oscillation on all leaves
```

### Phase 5 — Integration & cleanup

```
B15 [ ] Remove Bush.shader from runtime (keep as baker-only asset)
B16 [ ] Update BushViewController for new BushView API
B17 [ ] Validate: multi-slot clusters, gap-fill, sorting, performance
B18 [ ] Side-by-side with Puff clouds — visual harmony check
```

---

## Open questions

| # | Question | Notes |
|---|---|---|
| 1 | **How many leaf layers?** | 2-3 layers (8-12 leaves) per slot feels right. Profile on device. |
| 2 | **Bake at build time or editor time?** | Editor time preferred — baked assets ship with the build, zero runtime cost. Build-time baking adds complexity. |
| 3 | **Atlas or individual sprites?** | Atlas is better for batching. Unity's sprite atlas system handles this. |
| 4 | **Keep the SDF shader for anything runtime?** | Possibly for gap-fill circles (they're small, cheap) or as a debug overlay. Otherwise, baker-only. |
| 5 | **Base canopy sprite shader?** | Could be a plain sprite shader with optional disturbance UV-shift (much cheaper than full SDF). Or just `Sprites/Default`. |
| 6 | **Cluster merging** | Multi-slot clusters currently use one quad with all slot positions. With sprites, each slot could have its own base canopy + leaf set. The clustering infrastructure handles bounds and lifecycle. |

---

## Exit criteria

- Bush renders as **baked sprites** — no per-fragment SDF at runtime.
- **Baked quality exceeds real-time** — full Gielis leaf shapes, subsurface
  scattering, multi-sample soft shadows, hierarchical venation, ambient
  occlusion. All captured in the baked textures at zero runtime cost.
- **Outer leaves ruffle** when a projectile passes — rotation shake, scale
  bounce, position offset. Settles with elastic ease.
- **Wave effect** — leaves closest to the projectile react first, then
  neighbours follow.
- **Ambient wind** — gentle continuous oscillation on all leaves.
- **Visual quality** significantly exceeds the real-time SDF shader — SSS edge
  glow, proper soft shadows, varied leaf shapes.
- **Editor tool** generates baked variants with preview. Quality settings
  exposed for iteration.
- **Performance** — dramatically cheaper than the SDF approach. Standard sprite
  rendering, no custom fragment work.
- Clearly **differentiated from Puff** clouds (Puff is procedural noise
  volumes; bush is layered opaque leaf sprites with botanical detail).

