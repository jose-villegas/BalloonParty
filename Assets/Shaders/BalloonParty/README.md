# Shaders

Custom shaders for BalloonParty under `BalloonParty/` namespace.

## Shader list

| Shader | File | Used by |
|---|---|---|
| `BalloonParty/Balloon/RainbowBalloon` | `Balloon/RainbowBalloon.shader` | `BalloonRainbow` — "rainbow star" balloon. Replaces the flat sprite tint with scrolling diagonal colour bands cycling through up to four selectable colours (`_Color0`.._`Color3`, `_BandCount`), meant to be driven at runtime from the level's allowed colours; keeps SpriteShineShadow's diagonal shine sweep + soft drop shadow. `_StripeCount`/`_ScrollSpeed`/`_BandBlend`/`_BandAngle` tune stripe density, motion, edge softness, direction. Per-instance `_TimeOffset` (+ band colours/count) via `MaterialPropertyBlock`; GPU instancing **disabled** |
| `BalloonParty/Balloon/ToughBalloon` | `Balloon/ToughBalloon.shader` | `ToughBalloonMaterial` — procedural cracked-rubber surface with voronoi stress cracks, driven by `_DamageProgress` and `_VoronoiSeed` per-instance via `MaterialPropertyBlock`. Ash/crack coloring via `[GradientTexture]` baked gradients; `_SHADOW_OFF` toggle compiles out the built-in 9-tap shadow when a baked shadow child covers it |
| `BalloonParty/Sprite/SpriteShadow` | `Sprite/SpriteShadow.shader` | `BalloonMaterial`, `ProjectileMaterial`, `ToughBalloonShadow`, `ShadowSprite`, `ScoreTrail`, `ShieldTrail`, trail/particle materials — sprite with 9-tap soft drop shadow, UV-based sprite scaling, and UI stencil support |
| `BalloonParty/Sprite/SpriteShadowComposite` | `Sprite/SpriteShadowComposite.shader` | `PSMaterial_ShieldGain` — extends SpriteShadow with a second sprite layer composited via Porter-Duff "over" |
| `BalloonParty/Sprite/Blur` | `Sprite/SpriteBlur.shader` | `SpecularBalloonBlur`, `SpecularBlur`, `SpecularBlur2`, `BlurSprite` — 9-tap box blur with configurable radius and sprite scale margin |
| `BalloonParty/Sprite/ShinyDefault` | `Sprite/SpriteShine.shader` | Specular shine overlay — diagonal shine band controlled by `_ShineLocation` and `_ShineWidth` |
| `BalloonParty/Sprite/SpriteShineShadow` | `Sprite/SpriteShineShadow.shader` | Combines ShinyDefault's periodic diagonal shine band with SpriteShadow's drop shadow — for sprites and particle systems that want both in one pass |
| `BalloonParty/Paint/PaintBlob` | `Paint/PaintBlob.shader` | `PaintBlob`, `PaintFlyingBlob` — procedural wobbling paint blob with dual-frequency sine wobble, rim darkening, specular highlight, and optional shadow with configurable `_ShadowScale`. `_SpriteScale` controls content inset within the quad, preventing shadow clipping at edges. Per-instance `_TimeOffset`, `_ShadowScale`, and `_SpriteScale` via `MaterialPropertyBlock` |
| `BalloonParty/Balloon/SoapBubbleCluster` | `Balloon/SoapBubbleCluster.shader` | ⚗️ **Investigation prototype** — up to 5 discrete soap-bubble circles on a single quad. Uses Voronoi-style per-circle SDF ownership (no metaball merging). `_BubbleCount` (1–5) controls the active cluster size via `MaterialPropertyBlock` driven by `SoapBubbleClusterRenderer`. Features: iridescent rim hue, Plateau junction membrane between touching bubbles, per-bubble specular, gentle independent micro-float animation per bubble |
| `BalloonParty/Balloon/UnbreakableBalloon` | `Balloon/UnbreakableBalloon.shader` | Chrome sprite shader for the 4-quadrant composed sphere. Convex-mirror reflection samples the global `_SceneCaptureTex` (bound by `SceneCaptureService` — replaced the GrabPass, whose mid-frame resolve stalls tile GPUs) with sphere-normal UV deflection, radial metallic gradient (center bright / edge dark), specular highlight at a fixed sphere-local position for 3D depth cue, traveling chrome rim sweep, periodic diagonal shine band, and `_DeflectFlash` for hit feedback. Per-instance `_SphereCenter`, `_TimeOffset` via `MaterialPropertyBlock`. GPU instancing **disabled** (MPB) |
| `BalloonParty/Balloon/UnbreakableBalloonRim` | `Balloon/UnbreakableBalloonRim.shader` | Lightweight chrome-rim shader for the Unbreakable's ring sprites (outer + inner circles) — static metallic rim on the sprite's alpha edge plus an animated rotating sweep highlight. Sphere position derived from sprite UV, so no MPB push is needed and GPU instancing **works** across both rings |
| `BalloonParty/Grid/PuffCloud` | `Grid/PuffCloud.shader` | Procedural cloud on a SpriteRenderer quad (no assigned sprite). Three fetches of a tileable baked noise texture (`_NoiseTex`, 16-bit PNG histogram-matched to the original simplex — regenerate via Tools > BalloonParty > Generate Cloud Noise Texture), one per octave, sampled in **world space** for spatial stability: one continuous field board-wide, so noise never seams at cluster borders (there are no per-cluster seed offsets — `ClusterView` still writes a seed in slot `.z`, the shader ignores it). Slot-center array (`_SlotCentersWorld`) drives boundary falloff and occupancy masking via a smooth-min union (`_SlotBlend` — a hard min webs the fade band along slot midlines) — works identically for single-slot (1 entry) and merged clusters (N entries). Fragments outside every slot's falloff **discard before any texture fetch** (the quad spans the bounding box of all clusters, so most fragments bail); the `_SHADOW_ON` shadow only evaluates its two noise fetches where its own falloff reaches and the cloud doesn't fully cover it. Directional lighting derives its pseudo-normal from **`ddx`/`ddy` screen-space derivatives** of the low-frequency noise the density pass already computed — no extra noise evaluations. Optional `_DENSITY_ON` masking/displacement from the global `_DisturbanceTex`, `_NOISE_DEBUG` grayscale field visualizer (skips the early-out). Per-instance `_SlotCentersWorld`, `_SlotCount`, `_AnimationSpeed`, `_TimeOffset` via `MaterialPropertyBlock` (runtime animation is `_Time`-driven; `_TimeOffset` is only fed in edit mode). GPU instancing **disabled** (MPB) |
| `Hidden/BalloonParty/Grid/DisturbanceDiffusion` | `Grid/DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 Gaussian blur weighted toward equilibrium (1.0). `_DiffusionRate` controls spatial smoothing, `_ReformSpeed` × `_DeltaTime` controls temporal recovery. Used by `DisturbanceFieldService` in a ping-pong blit each diffusion tick |
| `Hidden/BalloonParty/Grid/DisturbanceStamp` | `Grid/DisturbanceStamp.shader` | Disturbance stamp blit — subtracts a radial falloff from the density field at `_StampCenter`. Optional directional wake via `_StampDirection` elongates the stamp into a teardrop shape. Single-stamp version, kept for reference |
| `Hidden/BalloonParty/Grid/DisturbanceStampBatched` | `Grid/DisturbanceStampBatched.shader` | Batched disturbance stamp blit — processes up to 32 stamps in a single blit via uniform arrays. Used by `DisturbanceFieldService.FlushPendingStamps()` |
| `BalloonParty/Grid/BushBranch` | `Grid/BushBranch.shader` | `BushView` — unlit alpha-test branch map shader. Samples `_MainTex` (baked branch map), tints with `_BranchColor`, depth shading via alpha channel. Material created at runtime by `BushView` from `IBushSettings.BranchShader` + variant's `BranchMap` texture. GPU instancing **enabled** |
| `Hidden/BalloonParty/Display/ScreenSpaceLightSmear` | `Display/ScreenSpaceLightSmear.shader` | Light-buffer builder for `ScreenSpaceLightService` — three blit passes over the scene capture: pass 0 marches 8 taps from each pixel toward the light (occluder alpha → shadow in A, occluder color → bounce in RGB), pass 1 is a 3×3 box soften, pass 2 temporally blends against the previous frame's buffer (`_HistoryTex`/`_TemporalBlend`) |
| `BalloonParty/Display/ScreenSpaceLightOverlay` | `Display/ScreenSpaceLightOverlay.shader` | Fullscreen composite for `ScreenSpaceLightService` — camera-fitted quad above all gameplay, NOT a post effect: multiplicative `DstColor SrcColor` blend (0.5-neutral) tints the frame in place with no framebuffer readback, sampling only the low-res light buffer (`_ShadowTint`/`_ShadowStrength`/`_BounceStrength`) |
| `BalloonParty/Grid/BushLeaf` | `Grid/BushLeaf.shader` | `BushView` — instanced alpha-blend leaf shader for `DrawMeshInstanced`. **GPU-driven animation:** wind idle (sine + dual-sine noise) and rattle (disturbance field sampling) computed entirely in the vertex shader — zero CPU animation cost. Per-instance `_LeafTint` (color), `_UVRect` (atlas sub-rect), and `_LeafWind` (phase, depth, baseAngle, scale) via instancing buffer + `MaterialPropertyBlock`. Features: pivot-based rotation, 9-tap soft drop shadow, specular highlight, `_RATTLE_ON` keyword for disturbance field reaction (samples global `_DisturbanceTex` via `tex2Dlod`). Shadow and highlight offsets are rotation-independent (inverse-rotated in vertex shader from the GPU-computed angle). Material created at runtime by `BushView` from `IBushSettings.LeafMaterial`. GPU instancing **enabled** |

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

