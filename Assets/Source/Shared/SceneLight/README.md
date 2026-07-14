# Scene Light Field

The **light field** — the disturbance-field architecture applied to light (see @ref plan_lighting,
"Milestone 3"). A small screen/world-space RenderTexture that every direction/specular consumer can
sample once at its anchor to get the local light *direction + magnitude + colour*, regardless of how
many lights contribute. This folder is the field's **producer**; consumers read it through the
shared shader include `Assets/Shaders/BalloonParty/Include/SceneLight.cginc`.

`SceneLightService` (in `Display/`) stays the owner of the single *directional* key light and its
flat globals (`_SceneLightDir` / `_SceneLightColor` / `_SceneLightIntensity`). The field is a
super-set layered on top: at rest it is exactly those globals painted uniformly, and every field
helper falls back to the flat globals when the field is off — so the field is a strictly additive
seam, never a replacement.

## Contents

| File | What it provides |
|---|---|
| `SceneLightFieldService` | Plain-C# DI service (`IStartable`/`ITickable`/`IDisposable`), registered in `GameScopeRegistration` next to `DisturbanceFieldService` (Singleton, `AsImplementedInterfaces().AsSelf()`). Builds the field via `SceneLightFieldResources`, sizes it through the shared `DisturbanceFieldCoordinates` (driven by `IGameDisplayConfiguration`), and runs the three-pass pipeline (below) — but only on ticks where a registered light or the directional owner changed. Holds the registry of on lights, subscribing to each one's reactive properties to know when to re-render. Resolves `SceneLightService` once via `FindFirstObjectByType` (a scene object, unreachable from a shared prefab — the `ScreenSpaceLightService` precedent); if it's absent it logs once and leaves the field off. Pushes the bounds globals + on-flag; releases the RTs in `Dispose`. API: `RegisterLight(Light) → IDisposable`, `ClearLights()` |
| `SceneLightFieldResources` | The field's GPU resources — **two** ping-pong RTs (`ARGBHalf` where supported, else `ARGB32`) and the fill / accumulate / gradient materials — kept separate from the service's logic, mirroring `DisturbanceFieldResources`. Owns the `_SceneLightTex` global push, `BlitAndSwap`, the `Fill(magnitude, direction)` and `Gradient()` passes, and exposes `AccumulateMaterial` for the service to upload the light arrays onto |
| `Light` | A small reactive model a caller owns: `Position` / `Radius` / `Intensity` / `PaletteIndex` as `ReactiveProperty`s plus an authored `EdgeSoftness`, with `const` defaults. On/off is `RegisterLight`/dispose; brightness is `Intensity` (the R magnitude); there is no built-in decay. **Not** a config ScriptableObject — nothing to author headless. (Name collides with `UnityEngine.Light` — alias when both are in scope.) |

## Channel encoding (single RT)

| Channel | Meaning | Rest value |
|---|---|---|
| **R** | light **magnitude** — the global intensity at rest; the accumulate pass adds each registered light's magnitude on top (soft-clamped) | `_SceneLightIntensity` (≈ 1) |
| **G/B** | 0.5-biased 2D **direction** (`gb = dir * 0.5 + 0.5`); toward-light, world/screen XY. The gradient pass recomputes it from `grad(R)` so it points toward the brightest nearby source | the global `_SceneLightDir` everywhere |
| **A** | palette-colour **index**, `(index+1)/16`; **0 = "no colour, use `_SceneLightColor`"**. The accumulate pass writes the palette index of the light that contributes most at each texel | `0` exactly |

## The render pipeline (three passes, ping-pong)

