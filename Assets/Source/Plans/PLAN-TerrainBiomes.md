@page plan_terrain_biomes Terrain & Biomes

# Terrain & Biomes: sky and ground scenarios

> Each level is set either in the **sky** (today's backdrop) or on the **ground** (a
> meadow the board rests on), chosen per level from the level's seed. A level-up Ascent
> can switch either way, so the run sometimes lands and sometimes climbs back up to the
> clouds. Actors that need ground under them (bushes first) appear only on ground levels,
> and the grass reacts to the ball as it flies through.
> First planned 2026-07-12 as a full seeded biome system; never started. Re-scoped
> 2026-11-08 to the **smallest version that lets bushes ship**: one ground, the
> per-level scenario, the grass reaction. The full biome system is kept below as *Later*,
> unscheduled. **Status: phase 1 built, awaiting in-editor review.**

---

## Why now

Bushes (`GridActorType.Bush`, unlock level 16 in `LevelPacingConfiguration`) are withheld
from level generation: over the sky backdrop a bush reads as floating. Owner direction
(2026-11-08): the scenario moves between sky and ground in both directions ("once bushes
appear that doesn't mean we should stay there, sometimes we go up away to clouds"), and
the ground reacts to the bullet, as blades of grass deforming while it moves through.

## Verified inventory (2026-11-08)

What the backdrop is today, back to front (all on sorting layer `Default`):

| Order | Layer | Owner | Notes |
|---|---|---|---|
| — | camera clear colour | `CameraBackgroundTint` | tinted to the live time-of-day light colour |
| −20 | sky scatter quad | `SkyScatterService` (App scope) | stationary, 1.6× overscan; 10-ring loop per pixel — the priciest backdrop pixel |
| −10 | cloud backdrop | `BackgroundCloud` shader over `_BackgroundDensityTex` | cloud bodies + an offset drop-shadow tap; clouds scroll with the root via `TransitionParallax` |
| −1 | smoke | `SmokeFieldView` | projectile trails |
| — | wall net, GI overlay, actors | | above the backdrop |

- **Disturbance field** (`Shared/Disturbance/`, App scope, always running): a
  camera-sized `ARGBHalf` ping-pong RT at 8 texels per world unit, published as the global
  `_DisturbanceTex` with `_FieldBoundsMin`/`_FieldBoundsSize`. R = signed density (0.5 at
  rest), GB = displacement XY (0.5-biased), A = palette colour tag. The projectile stamps
  its wake along its path (`StampSource.Projectile`: radius 0.3, strength 0.8); the
  diffusion tick (every 0.05 s) decays the displacement and reforms the density, so a
  wake recovers on its own. Bush leaves (`BushLeaf.shader`, vertex stage) and puff clouds
  already read it with one tap.
- **Scenario travel**: `ScenarioContentRoot` moves, the camera never does. Level-up Ascent
  (`LevelAscendCinematic`): root goes from `+Height` to 0 (`Height` = 15 world units,
  about 4 s at the authored curve and speed); old content is held at local `−Height` and
  slides out the bottom, new content slides in from the top. Game-over restart
  (`GameOverLossCinematic.RiseScenarioAsync`): root goes from `−Height` to 0, old content
  held at local `+Height` leaves through the top. `LevelAscendStartedMessage` fires right
  before the Ascent.
- **Per-level backdrop state already exists**: `TimeOfDayCycle` (`Game/Level/`) snaps the
  light to the level on run start/reset (so the start-level cheat works) and sweeps on
  `LevelUpPhase.Transitioning`. The ground controller copies that shape.
- **Seeded levels**: `LevelGenerationSettings.SeedForLevel(level)` hashes the level with a
  salt, so a retry replays the same mix. `PLAN-Coins.md` §10 ("luck levels", being written
  in parallel) rolls parts of a level from the same seed; the scenario is rolled the same
  way.
- **Baked noise**: `Assets/Textures/Grid/CloudNoiseTileable.png` (made by
  `Tools > BalloonParty > Generate Cloud Noise Texture`), sampled through
  `CloudNoise.cginc`. The ground reads it for its large patches; the blades are geometry
  (see How it looks). No per-pixel procedural noise.
- **Lighting**: `SceneLight.cginc` gives the time-of-day colour and the sun direction; the
  clouds already use both. The floor and the blades read its field-aware forms
  (`SceneLightTintAt`, per pixel on the floor and per blade root on the blades), so the
  local light of pops and items lands on the grass as it does on the sprites.
- **GI capture** picks GameObject layers (`SceneCaptureService._capturedLayers`), not
  sorting layers. The ground is on the captured Scenario layer, writing colour but not
  the capture's coverage alpha, so the light buffer gathers the meadow as it gathers the
  sky. The overlay measures bounce against the meadow's lit average
  (`IGroundSettings.BounceReference` × the scene light) on ground levels, as it measures
  against the sky's clear colour on sky levels: open grass nets to neutral, and a balloon
  bleeds onto the grass by how it differs from the grass. Measured against the sky blue,
  every balloon left a dark tinted ring on the grass (owner, 2026-11-09). GI shadows land
  on the grass everywhere, not just under cloud.
- **Level content**: levels are generated (`LevelGenerator` from `LevelGenerationSettings`),
  not authored per range; ranges are debug presets only. The `Withheld` catalogue flag is
  being added in separate work; it and the luck levels touch the same settings file, so
  edits there are coordinated.
- **Stale in the 2026-07 plan, now corrected**: "background is just the camera clear"
  (there are now four backdrop layers); "`LevelParameters` ranges carry per-altitude
  data" (levels are generated now).

## The smallest version

### Which levels are ground

- **A seeded per-level scenario**, `Sky` or `Ground`, computed by a pure
  `ScenarioSchedule.ForLevel(level)` from a small block on `LevelGenerationSettings`:
  - `FirstGroundLevel` (proposed **16**, the bush unlock): levels before it are always
    sky; this level is always ground, so the bushes' first appearance is reliable.
  - After that the run moves in **stretches**: a stretch lasts between `MinStretch` and
    `MaxStretch` levels (say 2–4), then flips. Each stretch length is rolled from a hash
    of the level and the scenario's own salt, so retuning other content doesn't reshuffle
    the scenarios, and a retry keeps the same one.
  - Pure and cheap: `ForLevel` walks the stretches from `FirstGroundLevel` up to the
    level (a few dozen steps at most), no state, no allocation.
- **Bushes only on ground**: a `RequiresGround` flag on `GridActorContentEntry`;
  `LevelGenerator` leaves flagged actors out of sky levels. One source of truth: the
  generator and the ground view both ask `ScenarioSchedule`.
- **Triggers** (the `TimeOfDayCycle` shape): snap to the level's scenario on run start and
  run reset (start-level cheat, restart); animate on the level-up Ascent and on the
  restart rise when the two levels' scenarios differ. Same scenario on both sides changes
  nothing.

### How it looks

- **The floor**: an opaque quad of soft darker and lighter patches (one cloud-noise tap),
  dark and muted so the saturated balloons stand off it. Tinted by the scene light, so it
  dims through dusk and night.
- **The blades are geometry**: one mesh of about 9,000 tapered blades over the floor, one
  draw call. The first version drew blades as stretched noise in the floor shader; it read
  as a texture, not grass, so the owner chose real blades (2026-11-08).
  - Seen from above, a standing blade is a short stroke leaning one way. Blades sway in
    gust waves that roll across the meadow, and tips turn drier on the floor's lighter
    patches.
- **The grass reacts to the ball** through the existing disturbance field, with no new
  stamp:
  - Each blade reads the grass wake (`GrassWakeBuffer`) once, at its root, in the vertex
    stage. The wake is a small field-sized buffer that keeps only what the disturbance
    field gained each frame and lets it go in about a third of a second: the shared field
    holds and spreads a push for seconds, which felt sluggish on grass (owner,
    2026-11-09).
  - The displacement (GB) turns the blade toward the push and lays it down: from above it
    shows more of its length, curving from its root, so where the ball passed the blades
    part and fan out.
  - The density change (R) lays them flatter still, and laid blades catch the sun toward
    their tips, so the path reads as a combed stripe.
  - Recovery is the wake's own (`BladeRecoverySeconds`), not the field's. Pops, bombs
    and lasers stamp the same field, so the grass answers them too.
  - The field covers the camera's rect, and the blade tile reaches past it: blades outside
    the field rest.
  - The field is 8 texels per unit, so the wake is soft, about a ball-width wide. A
    crisper wake would mean raising `TexelsPerUnit` for every consumer, which is a tuning
    decision, not planned.
- **The meadow travels with the board**: the bushes are planted on it, so the floor and
  the blades scroll with `ScenarioContentRoot` one-to-one (`_GroundScroll`; the blades wrap
  across their tile). On the Ascent and the
  restart rise the ground and its bushes move in together; a still ground made the
  bushes look like they slid in. The disturbance field stays camera-fixed, where the
  shots stamp it.
- **Clouds on ground levels**: the cloud bodies fade out and their drop shadows stay, so
  cloud shadows drift over the grass, as if the clouds were overhead and out of frame.
  If the bodies stayed, the ground would read as far below and the bushes would float
  again. Climbing back to the sky brings the bodies back.
- **The transition** rides the scenario root, in either direction. The edge between the
  two scenarios is a noise-warped line with a white mist fringe (the cloud colour),
  placed in the root's space halfway between old and new content: local y = −Height/2
  on the Ascent, +Height/2 on the restart rise. The old scenario is drawn on the old
  content's side, the new one on the new side, so one rule covers sky→ground,
  ground→sky, and both restart directions. The ground and the bushes slide in or out
  together; the edge needs no timing of its own. The switch is armed when the
  transition starts and takes effect when the root first moves, so nothing pops while
  the root waits at 0.
- **Dirt and colour**: the floor is muted mid-tone greens, light enough for the time of
  day to read across it and dark enough for the balloons to stand off it. A few large
  bare-dirt clusters (`IGroundSettings` dirt size, threshold and softness) fade into the
  grass: the floor blends to soil over a wide noise band with a frayed outline, and the
  blades dry, shorten and drop out across the same band (owner, 2026-11-08: patches
  blend; fewer, bigger clusters).
- **Shadows on the ground**: the sprite and paint drop shadows that fade where there is
  no cloud or smoke under them (`_CLOUD_FADE_ON`) stay on ground levels, since they land
  on the grass (`GroundCoverageAt` in `SpriteLightDriven` and `PaintBlob`).
- **Bush shadow**: the bush's drop-shadow distance is checked against the ground in the
  editor; a long offset shadow reads as height.

### Performance (mobile, 120 Hz = 8.33 ms frame)

- **Bake once**: no new RT, no per-level bake, no new field. The only baked input is the
  existing noise texture; the disturbance field already runs every frame for the clouds
  and bushes.
- **Ground cost**: the floor is 3 taps per pixel (patches, dirt, dirt fray). The grass
  wake is one blit and one copy a frame on a field-sized target (about 45×80). The blades
  add about 45k vertices (12 bytes each, 16-bit indices) in one draw, each with 3 vertex
  taps (disturbance, patch noise, dirt) plus the light field, and a flat-colour fragment over about a third of the screen. No alpha test or blending anywhere (a tile-GPU
  cost). Measure the vertex cost on a low-end phone at the review gate; `BladeSpacing`
  trades density for vertices with the inverse square.
- **Sky levels**: the ground renderer is disabled. Cost: zero.
- **Ground levels**: the ground covers every pixel, so the sky-scatter quad is disabled
  (`SkyScatterService` gains a visibility toggle). That swaps the 10-ring sky loop for
  the 3-tap floor plus the blades; measure it at the review gate.
- **During a transition** (about 4 s, gameplay paused): sky and ground both draw.
- **CPU**: no per-frame work at rest and no allocations; while a transition runs, one
  transform read and one material float per frame. `ScenarioSchedule.ForLevel` runs once
  per level.
- Not checkable by `dotnet build`: the shader needs an in-editor look and a Frame
  Debugger check of the draw order.

### What it reuses

`DisturbanceFieldService`'s `_DisturbanceTex` and the projectile's existing wake stamps
for the grass reaction; `QuadRendererBuilder` for the quad; `CloudNoiseTileable.png` +
`CloudNoise.cginc` for the patches; `SceneLight.cginc` for the tint and sun direction;
`BackgroundCloud`'s existing shadow tap (only a global ground-coverage fade on the bodies
is new); `SeedForLevel`'s hash for the scenario roll; the `TimeOfDayCycle`
snap/transition shape; `ScenarioContentRoot` and the two existing transition beats. No
changes to the Ascent or restart code.

