@page plan_hdr_color_pipeline HDR Color Pipeline

# HDR Color Pipeline — migration plan

> Migrate the color pipeline from Gamma/LDR to Linear color space with HDR rendering —
> unclipped additive stacking, real bloom on emissive effects, and a correct foundation
> for the screen-space GI math. Planned 2026-07-11 from a verified inventory (same method
> as the retired URP migration plan: facts first, tasks gated on in-editor/device
> verification). **Execution not started** — Wave A's look re-tune is the cost gate; read
> the decision section before pulling the trigger.

---

## Decision & scope

**What HDR buys this game, concretely** (all currently clip at 1.0):
- Additive particle stacks (pop VFX, lightning, beam shine, level-up glow) — brightness
  saturates exactly where the game is most spectacular.
- Chrome/unbreakable specular sweeps, rainbow glitter, shine bands — capped highlights.
- Bloom: with HDR emitters, URP's Bloom pass makes lightning/lasers/glow *radiate*
  instead of flat-filling. This is the visible payoff; without post/bloom, HDR rendering
  on an SDR screen is barely distinguishable.
- Correct blending math: Gamma-space alpha/additive blending is physically wrong; the GI
  bounce/shadow smears currently operate on gamma values. Linear fixes a quiet layer of
  wrongness everywhere.

**The cost structure is front-loaded in Wave A (Linear migration):** switching color
space shifts the rendered look of every authored color — palette entries, sprite tints,
particle gradients, shader constants, the GI tints. That's an art re-tune pass across
the whole game, not a code task. Waves B/C are mechanical by comparison. There is no
"HDR without Linear" shortcut worth taking: URP disables HDR color grading in Gamma
space and the blending benefits vanish.

**Performance reality (8.33 ms budget at 120 Hz):** HDR render target at 32-bit
(`B10G11R11`) costs no extra bandwidth vs LDR; FP16 doubles it. Enabling post-processing
adds the uber-post pass (tonemap + bloom chain) — the single biggest new GPU cost, on a
fill-rate-sensitive game. Every wave ends with the established measurement loop
(Baselines~ framedumps, device Profiler, SurfaceFlinger pacing via adb).

**Triggers to execute:** an art push that needs glow/bloom (level-up, lightning, lasers
are the candidates), or dissatisfaction with clipped VFX brightness. No external
forcing function — this is an elective quality migration.

## Verified inventory (2026-07-11)

- **Color space: Gamma** (`ProjectSettings.asset` `m_ActiveColorSpace: 0`). Linear on
  Android requires ES3/Vulkan — project is Vulkan-first ✓.
- **URP asset** (`Assets/Settings/URP/GameURP.asset`): `m_SupportsHDR: 0`,
  `m_HDRColorBufferPrecision: 0` (= 32-bit `B10G11R11` when enabled — no alpha channel;
  fine for the camera target, NOT usable for RTs whose alpha carries data),
  `m_ColorGradingMode: 0` (LDR), LUT size 32.
- **Post-processing: off everywhere.** All three runtime/editor-created cameras set
  `renderPostProcessing = false` explicitly; the scene main camera has post off. The
  Volume framework exists (`Assets/DefaultVolumeProfile.asset` from the URP install) but
  nothing uses it. Tonemapping/bloom require post ON for the main camera + a Volume with
  overrides.
- **HDR display output: off** (`allowHDRDisplaySupport: 0`, `useHDRDisplay: 0`) —
  true HDR10 panel output is Wave C, deliberately deferred.
- **Shader precision:** 25 of the custom shader files use `fixed4` — clamps at ±2 on
  mobile precision, so HDR color paths (> 2) clip inside the shader even with an HDR
  target. Needs a per-shader audit on paths that carry light/emissive color; `half4` is
  the fix where flagged.
- **Palette** (`Configuration/Palette/GamePalette.cs`): `PaletteEntry[]` with plain
  `Color` fields, no `[ColorUsage]` attributes — no way to author intensity > 1 today.
  Consumers read via `IGamePalette.GetColor(name)`; MPB-driven tints downstream.
