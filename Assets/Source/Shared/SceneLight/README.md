# Scene Light Field

The **light field** — the disturbance-field architecture applied to light (see @ref plan_lighting,
"Milestone 3"). A small screen/world-space RenderTexture that every direction/specular consumer can
sample once at its anchor to get the local light *direction + magnitude + colour*, regardless of how
many lights contribute. This folder is the field's **producer**; consumers read it through the
shared shader include `Assets/Shaders/BalloonParty/Include/SceneLight.cginc`.

`SceneLightFieldService` is the single owner of both the light **field** RT and the **ambient
globals** (`_SceneLightDir` / `_SceneLightColor` / `_SceneLightIntensity`). The ambient values are
project-wide config on the `SceneLightFieldSettings` SO (injected as `ISceneLightSettings`), pushed
once at startup (re-pushed every tick only in the editor, so they stay live-tunable in play mode). The
field is a super-set layered on top: at rest it is exactly
those globals painted uniformly, and every field helper falls back to the flat globals when the field
is off — so the field is a strictly additive seam, never a replacement.

## Contents

| File | What it provides |
|---|---|
| `SceneLightFieldService` | Plain-C# DI service (`IStartable`/`ITickable`/`IDisposable`), registered in `GameScopeRegistration` next to `DisturbanceFieldService` (Singleton, `AsImplementedInterfaces().AsSelf()`). Pushes the ambient globals (`_SceneLightDir`/`_SceneLightColor`/`_SceneLightIntensity`) from `ISceneLightSettings` once at startup (re-pushed every tick only in the editor, for live tuning), then builds the field via `SceneLightFieldResources` **only on ticks where a registered light changed AND a frame-interval cadence cap has elapsed** (`FieldFrameInterval`, see Cadence below). Holds the registry of on lights, subscribing to each one's reactive properties to know when to re-render. Pushes the bounds/texel/palette globals + on-flag; releases the RTs in `Dispose`. API: `RegisterLight(Light) → IDisposable`, `ClearLights()` |
| `SceneLightFieldResources` | The field's GPU resources — **two** ping-pong RTs (`ARGBHalf` where supported, else `ARGB32`) and the fill / accumulate / gradient materials — kept separate from the service's logic, mirroring `DisturbanceFieldResources`. Owns the `_SceneLightTex` global push, `BlitAndSwap`, the `Fill(magnitude, direction)` and `Gradient()` passes, and exposes `AccumulateMaterial` for the service to upload the light arrays onto |
| `ISceneLightFieldSettings` / `SceneLightFieldSettings` | Read-only config interface + `ScriptableObject` (in `Configuration/Effects`, mirroring `IDisturbanceFieldSettings`) exposing the field's tuning knobs: `TexelsPerUnit` (RT density), `FieldFrameInterval` (re-render cadence cap, see Cadence below), `MaxLights` (per-batch cap, \f$\le\f$ the accumulate shader's 32), `AccumulationCeiling` (overlap soft-clamp), `DirectionResponse` (how strongly a light's local brightness bends the field direction toward it). Light *falloff shape* is per-light (`Light.FalloffPower`), not here. Injected into the service; registered directly in `GameLifetimeScope` like the other settings SOs — so **create the asset** via `Create ▸ Configuration ▸ Scene Light Field Settings` and assign it on the `GameLifetimeScope` (an unassigned slot NREs on start, same as its siblings). |
| `Light` | A small reactive model a caller owns: `Position` / `EndPosition` / `Radius` / `EndRadius` (perpendicular half-width at each end) / `Intensity` / `FalloffPower` / `PaletteIndex` as `ReactiveProperty`s, with `const` defaults. `EndPosition == Position` is a point/disc light; set them apart (or use `Light.Segment(start, end, …)`) for a **capsule/area** light — a beam decaying from its axis to the sides (the laser cross is two of these). Set `EndRadius` apart from `Radius` (or pass `endRadius`) to **taper** the beam — the half-width lerps along the axis, and since the gradient pass derives GB from R, the taper shapes the local direction field too. On/off is `RegisterLight`/dispose; brightness is `Intensity`; no built-in decay. **Not** a config ScriptableObject. (Name collides with `UnityEngine.Light` — alias when both are in scope.) |

## Channel encoding (single RT)

| Channel | Meaning | Rest value |
|---|---|---|
| **R** | **local** light boost above the ambient — 0 at rest; the accumulate pass adds each registered light's magnitude (soft-clamped). The ambient magnitude is NOT stored here — it's the global `_SceneLightIntensity`, added by the include's helpers (so the field is purely local, and ambient tweaks don't re-render it) | `0` |
| **G/B** | 0.5-biased **local direction weight** (\f$gb = (\text{weight} \cdot \text{localDir}) \cdot 0.5 + 0.5\f$): the gradient pass writes `grad(R)`'s direction (toward the nearest local light) scaled by the direction weight. Length 0 = no local light. Consumers un-bias it and blend the global `_SceneLightDir` toward `localDir` by that length — so the field stores no ambient | `0.5` (neutral / no local direction) |
| **A** | palette-colour **index**, \f$(\text{index}+1)/16\f$; **0 = "no colour, use `_SceneLightColor`"**. The accumulate pass writes the palette index of the light that contributes most at each texel | `0` exactly |

