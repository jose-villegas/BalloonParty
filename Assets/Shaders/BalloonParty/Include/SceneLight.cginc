#ifndef BALLOONPARTY_SCENE_LIGHT_INCLUDED
#define BALLOONPARTY_SCENE_LIGHT_INCLUDED

// Shared scene-light access (see @ref plan_lighting). Two layers behind one API:
//
//   * the FLAT globals SceneLightService pushes — SceneLightDirection() / SceneLightTint() /
//     ShadowLightFade(), copied here verbatim so a per-shader helper block becomes a mechanical
//     "delete the local copy, #include this" at migration (Phase B);
//   * the field-aware *At(worldPos) helpers that sample the SceneLightFieldService RT, each falling
//     back to the flat path when the field is off (_SceneLightFieldOn < 0.5) so a shader that
//     includes this before the field ships is bit-identical to today.
//
// Uniforms are declared here, NOT in any Properties block — a per-material value would mask the
// global. A shader migrating onto this include deletes its own local declarations of these.

// Global shader property — set by SceneLightService, not in Properties so
// material values can't mask it. Points TOWARD the light, normalized;
// canonical (-0.707, 0.707) = upper-left.
float4 _SceneLightDir;

// Set globally by SceneLightService; kept out of Properties so no
// material value can shadow the scene-wide light. Colour's alpha is the
// "owner has pushed" validity flag (see SceneLightTint).
float4 _SceneLightColor;
float  _SceneLightIntensity;

// The light FIELD (SceneLightFieldService): R = magnitude, GB = 0.5-biased toward-light direction,
// A = palette-colour index, encoded (index+1)/16 (0 = untagged → use _SceneLightColor). Bounds map
// world XY → field UV; the on-flag is the fallback switch (0 = field absent, use the flat globals).
sampler2D _SceneLightTex;
float4    _SceneLightFieldBoundsMin;  // xy = world-space min corner of the field
float4    _SceneLightFieldBoundsSize; // xy = world-space size of the field
float4    _SceneLightTexelSize;       // xy = 1/width, 1/height — for point-sampling the A index
float     _SceneLightFieldOn;

// The game palette, pushed once by SceneLightFieldService in the same slot order the lights encode
// into A. A tagged light tints its region this colour instead of the global key light.
float4 _SceneLightPalette[16];

// Guarded read of the scene light (see SceneLightService): normalized, toward
// the light; falls back to the canonical direction if the global hasn't been
// pushed yet (protects edit-time before its first OnEnable/LateUpdate/OnValidate).
float2 SceneLightDirection()
{
    float2 raw = dot(_SceneLightDir.xy, _SceneLightDir.xy) < 1e-4
        ? float2(-0.707, 0.707)
        : _SceneLightDir.xy;
    return normalize(raw);
}

// The light's colour × intensity — multiplies into the authored specular response.
// Neutral (white) when the owner hasn't pushed yet, so nothing dims at edit time.
float3 SceneLightTint()
{
    return _SceneLightColor.a > 0.5
        ? _SceneLightColor.rgb * _SceneLightIntensity
        : float3(1.0, 1.0, 1.0);
}

// No light, no shadow: the shadow's opacity follows the light's intensity (clamped at the
// authored alpha). Neutral when the owner hasn't pushed yet (edit time).
float ShadowLightFade()
{
    return _SceneLightColor.a > 0.5 ? saturate(_SceneLightIntensity) : 1.0;
}

// World XY → field UV, clamped so anything outside the play-area rectangle reads the edge texel
// rather than wrapping. Guards a zero-size field (before bounds are pushed) to a safe centre.
float2 SceneLightFieldUV(float2 worldPos)
{
    float2 size = max(_SceneLightFieldBoundsSize.xy, 1e-4);
    return saturate((worldPos - _SceneLightFieldBoundsMin.xy) / size);
}

// Raw field taps. The LOD variant is for vertex-stage consumers (orbit accessories, per-object
// spec anchors) that can't use gradient-based tex2D — target 3.5, the SpeckField VTF precedent.
float4 SceneLightFieldSample(float2 worldPos)
{
    return tex2D(_SceneLightTex, SceneLightFieldUV(worldPos));
}

