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
        Capture  [label="SceneCaptureService\nchild camera, depth −1\nsolid-color clear, alpha 0\n+ auto mipmap generation"];
        CapTex   [label="_SceneCaptureTex\n(low-res RT + mip chain)\nRGB = scene, A = coverage", fillcolor="#cfe0f0"];

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

    subgraph cluster_config {
        label="Configuration (SceneLightFieldSettings SO)";
        style=filled;
        fillcolor="#f0e8f5";

        SO [label="IScreenSpaceLightSettings\nISceneLightSettings\nISceneLightFieldSettings\n(single SO, triple interface)"];
        SO -> Pass0 [label="march params\n(via DI)", style=dotted];
        SO -> Overlay [label="shadow/bounce\nstrength", style=dotted];
    }

    subgraph cluster_smear {
        label="ScreenSpaceLightSmear.shader — blit chain";
        style=filled;
        fillcolor="#f5f5dc";

        Pass0 [label="Pass 0 — RSM-style multi-direction gather\nSHADOW: 8-tap toward-light march\n(single direction, _ShadowMipSpread penumbra)\nBOUNCE: 4 directions × 8 taps\n(0°/90°/180°/270° around field dir)\n_SecondaryWeight modulates non-primary"];
        Pass1 [label="Pass 1 — 3×3 box soften"];

        Pass0 -> Pass1;
    }

    LightTex [label="_LightTex (light buffer)\nRGB = multi-dir bounce color\nA = shadow amount", fillcolor="#cfe0f0"];

    subgraph cluster_overlay {
        label="Composite — fullscreen, magnitude-coupled";
        style=filled;
        fillcolor="#e8f5dc";

        Overlay  [label="ScreenSpaceLightOverlay.shader\nbounce × abs(magnitude)\nshadow × relative(R / _MagnitudeRef)\ncolor = 0.5·lerp(white, shadowTint, shadow)\n+ (rgb − ambient)·bounce"];
        Quad     [label="Overlay quad\ncamera-fitted, layer TransparentFX\nSky / 32000, Blend DstColor SrcColor"];

        Overlay -> Quad [label="material"];
    }

    Frame [label="Framebuffer\n(tinted in place)", fillcolor="#ffe8cc"];

    CapTex   -> Pass0    [label="_MainTex\n(mip chain)"];
    Include  -> Pass0    [label="local direction\nper fragment\n(rotates all 4 dirs)", style=bold, color="#2266aa"];
    Include  -> Overlay  [label="local magnitude\nper fragment", style=bold, color="#2266aa"];
    Pass1    -> LightTex;
    LightTex -> Overlay  [label="_LightTex"];
    MainCam  -> Overlay  [label="_AmbientColor\n= camera bg", style=dashed];
    Quad     -> Frame    [label="2·src·dst\n(darken + brighten)"];

    Service [label="ScreenSpaceLightService\n(on Main Camera, DI-registered)\nowns blit chain + quad,\nreads IScreenSpaceLightSettings\n+ ISceneLightSettings", fillcolor="#f5dce8"];
    Service -> Pass0   [label="drives", lhead=cluster_smear, style=dotted];
    Service -> Overlay [label="drives", style=dotted];
    Service -> Capture [label="Acquire/Release", style=dotted];
}
@enddot

## What this diagram shows

A whole-screen fake of a global 2D directional light, now **field-aware** and **multi-directional**:
each fragment reads its local light direction from the scene light field (see @ref arch_light_field)
and gathers bounce color from 4 directions at 90° spacing around it — an RSM-style Virtual Point
Light gather in 2D. Shadow stays single-direction (toward the light) for correct directionality.
When the field is off, the shader falls back to the flat global direction — bit-identical to the
original single-direction march.

**Configuration** lives on the `SceneLightFieldSettings` ScriptableObject, injected via VContainer
as `IScreenSpaceLightSettings` (GI tuning) and `ISceneLightSettings` (ambient direction/colour/
intensity — the former `SceneLightService` MonoBehaviour, now decommissioned). All knobs are
live-tunable in play mode.

