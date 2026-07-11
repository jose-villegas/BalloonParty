@page plan_urp_migration URP Migration

# URP Migration — implementation plan

> Migration from the Built-in Render Pipeline to URP's **2D Renderer**. Originally recorded
> 2026-07-02 as a deferred decision; **elaborated to implementation-ready 2026-07-11** with a
> re-verified inventory and a full task breakdown. The go/no-go triggers below still gate
> execution — this plan makes pulling the trigger cheap, it does not pull it.

---

## Decision status (updated 2026-07-11)

**Triggers to execute — migrate when any becomes true (unchanged from 2026-07-02):**
- A feature genuinely needs 2D lights/shadows or URP-only post-processing.
- Unity announces Built-in RP removal/deprecation for a version this project must move to
  (Built-in is already maintenance-mode on Unity 6).
- A target platform/store requirement forces it.

**What changed since the original deferral** — the migration surface changed *shape*, not size:

| 2026-07-02 assumption | 2026-07-11 reality |
|---|---|
| `GrabPass` is the hard blocker (no URP equivalent) | **Gone.** 5b shipped `SceneCaptureService` — a second camera rendering to an RT, bound globally as `_SceneCaptureTex`. This pattern is URP-compatible. The single ugliest item no longer exists. |
| Prereq 5a retires the `Sprite/*` family (5 shaders + 28 materials) before migration | **Did not happen as predicted.** 5a baked shadows into the balloon *body* sprites, but the family is still alive: **6 shaders** (a new `SpriteGlitter` joined) referenced by **28 materials** — trails, UI hearts/shields, projectile, knot, item shadows, particles. They must be ported (or dieted first, task A2). |
| PuffCloud port must fold in the noise→texture rework (5c) | **5c shipped.** PuffCloud is now a plain tileable-texture shader — a straightforward port. |
| No camera hooks anywhere | **No longer true.** `ScreenSpaceLightService` (2D-GI overlay) shipped and drives its blit chain from `OnPreRender()` — a Built-in-only callback that **never fires under URP**. This is now the one mandatory code port (task B3). |
| ~15 runtime shaders | **21 runtime shaders** (new: `RainbowBalloon`, `SpeckField`, `SpriteGlitter`, `ScreenSpaceLightSmear`, `ScreenSpaceLightOverlay`; minus dead `DisturbanceStamp`). |

Net assessment: **zero hard blockers remain.** The cost is still concentrated in shader
ports plus one well-understood service port, and shader work can only be validated
in-editor/on-device. What URP buys is unchanged: 2D lights/shadow casters, Shader Graph,
long-term support, ScriptableRenderPass-based effects.

## Verified inventory (re-verified 2026-07-11)

**Environment:** Unity `6000.3.15f1` → URP 17.x (take the version the Package Manager pairs
with the editor). `GraphicsSettings.m_CustomRenderPipeline` is null (Built-in),
`m_TransparencySortMode: 0` (default — no custom sort axis to replicate), MSAA 0 on all
active quality levels (one legacy level has 2×).

**Camera-pipeline hooks (grep: `OnRenderImage|AddCommandBuffer|OnPreRender|OnPostRender|OnRenderObject|\.Render\(\)|RenderWithShader`):**
- `Display/ScreenSpaceLightService.cs:85` — `OnPreRender()`. **Must port** (task B3).
- `Cheats/BalloonRemoverCheat.cs:75` — `OnRenderObject()`. SRP still invokes this; verify the
  cheat overlay once, low stakes.
- `Editor/Bush/BushLeafBaker.cs:83`, `Editor/Bush/BushBranchBaker.cs:34` — `camera.Render()`,
  unsupported under SRP. Editor-only bake tools (task B5).
- `Graphics.Blit`: `Shared/Disturbance/DisturbanceFieldResources.cs` (offscreen RT ping-pong,
  camera-independent, works unchanged) and `ScreenSpaceLightService` (moves with B3).
- `Graphics.DrawMesh[Instanced]`: `BushView.cs:88,193,205` and `ClusterView` subclasses
  (world-space cluster rendering). Both APIs work under SRP; instancing + 2D Renderer
  interaction is the most likely silent breakage — test explicitly.