float4 SceneLightFieldSampleLOD(float2 worldPos)
{
    return tex2Dlod(_SceneLightTex, float4(SceneLightFieldUV(worldPos), 0.0, 0.0));
}

// Decode a raw field tap. The direction un-biases GB (*2 - 1) and normalizes with the same 1e-4
// guard as the flat path; a degenerate sample falls back to the flat direction.
float2 SceneLightFieldDecodeDir(float4 s)
{
    float2 raw = s.gb * 2.0 - 1.0;
    return dot(raw, raw) < 1e-4 ? SceneLightDirection() : normalize(raw);
}

// Toward-light direction at a world position. Field-aware, with a flat-global fallback when the
// field is off. The LOD variant samples in a vertex stage.
float2 SceneLightDirectionAt(float2 worldPos)
{
    return _SceneLightFieldOn < 0.5
        ? SceneLightDirection()
        : SceneLightFieldDecodeDir(SceneLightFieldSample(worldPos));
}

float2 SceneLightDirectionAtLOD(float2 worldPos)
{
    return _SceneLightFieldOn < 0.5
        ? SceneLightDirection()
        : SceneLightFieldDecodeDir(SceneLightFieldSampleLOD(worldPos));
}

// Light magnitude ("how much light here") at a world position. Off-field fallback is the flat
// intensity, guarded by the owner-pushed validity flag (1.0 when the owner hasn't pushed yet).
float SceneLightMagnitudeAt(float2 worldPos)
{
    return _SceneLightFieldOn < 0.5
        ? (_SceneLightColor.a > 0.5 ? _SceneLightIntensity : 1.0)
        : SceneLightFieldSample(worldPos).r;
}

float SceneLightMagnitudeAtLOD(float2 worldPos)
{
    return _SceneLightFieldOn < 0.5
        ? (_SceneLightColor.a > 0.5 ? _SceneLightIntensity : 1.0)
        : SceneLightFieldSampleLOD(worldPos).r;
}

// Palette index (0..15) packed into A as (index+1)/16, or -1 if untagged.
float SceneLightPaletteIndex(float a)
{
    return a > 0.001 ? min(floor(a * 16.0 + 0.5) - 1.0, 15.0) : -1.0;
}

// One texel's A → its palette colour, or the key light where untagged. The colour reconstruction
// blends these DECODED colours (never the raw indices — an interpolated (index+1)/16 would land in a
// foreign palette slot).
float3 SceneLightDecodeColor(float a, float3 keyColor)
{
    float index = SceneLightPaletteIndex(a);
    return index >= 0.0 ? _SceneLightPalette[(int)index].rgb : keyColor;
}

// Joint-trilateral guide constants (see SceneLightPaletteColorAt). The palette index (A) is a coarse,
// quantised signal; R (magnitude) and GB (direction) are smooth, co-located channels, so we upsample
// A using them as the guide — joint bilateral upsampling (Kopf 2007) + edge-directed weighting
// (Li & Orchard 2001). Larger _RANGE = sharper boundary; _DIRFLOOR is the floor weight a
// differently-oriented (different-source) tap keeps.
#define SCENE_LIGHT_RANGE_R  4.0
#define SCENE_LIGHT_DIR_FLOOR 0.2

// Accumulate one field tap into the trilateral colour blend, weighted by spatial falloff × how close
// the tap's magnitude and direction are to the fragment's smooth (bilinear) reference — so colour
// stays coherent within a light but doesn't bleed across the R/direction discontinuity at its edge.
void SceneLightColorTap(
    float4 s, float rRef, float2 dirRef, float3 keyColor, float spatial, inout float3 accum, inout float wsum)
{
    float wr = exp(-abs(s.r - rRef) * SCENE_LIGHT_RANGE_R);
    float2 dir = normalize(s.gb * 2.0 - 1.0 + 1e-4);
    float align = lerp(SCENE_LIGHT_DIR_FLOOR, 1.0, saturate(dot(dir, dirRef)));
    float w = spatial * wr * align;
    accum += SceneLightDecodeColor(s.a, keyColor) * w;
    wsum += w;
}

