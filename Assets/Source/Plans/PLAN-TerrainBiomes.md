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
  parallax factor < 1 — depth for free, and **biome transitions happen across levels**:
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
  PuffCloud, so detail never swims during travel) + `_DisturbanceTex`:
  - **Grass**: shear detail UVs along the displacement vector (G/B), strength × density
    deficit — blades "part" around a wake and recover as the field reforms.
  - **Water**: ripple = animated ring derived from density gradient (screen-cheap:
    two extra taps for a gradient, or a normal-from-density trick) + a light specular
    streak; subtle idle scroll on detail UVs.
  - **Sand/dirt**: brief darkening/scatter where disturbed (density deficit → albedo
    shift), slow recovery look for sand (lag the response with the field's reform).
  - **Lava**: emissive pulse on agitation (LDR-boost now, HDR emitter later); slow
    idle glow oscillation via the shader clock idiom (self-derived, no MPB churn).
  - **Stone**: no reaction — the contrast sells the others.
- Reaction params come from the composite's packed params channel — one shader, no
  per-biome materials, no branching beyond a biome-id switch on cheap math.
- **Opaque queue**: the terrain fully covers its pixels — render opaque (no blending)
  to save bandwidth under the fully-transparent 2D stack above it. Verify batching/
  sorting interaction in Frame Debugger (2D Renderer + opaque geometry — the URP
  migration taught us to verify, not assume).

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
The composite bake (warped hex-distance blend, palette albedo, packed params). Shader
work is in-editor-verified only — José's A-gate: inspect the composite in the Game
Render Maps window (add it via C3 or a temp custom-slot drop). Blend width, warp
strength, and per-biome albedo exposed on `BiomeProfile` for live tuning.

**A-gate (José)**: composite for a handful of seeds reads as natural, blended,
style-matched ground. Iterate A3 knobs before any view work.

### Wave B — view + reactions + GI

#### B1 — `TerrainView` + runtime shader v1 · **P0 · M · sonnet**
Quad on Default layer, frustum+margin sizing, parallax binding to `ScenarioContentRoot`
(mirror SpeckField's motion sampling incl. teleport rejection), opaque queue, composite
+ world-space detail sampling. No reactions yet. José gate: in-game look vs the
screenshots' soft style; travel/parallax feel during ascend + restart descent.

#### B2 — Disturbance reactions · **P1 · M–L · opus**
The per-biome reaction block (grass shear / water ripple / sand scatter / lava pulse)
per the architecture section. This is the showpiece and the most tuning-heavy task —
structure every constant as a material property so José can live-tune in play mode
(the FrameRateSettings/GI knob philosophy). Verify reaction cost on device: the added
taps run on every terrain pixel — budget ≤ 4 extra samples in the common path.

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
4. **Water spec**: how fancy the ripple is (gradient rings vs normal-faked specular) —
   decide by eye at B2 with device cost measured.
5. **Speck/biome tinting**: follow-up once both this and the speck palette work land —
   `GetBiomeAtWorld` feeds the speck compute's palette selection.