A render rebuilds the whole field from scratch (there's no in-texture persistence like the disturbance
field's diffusion) via a three-pass ping-pong over the two RTs. It runs only when the field is dirty
(a registered light or the directional owner changed) — see the cadence note below:

1. **Fill** (`Hidden/BalloonParty/SceneLightFieldFill`) — writes the rest state: R = the owner's
   intensity, GB = the 0.5-biased `_SceneLightDir`, A = 0. This is the read buffer the chain builds on.
2. **Accumulate** (`Hidden/BalloonParty/SceneLightAccumulate`, batched — up to 32 lights/blit,
   `Vector4[]`/`float[]` uploads, aspect-corrected radial falloff, `(index+1)/16` palette encoding,
   mirroring `DisturbanceStampBatched`) — ADDS each registered light's magnitude into R
   and writes the dominant light's palette index into A. The summed boost is **soft-clamped**:
   `boost = _MaxBoost * (1 - exp(-sum / _MaxBoost))`, so overlapping flashes approach the ceiling
   asymptotically instead of blowing out. GB is passed through untouched. **Skipped when no source
   contributes**, and an identity on R/GB/A when `_StampCount = 0`.
3. **Gradient** (`Hidden/BalloonParty/SceneLightGradient`) — recomputes GB from `grad(R)` via a central
   difference over `_FieldTexelSize`. The gradient points toward increasing brightness = toward the
   source = the toward-light convention. It blends smoothly from the rest GB toward the gradient
   direction by `smoothstep(_GradientLo, _GradientHi, |grad R|)` — a lerp weighted **exactly 0** on a
   flat field, so the rest GB passes through bit-for-bit (no seam, no rest drift).

**Rest invariant.** With no lights registered: fill writes rest, accumulate is skipped (or an exact
identity), and the gradient pass on the flat R field yields a zero gradient → weight 0 →
`lerp(restGB, …, 0) == restGB`. R and A pass through untouched. So the published field is bit-identical
to the rest state, hence bit-identical to the directional system.

## The active-source model

**Lights are state, not events** — a light is simply on or off, so (unlike the disturbance field, where
you fire a stamp and it lingers/diffuses) callers own the lifecycle directly:

- A **`Light`** (`Shared/SceneLight/Light.cs`) is a small reactive model — `Position`, `Radius`,
  `Intensity` (the R magnitude it adds), `PaletteIndex` as `ReactiveProperty`s, plus an authored
  `EdgeSoftness`. The caller creates and owns it.
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

## Deferred: device-build shader registration & SO authoring

All three passes are Hidden shaders resolved by `Shader.Find`, because the plain-C# service can't carry
serialized `Shader` references the way the disturbance config SO does. Hidden shaders reached only via
`Shader.Find` are stripped from device builds unless they're registered as **Always-Included** (or moved
behind a config SO that references them) — that registration is deferred for all three
(`SceneLightFieldFill`, `SceneLightAccumulate`, `SceneLightGradient`). Phase C is **editor-verified
only** until then. (There's no light config SO to defer — a `Light` is a plain runtime value callers
build directly.)

## The shared include & the off-fallback

`SceneLight.cginc` exposes two layers behind one API:

- the **flat** helpers copied verbatim from the old per-shader blocks — `SceneLightDirection()`,
  `SceneLightTint()`, `ShadowLightFade()` — so migrating a shader is a mechanical *delete the local
  copy, `#include` this*;
- the **field-aware** helpers — `SceneLightDirectionAt(worldPos)`, `SceneLightMagnitudeAt(worldPos)`,
  `SceneLightTintAt(worldPos)`, `ShadowLightFadeAt(worldPos)` (plus `…LOD` variants for vertex-stage / VTF
  consumers) — each of which returns the flat result when `_SceneLightFieldOn < 0.5`.

`SceneLightTintAt` also decodes the A **palette colour**: where a light tagged a region, it returns that
palette entry's RGB (from the global `_SceneLightPalette[16]` the service pushes) × the local magnitude;
untagged / field-off falls back to the global key light unchanged. The colour is a **decode-then-blend
bilinear** over the 2×2 texel neighbourhood (`_SceneLightTexelSize`) — each texel's index is decoded to a
colour *first*, then the colours are blended, so regions stay smooth without an interpolated index banding
into a foreign palette slot (a plain bilinear tap of A would decode to a wrong third colour). Consumers get
colour for free through `SceneLightTintAt`.

Because nothing includes the file yet and the field publishes an off-flag until it runs, **Phase A
has zero visual effect**: the field OFF is bit-identical to today.

## Phase roadmap (this is Phase C)

- **A (done, editor-verification pending)** — the field service, rest-state fill, the globals, the
  shared include, the off-fallback.
- **B (done)** — pilot consumers include the header and sample `…At(worldPos)` (PuffCloud per-pixel,
  PaintBlob per-object).
- **C (code-complete, editor-verification pending)** — the reactive `Light` model + on/off registry,
  the two new passes (accumulate + gradient), and `LightStampCheat`. The A
  channel now carries real palette indices and R accumulates. The gradient pass (folded in here rather
  than deferred to D) already derives direction from `grad(R)`, so area lights work today.
  Real game sources (balloon pops flashing their colour, then laser/lightning) are the next wiring
  step. In-editor render check still pending (`dotnet build` does not compile shaders).
- **D (code-complete, editor-verification pending)** — every remaining consumer (world bodies + sprite
  family) migrated onto the include; the screen-space GI smear/overlay now sample the field per-fragment
  (direction + magnitude), so lights bend the bounce and shadows. Field-off stays bit-identical.
- **Palette colour decode (code-complete, editor-verification pending)** — `SceneLightTintAt` decodes the
  A index to a palette colour via the global `_SceneLightPalette` (decode-then-blend bilinear); all consumers inherit it.
- **Next** — real game-source wiring (balloon pops flashing their colour, laser/lightning as lights) now
  that `RegisterLight` + coloured tint exist.
