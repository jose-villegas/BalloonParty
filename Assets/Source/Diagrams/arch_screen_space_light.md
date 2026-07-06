@page arch_screen_space_light Screen-Space Light (2D GI)

# Screen-Space Light (2D GI)

@dot
digraph ScreenSpaceLight {
    rankdir=TB;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_capture {
        label="Capture — every Nth frame (shared)";
        style=filled;
        fillcolor="#dce8f5";

        MainCam  [label="Main Camera\n(orthographic, static)"];
        Capture  [label="SceneCaptureService\nchild camera, depth −1\nsolid-color clear, alpha 0"];
        CapTex   [label="_SceneCaptureTex\n(low-res RT)\nRGB = scene, A = coverage", fillcolor="#cfe0f0"];

        MainCam -> Capture [label="framing, bg color"];
        Capture -> CapTex  [label="renders\ncaptured layers"];
    }

    subgraph cluster_smear {
        label="ScreenSpaceLightSmear.shader — blit chain";
        style=filled;
        fillcolor="#f5f5dc";

        Pass0 [label="Pass 0 — directional smear\nrgb: march TOWARD light (bleed)\na: march AWAY, × (1−ownCoverage)\n(cast shadow on open ground)"];
        Pass1 [label="Pass 1 — 3×3 box soften"];
        Pass2 [label="Pass 2 — temporal EMA\nvs ping-pong history\n(_temporalResponse)"];

        Pass0 -> Pass1 -> Pass2;
    }

    LightTex [label="_LightTex (light buffer)\nRGB = smeared scene color\nA = shadow amount", fillcolor="#cfe0f0"];

    subgraph cluster_overlay {
        label="Composite — fullscreen, no readback";
        style=filled;
        fillcolor="#e8f5dc";

        Overlay  [label="ScreenSpaceLightOverlay.shader\ncolor = 0.5·lerp(white, shadowTint, a·strength)\n+ (rgb − ambient)·bounce"];
        Quad     [label="Overlay quad\ncamera-fitted, layer TransparentFX\nSky / 32000, Blend DstColor SrcColor"];

        Overlay -> Quad [label="material"];
    }

    Frame [label="Framebuffer\n(tinted in place)", fillcolor="#ffe8cc"];

    CapTex   -> Pass0    [label="_MainTex"];
    Pass2    -> LightTex;
    LightTex -> Overlay  [label="_LightTex"];
    MainCam  -> Overlay  [label="_AmbientColor\n= camera bg", style=dashed];
    Quad     -> Frame    [label="2·src·dst\n(darken + brighten)"];

    Service [label="ScreenSpaceLightService\n(on Main Camera)\nowns blit chain + quad,\npushes knobs, Acquires capture", fillcolor="#f5dce8"];
    Service -> Pass0   [label="drives", lhead=cluster_smear, style=dotted];
    Service -> Overlay [label="drives", style=dotted];
    Service -> Capture [label="Acquire/Release", style=dotted];
}
@enddot

## What this diagram shows

A whole-screen fake of a global 2D directional light (~45°, matching the PuffCloud
`_LightDir` convention). It runs entirely off the shared low-res scene capture and a
final composite quad — **no existing material or shader is touched**, and there is no
post-processing readback.

**Capture** — `SceneCaptureService` (see @ref disturbance_field's sibling in `Display/`)
renders the captured layers into a low-res RT every Nth frame, clearing to a **solid
color with alpha 0**. Result: `RGB` is the composited scene (sprites over the sky
clear), `A` is a sprite-coverage mask. This capture is shared — the Unbreakable chrome
reflection is another consumer.

**Smear** (`ScreenSpaceLightSmear`, 3 blit passes at capture resolution):
- **Pass 0** does two opposite marches per pixel. `RGB` marches *toward* the light,
  accumulating the composited scene color — a lit neighbour bleeds its color onto this
  pixel (reflection/bleed, lands on the side facing the source). `A` marches *away* from
  the light, accumulating occluder coverage, then multiplies by `(1 − ownCoverage)` — an
  occluder between this pixel and the source darkens it (shadow, lands on the far side).
- **Pass 1** is a 3×3 box blur that removes the smear's directional streaks.
- **Pass 2** is a temporal EMA against a ping-pong history buffer: at capture resolution
  a moving sprite jumps whole texels per frame and the bounce flickers; folding each
  build gradually integrates that away, and the light's low frequency hides the lag.

**Composite** (`ScreenSpaceLightOverlay`): a camera-fitted quad on `TransparentFX`,
sorting `Sky`/32000 (above gameplay, below UI), drawn with `Blend DstColor SrcColor`
(= `2·src·dst`, neutral at 0.5, so it can darken *and* brighten). It samples only the
light buffer — the frame is tinted in place by the blend unit, nothing is read back.
The bounce term is measured **relative to the ambient sky** (`(rgb − ambient)`, ambient
pushed from the camera background): flat sky nets to zero (no global tint), a bright
sprite pushes positive (brightens neighbours in its hue), a dark/black sprite pushes
negative (absorbs — darkens them).

## Key design decisions & contracts

1. **Composite quad, not `OnRenderImage`.** A post effect would resolve the full frame
   — the same tile-GPU stall that `GrabPass` caused and 5b removed. The multiplicative
   quad needs no frame readback.
2. **The feedback loop is structurally impossible.** The overlay lives on
   `TransparentFX`, which **must stay excluded from the capture mask** — the capture
   always sees the *unlit* scene, so frame N's lighting can never feed frame N+1's input.
3. **The background must stay out of the capture.** A full-coverage background plane
   reads as a caster everywhere and whites out the shadow (`1 − ownCoverage → 0`). The
   ambient sky the bounce needs comes from the capture's **clear color**, not from
   capturing a background sprite.
4. **Shadow is caster-masked.** `(1 − ownCoverage)` keeps the cast shadow on open
   ground; without it a caster samples its own coverage and darkens into a centered
   blob instead of throwing a shadow beside itself.
5. **Shaders are serialized references, not `Shader.Find`.** Device builds strip
   name-only shader lookups; `Shader.Find` remains only as an editor fallback, and the
   service disables itself with a warning if neither resolves.

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
