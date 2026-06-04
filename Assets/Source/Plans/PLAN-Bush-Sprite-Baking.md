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

## Implementation — detailed phase guides

> **For AI assistant sessions:** Each phase below contains a **Session
> context** block with the codebase knowledge needed to produce correct code
> without re-reading every file. Read the project conventions in
> `.github/copilot-instructions.md` first, then the session context for the
> target phase.

### Project conventions (quick reference for AI sessions)

- **Architecture:** MVC. Model = plain C#, View = MonoBehaviour, Controller = plain C# with VContainer `IStartable`/`ITickable`.
- **DI:** VContainer. Controllers are `[Inject]`-constructed, registered in lifecycle scopes.
- **Reactive:** UniRx (`Subscribe`, `AddTo`), MessagePipe for pub/sub.
- **Async:** UniTask (no coroutines). **Tweens:** DOTween.
- **Formatting:** Allman braces, braces always required, `private` default, `internal` over `public`.
- **Fields order:** `const` → `static readonly` → `[SerializeField]` → `[Inject]` → `readonly` → mutable.
- **Namespaces** match folder structure (e.g. `BalloonParty.Slots.Actor.Archetype`).
- **Pooling:** `PoolChannel<T>` base, consumer calls `Return()`. Pooled objects use `CompositeDisposable`. `OnDespawned()` must kill tweens.
- **Config:** inject read-only interface (e.g. `IBushSettings`), never concrete SO.

### Key existing files

| File | Path | Role |
|---|---|---|
| `ClusterView` | `Source/Slots/Actor/Cluster/ClusterView.cs` | Abstract MonoBehaviour. Pushes `_SlotCentersWorld[]`, `_SlotCount`, `_TimeOffset` via MPB. Has `Configure(Vector4[], int, Rect, IClusterViewSettings)`, `OnConfigured(MPB)`, `OnUpdateBlock(MPB)`, `Clear()`. Protected: `SlotCentersBuffer`, `SlotCount`, `Renderer`. |
| `ClusterViewController<TModel,TView,TSettings>` | `Source/Slots/Actor/Cluster/ClusterViewController.cs` | Generic controller. `IStartable`, `IDisposable`. Instantiates one `TView` prefab. Subscribes to `_registry.OnClusterChanged` → `Reconfigure()`. `Reconfigure` calls `PopulatePositions` → computes bounds → calls `_view.Configure(...)`. `_positionsBuffer` is `Vector4[16]`. |
| `BushView` | `Source/Slots/Actor/Archetype/BushView.cs` | `ClusterView` subclass. Has `PhyllotaxisCenter(center, baseRadius, hash, depth, spread) → Vector2`. Has `ComputeBranchSegments` for SDF branch prebake (will be removed). |
| `BushViewController` | `Source/Slots/Actor/Archetype/BushViewController.cs` | `ClusterViewController<BushObstacleModel, BushView, IBushSettings>`. Overrides `PopulatePositions` to add gap-fill midpoints (`.w = 0.65`) between adjacent bush slots. |
| `IBushSettings` | `Source/Configuration/IBushSettings.cs` | Interface: `IClusterViewSettings` + `BushView BushPrefab { get; }`. |
| `MathUtils` | `Source/Shared/MathUtils.cs` | `GoldenAngle = 2.39996323f`, `TwoPi`, `Frac(float)`. |
| `IPoolable` | `Source/Shared/Pool/IPoolable.cs` | `OnSpawned()`, `OnDespawned()`. |
| `PoolChannel<T>` | `Source/Shared/Pool/PoolChannel.cs` | Abstract. `Get()`, `Return(T)`. Subclass implements `Create()`. |
| `PoolManager` | `Source/Shared/Pool/PoolManager.cs` | Registry keyed by string. `Register`, `Get<T>(key)`, `Return(key, item)`. |
| `Bush.shader` | `Shaders/BalloonParty/Grid/Bush.shader` | Current real-time SDF shader. ~607 lines. Has `PhyllotaxisLeaf`, `LeafSDF` (CSG circle-cut lens), `CapsuleSDF`, `CanopySDF`. Uses `_SlotCentersWorld[16]`, `_SlotCount`, `_BranchSegments[80]`, `_BranchCount`. Painter's algorithm, self-shadow, lateral veins, dome shading, specular highlight. |
| `DisturbanceFieldService` | `Source/Shared/Disturbance/DisturbanceFieldService.cs` | `IStartable`, `ITickable`. Owns RT pair. `Stamp(worldPos, radius, strength, direction, duration)`. Pushes global `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize`. |

---

### Phase 0 — Enhanced baking shader (`BushBake.shader`)

**Session context:** You are creating a new offline-only shader
`BushBake.shader` in `Assets/Shaders/BalloonParty/Grid/`. It will be used by
an editor baking tool (Phase 1-2) to render bush canopies into
RenderTextures, NOT assigned to any runtime material. The existing real-time
`Bush.shader` (same folder, ~607 lines) stays untouched during this phase.

The current `Bush.shader` uses a simplified Gielis via CSG — `max(d1, d2)` of
two offset circles. You are replacing this with the full Gielis superformula
`r(θ) = (|cos(mθ/4)/a|^n₂ + |sin(mθ/4)/b|^n₃)^(-1/n₁)` which requires
`atan2` and `pow` (too expensive for real-time, free for baking).

The shader receives slot positions via `_SlotCentersWorld[16]` / `_SlotCount`
(same MPB contract as `ClusterView`). The phyllotaxis placement in HLSL
(`PhyllotaxisLeaf`) uses `GOLDEN_ANGLE = 2.39996323`, `LEAF_COUNT = 16`,
`depth 0 = outermost`. Copy the existing painter's algorithm loop structure
from `Bush.shader` as the starting point.

