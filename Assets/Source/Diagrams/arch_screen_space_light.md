@page arch_screen_space_light Screen-Space Light (2D GI)

# Screen-Space Light (2D GI)

@dot
digraph ScreenSpaceLight {
    rankdir=TB;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_capture {
        label="Capture — time-paced, shared";
        style=filled;
        fillcolor="#dce8f5";

        MainCam  [label="Main Camera\n(orthographic, static)"];
        Capture  [label="SceneCaptureService\nchild camera, depth −1\nsolid-color clear, alpha 0"];
        CapTex   [label="_SceneCaptureTex\n(low-res RT)\nRGB = scene, A = coverage", fillcolor="#cfe0f0"];

        MainCam -> Capture [label="framing, bg color"];
        Capture -> CapTex  [label="renders\ncaptured layers"];
    }

    subgraph cluster_field {
        label="Scene Light Field (see @ref arch_light_field)";
        style=filled;
        fillcolor="#e0f0e8";

        FieldRT  [label="_SceneLightTex\nR = local boost\nGB = direction weight\nA = palette index", fillcolor="#cfe0f0"];
        Include  [label="SceneLight.cginc\nSceneLightDirectionAt(worldPos)\nSceneLightMagnitudeAt(worldPos)"];
        FlatFall [label="Field-OFF fallback\n→ flat _SceneLightDir\n→ _SceneLightIntensity", style=dashed];

        FieldRT -> Include;
        FlatFall -> Include [style=dashed];
    }

    subgraph cluster_smear {
        label="ScreenSpaceLightSmear.shader — blit chain";
        style=filled;
        fillcolor="#f5f5dc";

        Pass0 [label="Pass 0 — per-fragment field-directional smear\nrgb: march TOWARD local light dir (bleed)\na: march AWAY, × (1−ownCoverage)\n(direction from SceneLightDirectionAt)"];
        Pass1 [label="Pass 1 — 3×3 box soften"];
        Pass2 [label="Pass 2 — temporal EMA\nvs ping-pong history\n(_temporalResponse)"];

        Pass0 -> Pass1 -> Pass2;
    }

    LightTex [label="_LightTex (light buffer)\nRGB = smeared scene color\nA = shadow amount", fillcolor="#cfe0f0"];

    subgraph cluster_overlay {
        label="Composite — fullscreen, magnitude-coupled";
        style=filled;
        fillcolor="#e8f5dc";

        Overlay  [label="ScreenSpaceLightOverlay.shader\nbounce × abs(magnitude)\nshadow × relative(R / _MagnitudeRef)\ncolor = 0.5·lerp(white, shadowTint, shadow)\n+ (rgb − ambient)·bounce"];
        Quad     [label="Overlay quad\ncamera-fitted, layer TransparentFX\nSky / 32000, Blend DstColor SrcColor"];

        Overlay -> Quad [label="material"];
    }

    Frame [label="Framebuffer\n(tinted in place)", fillcolor="#ffe8cc"];

    CapTex   -> Pass0    [label="_MainTex"];
    Include  -> Pass0    [label="local direction\nper fragment", style=bold, color="#2266aa"];
    Include  -> Overlay  [label="local magnitude\nper fragment", style=bold, color="#2266aa"];
    Pass2    -> LightTex;
    LightTex -> Overlay  [label="_LightTex"];
    MainCam  -> Overlay  [label="_AmbientColor\n= camera bg", style=dashed];
    Quad     -> Frame    [label="2·src·dst\n(darken + brighten)"];

    Service [label="ScreenSpaceLightService\n(on Main Camera)\nowns blit chain + quad,\npushes _TapStepScale + _TapAspect\n+ _BounceStrength + _MagnitudeRef", fillcolor="#f5dce8"];
    Service -> Pass0   [label="drives", lhead=cluster_smear, style=dotted];
    Service -> Overlay [label="drives", style=dotted];
    Service -> Capture [label="Acquire/Release", style=dotted];
}
@enddot

## What this diagram shows

A whole-screen fake of a global 2D directional light, now **field-aware**: each fragment
reads its local light direction and magnitude from the scene light field (see @ref
arch_light_field) so point/area lights bend the bleed and shadows around them. When the
field is off, the shader falls back to the flat global direction — bit-identical to the
original single-direction march.

