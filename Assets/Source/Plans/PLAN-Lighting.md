@page plan_lighting 2D Lighting

# 2D Lighting

> A unified abstraction for **light direction** in the 2D renderer. Today a single conceptual
> scene light is expressed three different ways across shaders and services, each authored
> independently with no shared source and inconsistent conventions. This plan collapses them onto
> one owned light source, published as global shader properties, that every direction/specular
> consumer reads — and from which per-object highlights are *derived* rather than hand-placed.
>
> Exploratory beginning: the first milestone is only the **direction** abstraction; colour,
> intensity, and point-light falloff are deliberately deferred.
>
> **Status: milestones 1–3 SHIPPED. Milestone 4 (GI improvements) SHIPPED (2026-07-15):
> mip-chain cone march, RSM-style 4-direction bounce, shadow mip penumbra, settings SO
> refactor (SceneLightService decommissioned → SceneLightFieldService pushes ambient globals).
> In-editor shader verification still pending for shaders compiled only by Unity.**

---

## Goals

- One **source of truth** for the scene light direction; no per-material duplicate, no raw value
  restated in comments or READMEs.
- Every consumer that shades by a light direction reads the same value, so moving the light moves
  every cloud shade, grain, and specular together.
- One agreed **convention** for the light vector (space, sign, normalization).
- A seam that can grow to colour / intensity / a positioned (point) light without re-plumbing.

## Current state — everything that feeds a light/specular direction

