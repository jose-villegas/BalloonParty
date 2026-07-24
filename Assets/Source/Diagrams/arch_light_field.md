@page arch_light_field Scene Light Field (2D Multi-Light)

# Scene Light Field (2D Multi-Light)

@dot
digraph SceneLightField {
    rankdir=TB;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_producers {
        label="Producers — game sources register lights";
        style=filled;
        fillcolor="#dce8f5";

        Projectile [label="ProjectileView\npoint light, tracks position\ncolor follows projectile"];
        Laser      [label="LaserItemHandler\n2× capsule/segment lights\n(H + V cross beams)"];
        Bomb       [label="BombItemHandler\npoint light at blast center\nradius ×3 blast"];
        Lightning  [label="LightningItemHandler\npoint light per chain node\n(on each jump)"];
        Cheat      [label="LightStampCheat\n(editor testing)"];
    }

    subgraph cluster_model {
        label="Reactive Light model";
        style=filled;
        fillcolor="#f5e8dc";

        Light [label="Light (plain C#)\nPosition / EndPosition\nRadius / Intensity\nFalloffPower / PaletteIndex\n(all ReactiveProperty<T>)", fillcolor="#fff5e0"];
        Segment [label="Light.Segment(start, end, …)\ncapsule / area light"];

        Light -> Segment [label="factory", style=dashed, dir=none];
    }

    subgraph cluster_service {
        label="SceneLightFieldService — singleton, IStartable / ITickable / IDisposable";
        style=filled;
        fillcolor="#f5f5dc";

        Registry [label="On/Off Registry\nRegisterLight(Light) → IDisposable\nClearLights()"];
        Dirty    [label="Dirty flag\n(reactive subscriptions\nflip on property change)"];
        Pipeline [label="3-pass pipeline\n(dirty + cadence cap)"];

        Registry -> Dirty [label="subscribe\nto each light"];
        Dirty -> Pipeline [label="trigger\nre-render"];
    }

    subgraph cluster_pipeline {
        label="Render pipeline — ping-pong over two ARGBHalf RTs";
        style=filled;
        fillcolor="#e8f5dc";

        Fill       [label="Pass 1 — Fill\nR=0, GB=0.5, A=0\n(rest state)"];
        Accumulate [label="Pass 2 — Accumulate\nbatched ≤32 lights/blit\ncapsule falloff\nsoft-clamp R\ndominant palette → A"];
        Gradient   [label="Pass 3 — Gradient\ngrad(R) central diff\nblend rest→localDir\nby saturate(R·response)"];

        Fill -> Accumulate -> Gradient;
    }

    FieldRT [label="_SceneLightTex (global RT)\nR = local boost\nGB = direction weight\nA = palette index", fillcolor="#cfe0f0"];

    subgraph cluster_globals {
        label="Published globals";
        style=filled;
        fillcolor="#f0f0f0";

        Bounds  [label="_SceneLightFieldBoundsMin / Size\n(world-space rect)"];
        OnFlag  [label="_SceneLightFieldOn\n(0 until first render)"];
        Palette [label="_SceneLightPalette[16]\n(game palette RGB)"];
    }

    subgraph cluster_consumers {
        label="Consumers — sample once at anchor";
        style=filled;
        fillcolor="#f5dce8";

        Include [label="SceneLight.cginc\nSceneLightDirectionAt(worldPos)\nSceneLightMagnitudeAt(worldPos)\nSceneLightTintAt(worldPos)\nShadowLightFadeAt(worldPos)", fillcolor="#ffe8f5"];

        GI       [label="ScreenSpaceLightService\nper-fragment field direction\n+ magnitude coupling"];
        PuffCloud [label="PuffCloud.shader\n(per-pixel)"];
        PaintBlob [label="PaintBlob.shader\n(per-object VTF)"];
        Bodies   [label="World bodies\nTough / Soap / Rainbow\nUnbreakable / BushLeaf"];
        Sprites  [label="Sprite family\nShadow / Shine\nLightDriven / Diffuse"];
        Specks   [label="SpeckField.shader\n(per-speck VTF)"];
    }

    /* Producer → Model */
    Projectile -> Light [label="new Light(…)"];
    Laser      -> Segment [label="Light.Segment(…)"];
    Bomb       -> Light [label="new Light(…)"];
    Lightning  -> Light [label="new Light(…)"];
    Cheat      -> Light [label="new Light(…)"];

    /* Model → Service */
    Light    -> Registry [label="RegisterLight\n→ IDisposable"];

    /* Service → Pipeline */
    Pipeline -> Fill [lhead=cluster_pipeline, style=dotted];

    /* Pipeline → Output */
    Gradient -> FieldRT;

    /* Output → Globals */
    FieldRT -> Bounds [style=dashed];
    FieldRT -> OnFlag [style=dashed];
    FieldRT -> Palette [style=dashed];

    /* Globals → Include */
    FieldRT -> Include [label="texture sample"];
    Bounds  -> Include [style=dashed];
    OnFlag  -> Include [label="off-fallback\n(flat globals)", style=dashed];

    /* Include → Consumers */
    Include -> GI [label="direction\n+ magnitude"];
    Include -> PuffCloud;
    Include -> PaintBlob;
    Include -> Bodies;
    Include -> Sprites;
    Include -> Specks;

    /* Flat globals (rest / field-off) */
    SceneLightSettings [label="TimeOfDayService\n(ambient owner, reads\nISceneLightSettings)\n_SceneLightDir\n_SceneLightColor\n_SceneLightIntensity", fillcolor="#dce8f5"];
    SceneLightSettings -> Include [label="flat fallback\n(field OFF)", style=dashed];
    SceneLightSettings -> Fill [label="rest state\n= these globals", style=dashed];
}
@enddot