**`SceneCaptureService` (`Display/SceneCaptureService.cs`)** — the GrabPass replacement, and
now also the GI overlay's input. A child camera (`depth = main - 1`, solid-color clear with
**alpha 0** — the alpha doubles as the sprite-coverage mask for the GI shaders), toggled
`enabled` per interval from `LateUpdate`, `targetTexture` = downscaled ARGB32 RT, bound via
`Shader.SetGlobalTexture`. Everything here is URP-legal; it needs camera-data conversion and
an ordering check (task B2).

### Runtime shaders (21)

| Shader | Materials | Port notes |
|---|---|---|
| `Balloon/UnbreakableBalloon` | 1 | GrabPass gone — samples global `_SceneCaptureTex`. Unlit, MPB-driven (`_SphereCenter`, `_TimeOffset`). Verify after B2 lands, then port. Still has the 3×3 alpha-tap loop + `pow` chains worth trimming in passing. |
| `Balloon/UnbreakableBalloonRim` | 1 | Straightforward unlit. |
| `Balloon/ToughBalloon` | 1 | Straightforward unlit. Animator-driven material props (`ToughStableIdle.anim` → `_SphereWarp`/`_CrackThreshold`) instantiate materials at runtime — re-verify visuals + batching. |
| `Balloon/RainbowBalloon` | 1 | **New since original plan.** SpriteShineShadow-derived bands + glitter, band colors via MPB. Straightforward unlit port; keep the MPB contract intact (`RainbowBalloonVariant` drives it). |
| `Balloon/SoapBubbleCluster` | 1 | Unlit; self-derived clock (5f) already removed per-frame MPB churn. |
| `Sprite/SpriteShadow` | **13** | Biggest surviving family member (trails, projectile, knot, item shadows). Port — or shrink first via A2. |
| `Sprite/SpriteBlur` | 5 | Port survivors after A2. |
| `Sprite/SpriteShine` | 5 | Port. |
| `Sprite/SpriteShineShadow` | 3 | UI hearts/shields. Port. |
| `Sprite/SpriteShadowComposite` | 1 | Port. |
| `Sprite/SpriteGlitter` | 1 | New (Appear sparkle). Port. |
| `Grid/BushLeaf` | — | `Graphics.DrawMeshInstanced` (`BushView.cs:193`, fallback `:205`) with a real `UNITY_INSTANCING_BUFFER` (`_LeafTint/_UVRect/_LeafWind`). Port must keep GPU instancing working; test instanced batches in Frame Debugger. |
| `Grid/BushBranch` | — | Not instanced — plain `Graphics.DrawMesh` per slot (`BushView.cs:88`). Straightforward. |
| `Grid/PuffCloud` | 1+ | Now tileable-texture noise (5c done) — straightforward. |
| `Grid/DisturbanceDiffusion`, `Grid/DisturbanceStampBatched` | via settings SO | Offscreen blit shaders (`ZTest Always/Cull Off/ZWrite Off`, no camera matrices) — near-verbatim. |
| `Grid/DisturbanceStamp` | **0** | **Still dead** (GUID unreferenced, settings wire only diffusion + stampBatched). Delete — task A1. |
| `Paint/PaintBlob` | 1 | Straightforward. |
| `Scenario/SpeckField` (+ `SpeckField.compute`) | 1 | New — the dust-specks system, and **not** a plain sprite shader. The `.compute` half (Advect kernel, `ComputeBuffer` dispatch from `SpeckField.LateUpdate`, disturbance-RT sampling) is **pipeline-independent — zero URP work**. The render half is a vertex-pull shader (`StructuredBuffer<Speck>` read in the vertex stage via `SV_VertexID`, `#pragma target 4.5`) drawn through a normal MeshRenderer on a dummy 6×count-vert mesh so it sorts with sprites. Port recipe applies (`UnityWorldToClipPos` → `TransformWorldToHClip`); StructuredBuffer vertex fetch works identically under URP (the existing `maxComputeBufferInputsVertex` device gate already covers the platform risk). Single draw call — SRP Batcher irrelevant. Verify: sorting-layer placement, slow-mo/pause freeze, ascend/restart travel matching, disturbance swirl. |
| `Display/ScreenSpaceLightSmear` (Hidden) | runtime-created | 3-pass blit shader, but each pass is selected explicitly by `Graphics.Blit(…, pass)` — no LightMode ambiguity. Ports with B3. |
| `Display/ScreenSpaceLightOverlay` | runtime-created | Fullscreen quad drawn as a normal sorted MeshRenderer — ports like any unlit sprite-ish shader. |
| `Plugins/UIRays/Rays` | 3 (`GameOverRays`, `LevelUpRays`, +1) | Third-party but tiny (single pass, `Cull Off`, alpha blend). Check upstream for a URP variant; otherwise likely works as-is or is a 10-minute port. |