| Consumer | Reads | Form | Where it's authored |
|---|---|---|---|
| `ScreenSpaceLightService` (the "GI") | `_lightDirection` `(1,-1)` | direction vector — **away from the light** (light-travel; the tooltip's "toward" claim is wrong, see Findings) | serialized on the service; drives the shadow-smear tap step |
| `Grid/PuffCloud.shader` | `_LightDir` — authored `(1,-1)` in PuffMain.mat (shader default `(-0.4,0.7)`) | direction vector — **toward the light** (Lambert L) | per-material |
| `Balloon/ToughBalloon.shader` | `_GrainLightDir` — authored `(-5,5)` in ToughBalloonMaterial.mat (shader default `(0.4,0.6)`) | direction vector — **away from the light** (grain math brightens uphill-along-vector) | per-material (leather grain) |
| `Paint/PaintBlob.shader` | `_SpecularOffsetX/Y` | 2D hotspot **position** | per-material |
| `Balloon/UnbreakableBalloon.shader` | `_SpecularPos` + `_SpecularAngle`/`_SpecularBend` | hotspot position + aniso **angle** | per-material |
| `Balloon/RainbowBalloon.shader` | `_ShineAngle` (turns) | scalar **angle** | per-material |
| `Balloon/SoapBubbleCluster.shader` | specular in unrotated space | fixed offset/angle | per-material |
| `Grid/BushLeaf.shader` | specular highlight offset | fixed offset | per-material |
| *(pattern reference)* Disturbance field G/B | per-pixel displacement dir | dynamic **direction buffer** | the field RT — motion, not light, but the "sample a direction from a texture" pattern a light buffer would reuse |

Three forms of one thing: **direction vectors** (GI / Puff / Tough), **hotspot positions**
(Paint / Unbreakable), and **angles** (Rainbow / Soap / shine). All authored separately.

## The problem

- **No source of truth.** GI `(1,-1)`, Puff `(-0.4,0.7)`, Tough `(0.4,0.6)` are three independent
  values with different sign/normalization. They drift.
- **Raw values duplicated into docs/comments.** `ScreenSpaceLightService`'s tooltip states its
  default "matches the PuffCloud material's Light Direction" (a prose coupling that silently rots),
  and `Balloon/Type/README` restates specular defaults ("default upper-left"). Config values belong
  to their single owner, never to a comment or README — docs reference the *source*, not the number.
- **Speculars are hand-placed, not derived.** The Paint/Unbreakable/Rainbow/Soap highlights *imply*
  a light-from-upper-left but nothing computes them from an actual light vector.

## Proposed abstraction

A single **scene light**, owned by one service, published as **global shader properties** — the
same pattern the disturbance field uses for `_DisturbanceTex` / `_FieldBoundsMin`:

- `_SceneLightDir` — normalized, one agreed convention (proposed: a screen/world-space 2D vector
  pointing **toward** the light; shadows extend the opposite way). Growth room: `_SceneLightColor`,
  `_SceneLightIntensity`, `_SceneLightPos` (for a positioned light + parallax).
- **Direction consumers** (`PuffCloud._LightDir`, `ToughBalloon._GrainLightDir`, the GI smear) drop
  their local knob and read `_SceneLightDir`.
- **Hotspot consumers** (Paint / Unbreakable / Rainbow / Soap / BushLeaf) *derive* their highlight
  position/angle from `_SceneLightDir` plus a small per-object artistic offset, instead of a
  hardcoded position.

Ownership: extend the existing GI service (it already holds the de-facto light vector) or a small
dedicated `SceneLightService` that pushes the globals once and republishes when the direction
changes. Plain C# / a thin MonoBehaviour writer, no per-material wiring.

## Convention (LOCKED — from the 2026-07-14 investigation)

- **Space:** screen/world 2D, +x right, +y up (the GI is screen-space; sprites are screen-facing).
- **Meaning:** `_SceneLightDir` points **toward** the light; occlusion/shadow extends `-dir`.
- **Normalization:** normalized at the source (degenerate fallback lives on the owner);
  consumers assume unit length.
- **Canonical value:** `(-0.707, 0.707)` — light upper-left. Every authored shadow offset, every
  specular hotspot (135° across Unbreakable/Soap/BushLeaf, 130.6° PaintBlob), and the GI's actual
  shadow math already agree on this; one value reproduces the shipped look within ~4°.
- **Who flips at migration:** the GI and the tough grain consume `-_SceneLightDir` (their math is
  away-from-light); PuffCloud consumes it directly.

## Investigation findings (2026-07-14)

- **The GI tooltip is wrong.** The smear samples bounce along `+dir` and occluders along `-dir`
  (ScreenSpaceLightSmear.shader:73/76), so shadows land on the `+dir` side — the vector is the
  light-*travel* direction, not "toward the light" as the tooltip claims. Its `(1,-1)` still means
  light upper-left. The GI also derives `_TapStepUV` on the CPU from camera ortho size/aspect —
  it must read the owner's C# property, not the global.
- **Two consumers are physically inverted in shipped materials.** PuffMain.mat's `_LightDir (1,-1)`
  is consumed as a toward-light Lambert vector → clouds shade lit-from-LOWER-RIGHT, contradicting
  the material's own drop shadow (light upper-left). ToughBalloonMaterial.mat's `_GrainLightDir
  (-5,5)` is from-light → grain also lit-from-lower-right, contradicting its own shadow offset.
  Both "numerically matched" the GI value while inverting its meaning — the exact drift this plan
  exists to kill. Unifying **changes their look** (diffuse/grain flips to upper-left, finally
  matching the shadows): an explicit art approval at migration, not a silent change.
- **Ownership verdict:** a thin `SceneLightService` MonoBehaviour on the Main Camera prefab (the
  `SceneCaptureService` precedent: one owner per global), NOT an extension of the GI — the GI is a
  disableable A/B effect whose early-returns would stale the global. Push unconditionally
  (OnEnable + LateUpdate). The Main Camera prefab is instanced in all three scenes; one edit covers all.
- **Biggest migration risk — edit mode:** nothing pushes globals outside play mode; migrated
  shaders would `normalize(0)` → NaN/black while authoring. Mitigate with `[ExecuteAlways]` +
  `OnValidate` push on the owner and/or in-shader `sqrMagnitude` fallbacks.
- **Mechanics confirmed:** a global only reaches a shader when the uniform is NOT in the
  `Properties` block (per-material values always mask globals) — each migration deletes the
  Properties entry; PuffCloud itself documents this pattern for the disturbance globals
  (lines 150-151). Globals coexist with MPB and `DrawMeshInstanced` (BushLeaf already reads
  `_DisturbanceTex`; Unbreakable reads `_SceneCaptureTex`). No C# writes any of the affected
  uniforms; `_SceneLight*` names are unused. Each affected shader has exactly one material.
- **Specular derivations are pre-computed** (see tasks): \f$hotspot = \_SceneLightDir \cdot k\f$ with
  measured k per material (PaintBlob 0.2305, Unbreakable 0.495/0.283, Soap 0.1414, BushLeaf 0.113);
  Unbreakable's streak angle = \f$\operatorname{atan2}(L) - 90^\circ\f$, which makes its across-axis equal L so the
  authored negative bend keeps meaning "bow away from the light" at any angle.
- **Excluded as decoration (not light):** SpriteShine / SpriteShineShadow / Unbreakable's shine
  band (hardcoded `(u+v)/2` time-scrolled sweeps, no direction uniform) and RainbowBalloon's
  `_ShineAngle` (time sweep; its authored angle isn't even light-consistent). Rainbow's axis may
  optionally join in milestone 3.
- **Stale material data found in passing:** PuffMain `_LightDir`, ToughBalloonMaterial
  `_GrainLightDir`, UnbreakableMat* `_SpecularBendEdge`, LeafMain's old `_LightDir` colour and
  float-typed `_HighlightOffset` — pruned during the respective migrations.

## Task breakdown (harness tasks #5–#12)

| Task | Scope | Depends on | Assignee | Complexity |
|---|---|---|---|---|
| T1 (#5) | `SceneLightService` owner + `_SceneLightDir` global, convention locked, edit-mode push, prefab step | — | main session (opus) | M |
| T2 (#6) | GI reads the owner (`-Direction` march), wrong tooltip/header fixed | T1 | general-purpose (sonnet) | S |
| T3 (#7) | PuffCloud `_LightDir` → global (+ look-flip approval, ddy caveat) | T1 | general-purpose (sonnet) | M |
| T4 (#8) | ToughBalloon `_GrainLightDir` → `-global` (+ look flip) | T1 | general-purpose (haiku/sonnet) | S |
| T5 (#9) | Derived-specular prototype on PaintBlob (`L * k`) | T1 | general-purpose (sonnet) | S |
| T6 (#10) | Generalize speculars: Unbreakable (pos+angle), Soap, BushLeaf, bush shadows via BushSettings | T5 | general-purpose (sonnet) | M |
| T7 (#11) | Docs + raw-value cleanup sweep (READMEs, plan status, BushBake decision) | T2–T6 | general-purpose (haiku) | S |
| T8 (#12) | Backlog: colour/intensity, point light, optional Rainbow-axis join | T7 (gated) | unassigned | L |

T2/T3/T4/T5 are parallelizable once T1 lands. Every shader task needs an in-editor pass
(`dotnet build` does not compile shaders); T3/T4 carry the look-flip approval.

## Milestones

1. **Direction seam.** Introduce `_SceneLightDir` (single owner + global push, chosen convention).
   Migrate the pure direction-vector consumers: `PuffCloud`, `ToughBalloon` grain, the GI smear.
   Delete the per-material knobs and the raw-value comment/README duplication. Specular hotspots
   untouched. — *smallest change, immediate consistency.*
2. **Derived speculars.** Make the hotspot consumers compute their highlight from `_SceneLightDir`
   (+ per-object offset) rather than a hardcoded position/angle. Prove on one shader (PaintBlob or
   Unbreakable) first, then generalize.
3. **Growth.** Add colour / intensity, and optionally a positioned light (`_SceneLightPos`) for
   falloff and parallax; fold the screen-space GI's own parameters under the same source.

## Status (2026-07-14)

Milestones 1 and 2 **SHIPPED** (tasks T1–T6c). The direction abstraction is complete; all consumer 
shaders now read from the owned global. Key decisions and additions:

- **(a) Look flip accepted:** The early-stage in-editor approvals stand. PuffCloud and ToughBalloon 
  grain reversed polarity (diffuse and grain now light from upper-left, matching their shadows).
- **(b) Polarity evolution:** Generic sprite shadow/shine shaders (`SpriteShadow`, `SpriteShineShadow`, 
  `SpriteShine`) became per-material opt-in toggles so expressive authored art stays authored — 
  e.g., PU_Lightning's radiating bolt shadows kept their baked orientation, UI untouched.
- **(c) Additions:** `[UnitCircle]` inspector drawer on `SceneLightService.Direction`, rainbow 
  seam swirl (`_SeamSwirlAmount/Scale/Speed`) + spherical bulge (`_SphereBend`), and `LightRotate` 
  sprite shader (orbit + optional rotate for baked directional art like shadow/shine children).
- **(d) Editor tool decision:** `BushBake.shader` keeps its own authored `_LightDir` (editor-only 
  bake-time tool, decision made to preserve bake reproducibility).
- **(e) Audit:** `UnbreakableBalloonRim.shader` carries no directional/light uniforms (time-driven 
  rim sweep only, nothing to migrate).
- **(f) Backlog:** Milestone 3 deferred (task T8: colour/intensity, point light, optional Rainbow-axis join).

---


## Milestone 3 — the light field (designed 2026-07-14, not yet actioned)

Multi-light + point/area lights WITHOUT per-consumer light loops: a screen/world-space RT — the
disturbance-field architecture applied to light ("what we are stamping is light"). Top-down 2D.

**Channel layout** (single RT, disturbance-style, ARGBHalf):
- **R — local boost**: the local light magnitude above the ambient, 0 at rest (the ambient is the
  global `_SceneLightIntensity`, added by the consumers — the field stores only local lights).
- **G/B — local direction weight**: 0.5-biased \f$weight \cdot localDir\f$ (neutral/zero at rest); consumers
  blend the global `_SceneLightDir` toward `localDir` by the weight. The field stores no ambient.
- **A — palette colour index**: \f$(index+1)/16\f$, 0 = "use `_SceneLightColor`" (light colours are
  deliberately palette-limited; the key light keeps free RGB via the rest state).

**Consumers sample once** at their anchor (pixel or object centre) and get direction + magnitude +
colour regardless of light count — N lights collapse into the composite. Rest state (no stamps) is
bit-identical to the directional system, the same migration safety the colour slice used.

**Compositing:** stamps accumulate magnitude (capped) + dominant colour index; direction is NOT
composited — it derives from the magnitude gradient in a post pass (\f$dir = \mathrm{normalize}(\nabla R)\f$, the
heightfield-normal trick PuffCloud already uses via ddx/ddy). This makes AREA lights trivial:
authors paint brightness only (greyscale cookie stamps, line/capsule lights for the laser, rect
washes) and every shape gets plausible directions automatically.

**Reused verbatim from the disturbance system:** DisturbanceFieldCoordinates (world↔UV), the RT
resources/blit pattern, batched stamps, the A-channel palette encoding.

**Reconciliation — the lerp scheduler does NOT transfer (corrected Phase C).** The original sketch
proposed reusing the disturbance `LerpStampScheduler` for "ramped stamps = light flashes". That is the
wrong tool: the scheduler emits decaying *deltas* into a *persistent* field that integrates them in
place. The light field has no in-texture persistence — each render rebuilds it — so a delta has nothing
to accumulate into. Lights are **state, not events**: a light is simply on or off, and the caller owns
that lifecycle.

- A **`Light`** is a small reactive model (`Position`/`Radius`/`Intensity`/`PaletteIndex` reactive; the
  falloff shape is the per-light `FalloffPower`). `RegisterLight(Light) → IDisposable` turns it on; disposing turns it off;
  `ClearLights()` clears all. **No decay in the service** — a fade is the caller animating `Intensity`
  (the R magnitude), which the field follows.
- The service **watches** each registered light + the directional owner and re-renders **only when
  something changed** (a dirty flag the reactive subscriptions flip). An idle scene skips the pipeline;
  the RT keeps its last, still-correct contents.

This reactive on/off registry (dirty-gated re-render) is Phase C's model.

**Known caveats (from the investigation + prior lessons):**
- A-index + bilinear = decoded-garbage banding (the SpeckField lesson): point-sample A and let the
  magnitude falloff hide colour seams (domains meet in the dark); if seams still show, resurrect
  the smoothed-colour-layer companion RT from git history (commit e9544c3a).
- Vertex-stage consumers (orbit accessories, bush leaves, per-object spec anchors) need VTF
  (`SampleLevel`, target 3.5) — precedent: SpeckField, the retired SpriteDisturbanceTint.
- The GI smear goes PER-FRAGMENT directional (decided 2026-07-14): the smear is already a
  fragment shader — each fragment samples the field once (capture-UV ↔ field-UV is a trivial
  camera-fitted affine) and marches its 8 taps along the LOCAL direction; a straight march along
  the fragment's own field direction is physically right for occlusion. This kills the GI's
  special-case CPU path (`_TapStepUV` from `SceneLightService.Direction` — C# then pushes only
  world→UV scale factors) and makes the GI just another field consumer. Bonus: the same sample's R
  modulates shadow strength/reach per fragment (dim region = weak short shadows) — which resolves
  the open "should GI shadow strength scale with intensity" question organically.
- Consumer helpers go texture-based → the per-shader helper copies stop scaling; introduce a shared
  `SceneLight.cginc` include (`SampleSceneLight*(worldPos)` with a globals fallback when the field
  is off) as part of Phase A.
- Hold the line at ONE field: no per-light shadowcasting, no Light2D port (custom shaders are
  SRPDefaultUnlit and can't receive Light2D anyway).

**Phases:** A — field service (rest-state fill from SceneLightService, `_SceneLightTex` + bounds
globals, the shared include, field-off fallback). B — pilots (PuffCloud per-pixel, PaintBlob
per-object). C — the reactive `Light` model + on/off registry (above), the accumulate + gradient
passes. D — generalize game-source wiring
(balloon pops flash their colour, then laser/lightning) + the per-fragment GI march (see the caveat
bullet above — it upgrades the GI, not just patches it).

**Phase A status (2026-07-14 — CODE-COMPLETE, in-editor verification pending).** Shipped:
`Shared/SceneLight/SceneLightFieldService` (+ `SceneLightFieldResources`), registered in
`GameScopeRegistration` beside `DisturbanceFieldService` (Singleton); the `ARGBHalf` field RT sized
via the reused `DisturbanceFieldCoordinates`; the rest-state fill
(R = 0, GB = 0.5 neutral, A = 0 — a constant, fully local/ambient-independent) via `Hidden/BalloonParty/SceneLightFieldFill`;
globals `_SceneLightTex` / `_SceneLightFieldBoundsMin` / `_SceneLightFieldBoundsSize` /
`_SceneLightFieldOn`; and the shared include `Assets/Shaders/BalloonParty/Include/SceneLight.cginc`
(flat helpers verbatim + `…At(worldPos)` field helpers with a `…LOD` VTF variant, all falling back to
the flat globals when the field is off). No shader includes it yet, so field-OFF is bit-identical to
today (zero visual effect). See `Shared/SceneLight/README.md`. The three `Hidden/` field shaders are
**serialized on the settings SO** (`FillShader`/`AccumulateShader`/`GradientShader`) so device builds keep
them (Shader.Find is only an editor fallback) — assign all three on the asset. In-editor render check of
the shaders still needed (`dotnet build` does not compile shaders).

**Phase B status (2026-07-14 — DONE).** PuffCloud (per-pixel) and PaintBlob (per-object) include the
shared header and sample `…At(worldPos)`.

**Phase C status (2026-07-14 — CODE-COMPLETE, in-editor verification pending).** Shipped: the reactive
`Light` model (`Position`/`Radius`/`Intensity`/`PaletteIndex` + per-light `FalloffPower`
`ReactiveProperty`s, `const` defaults; the magnitude is a smooth \f$(1 - dist/radius)^{FalloffPower}\f$ cone,
no plateau — a plateau's zero-gradient centre made the derived direction read as a ring);
`SceneLightFieldResources` upgraded to two ping-pong
RTs + `BlitAndSwap` with fill/accumulate/gradient materials; two new Hidden shaders
`SceneLightAccumulate` (batched, 32/blit, mirrors `DisturbanceStampBatched`; each light is a CAPSULE —
falloff on aspect-corrected distance to the segment `[start,end]`, so point lights are discs and
segments are area beams (the laser cross); adds to R soft-clamped \f$\_MaxBoost \cdot \big(1 - \exp(-sum/\_MaxBoost)\big)\f$, writes the dominant palette
index to A, passes GB through) and `SceneLightGradient` (\f$\mathrm{grad}(R)\f$ central difference over
`_FieldTexelSize`, blends rest GB → gradient dir by \f$\mathrm{saturate}(localR \cdot \_DirectionResponse)\f$ —
presence-weighted, so flat/rest fields keep the global direction); `SceneLightFieldService` upgraded
with the reactive on/off registry + dirty-gated fill→accumulate→gradient→publish render
(`RegisterLight(Light) → IDisposable` / `ClearLights` API); and `LightStampCheat` (registered beside
`DisturbanceStampCheat` in `GameScopeRegistration`). **Rest invariant verified in code:** no lights ⇒
accumulate skipped, R stays 0, gradient weight `saturate(0)` = 0 ⇒ GB = the global rest direction,
so the consumer-seen light is bit-identical to the directional system. All three field shaders are now
serialized on the settings SO (device-build safe — assign them on the asset). **Open:** the passes +
`FalloffPower`/`DirectionResponse` need an in-editor render check and tuning (`dotnet build` does not
compile shaders).

**Phase D status (2026-07-14 — CODE-COMPLETE, in-editor verification pending).** All remaining consumers
migrated onto `SceneLight.cginc` and sample `…At(worldPos)` (world bodies: ToughBalloon, SoapBubbleCluster,
RainbowBalloon, UnbreakableBalloon — anchored at the shared `_SphereCenter` for quadrant coherence —
BushLeaf, BushBranch; sprite family: SpriteShadow/ShadowComposite/Shine/ShineShadow, LightDriven,
Diffuse — per-material toggles preserved). Anchor per shader: per-fragment where it shades per-pixel,
per-object VTF (`target 3.5`) where it keys off one hotspot (the PaintBlob pattern). The GI march is now
**per-fragment**: `ScreenSpaceLightSmear` derives its march direction from `SceneLightDirectionAt(worldPos)`
(worldPos from the field-bounds globals) and `ScreenSpaceLightOverlay` scales bounce by the **absolute**
local magnitude and shadow by the **relative** magnitude (\f$local/\_MagnitudeRef\f$) — resolving the old
shadow-coupling open question; `ScreenSpaceLightService` stopped pushing `_TapStepUV`, now pushes
`_TapStepScale`+`_TapAspect` (the shader builds the step from a unit direction) and raw `_BounceStrength`
+ `_MagnitudeRef`. **Field-OFF bit-identical verified in code** (uniform flat direction reproduces the old
`_TapStepUV`; shadow `relative`→1; bounce `magnitude`→global intensity with raw bounce = old product).
**Open:** all shaders need an in-editor compile + visual A/B (field OFF unchanged; field ON bends
bounce/shadow/bodies), the four `target 3.5` VTF bumps need a device check, and the capture/overlay UV →
field-bounds mapping assumes the field is camera-view-aligned.

**Palette-colour decode (2026-07-14 — CODE-COMPLETE, in-editor verification pending).** A tagged light
now tints its region its palette colour, not just brighter white. `SceneLightFieldService` pushes the
game palette once as a global `_SceneLightPalette[16]` (`IGamePalette`, same slot order the lights encode)
plus `_SceneLightTexelSize`; `SceneLight.cginc`'s `SceneLightTintAt`/`AtLOD` decode A → index
(\f$\mathrm{round}(A \cdot 16) - 1\f$, -1 = untagged) and return `_SceneLightPalette[index].rgb * magnitude`, else the global
key-light path (so field-off / untagged is bit-identical). Smoothness comes from two things: (1) the
field runs at `TexelsPerUnit = 32` (far finer than the disturbance field's 8 — affordable because the
light field is dirty-gated, not ticked every frame), and (2) the colour edge is driven by the SMOOTH
magnitude — `SceneLightTintAt` blends key→palette by \f$\mathrm{saturate}\big((R - rest) / \mathit{SCENE\_LIGHT\_COLOR\_RAMP}\big)\f$, so a
light's colour fades in with its (bilinear) brightness rather than snapping at the quantised index texels.
The colour identity is a plain 2×2 decode-then-blend (`SceneLightPaletteColorAt`). The render-maps preview
stays raw/point-sampled by design (a field-data inspector). All migrated consumers inherit this through
`SceneLightTintAt` — no consumer edits.

  *(An edge-preserving joint-trilateral — JBU/NEDI, guided by R + direction — was tried first and
  reverted: edge preservation hardens a soft glow, and the direction term distorts a single disc via
  asymmetric weighting near the radial centre. Direction remains available for colour-vs-colour seam
  separation if overlapping lights need it.)*

**Open:** in-editor check that a coloured cheat light reads smooth on PuffCloud; `SCENE_LIGHT_COLOR_RAMP`
(soft-edge width) and `TexelsPerUnit` are the tuning knobs.

---

## Milestone 4 — GI Improvements (SHIPPED 2026-07-15)

A series of enhancements to the screen-space GI pipeline, driven by the newly available buffer data
(light field direction/magnitude, mip-chain capture). All changes are zero-extra-RT, zero-extra-pass.

### 4a — Mip-Chain Cone March

Enabled `useMipMap` + `autoGenerateMips` on the capture RT. The smear shader's 8-tap march now
samples at increasing mip levels (\f$mip = \_MipSpread \times \log_2(1 + t)\f$), so distant taps read averaged
scene color over a widening solid angle — approximating the integral of incoming radiance at each
distance (HSSVGI/HBIL pattern). Near taps stay at mip 0 for sharp contact. `_MipSpread = 0`
collapses to the old flat march.

### 4b — Shadow Mip Penumbra

Separate `_ShadowMipSpread` (default 1.4, higher than bounce's 0.7) gives the shadow march a
steeper mip ramp — sharp contact shadows, soft penumbra far from the caster. Zero extra cost
(one extra mip param per shadow tap).

### 4c — RSM-Style 4-Direction Bounce

Decoupled shadow from bounce. Shadow stays single-direction (toward the light, physically correct).
Bounce now gathers from 4 directions at 90° spacing around the field's local direction — an RSM-style
Virtual Point Light gather in 2D. Each direction runs 8 taps with cone widening. The primary
direction (down-light) has weight 1; three secondary directions are scaled by `_SecondaryWeight`
(0 = old single-dir, 0.5 = default, 1 = fully omnidirectional). All 4 directions rotate with the
field's local light, so nearby point/area lights bend the entire gather pattern around them.

### 4d — Settings SO Refactor

- Decommissioned `SceneLightService` MonoBehaviour entirely.
- Moved ambient globals (direction/colour/intensity) to `SceneLightFieldSettings` SO, exposed via
  new `ISceneLightSettings` interface.
- `SceneLightFieldService` now pushes the ambient globals every tick (replaces the former MonoBehaviour).
- All GI tuning (shaders, distances, weights, temporal) moved from serialized fields on
  `ScreenSpaceLightService` to `IScreenSpaceLightSettings` on the same SO.
- `CameraBackgroundTint` reads from `ISceneLightSettings` (DI-injected).
- Single SO serves three interfaces (`ISceneLightFieldSettings`, `IScreenSpaceLightSettings`,
  `ISceneLightSettings`), registered in `GameLifetimeScope`.
- 2026-07-18: the temporal-smoothing path (smear Pass 2 EMA, ping-pong history buffers,
  bounce-direction jitter, `TemporalSmoothing`/`TemporalResponse` knobs) was removed — it shipped
  disabled and never earned its complexity. The smear is now two passes (gather + box soften).

### Future exploration

- **Phase D (Radiance Cascades)**: 2-level cascade replacing the flat multi-direction march. The
  light field is converging toward a cascade-0 representation — a natural evolution if more spatial
  coherence is needed for far-field bounce.

## Open questions

- ~~Screen-space vs world-space light vector for balloons that move (parallax)?~~ **Resolved:**
  Screen-space, matching the GI. The field is world-space but camera-view-aligned; consumers sample
  in world and the result is screen-consistent.
- ~~Should the light be static (authored once) or animatable (day/night, event beats)?~~ **Resolved:**
  Global + reactive owner supports both; the flat globals are live-tunable in play mode, and local
  lights animate via their reactive `Intensity`/`Position`.
- ~~Do any consumers need a *different* light than the global (a rim/fill)?~~ **Resolved:** Yes —
  `SceneLightMode` enum (`Full` / `Ambient` / `Local`) gives per-consumer control. `Local` mode
  (field contribution above ambient only) is used for glint effects (`LightDriven`) that should flare
  only near a local light source, not glow uniformly under the global ambient.
- ~~Should GI shadow strength scale with intensity?~~ **Resolved:** The overlay's shadow term now
  scales by relative magnitude (\f$localR / \_MagnitudeRef\f$), so dim regions get weaker shadows and
  bright local lights intensify the bounce — organically, per-fragment, without a separate knob.
