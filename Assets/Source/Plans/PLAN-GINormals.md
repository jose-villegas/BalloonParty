@page plan_gi_normals GI Normals — oriented bounce for the screen-space light

# GI Normals — hemisphere receivers for the 2D GI

> **Status: PARKED (2026-07-17).** Fully prototyped and working — normals field, smear bounce
> direction, overlay N·L, plus SpriteDiffuse sphere shading — but the bounce-term payoff was too
> subtle for the code weight, so it was removed from main. The complete implementation lives on
> branch `backup/gi-normals-spherize` (commit `da1b143a`), ready to revive if a stronger consumer
> appears (most likely: normals driving DIRECT light, §2.4). What main kept from this effort:
> the direction-flip fix, axis-directed stamps, and the capsule/taper work — all shipped.

> Extends the screen-space light approximation (@ref arch_screen_space_light) with a normals
> field so bounce light becomes oriented irradiance: balloons stop receiving isotropic colour
> bleed and start shading as the spheres they depict — lit limb toward a bright neighbour,
> dark limb away.

---

## 1. Motivation

The shipped GI chain is: albedo capture (`SceneCaptureService`) → 8-tap directional smear with
mip spread + temporal history (`ScreenSpaceLightSmear`) → `_LightTex` → overlay quad
(`ScreenSpaceLightOverlay`) whose bounce/shadow strengths scale by the scene-light field's
magnitude per fragment. Two blind spots:

1. **The bounce is isotropic.** A balloon's near and far limbs receive identical bleed —
   nothing in the chain knows surface orientation, so receivers read as tinted discs.
2. **The bounce has magnitude but no direction at the receiver.** The smear knows its tap
   directions while accumulating, then collapses them into colour.

Balloons are spheres seen face-on: their normals are an analytic hemisphere over a disc —
no authored normal maps needed. Supplying (a) a normals field for participants and (b) a net
bounce direction from the smear turns the overlay's bounce term into `N·L` irradiance.

This is the per-pixel generalisation of the circle-receiver idea (net ray flux through a
disc): the same "balloons are spheres" model, taken from per-sprite response to per-fragment
shading. Non-participating pixels keep today's exact behaviour, so the feature is opt-in and
incrementally tunable.

## 2. Architecture

### 2.1 Normals field (stamped, no camera)

The disturbance-field pattern a third time: a small service stamps an analytic hemisphere
per participating balloon into a low-res RT.

- **Encoding**: RG = normal.xy mapped 0..1 (`0.5` = flat); A = participation mask.
  `normal.z = sqrt(saturate(1 - dot(xy, xy)))` reconstructs in the consumer — no B channel
  needed (B stays free for a future height/material tag).
- **Stamp**: per balloon, a quad covering its disc; inside the disc
  `N.xy = (p - center) / radius`, alpha 1; outside, discard. Painter's order by the balloon's
  sorting order approximates occlusion (balloons barely overlap; accepted trade-off vs a
  capture camera — revisit only if overlap artefacts show).
- **Service**: `SceneNormalFieldService` — registry mirrors `SceneLightFieldService`
  (register/dispose; participants are the balloon views, radius from config/prefab entry).
  Re-render when a participant moved (positions mutate every frame during motion — same
  dirty model as the light field). Resolution and participation live in a new settings block
  on the screen-space light settings SO.
- **Primitive roadmap**: the stamp shape is the primitive. Circle now; squares/triangles/
  polygons later are new stamp footprints in the same service — no consumer changes.

### 2.2 Bounce direction from the smear

`ScreenSpaceLightSmear` already loops its 8 tap directions — accumulate
`Σ tapDir × tapLuminance` alongside the colour into a second small target (or packed spare
channels if format allows). The result inherits the mip spread AND the temporal history
blend, so the direction is pre-smoothed — no flip/shimmer class of bugs (the same reason the
light-field direction work avoids hard normalisation).

**As shipped:** a dedicated half-resolution direction target (`_BounceDirTex`, separate ping-pong pair from the colour target) with its own temporal history (Pass 3 of `ScreenSpaceLightSmear`), rather than packed channels.

### 2.3 Overlay N·L

In `ScreenSpaceLightOverlay`:

```
float4 nrm = tex2D(_NormalTex, uv);              // a = participation
float3 N   = float3(nrm.rg * 2 - 1, 0);  N.z = sqrt(saturate(1 - dot(N.xy, N.xy)));
float3 L   = normalize(float3(bounceDir.xy, _BounceZBias));
float  ndl = saturate(dot(N, L));
bounce     = lerp(bounceIso, bounceIso * (ndl + _BounceAmbientZ * N.z), nrm.a);
```

- `_BounceZBias` tunes how "overhead" the bounce reads (0 = pure in-plane, higher = flatter
  response); `_BounceAmbientZ` keeps a face-on floor so participants never go black.
- `nrm.a = 0` → today's isotropic bounce, bit-for-bit. The whole feature sits behind one
  overlay toggle for A/B during tuning.

### 2.4 Direct-light mismatch (accepted, with a path)

Participants gain oriented bounce while their direct sprite lighting stays flat — acceptable
(bounce is soft), and the escape hatch is already designed: the same analytic hemisphere doing
`N·L` against the scene-light field in the balloon shader later unifies direct light, GI, and
the sprite receiver-radius work under one model.

## 3. Tasks

1. **Normals field service + stamp shader** — `SceneNormalFieldService`, stamp shader,
   settings block, registration, balloon participation wiring (register on spawn/bind,
   dispose on despawn — pooling rules apply). Edit-mode tests for the registry/dirty logic
   (mirror the light-field service's testable seams). Push the RT as a global texture and
   add a `GameRenderMapsWindow` descriptor ("GI Normals Field": R/G = 0.5-biased normal XY,
   A = participation mask) so it's inspectable like every other field.
2. **Smear direction + overlay N·L** — direction accumulation target, overlay sampling,
   the three tunables, the feature toggle. Shader-heavy; `dotnet build` cannot compile
   shaders — in-editor validation required. If the direction lands in its own target (not
   packed channels), give it a `GameRenderMapsWindow` descriptor too ("GI Bounce Direction").
3. **Docs** — new `Display`/GI README sections, SceneLight README cross-reference,
   register this plan in `Plans.md`.
4. **Adversarial review** — RT lifecycle/leaks (URP 2D, device formats), pooling races on
   participant registration, overlay cost at 120 Hz (honest numbers), fallback correctness
   (feature off / field absent / non-participants), palette/HDR interaction.

## 4. Verification & rollout

1. `dotnet build` for all C# + style audit (shaders excluded — flag).
2. In-editor: toggle off = pixel-identical overlay; toggle on = balloons shade toward
   bright neighbours; overlap spot-check; device profile at 120 Hz before enabling by
   default (GC + draw-call accounting per the optimization priorities).
3. Tuning pass: `_BounceZBias`, `_BounceAmbientZ`, normals-field resolution.
