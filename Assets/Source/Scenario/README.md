# Scenario

The shared field services and the views that render the play-area frame — the visible boundary the
projectile bounces off.

## SkyScatterService

`SkyScatterService.cs` — owns the sky-scatter backdrop: a single stationary quad rendered immediately
after the camera's flat clear colour and behind every other backdrop layer (`BackgroundCloud`, `WallNet`,
`SmokeField`) — a naive 2D stand-in for atmospheric scattering. Unlike `BackgroundFieldService` below it
bakes nothing: the look (a stack of circles sharing one tangent point toward the scene light, growing
outward from the light's own colour to an authored horizon colour, with a slow per-ring wave and
cloud-perturbed edges) lives entirely in `Shaders/BalloonParty/Display/SkyScatter.shader`, driven off
uniforms the shader already has for free — `SceneLight.cginc`'s flat globals (`_SceneLightDir`/
`_SceneLightColor`/`_SceneLightIntensity`) and `BackgroundField.cginc`'s shared cloud-density map. The
service's only job is building and sizing the quad once: a plain-C# DI singleton (`IStartable`/
`IDisposable`, no `MonoBehaviour`) that builds the quad via the shared `Shared/Rendering/
QuadRendererBuilder` (also used by `ScreenSpaceLightService`'s GI overlay), assigns the material/sorting
layer/order from `ISkyScatterSettings`, and scales it to `IGameDisplayConfiguration.
ReferenceWorldWidth/Height × OverscanMultiplier`. It stays stationary and generously oversized rather
than tracking the camera, so a shake or cinematic pan never reveals an edge — the same trade
`BackgroundCloud` makes. Registered in `AppLifetimeScope` (the persistent app root, not
`GameLifetimeScope`) so the sky reads correctly from the Launch begin-screen's first frame. Tuning lives
on `SkyScatterSettings` (`ISkyScatterSettings`: the display material + overscan + sorting layer/order) —
the actual ring count/colours/wave/cloud-distortion knobs are all Properties on the material itself, not
on the settings SO (mirroring how `BackgroundFieldSettings.DensityMaterial` owns the cloud roll's
tuning).

This is the one exception in `Scenario/` to "a `Renderer` is owned by a View, never a plain-C# service":
the quad is built once and never touched again (no `Tick`, no per-frame state), so a `MonoBehaviour`
View would only exist to satisfy `RegisterComponentInHierarchy`'s requirement that the component already
sit in a prefab's serialized hierarchy — a materially riskier hand-edit than the single settings-asset
field reference this design needs instead. If a future revision gives the quad per-frame behaviour
(tracking the camera, reacting to input), split the renderer-owning half into a View at that point rather
than growing this service's Unity-API surface further.

The service also implements `ISkyBackdrop` (`SetVisible`), so the ground can hide the sky while it fully
covers it (see the next section).

## Sky and ground scenarios

Each level is set either in the **sky** (the backdrop above) or on the **ground**, a meadow the board rests
on, so actors that need ground under them (bushes) don't look like they float. Which one is a per-level,
seeded choice: `ILevelPacingConfiguration.ScenarioForLevel` (see `Configuration/README.md`) is sky until the
first ground level, rolled between levels 6 and 8, stays on the ground for a fixed first meadow, then
alternates sky and ground stretches of a rolled length. The rolls mix in the run's seed, so each run lays
out its own, and a retry keeps the same scenario. A meadow arrives under puff clouds and
trades them for bushes as it goes: by the level's place in its stretch, the level generator thins and then
drops the board's puff-cloud actors and grows the bushes, so the meadow's clear second half is its bushiest.
Sky levels never have bushes: the catalogue flags them `RequiresGround`, and the generator leaves them out.
Plan: `Plans/PLAN-TerrainBiomes.md`.

- `IActiveScenario` / `ActiveScenario`: the model, the scenario the backdrop is on, as a
  `ReactiveProperty<ScenarioShift>`: the kind, and the seconds a mist reveal into it takes (0 for a cut).
  Written only by `Game/Level/ScenarioController`, which cuts to the current level's scenario on run start and
  on an immediate restart, and reveals it over the beat's own length on a level-up Ascent
  (`LevelAscendSettings.AscentSeconds`) and on the restart rise after a loss (`RestartRiseSeconds`, from
  `RestartRiseStartedMessage`, once the new scenery is placed). A reveal into the scenario already showing
  changes nothing.
- The mist reveal (`GroundRevealCurve`, pure): sky to ground, a mist rolls in over the sky
  (`IGroundSettings.MistInShare`, the first third), then a hole opens from the middle of the view and grows
  until the meadow fills it. Ground to sky plays the same curve backwards: the meadow closes into the mist,
  which then clears to the sky. The edge wanders from a circle (`RevealEdgeWander`), so it opens like parting
  cloud rather than an iris. The same reveal plays the drop between the layers
  (`GroundRevealCurve.Depth`): the ground sits `RevealDepthZoom` (1.5) times deeper than the sky, and both
  scale about the camera over the beat, so going down the meadow grows into place while the clouds grow
  past, and going up the meadow shrinks away while the clouds settle. Whichever layer the beat ends on
  reaches 1 as it ends, and the one on screen at the start starts at 1, so nothing jumps.
  `GroundRevealPlanner` (pure) decides whether a change cuts, reveals or does nothing, from the shift, the
  navigation and what is showing.
- `View/GroundView.cs`: a `MonoBehaviour` the Game scope creates on a new GameObject
  (`RegisterComponentOnNewGameObject`, so no prefab edit), parented under the Game scope's transform: the
  Game scene is preloaded additively, so an unparented object would land in the Launcher and die with it.
  It builds one stationary floor quad with `QuadRendererBuilder` from `IGroundSettings` (material,
  overscan, sorting order −15: above the sky at −20, below the clouds at −10) on the **Scenario** layer, like
  the sky it replaces, so `SceneCaptureService` captures the meadow for the GI. Both ground shaders write
  colour only (`ColorMask RGB`): the capture's alpha is its sprite-coverage mask, and a full-screen ground
  writing it would read as one big occluder. The floor draws through its own instance of the material, so a
  reveal can switch its blend state without touching the asset. Over the quad it builds one mesh of grass
  blades (`GrassBladeMesh`, drawn through `MeshRendererBuilder` at sorting order −14) with its own material.
  At rest on ground it shows both, hides the sky (`ISkyBackdrop`, an optional lookup because the sky only
  registers when its settings are wired) and sets the global `_GroundCoverage` to 1; on sky it reverses all
  of it. During a reveal it shows the sky and the ground together, turns on the mist quad, switches the floor
  to premultiplied blending and pushes the hole and the two layers' scales each frame (`_GroundReveal`,
  `_GroundRevealShape`) on unscaled time, like the beats it rides on, and scales the board's content with
  them (`ScenarioContentRoot.SetDepth`: the incoming scenery with the incoming layer, the `Outgoing` holder
  with the outgoing one, so the bushes stay planted on their meadow); at the end it cuts to the target and
  every scale returns to 1. Entering or leaving the Launch
  begin-screen cuts (it is always sky); other navigation leaves a running reveal alone, since the reveal rides
  a beat of its own that navigation doesn't end. It also pushes `_GroundPatchSize` and `_GroundDirt`,
  so the floor and the blades read the same patches and dirt. The quad stays put, but the meadow scrolls with `ScenarioContentRoot`
  (the global `_GroundScroll`, the root's travel times the meadow's scale, pushed in `LateUpdate` after the
  beat moves the root): the bushes stand on
  this ground, so on the Ascent and the restart rise the whole meadow travels in with them. A still ground
  would show them sliding over it. The clouds scroll with the root too, but at a parallax, because they are
  far away. On destroy it hands the App-owned sky and clouds their sky look back and frees the blade mesh and
  the floor's material instance (the floor's quad mesh is shared, so it stays).
