@page plan_performance_recovery Performance Recovery

# Performance Recovery Plan — BalloonParty (Phase 2, revised 2026-07-23)

**Status**: Phase 1 complete (merged to main). Phase 2 fully re-audited 2026-07-23 by a
four-agent challenge (optimizer / reviewer / researcher / scribe) plus main-session
review. Several items were dropped as stale or factually wrong, shader priorities were
inverted for the real target GPU, and a mandatory diagnosis step was added before any
further optimization.

**Problem**: Game sits at ~80 FPS on device where 120 FPS was previously reached, after
a wave of new visual features (wall nets, shared cloud field, painting field, velocity
stamps, cruise specks). Target is stable, smooth frame delivery at the highest rate the
device sustains.

**Target device (corrected)**: Google Pixel 9 — Tensor G4, **Arm Mali-G715 MP7 @ 940 MHz**
(Valhall Gen 4, tile-based, native 2× fp16 throughput, no HW ray tracing). The previous
"Pixel 10 (Adreno 750)" line was wrong twice over: Adreno 750 is Snapdragon 8 Gen 3 and
has never shipped in a Pixel, and Pixel 10 is Tensor G5 with an Imagination PowerVR GPU.
Mali-specific reasoning below does **not** transfer to a Pixel 10 test device.

**Root cause (revised)**: The prior claim of "8-10 unmerged render passes every frame" is
stale. After the 2026-07-18 sweep, all five field services are cadence- or dirty-gated and
the GI smear chain re-runs only when the scene capture refreshes. Worst-case coincidence is
still ~9 off-graph blits (phase offsets are one-time and drift; `ScreenSpaceLightService`
is not registered with the coordinator at all), but typical frames are far lighter. The
sustained-80 problem is therefore **unattributed until Step 0 runs**. Candidate causes, in
rough likelihood order: main-pass fill rate/overdraw from the new features, the score-
arrival UI canvas-rebuild storm, frame pacing/ARR arbitration, thermal throttling, blit
coincidence spikes. Note the suspicious arithmetic: 80 = 120 × ⅔ — the exact cadence of
missing every third vsync, and also a plausible ARR-arbitration artifact (the ARR echo-loop
fix was validated on different hardware than the Pixel 9).

**Unity version**: 6000.3.15f1 (Unity 6.3) — URP 2D Renderer, Render Graph is the default
pipeline.

---

## Phase 1 — Completed ✅ (spot-checked 2026-07-23: all commits exist and match)

| # | Fix | Commit | Impact |
|---|-----|--------|--------|
| 1A | Batch SetPositions in FlyingTrail | `7c027bb8` | ~1.5ms CPU during BigScore |
| 1B | BackgroundFieldService cadence gate | `7c027bb8` | 1 tile flush eliminated 50-75% of frames |
| 1C | SceneCaptureService manual GenerateMips | `7c027bb8` | ~0.3ms GPU per capture |
| 2A | Pre-multiplied alpha on PuffCloud + BackgroundCloud | `75bb57ee` | 1 ROP multiply per pixel saved |
| 2B | Light warp early-out in PuffCloud | `75bb57ee` | 2 NoiseOctave taps saved when no local light |
| 4A | Slerp → NormalizedLerp in pen orbit | `8b5ffeae` | ~0.4ms CPU during BigScore |
| 4B | Pool LightRegistrationHandle (no closure) | `8f2bfa1d` | 2 fewer GC objects per light |
| 4C | Inline .Where() in PierceEndedEndCondition | `8f2bfa1d` | 2 fewer GC objects per projectile |
| 3A | EffectCadenceCoordinator — phase-offset all services | `e7e64712` | Prevents 10+ tile flush spikes |
| — | Merge SceneLight Fill into Accumulate shader | `b252b9e4` | 1 tile flush per light-field render saved |
| — | Smear shader tap reduction on mobile (41→16) | `1480d00b` | ~0.5ms GPU per smear rebuild |
| — | `_LOW_QUALITY_CLOUD` for PuffCloud (20→12 taps) | `6f738ca8` | ~40% fragment cost reduction |
| — | `_LOW_QUALITY_CLOUD` for BackgroundCloud | `1094b867` | ~4 fewer taps per fragment |
| — | Bypass cadence during transitions | `83a91af6` | Fixes visible lag during ascend/descent |