**Editor shaders (4):** `Grid/Editor/Bush*.shader` — trivial unlit bake shaders; the tooling
around them breaks instead (task B5).

### Material debt outside the table

- **~30 materials on built-in stock shaders** (`Mobile/Particles/Additive`/`Alpha Blended`
  on particle systems, line renderers, several trails — `HeartTrail`, `BeamShine`, lightning,
  `TrailMaterial_ScoreTrail`/`_Prediction`; `Laser/Beam.mat` on `Sprites/Default`). The
  Render Pipeline Converter upgrades most stock materials, but sweep every one — task B6.
- **TextMesh Pro** — URP shader variants ship in the project (`TMP_SDF-URP` shadergraphs);
  swap materials, no work.

### Behavioural touchpoints (unchanged semantics, re-verify anyway)

- **MPB usage** (bushes, balloon variants, rainbow bands): excludes those renderers from the
  SRP Batcher; the 2D Renderer's sprite batching has its own rules — re-measure in Frame
  Debugger, don't assume. 5f already removed most per-frame MPB traffic.
- **Sorting layers / SpriteRenderers / `MeshRenderer.sortingLayerName`** (GI overlay quad
  sorts on `Sky`) — unchanged under the 2D Renderer.
- **Camera preload flow** — two cameras coexist during launcher→game preload; the suppressed
  one is *disabled* via `SceneExtensions.SuppressRendering` (pipeline-agnostic). Smoke-test
  early.
- **Compute dispatch** (`SpeckField`, `Slots/Actor/SpeckField.cs:297`) — `ComputeShader.Dispatch`
  from `LateUpdate` is outside the render pipeline entirely; URP changes nothing. Camera
  rendering still happens after `LateUpdate` under URP, so the sim-before-render ordering
  and the `PushRenderParams` timing are preserved as-is.
- **DOTween, UniRx, MessagePipe, physics, UI Canvas** — no pipeline coupling.

---

## Task plan

Sizes: S ≤ half a day · M ≈ 1–2 days · L ≈ 3+ days. Priorities: **P0** = critical path,
migration cannot ship without it · **P1** = required before merge, but parallelizable ·
**P2** = optional scope-shrink or follow-up.

### Dependency graph

```
A1 (dead shader)  ──────────────────────────┐
A2 (sprite-family diet, optional) ──────────┤
A3 (5d atlas, optional, perf-only) ─┐       │
                                    ▼       ▼
B0 baseline ─▶ B1 pipeline setup ─▶ B2 cameras ─▶ B3 GI service ─┐
                        │                                        │
                        ├─▶ B4 shader ports (bulk) ──────────────┤
                        └─▶ B5 editor bake tools ────────────────┤
                                                                 ▼
                                     B6 material/prefab sweep ─▶ B7 perf gate ─▶ B8 sign-off
```

A-tasks run on `main` beforehand; B-tasks live on the migration branch. B3, B4, B5 are
mutually parallel once their prerequisites land.

### Wave A — pre-migration, on `main`

#### A1 — Delete `Grid/DisturbanceStamp.shader` · **P1 · S (minutes)** · deps: none
Dead code shrinking the port table by one. GUID-verified unreferenced by any material;
`DisturbanceFieldSettings` wires only `_diffusionShader` + `_stampBatchedShader`.
1. Delete the `.shader` + `.meta`.
2. `dotnet build BalloonParty.Runtime.csproj` (should be untouched) + quick play-mode ripple
   check on the disturbance field.