- `Shaders/BalloonParty/Scenario/GroundMist.shader`: the reveal's mist, on a second overscanned quad that
  `GroundView` turns on only while a reveal runs, at sorting order −9 (over the cloud backdrop, under the
  paint and the board) on the **Default** layer, which `SceneCaptureService` leaves out, so the GI never reads
  it as a bright bounce. Thick outside the hole, it thins inward over `MistReach` (1.6 world units), frayed by
  two drifting taps of the shared cloud noise, and it is never thinner than the sky showing through the
  ground's soft edge, so no seam shows. At its thickest it is still see-through (`MistOpacity`, 0.6), so the
  sky shows faintly behind it. Lit by the scene light, so it dims at night.
- `View/GrassBladeMesh.cs`: builds the blade mesh once, from a `GrassBladeLayout` (tile size, spacing,
  seed). A jittered grid over the camera's view plus a margin (`BladeTileMargin`); each blade is a tapered
  five-vertex strip whose vertices all sit on its root, with per-blade randoms for lean, length, shade and sway
  phase. A jittered grid repeats without a seam, so the shader can wrap the roots across the tile as the
  meadow scrolls. Vertices are packed to 12 bytes (root UNorm16, corner SNorm8, randoms UNorm8) and the
  blade count is capped so 16-bit indices always fit: about 9,000 blades (45k vertices) in one draw on a
  9:16 phone at the default spacing.
