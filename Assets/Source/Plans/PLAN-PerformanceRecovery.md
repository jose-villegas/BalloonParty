@page plan_performance_recovery Performance Recovery

# Performance Recovery Plan — BalloonParty (Phase 2)

**Status**: Phase 1 complete (merged to main). Phase 2 is the next round of optimizations
identified by optimizer, architect, and researcher agents.

**Problem**: After Phase 1 optimizations, game still sits at ~80 FPS on Pixel 10
(Adreno 750 tile-based GPU). Target is ≥110 FPS sustained.

**Root cause (refined)**: Aggregate tile flush count from unmerged RT switches
(8-10 native render passes/frame), dependent texture reads in smear shader, per-frame
CPU waste in projectile hot paths, and GC pressure from trail ribbon transforms.

**Unity version**: 6000.3.15f1 (Unity 6.3) — Render Graph is the default pipeline.

---

## Phase 1 — Completed ✅

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

---

## Phase 2 — Performance Recovery Round 2

### Tier 0: Free / Near-Free Wins (do first)

| # | Fix | File(s) | Effort | Est. Impact |
|---|-----|---------|--------|-------------|
| ~~**F1**~~ | ~~**Enable Native RenderPass**~~ — **IRRELEVANT on Unity 6.3** (6000.3.15f1). Render Graph is the default pipeline and handles native pass merging automatically. The `m_UseNativeRenderPass` toggle only applies to the legacy compatibility renderer. The real issue is that field services use `Graphics.Blit` which **bypasses the Render Graph entirely**, preventing pass merging. See **A1** instead. | `GameURP_Renderer.asset` | N/A | N/A — superseded by A1 |
| **F2** | **Remove `Array.Resize` in `TransformRibbon`** — `SetPositions` reads `positionCount`, not `array.Length` | `FlyingTrail.cs:278-280` | 1 line | Eliminates array alloc per pen per frame (720/s peak during BigScore) |
| **F3** | **`SetCharArray` instead of `new string`** in `RollingTextAnimator` | `RollingTextAnimator.cs:81` | 1 line | Eliminates string alloc + GC per digit change |
| **F4** | **Shader variant warmup collection** — author `ShaderVariantCollection`, call `WarmupAllShaders()` on load screen | New asset + loader call | Low | Eliminates first-hit JIT stalls (frame hitches) |

### Tier 1: High Impact — CPU Hot Paths

| # | Fix | File(s) | Effort | Est. Impact |
|---|-----|---------|--------|-------------|
| **C1** | **Cache `Physics2D.CircleCast` per frame** — `TryFindToughAhead()` called twice when piercing (Update light telegraph + TickPierceSpiral) | `ProjectileView.cs:131, 692, 828` | Small | Eliminates duplicate physics query every frame during pierce |
| **C2** | **Guard ReactiveProperty writes** on projectile light — skip when `transform.position == _lastLightPos` and not piercing; write constant values (Radius, EndRadius, Intensity) once on state transition, not every frame | `ProjectileView.cs:130-146` | Small | Eliminates 5-9 UniRx subscription chain traversals/frame |
| **C3** | **Pre-allocated `List<>` for `CircleCastAll`** — replace allocating `Physics2D.CircleCastAll` with `ContactFilter2D` overload using a reusable `List<RaycastHit2D>` (same pattern as `LaserItemHandler._castResults`) | `ProjectileView.cs:872` | Small | Eliminates per-bounce array allocation |
| **C4** | **Dirty flag on `ProjectileShieldView`** — skip `WriteAllProperties()` + `SetPropertyBlock()` when springs have converged (no active squash/morph) | `ProjectileShieldView.cs:91-145` | Small | Avoids per-frame native memory copy when shield is idle |

### Tier 2: High Impact — GPU / Shader Optimization

| # | Fix | File(s) | Effort | Est. Impact |
|---|-----|---------|--------|-------------|
| **G1** | **Hard per-frame blit budget cap** — upgrade `EffectCadenceCoordinator` from static phase-assigner to active frame budget controller. Rule: max 4 tile flushes/frame. Each `ICadencedEffect` reports `BlitWeight`; coordinator skips effects that would exceed budget (they fire next frame) | `EffectCadenceCoordinator.cs`, all field services | Medium | **+15-25 FPS** — transforms worst-case from 12 flushes to hard cap of 4 |
| **G2** | **Vertex shader stepBase pre-computation** — move `SceneLightDirectionAtLOD` decode from fragment to vertex shader on the blit quad (4 VTF calls total vs millions per-fragment). All 18+ subsequent taps become independent reads | `ScreenSpaceLightSmear.shader` | 1 hour | **2-3× smear throughput on Adreno** — converts dependent→independent texture reads |
| **G3** | **4-tap bilinear box blur** — replace 9-tap 3×3 box filter in smear Pass 1 with 4 bilinear samples at half-texel offsets (mathematically identical output) | `ScreenSpaceLightSmear.shader` Pass 1 | 15 min | 55% fewer texture instructions in blur pass |
| **G4** | **Skip fine noise octave in BackgroundField bake shader** on mobile — the display shader already skips it via `_LOW_QUALITY_CLOUD`, but the bake material still computes it | BackgroundField bake material/shader | Small | 25% bake cost reduction |
| **G5** | **Conditional gradient skip** in SceneLightField — track `_directionDirty` separately; when only magnitudes update (fade-in/out), skip the gradient blit | `SceneLightFieldService.cs` | Small | Saves 1 blit/fire when direction is stable |
| **G6** | **Half-precision (`half`) in smear shader** — UV deltas, per-tap weights, color fetches are all safe at `mediump` | `ScreenSpaceLightSmear.shader` | 2 hours | ~30-40% ALU savings on Mali; marginal on Adreno |

