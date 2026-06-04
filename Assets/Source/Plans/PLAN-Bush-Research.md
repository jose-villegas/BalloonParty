@page plan_bush_research Bush Rendering — Research & References

# Bush Rendering — Research & References

> Academic papers, techniques, and algorithms relevant to procedural bush
> canopy rendering and baking, with emphasis on top-down perspective.
> Compiled to inform `PLAN-Bush-Sprite-Baking.md`.

---

## 1. Leaf shape generation

### 1.1 Gielis superformula

**Paper:** Gielis, J. (2003). *A generic geometric transformation that unifies
a wide range of natural and abstract shapes.* American Journal of Botany,
90(3), 333–338.

The superformula generalises circles, ellipses, and many biological shapes
into a single polar equation:

```
r(θ) = ( |cos(m·θ/4) / a|^n₂  +  |sin(m·θ/4) / b|^n₃ )^(-1/n₁)
```

**Key insight for the baker:** the paper catalogues parameter ranges for
specific botanical shapes. Relevant presets:

| Shape | m | n₁ | n₂ | n₃ | a | b |
|---|---|---|---|---|---|---|
| Ovate leaf | 1 | 0.5 | 0.5 | 0.5 | 1 | 1 |
| Lanceolate leaf | 2 | 2.0 | 4.0 | 4.0 | 1 | 1 |
| Cordate (heart) | 1 | 0.8 | 1.0 | 0.3 | 1 | 1 |
| Rounded triangle | 3 | 4.0 | 8.0 | 8.0 | 1 | 1 |
| Serrated edge | 12 | 15 | 15 | 15 | 1 | 1 |

**SDF conversion:** the superformula defines `r(θ)` — to get a signed distance
field, compute `d = |p| - r(atan2(p.y, p.x))` at each texel. For smooth SDF
behaviour near the boundary, Inigo Quilez recommends normalizing by the
gradient magnitude: `d / |∇d|`. The baker can afford this per-texel.

**Compound shapes:** multiply two superformulas with different `m` values to
get a serrated leaf (high-frequency edge detail on a low-frequency leaf body).
`r_final(θ) = r_body(θ) × (1 + amplitude × (r_serration(θ) - 1))`.

### 1.2 Leaf contour from Fourier descriptors

**Paper:** Neto, J.C. et al. (2006). *Plant species identification using
Elliptic Fourier leaf shape analysis.* Computers and Electronics in
Agriculture, 50(2), 121–134.

Real leaf outlines decomposed into Fourier harmonics. The first 10-20
harmonics capture 99% of the contour variation. Useful if we want to match
specific real species by fitting Fourier coefficients to scanned leaf outlines.

**Applicability:** lower priority than Gielis — the superformula is more
intuitive to tune. But Fourier descriptors could be used to validate that
Gielis parameter ranges produce botanically plausible shapes.

---

## 2. Leaf placement — phyllotaxis & alternatives

### 2.1 Douady-Couder phyllotaxis (already implemented)

**Paper:** Douady, S. & Couder, Y. (1992). *Phyllotaxis as a Physical
Self-Organized Growth Process.* Physical Review Letters, 68(13), 2098–2101.

Golden-angle (137.508°) spiral with `r ∝ √n` spacing. Already implemented
in `BushView.PhyllotaxisLeaf`. This is the standard for spiral phyllotaxis.

**Extension for the baker:** Douady-Couder also describes **parastichy** —
the visible spiral arms in sunflower heads. The baker could highlight
parastichy patterns in the canopy by grouping leaves along Fibonacci spiral
arms and applying per-arm colour variation.

### 2.2 Poisson disk sampling for naturalistic scatter

**Paper:** Bridson, R. (2007). *Fast Poisson disk sampling in arbitrary
dimensions.* SIGGRAPH Sketches.

Produces blue-noise distributed points with a minimum distance guarantee.
More random-looking than phyllotaxis but still evenly spaced. Runs in O(n).

**Applicability:** useful for placing leaf sprites in the baked canopy where
strict spiral order isn't needed — e.g. filling gaps between phyllotaxis
leaves, or scattering tertiary leaf detail in the background layer.

### 2.3 Space colonisation algorithm

**Paper:** Runions, A. et al. (2007). *Modeling Trees with a Space
Colonization Algorithm.* Eurographics Workshop on Natural Phenomena.

Grows branching structures toward a cloud of attractor points. Each branch
tip claims nearby attractors, extends toward them, then the attractors are
consumed. Produces realistic tree crowns with natural density variation.