SSS uses Beer-Lambert: `transmittance = exp(-thickness × absorption)` where
thickness comes from the SDF distance field. Self-shadow removes the
`SELF_SHADOW_LAYERS = 4` cap and uses `GielisSDF` instead of circle checks.
AO counts all overlapping leaves above the current pixel.

Also create `LeafVenationSimulator.cs` in `Assets/Source/Editor/Bush/` — a
pure C# CPU simulation (Runions 2005 auxin model) that outputs vein line
segments. This is editor-only (`BalloonParty.Editor` assembly).

**Goal:** a single shader that, when rendered to a RenderTexture, produces a
high-quality canopy image with Gielis leaf shapes, SSS, full-depth shadows,
AO, and hierarchical veins.

#### Files

| File | Location | Type |
|---|---|---|
| `BushBake.shader` | `Assets/Shaders/BalloonParty/Grid/` | New shader |
| `BushBakeLeaf.shader` | `Assets/Shaders/BalloonParty/Grid/` | New shader (single-leaf mode) |
| `GielisSDF.cginc` | `Assets/Shaders/BalloonParty/Grid/` | New include — Gielis functions |

#### B0a — Gielis superformula SDF

Start from `Bush.shader`'s `LeafSDF` function. Replace the CSG lens with:

```hlsl
// GielisSDF.cginc
float GielisRadius(float theta, float m, float n1, float n2, float n3)
{
    float t = m * theta * 0.25;
    float a = pow(abs(cos(t)), n2);
    float b = pow(abs(sin(t)), n3);
    return pow(a + b, -1.0 / n1);
}

float GielisSDF(float2 wp, float2 center, float radius,
                float2 leafDir, float m, float n1, float n2, float n3)
{
    float2 local = wp - center;
    float2 tang = float2(-leafDir.y, leafDir.x);
    float u = dot(local, leafDir);
    float v = dot(local, tang);

    float theta = atan2(v, u);
    float dist = length(local);
    float boundary = radius * GielisRadius(theta, m, n1, n2, n3);
    return dist - boundary;
}
```

Properties to add: `_GielisM`, `_GielisN1`, `_GielisN2`, `_GielisN3`.
Per-leaf variation via hash: `m_leaf = _GielisM + hash * 0.3`.

**Checkpoint:** render a single leaf with Gielis SDF; confirm varied shapes by
changing `m` and `n` sliders.

#### B0b — Subsurface scattering

After computing leaf colour and before self-shadow, add SSS:

```hlsl
float thickness = saturate(-d / (radius * 0.5)); // 0 at edge, 1 at centre
float transmittance = exp(-thickness * _SSSAbsorption);
float backLight = max(0, dot(-leafDir, _LightDir.xy));
circleColor = lerp(circleColor, _SSSColor.rgb,
                   transmittance * _SSSStrength * (0.3 + backLight * 0.7));
```

Properties: `_SSSAbsorption` (3.0), `_SSSStrength` (0.25), `_SSSColor`.

**Checkpoint:** leaf edges glow warm yellow-green. Centre stays dark green.

#### B0c — Full-depth self-shadow

Remove `SELF_SHADOW_LAYERS` cap. Replace circle check with actual `GielisSDF`.
Add multi-sample penumbra:

```hlsl
// 4-8 jittered offsets for soft shadow
static const float2 shadowJitter[8] = { /* Poisson disk samples */ };
for (int sj = 0; sj < _ShadowSamples; sj++)
{
    float2 jPos = shPos + shadowJitter[sj] * _ShadowJitterRadius;
    // ... evaluate GielisSDF at jPos for all upper leaves ...
}
shadowAmount /= _ShadowSamples;
```

**Checkpoint:** shadows are soft and contact-hardening (crisp near the casting
leaf, soft further away).

#### B0d — Ambient occlusion

After the painter's loop, for each visible pixel, count overlapping leaves
across ALL slots and ALL depths:

```hlsl
float aoCount = 0;
float aoTotal = 0;
for (int aoi = 0; aoi < _SlotCount; aoi++)
    for (int aod = coveringDepth + 1; aod < LEAF_COUNT; aod++)
        // GielisSDF at this pixel for leaf (aoi, aod)
        // if inside: aoCount++
        aoTotal++;
float ao = 1.0 - (aoCount / max(aoTotal, 1.0)) * _AOMul;
leafColor *= ao;
```

**Checkpoint:** canopy centre is darker than edges. Individual leaf clumps show
depth through AO darkening.

#### B0e — Runions venation (CPU simulation)

This is **not** in the shader — it's a C# CPU simulation that outputs vein
line segments. The baker rasterises these into the leaf texture.

| File | Location |
|---|---|
| `LeafVenationSimulator.cs` | `Assets/Source/Editor/Bush/` |

Algorithm (from Runions 2005):

```csharp
internal static class LeafVenationSimulator
{
    internal struct VeinNode
    {
        internal Vector2 Position;
        internal int Parent; // -1 for root
    }

    internal struct VeinSegment
    {
        internal Vector2 Start;
        internal Vector2 End;
        internal int Depth; // 0 = midrib, 1 = secondary, 2 = tertiary
    }

    internal static List<VeinSegment> Simulate(
        Vector2 leafSize, float dKill, float dAttract, int sourceCount,
        int maxIterations, uint seed)
    {
        // 1. Place midrib node at (0, -halfHeight) to (0, +halfHeight)
        // 2. Scatter sourceCount auxin sources inside the leaf boundary
        // 3. For each iteration:
        //    a. Associate each source with nearest vein node (within dAttract)
        //    b. Average direction from each node toward its sources
        //    c. Grow node in that direction (fixed step size)
        //    d. Kill sources within dKill of any node
        // 4. Output vein segments with depth = distance from midrib root
    }
}
```