- `Shaders/BalloonParty/Scenario/GrassBlades.shader`: places, bends and wraps every blade in the vertex
  stage; the fragment stage only writes the interpolated colour. Seen from above, a standing blade shows a
  short stroke leaning one way, swaying in gust waves that roll across the meadow. Each blade reads the grass
  wake once at its root (`_GrassWakeTex`, below): where a ball, a pop, a bomb or a laser stamped the
  disturbance field, the blade turns toward the push and lies down, so it shows its full length, curving from
  its root, and its tip catches the sun. The response saturates (`_WakeBend`, "Wake Response"), so a blade
  answers the first light touch of the wake instead of waiting for the push to build. One
  patch-noise tap per blade turns the tips drier on the floor's lighter patches; a second, the dirt mask,
  dries, shortens and drops out blades across a dirt patch's fade (each at its own point, so the grass frays
  into the dirt). Lit by the scene-light field at its root (`SceneLightTintAtLOD`), like the sprites' VTF
  light: the time of day, plus the local light of pops and items.
- `View/GrassWakeBuffer.cs` + `Shaders/BalloonParty/Scenario/GrassWake.shader`: the grass's own short memory
  of the disturbance field (`Shared/Disturbance/README.md`), owned by `GroundView` and stepped in its
  `LateUpdate` while the ground shows. The shared field holds a push for seconds and spreads it, which suits
  the clouds' slow drift but made the grass feel sluggish. The wake keeps only what the field gained since
  last frame and lets it go on its own recovery (`IGroundSettings.BladeRecoverySeconds`, 0.35 s), so the
  blades snap down as the ball passes and stand back up quickly behind it. Field-sized, in the field's UV
  space, half-float (the push is signed): one small blit and one copy a frame. Restarted each time the ground
  appears, so a push the field still holds doesn't land on the blades at once.
- `Shaders/BalloonParty/Scenario/Ground.shader`: the floor under the blades, three taps per pixel: soft
  darker and lighter patches from the shared tileable cloud noise, and a few large bare-dirt clusters (a
  coarse tap for the cluster, a finer one to fray its outline) that fade into the grass. Muted mid-tones, so
  the saturated balloons stand off the ground and the scene light carries the time of day across it; lit
  per pixel by the scene-light field (`SceneLightTintAt`), so pops and items light it locally too. It writes
  premultiplied colour times the coverage at the pixel: opaque at rest (coverage 1), blended over the sky
  during a reveal. The blades shorten by the same coverage at their root, so outside the hole they collapse and
  draw nothing, and the opaque blades need no blending.
- `Shaders/BalloonParty/Include/Ground.cginc`: the ground's shared globals, all pushed by `GroundView`:
  coverage, scroll, patch size and the dirt settings (`_GroundDirt`), with `GroundPatchUV`, `GroundDirtUV`
  and `GroundDirtMask` so the floor and the blades read the same patches. `GroundCoverageAt(worldPos)` is
  `_GroundCoverage` at rest; while a reveal runs (a uniform branch, so rest pays almost nothing) it is the
  hole: 1 inside, 0 outside, soft over `RevealEdgeSoftness`, from `GroundRevealDistance`, a signed distance
  from the hole's wandering edge built from two sine waves, with no texture so every reader can afford it.
  Every reader follows the hole during a reveal with no change of its own.
  `GroundRevealGroundScale` and `GroundRevealSkyScale` give the two layers' scales (1 at rest), and
  `GroundPointAt` the ground point a world position shows through the meadow's scroll and scale. The floor
  and the blades read their patches, dirt and gusts through it; the blades keep their world spacing so the
  tile still covers the view and only shorten with the scale. `BackgroundCloud` samples its cloud bodies at
  the sky's scale, which is never below 1, so the taps stay inside the view-sized cloud field; the shadows
  it casts on the meadow stay unscaled for the same reason. The puff clouds (`_RestScale`) and the bushes
  (their offset matrix) follow the content root's scale.
  `BackgroundCloud` multiplies its cloud bodies by `1 − coverage × _GroundBodyFade` and keeps the drop
  shadows, so on the ground the clouds read as overhead and out of frame, their shadows drifting over the
  grass. With the bodies fully faded it takes a shadow-only branch that skips the body's lighting.
  `SpriteLightDriven` and `PaintBlob` also read it: their drop shadows normally fade where there is no cloud
  or smoke to land on (`_CLOUD_FADE_ON`), and on the ground they land on the grass instead, so the balloon,
  projectile and paint shadows stay. The GI smear (`ScreenSpaceLightSmear`) gates its shadow the same way.