## The render pipeline (three passes, ping-pong)

A render rebuilds the whole field from scratch (there's no in-texture persistence like the disturbance
field's diffusion) via a three-pass ping-pong over the two RTs. It runs only when the field is dirty
(a registered light or the directional owner changed) **and** the frame-interval cadence cap has
elapsed — see the cadence note below:

1. **Fill** (`Hidden/BalloonParty/SceneLightFieldFill`) — clears to the constant rest state: R = 0
   (no local light), GB = 0.5 (neutral), A = 0. This is the read buffer the chain builds on.
2. **Accumulate** (`Hidden/BalloonParty/SceneLightAccumulate`, batched — up to 32 lights/blit,
   `Vector4[]`/`float[]` uploads, \f$(\text{index}+1)/16\f$ palette encoding, mirroring `DisturbanceStampBatched`) —
   ADDS each registered light's magnitude into R and writes the dominant light's palette index into A.
   Each light is a **capsule**: the falloff uses aspect-corrected distance to the segment `[start, end]`
   (`_StampCenters[s].xy`/`.zw`), so a point light (`start == end`) is a disc and a segment is a beam
   decaying from its axis — \f$\mathrm{pow}\big(\mathrm{saturate}(1 - \text{dist}/\text{radius}), \text{FalloffPower}\big)\f$. The summed boost is **soft-clamped**:
   `boost = _MaxBoost * (1 - exp(-sum / _MaxBoost))`, so overlapping flashes approach the ceiling
   asymptotically instead of blowing out. GB is passed through untouched. **Skipped when no source
   contributes**, and an identity on R/GB/A when `_StampCount = 0`.
3. **Gradient** (`Hidden/BalloonParty/SceneLightGradient`) — recomputes GB from `grad(R)` via a central
   difference over `_FieldTexelSize`. The gradient points toward increasing brightness = toward the
   source = the toward-light convention. It blends from the rest GB toward the gradient direction by
   `saturate(localR * _DirectionResponse)` — weighted by how much *local* light is here, so a flat/rest
   field (localR = 0) keeps the rest GB bit-for-bit, and bright local lights capture the direction
   (low local R also means low weight, which suppresses gradient noise where there's no real light).

**Rest invariant.** With no lights registered: fill writes rest, accumulate is skipped (or an exact
identity), and the gradient pass on the flat R field yields a zero gradient → weight 0 →
`lerp(restGB, …, 0) == restGB`. R and A pass through untouched. So the published field is bit-identical
to the rest state, hence bit-identical to the directional system.

## The active-source model

**Lights are state, not events** — a light is simply on or off, so (unlike the disturbance field, where
you fire a stamp and it lingers/diffuses) callers own the lifecycle directly:

- A **`Light`** (`Shared/SceneLight/Light.cs`) is a small reactive model — `Position`, `Radius`,
  `Intensity` (the R magnitude it adds), `PaletteIndex` as `ReactiveProperty`s. The caller creates and
  owns it. The magnitude's radial falloff shape is the per-light `FalloffPower`.
- **`RegisterLight(Light) → IDisposable`** turns it on; disposing the registration turns it off.
  `ClearLights()` turns them all off at once. There is **no decay** in the service: a fade is just the
  caller animating `Intensity` (e.g. a DOTween on the reactive property) — the field follows.
- The service **watches** each registered light's reactive properties and the directional owner's
  direction/intensity, and only re-renders when something actually changed. An idle scene skips the
  whole pipeline; the RT keeps its last (still-correct) contents.

To make a light track something (a projectile, a glowing balloon), the caller mutates `light.Position.Value`
each frame — the change dirties the field and it re-renders. This immediate re-composite replaces the
disturbance `LerpStampScheduler` (which integrates decaying *deltas* into a *persistent* field — the
wrong tool for an on/off light).

> **Naming note:** `Light` collides with `UnityEngine.Light`. Inside the `Shared.SceneLight` namespace it
> resolves to ours; a caller that also `using`s `UnityEngine` needs `using Light = BalloonParty.Shared.SceneLight.Light;`.

## Globals published

- `_SceneLightTex` — the field RT.
- `_SceneLightFieldBoundsMin` / `_SceneLightFieldBoundsSize` (float4, xy) — world-space rectangle for
  the world→field-UV mapping. Distinct from the disturbance field's `_FieldBoundsMin/Size`.
- `_SceneLightFieldOn` (float) — the **fallback switch**: 1 while the service is alive and has filled
  at least once, 0 in `Dispose` and any time the service never ran (edit mode, or no owner in scene).

## Cadence

Renders **only when dirty** — a registered light's reactive property changed, a light was
registered/cleared, or the directional owner's live-tunable direction/intensity moved. An idle scene
(static lights, no owner tweaking) skips the pipeline entirely; the RT keeps its last contents, which
are still correct. This is why lights are reactive: the service subscribes to each one and flips a dirty
flag, rather than polling or re-rendering blindly. The on-flag is set once, after the first render, so a
missing owner leaves it at 0.

Dirty alone isn't the whole gate: a light that moves every frame (a tracked projectile) would otherwise
dirty the field every tick, running the full pipeline at the display's refresh rate — 2× the GPU cost on
a 120 Hz panel versus 60 Hz, for identical visuals. `FieldFrameInterval` (on `ISceneLightFieldSettings`)
is authored as "every N frames at 60 fps" and reinterpreted as seconds, accumulated with
`Time.unscaledDeltaTime` (mirroring `SceneCaptureService`'s capture-cadence knob) — so the render cadence
is capped independent of display refresh. Unscaled time keeps the cadence advancing during a freeze
(e.g. the level-up cinematic), same as the dirty-driven lights animating through it. The very first
render (before the on-flag is set) always fires immediately, regardless of the cap, so consumers never
sample an empty RT.

Past the cadence gate, the service also compares the freshly built stamp batch against the one last
sent to the GPU (texel-quantized epsilons for position/radius, a small absolute epsilon for
magnitude/falloff, exact match for the palette index) and skips the render when nothing changed beyond
what the field's own resolution can show — sub-texel drift, a converged fade tail, or an equal-within-a-texel
rewrite no longer cost a re-render.

## Device builds: the field shaders are serialized

All passes are `Hidden/` shaders (`SceneLightFieldFill`, `SceneLightAccumulate`, `SceneLightGradient`),
which a device build would strip if they were only reached by `Shader.Find`. So the settings SO carries
**serialized `Shader` references** (`FillShader`/`AccumulateShader`/`GradientShader`, mirroring
`IDisturbanceFieldSettings`), which pull them into the build via the asset. **Assign all three on the
`SceneLightFieldSettings` asset** — the resources prefer the serialized reference and fall back to
`Shader.Find` only as an editor convenience (that fallback is stripped on device, so an unassigned slot
breaks the field there).

## The shared include & the off-fallback

`SceneLight.cginc` exposes two layers behind one API:

- the **flat** helpers copied verbatim from the old per-shader blocks — `SceneLightDirection()`,
  `SceneLightTint()`, `ShadowLightFade()` — so migrating a shader is a mechanical *delete the local
  copy, `#include` this*;
- the **field-aware** helpers — `SceneLightDirectionAt(worldPos)`, `SceneLightMagnitudeAt(worldPos)`,
  `SceneLightTintAt(worldPos)`, `ShadowLightFadeAt(worldPos)` (plus `…LOD` variants for vertex-stage / VTF
  consumers) — each of which returns the flat result when `_SceneLightFieldOn < 0.5`.

**Flow helpers (perpendicular-flip fix).** `SceneLightDirectionAt`/`SceneLightFieldDecodeDir` decode a
direction and re-normalize it — correct on average, but the re-normalize is what made local
light-driven accessories SNAP 180° (shadows/glints flipping left↔right, up↔down) whenever a local
light's direction opposed the ambient enough to cancel, or its peak crossed the sample point (the
field's raw GB is already near-zero there, so the sign flip that `normalize()` turns into an instant
180 is a texel-to-texel accident, not a real direction change). `SceneLightFlowAtLOD(worldPos,
receiverRadius)` / `SceneLightLocalFlowAtLOD(worldPos, receiverRadius)` fix this by never normalizing:
they return a **flow** vector whose magnitude IS the confidence in its direction (0 at a cancellation,
not an arbitrary pick), so a consumer that scales its response by that magnitude reads the
cancellation as a fade-through-rest instead of a teleport. `receiverRadius > 0` additionally integrates
the field over a small disc (the anchor plus 4 rim taps) so a light sweeping across a sprite's
footprint turns the flow continuously instead of flipping the instant its peak crosses one texel.
`SpriteLightDriven.shader` is the first consumer: its `_ReceiverRadius` property (0 = point sample,
matching the old cost) exposes the disc radius per material, and its Full/Local rotation modes now
size the swing/orbit amplitude from the flow's own magnitude instead of assuming full confidence —
Ambient mode is unaffected (the flat global direction can't flip). Existing materials get the flip fix
automatically at the default radius of 0; the disc integration is an opt-in per-material tuning knob
on top.


`SceneLightTintAt` also applies the A **palette colour**: it blends from the global key light toward the
tagged light's palette RGB (from the global `_SceneLightPalette[16]` the service pushes) by how far the
local magnitude sits above the ambient rest (`colorAmount = saturate((R − rest) / SCENE_LIGHT_COLOR_RAMP)`),
then × the magnitude. Because R is smooth (bilinear), the **colour edge is as soft as the brightness
falloff** — the intensity channel drives a soft glow rather than a hard hue boundary at the quantised
index texels. The colour *identity* is a plain 2×2 decode-then-blend (`SceneLightPaletteColorAt` — decode
each texel's index to a colour first, then bilinear-blend; never the raw indices, which would band into a
foreign slot). Untagged / field-off is the key light unchanged. Consumers get this through
`SceneLightTintAt` — no per-shader edits.

> An earlier attempt used an edge-*preserving* joint-trilateral (guided by R + direction). It was the
> wrong tool for a soft glow: edge preservation keeps the boundary hard, and the direction term distorts a
> single disc (asymmetric weighting near the radial centre). Softness now comes from the intensity-driven
> fade above plus the field's resolution (`TexelsPerUnit = 32`, far finer than the disturbance field's 8 —
> affordable because the light field only re-renders when dirty, not every frame). Direction is still
> available if colour-vs-colour seams between overlapping lights ever need separating.

(The render-maps preview stays raw/point-sampled on purpose — a field-data inspector, not the consumer view.)

Because nothing includes the file yet and the field publishes an off-flag until it runs, **Phase A
has zero visual effect**: the field OFF is bit-identical to today.

## Phase roadmap

- **A (shipped)** — the field service, rest-state fill, the globals, the
  shared include, the off-fallback.
- **B (shipped)** — pilot consumers include the header and sample `…At(worldPos)` (PuffCloud per-pixel,
  PaintBlob per-object).
- **C (shipped)** — the reactive `Light` model + on/off registry,
  the two new passes (accumulate + gradient), and `LightStampCheat`. The A
  channel now carries real palette indices and R accumulates. The gradient pass (folded in here rather
  than deferred to D) already derives direction from `grad(R)`, so area lights work today.
  Real game sources (balloon pops flashing their colour, then laser/lightning) are the next wiring
  step. In-editor render check still pending (`dotnet build` does not compile shaders).
- **D (shipped)** — every remaining consumer (world bodies + sprite
  family) migrated onto the include; the screen-space GI smear/overlay now sample the field per-fragment
  (direction + magnitude), so lights bend the bounce and shadows. Field-off stays bit-identical.
- **Palette colour decode (shipped)** — `SceneLightTintAt` decodes the
  A index to a palette colour via the global `_SceneLightPalette` (2×2 decode-blend + intensity-driven soft edge); all consumers inherit it.
- **Specks (shipped)** — `SpeckField.shader` samples the field per speck (at its world position, VTF) and
  tints it, tunable **per appearance look**: `LightInfluence` (0 = emissive, ignores light; blends
  base→colour by heat) and `LightMode` (`SceneLightMode` enum — Full / Ambient / Local, resolved per the
  speck's colour slot). Default influence 0, so existing looks are unchanged until an artist opts in.
- **Laser cross (shipped)** — `LaserItemHandler` registers two `Light.Segment` beams (H + V through the
  cross centre, `RaycastDistance` each way), coloured by the host balloon, for the beam effect's
  duration — the first **area-light** consumer. A held (idle) laser can also cast a spinning cross
  telegraph following the item's rotation (`LaserItemRotation`, per-prefab `_castTelegraphLight` toggle,
  off by default — experimental).
- **Bomb (shipped)** — `BombItemHandler` casts a point flash light at the blast, radius ~3× the blast
  radius (visual-scaled for a rainbow bomb), for the effect duration.
- **Lightning (shipped)** — `LightningItemHandler` casts a point flash light at each chain node it pops,
  palette-tagged to the matched target colour, lasting `PopLightSeconds`.
- **First consumer (shipped)** — the projectile registers a small `Light` that follows it and takes its
  colour (Sparks while colourless); see `Projectile/README.md`.
- **Next** — more game-source wiring (balloon pops flashing their colour), further field tuning.
