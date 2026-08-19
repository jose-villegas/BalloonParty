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