Parameters: `dKill = 0.02`, `dAttract = 0.08`, `sourceCount = 150`,
`maxIterations = 100`.

**Checkpoint:** visualise vein network in a debug Gizmo. Veins branch
naturally from midrib outward with 2-3 levels of hierarchy.

#### B0f — Per-leaf colour variation

In the shading block, after base colour computation:

```hlsl
// Hue jitter — rotate hue by ±_HueJitter degrees per leaf
float hueShift = (hash - 0.5) * 2.0 * _HueJitter / 360.0;
circleColor = HueRotate(circleColor, hueShift);

// Age gradient — outer leaves yellower
circleColor = lerp(circleColor, _AgeColor.rgb, depthT * _AgeStrength);

// Edge browning — SDF distance drives brown tint
float edgeBrown = smoothstep(0.0, _EdgeBrowningWidth, -d / radius);
circleColor = lerp(_BrowningColor.rgb, circleColor, edgeBrown);
```

**Checkpoint:** each leaf has slightly different green. Outer leaves tint
yellowish. Leaf tips show warm brown edges.

#### B0g — Enhanced ground shadow

Same as `CanopySDF` but using `GielisSDF` at all depths:

```hlsl
float BakeCanopySDF(float2 wp)
{
    float d = 999.0;
    for (int i = 0; i < _SlotCount; i++)
        for (int depth = 0; depth < LEAF_COUNT; depth++)
            d = min(d, GielisSDF(wp, cc, cr, ld, m, n1, n2, n3));
    return d;
}
```

Multi-sample offset averaging for soft penumbra (same jitter pattern as B0c).

**Checkpoint:** shadow silhouette shows individual Gielis leaf shapes with soft
edges. No sharp aliasing.

#### Phase 0 task checklist

```
B0a [x] GielisSDF.cginc + BushBake.shader with Gielis superformula
B0b [x] SSS pass — edge glow from Beer-Lambert transmittance
B0c [x] Full-depth self-shadow with multi-sample penumbra
B0d [x] Ambient occlusion from coverage accumulation
B0e [x] LeafVenationSimulator.cs — Runions auxin-based CPU simulation
B0f [x] Per-leaf colour variation — hue jitter, age, edge browning
B0g [x] Enhanced ground shadow — full-depth Gielis + soft penumbra
```

---

### Phase 1 — Leaf atlas baking (Editor tool)

**Session context:** Phase 0 created `BushBake.shader` (or `BushBakeLeaf.shader`)
in `Assets/Shaders/BalloonParty/Grid/` and `LeafVenationSimulator.cs` in
`Assets/Source/Editor/Bush/`. This phase builds the editor tooling that uses
them to bake individual leaf sprites into a packed atlas.

All files this phase creates go in `Assets/Source/Editor/Bush/` and belong to
the `BalloonParty.Editor` assembly (see `BalloonParty.Editor.asmdef` in
`Assets/Source/Editor/`). This assembly has access to `UnityEditor` APIs.
Follow the existing `TextureAuditWindow.cs` and `GradientTextureDrawer.cs`
patterns for editor window and texture baking conventions.

The baker creates a temporary scene (Camera + SpriteRenderer + material) to
render via `Camera.Render()` into a `RenderTexture`, then `ReadPixels` to
`Texture2D`. Output textures use premultiplied alpha to avoid dark edge
fringing. The leaf atlas is saved as a PNG and reimported as `Sprite`
(Multiple) with auto-slicing.

**Goal:** an EditorWindow that renders N leaf variants into a packed sprite
atlas asset, ready for runtime use.

#### Files

| File | Location | Type |
|---|---|---|
| `BushLeafBaker.cs` | `Assets/Source/Editor/Bush/` | Static utility — renders one leaf to Texture2D |
| `LeafAtlasPacker.cs` | `Assets/Source/Editor/Bush/` | Packs multiple leaves into a sprite atlas |
| `BushBakerWindow.cs` | `Assets/Source/Editor/Bush/` | EditorWindow — UI for previewing and exporting |

#### B1 — BushLeafBaker

```csharp
namespace BalloonParty.Editor.Bush
{
    internal static class BushLeafBaker
    {
        /// <summary>
        /// Renders a single leaf into a Texture2D using BushBakeLeaf.shader.
        /// </summary>
        internal static Texture2D BakeLeaf(
            BushLeafBakeSettings settings, int variantIndex, uint seed)
        {
            // 1. Create temporary RenderTexture (settings.Resolution × settings.Resolution)
            // 2. Create temporary Camera (orthographic, sized to leaf bounds)
            // 3. Create temporary GameObject with SpriteRenderer + BushBakeLeaf material
            // 4. Set Gielis params from settings + per-variant hash jitter
            // 5. Run LeafVenationSimulator → rasterise veins into a vein texture
            // 6. Assign vein texture to material (_VeinTex)
            // 7. Camera.Render() into RT
            // 8. ReadPixels → Texture2D (premultiplied alpha)
            // 9. Destroy temporaries
            // 10. Return Texture2D
        }
    }

    [Serializable]
    internal class BushLeafBakeSettings
    {
        internal int Resolution = 64;
        internal float GielisM = 2f;
        internal float GielisN1 = 1f;
        internal float GielisN2 = 1.5f;
        internal float GielisN3 = 1.5f;
        internal float SSSStrength = 0.25f;
        internal float HueJitter = 10f;
        internal int VeinSources = 150;
        internal int LeafVariants = 8;
    }
}
```