### Tier 3: Architectural (Bigger Effort, Higher Risk)

| # | Fix | File(s) | Effort | Est. Impact |
|---|-----|---------|--------|-------------|
| **A1** | **Migrate `Graphics.Blit` → Render Graph passes** — **Highest-priority GPU fix.** On Unity 6.3, Render Graph is the default pipeline and handles native pass merging automatically — but all field services use `Graphics.Blit` from `LateUpdate`/`Tick`, which bypasses the Render Graph entirely and forces unmerged tile flushes. Migrating to `AddBlitPass` / `AddRasterRenderPass` with explicit `RenderBufferLoadAction.DontCare` enables pass merging + eliminates tile load overhead (~470 MB/s saved per pass at 1080p). | All field services using `Graphics.Blit` | 1-2 days | **+10-30% GPU** — the win originally attributed to F1 lives here instead |
| **A2** | **SceneCapture → URP Renderer Feature** — replace capture camera with a RendererFeature that renders capture layers during main camera's render graph, reusing culling results | `SceneCaptureService.cs` | High | Saves ~0.5ms/fire (full URP per-camera setup eliminated) |
| **A3** | **Procedural mesh trails** — replace 58 individual TrailRenderers with batched procedural mesh sharing one material (GPU Instanced, 1-4 draw calls instead of 58) | `FlyingTrail.cs`, `ShapeFormationTicker.cs` | High | Draw-call spike reduction during burst pops |
| **A4** | **Quality Tier System** — `IQualityProfile` interface with runtime FPS-driven tier switching (trails, smear resolution, field cadence, speck count). Thermal adaptation via ADPF (120→60→30 fallback) | New system | 1 day | Required for sustained 120 FPS shipping |
| **A5** | **Merge Disturbance + Painting into single RT** — RG: disturbance, BA: painting. Single diffusion/decay pass handles both | `DisturbanceFieldService.cs`, `PaintingFieldService.cs` | High | Eliminates 1-2 tile flushes per frame |
| **A6** | **Burst+Jobs for bulk transforms** — migrate `FlyingTrail.TransformRibbon` (100+ points per pen per frame) to `IJobParallelFor` with `NativeArray<float3>` and `Unity.Mathematics`. Classic bulk rotate/scale/translate — ideal for NEON SIMD via Burst. Secondary candidate: `ShapeFormationTicker.AdvanceFormation` per-pen orbit loop (6-50 pens), though pen count may be too small to offset job scheduling overhead. Note: the sustained ~80 FPS bottleneck is GPU-bound (tile flushes), not CPU math — this primarily helps the burst-pop spike frames (27ms) rather than sustained framerate. | `FlyingTrail.cs`, `ShapeFormationTicker.cs` | Medium | Spike frame reduction during BigScore pops |

---

## Investigated and Skipped

- **Dithered transparency** — No TAA to integrate patterns. Visible dither artifacts on mobile
  without temporal resolve.
- **SceneLightTintAt 4-tap → 1-tap** — The 4 taps are from a tiny (~80×140) cache-resident
  texture. Marginal benefit vs. complexity.
- **Overlay fullscreen field sample** — SceneLightTex is fully cache-resident regardless of
  screen resolution. Not a bottleneck.
- **Half-res PuffCloud RT** — Needs a custom Unity Layer (requires editor change to
  TagManager.asset, which repo conventions forbid). Deferred.
- **Vector4 allocation in `ProjectileShieldView.WriteAllProperties`** — `Vector4` is a value
  type (stack alloc), not heap. No GC pressure. False positive.
- **Unbreakable balloon light writes** — Only 1-3 active at once, well-guarded by
  sqrMagnitude threshold. Negligible.

---

## Verification Protocol

**All shader changes need in-editor visual verification on device:**
- Smear stepBase vertex pre-computation — compare GI quality
- Box blur change — compare smear softness
- Half-precision — watch for banding artifacts

**Profiling checkpoints** (measure on Pixel 10 after each tier):
- P50 frame time target: < 8.3ms (120 FPS)
- P99 frame time target: < 12ms (allows 1-frame dips to 90 FPS)
- RT switch count target: ≤ 4 native render passes per frame
- Thermal sustainability: ≥ 15 min continuous play at target FPS

---

## Recommended Implementation Order

1. **F2 + F3** (array/string alloc fixes) — trivial, commit together
2. **C1 + C2 + C3** (projectile hot path fixes) — small, commit together
3. **G1** (blit budget cap) — highest-impact code change achievable without render graph migration
4. **G2 + G3** (smear shader vertex pre-compute + bilinear blur) — commit together
5. **Re-profile on device** — measure where we stand
6. **G4 + G5 + C4** (remaining medium wins) — mop up
7. **F4** (shader warmup) — eliminates stutter
8. **A1** (migrate `Graphics.Blit` → Render Graph) — **biggest GPU win**, but 1-2 day effort.
   On Unity 6.3, this is what unlocks native pass merging (the Render Graph handles it
   automatically, but only for passes registered through it — `Graphics.Blit` bypasses it
   entirely)
9. **A4** (quality tier + thermal) — required before shipping 120 FPS
10. Remaining Tier 3 items only if still under target after re-profiling