#### A2 — Sprite-family diet · **P2 · M** · deps: none
Every material moved off `Sprite/*` before migration is a material that never enters the B4
A/B loop. Only take the *free* swaps — do **not** touch the score-trail publish path (its
aggregation redesign is constrained by `PLAN-CinematicsArchitecture.md` Phase 3c; the
trail-storm perf item is coupled to it).
1. Audit the 28 materials: for each, check whether the shadow/blur/shine feature is visually
   load-bearing at gameplay size (trails moving at speed usually can't show a 10-tap blur).
2. Swap non-load-bearing ones to `Sprites/Default` (or a baked variant sprite, the 5a
   technique — `SpriteLayerCombiner` tooling exists in `Editor/SpriteCombine`).
3. In-editor visual pass per swap; this is judgment work, not mechanical.

#### A3 — Finish 5d balloon atlas · **P2 · M** · deps: none (parallel)
Not URP work, but it changes batching, and B0's baseline should reflect the end-state so
B7's comparison is honest. The combined balloon sprites need packing into one SpriteAtlas +
shared material + Frame Debugger before/after (see memory / optimization findings). If it
slips, just run it *after* B8 instead — never concurrently with the migration branch.

### Wave B — migration branch

#### B0 — Branch + baseline capture · **P0 · S** · deps: A-tasks decided (done or waived)
The repo commits to `main`; the migration is the exception — branch `urp-migration`, merge
only after on-device sign-off.
1. `git checkout -b urp-migration`.
2. Capture on the main device targets, stored under a repo-root `Baselines~/` folder (the
   `~` suffix keeps Unity from importing it):
   - Frame Debugger walk (draw-call count + batch composition for a busy board),
   - Profiler capture (CPU/GPU ms, GC),
   - reference screenshots/video of every visual system: all balloon types (incl. rainbow
     bands + unbreakable chrome reflection), pops, trails, level-up cinematic, loss
     cinematic + heart drain, bushes + wind, clouds, paint, lightning, danger gradient,
     disturbance ripples, **GI overlay on/off comparison**, speck field, UIRays popups.
3. Note the current `SceneCaptureFrameInterval` / downscale config values — B2 must
   reproduce identical capture cadence.

#### B1 — Pipeline install + URP asset setup · **P0 · S** · deps: B0
1. Package Manager → install `com.unity.render-pipelines.universal` (editor-paired version).
2. Create `Assets/Settings/URP/`: one URP Asset + a **2D Renderer Data** asset (Renderer
   List slot 0). Let Unity generate `.meta`s.
3. URP Asset settings to match current behaviour, not URP defaults: **HDR off**, MSAA per
   current QualitySettings (0), **Depth Texture off**, **Opaque Texture off** (the project
   has its own capture service — never enable both), SRP Batcher on.
4. Assign in Graphics Settings (Default Render Pipeline) **and every quality level** in
   Quality Settings.
5. Run Window → Rendering → Render Pipeline Converter → Built-in to URP: material upgrade +
   read-only material converters. Stock sprite/UI/particle materials convert; all 21 custom
   shaders go magenta — expected, that's the B4 worklist.
6. Commit the converter output separately from any hand fixes (reviewable diff).

#### B2 — Camera conversion · **P0 · S–M** · deps: B1
1. Main camera: Unity auto-adds `UniversalAdditionalCameraData`; set it to the 2D Renderer,
   post-processing off, Base type.
2. `SceneCaptureService.CreateCaptureCamera()`: after creating the camera, call
   `_captureCamera.GetUniversalAdditionalCameraData()` and set renderer (2D) +
   post-processing off. The camera stays a **Base** camera with a `targetTexture` — do not
   make it an overlay.
3. Ordering: Built-in guaranteed capture-before-main via `depth = main - 1`. URP sorts base
   cameras by depth too, and RT-targeting cameras render before screen cameras — but
   **verify in Frame Debugger** that the capture pass precedes the main camera in the same
   frame; the GI service and the unbreakable chrome both assume same-frame freshness.
4. Verify the per-interval `enabled` toggling from `LateUpdate` still works (it does under
   URP — camera culling/render only happens for enabled cameras — but the interval cadence
   must match B0's notes), and that the RT's **alpha channel** still carries the coverage
   mask (URP camera clear honors background alpha; the GI shaders break silently if alpha
   comes back 1).
5. Smoke-test the launcher→game preload dual-camera flow.

#### B3 — `ScreenSpaceLightService` port · **P0 · M** · deps: B2
> **Outcome (2026-07-11): shipped as `LateUpdate`, not the event below.** The
> `beginCameraRendering` version ran but URP's RenderGraph rejected mid-render-loop
> `Graphics.Blit` ("EndRenderPass: Not inside a Renderpass") and depthless camera output
> RTs ("Fake or uninitialized surface" — fixed by giving the capture RT a 24-bit depth).
> The chain now blits from `LateUpdate` (the disturbance field's proven pattern), reading
> the previous frame's capture — invisible on a temporally blended buffer. F2 stays moot.

The only mandatory *code* port. `OnPreRender` never fires under SRP; the equivalent hook
with identical timing semantics is `RenderPipelineManager.beginCameraRendering` filtered to
the main camera (the capture camera has already rendered by then, exactly like today).
1. In `OnEnable`, after `_capture.Acquire()`:
   `RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;` — unsubscribe in
   `OnDisable` before `_capture.Release()`.
2. Rename `OnPreRender()` → `private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)`
   with an early-out: `if (camera != _camera) { return; }` — the event fires for **every**
   camera, including the capture camera and scene-view cameras; without the guard the blit
   chain runs multiple times per frame and the ping-pong swap corrupts.
3. Body stays byte-identical: the three `Graphics.Blit` calls execute immediately outside
   the camera's render graph, which is legal at `beginCameraRendering` time. If (and only
   if) RenderGraph validation warns on-device, the fallback is a `ScriptableRenderPass`
   enqueued on the 2D renderer — don't build that speculatively.
4. The overlay quad (plain `MeshRenderer` on sorting layer `Sky`) needs no changes; its
   shader ports in B4.
5. Verify against B0's GI on/off references: shadow direction, bounce tint, temporal
   smoothing (wiggle a balloon, check for texel flicker), and that disabling the component
   releases the capture consumer (interval camera goes idle).

#### B4 — Shader triage + ports · **P0/P1 · L (the bulk)** · deps: B1 (B2 for Unbreakable)
Hand-written **unlit** CGPROGRAM shaders mostly keep compiling and rendering under URP
(untagged passes run as `SRPDefaultUnlit`) — so triage before porting:
1. **Triage pass (one session):** load the game with converter output only. For each shader
   in the table, record: renders correctly / renders wrong / magenta. Anything correct-as-is
   gets a low-priority "hygiene port" note instead of a blocking task.
2. **Port recipe** (per shader that needs it):
   - `Tags { … "RenderPipeline"="UniversalPipeline" }`, `CGPROGRAM` → `HLSLPROGRAM`,
     `#include "UnityCG.cginc"` → `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`.
   - `UnityObjectToClipPos(v)` → `TransformObjectToHClip(v.xyz)`; `fixed` → `half`.
   - `tex2D`/`sampler2D` still compile in URP HLSL — keep them for a minimal diff; don't
     rewrite to `TEXTURE2D`/`SAMPLE_TEXTURE2D` unless the shader is being touched anyway.
   - Keep MPB-driven properties as plain uniforms (these renderers are SRP-Batcher-excluded
     by MPB use regardless, so `UnityPerMaterial` CBUFFER hygiene is optional — add it only
     to shaders whose materials carry no MPB).
   - Instancing (`BushLeaf`): `UNITY_INSTANCING_BUFFER` macros are identical in URP —
     verify `#pragma multi_compile_instancing` survives and instanced batches appear in
     Frame Debugger (this is the highest-risk silent breakage in the project).
3. **Per-shader order** (dependency + risk first): Unbreakable + Rim (needs B2's capture),
   BushLeaf/BushBranch (instancing risk), disturbance pair (feature-critical), GI pair
   (with B3), then balloons (Tough/Rainbow/SoapBubble), PuffCloud, PaintBlob, SpeckField,
   the six `Sprite/*` shaders (or fewer after A2), Rays last (check upstream first).
4. Each port: A/B against B0 references + Frame Debugger batching check, one commit per
   shader (or family). **Keep Built-in originals in-tree until B8** — URP ignores them;
   they're the rollback path.

#### B5 — Editor bake tools · **P1 · S** · deps: B1
`camera.Render()` throws under SRP. In `BushLeafBaker.cs:83` and `BushBranchBaker.cs:34`:
1. Replace with the Unity 6 render-request API:
   ```csharp
   var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
   RenderPipeline.SubmitRenderRequest(camera, request);
   ```
   (guard with `RenderPipeline.SupportsRenderRequest(camera, request)` and keep the assert
   style of the surrounding code). The bake camera also needs its
   `UniversalAdditionalCameraData` renderer set, same as B2. **And the destination RTs need
   a depth-stencil format** — URP's RenderGraph rejects depthless camera output textures
   ("Fake or uninitialized surface"), as discovered on `SceneCaptureService` during B3.
2. The four `Grid/Editor/Bush*` bake shaders triage like B4 (trivial unlit — likely fine).
3. Rebake one bush and diff the output texture against the committed one — the bake is only
   correct if it's visually identical (bit-exactness not required; filtering may differ).

#### B6 — Materials, prefabs, scenes sweep · **P1 · M** · deps: B3, B4, B5
1. Project-wide magenta/missing-shader sweep (`t:Material` search + play-through).
2. The ~30 legacy stock-shader materials: confirm the converter upgraded each, or swap
   manually to `Universal Render Pipeline/Particles/Unlit` (additive/alpha-blended as
   appropriate) / `Universal Render Pipeline/2D/Sprite-Unlit-Default`. Verify particle
   trails and lightning lines specifically — line/trail renderers with vertex colors.
3. TMP: swap text materials to the URP shadergraph variants.
4. Re-verify every **pooled** prefab renders correctly *from the pool* (pooled instances
   cache renderer state; a warm pool from before a material swap is the classic ghost).
5. Re-verify: disturbance-field RT chain, bush instanced + fallback paths, world-space
   cluster rendering (`ClusterView` DrawMesh subclasses), speck field end-to-end (compute
   sim + vertex-pull rendering + disturbance reaction), Tough balloon animator-driven
   material props, `BalloonRemoverCheat` overlay, launcher preload suppression flow.

#### B7 — Performance validation gate · **P0 · M** · deps: B6
Same captures, same devices, same scenes as B0. **Gate: no regression** in GPU ms, draw
calls, or GC allocations.
1. Frame Debugger diff vs B0: expect *different* batch composition (2D Renderer batching ≠
   dynamic batching) — different is fine, *more draw calls or more GPU ms* is not.
2. Watch the two known-hot paths: balloon fill rate (should be identical — same shaders,
   same overdraw) and the GI blit chain (three low-res blits, should be identical).
3. Document batching-behaviour differences in `Display/README.md`; the memory optimization
   notes recorded against dynamic batching need re-validation flags.

#### B8 — Sign-off + cleanup + docs · **P0 · S** · deps: B7
1. Visual parity review against every B0 reference, on device.
2. Delete: Built-in shader originals kept as rollback, converter leftovers, `Baselines~/`
   stays local (never committed).
3. Update living docs: `Assets/Source/README.md`, `Display/README.md`,
   `Shaders/BalloonParty/README.md`, `Diagrams/arch_screen_space_light.md` (the service's
   hook changed), and memory (the "project is Built-in RP" key fact flips here — it gates
   perf reasoning everywhere).
4. Merge to `main` only after on-device sign-off.

### Follow-ups (explicitly out of scope for the migration branch)

- **F1 — adopt 2D lights**: migrate unlit-first (pure parity) so regressions are
  attributable; lights are a separate feature branch afterwards. This was open question 3 —
  answered: unlit first.
- **F2 — ScriptableRenderPass conversion of the GI chain**: only if B3's
  event-plus-`Graphics.Blit` approach shows RenderGraph friction on device.
- **F3 — SRP-Batcher CBUFFER hygiene** for MPB-free shaders, if B7 shows it matters.

## Open questions from 2026-07-02 — resolved

1. **Unity/URP version** → Unity `6000.3.15f1`, URP 17.x (pin whatever the Package Manager
   pairs; check the 2D Renderer changelog for sorting/batching changes since 6000.0).
2. **Is the unbreakable reflection still wanted live?** → Moot. It *is* live, via
   `SceneCaptureService`, which is URP-portable — the feared custom transparent-queue
   capture pass is not needed at all.
3. **2D lights at migration time?** → No; follow-up F1.