**Checkpoint:** call `BushLeafBaker.BakeLeaf()` from a test menu item. Inspect
the resulting Texture2D — it should show a single Gielis leaf with veins, SSS
edge glow, and dome shading baked in.

#### B2 — LeafAtlasPacker

```csharp
internal static class LeafAtlasPacker
{
    /// <summary>
    /// Bakes N leaf variants, packs them into a square atlas, creates
    /// Sprite[] sub-sprites, and saves the atlas as an asset.
    /// </summary>
    internal static (Texture2D atlas, Sprite[] sprites) Pack(
        BushLeafBakeSettings settings, string outputPath)
    {
        // 1. Bake settings.LeafVariants leaves via BushLeafBaker
        // 2. Compute grid layout (e.g. 4×2 for 8 variants in a 256×128 atlas)
        // 3. Blit each leaf into the atlas at its grid cell
        // 4. Create Sprite for each cell (pivot = centre)
        // 5. Save atlas as PNG at outputPath
        // 6. Import as Sprite (Multiple) with correct slice rects
        // 7. Return (atlas, sprites)
    }
}
```

**Checkpoint:** a single `.png` file in `Assets/Art/Bush/` containing 8 leaf
variants in a grid. Each variant has a `Sprite` reference accessible via the
atlas.

#### B3 — BushBakerWindow

Pattern: follows `TextureAuditWindow` structure (existing EditorWindow in
`Assets/Source/Editor/`).

```csharp
internal sealed class BushBakerWindow : EditorWindow
{
    [MenuItem("Tools/Bush Baker")]
    private static void Open() => GetWindow<BushBakerWindow>("Bush Baker");

    [SerializeField] private BushLeafBakeSettings _leafSettings = new();
    [SerializeField] private BushCanopyBakeSettings _canopySettings = new();
    [SerializeField] private string _outputFolder = "Assets/Art/Bush/Baked";

    // Preview textures (not serialized — regenerated on demand)
    private Texture2D[] _leafPreviews;
    private Texture2D[] _canopyPreviews;

    private void OnGUI()
    {
        // ── Leaf Atlas section ──
        // Gielis parameter sliders
        // Resolution dropdown
        // Variant count slider
        // "Preview Leaves" button → renders to _leafPreviews[]
        // Preview grid: draw each leaf in a horizontal row
        // "Export Leaf Atlas" button → LeafAtlasPacker.Pack()

        // ── Canopy section (Phase 2) ──
        // Slot count, seed range, variant count
        // "Preview Canopies" button
        // "Export Canopy Variants" button
    }
}
```

**Checkpoint:** open `Tools > Bush Baker`. Adjust Gielis sliders. Click
"Preview Leaves" — see 8 leaf variants rendered in the inspector. Click
"Export Leaf Atlas" — atlas PNG saved to disk.

#### Phase 1 task checklist

```
B1  [x] BushLeafBaker.cs — renders one leaf to Texture2D
B2  [x] LeafAtlasPacker.cs — packs N variants into atlas + Sprite[]
B3  [x] BushBakerWindow.cs — EditorWindow with preview + export
```

#### Dependencies

- Requires `BushBakeLeaf.shader` from Phase 0 (B0a, B0b, B0f).
- Requires `LeafVenationSimulator.cs` from Phase 0 (B0e).
- No runtime code changes. Editor-only assembly (`BalloonParty.Editor`).

---

### Phase 2 — Baked canopy sprite

**Session context:** Phase 1 created `BushLeafBaker.cs`, `LeafAtlasPacker.cs`,
and `BushBakerWindow.cs` in `Assets/Source/Editor/Bush/`. This phase adds
full-canopy baking (all slots, all 16 depth layers) and extends the runtime
configuration to reference baked assets.

The canopy baker renders the same `BushBake.shader` but with full slot
positions — same `_SlotCentersWorld[16]` / `_SlotCount` MPB contract as
`ClusterView.Configure()`. Each variant uses a different seed that rotates
the phyllotaxis spiral and jitters Gielis parameters per leaf.

`IBushSettings` (at `Source/Configuration/IBushSettings.cs`) currently only
has `BushView BushPrefab { get; }` extending `IClusterViewSettings` (which
has `AnimationSpeed`, `Padding`, `SortingLayerId`, `SortingOrderOffset`).
The concrete SO is `BushSettings.cs` in `Source/Configuration/`. Both need
new properties for baked sprite arrays, leaf atlas references, ruffle leaf
count, and ruffle radius. Remember: inject the interface, never the SO.

**Goal:** N canopy variant textures that capture the dense inner bush. The
editor window from Phase 1 gains a "Canopy" section.

#### Files

| File | Location | Type |
|---|---|---|
| `BushCanopyBaker.cs` | `Assets/Source/Editor/Bush/` | Renders full canopy to Texture2D |
| `BushCanopyBakeSettings` | (inner class of baker or shared) | Settings for canopy baking |
| `IBushSettings.cs` | `Assets/Source/Configuration/` | **Modified** — add baked asset refs |
| `BushSettings.cs` | `Assets/Source/Configuration/` | **Modified** — serialized fields for baked assets |

#### B4 — BushCanopyBaker

```csharp
internal static class BushCanopyBaker
{
    internal static Texture2D BakeCanopy(
        BushCanopyBakeSettings settings, uint seed)
    {
        // 1. Create RenderTexture (settings.Resolution × settings.Resolution)
        // 2. Create temporary scene: Camera + SpriteRenderer + BushBake material
        // 3. Generate slot positions from seed:
        //    - Single slot at origin (for 1-slot variant)
        //    - Or 3-5 slots in hex pattern (for multi-slot variant)
        // 4. Push slot positions + count via MPB (same as ClusterView.Configure)
        // 5. Camera.Render() → reads full canopy with all leaves, shadows, AO
        // 6. ReadPixels → Texture2D (premultiplied alpha)
        // 7. Trim transparent border (optional — reduce texture waste)
        // 8. Return Texture2D
    }
}
```

