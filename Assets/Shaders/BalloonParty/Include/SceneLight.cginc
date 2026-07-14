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

// Palette index (0..15) tagged into A at a world position, or -1 if untagged. A is POINT-sampled:
// it's bilinear like R/GB, but an interpolated (index+1)/16 decodes to a wrong colour, so snap the UV
// to the texel centre first (bilinear at a centre returns that texel exactly). Encoding: (index+1)/16.
float SceneLightPaletteIndex(float a)
{
    return a > 0.001 ? min(floor(a * 16.0 + 0.5) - 1.0, 15.0) : -1.0;
}

float2 SceneLightTexelSnap(float2 fieldUV)
{
    return (floor(fieldUV / _SceneLightTexelSize.xy) + 0.5) * _SceneLightTexelSize.xy;
}

// Light colour × magnitude at a world position. A field-tagged region takes that light's palette
// colour; otherwise (or field-off) the global key light, unchanged. Falls back to white before the
// owner's first push.
float3 SceneLightTintAt(float2 worldPos)
{
    if (_SceneLightFieldOn > 0.5)
    {
        float2 uv = SceneLightTexelSnap(SceneLightFieldUV(worldPos));
        float index = SceneLightPaletteIndex(tex2D(_SceneLightTex, uv).a);
        if (index >= 0.0)
        {
            return _SceneLightPalette[(int)index].rgb * SceneLightMagnitudeAt(worldPos);
        }
    }

    return _SceneLightColor.a > 0.5
        ? _SceneLightColor.rgb * SceneLightMagnitudeAt(worldPos)
        : float3(1.0, 1.0, 1.0);
}

float3 SceneLightTintAtLOD(float2 worldPos)
{
    if (_SceneLightFieldOn > 0.5)
    {
        float2 uv = SceneLightTexelSnap(SceneLightFieldUV(worldPos));
        float index = SceneLightPaletteIndex(tex2Dlod(_SceneLightTex, float4(uv, 0.0, 0.0)).a);
        if (index >= 0.0)
        {
            return _SceneLightPalette[(int)index].rgb * SceneLightMagnitudeAtLOD(worldPos);
        }
    }

    return _SceneLightColor.a > 0.5
        ? _SceneLightColor.rgb * SceneLightMagnitudeAtLOD(worldPos)
        : float3(1.0, 1.0, 1.0);
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
