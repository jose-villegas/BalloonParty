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
| `BalloonParty/Grid/PuffCloud` | `Grid/PuffCloud.shader` | Procedural cloud on a SpriteRenderer quad (no assigned sprite). Three-octave Simplex noise sampled in **world space** for spatial stability. Slot-center array (`_SlotCentersWorld`) drives boundary falloff and occupancy masking — works identically for single-slot (1 entry) and merged clusters (N entries). Optional `_SHADOW_ON` shadow pass. Per-instance `_TimeOffset`, `_SlotCentersWorld`, `_SlotCount` via `MaterialPropertyBlock`. GPU instancing **disabled** (MPB). Shared noise include: `Noise/SimplexNoise2D.cginc` |

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