The canopy baker renders with `BushBake.shader` (Phase 0), which includes all
the quality upgrades. The result captures the **inner canopy** — the dense
layered centre that doesn't need per-leaf animation.

#### B5 — Variant system

Generate N variants by varying the seed. Each seed produces different:
- Phyllotaxis rotation (hash-based)
- Gielis parameter jitter per leaf
- Hue jitter per leaf
- Venation patterns

```csharp
internal static Texture2D[] BakeVariants(
    BushCanopyBakeSettings settings, int variantCount)
{
    var results = new Texture2D[variantCount];
    for (var i = 0; i < variantCount; i++)
    {
        results[i] = BakeCanopy(settings, seed: (uint)(i * 7919 + 31));
    }
    return results;
}
```

**Checkpoint:** the BushBakerWindow shows a grid of N canopy variants. Each
looks like a different bush — varied leaf shapes, rotations, colours.

#### B6 — BushSettings extension

```csharp
// IBushSettings.cs — add to the interface
internal interface IBushSettings : IClusterViewSettings
{
    BushView BushPrefab { get; }
    Sprite[] CanopyVariants { get; }     // baked canopy sprites
    Sprite[] LeafAtlasSprites { get; }   // individual leaf sprites from atlas
    int RuffleLeafCount { get; }         // how many outer leaves get sprites
    float RuffleRadius { get; }          // projectile distance for ruffle trigger
}
```

```csharp
// BushSettings.cs — add serialized fields
[Header("Baked Assets")]
[SerializeField] private Sprite[] _canopyVariants;
[SerializeField] private Sprite[] _leafAtlasSprites;

[Header("Ruffle")]
[SerializeField] private int _ruffleLeafCount = 6;
[SerializeField] private float _ruffleRadius = 1.5f;
```

**Checkpoint:** `BushSettings` SO in the inspector shows slots for baked
canopy variants and leaf atlas sprites. Drag-and-drop from exported assets.

#### Phase 2 task checklist

```
B4  [x] BushCanopyBaker.cs — renders full canopy to Texture2D
B5  [x] Variant system — N seeds → N canopy textures
B6  [x] IBushSettings + BushSettings — baked asset references + ruffle config
```

#### Dependencies

- Requires `BushBake.shader` from Phase 0 (full pipeline).
- Extends `BushBakerWindow` from Phase 1 (B3) with canopy section.
- `IBushSettings` change affects `BushSettings.cs` (concrete SO).

---

### Phase 3 — Runtime leaf sprites

**Session context:** Phase 2 extended `IBushSettings` / `BushSettings` with
`Sprite[] CanopyVariants`, `Sprite[] LeafAtlasSprites`, `int RuffleLeafCount`,
`float RuffleRadius`. Baked PNG assets exist in `Assets/Art/Bush/Baked/`.

This phase refactors the runtime `BushView` from a single SDF quad to a
layered sprite system. Key understanding of the current flow:

1. `ClusterViewController.Reconfigure()` calls `PopulatePositions(buffer)` →
   computes bounds → calls `_view.Configure(positions, count, bounds, settings)`.
2. `ClusterView.Configure` stores positions in `_slotCenters[16]`, sets
   transform position/scale to bounds, pushes MPB, then calls
   `OnConfigured(block)`.
3. `BushView.OnConfigured` currently computes branch segments for the SDF
   shader. This is what we're replacing.

The new `BushView.OnConfigured` (or a new `ConfigureSprites` method) should:
- For each slot in `SlotCentersBuffer` (accessible via `protected` property):
  - Spawn a canopy `SpriteRenderer` child with a variant sprite
  - For the outer `RuffleLeafCount` leaves (depth 0 to N-1): spawn
    `LeafSpriteView` from the pool at phyllotaxis positions
- Use the existing `PhyllotaxisCenter` static method (already in `BushView`)
  for leaf placement.

`LeafSpriteView` implements `IPoolable`. `OnDespawned()` MUST call
`DOTween.Kill(transform)` per project convention. The pool uses
`PoolChannel<LeafSpriteView>` registered in `PoolManager`.

Slot `.w` encodes: `> 0.99` = real slot, `> 0.001 && < 0.99` = gap-fill
(smaller canopy, no ruffle leaves), `<= 0.001` = treated as `w = 1.0`.

**Goal:** bushes render as baked canopy sprite + outer leaf sprites placed by
phyllotaxis, using standard `SpriteRenderer`s from a pool.

#### Key design decisions

- **Each slot** gets its own canopy sprite (not one quad for the whole cluster).
  The `ClusterViewController` infrastructure provides per-slot world positions.
- **Gap-fill slots** (`.w < 1`) get a smaller canopy sprite, no ruffle leaves.
- **Outer N leaves** (depth 0 to `RuffleLeafCount-1`) become individual leaf
  sprites. Inner leaves are part of the baked canopy.
- All leaf sprites come from the pool. Returned on `Clear()` / reconfigure.

#### Files

| File | Location | Type |
|---|---|---|
| `LeafSpriteView.cs` | `Assets/Source/Slots/Actor/Archetype/` | MonoBehaviour — SpriteRenderer + ruffle state |
| `LeafSpritePoolChannel.cs` | `Assets/Source/Slots/Actor/Archetype/` | Pool channel for leaf sprites |
| `BushView.cs` | `Assets/Source/Slots/Actor/Archetype/` | **Rewritten** — manages canopy + leaf sprites |
| `BushViewController.cs` | `Assets/Source/Slots/Actor/Archetype/` | **Modified** — passes settings for sprite setup |