The 2026-07-18 sweep (separate arc, see memory) additionally shipped: light-field cadence
cap + stamp-batch absorption (`9e994225`/`d01c2799`), smear gated to capture refreshes
(`12df3c37`) with the temporal path deleted (`4f6fab33`), half-res smear + covered-fragment
shadow skip (`90234423`), zero-alloc counters (`323cf79f`/`6b664409`), particle stop
callbacks (`c68827a5`), static-state write retirement (`5268c991`). Several original
Phase 2 items were partially or fully subsumed by that sweep — reflected below.

---

## Phase 2 — Revised

### Step 0: Diagnose before optimizing (NEW — mandatory, do first)

The plan previously jumped straight to fixes; every impact estimate below is a hypothesis
until this step attributes the sustained-80.

1. **Pacing / ARR check** — while the game reads ~80 FPS on the Pixel 9, run
   `adb shell dumpsys display` and check SurfaceFlinger: is the panel actually in 120 Hz
   mode, and what render rate did Android arbitrate for the app? Cross-check
   `[FrameRateSettings]` startup diagnostics. The ARR echo-loop lesson (memory) was
   validated on other hardware — the Pixel 9 vote path is unverified. If the app is being
   arbitrated to a sub-120 rate, no shader work will fix it.
2. **CPU vs GPU classification** — one Unity Profiler capture (development build, on
   device) during normal play and during a BigScore burst. If CPU-bound: look at
   `Canvas.SendWillRenderCanvases`/`BuildBatch` (known dominator from José's prior
   capture) and GC spikes. If GPU-bound: capture with Android GPU Inspector (AGI supports
   Mali) — fragment-bound vs bandwidth-bound counters decide between Tier G items and
   overdraw work.
3. **Overdraw look** — the new features (wall nets, cloud backdrop, danger gradients,
   specks) are layered transparencies; use the editor overdraw view + Frame Debugger dump
   tool (Tools > BalloonParty > Dump Frame Debugger) to count fullscreen-ish transparent
   layers. Fill rate on a 7-core Mali at native res is a prime sustained-cost suspect and
   **no current plan item addresses it** — if this dominates, resolution scaling of the
   render target (URP Render Scale) becomes the lever, feeding A4.
4. **Thermal baseline** — is 80 FPS from a cold start or only after minutes of play?
   Tensor G4 3DMark stress stability is ~59%; if 120→80 correlates with heat, A4 (tiers)
   is the fix, not micro-optimizations.

### Tier 0: Free / Near-Free Wins

| # | Fix | File(s) | Effort | Notes |
|---|-----|---------|--------|-------|
| **F2** | **Remove the shrink `Array.Resize` in `TransformRibbon`** — the shared static `_ribbonScratch` is grown via `NextPowerOfTwo` then shrunk back to `count` every call; because pens have differing `positionCount`, consecutive calls thrash the buffer → a fresh `Vector3[]` alloc on nearly every call, up to ~100 pens/frame during formations. `TrailRenderer.positionCount` is read-only, so an oversized scratch is safe (`SetPositions` clamps). | `FlyingTrail.cs:278-281` | 1 line | **Top GC item.** In-editor verify: no ghost tail points on a tumbling formation |
| **F3** | **`SetCharArray` instead of `new string`** in `RollingTextAnimator` — the zero-alloc path already exists (`TmpTextExtensions`, `323cf79f`); this animator was never converted. Fix the hot site `:81` (per frame while an odometer rolls) and the cold sites `:114`, `:267`, `:285` in the same pass. | `RollingTextAnimator.cs` | 4 lines | Same code path as the shipped counter fix — finishing the job |
| **F4** | **Shader variant warmup** — author a `ShaderVariantCollection` and call **`ShaderVariantCollection.WarmUp()`** (or `WarmUpProgressively`) on the load screen. The previously named `WarmupAllShaders()` API doesn't exist on SVC. Vulkan gotcha: PSOs depend on vertex layout/blend/RT formats, so **capture the SVC from a real on-device play session** (Graphics Settings → Save to asset), don't hand-author. | New asset + loader call | Low | Fixes first-use hitches, not sustained rate |
| **C1** | Dedupe `TryFindToughAhead` — called from both `Update` (light telegraph) and `TickPierceSpiral` in the same frame during pierce fade-in. Compute once, pass down. | `ProjectileView.cs:131, 692, 828` | Small | Cleanup; ~1 extra CircleCast/frame for a few frames per pierce — no FPS claim |
| **C3** | Non-allocating cast — replace `Physics2D.CircleCastAll` with the `ContactFilter2D` + reusable `List<RaycastHit2D>` overload (copy `LaserItemHandler._castResults` pattern). | `ProjectileView.cs:872` | Small | GC hygiene; fires per wall-bounce while piercing, not per frame |