- **Special RTs whose alpha carries data** (from Display/README.md):
  `_SceneCaptureTex` (ARGB32; A = sprite coverage mask feeding GI) and the GI light
  buffers (ARGB32; A = shadow amount). If these upgrade to HDR they need an
  alpha-bearing HDR format (`ARGBHalf`/`ARGB2101010`), NOT `B10G11R11`. At
  downscale-12 resolution the bandwidth delta is negligible.
- **GI overlay composite** is a multiplicative blend authored against LDR [0,1]
  semantics (`ScreenSpaceLightOverlay.shader`) — must be revisited when the scene
  behind it goes HDR.
- **Stock materials:** ~30 on `Mobile/Particles/Additive`/`Alpha Blended` — these render
  under URP compatibility and work in Linear/HDR, but additive brightness *appearance*
  changes in Linear (part of the Wave A re-tune, not a code break).
- **Tooling in place from the URP migration** (reuse, don't rebuild): Baselines~
  framedump + step screenshots for A/B, Game Render Maps window for buffer inspection
  (gains an "HDR values > 1" blind spot — see task B5), adb pacing loop, reference
  video workflow, `Tools > BalloonParty` menu conventions.

## Task plan

Sizes: S ≤ half a day · M ≈ 1–2 days · L ≈ 3+ days. **Every wave ends with a José
in-editor/device verification gate — same rhythm as the URP migration.**

### Dependency graph

```
A0 baseline capture ─▶ A1 Linear switch ─▶ A2 look re-tune (art, L) ─▶ A-gate
                                                                        │
        B1 HDR target + post scaffold ─▶ B2 tonemap choice ─▶ B3 emissive authoring ─▶ B4 bloom ─▶ B-gate
                                                │
        B5 tooling: Render Maps HDR view ───────┘ (parallel, any time after B1)
        B6 GI/capture RT format + composite audit (after B1, before B-gate)
                                                                        │
        C1 HDR display output (deferred, separate trigger) ◀────────────┘
```

### Wave A — Linear color space (the foundation, and the cost gate)

#### A0 — Baseline captures · **P0 · S**
Same drill as the URP migration B0: reference video of every visual system, framedumps
of busy frames, device Profiler capture — all under Gamma, stored in `Baselines~/`
(`framedump_gamma_baseline` naming). The A2 re-tune is judged against these.

#### A1 — Flip to Linear · **P0 · S (mechanical) — do NOT merge without A2**
Editor: Project Settings → Player → Color Space → Linear. Everything re-renders
"washed/different" immediately — that is expected, not broken. Branch like the URP
migration (`hdr-color-pipeline`), because main stays shippable while A2 runs.
Compile-level risk: none (color space is a project setting). Runtime risk: shaders that
do their own gamma-ish math (check `pow(x, 2.2)`-style hacks — grep before flipping;
none known, verify).

#### A2 — Look re-tune · **P0 · L — art-driven, José-led**
Re-tune authored colors until the game reads like the A0 references (or deliberately
better): palette entries, particle gradients/materials, shader color constants (danger
gradient, GI shadow tint/bounce strength, cloud shading, bush tints), UI. Agents can
batch-adjust values you specify, but the eye is yours. Practical technique: run A0's
reference video side-by-side; work system-by-system with the checklist. Timebox
expectation: this is the longest single item in the whole plan.

#### A-gate — parity/quality sign-off + perf sanity
Linear costs nothing measurable by itself, but verify: device build, pacing sample,
GI/chrome/disturbance intact (their shaders are color-space-agnostic math, but their
*tints* were re-tuned). Merge to main here if desired — Linear stands alone as a
correctness win even if HDR waits.

### Wave B — HDR rendering + post + bloom

#### B1 — HDR target + post scaffold · **P0 · S**
URP asset: `Supports HDR` on, precision **32-bit** (`B10G11R11`; FP16 only if banding
shows in gradients — it doubles target bandwidth). Main camera: post-processing ON.
Scene: one global Volume with an (initially empty) profile — do not reuse
`DefaultVolumeProfile.asset`; author `Assets/Settings/URP/GameVolumeProfile.asset`.
Capture camera + bake cameras stay post-OFF (they must render raw values).
Measure immediately: the empty-post pass cost on device is the go/no-go number for
the whole wave.

#### B2 — Tonemapping · **P0 · S**
Volume override: Tonemapping = **Neutral** (ACES costs more and crushes saturated hues
— this game is saturated hues; audition both in-editor, decide by eye). Color Grading
Mode: HDR, LUT 32. Without tonemapping HDR values just clip at white — this is what
makes >1 values *render* as brightness rolloff.

#### B3 — Emissive authoring path · **P1 · M**
The palette gains HDR intensity: add `[ColorUsage(true, true)]` where entries should
author >1 (or an explicit per-entry intensity float if full HDR pickers are overkill —
decide in-editor by authoring one real effect). Sweep the effect systems that deserve
emitters: lightning, laser/beam, chrome specular sweep, rainbow glitter, level-up glow,
pop-VFX cores. Per-shader `fixed4 → half4` on the paths that now carry >1 values (the
25-file audit; only flagged shaders change — blanket conversion is churn).

#### B4 — Bloom · **P1 · S–M**
Volume override: Bloom, threshold ≥ 1 (only true HDR emitters bloom — the scene's LDR
content stays clean), low scatter to start. Tune per effect with José's eye. Device
cost check: bloom's downsample chain is the second-biggest new GPU cost after the post
pass itself.

#### B5 — Tooling: Render Maps HDR view · **P2 · S (parallel after B1)**
The Game Render Maps window draws raw values — HDR buffers will display clamped.
Add a small exposure slider (multiply in the ChannelPreview shader) + a "values > 1"
highlight toggle (tint pixels exceeding 1 red). Small, and it makes B3/B4 authoring
debuggable.

#### B6 — GI/capture chain under HDR · **P1 · M**
Decide per RT: capture stays LDR (reflections/GI read tonemapped-ish scene — cheapest,
probably fine at downscale-12) vs upgrade to `ARGBHalf` (alpha channel REQUIRED — the
coverage mask and shadow-amount semantics live in A; `B10G11R11` is ineligible).
Re-audit the GI overlay's multiplicative composite against the HDR scene behind it,
and the smear shaders' `fixed` precision. Verify with the Render Maps window (B5).

#### B-gate — device sign-off
Full reference sweep vs A-gate captures + Profiler/pacing on the Pixel at 120 Hz.
Gate: post + bloom fit the 8.33 ms budget with the game's busiest moments. If they
don't, the fallback ladder is: cheaper bloom (fewer mips) → Neutral-only post (no
bloom) → park Wave B until content demands it.

### Wave C — HDR display output · **deferred, own trigger**
True HDR10 output on capable panels (`allowHDRDisplaySupport`, paper-white/UI
calibration, tonemap-to-display). Meaningful only after B ships and only on HDR
screens; parked exactly like 2D lights were in the URP plan. Do not scope until B has
lived on devices for a while.

## Open questions (answer at execution time)

1. **Which effects get HDR emitters first?** José's call — the plan assumes lightning,
   beams, chrome sweep, glitter, level-up glow; the authoring pass (B3) should start
   with ONE showcase effect to validate the whole pipeline before sweeping.
2. **Palette authoring shape**: full HDR color pickers on `PaletteEntry`
   (`[ColorUsage]`) vs a separate intensity scalar per emissive use — decide after
   authoring the first effect in B3.
3. **Tonemapper**: Neutral vs ACES, decided by eye in B2 (audition both against the
   saturated balloon palette).
4. **Capture/GI RT policy** (B6): LDR-stays vs ARGBHalf-upgrade — decide from how
   wrong chrome reflections/GI bounce look once the scene is HDR, not speculatively.