#### B7 — LeafSpriteView

```csharp
namespace BalloonParty.Slots.Actor.Archetype
{
    [RequireComponent(typeof(SpriteRenderer))]
    internal class LeafSpriteView : MonoBehaviour, IPoolable
    {
        private SpriteRenderer _renderer;

        // Ruffle state — set by BushRuffleController
        internal int PhyllotaxisIndex { get; set; }
        internal float DepthFactor { get; set; } // 0 = inner, 1 = outermost
        internal Vector2 LeafDirection { get; set; }
        internal float BaseRotation { get; set; }

        public void OnSpawned()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = true;
        }

        public void OnDespawned()
        {
            DOTween.Kill(transform);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            _renderer.enabled = false;
        }
    }
}
```

#### B8 — LeafSpritePoolChannel

```csharp
internal class LeafSpritePoolChannel : PoolChannel<LeafSpriteView>
{
    private readonly LeafSpriteView _prefab;

    internal LeafSpritePoolChannel(LeafSpriteView prefab)
    {
        _prefab = prefab;
    }

    protected override LeafSpriteView Create()
    {
        return Object.Instantiate(_prefab);
    }
}
```

The prefab: an empty GameObject with `SpriteRenderer` + `LeafSpriteView`.
No sprite assigned — set at runtime from the atlas.

#### B9 — BushView refactor

The current `BushView` pushes `_BranchSegments` to the SDF shader. The new
version manages a canopy sprite + pooled leaf sprites per slot.

```csharp
internal class BushView : ClusterView
{
    [SerializeField] private LeafSpriteView _leafPrefab;

    private readonly List<SpriteRenderer> _canopyRenderers = new();
    private readonly List<LeafSpriteView> _leafSprites = new();

    // Injected at configure time
    private Sprite[] _canopyVariants;
    private Sprite[] _leafAtlasSprites;
    private int _ruffleLeafCount;

    internal IReadOnlyList<LeafSpriteView> LeafSprites => _leafSprites;

    internal void ConfigureSprites(
        IReadOnlyList<Vector4> slots, int slotCount,
        IBushSettings settings)
    {
        ClearSprites();
        _canopyVariants = settings.CanopyVariants;
        _leafAtlasSprites = settings.LeafAtlasSprites;
        _ruffleLeafCount = settings.RuffleLeafCount;

        for (var i = 0; i < slotCount; i++)
        {
            var slot = slots[i];
            var center = new Vector2(slot.x, slot.y);
            var isGap = slot.w > 0.001f && slot.w < 0.99f;
            var hash = MathUtils.Frac(
                Mathf.Sin(center.x * 127.1f + center.y * 311.7f) * 43758.5453f);

            // Canopy sprite — pick variant from hash
            SpawnCanopySprite(center, hash, isGap, sortBase: i);

            if (isGap) continue;

            // Outer leaf sprites — phyllotaxis placement
            var baseRadius = /* from material or settings */;
            for (var d = 0; d < _ruffleLeafCount; d++)
            {
                SpawnLeafSprite(center, baseRadius, hash, d, sortBase: i);
            }
        }
    }

    private void SpawnCanopySprite(Vector2 center, float hash, bool isGap, int sortBase)
    {
        // Create or reuse a child SpriteRenderer
        // Assign variant sprite: _canopyVariants[hash * variantCount]
        // Position at center
        // Scale based on isGap (smaller for gap fills)
    }

    private void SpawnLeafSprite(
        Vector2 center, float baseRadius, float hash, int depth, int sortBase)
    {
        // Get from pool (via injected PoolManager or direct channel)
        // Position using PhyllotaxisCenter (reused from BushView)
        // Assign random sprite from _leafAtlasSprites
        // Set sorting order = sortBase * 10 + depth
        // Set initial rotation = phyllotaxis angle
        // Store in _leafSprites list
    }

    internal void ClearSprites()
    {
        // Return all leaf sprites to pool
        // Destroy/disable canopy renderers
    }
}
```

#### B10 — Phyllotaxis placement

The existing `PhyllotaxisCenter` method in `BushView` is reused directly.
It already computes the world-space position for a leaf at a given depth and
slot. The leaf sprite is placed at this position with `transform.position`.

The phyllotaxis angle (direction from slot centre to leaf centre) becomes
the leaf sprite's initial Z rotation:

```csharp
var angle = fn * MathUtils.GoldenAngle + hash * MathUtils.TwoPi;
leafSprite.BaseRotation = angle * Mathf.Rad2Deg;
leafSprite.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
```

**Checkpoint:** bushes render with a baked canopy sprite in the centre and
individual leaf sprites around the edges. No animation yet — static. The
visual should roughly match the SDF shader output.

#### Phase 3 task checklist

```
B7  [x] LeafSpriteView.cs — MonoBehaviour + IPoolable + ruffle state
B8  [x] LeafSpritePoolChannel.cs — pool channel for leaf prefab
B9  [x] BushView.cs rewrite — manages canopy + leaf sprites per slot
B10 [x] Phyllotaxis placement — reuse PhyllotaxisCenter for leaf positions
```

#### Dependencies

- Requires baked assets from Phase 1 + 2 (leaf atlas + canopy sprites).
- Requires `IBushSettings` extension from Phase 2 (B6).
- `LeafSpriteView` prefab must be created manually in Unity (one-time).
- Pool registration in the VContainer lifecycle scope.

---

### Phase 4 — Ruffle animation