**Capture** — `SceneCaptureService` (see @ref disturbance_field's sibling in `Display/`)
renders the captured layers into a low-res RT on a **time-paced cadence**, clearing to
a **solid color with alpha 0**. Its own `LateUpdate` accumulates `Time.unscaledDeltaTime`
and fires once that passes `SceneCaptureFrameInterval / 60` seconds — the field reads as
"every Nth frame" but is interpreted as seconds at a 60 fps reference, so this chain's
cost doesn't scale with display refresh (a 120 Hz panel would otherwise double it).
Result: `RGB` is the composited scene (sprites over the sky clear), `A` is a
sprite-coverage mask. This capture is shared — the Unbreakable chrome reflection is
another consumer. `ScreenSpaceLightService` drives the blit chain below from its own
`LateUpdate`, which runs *before* the capture camera renders that frame — so each build
reads the **previous** frame's capture. One frame of staleness is invisible on a buffer
that's already temporally blended (Pass 2) and only refreshes on the same time-paced
cadence.

**Smear** (`ScreenSpaceLightSmear`, 3 blit passes at capture resolution):
- **Pass 0** — **per-fragment field-directional**. Each fragment maps its capture UV to
  world position through the field-bounds globals and reads the local toward-light
  direction via `SceneLightDirectionAt(worldPos)`. The 8-tap march follows this local
  direction instead of the old single global step — so a point/area light bends the
  bleed and shadows around it organically. `RGB` marches toward the local light,
  accumulating the composited scene color (bleed). `A` marches away, accumulating
  occluder coverage × `(1 − ownCoverage)` (shadow on open ground). The service pushes
  `_TapStepScale` + `_TapAspect` (world→UV conversion factors); the shader builds the
  per-fragment step from those + the local unit direction.
- **Pass 1** is a 3×3 box blur that removes the smear's directional streaks.
- **Pass 2** is a temporal EMA against a ping-pong history buffer: at capture resolution
  a moving sprite jumps whole texels per frame and the bounce flickers; folding each
  build gradually integrates that away, and the light's low frequency hides the lag.

**Composite** (`ScreenSpaceLightOverlay`): a camera-fitted quad on `TransparentFX`,
sorting `Sky`/32000 (above gameplay, below UI), drawn with `Blend DstColor SrcColor`
(= `2·src·dst`, neutral at 0.5, so it can darken *and* brighten). It samples only the
light buffer — the frame is tinted in place by the blend unit, nothing is read back.
The bounce term now scales by the **absolute local magnitude** (field-off R == global
intensity, matching the old `_bounceStrength × intensity` product). The shadow term
scales by the **relative magnitude** (`localR / _MagnitudeRef`) — field-off resolves to
1.0, so authored shadow strength is unchanged. This per-fragment coupling means a dim
region casts weaker, shorter shadows and a bright local light intensifies the bounce —
resolving the old open question of whether GI strength should track light intensity.

## Key design decisions & contracts

1. **Composite quad, not `OnRenderImage`.** A post effect would resolve the full frame
   — the same tile-GPU stall that `GrabPass` caused and 5b removed. The multiplicative
   quad needs no frame readback.
2. **Blit chain runs from `LateUpdate`, not a camera callback.** The URP migration's
   first attempt drove the three blits from `RenderPipelineManager.beginCameraRendering`
   (the timing-equivalent of the old Built-in `OnPreRender`), but URP's RenderGraph
   rejects a `Graphics.Blit` issued from inside that callback ("EndRenderPass: Not
   inside a Renderpass"). `LateUpdate` — the disturbance field's already-proven pattern
   — runs outside the render loop entirely, at the cost of reading the previous frame's
   capture (see above).
3. **The feedback loop is structurally impossible.** The overlay lives on
   `TransparentFX`, which **must stay excluded from the capture mask** — the capture
   always sees the *unlit* scene, so frame N's lighting can never feed frame N+1's input.
4. **The background must stay out of the capture.** A full-coverage background plane
   reads as a caster everywhere and whites out the shadow (`1 − ownCoverage → 0`). The
   ambient sky the bounce needs comes from the capture's **clear color**, not from
   capturing a background sprite.
5. **Shadow is caster-masked.** `(1 − ownCoverage)` keeps the cast shadow on open
   ground; without it a caster samples its own coverage and darkens into a centered
   blob instead of throwing a shadow beside itself.
6. **Shaders are serialized references, not `Shader.Find`.** Device builds strip
   name-only shader lookups; `Shader.Find` remains only as an editor fallback, and the
   service disables itself with a warning if neither resolves.
7. **Per-fragment field direction (new).** The smear no longer marches one global direction.
   Each fragment reads its own local light direction from the field — so a nearby point or
   area light bends the GI around it. Field-off returns the flat global everywhere,
   reproducing the old single-direction behaviour identically.
8. **Magnitude coupling (new).** Bounce and shadow scale per-fragment by the field's
   local magnitude R. Bounce uses absolute R (field-off == global intensity, matching
   the old CPU product). Shadow uses relative `R / _MagnitudeRef` (field-off == 1.0,
   preserving authored shadow strength). A dim region naturally gets weaker, shorter
   shadows; a bright local light intensifies the bounce.

## Deliberately not built

Balloons *receiving* GI shadows. Coverage alone can't separate a caster's own body from
another caster stacked in front of it, so the mask that stops self-shadowing also stops
casters from being shadowed by others. Per-balloon drop-shadows are covered instead by
the baked shadows (`SpriteShadowBaker`); the GI handles large-scale occlusion from
bushes and clusters.

## In-editor setup

1. `ScreenSpaceLightService` sits on the **Main Camera**, beside `SceneCaptureService`;
   assign the two shader references (`ScreenSpaceLightSmear`, `ScreenSpaceLightOverlay`).
2. The capture mask excludes `TransparentFX` and the background, and includes what
   should cast/bounce (Grid, Balloons, …).
3. All knobs (light direction, smear distance, tap decay/start, shadow strength/tint,
   bounce strength, temporal response, sorting) are serialized on the service and tune
   live in play mode; disable the component to A/B. Its inspector previews the bounce
   (RGB) and shadow (A) buffers.

## Cost

Three blits at capture resolution (~150×70 px) per captured frame + one fullscreen
alpha-blended quad with a single low-res fetch — comparable to a vignette. The service
holds a capture `Acquire`, so disabling it releases the shared capture when nothing
else uses it.