**Applicability for top-down bushes:** the attractor cloud defines the canopy
silhouette. Branch tips become leaf placement sites. The algorithm naturally
produces denser growth in open areas and sparser growth in occluded regions.
Could replace or complement phyllotaxis for more organic canopy shapes.

**Baker integration:** run space colonisation on the CPU to generate branch
structure + leaf positions, then bake each leaf using the Gielis SDF. The
branching structure itself becomes the branch overlay layer.

---

## 3. Leaf venation

### 3.1 Runions venation model (most relevant)

**Paper:** Runions, A. et al. (2005). *Modeling and visualization of leaf
venation patterns.* ACM SIGGRAPH 2005.

Simulates vein growth using an **open venation** model where veins grow toward
auxin sources (growth hormone). The algorithm:

1. Scatter auxin source points across the leaf surface.
2. Each source is associated with the nearest vein node.
3. Vein nodes grow toward their associated sources.
4. Sources consumed when a vein reaches them.
5. New sources can appear as the leaf "grows."

This produces realistic pinnate (fishbone), palmate (hand-like), and
reticulate (net-like) venation depending on initial vein placement and auxin
distribution.

**Key parameters:**
- `d_kill` — distance at which a source is consumed by a vein node
- `d_attract` — maximum distance a source can influence a vein node
- Initial vein positions (midrib placement determines pinnate vs. palmate)

**Baker integration:** run the venation simulation at high resolution on the
CPU (it's fast — a few hundred nodes). Rasterise the resulting vein graph as
anti-aliased lines with thickness hierarchy (primary → secondary → tertiary).
Bake into the leaf texture. This produces venation that is biologically
accurate — not just stripes.

### 3.2 Leaf venation from DLA (Diffusion-Limited Aggregation)

**Paper:** Rodkaew, Y. et al. (2003). *Particle Systems for Plant Modeling.*
Plant Growth Modeling and Applications.

Uses DLA — particles random-walk until they contact the growing vein
structure, then stick. Produces dendritic patterns similar to real veins.
Simpler to implement than Runions but less controllable.

**Applicability:** lower priority. The Runions model gives more control over
venation type (pinnate vs. palmate) and is better documented.

---

## 4. Subsurface scattering in foliage

### 4.1 Physically-based leaf translucency

**Paper:** Habel, R. et al. (2007). *Physically-Based Real-Time Translucency
for Leaves.* I3D (Symposium on Interactive 3D Graphics and Games).

Models leaf translucency as a function of:
- **Leaf thickness** — measured from BSSRDF data of real leaves
- **Chlorophyll absorption** — wavelength-dependent, peaks at red and blue
- **Internal scattering** — light bounces between cell layers (palisade and
  spongy mesophyll)

**Simplified model for baking:**

```
T(x) = exp(-σ_t · thickness(x))
colour(x) = front_colour × (1 - T) + sss_colour × T × back_light
```

Where `σ_t` is the extinction coefficient (~3-6 per leaf thickness unit),
`thickness(x)` is the local leaf thickness from the SDF, and `back_light`
is the dot product between the surface normal and the reverse light direction.

**Key finding:** even a very simplified SSS model (just edge glow based on SDF
distance) reads as translucent in still images. The visual improvement is
disproportionate to the implementation cost — high priority for the baker.

### 4.2 BSSRDF leaf model

**Paper:** Wang, L. et al. (2005). *Rendering of Plant Leaves.* Technical
Report, University of Virginia.

Measures real leaf BSSRDF using a laser scanner. Provides fitted parameters
for several species. Key finding: the **spongy mesophyll** layer dominates
scattering — light enters through the bottom surface and exits diffusely from
the top, creating the warm glow visible when a leaf is back-lit.

**Simplified for 2D baking:** since the baker works in 2D (top-down), we can't
do full BSSRDF. But we can approximate:
- **Edge translucency** — pixels near the leaf SDF boundary (thin sections)
  transmit more light. Use `exp(-dist_from_edge × absorption)`.
- **Depth-dependent back-light** — lower leaves (higher depth index) are more
  back-lit because upper leaves are between them and the light source.

### 4.3 Precomputed subsurface for vegetation

**Paper:** Jensen, H.W. et al. (2001). *A Practical Model for Subsurface
Light Transport.* SIGGRAPH 2001.

The classic dipole SSS model. Overkill for 2D leaf baking, but the key
takeaway is the **diffusion profile** concept — SSS can be precomputed as a
blur kernel applied to the irradiance. For the baker, this means:

1. Compute direct lighting per texel (dome shading, shadows).
2. Apply a small Gaussian blur to the result (σ ≈ 2-4 texels).
3. Blend the blurred result back into the original based on thickness.

This gives a physically-grounded SSS approximation with minimal code.

---

## 5. Tree/bush canopy rendering

### 5.1 Texture lobes for tree modelling

**Paper:** Livny, Y. et al. (2011). *Texture-Lobes for Tree Modelling.*
Computer Graphics Forum, 30(7).

Represents tree crowns as overlapping **lobes** — textured ellipsoidal volumes
positioned along branches. Each lobe is a pre-rendered leaf cluster. The tree
is composed of ~10-50 lobes instead of thousands of individual leaves.

**Directly applicable to baking:** this is essentially what the baked canopy
sprite approach does — each slot's canopy is a pre-rendered lobe. Multiple
slots compose the full bush. The paper's insight about lobe overlap blending
(soft alpha at lobe edges, depth-sorted compositing) applies to our layer
compositing.

### 5.2 Billboard clouds for vegetation

**Paper:** Décoret, X. et al. (2003). *Billboard Clouds for Extreme Model
Simplification.* SIGGRAPH 2003.

Approximates complex 3D geometry with a small set of textured billboards
(quads). For trees, the crown is represented by ~10-20 billboards oriented
to match the dominant leaf cluster directions.

**Top-down relevance:** from directly above, a billboard cloud degenerates to
a single horizontal billboard per cluster — which is exactly our baked canopy
sprite. The paper's **billboard placement algorithm** (greedy error-minimizing
selection of billboard orientations) could inform how we choose which leaves
get individual sprites vs. which get baked into the base.