**Session context:** Phase 3 created `LeafSpriteView` (MonoBehaviour +
`IPoolable` in `Source/Slots/Actor/Archetype/`) with properties:
`PhyllotaxisIndex`, `DepthFactor` (0=inner, 1=outermost), `LeafDirection`,
`BaseRotation`. `BushView` spawns these at phyllotaxis positions and exposes
`IReadOnlyList<LeafSpriteView> LeafSprites`.

This phase creates `BushRuffleController` — a VContainer `ITickable` in
`Source/Slots/Actor/Archetype/`. It detects projectile proximity and drives
DOTween animations on each `LeafSpriteView.transform`.

**DOTween conventions in this project:**
- Use `DOTween.Kill(transform)` before starting new sequences on the same target.
- `SetId(transform)` on all tweens so `OnDespawned` can kill them.
- Use `Ease.OutElastic` or `Ease.OutBack` for natural spring-back.
- Project uses DOTween modules: `DOTweenModuleSprite`, `UniTask.DOTween`.

**Projectile data access:** `ProjectileView` stamps the `DisturbanceFieldService`
with position + direction. The controller can either:
(A) Sample `DisturbanceFieldService.FieldTexture` at the cluster position, or
(B) Subscribe to projectile position events via MessagePipe.
Option B is recommended — see `Source/Projectile/` for the event types.

**Wind:** continuous gentle oscillation uses `DOTween.To` with `LoopType.Yoyo`
and `Ease.InOutSine`. Per-leaf phase offset from `PhyllotaxisIndex` so leaves
don't sway in sync. Kill via `DOTween.Kill(transform)` in `OnDespawned`.

**Goal:** leaves shake and settle when a projectile flies past. Continuous
gentle wind sway at all times.

#### Files

| File | Location | Type |
|---|---|---|
| `BushRuffleController.cs` | `Assets/Source/Slots/Actor/Archetype/` | ITickable — detects proximity, drives tweens |
| `BushRuffleSettings` | in `IBushSettings` or own file | Config: intensity, radius, duration, wind params |

#### B11 — BushRuffleController

```csharp
namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Detects projectile proximity to bush clusters and drives per-leaf
    /// ruffle animation via DOTween. Also manages ambient wind oscillation.
    /// </summary>
    internal class BushRuffleController : ITickable
    {
        private readonly IBushSettings _settings;
        private readonly BushView _bushView; // or reference via controller
        // Projectile tracking: inject IReadOnlyList<ProjectileModel> or
        // subscribe to ProjectileMovedEvent via MessagePipe

        [Inject]
        internal BushRuffleController(IBushSettings settings, /* projectile source */)
        {
            _settings = settings;
        }

        void ITickable.Tick()
        {
            // For each active projectile:
            //   distance = |projectilePos - clusterCentre|
            //   if distance < _settings.RuffleRadius:
            //     intensity = 1 - (distance / _settings.RuffleRadius)
            //     direction = normalize(clusterCentre - projectilePos)
            //     TriggerRuffle(intensity, direction)
        }

        private void TriggerRuffle(float intensity, Vector2 direction)
        {
            var leaves = _bushView.LeafSprites;
            for (var i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                var delay = leaf.PhyllotaxisIndex * 0.03f; // stagger
                var amp = intensity * leaf.DepthFactor;
                RuffleLeaf(leaf, amp, direction, delay);
            }
        }

        private static void RuffleLeaf(
            LeafSpriteView leaf, float amplitude, Vector2 direction, float delay)
        {
            var t = leaf.transform;
            DOTween.Kill(t); // cancel any running ruffle

            // Rotation punch
            t.DOPunchRotation(
                new Vector3(0, 0, 15f * amplitude),
                duration: 0.4f, vibrato: 6)
                .SetDelay(delay)
                .SetEase(Ease.OutElastic);

            // Scale bounce
            t.DOPunchScale(
                Vector3.one * 0.1f * amplitude,
                duration: 0.3f, vibrato: 4)
                .SetDelay(delay);

            // Position offset (push away from projectile)
            var offset = (Vector3)(direction.normalized * 0.04f * amplitude);
            t.DOPunchPosition(offset, duration: 0.35f, vibrato: 5)
                .SetDelay(delay);
        }
    }
}
```

#### B12-B13 — Stagger system

Built into `TriggerRuffle` above. The delay per leaf is:

```csharp
// Distance-based stagger: leaves closer to projectile react first
var leafWorldPos = leaf.transform.position;
var distToProjectile = Vector2.Distance(leafWorldPos, projectilePos);
var delay = distToProjectile * 0.15f; // 0.15s per world unit of distance
```

This creates a radial wave — the impact point reacts instantly, leaves further
away react progressively later.

#### B14 — Ambient wind

A persistent slow animation on all leaf sprites, independent of ruffle:

```csharp
private void StartWind(LeafSpriteView leaf)
{
    var phase = leaf.PhyllotaxisIndex * 0.4f; // different phase per leaf
    var period = 2.0f + leaf.PhyllotaxisIndex * 0.1f; // slight period variation

    DOTween.To(
        () => 0f,
        angle =>
        {
            leaf.transform.localRotation = Quaternion.Euler(
                0, 0, leaf.BaseRotation + angle);
        },
        endValue: 3f, // ±3 degrees
        duration: period)
        .SetLoops(-1, LoopType.Yoyo)
        .SetEase(Ease.InOutSine)
        .SetDelay(phase)
        .SetId(leaf.transform); // kill with transform
}
```

Called once per leaf in `ConfigureSprites`. Killed in `OnDespawned`.

**Checkpoint:** leaves sway gently at all times. When a projectile passes
near the bush, leaves shake, bounce, and settle in a radial wave pattern.

#### Phase 4 task checklist