### Shape (MVC)

- `ScenarioKind` (`Sky`, `Ground`), `ScenarioSchedule` and `ScenarioScheduleSettings` (pure,
  `Configuration/Level/`), the settings block on `LevelGenerationSettings`, read through
  `ILevelPacingConfiguration.ScenarioForLevel`; EditMode-tested. `LevelSeedHash` is the
  hash shared with `SeedForLevel`.
- `GroundSettings` / `IGroundSettings` (`Configuration/Effects/`): ground material,
  sorting order (−15, between sky −20 and clouds −10), overscan.
- `ActiveScenario` / `IActiveScenario` (`Scenario/`): the model, the scenario shown now.
- `ScenarioController` (plain C#, `Game/Level/`, `IStartable`/`IRunResettable`): asks the
  schedule for the current level and writes the model.
- `GroundView` (thin MonoBehaviour, `Scenario/View/`, parented under the Game scope so the
  additive Launcher→Game load doesn't destroy it): owns the quad, pushes the
  `_GroundCoverage` global the cloud shader reads (`Include/Ground.cginc`), and hides the
  sky through `ISkyBackdrop`. The Launch begin-screen always shows sky.
- `Ground.shader` (`Shaders/BalloonParty/Scenario/`).

### Phases

1. **Ground at rest, with the grass reaction**: `ScenarioSchedule` + tests (determinism,
   level 16 always ground, stretch bounds, no flips before 16), `GroundSettings`,
   controller snap on run start/reset, `GroundView`, the meadow shader reading the
   disturbance field, the sky toggle, the cloud-body fade. Seen in the editor with the
   start-level cheat at 16, firing balls through the grass. *Review: architect,
   test-everything, reviewer, optimizer.*
2. **Transitions both ways**: the edge in root space on the Ascent and the restart rise,
   the mist fringe, arm-then-commit. Seen by playing across a scenario flip and by
   losing on a ground level. Seams to change (architect, phase 1 review):
   - The restart goes through `IRunResettable.ResetRun`, which runs before the root drops
     to −Height and can't tell a cinematic restart (rise) from a skip-path one (cut).
     Move the reset onto `RunResetMessage`: `BoardReset == false` arms a rise edge at
     +Height/2, `BoardReset == true` and run start snap.
   - `ActiveScenario` keeps `Kind` as the committed scenario and gains the pending
     transition (target, edge side); `GroundView` commits when the root first moves. It
     already feeds the root offset to the meadow (phase 1), so the grass travels with the
     board.
   - Phase 1 visible gap until then: the switch is a cut, so on the Ascent the old board
     floats out over the new backdrop, and on a cinematic restart a ground board sits on
     the sky for the spawn wait and the rise.
3. **Bushes on ground only**: `RequiresGround` on the catalogue and in `LevelGenerator`
   (with a test that no sky level contains a ground-only actor), un-withhold bushes once
   the `Withheld` flag lands, tune the bush shadow on grass, run a playtest-bot
   comparison, update the Configuration README. Debug presets (level < 1) skip
   `LevelGenerator` and are always sky, so a preset that lists bushes would float them;
   the preset validator should warn.

## Decisions (signed off 2026-11-08)

1. First ground level **16**, with the first bushes.
2. Stretches of **2–4 levels** per scenario.
3. Cloud bodies **fade out on ground**; their shadows stay.

## In-editor checks for phase 1

- The meadow and the grass reaction at game zoom, by day and night (start-level cheat 16).
- Frame Debugger: the ground draws before the clouds and the smoke, and the sky quad is
  gone on ground levels.
- The GI: on ground levels the capture sees the meadow in place of the sky, so check
  the balloons' glow by day, the GI shadows on the grass, and that sky levels are
  unchanged.
- Launcher → Game in play mode: the `Ground` object lives under the Game scope.
- The blades on a phone: their width and density at game zoom, and the vertex cost on a
  low-end device (`BladeSpacing` 0.085, about 45k vertices on 9:16), and a 4:3 tablet and a
  21:9 phone show blades to every edge with no wrap pops.
- A level-up within a ground stretch (16 → 17 with the default salt): the meadow and the
  bushes travel in together, with nothing sliding over still grass.

## Follow-ups from the phase 1 optimizer review

- Shader prewarm: the ground's first draw lands on the first ground level's Ascent;
  add `BalloonParty/Scenario/Ground` and `GrassBlades` to the shader-variant capture (`PLAN-PerformanceRecovery`
  F4), with both blend states once phase 2 switches them.
- Only if an on-device profile shows ground levels vertex-bound: a per-blade pre-pass. Each
  of a blade's five vertices repeats the same per-blade work (the wrap, both taps, the bend);
  a small blit into one texel per blade cell would do it once, and the vertex stage would
  only evaluate the curve. Estimated 0.15–0.3 ms on a Mali-G52-class GPU. A blit, not a
  compute buffer: many Mali GLES drivers can't read storage buffers in the vertex stage.
- Optional, once the phase 2 edge allows it: draw the cloud shadow inside the ground shader
  and turn the cloud pass off on ground levels (one fewer full-screen pass, five taps).

---

## Later: the full biome system (unscheduled)

The 2026-07-12 design, kept for when more biomes earn it. Its inventory is superseded by
the one above. In short:

- **Seeded terrain**: seed + level → identical map. Two baked-noise fields (elevation,
  moisture) sampled at hex centres classify each slot into a biome (water, sand, grass,
  dirt, stone, lava) through a `BiomeProfile`; `ITerrainQuery.GetBiome(slot)` /
  `GetBiomeAtWorld(pos)` is the seam for placement gating (bushes only on grass) and
  biome-tinted specks.
- **Bake once per level** into a ~512-wide RT: noise-warped hex distances blend the
  nearest biomes (height blending recommended over a plain cross-fade), plus a data map
  with biome id and a signed distance to the boundary (shorelines, wet sand, lava crust,
  edge AO as one-tap lookups).
- **Runtime**: one quad sampling the composite, a world-space detail texture and
  `_DisturbanceTex` for reactions: grass shears along the field's displacement, water
  gets dual scrolling noise plus rings from the density gradient, sand darkens, lava
  pulses emissive (HDR-ready per `PLAN-HDRColorPipeline.md`), stone stays inert. Budget
  ≤ 4 extra taps in the common path; no alpha-tested grass (a tile-GPU cost).
- **Anti-repetition**, cheapest first: per-cell hash flips, a macro variation tap, a
  channel-packed detail atlas, scatter stamps; hex-tiling only where those fail.
- **Composition rules**: one sun (`SceneLight` direction), one wind (shared with the bush
  wind), no second lighting model; the ground feeds the GI through the same capture as the sky.

The meadow above is the first biome of that system. When it grows, `ScenarioKind` gains
more grounds (the schedule rolls which one a ground stretch uses), the meadow shader and
its grass reaction become the grass biome's runtime look, and the sky/ground edge becomes
the first boundary.
