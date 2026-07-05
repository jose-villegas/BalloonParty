@page plan_screen_space_light Screen-Space Light (2D GI prototype)

# Screen-Space Light (2D GI prototype)

> **NOW (2026-07-04):** prototype implemented, awaiting in-editor evaluation. Everything
> below the Status section is the design record. This is an *art experiment* — the
> pipeline is cheap and proven; whether it reads as "light" instead of "smudge" is the
> open question, and the answer decides if this graduates or gets deleted.

---

## Status

| Piece | State |
|---|---|
| `ScreenSpaceLightService` (Display/) | Built — needs adding to the Main Camera prefab |
| `ScreenSpaceLightSmear.shader` (blit, 2 passes) | Built — needs in-editor compile check |
| `ScreenSpaceLightOverlay.shader` (composite) | Built — needs in-editor compile check |
| `SceneCaptureService` capture alpha = coverage | Built (background alpha cleared to 0) |
| Evaluation / tuning | NOT started |

## Goal

Fake a global 2D directional light at ~45° as a **whole-screen effect**: objects softly
shadow whatever lies down-light of them, and their colors bleed in that direction —
without touching any existing material or shader. The service's light vector uses the
**PuffCloud L-vector convention** (points *toward* the source; default X 1 / Y −1
matches the PuffMain material), and shadows extend opposite the vector.

## Approach — why screen space works unusually well here

- The camera is orthographic and near-static: the screen *is* the world, so the classic
  screen-space failure (offscreen occluders popping) barely exists.
- `SceneCaptureService` already produces a low-res scene image every Nth frame.
- **No post-processing pass.** `OnRenderImage` would resolve the full frame — the same
  tile-GPU stall 5b removed with GrabPass. Instead the composite is a fullscreen
  transparent quad rendered last with a multiplicative blend (`Blend DstColor
  SrcColor`, 2·src·dst — 0.5 is neutral, so the overlay can both darken and brighten).
  The frame is tinted in place by the blend unit; nothing reads it back.

## Pipeline

1. **Capture** (existing): `_SceneCaptureTex`, 1/8 res, every Nth frame. The capture
   camera now clears with **alpha 0**, so the RT alpha channel is a sprite-coverage
   mask — the occlusion source. (Existing consumers sample RGB only; unaffected.)
2. **Smear blit** (`ScreenSpaceLightSmear` pass 0): two opposite 8-tap marches per
   pixel with exponential decay. `rgb` marches TOWARD the light — a lit neighbour's
   color bleeds onto this pixel (reflection/bleed, shows up on the side facing the
   source). `a` marches AWAY from the light — an occluder between this pixel and the
   source darkens it (shadow, shows up on the far side). The two must march opposite
   ways: a shadow falls on an object's far side, its glow bleeds onto its near side;
   sampling both the same direction stacks them on the same side instead (fixed
   2026-07-04 — an in-editor screenshot showed both effects on the near side).
   `_TapStart` skips the first fraction of both marches so an object doesn't fully
   shadow/glow itself.
3. **Soften blit** (pass 1): 3×3 box to remove smear streaks.
3b. **Temporal blend** (pass 2): the fresh build is folded into the previous smoothed
   buffer (ping-pong pair, `_temporalResponse` per frame, full-accept on the first
   frame after a resize). At capture resolution a moving sprite jumps whole texels
   per frame and the bounce tint flickers; the EMA integrates that away, and the
   light's low-frequency nature hides the added lag.
4. **Overlay quad**: child of the camera, sized to the ortho frustum each frame, layer
   `TransparentFX`, sorting layer `Sky` order 32000 (above all gameplay, below UI).
   Samples the light RT: `0.5 · lerp(white, shadowTint, a·strength) + rgb·bounce`.

**Feedback loop is structurally impossible**: the overlay lives on `TransparentFX`,
which must stay excluded from the capture mask — the capture always sees the unlit
scene, so frame N's lighting can never feed frame N+1's input.

## Knobs (serialized on the service — prototype-grade on purpose)

Light direction, smear distance (world units), tap decay, tap start, shadow strength,
shadow tint, bounce strength, temporal response, sorting layer/order. All pushed every blit, so they tune
live in play mode. If the effect graduates, promote to a config asset per the
configuration rule; while it's an experiment they stay on the component.

## In-editor setup

1. Add `ScreenSpaceLightService` to the **Main Camera** prefab (next to
   `SceneCaptureService`) and assign the two shader references
   (`ScreenSpaceLightSmear`, `ScreenSpaceLightOverlay`) — device builds strip
   name-only shader lookups.
2. Confirm the capture mask excludes `TransparentFX` and includes everything that
   should cast/bounce (Grid, Balloons, Scenario…).
3. Enter play mode; tune on the component. Disable the component to A/B.

## Cost

Two blits at capture resolution (~150×70 px) per captured frame + one fullscreen
alpha-blended quad with a single low-res fetch (≈ a vignette). The service holds a
capture `Acquire`, so disabling the component releases the capture chain when nothing
else uses it.

## Risks / open questions

- **Art direction is the real risk**: uniform tint over already-shadowed sprites can
  double-shadow; intensity wants to stay subtle. Evaluate before investing further.
- **Capture alpha as occlusion** is approximate: alpha-blended sprites write partial
  alpha (soft occluders occlude less — actually desirable); additive materials may
  write little alpha and under-occlude.
- ~~Shaders found via `Shader.Find` get stripped from builds~~ — bit on the first
  device test (effect silently absent on mobile); fixed with serialized `Shader`
  references on the service (`Shader.Find` remains as an editor fallback, and the
  service warns + disables itself if neither resolves). **The two shader fields must
  be assigned on the Main Camera prefab component.**
- Blits currently run every frame even though the capture updates every Nth — if kept,
  sync to the capture cadence (expose a capture-frame counter on the service).
- Gamma color space: the blur/blend math runs in gamma — fine for a stylized effect.

## If it graduates

Config-asset knobs, capture-cadence sync, Always Included Shaders (or serialized
refs), device GPU profile, and a look at consumers that would benefit from *receiving*
the light buffer directly (PuffCloud lighting tint, baked shadow tinting) as a second
layer of the same system.
