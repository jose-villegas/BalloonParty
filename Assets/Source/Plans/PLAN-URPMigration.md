@page plan_urp_migration URP Migration (conditional)

# URP Migration (conditional)

> Migration plan from the Built-in Render Pipeline to URP's 2D Renderer. **Decision as of
> 2026-07-02: do not migrate now.** The current performance problems are fill rate and
> shader cost, which URP does not fix, and one shader (`UnbreakableBalloon`'s `GrabPass`)
> has no clean URP equivalent. This plan exists so the decision is recorded, the triggers
> for revisiting are explicit, and the actual migration — when it happens — starts from a
> verified inventory instead of a blank page.

---

## Decision & rationale

Why not now (verified against the project, not generic advice):

1. **The bottleneck is fill rate.** The dominant costs (10-tap blur/shadow sprite
   shaders on every balloon layer, PuffCloud noise, transparent overdraw — see
   @ref plan_audit_remediation Phase 5) cost the same under either pipeline. The fixes
   are pipeline-agnostic baking work.
2. **The batching win is smaller than it sounds.** URP's SRP Batcher excludes renderers
   using `MaterialPropertyBlock`s — which the balloon variants, trails, and bushes use.
   The real draw-call problem is the per-balloon sorting scheme interleaving materials
   (remediation 5d), fixable on Built-in.
3. **`GrabPass` has no URP equivalent for this use.** URP's `_CameraOpaqueTexture`
   captures only the opaque pass; in a 2D sprite game virtually everything renders in
   the transparent queue, so the unbreakable balloon's convex-mirror reflection would
   sample an empty background. A faithful port needs a custom `ScriptableRenderPass`
   capturing mid-transparent-queue — the single ugliest item in the migration.
4. **The cost is concentrated in shader ports** (~15 runtime shaders, see inventory),
   and shader work can only be validated in-editor/on-device.

What URP would buy: the 2D Renderer's dynamic lights / shadow casters (currently
unused), Shader Graph, better long-term support (Built-in is in maintenance mode), and
render features for post effects.

**Triggers to revisit — migrate when any of these becomes true:**
- A feature genuinely needs 2D lights/shadows or URP-only post-processing.
- Unity announces Built-in RP removal/deprecation for a version this project must move to.
- A target platform/store requirement forces it.

## Prerequisites — do these first (they shrink the migration ~70%)

From @ref plan_audit_remediation Phase 5, independently worthwhile:

1. **5a — bake shadow/blur into sprite textures.** Retires the entire
   `Sprite/SpriteShadow`, `SpriteBlur`, `SpriteShadowComposite`, `SpriteShine`,
   `SpriteShineShadow` family (5 shaders + ~14 materials) → those renderers move to
   stock sprite shaders, which URP converts automatically.
2. **5b — replace the `GrabPass` reflection with a prebaked texture.** Removes the only
   hard pipeline dependency in the project.
3. **5f — self-derived shader clocks.** Removes per-frame MPB churn, which matters more
   under URP (SRP Batcher exclusion) than under Built-in.

After these, the custom-shader surface is roughly: balloon variants (Tough, Unbreakable
rim/body sans grab, SoapBubbleCluster), bush pair, PuffCloud, PaintBlob, and the
offscreen disturbance shaders.

## Verified inventory (2026-07-02)

**Good news, grep-verified:** no `OnRenderImage`, no `Camera.AddCommandBuffer`, no
`OnPreRender`/`OnPostRender` anywhere in `Assets/Source` — the camera pipeline is never
hooked. `Graphics.Blit` appears only in `Shared/Disturbance/DisturbanceFieldResources.cs`
for the offscreen RT ping-pong, which is camera-independent and works unchanged under
URP. Single camera per scene, no stacking, no post-processing.

Runtime shaders to port (all hand-written CGPROGRAM → HLSL + URP includes, or rebuild in
Shader Graph):

| Shader | Notes / port strategy |
|---|---|
| `Balloon/UnbreakableBalloon.shader` | **GrabPass at line 88** — retire via prerequisite 5b *before* migrating; otherwise needs a custom transparent-queue capture pass. Also 3×3 alpha-tap loop + `pow` chains worth trimming during the port. |
| `Balloon/UnbreakableBalloonRim.shader` | Straightforward unlit port. |
| `Balloon/ToughBalloon.shader` | Straightforward unlit port. |
| `Balloon/SoapBubbleCluster.shader` | Port; pairs with variant-clock fix 5f. |
| `Grid/BushBranch.shader`, `Grid/BushLeaf.shader` | Used with `DrawMeshInstanced` — port must keep GPU instancing (`UNITY_INSTANCING_BUFFER` blocks) working; the `_RendererColor` instancing caveat from the bush work applies. Test instanced batches explicitly. |
| `Grid/PuffCloud.shader` | Prefer doing remediation 5c (noise → texture) as part of the port rather than porting 21 octaves/px verbatim. |
| `Grid/DisturbanceDiffusion/Stamp/StampBatched.shader` | Offscreen blit shaders; likely near-verbatim ports (unlit, no lighting, no camera coupling). |
| `Paint/PaintBlob.shader` | Straightforward port. |
| `Sprite/*` (5 shaders) | Retired by prerequisite 5a; if any survive, port the survivors only. |
| `Grid/Editor/Bush*.shader` (4) | Editor-only bake shaders drawing to RTs; verify the bake tool still renders correctly under URP (bake via camera vs blit — check `Editor/Bush` pipeline). |
| `Plugins/UIRays/Rays.shader` | Third-party — check upstream for a URP variant; otherwise port or replace the effect. |