### Water

The third scenario: after the first meadow, each surface stretch is a meadow or **water** by a coin flip
(`ScenarioKind.Water`). Water levels have no puff clouds or bushes, and nothing on them bounces shots but the
unbreakable balloon; the longer a water level runs, the more of each spawn line is bubble clusters
(`Balloon/Spawner/BalloonSpawner`). Plan: `Plans/PLAN-WaterScenario.md`.

Water is a surface like the meadow, so everything above about reveals, depth, coverage and scrolling
applies to it unchanged; the planner and the curve treat any kind but sky as a surface. Sky always sits
between two surfaces, so a reveal never goes from meadow to water. `GroundView` draws water on the same floor quad
through its own instance of `IGroundSettings.WaterMaterial` (no blades, no grass wake), and swaps the GI's
bounce reference to `WaterBounceReference`.

- `Shaders/BalloonParty/Scenario/Water.shader`: one pass, drawn as a body of water over a sandy bottom.
  The bottom's depth drifts across the level from the shared noise, from sandbanks to deep channels. It is
  seen as from a camera above the middle of the screen: a deeper bottom shrinks toward the middle and
  slides slower when the board scrolls, and the surface's slope bends the view of it more over deep water.
  The sand carries ripple bands and the caustic net (below), shaded around the sand's own colour, never
  added past it. The water over it absorbs each colour by its own rate per world unit (red first, blue
  last) and scatters its own colour back, so shallows read as sand and deep water as teal. On top, slopes
  facing the sun catch a soft highlight and crests turned just so between the sun and the eye glint. Lit by
  the scene-light field at 0.7 influence (a warm evening tints the water rather than turning it olive), with
  light above full eased in so a pop's glow doesn't clip to white; premultiplied by `GroundCoverageAt`,
  like the floor.
- `Shaders/BalloonParty/Include/WaterSurface.cginc`: the surface's slope: a slow swell of two noise layers
  on different headings (the smaller slower, as shorter waves are), travelling with the board, plus the
  rings of the ripple simulation (below). The slope is what bends the bottom and catches the light.
