# Shaders

Custom shaders for BalloonParty under `BalloonParty/` namespace.

## Pipeline

The project runs URP's 2D Renderer (migrated from Built-in, 2026-07). None of the hand-written `CGPROGRAM` shaders below were ported: their passes carry no `LightMode`/`RenderPipeline` tag, so URP runs them as `SRPDefaultUnlit` compatibility passes — this is a supported fallback, not a hack, and every shader in this folder renders correctly under it with zero visual changes.

### Porting a shader to URP-native (if compatibility ever breaks)

Mechanical steps, per shader:
- Add `Tags { "RenderPipeline"="UniversalPipeline" }` to the Pass (or SubShader), and rename `CGPROGRAM`/`ENDCG` to `HLSLPROGRAM`/`ENDHLSL`.
- Swap `#include "UnityCG.cginc"` for `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`.
- Replace `UnityObjectToClipPos(v)` with `TransformObjectToHClip(v.xyz)`; `fixed` → `half`.
- `tex2D`/`sampler2D` still compile under URP HLSL — keep them for a minimal diff rather than rewriting to `TEXTURE2D`/`SAMPLE_TEXTURE2D` unless the shader is being touched anyway.
- Keep MPB-driven properties (`_SphereCenter`, `_TimeOffset`, `_DamageProgress`, etc.) as plain uniforms — these renderers are already SRP-Batcher-excluded by MPB use, so wrapping them in a `UnityPerMaterial` CBUFFER buys nothing; only add that hygiene to shaders whose materials carry no MPB.
- Instancing (`BushLeaf`): `UNITY_INSTANCING_BUFFER` macros are unchanged under URP — verify `#pragma multi_compile_instancing` survives and instanced batches still appear in Frame Debugger; this is the highest-risk silent breakage.

## Shader list