Other touchpoints:
- **TextMesh Pro** ships URP shader variants — swap materials, no work.
- **Sorting layers / SpriteRenderers / SortingGroup** — unchanged semantics under the 2D
  Renderer.
- **MPB usage** (`BushView`, variants, trails): still functional under URP, but excludes
  those renderers from the SRP Batcher; the 2D Renderer's sprite batching has its own
  rules — re-verify batch counts in Frame Debugger after migration, don't assume.
- **`DOTween`, UniRx, physics, UI Canvas** — no pipeline coupling.
- **Camera** — single ortho camera; convert to `UniversalAdditionalCameraData` (automatic
  via render pipeline converter). `CameraShakeService` manipulates the transform only —
  unaffected. `SceneTransition`'s rendering suppression (layer-based culling +
  `SuppressRendering`) must be re-verified — culling-mask behaviour is identical, but
  test the launcher→game preload flow early.

## Migration phases

**Phase 0 — Branch + baseline (S).**
Dedicated branch (this repo commits to `main`; the migration is the exception — keep it
on a branch until visual parity is signed off). Capture baselines on the main device
targets: Frame Debugger capture, Profiler capture (CPU/GPU ms), and reference
screenshots/video of every visual system (balloons all types, pops, trails, level-up
cinematic, heart drain, bushes + wind, clouds, paint, lightning, danger gradient,
disturbance field ripples).

**Phase 1 — Pipeline setup (S).**
Install `com.unity.render-pipelines.universal`; create a URP Asset + **2D Renderer**
data asset; assign in Graphics + Quality settings; run the Render Pipeline Converter
for built-in material remapping (converts stock sprite/UI materials; custom ones turn
magenta — expected). Set the URP asset to match current behaviour: no HDR surprises,
MSAA as currently configured, transparency sort mode matching the current custom-axis
settings if any.

**Phase 2 — Shader ports (M–L, the bulk).**
Work through the inventory table top-down (assuming prerequisites retired the Sprite
family and the GrabPass). For each shader: port, A/B against the Phase 0 reference
captures, Frame Debugger check for batching regressions. Keep Built-in versions in the
tree until Phase 5 sign-off (URP ignores them; they're the rollback).

**Phase 3 — Materials, prefabs, scenes sweep (M).**
Sweep all materials for magenta/missing-shader; re-verify every pooled prefab
(balloons, trails, particles, effects) renders correctly *from the pool* (pooled
instances can cache renderer state); re-verify the launcher→game preload +
rendering-suppression flow; re-verify the disturbance-field RT chain and the bush
`DrawMeshInstanced` path (instancing + 2D Renderer interaction is the most likely
silent breakage).

**Phase 4 — Performance validation (M).**
Same captures as Phase 0, same devices. Gate: no regression in GPU ms, draw calls, or
GC; document any batching-behaviour differences in `Display/README.md` and update
memory's optimization notes (batching rules differ from Built-in — findings recorded
against dynamic batching need re-validation).

**Phase 5 — Sign-off + cleanup (S).**
Visual parity review against Phase 0 references; delete retired Built-in shaders and
the converter leftovers; update `Assets/Source/README.md` / `Display/README.md` /
`Shaders/BalloonParty/README.md` and the memory index (the "project is Built-in RP"
fact flips here). Merge to `main` only after on-device sign-off.

## Open questions (answer at migration time)

1. Unity/URP version to target — take the URP version paired with the editor at that
   time; check the 2D Renderer changelog for sorting/batching behaviour changes since
   6000.x.
2. Is the unbreakable reflection still wanted as a *live* effect anywhere (e.g. a future
   cinematic close-up)? If yes, budget the custom transparent-capture render feature;
   if no, the prebaked texture from prerequisite 5b is final.
3. Whether to adopt 2D lights at migration time or keep unlit — recommend migrating
   unlit first (pure parity), lights as a separate follow-up so regressions are
   attributable.