- `View/WaterRippleBuffer.cs` + `Shaders/BalloonParty/Scenario/WaterRipple.shader`: the ripples. A wave
  equation over a height field covering the disturbance field, in its UV space (`IGroundSettings.
  WaterRippleTexelsPerUnit`, 12), published as `_WaterRippleTex`. Wherever the field gained a push since the
  last step the water is pressed down (a big gain only a little deeper than a small one, so pops don't
  drown a passing ball's wake), and the dip travels outward as rings at the ripple material's speed and
  fades over its fade time; the field's edges soak rings up rather than bounce them back. Fixed 1/60 s
  steps (at most four a frame), in game time, so the rings walk at the same speed at any frame rate and a
  pause holds them. It restarts calm whenever water appears. An earlier version drew rings straight from
  the field's push (`sin(k·push + ωt)`): they stayed in the push's shape and wobbled in place like gel.
- `View/WaterCausticBuffer.cs` + `Shaders/BalloonParty/Scenario/WaterCaustics.shader`: the caustics. Each
  frame on water, one blit draws a tile of the moving caustic net into a 256² R8 target that wraps, with
  mips, published as `_WaterCausticTex`. The net is the borders between drifting cells, warped so they
  curve. The water shader lays the tile twice, at two scales and angles (the second varies the first's
  brightness along its lines, so no tiling shows), shifts it away from the sun, splits it into colour
  fringes along the sun and blurs it where the water is deep. The tile is not tied to the view, so it costs
  the same on any screen.

Shader changes are checked in the editor only; `dotnet build` doesn't compile them.

## BackgroundFieldService

`BackgroundFieldService.cs` — owns the shared cloud field: an RT (`_BackgroundDensityTex`) blitted from
the `BackgroundFieldDensity` material on a cadence timer rather than every frame (`ICadencedEffect`,
weight 1 — see `Shared/Cadence/README.md`), with the disturbance field baked in so clouds part around a
wall hit or a pop. The cadence gate is bypassed while the scenario root is moving (launch ascend,
level-up, game-over descent), so the density texture never visibly lags the scrolling camera during a
transition. It publishes the RT plus its world bounds as global shader properties, so
every consumer reads the SAME clouds with a single tap rather than each running its own noise. It is
the ONE cloud generator in the project; consumers only sample, never roll their own — currently the
`BackgroundCloud` backdrop, sprite drop-shadows (`SpriteLightDriven`), the GI light smear
(`ScreenSpaceLightSmear`), and the wall net below.

It is a **plain-C# DI singleton** — `IStartable/ITickable/IDisposable`, no `MonoBehaviour` and no scene
GameObject — exactly like its siblings `DisturbanceFieldService` / `SceneLightFieldService`. Its bounds
come from `IGameDisplayConfiguration` via the shared `DisturbanceFieldCoordinates` (origin-centered, so
it aligns with the disturbance/light fields and needs no camera). It is registered in `AppLifetimeScope`
— the persistent app root, not the per-run `GameLifetimeScope` — so it exists before either scene loads,
which is what makes the launch begin-screen's clouds live from the first frame rather than depending on
the Game scene's preload. Its tuning lives on `BackgroundFieldSettings` (`IBackgroundFieldSettings`: the
density blit material + resolution + transition parallax). The GPU-side tap is
`Assets/Shaders/BalloonParty/Include/BackgroundField.cginc`; generation is `BackgroundFieldGen.cginc` +
the `BackgroundFieldDensity` blit shader. See `Shared/Disturbance/README.md`.

## SmokeFieldService

`SmokeFieldService.cs` — the smoke trail field: a persistent screen-space RT that accumulates
palette-colored capsule stamps and decays them into animated smoke wisps. The 4th field service
alongside Disturbance, SceneLight, and Background.

**Profile-based stamping:** every stamp originates from a `PaintSource` (`ProjectileTrail`,
`ToughPop`, `ToughBreathing`, `ToughDeflect`). The service resolves the source to a `PaintProfile`
(via `ISmokeFieldSettings.GetProfile`) that defines radius, opacity, and how color is chosen
(`PaintColorMode.Dynamic` — caller supplies a palette index, `Palette` — profile names a fixed
palette entry, `Custom` — profile carries a fixed RGB color). The public API is:

- `Paint(PaintSource, Vector3, int paletteIndex = -1)` — single stamp.
- `PaintScatter(PaintSource, Vector3, int count, float scatterRadius, int paletteIndex = -1)` —
  multiple stamps scattered in a random ring.

**Pipeline:** each frame the projectile is moving, `ProjectileView` calls `Paint` with
`PaintSource.ProjectileTrail` (capsule from previous → current position). The service batches up to
32 stamps per blit pass into `SmokeFieldStamp.shader`, then runs a decay pass
(`SmokeFieldDecay.shader`) that advects, expands, erodes, and fades the accumulated paint. The
result is published as the global `_SmokeTex`, sampled by the display shader.

**Decay model:** wind advection (swinging direction via sin oscillation), turbulent perturbation,
radius expansion, noise erosion, and linear alpha decay — all driven by `_SmokeTime` (which
respects `TimeScaleService` pause/slow-mo). Fresh stamps resist wind (age-based bias); fast projectile
stamps get dampened wind so they don't smear.

**Lifecycle:** clears the RT on `LevelUpDismissedMessage` (new level = blank canvas) and on
`GameOverMessage` (descend/game-over = blank canvas).

### SmokeFieldResources

`SmokeFieldResources.cs` — owns the GPU resources: two `ARGBHalf` ping-pong RTs and the stamp +
decay materials (instantiated from the shaders on `ISmokeFieldSettings`). Handles blit-and-swap;
separated from the service so resource lifecycle is testable independently.

### SmokeFieldView