## What this diagram shows

The **scene light field** — a screen/world-space RT that collapses N point/area lights into a
single per-position composite (direction + magnitude + colour) that every consumer samples once.
The architecture mirrors the disturbance field: a batched stamp pipeline producing a global RT,
shared coordinates, and a cginc include for consumers.

**Producers** are game sources that own a reactive `Light` model and register it with the service.
Each light is a small C# object (`Position`/`EndPosition`/`Radius`/`Intensity`/`FalloffPower`/
`PaletteIndex` as `ReactiveProperty`s). Point lights have `EndPosition == Position` (disc falloff);
area lights set them apart (capsule — the laser cross is two of these). Registration is
`RegisterLight(Light) → IDisposable`; disposing turns it off. No built-in decay — a fade is the
caller animating `Intensity`.

**The service** (`SceneLightFieldService`, singleton `IStartable`/`ITickable`/`IDisposable`)
subscribes to each registered light's reactive properties. When anything changes it flips a dirty
flag; `Tick` re-renders only when dirty AND a frame-interval cadence cap (`FieldFrameInterval`,
authored as "every N frames at 60 fps") has elapsed — so a light that dirties the field every frame
(a tracked projectile) can't run the pipeline faster than its authored cadence, regardless of display
refresh rate. An idle scene (static lights, no movement) skips the pipeline entirely — the RT keeps
its last, still-correct contents.

**The pipeline** (three blit passes over two ping-pong RTs):

1. **Fill** — clears to rest (R=0, GB=0.5 neutral, A=0). The rest state encodes "no local light" —
   the ambient comes from the globals `TimeOfDayService` pushes, not from the field.
2. **Accumulate** — batches up to 32 lights per blit (mirrors `DisturbanceStampBatched`). Each
   light is a capsule: falloff uses aspect-corrected distance to the segment `[start, end]`.
   R is soft-clamped
   (\f$ \mathit{MaxBoost}\cdot\big(1-e^{-\mathit{sum}/\mathit{MaxBoost}}\big) \f$); A writes
   the dominant light's palette index (\f$ (\mathit{index}+1)/16 \f$). GB passes through.
3. **Gradient** — derives direction from `grad(R)` via central difference. Blends from rest GB
   toward the gradient direction by \f$ \mathrm{saturate}(\mathit{localR}\cdot \mathit{DirectionResponse}) \f$
   — presence-weighted, so flat regions keep the global direction and bright regions capture
   the local one.