// Colour at a world position, reconstructed by a 3×3 joint-trilateral upsample of the palette index:
// R and the direction guide the blend so the colour boundary follows the smooth field structure
// instead of the coarse texel grid. Rest / untagged → the key colour (every tap decodes to it).
float3 SceneLightPaletteColorAt(float2 fieldUV, float3 keyColor)
{
    float2 texel = _SceneLightTexelSize.xy;
    float4 ref = tex2D(_SceneLightTex, fieldUV);
    float rRef = ref.r;
    float2 dirRef = normalize(ref.gb * 2.0 - 1.0 + 1e-4);
    float2 center = (floor(fieldUV / texel) + 0.5) * texel;

    float k[3] = { 0.25, 0.5, 0.25 };
    float3 accum = float3(0.0, 0.0, 0.0);
    float wsum = 0.0;
    [unroll] for (int dy = -1; dy <= 1; dy++)
    {
        [unroll] for (int dx = -1; dx <= 1; dx++)
        {
            float2 uv = center + float2(dx, dy) * texel;
            SceneLightColorTap(tex2D(_SceneLightTex, uv), rRef, dirRef, keyColor, k[dx + 1] * k[dy + 1], accum, wsum);
        }
    }

    return accum / max(wsum, 1e-4);
}

float3 SceneLightPaletteColorAtLOD(float2 fieldUV, float3 keyColor)
{
    float2 texel = _SceneLightTexelSize.xy;
    float4 ref = tex2Dlod(_SceneLightTex, float4(fieldUV, 0.0, 0.0));
    float rRef = ref.r;
    float2 dirRef = normalize(ref.gb * 2.0 - 1.0 + 1e-4);
    float2 center = (floor(fieldUV / texel) + 0.5) * texel;

    float k[3] = { 0.25, 0.5, 0.25 };
    float3 accum = float3(0.0, 0.0, 0.0);
    float wsum = 0.0;
    [unroll] for (int dy = -1; dy <= 1; dy++)
    {
        [unroll] for (int dx = -1; dx <= 1; dx++)
        {
            float2 uv = center + float2(dx, dy) * texel;
            SceneLightColorTap(
                tex2Dlod(_SceneLightTex, float4(uv, 0.0, 0.0)), rRef, dirRef, keyColor, k[dx + 1] * k[dy + 1], accum, wsum);
        }
    }

    return accum / max(wsum, 1e-4);
}

// Light colour × magnitude at a world position. A field-tagged region takes that light's palette
// colour (3×3 joint-trilateral upsample, guided by the smooth R/direction channels); untagged /
// field-off is the global key light. Falls back to white before the owner's first push.
float3 SceneLightTintAt(float2 worldPos)
{
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    float3 color = _SceneLightFieldOn > 0.5
        ? SceneLightPaletteColorAt(SceneLightFieldUV(worldPos), keyColor)
        : keyColor;
    return color * SceneLightMagnitudeAt(worldPos);
}

float3 SceneLightTintAtLOD(float2 worldPos)
{
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    float3 color = _SceneLightFieldOn > 0.5
        ? SceneLightPaletteColorAtLOD(SceneLightFieldUV(worldPos), keyColor)
        : keyColor;
    return color * SceneLightMagnitudeAtLOD(worldPos);
}

// No light, no shadow, at a world position — same clamp as ShadowLightFade(), scaled by the
// local magnitude once the field is live (the fallback comes free from SceneLightMagnitudeAt's
// own field-off branch, so this needs no separate _SceneLightFieldOn check).
float ShadowLightFadeAt(float2 worldPos)
{
    return _SceneLightColor.a > 0.5 ? saturate(SceneLightMagnitudeAt(worldPos)) : 1.0;
}

float ShadowLightFadeAtLOD(float2 worldPos)
{
    return _SceneLightColor.a > 0.5 ? saturate(SceneLightMagnitudeAtLOD(worldPos)) : 1.0;
}

#endif // BALLOONPARTY_SCENE_LIGHT_INCLUDED