### Tier 1: UI arrival storm (NEW — likely the biggest sustained CPU win)

José's own profiler capture named `Canvas.SendWillRenderCanvases`/`BuildBatch` as the peak
dominator; the previous plan had nothing aimed at it.

| # | Fix | File(s) | Effort | Notes |
|---|-----|---------|--------|-------|
| **U1** | **Coalesce the progress-bar hit pulse** — `OnTrailArrived` fires `_animator.SetTrigger(TrailHitTrigger)` per arrival (up to ~50/s sustained), each retriggering a ~1s animation on sliced Images → canvas relayout every arrival. Throttle to one pulse per frame regardless of arrival count, or drive the pulse without an Animator (shared tween / material property). | `ColorProgressBar.cs:357` | Small-Med | Profiler A/B: canvas rebuild ms during a 250-point burst, before/after |
| **U2** | **Stop reparenting `ProgressNotice` per spawn** — `SetParent` on spawn (`:108`) + again via presenter (`:123`) dirties the canvas each time, ~50/s during streaks. Parent once at prewarm; move only `anchoredPosition` on spawn. Verify the pool doesn't reparent on return. | `ProgressNotice.cs:108,123`, `ProgressNoticePresenter.cs:59,80` | Small | Compounds with U1 |

### Tier 2: GPU / Shader (re-prioritized for Mali-G715)