| Shader | File | Used by |
|---|---|---|
| `BalloonParty/Balloon/RainbowBalloon` | `Balloon/RainbowBalloon.shader` | `BalloonRainbow` — "rainbow star" balloon. Replaces the flat sprite tint with scrolling colour bands cycling through up to four selectable colours (`_RainbowBandColor0`.._`RainbowBandColor3`, `_RainbowBandCount`) — the level's allowed set, identical for every rainbow, so they are **global** uniforms pushed once per level by `LevelDifficultyResolver` (NOT in the `Properties` block, so a serialized material value can't shadow the global; the editor bands preview feeds the same names via MPB since edit mode has no resolver). Bands and shine axis each have per-material opt-in toggles to follow `_SceneLightDir` (bands scroll along it; shine sweeps DOWN-light, entering from the lit side). Seam swirl (`_SeamSwirlAmount/Scale/Speed`, dual-sine, shared by seams and shine) and spherical bulge (`_SphereBend`/`_SphereCenter`/`_SphereRadius`, C1-smooth dome so banding stays continuous). Adds a scattered twinkling **glitter** layer on top of the shine — grid-hash sparkle field (`Hash21`, no texture) tuned via `_GlitterDensity`/`_GlitterSize`/`_GlitterChance`/`_GlitterSpeed`/`_GlitterSharpness`/`_GlitterBrightness`. `_MaskMin`/`_MaskMax` (UV rect) + `_MaskSoftness` exclude a region from the band tint; zero-size rect (default) disables the mask. Only per-instance `_TimeOffset` (random band/swirl phase) is still pushed per-renderer via `MaterialPropertyBlock` (band colours/count are global, above); that lone MPB keeps the renderer SRP-Batcher-excluded, so GPU instancing stays **disabled** |
| `BalloonParty/Balloon/ToughBalloon` | `Balloon/ToughBalloon.shader` | `ToughBalloonMaterial` — procedural cracked-rubber surface with voronoi stress cracks, driven by `_DamageProgress` and `_VoronoiSeed` per-instance via `MaterialPropertyBlock`. Grain directional lighting derives from the global `_SceneLightDir` (negated — the grain math brightens away from the light). Ash/crack coloring via `[GradientTexture]` baked gradients; `_SHADOW_OFF` toggle compiles out the built-in 9-tap shadow when a baked shadow child covers it |
| `BalloonParty/Sprite/SpriteShadow` | `Sprite/SpriteShadow.shader` | `BalloonMaterial`, `ProjectileMaterial`, `ToughBalloonShadow`, `ShadowSprite`, `ScoreTrail`, `ShieldTrail`, trail/particle materials — sprite with 9-tap soft drop shadow (opt-in "Follow Scene Light" toggle, default = authored `_ShadowOffset`), UV-based sprite scaling, and UI stencil support |
| `BalloonParty/Sprite/SpriteShadowComposite` | `Sprite/SpriteShadowComposite.shader` | `PSMaterial_ShieldGain` — extends SpriteShadow with a second sprite layer composited via Porter-Duff "over" (shadow direction optional, default = authored offset) |
| `BalloonParty/Sprite/Blur` | `Sprite/SpriteBlur.shader` | `SpecularBalloonBlur`, `SpecularBlur`, `SpecularBlur2`, `BlurSprite` — 9-tap box blur with configurable radius and sprite scale margin |
| `BalloonParty/Sprite/Diffuse` | `Sprite/SpriteDiffuse.shader` | The scene light's DIFFUSE term for ordinary sprites: renders the sprite multiplied by the global light's colour × intensity (albedo × light) — makes a tinted/dimmed light read on surfaces, not just speculars/shines. `_LightInfluence` (0-1) eases between unlit and fully lit so gameplay colour identity stays readable (scenery can run at 1). Neutral at white × 1 / before the owner's first push |
| `BalloonParty/Sprite/LightDriven` | `Sprite/SpriteLightDriven.shader` | `ShadowRotation`, `ShineRotation` — sprite accessory fully driven by the scene light: placement, orientation, and colour response, each independently toggleable. **orbit** — `_OrbitDistance` places the sprite down-light of its rest position in world space (scaled by the transform's world scale), replacing an authored transform offset like a baked shadow child's `(0.04, -0.04)` (zero the child's localPosition, put the magnitude in the material); **rotate** — `_RotateArt` swings the art so its authored `_BakedAngle` lies down-light. Colour response per archetype: `_TintBySceneLight` multiplies the sprite by the light's colour × intensity (glints ARE reflected light); `_FadeWithSceneLight` scales opacity by intensity, clamped at authored (shadows: no light, no shadow). Baked 2D ground shadows want orbit + fade WITHOUT rotate/tint (their squashed shape encodes ground perspective) — direction-baked glints want orbit + rotate + tint + fade (a glint must vanish without light, not darken). Rotation hinges on the sprite pivot; the transform's own world rotation is compensated so parent sway doesn't drag the baked direction |
| `BalloonParty/Sprite/ShinyDefault` | `Sprite/SpriteShine.shader` | Specular shine overlay — diagonal shine band with opt-in "Shine Follows Scene Light" toggle (default = classic 45° diagonal), controlled by `_ShineLocation` and `_ShineWidth` |
| `BalloonParty/Sprite/SpriteShineShadow` | `Sprite/SpriteShineShadow.shader` | Combines ShinyDefault's periodic shine band with SpriteShadow's drop shadow — for sprites and particle systems that want both in one pass. Both shadow direction and shine axis have independent opt-in toggles to follow `_SceneLightDir` (defaults = authored offsets) |
| `BalloonParty/Sprite/Glitter` | `Sprite/SpriteGlitter.shader` | Scattered twinkling glitter over any sprite — reuses `RainbowBalloon`'s grid-hash sparkle (`Hash21` + `GlitterAmount`) in the standard premultiplied sprite pass. Tuned via `_GlitterColor` (HDR), `_GlitterDensity`/`_GlitterSize`/`_GlitterChance`/`_GlitterSpeed`/`_GlitterSharpness`/`_GlitterBrightness`; masked by sprite alpha |
| `BalloonParty/Sprite/GlitterSwirl` | `Sprite/SpriteGlitterSwirl.shader` | Same sparkle as `Sprite/Glitter`, but the assigned sprite is **never drawn** — its alpha only masks where specks may appear (a shaped sprite confines the glints) — plus motion: the field drifts toward `_Drift` and each speck orbits (`_SwirlSpeed`/`_SwirlRadius`) so it reads as spinning around a beam. `_MirrorX` splits the sprite at its centre along the local `_MirrorAxis` (follows rotation) and reverses the motion on the far half so the two sides flow oppositely — set `_MirrorAxis` per sprite when beams face different ways. Same `_Glitter*` knobs; set `_GlitterColor` alpha to 0 for pure-additive glow |
| `BalloonParty/Sprite/SightSmoke` | `Sprite/SightSmoke.shader` | Sprite whose alpha is eaten by a scrolling noise mask — reads as drifting smoke covering/uncovering parts of it (e.g. a laser aim sight). Two offset samples of `_NoiseTex` drift past each other; tuned via `_NoiseScale`/`_ScrollSpeed`/`_SmokeStrength`/`_SmokeContrast`/`_MinVisibility`. Standard premultiplied sprite pass |
| `BalloonParty/Paint/PaintBlob` | `Paint/PaintBlob.shader` | `PaintBlob`, `PaintFlyingBlob` — procedural wobbling paint blob with dual-frequency sine wobble, rim darkening, specular highlight, and optional shadow. Specular hotspot and drop shadow are both world-anchored and derived from `_SceneLightDir` (shadow extends opposite direction). Per-material magnitude knobs: `_SpecularDistance`, `_ShadowDistance`, `_ShadowScale`. `_SpriteScale` controls content inset within the quad, preventing shadow clipping at edges. Per-instance `_TimeOffset`, `_ShadowScale`, and `_SpriteScale` via `MaterialPropertyBlock` |
| `BalloonParty/Balloon/SoapBubbleCluster` | `Balloon/SoapBubbleCluster.shader` | ⚗️ **Investigation prototype** — up to 5 discrete soap-bubble circles on a single quad. Uses Voronoi-style per-circle SDF ownership (no metaball merging). `_BubbleCount` (1–5) controls the active cluster size via `MaterialPropertyBlock` driven by `SoapBubbleClusterRenderer`. Features: iridescent rim hue, Plateau junction membrane between touching bubbles, per-bubble specular, gentle independent micro-float animation per bubble |
| `BalloonParty/Balloon/UnbreakableBalloon` | `Balloon/UnbreakableBalloon.shader` | Chrome sprite shader for the 4-quadrant composed sphere. Convex-mirror reflection samples the global `_SceneCaptureTex` (bound by `SceneCaptureService` — replaced the GrabPass, whose mid-frame resolve stalls tile GPUs) with sphere-normal UV deflection, radial metallic gradient (center bright / edge dark), specular highlight (position + streak angle derived from `_SceneLightDir`), traveling chrome rim sweep, periodic shine band with opt-in to sweep down-light sphere-coherently across quadrants, and `_DeflectFlash` for hit feedback. Per-instance `_SphereCenter`, `_TimeOffset` via `MaterialPropertyBlock`. GPU instancing **disabled** (MPB) |
| `BalloonParty/Balloon/UnbreakableBalloonRim` | `Balloon/UnbreakableBalloonRim.shader` | Lightweight chrome-rim shader for the Unbreakable's ring sprites (outer + inner circles) — static metallic rim on the sprite's alpha edge plus an animated rotating sweep highlight. Sphere position derived from sprite UV, so no MPB push is needed and GPU instancing **works** across both rings |
| `BalloonParty/Grid/PuffCloud` | `Grid/PuffCloud.shader` | Procedural cloud on a SpriteRenderer quad (no assigned sprite). Three fetches of a tileable baked noise texture (`_NoiseTex`, 16-bit PNG histogram-matched to the original simplex — regenerate via Tools > BalloonParty > Generate Cloud Noise Texture), one per octave, sampled in **world space** for spatial stability: one continuous field board-wide, so noise never seams at cluster borders (there are no per-cluster seed offsets — `ClusterView` still writes a seed in slot `.z`, the shader ignores it). Slot-center array (`_SlotCentersWorld`) drives boundary falloff and occupancy masking via a smooth-min union (`_SlotBlend` — a hard min webs the fade band along slot midlines) — works identically for single-slot (1 entry) and merged clusters (N entries). Fragments outside every slot's falloff **discard before any texture fetch** (the quad spans the bounding box of all clusters, so most fragments bail); the `_SHADOW_ON` shadow only evaluates its two noise fetches where its own falloff reaches and the cloud doesn't fully cover it. Directional lighting derives its pseudo-normal from **`ddx`/`ddy` screen-space derivatives** of the low-frequency noise and the global `_SceneLightDir` — no extra noise evaluations. Optional `_DENSITY_ON` masking/displacement from the global `_DisturbanceTex`, `_NOISE_DEBUG` grayscale field visualizer (skips the early-out). Per-instance `_SlotCentersWorld`, `_SlotCount`, `_AnimationSpeed`, `_TimeOffset` via `MaterialPropertyBlock` (runtime animation is `_Time`-driven; `_TimeOffset` is only fed in edit mode). GPU instancing **disabled** (MPB) |
| `Hidden/BalloonParty/Grid/DisturbanceDiffusion` | `Grid/DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 Gaussian blur weighted toward equilibrium (1.0). `_DiffusionRate` controls spatial smoothing, `_ReformSpeed` × `_DeltaTime` controls temporal recovery. Used by `DisturbanceFieldService` in a ping-pong blit each diffusion tick |
| `Hidden/BalloonParty/Grid/DisturbanceStampBatched` | `Grid/DisturbanceStampBatched.shader` | Batched disturbance stamp blit — processes up to 32 stamps in a single blit via uniform arrays. Used by `DisturbanceFieldService.FlushPendingStamps()` |
| `BalloonParty/Grid/BushBranch` | `Grid/BushBranch.shader` | `BushView` — unlit alpha-test branch map shader. Samples `_MainTex` (baked branch map), tints with `_BranchColor` (× the `_BranchGradient`), depth shading via alpha channel. A radial central **AO blob** simulates occlusion under the trunk: `_AOColor`/`_AORadius`/`_AOSoftness` shape it, `_AOIntensity` scales its strength. All driven from `IBushSettings` (`BranchColor`, `BranchAO*`). Material created at runtime by `BushView` from `IBushSettings.BranchShader` + variant's `BranchMap` texture. GPU instancing **enabled** |
| `Hidden/BalloonParty/Display/ScreenSpaceLightSmear` | `Display/ScreenSpaceLightSmear.shader` | Light-buffer builder for `ScreenSpaceLightService` — two blit passes over the scene capture: pass 0 marches 8 taps from each pixel toward the light (occluder alpha → shadow in A, occluder color → bounce in RGB), pass 1 is a 3×3 box soften |
| `BalloonParty/Display/ScreenSpaceLightOverlay` | `Display/ScreenSpaceLightOverlay.shader` | Fullscreen composite for `ScreenSpaceLightService` — camera-fitted quad above all gameplay, NOT a post effect: multiplicative `DstColor SrcColor` blend (0.5-neutral) tints the frame in place with no framebuffer readback, sampling only the low-res light buffer (`_ShadowTint`/`_ShadowStrength`/`_BounceStrength`) |
| `BalloonParty/Grid/BushLeaf` | `Grid/BushLeaf.shader` | `BushView` — instanced alpha-blend leaf shader for `DrawMeshInstanced`. **GPU-driven animation:** wind idle (sine + dual-sine noise) and rattle (disturbance field sampling) computed entirely in the vertex shader — zero CPU animation cost. Per-instance `_LeafTint` (color), `_UVRect` (atlas sub-rect), and `_LeafWind` (phase, depth, baseAngle, scale) via instancing buffer + `MaterialPropertyBlock`; a material-level `_LeafColor` (from `IBushSettings.LeafTint`) multiplies the per-instance tint for a global foliage tint knob. Features: pivot-based rotation, 9-tap soft drop shadow (direction from `_SceneLightDir`, magnitude `_ShadowDistance` fed from IBushSettings via BushMaterialSet), specular highlight (direction from `_SceneLightDir`, magnitude `_HighlightDistance` — a material property), `_RATTLE_ON` keyword for disturbance field reaction (samples global `_DisturbanceTex` via `tex2Dlod`). Shadow and highlight directions are rotation-independent (inverse-rotated in vertex shader from the GPU-computed angle). Material created at runtime by `BushView` from `IBushSettings.LeafMaterial`. GPU instancing **enabled** |

## GPU instancing

All shaders support GPU instancing following Unity's `UnitySprites.cginc` pattern:

```hlsl
#pragma multi_compile_instancing

// Per-instance SpriteRenderer.color — matches Unity's internal buffer name
#ifdef UNITY_INSTANCING_ENABLED
    UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
        UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
    UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
    #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
#else
    fixed4 _RendererColor;
#endif

fixed4 _Color;  // material tint — regular uniform, not instanced

// In vertex shader:
OUT.color = IN.color * _Color * _RendererColor;
```

### Key rules

- **`_RendererColor`** must be declared in the `Properties` block with default `(1,1,1,1)`. SpriteRenderer populates it via the instancing buffer; other renderer types (TrailRenderer, ParticleSystemRenderer, UI) use the material default.
- **`_Color`** stays as a regular uniform — it is the material-level tint, not per-instance.
- **Do NOT use `CBUFFER_START(UnityPerDrawSprite)`** — it is only populated for SpriteRenderer. Use a plain `#else fixed4 _RendererColor;` fallback instead.
- Materials using `MaterialPropertyBlock` for per-instance shader properties (`_DamageProgress`, `_VoronoiSeed`, `_TimeOffset`, `_ShadowScale`, `_SpriteScale`) must have GPU instancing **disabled** — instancing batching discards MPB values for properties not in the instancing buffer.
- Materials on ParticleSystem or TrailRenderer using custom shaders must have GPU instancing **disabled** — these renderer types do not populate `unity_SpriteRendererColorArray`.

## Code Reuse — Shared Includes

Shared logic lives in `.cginc` files under `Include/` (or `Noise/` for procedural generation). Any function, constant, or pattern that appears in two or more shaders **must** be extracted into a shared include — copy-pasting shader code across files is a maintenance hazard identical to duplicating C# methods.

### Existing includes

| Include | Location | What it provides |
|---|---|---|
| `SceneLight.cginc` | `Include/` | All scene-light access — direction, tint, magnitude, field sampling, palette decode, shadow fade. The single source of truth for lighting uniforms and helpers |
| `CloudNoise.cginc` | `Include/` | Single-octave sampler (`CloudNoiseOctaveTex2D`/`CloudNoiseOctaveTex2Dlod`) over the shared tileable baked noise texture (`_NoiseTex`, `_NoisePeriod`), used by both `BackgroundFieldGen.cginc` (the density bake) and `PuffCloud.shader` (per-fragment puffs). Only the octave fetch is shared — each call site keeps its own three-octave blend, since they differ in structure (world-offset/time source, low-quality branch) |
| `SimplexNoise2D.cginc` | `Noise/` | 2D simplex noise `SimplexNoise2D(float2 p)` → `[-1, 1]` |
| `GielisSDF.cginc` | `Grid/Editor/` | Gielis superformula SDF for procedural leaf/petal shapes |
| `LeafVeins.cginc` | `Grid/Editor/` | Procedural leaf vein pattern for bake shaders |

### Rules

1. **Extract on second use.** The first time a function appears in one shader, it stays inline. The moment it appears in a second shader, extract into a `.cginc`.
2. **One responsibility per include.** Each `.cginc` covers a single concern (shadow blur, glitter hash, sprite scaling). Don't bundle unrelated helpers.
3. **Include-guard every file.** Use `#ifndef BALLOONPARTY_<NAME>_INCLUDED` / `#define` / `#endif`.
4. **No uniforms in includes** unless they are truly global (scene-wide, set by a service). Per-material properties stay declared in the shader that uses them. `SceneLight.cginc` is the exception — its uniforms are global and must NOT appear in any `Properties` block.
5. **Inline helper functions.** Mark all include functions `inline` — the compiler can eliminate call overhead and the intent (small reusable helper) is clear.
6. **`Include/` for cross-domain, subfolder for domain-specific.** Helpers used across Sprite/, Balloon/, Grid/ live in `Include/`. Helpers used only within one domain (e.g., grid-only SDF) live in that domain's subfolder.
7. **Name constants, not magic numbers.** Numeric literals that carry meaning (`6.2831853`, `0.0174533`, `0.707`) must be `#define` constants in the include or at the top of the shader.

### Identified shared patterns (extraction candidates)

| Pattern | Current locations | Target include |
|---|---|---|
| 9-tap box-blur shadow (`SampleAlpha` + `SoftShadowAlpha`) | SpriteShadow, SpriteShadowComposite, SpriteShineShadow, BushBranch, BushLeaf | `Include/ShadowBlur.cginc` |
| Glitter hash + grid sparkle (`Hash21` + `GlitterAmount`) | SpriteGlitter, SpriteGlitterSwirl, RainbowBalloon | `Include/Glitter.cginc` |
| Shine sweep band (periodic sweep timing + projection) | SpriteShine, SpriteShineShadow, RainbowBalloon | `Include/ShineSweep.cginc` |
| Sprite-centre VTF light sampling (`spriteCenterWorld` + `SceneLightDirectionAtLOD`) | SpriteShadow, SpriteShineShadow, SpriteLightDriven, SpriteDiffuse, PaintBlob, BushBranch, BushLeaf | Already in `SceneLight.cginc` (helpers exist); vertex boilerplate is trivial (2 lines) — no include needed |
| Sprite scale + bounds mask (`ScaleSpriteUV` + `inBounds`) | SpriteShadow, SpriteShadowComposite, SpriteShineShadow, PaintBlob, BushBranch, BushLeaf | `Include/SpriteScale.cginc` |
| Porter-Duff "over" composite | SpriteShadow, SpriteShadowComposite, SpriteShineShadow, PaintBlob, BushBranch, BushLeaf | `Include/Composite.cginc` |
| Radial falloff (aspect-corrected smoothstep) | DisturbanceStampBatched, SceneLightAccumulate | `Include/RadialFalloff.cginc` |
| Math constants (`TAU`, `DEG_TO_RAD`, `HALF_SQRT2`) | 20+ shaders use `6.2831853`, `0.0174533`, `0.707` inline | `Include/MathConst.cginc` |

### Include path convention

All includes use project-relative paths from the shader's location:
```hlsl
#include "../Include/SceneLight.cginc"
#include "../Include/ShadowBlur.cginc"
#include "../Noise/SimplexNoise2D.cginc"
```

### Comments in shaders

Same rule as C# — comment the **why**, not the what. Shader comments should explain:
- Why a particular approximation was chosen over the exact math
- Why a tap/sample count was picked (performance vs quality trade-off)
- Why a fallback exists (e.g., "field not yet pushed at edit time")
- Platform/hardware constraints a section works around

Do NOT comment:
- What a standard operation does (`// sample the texture`, `// transform to clip space`)
- What a well-named function returns (`// returns the shadow alpha`)

