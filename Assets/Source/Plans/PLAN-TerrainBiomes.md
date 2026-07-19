@page plan_terrain_biomes Terrain & Biomes

# Terrain & Biomes — scenario ground generator

> Replace the flat blue canvas with a seeded, biome-blended terrain seen from above —
> grass, water, sand, dirt, stone, lava — bound to the hex grid (each slot has a biome
> type that gameplay can query), reacting to the disturbance field (grass parts, water
> ripples), feeding the GI bounce, and gating what static actors may grow where.
> Planned 2026-07-12 from a verified inventory. **Execution not started.**

---

## Vision & constraints

- **Top-down ground plane**: the game is airborne; the terrain is the distant ground
  seen from high above. It scrolls with the scenario travel (ascend/descent) with a
  parallax factor \f$< 1\f$ — depth for free, and **biome transitions happen across levels**:
  the noise field is continuous along the ascent axis, so climbing reveals new biome
  bands naturally.
- **Seeded & reproducible**: one integer seed + level index → identical terrain.
  Deterministic generation only (no `UnityEngine.Random` state leakage).
- **Style match**: soft, painterly, blurry-edged (see the bushes/clouds) — biome
  boundaries are noise-warped and blended, never hard hex edges. Hexes are the *logic*
  granularity, not the *visual* one.
- **Performance doctrine (the clever optimization)**: all noise is baked, never
  evaluated per-pixel at runtime (the PuffCloud 5c lesson — tileable baked noise,
  `Tools > BalloonParty > Generate Cloud Noise Texture` pipeline exists). The biome
  composite is **baked once per level into an RT**; the per-frame cost is one quad
  sampling that RT + a detail texture + `_DisturbanceTex` for reactions. Budget frame:
  8.33 ms at 120 Hz on a fill-rate-sensitive game — the terrain replaces background
  pixels we already pay for, so the net new cost must stay near shader-sampling only.

## Verified inventory (2026-07-12)

- **Grid**: hex coordinates in `Slots/Grid/HexCoordinates.cs`; slot→world via the grid
  service's `IndexToWorldPosition(Vector2Int)` (used by balloons, trails, pressure).
  Slots are the biome granularity; board footprint + margin defines the bake area.
- **Sorting layers** (TagManager): `Default < Balloons < Projectile < PowerUps <
  GridActors < ScoreTrail < Sky`. Terrain renders on **Default** — under everything,
  above the camera clear.
- **Background today**: main camera solid clear color + UI frame — nothing to retire;
  the terrain quad simply covers it.
- **Disturbance field**: global `_DisturbanceTex` (R = density, 1 = calm, stamps dig
  toward 0; G/B = displacement, 0.5-biased), world-bounds mapped
  (`DisturbanceFieldCoordinates`), already consumed by PuffCloud and SpeckField — the
  terrain reaction shader samples the same global. Recovery comes free from the field's
  own diffusion/reform; the terrain never simulates anything.
- **GI**: `SceneCaptureService` captures configured layers into `_SceneCaptureTex`
  (alpha = coverage); the GI smear derives bounce color from captured RGB. Putting the
  terrain on a captured layer feeds biome-colored bounce (green over grass, orange over
  lava) with **zero new code** — tuning only. Chrome reflections likewise.
- **Static actor placement**: `PLAN-GridActorExpansion.md` 8.3 (procedural placement,
  blocked on content) is the future consumer of biome gating; today bushes/clusters
  place via configuration. The biome query must be a service that placement can consult
  *when 8.3 lands* — design the seam now, don't build placement logic here.
- **Pacing**: `LevelParameters`/`LevelPacingConfiguration` already model per-level-range
  parameters — the natural carrier for "which biome profile applies at this altitude".
  (File is under active edit for the balance feature — coordinate before touching.)
- **Scenario travel**: `ScenarioContentRoot` motion is already the reference for
  travel-reactive systems (SpeckField matches it, incl. teleport rejection) — the
  terrain parallax binds the same way.
- **In-flight synergy (not scope)**: SpeckField is gaining palette/heat tinting —
  biome-tinted ambient specks (pollen over grass, embers over lava) become a natural
  follow-up once terrain exposes "biome under this world position".
- **HDR coupling**: lava/emissive biomes are first-class HDR emitter candidates when
  `PLAN-HDRColorPipeline.md` Wave B lands — author biome emissive params HDR-ready
  (float intensity, not baked-into-LDR-color).