### 5.3 Approximate image-based tree modelling

**Paper:** Neubert, B. et al. (2007). *Approximate Image-Based Tree-Modeling
using Particle Flows.* ACM Transactions on Graphics, 26(3).

Reconstructs 3D tree models from photographs using particle flow simulation.
Not directly applicable to procedural generation, but the paper's **density
field** concept is useful: the canopy is represented as a 3D density field,
and leaves are placed where density exceeds a threshold.

**For the baker:** define a 2D density field per slot (radial falloff from
slot centre, modified by phyllotaxis). Leaves are denser where the field is
high. This controls the fluffy-to-thin spectrum — dense clusters have more
leaves at more positions, thin clusters have fewer.

### 5.4 Real-time rendering of forest canopy from above

**Paper:** Bruneton, E. & Neyret, F. (2012). *Real-time Realistic Rendering
and Lighting of Forests.* Computer Graphics Forum, 31(2).

Renders large forest scenes from aerial/satellite perspectives using:
- **Precomputed canopy BRDFs** — each tree species has a baked BRDF that
  encodes how the canopy reflects light from above. Includes self-shadowing
  and inter-leaf scattering.
- **Aperiodic tiling** — Wang tiles of canopy textures avoid repetition.
- **View-dependent level of detail** — close trees use individual leaves,
  distant trees use the precomputed BRDF.

**Key insight for our baker:** the **precomputed canopy BRDF** idea. We can
bake not just a colour texture but also a view-dependent shading response.
Since our game is strictly top-down (fixed view), we only need one view
direction, simplifying the BRDF to a single lighting response per texel.

---

## 6. Ambient occlusion in foliage

### 6.1 Screen-space ambient occlusion for vegetation

**Paper:** Laine, S. & Karras, T. (2011). *Two Methods for Fast Ray-Cast
Ambient Occlusion.* Eurographics Symposium on Rendering.

Not directly about vegetation, but the **bent normal** concept applies: for
each leaf pixel, the AO value encodes how much of the hemisphere above is
occluded by other leaves. This can be precomputed during baking.

### 6.2 Precomputed inter-leaf occlusion

**Technique (no single paper — common in production):** For each leaf at depth
D, count how many leaves at depths D+1…N overlap the same pixel. The overlap
count drives an AO darkening term. This is essentially what our self-shadow
system does, but generalized to all depths.

**Enhanced for baking:**
- For each canopy texel, cast ~16 sample rays in the upper hemisphere.
- Each ray checks intersection with all leaf SDFs above the current layer.
- The ratio of blocked rays = AO value.
- Store as a multiplier baked into the canopy colour.

This gives proper directional occlusion — leaves that are only partially
covered get partial darkening, not binary shadow.

---

## 7. Procedural colour & detail

### 7.1 Reaction-diffusion for leaf patterns

**Paper:** Turk, G. (1991). *Generating Textures on Arbitrary Surfaces using
Reaction-Diffusion.* SIGGRAPH 1991.