| # | Fix | File(s) | Effort | Notes |
|---|-----|---------|--------|-------|
| **G6** | **Half-precision in the smear (and other heavy) shaders** — PROMOTED. Valhall does 2× fp16 (512 fp16 vs 256 fp32 ops/cy); this is a top shader lever on the actual device, not the "marginal" afterthought the Adreno framing implied. UV deltas, weights, color fetches → `half`. ~30-40% ALU on this shader is plausible (texture/bandwidth don't halve). | `ScreenSpaceLightSmear.shader` (then other field shaders) | 2 hours | Watch for banding on accumulating values; in-editor + on-device visual check |
| **G3** | **4-tap bilinear box blur** — replace the unrolled 9-tap 3×3 box in Pass 1 with 4 bilinear samples at half-texel offsets. Identical output only for a uniform box with correct half-texel centering — verify. | `ScreenSpaceLightSmear.shader` Pass 1 (`:144-172`) | 15 min | Low risk |
| **G4** | **Skip fine noise octave in the BackgroundField bake shader** — `BackgroundGenRawNoise` computes all 3 octaves unconditionally; display shaders already gate via `_LOW_QUALITY_CLOUD`. **Must** follow the `multi_compile_local` + per-material `EnableKeyword` pattern — a global keyword reproduces the release crash `b0f9ad83` just fixed. | `Include/BackgroundFieldGen.cginc:49-62`, bake material | Small | ~25% bake cost; bake is cadence-gated so absolute win is modest |
| **G1** | **Blit budget cap** (rescoped) — the coordinator only assigns one-time phase offsets; independent accumulators can drift back into coincidence, and `ScreenSpaceLightService` (2 blits) isn't registered at all. Step 1: register it (`ICadencedEffect` or at least a weight the coordinator sees). Step 2: upgrade to an active per-frame budget (max N weighted blits; over-budget effects defer a frame). Honest estimate: **smooths coincidence spikes; will not fix a sustained 80** — the old "+15-25 FPS" claim assumed the pre-2026-07-18 ungated world. | `EffectCadenceCoordinator.cs`, field services | Medium | Do after Step 0 confirms spike frames matter |
| **G5** | **Gradient-skip residual** — the 2026-07-18 cadence cap + batch absorption already covers the idle case; what remains is the literal item: a magnitude-only change (fade in/out) still pays Accumulate **and** Gradient. Track direction-dirty separately, skip the Gradient blit when only magnitudes moved. | `SceneLightFieldService.cs:184-186, 308-345` | Small | Opportunistic |

### Tier 3: Architectural (gated on Step 0 evidence)

| # | Fix | File(s) | Effort | Notes |
|---|-----|---------|--------|-------|
| **A4** | **Quality tiers + ADPF thermal** — PROMOTED to required-for-ship, and earlier. Use the Adaptive Performance package + Google Android provider (`com.unity.adaptiveperformance.google.android`): thermal headroom / hint sessions, GameMode integration. Drive tier switches from **thermal status, not an FPS counter** (thermal precedes the drop). Tiers: smear resolution & taps, field cadences, speck/trail counts, URP Render Scale, target frame rate ladder (120→90→60 — confirm which panel modes the Pixel 9 exposes rather than assuming vsync divisors). Sustained 120 on a Tensor G4 without this is unrealistic (~59% GPU stress stability). | New system + package | 1-2 days | The strategic fix for "80 where we had 120" if Step 0 shows thermal |
| **A1** | **Migrate field blits into Render Graph** (rewritten) — `Graphics.Blit` from `LateUpdate`/`Tick` does bypass the render graph, but the old justification was wrong: RG native-pass merging only fuses passes sharing the same attachments/dimensions that don't sample previous attachments — the field services write **separate, differently-sized, persistent ping-pong RTs**, which can never merge, and as cross-frame resources they'd have to be imported as external anyway. The '~470 MB/s per pass' figure was fabricated (the RTs are small texel grids, not 1080p). Real, smaller benefits: `RenderBufferLoadAction.DontCare` (skip tile restore), in-frame scheduling, dead-pass culling. Also unverified: `AddBlitPass` interop with the LateUpdate-driven, ContentVersion-gated architecture (Display/README.md documents RG rejecting blits from `beginCameraRendering`), and 2D-Renderer native-pass behavior. | Field services | 1-2 days | **Demoted**: only if Step-0 GPU capture shows pass load/store overhead is real; prototype on ONE service first |
| **A2** | **SceneCapture → Renderer Feature** — still valid: it's still a second full camera render per cadence tick (weight 3, the heaviest single consumer). `12df3c37` only gated the downstream smear, not the capture's own cost. | `SceneCaptureService.cs` | High | After A1 evidence; same RG caveats |
| **A5** | **Merge Disturbance + Painting RTs** — DEFERRED. Blockers the old plan missed: texel density mismatch (8 vs 16 texels/unit — packing forces a resolution compromise) and structurally different math (neighbor-sampling diffusion PDE vs local-only decay), so "single pass handles both" is a risky shader unification, not an RT-format change. | — | High | Only if Step 0 shows both fields hot in the same frames |

---

## Dropped in the 2026-07-23 revision

- **F1 (Native RenderPass toggle)** — confirmed irrelevant on Unity 6.3 (Render Graph
  default); already struck in the prior revision, kept struck.
- **G2 (vertex stepBase pre-computation)** — INVALIDATED. The per-fragment march direction
  is the *feature*: local lights bend all four GI directions around them (shader header
  says so; 2026-07-14 light-field work). Hoisting to the vertex shader makes direction
  uniform across the quad and regresses local-light bending. The "2-3× on Adreno" claim was
  also wrong-GPU reasoning — on Mali the real mechanism (varying-time prefetch) is worth
  single-digit percent at best. Revisit only as a field-off fast-path variant if a profiler
  shows the smear hot with zero local lights.
- **C2 (guard projectile-light ReactiveProperty writes)** — mechanism was wrong. UniRx
  `ReactiveProperty` short-circuits equal writes (no subscription traversal), and
  `Position` changes every frame on a moving shot so the field's dirty flag is set
  regardless — the fix could never reduce blits. Adopt the `UnbreakableBalloonVariant`
  write-once pattern for consistency if touching the file, but it is not a perf item.
- **C4 (ProjectileShieldView dirty flag)** — one shield instance, springs rarely converged
  mid-flight; sub-microsecond win for added state complexity. Only revisit if a profiler
  names this renderer.
- **A3 (procedural mesh trails)** — stale premise. The "58 TrailRenderers" catalog no
  longer exists (yarn-ball/torus catalog, ≤100 short-ribbon pens, ~250 spawns); trail
  materials are already plain/batchable; the profiler blames canvas rebuilds, not trails.
- **A6 (Burst+Jobs for TransformRibbon)** — retired by design (short ribbons after the
  legibility pass) and by the plan's own admission that the bottleneck isn't CPU math.
  Job-scheduling overhead for ≤100 small point sets would eat the win. F2 captures the
  real cost for one line.

## Investigated and Skipped (carried over)

- **Dithered transparency** — No TAA to integrate patterns; visible artifacts on mobile.
- **SceneLightTintAt 4-tap → 1-tap** — tiny cache-resident texture; marginal.
- **Overlay fullscreen field sample** — SceneLightTex is cache-resident; not a bottleneck.
- **Half-res PuffCloud RT** — needs a custom Layer (TagManager edit forbidden). Deferred.
- **Vector4 "allocation" in ProjectileShieldView** — value type, false positive.
- **Unbreakable balloon light writes** — 1-3 active, guarded; negligible (and further
  fixed by `5268c991`).

---

## Verification Protocol

**All shader changes need on-device visual verification** (dotnet build does not compile
shaders): G6 banding, G3 blur softness, G4 cloud detail.

**Profiling checkpoints** (Pixel 9, after each tier):
- P50 frame time < 8.33 ms (120 FPS)
- P99 frame time < 11.11 ms (allows 1-frame dips to 90 FPS — the previous "12 ms = 90 FPS"
  was arithmetically wrong; 12 ms ≈ 83 FPS)
- Off-graph blit count: ≤ 4 per frame worst case (Frame Debugger dump / AGI)
- Thermal: 15 min continuous play without dropping a tier (once A4 exists, tier residency
  is the metric — sustained raw 120 is not achievable on this SoC without tiers)

**Tooling gap**: `FPSCounter` reports rolling average + worst-frame-in-window, not
percentiles — extend it with a small frame-time histogram (P50/P99 readout) or use
Perfetto traces for the checkpoint numbers. The Frame Debugger dump tool is editor-only
(structure, not timing).

---

## Recommended Implementation Order

1. **Step 0** — pacing/ARR check, CPU-vs-GPU capture, overdraw look, thermal baseline.
   Everything below re-ranks on its outcome.
2. **F2 + F3** — trivial GC fixes, commit together.
3. **U1 + U2** — arrival-storm canvas fixes, with a profiler A/B on a 250-point burst.
4. **G6 + G3** — Mali fp16 pass + bilinear blur, commit together, on-device visual check.
5. **Re-profile on device** — measure where we stand.
6. **G4 + G5 + C1 + C3** — mop-up round.
7. **F4** — shader warmup (SVC captured on device).
8. **A4** — quality tiers + ADPF; required before shipping any high-refresh target.
9. **A1 prototype on one service** — only if step 5's GPU capture justifies it; A2/A5
   remain parked behind that evidence.