**Consumers** read the field through `SceneLight.cginc` (`SceneLightDirectionAt`, `…MagnitudeAt`,
`…TintAt`, `ShadowLightFadeAt`). When `_SceneLightFieldOn < 0.5` (field off, edit mode, or no
service alive) all helpers return the flat globals — bit-identical to the pre-field state. This
makes the field a strictly additive seam.

## Key design decisions

1. **Lights are state, not events.** Unlike the disturbance field (fire a stamp, it lingers/diffuses),
   a light is simply on or off. The caller owns the lifecycle — there is no decay in the service.
   A flash is just the caller DOTweening `Intensity` to zero and disposing.

2. **Direction is derived, not stamped.** The gradient pass computes direction from `grad(R)` —
   the heightfield-normal trick. This makes area lights trivial: authors paint brightness only
   (capsule stamps for beams) and every shape gets plausible directions automatically.

3. **The field is purely local.** R stores only the local boost above ambient; the ambient magnitude
   and direction come from the globals `TimeOfDayService` pushes, added by the include's helpers. This
   means ambient tweaks (day/night, fade-outs) never dirty the field and never re-render it.

4. **Palette colour via intensity-driven soft edge.** The A channel carries a quantised palette
   index, but `SceneLightTintAt` decodes it into a smooth colour glow by blending key→palette by
   \f$ \mathrm{saturate}((R - \mathit{rest}) / \mathit{RAMP}) \f$ — the smooth bilinear R magnitude
   drives the colour edge, not the hard index texels. Overlapping lights meet in the dark
   (between them R is low → colour fades out).

5. **Device-build safety.** All three `Hidden/` shaders are serialized on the settings SO
   (`ISceneLightFieldSettings`). `Shader.Find` is an editor-only fallback — an unassigned slot
   breaks the field on device.

6. **Dirty-gated cost.** The pipeline is skipped entirely when nothing changed. An idle scene with
   static lights pays zero GPU cost per frame. The field runs at `TexelsPerUnit = 32` (finer than
   the disturbance field's 8) — affordable because it's not ticked every frame.

7. **Cadence-capped, not just dirty-gated.** A light that dirties every tick (a tracked projectile)
   would otherwise re-render at the display's full refresh rate for identical visuals — 2× the GPU
   cost on a 120 Hz panel versus 60 Hz. `FieldFrameInterval` is authored as "every N frames at 60 fps"
   and reinterpreted as seconds, accumulated with unscaled time (mirroring `SceneCaptureService`'s
   capture cadence), decoupling render cost from display refresh. The very first render is exempt
   from the cap so consumers never sample an empty RT.

## Channel encoding (single RT)

| Channel | Meaning | Rest value |
|---|---|---|
| **R** | Local light boost above ambient | `0` |
| **G/B** | 0.5-biased \f$ \mathit{weight}\cdot \mathit{localDir} \f$ (direction toward nearest local light) | `0.5` (neutral) |
| **A** | Palette colour index \f$ (\mathit{index}+1)/16 \f$; 0 = use `_SceneLightColor` | `0` exactly |

## Current game sources

| Source | Light type | Count | Duration | Palette |
|---|---|---|---|---|
| **Projectile** | Point (disc) | 1 | Flight duration | Projectile colour (Sparks while colourless) |
| **Laser** | Capsule (segment) | 2 (H + V) | Effect duration | Source balloon colour |
| **Bomb** | Point (disc) | 1 | Effect duration | Source balloon colour |
| **Lightning** | Point (disc) | Per target | `PopLightSeconds` | Matched target colour |
| **Specks** | — (consumer only) | — | — | Per-appearance `LightMode` + `LightInfluence` |

## Relationship to the GI

`ScreenSpaceLightService` (the screen-space GI, see @ref arch_screen_space_light) is now a
**field consumer**: its smear shader reads the local direction and magnitude per-fragment via
`SceneLight.cginc`. A local light bends the bleed and shadows around it; field-off reproduces
the old single-direction march. The composite couples intensity: bounce scales by absolute R,
shadow scales by R relative to the owner's `Intensity` reference. See the updated GI diagram.

## See also

- @ref arch_screen_space_light — the GI composite that consumes this field
- `Shared/SceneLight/README.md` — implementation details and API reference