```
B11 [ ] BushRuffleController.cs — ITickable, projectile detection
B12 [ ] Per-leaf DOTween sequences — rotation, scale, position punch
B13 [ ] Stagger system — distance-based delay for wave effect
B14 [ ] Ambient wind — continuous gentle oscillation
```

#### Dependencies

- Requires `LeafSpriteView` with ruffle state (Phase 3, B7).
- Requires `BushView.LeafSprites` accessor (Phase 3, B9).
- Requires projectile position data — inject via MessagePipe event or
  direct `ProjectileView` query.
- DOTween already in the project.
- VContainer registration: `BushRuffleController` as `ITickable`.

---

### Phase 5 — Integration & cleanup

**Session context:** All prior phases are complete. The system has:
- `BushBake.shader` + `BushBakeLeaf.shader` (editor-only baking shaders)
- `BushBakerWindow` + `BushLeafBaker` + `BushCanopyBaker` (editor tools)
- `LeafVenationSimulator` (CPU vein generation)
- `LeafSpriteView` + `LeafSpritePoolChannel` (pooled leaf sprites)
- `BushView` (refactored: spawns canopy sprites + leaf sprites)
- `BushRuffleController` (projectile detection + DOTween animation)
- `IBushSettings` extended with `CanopyVariants`, `LeafAtlasSprites`,
  `RuffleLeafCount`, `RuffleRadius`

This phase connects everything and removes the old SDF runtime path.

**Key integration point:** `ClusterViewController.Reconfigure()` calls
`_view.Configure(positions, count, bounds, settings)` which calls
`ClusterView.Configure()` → `OnConfigured(block)`. The new `BushView` uses
`OnConfigured` to trigger `ConfigureSprites`. But `OnConfigured` receives a
`MaterialPropertyBlock` — the new sprite-based view doesn't need MPB. The
cleanest approach: `BushView.OnConfigured` ignores the MPB parameter and
calls `ConfigureSprites` using `SlotCentersBuffer` / `SlotCount` (both are
`protected` on `ClusterView`). The base canopy `SpriteRenderer` on the
prefab can use `Sprites/Default` — no custom shader needed at runtime.

The old `Bush.shader` moves to an editor-only folder or is excluded from
builds via shader variant stripping. `BushBake.shader` was already
editor-only.

**Goal:** bushes work seamlessly in the game. Old SDF shader is editor-only.

#### Files

| File | Change |
|---|---|
| `BushView.cs` | Remove old `OnConfigured` (MPB branch segments). New `ConfigureSprites` is the main path. |
| `BushViewController.cs` | Call `ConfigureSprites` in `Reconfigure` instead of the old `Configure` path |
| `ClusterViewController.cs` | May need a virtual hook for sprite-based views (or just override `Reconfigure` in `BushViewController`) |
| `Bush.shader` | Move to `Assets/Shaders/BalloonParty/Grid/Editor/` — excluded from builds |
| `BushBake.shader` | Same — editor-only |
| `BushSettings.cs` | Verify all baked asset references are assigned |

#### B15 — Remove Bush.shader from runtime

- Move `Bush.shader` and `BushBake.shader` to an Editor-only folder
  (or add them to a shader variant stripping list).
- Remove the Bush material from any runtime prefab.
- The `BushView` prefab no longer needs a `SpriteRenderer` with a material —
  it spawns child sprites instead.

#### B16 — Update BushViewController

The current `ClusterViewController` calls `_view.Configure(positions, count,
bounds, settings)`. The new `BushView` needs `ConfigureSprites`. Options:

**Option A:** override the controller's `Reconfigure` to call the new API.
**Option B:** have `BushView.OnConfigured` call `ConfigureSprites` internally.

Option B is cleaner — the base `ClusterView.Configure` flow stays intact,
and `BushView.OnConfigured` does the sprite setup using the slot positions
already stored in `SlotCentersBuffer`.

```csharp
// BushView.cs
protected override void OnConfigured(MaterialPropertyBlock block)
{
    ConfigureSprites(SlotCentersBuffer, SlotCount, _settings);
}
```

This requires `BushView` to hold a reference to `IBushSettings`. The
controller can pass it via a new `SetSettings(IBushSettings)` method called
before `Configure`, or via `[Inject]`.

#### B17 — Validation checklist

```
[ ] Single-slot bush: canopy + outer leaves render correctly
[ ] Multi-slot cluster: multiple canopy sprites + leaf sprites, no overlap issues
[ ] Gap-fill slots: smaller canopy, no leaf sprites
[ ] Ruffle: projectile fly-by triggers wave animation
[ ] Wind: ambient sway on all leaves
[ ] Pooling: leaves return to pool on Clear/reconfigure
[ ] Sorting: canopy behind leaves, leaves sorted by depth
[ ] Performance: profile on target device — draw calls, CPU tween cost
[ ] Memory: total texture memory for all variants + atlas
```

#### B18 — Puff cloud comparison

Visual side-by-side check:
- Puff clouds are volumetric noise (soft, translucent, cloud-like).
- Bushes are layered opaque leaf sprites (sharp edges, botanical detail).
- They should look like clearly different obstacle types.
- Sorting between Puff and Bush clusters must work correctly.

#### Phase 5 task checklist

```
B15 [ ] Move Bush.shader + BushBake.shader to editor-only path
B16 [ ] BushView.OnConfigured → ConfigureSprites bridge
B17 [ ] Full validation: single-slot, multi-slot, gap-fill, ruffle, wind, pool
B18 [ ] Side-by-side Puff comparison + sorting validation
```

#### Dependencies

- Requires all prior phases complete.
- Requires baked assets generated and assigned in `BushSettings` SO.
- Requires `LeafSpriteView` prefab created and assigned.

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

