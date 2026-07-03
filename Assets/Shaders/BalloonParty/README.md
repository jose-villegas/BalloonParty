# Shaders

Custom shaders for BalloonParty under `BalloonParty/` namespace.

## Shader list

| Shader | File | Used by |
|---|---|---|
| `BalloonParty/Balloon/ToughBalloon` | `Balloon/ToughBalloon.shader` | `ToughBalloonMaterial` — procedural cracked-rubber surface with voronoi stress cracks, driven by `_DamageProgress` and `_VoronoiSeed` per-instance via `MaterialPropertyBlock` |
| `BalloonParty/Sprite/SpriteShadow` | `Sprite/SpriteShadow.shader` | `BalloonMaterial`, `ProjectileMaterial`, `ToughBalloonShadow`, `ShadowSprite`, `ScoreTrail`, `ShieldTrail`, trail/particle materials — sprite with 9-tap soft drop shadow, UV-based sprite scaling, and UI stencil support |
| `BalloonParty/Sprite/SpriteShadowComposite` | `Sprite/SpriteShadowComposite.shader` | `PSMaterial_ShieldGain` — extends SpriteShadow with a second sprite layer composited via Porter-Duff "over" |
| `BalloonParty/Sprite/Blur` | `Sprite/SpriteBlur.shader` | `SpecularBalloonBlur`, `SpecularBlur`, `SpecularBlur2`, `BlurSprite` — 9-tap box blur with configurable radius and sprite scale margin |
| `BalloonParty/Sprite/ShinyDefault` | `Sprite/SpriteShine.shader` | Specular shine overlay — diagonal shine band controlled by `_ShineLocation` and `_ShineWidth` |
| `BalloonParty/Paint/PaintBlob` | `Paint/PaintBlob.shader` | `PaintBlob`, `PaintFlyingBlob` — procedural wobbling paint blob with dual-frequency sine wobble, rim darkening, specular highlight, and optional shadow with configurable `_ShadowScale`. `_SpriteScale` controls content inset within the quad, preventing shadow clipping at edges. Per-instance `_TimeOffset`, `_ShadowScale`, and `_SpriteScale` via `MaterialPropertyBlock` |
| `BalloonParty/Balloon/SoapBubbleCluster` | `Balloon/SoapBubbleCluster.shader` | ⚗️ **Investigation prototype** — up to 5 discrete soap-bubble circles on a single quad. Uses Voronoi-style per-circle SDF ownership (no metaball merging). `_BubbleCount` (1–5) controls the active cluster size via `MaterialPropertyBlock` driven by `SoapBubbleClusterRenderer`. Features: iridescent rim hue, Plateau junction membrane between touching bubbles, per-bubble specular, gentle independent micro-float animation per bubble |
| `BalloonParty/Balloon/UnbreakableBalloon` | `Balloon/UnbreakableBalloon.shader` | Chrome sprite shader for the 4-quadrant composed sphere. GrabPass convex-mirror reflection using sphere-normal UV deflection, radial metallic gradient (center bright / edge dark), specular highlight at a fixed sphere-local position for 3D depth cue, traveling chrome rim sweep, periodic diagonal shine band, and `_DeflectFlash` for hit feedback. Per-instance `_SphereCenter`, `_TimeOffset` via `MaterialPropertyBlock`. GPU instancing **disabled** (GrabPass + MPB) |
| `BalloonParty/Grid/PuffCloud` | `Grid/PuffCloud.shader` | Procedural cloud on a SpriteRenderer quad (no assigned sprite). Three octaves of a tileable baked noise texture (`_NoiseTex`, histogram-matched to the original simplex — regenerate via Tools > BalloonParty > Generate Cloud Noise Texture) sampled in **world space** for spatial stability: one continuous field board-wide, so noise never seams at cluster borders. Slot-center array (`_SlotCentersWorld`) drives boundary falloff and occupancy masking via a smooth-min union (`_SlotBlend` — a hard min webs the fade band along slot midlines) — works identically for single-slot (1 entry) and merged clusters (N entries). Optional `_SHADOW_ON` shadow pass, optional `_DENSITY_ON` density RT masking (P2), `_NOISE_DEBUG` grayscale field visualizer. Per-instance `_TimeOffset`, `_SlotCentersWorld`, `_SlotCount`, `_DensityTex` via `MaterialPropertyBlock`. GPU instancing **disabled** (MPB) |
| `Hidden/BalloonParty/Grid/DisturbanceDiffusion` | `Grid/DisturbanceDiffusion.shader` | Density field diffusion blit — 3×3 Gaussian blur weighted toward equilibrium (1.0). `_DiffusionRate` controls spatial smoothing, `_ReformSpeed` × `_DeltaTime` controls temporal recovery. Used by `DisturbanceFieldService` in a ping-pong blit each diffusion tick |
| `Hidden/BalloonParty/Grid/DisturbanceStamp` | `Grid/DisturbanceStamp.shader` | Disturbance stamp blit — subtracts a radial falloff from the density field at `_StampCenter`. Optional directional wake via `_StampDirection` elongates the stamp into a teardrop shape. Single-stamp version, kept for reference |
| `Hidden/BalloonParty/Grid/DisturbanceStampBatched` | `Grid/DisturbanceStampBatched.shader` | Batched disturbance stamp blit — processes up to 16 stamps in a single blit via uniform arrays. Used by `DisturbanceFieldService.FlushPendingStamps()` |
| `BalloonParty/Grid/BushBranch` | `Grid/BushBranch.shader` | `BushView` — unlit alpha-test branch map shader. Samples `_MainTex` (baked branch map), tints with `_BranchColor`, depth shading via alpha channel. Material created at runtime by `BushView` from `IBushSettings.BranchShader` + variant's `BranchMap` texture. GPU instancing **enabled** |
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