**Capture** — `SceneCaptureService` (see @ref disturbance_field's sibling in `Display/`)
renders the captured layers into a low-res RT (with auto-generated mip chain) on a **time-paced
cadence**, clearing to a **solid color with alpha 0**. The mip chain enables cone-march sampling:
near taps read mip 0 (sharp), far taps read higher mips (averaged over wider area). Result:
`RGB` is the composited scene (sprites over the sky clear), `A` is a sprite-coverage mask. This
capture is shared — the Unbreakable chrome reflection is another consumer.

**Smear** (`ScreenSpaceLightSmear`, 2 blit passes at capture resolution):
- **Pass 0** — **RSM-style 4-direction VPL gather**. Shadow and bounce are decoupled:
  - *Shadow* (single direction): 8-tap march toward the light with `_ShadowMipSpread`
    penumbra — sharp at contact, soft at distance. Multiplied by `(1 − ownCoverage)` so
    casters don't self-shadow.
  - *Bounce* (4 directions): each direction is 8 taps with `_MipSpread` cone widening.
    The primary direction (down-light) has weight 1; three secondary directions (±90°, 180°)
    are scaled by `_SecondaryWeight` (0–1). Setting it to 0 collapses to the old
    single-direction march. All 4 directions rotate with the field's local direction, so
    a nearby point light bends the entire gather pattern around it.
- **Pass 1** is a 3×3 box blur that removes the smear's directional streaks. (A former
  Pass 2 — temporal EMA against a ping-pong history — was never enabled in the shipped
  tuning and was removed 2026-07-18.)

**Composite** (`ScreenSpaceLightOverlay`): a camera-fitted quad on `TransparentFX`,
sorting `Sky`/32000 (above gameplay, below UI), drawn with `Blend DstColor SrcColor`
(= `2·src·dst`, neutral at 0.5, so it can darken *and* brighten). The bounce term scales
by the **absolute local magnitude** (field-off == global intensity). The shadow term scales
by the **relative magnitude** (`localR / _MagnitudeRef`) — field-off resolves to 1.0,
preserving authored shadow strength.

## Key design decisions & contracts

1. **Composite quad, not `OnRenderImage`.** A post effect would resolve the full frame
   — the same tile-GPU stall that `GrabPass` caused. The multiplicative quad needs no
   frame readback.
2. **Blit chain runs from `LateUpdate`, not a camera callback.** URP's RenderGraph
   rejects `Graphics.Blit` issued from inside render callbacks. `LateUpdate` runs outside
   the render loop entirely, reading the **previous** frame's capture (invisible at the
   time-paced capture cadence).
3. **The feedback loop is structurally impossible.** The overlay lives on
   `TransparentFX`, excluded from the capture mask — the capture always sees the *unlit*
   scene.
4. **Shadow is caster-masked.** `(1 − ownCoverage)` keeps the cast shadow on open
   ground.
5. **Shaders are serialized references on the SO, not `Shader.Find`.** Device builds
   strip name-only shader lookups; `Shader.Find` is an editor-only fallback.
6. **Per-fragment field direction.** Each fragment reads its own local light direction
   from the field — nearby lights bend the GI around them. Field-off returns the flat
   global everywhere.
7. **Multi-direction bounce (RSM-style).** 4 cone marches at 90° spacing gather indirect
   color from all quadrants. `_SecondaryWeight = 0` collapses to single-direction.
8. **Shadow mip penumbra.** `_ShadowMipSpread` (steeper than bounce spread) gives
   distance-dependent penumbra: sharp contact shadows, soft far from the caster.
9. **All config on SO (DI).** The service holds no tuning fields — everything comes from
   `IScreenSpaceLightSettings` / `ISceneLightSettings`, injected by VContainer.

## Deliberately not built

Balloons *receiving* GI shadows. Coverage alone can't separate a caster's own body from
another caster stacked in front of it, so the mask that stops self-shadowing also stops
casters from being shadowed by others. Per-balloon drop-shadows are covered by the baked
shadows (`SpriteShadowBaker`); the GI handles large-scale occlusion from bushes and
clusters.

## In-editor setup

1. `ScreenSpaceLightService` sits on the **Main Camera**, beside `SceneCaptureService`;
   the smear/overlay shader refs are on the `SceneLightFieldSettings` SO.
2. The capture mask excludes `TransparentFX` and the background, and includes what
   should cast/bounce (Grid, Balloons, …).
3. All knobs on the SO (`SceneLightFieldSettings`) tune live in play mode; disable the
   service component to A/B. Its inspector previews the bounce (RGB) and shadow (A)
   buffers.

## Cost

Three blits at capture resolution (~150×70 px) per captured frame + one fullscreen
alpha-blended quad with a single low-res fetch. Pass 0 now does 4×8 + 8 = 40 taps per
pixel (vs. the old 16), but at 135×67 (~9K texels) this is still ~360K texture samples
— trivial on mobile at the time-paced cadence.