`View/SmokeFieldView.cs` — a `MonoBehaviour` that renders the smoke field RT as visible smoke
behind the cloud backdrop. Builds a viewport-sized quad at startup, assigns the `SmokeTrailDisplay`
material, and sits on the Default sorting layer at order −1 (behind the clouds at 0). Registered via
`RegisterComponentInHierarchy<SmokeFieldView>()`.

### Shaders

| Shader | Role |
|---|---|
| `SmokeFieldStamp` | Capsule SDF batched blit (32 stamps/pass) |
| `SmokeFieldDecay` | Smoke dispersion: wind + turbulence + expansion + erosion + decay |
| `SmokeTrailDisplay` | Display: curl-noise swirl, 5-tap bleed, sigmoid edges, wisp noise, paper grain, sky transmission, shadow lift, scene-light integration |
| `SmokeField.cginc` | Consumer include — exposes global `_SmokeTex` |

### Configuration

`ISmokeFieldSettings` / `SmokeFieldSettings` SO — stamps shader, decay shader, texels-per-unit,
decay rate, decay tick interval, wind speed/influence/age-bias/direction/swing, and a `PaintProfile[]`
array (each entry maps `PaintSource` flags → radius, opacity, `PaintColorMode`, palette name / custom
color). `GetProfile(PaintSource)` resolves the active profile for a given source.

## WallNetView

`View/WallNetView.cs` — a `MonoBehaviour` that builds the four **net-strip** meshes framing the play
area, one per edge of the logical play rectangle. It reads that rectangle from the injected
`IProjectileFlightConfig.LimitsClockwise` (unpacked via `WallLimits`), so the visible frame is sourced from
the same rectangle the projectile billiard reflects off — no hand-syncing a separate visual frame.

Each strip is a flat quad band laid **outward** from its wall by `_stripWidth` (away from the play area,
so its reveal never covers the balloons), tessellated into a quad grid (`_cellsPerUnitAlong` ×
`_cellsAcross`) fine enough to deform smoothly. The meshes are built once in `Start` and never rewritten
from C#: all motion lives in the shared net material's vertex stage, so there is zero per-frame CPU. Per
vertex the view bakes:

- **normal** = the edge's outward direction, so the shader can un-extrude each strip to a thin line at
  rest and billow it back out along that same direction.
- **UV0** = `(across 0→1, along 0→1)` — normalised, for the band-edge feather and the unfurl/billow maths.
- **UV1** = the net-pattern tiling (cells kept square: the across cell size sets the along tiling).

The strips render through generated child `MeshRenderer`s sharing `_netMaterial`, on the configured
sorting layer. Registered with `RegisterComponentInHierarchy<WallNetView>()` in `GameScopeRegistration`
so its `IProjectileFlightConfig` injection resolves; the GameObject lives as a static child of the Scenario
prefab (identity transform, so mesh world coordinates map straight through).

The net's motion and look now read two shared fields, not just one:

- **Disturbance** — the material samples the shared `_DisturbanceTex` in the vertex stage (the `BushLeaf`
  precedent): the projectile's existing wall-contact stamp unfurls the strip from a thin resting line to
  its full depth and billows it along the impact direction, and the same disturbance amount shifts the
  net's colour toward a "struck" gradient and pushes its visibility toward `_DisturbVisibilityTarget` —
  no wall-hit event, no CPU sim.
- **Cloud field** — the net's baseline visibility and idle breathing both read `BackgroundFieldService`'s
  shared field via `BackgroundField.cginc`: it fades in only where there's cloud (so it reads as part of
  the clouds rather than a separate frame) and its idle undulation is phase-warped by the cloud noise
  instead of a rigid marching sine.

`WallNetView` also drives a `_TransitionFade` on the material, independent of the fields above: the net
starts hidden, fades in on the first shot (`ProjectileFiredMessage`), and fades back out on a Game
transition (`ScoreLevelUpMessage`, `RunResetMessage`) until the next run's first shot.

See `Assets/Source/Shared/Disturbance/README.md` for the disturbance field.

## Shader

`Assets/Shaders/BalloonParty/Scenario/WallNet.shader` — the shared world-space net material: a
procedural tennis-net pattern (grid lines from UV1, feathered band edges, HDR-emissive tint so bloom
carries the glow) driven by the disturbance-field unfurl/billow/colour-gradient, the cloud-field
visibility/breathing, and the scene-light tint described above. Shader/visual changes are validated
in-editor, not by `dotnet build`.