## Architecture

Three layers, MVC-conformant (model/service plain C#, view MonoBehaviour):

### 1. Data — `TerrainMapService` (plain C#, VContainer)
- Input: seed + level index + `BiomeProfile` (new config SO + read-only interface,
  registered like the other `I*Settings`).
- **Classification**: two independent baked-noise fields (elevation + moisture) sampled
  at hex centers → biome via profile thresholds (classic 2-axis biome matrix — gives
  natural adjacency for free: water→sand→grass→dirt→stone; lava only where the profile
  allows). Evaluated on CPU at level build over the slot array — the grid is tiny
  (~hundreds of slots), cost is nothing; determinism trivial.
- Output A: `ITerrainQuery` — `BiomeType GetBiome(Vector2Int slot)` +
  `BiomeType GetBiomeAtWorld(Vector2 world)` (for non-slot consumers like specks).
- Output B: a hex-resolution **index map** `Texture2D` (point-filtered, R8: biome index;
  board + 1-hex margin) uploaded for the bake.

### 2. Bake — one blit per level (GPU, amortizable)
A bake shader expands the index map into the **terrain composite RT** (moderate res,
~512–1024 wide, screen-aspect):
- Per pixel: find containing hex + neighbors from the index map, compute noise-warped
  hex distances (warp sampled from the baked tileable noise — no procedural noise),
  blend the 2–3 nearest biomes' albedo/params.
- Output channels: RGB = blended albedo (palette-driven per biome), A = packed biome
  params for the runtime shader (reaction type/strength — e.g. quantized biome id, so
  the runtime shader knows "this pixel is watery" without re-deriving).
- RT needs a depth-stencil if ever rendered by a camera — it is a **blit target**, so
  depthless is fine (the RenderGraph rule applies to camera outputs only; documented in
  Display/README.md).
- Re-bake on level start / seed change only. If the one-time bake ever spikes a frame,
  amortize into strips (the prewarm idiom) — measure first.

### 3. View — `TerrainView` (MonoBehaviour) + runtime shader
- One quad, sorting layer **Default**, sized to camera frustum + travel margin,
  parented/bound to `ScenarioContentRoot` with a `_parallaxFactor` (serialized).
- Runtime shader samples: composite RT + detail noise (tileable, world-space UVs like
  PuffCloud, so detail never swims during travel) + `_DisturbanceTex`. The per-biome
  reaction and material techniques are a design space, not a single answer — see
  **Shader design — options per stage** below; final picks are made by eye via the
  spike protocol at A3/B2.
- Reaction params come from the bake's packed data channels — one shader, no per-biome
  materials, no branching beyond a biome-id selector on cheap math.
- **Opaque queue**: the terrain fully covers its pixels — render opaque (no blending)
  to save bandwidth under the fully-transparent 2D stack above it. Verify batching/
  sorting interaction in Frame Debugger (2D Renderer + opaque geometry — the URP
  migration taught us to verify, not assume).

## Shader design — options per stage

The visual calls below can't be settled on paper — each stage lists its candidate
techniques with tradeoffs, a recommendation, and what the in-editor **spike** must
prove. Spike protocol: shader variants behind `#pragma multi_compile`-style keywords
(or just material swaps), toggled live in play mode with José's eye as judge; losers
deleted, never shipped dormant.

### S1 — Zone blending (bake-time)

How two biomes meet is the single biggest style lever.

| Option | How | Tradeoffs |
|---|---|---|
| **(a) Cross-fade** | smoothstep over noise-warped hex distance | Simplest; risks "airbrushed mush" between contrasting biomes (grass→lava) |
| **(b) Height blending** | each biome's detail texture carries a "height" channel; blend weight is modulated by height so high features (grass tufts, stones) interleave INTO the neighbor instead of fading | The classic splat-map trick; organic interlocking edges; costs one extra channel per detail texture, still bake-time-only |
| **(c) Dithered/stochastic** | blue-noise threshold between biomes | Cheapest; reads as pixel grit — likely fights the soft painterly style, keep only as a fallback |
| **(d) Boundary SDF** | bake **signed distance to the biome boundary** into a data channel | Not a blend by itself — an *enabler*: shoreline foam bands, wet-sand darkening, lava crust edges, edge AO all become one-tap lookups at runtime. Strongly recommended **in addition to** (a) or (b) |

**Recommendation**: (b) + (d). Domain-warp the hex distance with the baked tileable
noise *before* blending (warp amplitude per biome pair — water wants smooth coasts,
grass→dirt wants ragged). **Spike SP-1 (at A3)**: (a) vs (b) on a grass/sand/water
test seed, judged at game zoom.

### S2 — Grass (the flagship reaction)

Top-down 2D "grass" is a material illusion — no geometry, no shells.

| Option | How | Tradeoffs |
|---|---|---|
| **(a) Tinted noise** | detail noise × palette green | Baseline/fallback; flat, no identity |
| **(b) Stroke-clump texture** | hand-authored (or tool-baked) tileable texture of top-down blade clumps, palette-tinted | The painterly look lives here; one texture; matches the bush foliage art language |
| **(c) Two-layer offset depth** | dark under-layer + bright top-layer sampled with a small UV offset; offset vector = wind + disturbance displacement | Fake parallax depth for one extra tap; the *offset* is what makes blades "lean" convincingly |
| **(d) Flow-map anisotropy** | per-pixel stroke direction from a baked flow field | Prettiest wind, most authoring; overkill unless (b)+(c) reads flat |

**Recommendation**: (b) + (c). The disturbance reaction is then *shear*: displace the
top layer's UVs along the field's displacement vector (G/B channels) scaled by density
deficit — blades part around a wake and recover as the field reforms (recovery is free,
it IS the field's reform). Idle wind: scroll a large-scale gust band (low-frequency
noise) through the offset vector — **coordinate direction/speed with the bush wind so
the world shares one wind**. **Spike SP-2 (at B2)**: (b)+(c) against (a), plus gust
sync with bushes on screen.

**Interaction fidelity ladder** (industry consensus for mobile is exactly our
architecture — a world-space "trample/bend" RT that the grass shader samples; the
disturbance field already is that, globally bound and stamped by gameplay):
1. **Bend** (base, free): the shear above. Monotonic recovery from field reform.
2. **Overshoot wiggle** (cheap, in-shader): real grass springs *past* rest and damps.
   The field only reforms monotonically — fake the overshoot by sampling the
   displacement at two temporal lags (current + shader-clock-delayed strength) and
   taking a damped difference. Two ALU-cheap ops, no new resources; judge in SP-2.
3. **N-layer shells, top-down variant** (medium): generalize the two-layer offset to
   3–4 layers, each offset progressively further along the lean vector with
   per-layer darkening — under an ortho top-down camera this reads as dense blade
   depth precisely when grass leans (wind/wake), which is when eyes are on it.
   +1 tap per layer over a *limited* reaction radius (branch on displacement
   magnitude); cap at 4 layers total.
4. **Persistent trample RT** (opt-in, shares the sand footprint decision): a tiny
   ping-pong RT accumulating bend with slow decay — paths stay flattened for seconds.
   Same infrastructure as the field/footprints; only if the game reads better with
   memory of where things flew.

**Mobile rule (from the field's consensus, encode as a hard constraint): no
alpha-tested grass anywhere** — alpha clip is a tile-GPU killer (reported ~2× frame
cost vs non-clipped); everything above is opaque/blended sampling, which is why this
design stays cheap.

### S3 — Water

| Option | How | Tradeoffs |
|---|---|---|
| **(a) Dual scrolling noise** | two detail-noise layers scrolled at different speeds/scales, combined into a pseudo-normal; \f$\text{glint} = \text{pseudo-normal} \cdot \text{light dir}\f$ | Two taps; the standard cheap water; light dir MUST be the GI/PuffCloud `_lightDirection` convention so the world shares one sun |
| **(b) Baked Voronoi sparkle** | scrolled pre-baked Voronoi ridge texture as caustic glints | One tap, very stylized; can layer on (a) |
| **(c) Ripples from density gradient** | band-pass the disturbance density deficit → expanding rings as the field diffuses; 2 extra taps for the gradient | Free propagation (the field already diffuses outward); rings are soft and painterly — likely enough |
| **(d) True wave sim** | a second small ping-pong RT running the wave equation, stamped by the same disturbance events | **The infrastructure already exists** — `DisturbanceFieldResources`' ping-pong blit is structurally a wave-solver scaffold; a wave kernel + dedicated stamp hook gives real interference/reflection ripples. Cost: one more low-res RT + blit per tick. Gorgeous, but build only if (c) reads flat |
| Shoreline | boundary-SDF band (S1-d): foam stripe + wet-sand darkening on the sand side | One tap; sells water more than ripples do — prioritize it |

**Recommendation**: (a) + (c) + shoreline first; (d) as a flagged stretch task.
**Spike SP-3 (at B2)**: (c) vs (d-prototype) on a water-heavy seed — decide if the
wave sim earns its RT.

### S4 — Sand / dirt / stone / lava

- **Sand**: granular high-frequency detail; reaction options: *(a)* instantaneous
  darkening by density deficit (cheap, recovers with the field) vs *(b)* a persistent
  **footprint RT** (tiny ping-pong accumulating disturbance with very slow decay —
  projectile tracks that linger in sand). (b) is charming and cheap (same pattern as
  the field itself) but is a new resource — build behind the same decision gate as the
  wave sim: only if (a) feels dead. Dirt = (a) with lower strength.
- **Stone**: static detail + baked edge-AO from the boundary SDF. No reaction — the
  contrast makes grass/water read as alive.
- **Moss (derived overlay, NOT a biome)**: moss is where the design gets procedural
  depth for free — compute a bake-time moss mask from data we already have:
  `moisture high` AND (`boundary SDF small` — growth creeps along transitions — OR
  near static actors, approximated by stamping bush/cluster slot positions into the
  mask). Render as a tinted stroke-clump layer (same texture family as grass) over
  whatever biome it lands on: mossy stone, mossy dirt, moss-edged water — one mask,
  many looks. Costs one bake channel + one runtime tap *only where the mask is hot*.
  Optional flourish: moss darkens/saturates with moisture (macro-noise modulated), so
  no two patches read identical.
- **Lava**: crust = thresholded detail noise (dark crust plates over bright gaps);
  slow domain-scroll for flow; boundary SDF → cooled-crust rim. Reaction: agitation
  boosts emissive intensity (authored as a float, HDR-ready for
  `PLAN-HDRColorPipeline.md` B3 — under LDR it clamps, under HDR it blooms). Idle
  pulse via the self-derived shader clock idiom (no MPB churn).

### S5 — Data layout (bake outputs)

| Option | Layout | Tradeoffs |
|---|---|---|
| (a) Single RGBA8 | RGB albedo + A packs biome id (high bits) & reaction strength (low bits) | One tap; packing/unpacking is fiddly and id quantization caps biome count & param resolution |
| **(b) Two RTs** | RGBA8 composite (RGB albedo, A = blend/AO) + RG8 data map (R = dominant biome id, G = boundary SDF) | Two taps (identical bandwidth class at these resolutions); every technique above that wants the SDF or clean ids gets them without bit tricks |

**Recommendation**: (b). Resolution spike at A-gate: 512 vs 1024 wide — the soft
style may make 512 free money.

### S6 — Cheap density & anti-repetition (mobile)

Fixed ortho zoom means tiling repetition is *the* density-killer — the eye finds the
pattern in seconds. The ladder, cheapest first (stack until the repetition dies, stop
there):

| Technique | Cost | What it buys |
|---|---|---|
| **Per-cell hash bombing** | ~free (one hash, conditional UV flip/rotate per virtual cell) | Kills the grid-aligned repeat of any tileable detail — the GPU Gems "texture bombing" trick reduced to flips/rotations; do this unconditionally |
| **Macro variation map** | 1 low-frequency tap (reuse the baked tileable noise at a big world scale) | Modulates tint/brightness/detail-scale across meters — breaks the "same green everywhere" flatness; also drives moss saturation and grass gust response variation |
| **Channel-packed detail atlas** | 1 tap serves 4 biomes (grass/sand/dirt/stone grayscale details in RGBA of ONE texture, palette-tinted per biome) | Bandwidth win AND a texture-slot win; ASTC-compress; blend regions read two channels of the same tap |
| **Blue-noise scatter stamps** | 1 tap into a scatter atlas, hash-gated per cell | Flowers on grass, pebbles on dirt, shells on sand — sparse sprinkles that read as authored density; gate by biome id + moisture so meadows cluster naturally |
| **Hex-tiling (Mikkelsen)** | 3 taps per texture in blend regions | The heavyweight anti-repetition — analytically-weighted 3-sample blend with per-tile hash transforms; reserve for the ONE surface where cheaper rungs fail (water sparkle is the likely candidate) |
| **Histogram-preserving stochastic (Heitz/Deliot)** | 3 taps + LUT + gaussian-domain transform | The full-fat version (Unity Labs ships a reference implementation); almost certainly overkill for a soft painterly style at fixed zoom — listed so the option is known, not planned |
| **Bicubic composite upsample** | 4-tap B-spline read of the composite RT | Makes a 512-wide composite look smooth at screen res — likely lets us halve the bake resolution; pairs with the S5 resolution spike |

Precision hygiene throughout: `half` for all color/detail math (the URP shaders here
are unlit and LDR), world-space UVs so nothing swims during travel.

References: Heitz/Deliot procedural stochastic texturing
([Unity blog](https://unity.com/blog/engine-platform/procedural-stochastic-texturing-in-unity),
[Unity Labs implementation](https://github.com/UnityLabs/procedural-stochastic-texturing)),
Mikkelsen hex-tiling ([demo/paper](https://github.com/mmikk/hextile-demo)),
texture bombing (GPU Gems ch. 20).

### S7 — Runtime composition rules

- **One sun**: light direction is the existing `_lightDirection` convention (GI +
  PuffCloud) — terrain glints/shading must read from the same serialized value or a
  shared global, never a second authored direction.
- **One wind**: gust phase/direction shared with the bush wind parameters.
- The GI overlay already composites over the terrain (it's below the overlay in
  sorting) — terrain needs no lighting of its own beyond flavor; do NOT add a second
  lighting model.
- Opaque queue for the terrain quad (it fully covers its pixels; saves blend
  bandwidth under a fully-transparent stack) — verify 2D Renderer opaque-pass ordering
  in the Frame Debugger before relying on it (URP migration rule: verify, don't
  assume).
- Reaction budget: \f$\le 4\f$ extra taps in the common path (disturbance + gradient pair +
  detail); wave-sim/footprint RTs are opt-in extras with their own device measurement.

## Task plan

Sizes: S ≤ half a day · M ≈ 1–2 days · L ≈ 3+ days. Every wave ends with a José
in-editor gate. Agent assignments follow the established split: opus for
quality-critical/subtle work, sonnet for well-specified implementation, haiku for
mechanical tasks; Fable investigates handoffs, reviews, commits.

### Dependency graph

```
A1 data service (opus) ─▶ A2 index-map upload (sonnet) ─▶ A3 bake blit (opus, shader) ─▶ A-gate (José: bake looks right in Render Maps)
                                                                 │
                              B1 view + runtime shader v1 (sonnet) ─▶ B2 reactions (opus, shader) ─▶ B3 GI feed (sonnet) ─▶ B-gate (José: style + device perf)
                                                                 │
        C1 placement gating seam (sonnet) ◀──────────────────────┤   (needs only A1)
        C2 seed plumbing + debug (haiku) ◀───────────────────────┤   (needs only A1)
        C3 Render Maps registry entry (sonnet) ◀─────────────────┘   (any time after A3)
```

### Wave A — data + bake

#### A1 — `TerrainMapService` + `BiomeProfile` config + `ITerrainQuery` · **P0 · M · opus**
Pure C#: seeded two-noise classification over slots, profile SO (+ read-only interface,
registered in scope), both query methods, and **EditMode tests** (determinism: same
seed → same map; adjacency sanity: no lava-touching-water unless profile allows;
threshold coverage). Opus — the classification quality and the profile's expressiveness
shape everything downstream. Noise source: sample the existing baked tileable noise
texture data on CPU (import readable copy or bake a float array via the generator
tool) — do NOT hand-roll simplex; determinism and visual continuity with the bake
depend on both stages reading the same fields. Fable pre-investigates the noise-data
access path before dispatch.

#### A2 — Index-map build/upload · **P0 · S · sonnet**
`Texture2D` R8 point-filtered from the service's slot array (+ margin ring extrapolated
from nearest slot), rebuilt on level start; lifecycle owned by the service's disposal.

#### A3 — Bake blit shader + `TerrainBaker` · **P0 · M · opus**
The composite bake per shader-design S1 + S5: noise-warped hex-distance blending into
the two-RT layout (composite RGBA8 + id/SDF data map). Ships **spike SP-1**
(cross-fade vs height-blend, keyword-switched) for the A-gate decision. Shader work is
in-editor-verified only — José's A-gate: inspect both RTs in the Game Render Maps
window (add via C3 or the custom slot), pick the blend style, delete the loser. Blend
width, warp amplitude per biome pair, and per-biome albedo exposed on `BiomeProfile`
for live tuning; include the 512-vs-1024 resolution check here.

**A-gate (José)**: composite for a handful of seeds reads as natural, blended,
style-matched ground. Iterate A3 knobs before any view work.

### Wave B — view + reactions + GI

#### B1 — `TerrainView` + runtime shader v1 · **P0 · M · sonnet**
Quad on Default layer, frustum+margin sizing, parallax binding to `ScenarioContentRoot`
(mirror SpeckField's motion sampling incl. teleport rejection), opaque queue, composite
+ world-space detail sampling. No reactions yet. José gate: in-game look vs the
screenshots' soft style; travel/parallax feel during ascend + restart descent.

#### B2 — Disturbance reactions · **P1 · M–L · opus**
The per-biome reaction block per shader-design S2–S4 recommendations: grass
stroke+offset shear, water dual-noise + gradient rings + SDF shoreline, sand
darkening, lava emissive pulse, stone inert. Ships **spikes SP-2** (grass technique
A/B + bush wind sync) and **SP-3** (gradient rings vs wave-sim prototype — the wave
sim reuses the disturbance ping-pong pattern; build the prototype minimal and be
ready to delete it). This is the showpiece and the most tuning-heavy task — every
constant a material property so José live-tunes in play mode. Device measurement
closes it: \f$\le 4\f$ extra taps in the common path; wave-sim/footprint RTs only survive
with their own pacing numbers.

#### B3 — GI integration · **P1 · S · sonnet**
Put the terrain on a captured layer (config change + docs), re-tune `_BounceStrength`/
shadow tint so biome bounce reads without overpowering (lava orange on balloon
undersides = the money shot). Verify in Game Render Maps (capture alpha semantics
unchanged — terrain is opaque, so coverage = 1 under the board; check the GI ground
shadow still behaves).

**B-gate (José)**: full style pass + device build — pacing sample via the adb loop,
overdraw check in Frame Debugger dump vs a pre-terrain baseline capture (take one at
A-gate time).

### Wave C — gameplay + tooling (parallel after A1)

#### C1 — Placement gating seam · **P1 · S–M · sonnet**
`BiomeProfile` gains per-biome allowed-content flags (bush, cluster, future actor
archetypes); expose a query helper on `ITerrainQuery`. Wire into today's placement
path only where it exists (bush/cluster spawn points); leave 8.3's procedural
placement to `PLAN-GridActorExpansion.md` — this task builds the seam it will consume.
Coordinate with the in-flight balance work before touching shared files.

#### C2 — Seed plumbing + reproduction affordance · **P2 · S · haiku**
Seed source (level config or run-random), logged at level start
(`[TerrainMap] seed=… level=…`), settable via a cheat/debug entry for reproduction.
Mechanical once A1 defines the seam.

#### C3 — Game Render Maps entries · **P2 · S · sonnet**
Register the index map + composite RT in the Render Maps window with channel tooltips
(R = biome index / packed params). Tooling continuity — this is how A-gate and B-gate
inspect the pipeline.

## Open questions (answer at execution time)

1. **Biome set + matrix**: final list and the elevation/moisture matrix — José authors
   in `BiomeProfile` during A-gate iteration; the plan assumes
   grass/water/sand/dirt/stone/lava.
2. **Coverage**: full-frustum ground vs board-footprint-with-sky-margin — decide at B1
   against the frame/vignette styling in the current UI.
3. **Biome-per-altitude**: whether `LevelParameters` ranges carry a biome-profile
   reference (biomes change as you climb) or one profile spans the run with noise-only
   variation — decide with the pacing file's owner (it's under active edit).
4. **Water spec**: settled by spike SP-3 (gradient rings vs wave-sim), not on paper —
   the shoreline SDF band ships regardless (it sells water more than ripples do).
5. **Speck/biome tinting**: follow-up once both this and the speck palette work land —
   `GetBiomeAtWorld` feeds the speck compute's palette selection.