Turing patterns (reaction-diffusion) generate organic spots, stripes, and
labyrinthine patterns. Applied to leaf surfaces, this could generate:
- Variegated leaf patterns (light/dark patches)
- Disease spots or age marks
- Natural colour variation that follows the leaf geometry

**Baker integration:** run a small reaction-diffusion simulation on the leaf's
UV space. Use the result as a colour modulation texture. Cheap to compute
offline, adds organic detail that's impossible with hash-based noise.

### 7.2 Procedural leaf colour from spectral models

**Paper:** Baranoski, G.V.G. & Rokne, J.G. (2001). *An Algorithmic Reflectance
and Transmittance Model for Plant Tissue.* Computer Graphics Forum, 20(3).
(The ABM model, later extended as ALM in subsequent papers.)

Models leaf colour from physical properties:
- Chlorophyll concentration → green intensity
- Carotenoid concentration → yellow/orange
- Anthocyanin concentration → red/purple
- Water content → near-infrared reflectance
- Leaf structure (cell layers) → overall brightness

**For the baker:** instead of hand-picking `_BaseColor` and `_TopColor`, derive
leaf colour from a small set of biological parameters. Varying chlorophyll
concentration per leaf produces realistic green variation. Reducing chlorophyll
(autumn effect) naturally shifts leaves toward yellow/orange.

---

## 8. Production techniques from games/VFX

### 8.1 Sprite-based foliage in top-down games

Common in 2D/2.5D games (Stardew Valley, Graveyard Keeper, etc.):
- **Layered sprites** — 2-4 depth layers per tree canopy
- **Parallax offset** — layers shift slightly based on camera/wind
- **Individual leaf particles** — a few loose leaf sprites animate
  independently at the canopy edges
- **Baked normal maps** — enable dynamic lighting on pre-rendered sprites

**Applicable pattern:** the "core + fringe" approach. A solid pre-rendered
core (our baked canopy) surrounded by a fringe of animated individual leaves.
The core provides visual density; the fringe provides life and interaction.

### 8.2 Baked vegetation in mobile games

Common mobile optimization:
1. Render vegetation in a high-quality offline pass (ray-traced if needed).
2. Bake into sprite sheets with premultiplied alpha.
3. Runtime uses simple alpha-blended quads with per-vertex colour tinting.
4. Wind via vertex shader sine wave on the sprite quad.

**Key optimisation:** premultiplied alpha avoids dark fringing at leaf edges.
The baker should output premultiplied alpha textures.

---

## 9. Summary — recommended techniques for the baker

| Priority | Technique | Source | Impact |
|---|---|---|---|
| **P0** | Full Gielis superformula | Gielis 2003, §1.1 | Varied, botanically accurate leaf shapes |
| **P0** | Runions venation model | Runions 2005, §3.1 | Realistic hierarchical vein networks |
| **P0** | Edge-based SSS | Habel 2007, §4.1 | Translucent leaf edges — massive visual upgrade |
| **P0** | Full-depth self-shadow + AO | §6.2 | Proper depth and density in the canopy |
| **P1** | Space colonisation for branching | Runions 2007, §2.3 | Organic branch + leaf placement |
| **P1** | Spectral leaf colour model | Baranoski 2001, §7.2 | Physically-based colour variation |
| **P1** | Poisson disk gap-fill | Bridson 2007, §2.2 | Natural scatter for background detail |
| **P2** | Precomputed canopy BRDF | Bruneton 2012, §5.4 | View-dependent shading response |
| **P2** | Reaction-diffusion patterns | Turk 1991, §7.1 | Organic colour variation |
| **P2** | Texture-lobe compositing | Livny 2011, §5.1 | Multi-slot canopy blending |
| **P3** | Fourier leaf contours | Neto 2006, §1.2 | Species-accurate leaf matching |
| **P3** | Full BSSRDF | Wang 2005, §4.2 | Accurate SSS (probably overkill) |

### Quickest wins for iteration

1. **Gielis SDF** — swap the `max(d1, d2)` CSG lens for the full superformula.
   Immediate visual variety from different `m, n₁, n₂, n₃` per leaf.

2. **Edge SSS** — `transmittance = exp(-sdf_dist × absorption)`. Three lines
   of code, dramatic visual improvement. Leaf edges glow warm green-yellow.

3. **Runions venation** — run the auxin-based simulation at ~200 nodes per
   leaf. Rasterise as anti-aliased lines. 50 lines of C# code produces
   venation that took hundreds of lines of shader math to approximate.

4. **Full-depth AO** — just extend the existing self-shadow loop to all depths
   and all slots. No new algorithm needed — remove the `SELF_SHADOW_LAYERS`
   cap and use the actual Gielis SDF.

