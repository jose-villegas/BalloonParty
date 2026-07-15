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

// The light FIELD (SceneLightFieldService): R = LOCAL boost above the ambient (0 at rest — the ambient
// magnitude is the global _SceneLightIntensity, added by the helpers below), GB = 0.5-biased toward-light
// direction, A = palette-colour index, encoded (index+1)/16 (0 = untagged → use _SceneLightColor). Bounds
// map world XY → field UV; the on-flag is the fallback switch (0 = field absent, use the flat globals).
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

// Decode the toward-light direction from a field tap. The field GB stores (localWeight * localDir), so
// un-biasing gives that vector: its length is how much the LOCAL light captures the direction here
// (0 = none), its direction is the local light's. We blend the global (ambient) direction toward the
// local by that weight — the field carries no ambient, so a rest tap (weight 0) resolves to the global.
float2 SceneLightFieldDecodeDir(float4 s)
{
    float2 raw = s.gb * 2.0 - 1.0;
    float weight = length(raw);
    if (weight < 1e-4)
    {
        return SceneLightDirection();
    }

    return normalize(lerp(SceneLightDirection(), raw / weight, saturate(weight)));
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

// The LOCAL light's toward-direction ONLY — no ambient/global blend. `weightOut` (0..1) is how strongly
// a local light defines the direction here (0 = none), so a caller can fade an effect in from rest by
// it. Returns a neutral up-vector when there's no local light or the field is off. Vertex-stage (LOD).
float2 SceneLightLocalDirectionAtLOD(float2 worldPos, out float weightOut)
{
    weightOut = 0.0;
    if (_SceneLightFieldOn < 0.5)
    {
        return float2(0.0, 1.0);
    }

    float2 raw = SceneLightFieldSampleLOD(worldPos).gb * 2.0 - 1.0;
    float len = length(raw);
    weightOut = saturate(len);
    return len > 1e-4 ? raw / len : float2(0.0, 1.0);
}

// The ambient (global) light magnitude — the key light's intensity, guarded to 1.0 before the owner
// has pushed. This is the baseline the field's local boost adds ON TOP of; the field itself no longer
// stores it (its R channel is the local boost only, 0 at rest).
float SceneLightAmbientMagnitude()
{
    return _SceneLightColor.a > 0.5 ? _SceneLightIntensity : 1.0;
}

// Light magnitude ("how much light here") at a world position: the ambient baseline + the field's
// local boost (R). Field-off there is no local boost, so it's just the ambient.
float SceneLightMagnitudeAt(float2 worldPos)
{
    float ambient = SceneLightAmbientMagnitude();
    return _SceneLightFieldOn < 0.5 ? ambient : ambient + SceneLightFieldSample(worldPos).r;
}

float SceneLightMagnitudeAtLOD(float2 worldPos)
{
    float ambient = SceneLightAmbientMagnitude();
    return _SceneLightFieldOn < 0.5 ? ambient : ambient + SceneLightFieldSampleLOD(worldPos).r;
}

// Palette index (0..15) packed into A as (index+1)/16, or -1 if untagged.
float SceneLightPaletteIndex(float a)
{
    return a > 0.001 ? min(floor(a * 16.0 + 0.5) - 1.0, 15.0) : -1.0;
}

// The palette index of the LOCAL light tagging this position (0..15), or -1 if untagged / field off.
// Lets a consumer opt a specific stamp colour out of its lighting (e.g. a cloud that ignores a beam).
// A is POINT-sampled (snapped to the nearest texel centre) — bilinear A blends toward neighbours and
// decodes to a foreign slot, which on a thin stamp (a laser beam) is every fragment, so a bilinear read
// would never match the tag. Mirrors the texel snap SceneLightPaletteColorAt uses.
float SceneLightPaletteIndexAt(float2 worldPos)
{
    if (_SceneLightFieldOn < 0.5)
    {
        return -1.0;
    }

    float2 texel = _SceneLightTexelSize.xy;
    float2 snapped = (floor(SceneLightFieldUV(worldPos) / texel) + 0.5) * texel;
    return SceneLightPaletteIndex(tex2D(_SceneLightTex, snapped).a);
}

// One texel's A → its palette colour, or the key light where untagged. The colour reconstruction
// blends these DECODED colours (never the raw indices — an interpolated (index+1)/16 would land in a
// foreign palette slot).
float3 SceneLightDecodeColor(float a, float3 keyColor)
{
    float index = SceneLightPaletteIndex(a);
    return index >= 0.0 ? _SceneLightPalette[(int)index].rgb : keyColor;
}

// Palette-colour IDENTITY at a world position — a 2×2 decode-then-blend: decode each texel's index to
// a colour first, then bilinear-blend by the sub-texel fraction. Blending decoded colours (never the
// raw indices, which would band into a foreign slot) keeps the hue smooth. The soft light EDGE is not
// done here — it comes from the magnitude-driven fade in SceneLightTintAt — so this stays a plain
// smooth blend rather than an edge-preserving filter, which would harden the glow.
float3 SceneLightPaletteColorAt(float2 fieldUV, float3 keyColor)
{
    float2 texel = _SceneLightTexelSize.xy;
    float2 p = fieldUV / texel - 0.5;
    float2 f = frac(p);
    float2 uv00 = (floor(p) + 0.5) * texel;
    float3 c00 = SceneLightDecodeColor(tex2D(_SceneLightTex, uv00).a, keyColor);
    float3 c10 = SceneLightDecodeColor(tex2D(_SceneLightTex, uv00 + float2(texel.x, 0.0)).a, keyColor);
    float3 c01 = SceneLightDecodeColor(tex2D(_SceneLightTex, uv00 + float2(0.0, texel.y)).a, keyColor);
    float3 c11 = SceneLightDecodeColor(tex2D(_SceneLightTex, uv00 + texel).a, keyColor);
    return lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
}

float3 SceneLightPaletteColorAtLOD(float2 fieldUV, float3 keyColor)
{
    float2 texel = _SceneLightTexelSize.xy;
    float2 p = fieldUV / texel - 0.5;
    float2 f = frac(p);
    float2 uv00 = (floor(p) + 0.5) * texel;
    float3 c00 = SceneLightDecodeColor(tex2Dlod(_SceneLightTex, float4(uv00, 0.0, 0.0)).a, keyColor);
    float3 c10 = SceneLightDecodeColor(tex2Dlod(_SceneLightTex, float4(uv00 + float2(texel.x, 0.0), 0.0, 0.0)).a, keyColor);
    float3 c01 = SceneLightDecodeColor(tex2Dlod(_SceneLightTex, float4(uv00 + float2(0.0, texel.y), 0.0, 0.0)).a, keyColor);
    float3 c11 = SceneLightDecodeColor(tex2Dlod(_SceneLightTex, float4(uv00 + texel, 0.0, 0.0)).a, keyColor);
    return lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
}

// Boost above the ambient rest at which a stamp's palette colour reaches full strength. The colour
// fades in with the SMOOTH local magnitude, so a light's colour edge is as soft as its brightness
// falloff (R is bilinear) — no hard hue boundary at the quantised palette-index texels.
#define SCENE_LIGHT_COLOR_RAMP 1.0

// Light colour × magnitude at a world position. A field-tagged region blends from the global key light
// toward that light's palette colour by how far the local magnitude sits above the ambient rest — the
// intensity channel drives a soft colour edge. Untagged / field-off is the global key light unchanged.
float3 SceneLightTintAt(float2 worldPos)
{
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    float ambient = SceneLightAmbientMagnitude();
    if (_SceneLightFieldOn < 0.5)
    {
        return keyColor * ambient;
    }

    float local = SceneLightFieldSample(worldPos).r;
    float colorAmount = saturate(local / SCENE_LIGHT_COLOR_RAMP);
    float3 palette = SceneLightPaletteColorAt(SceneLightFieldUV(worldPos), keyColor);
    return lerp(keyColor, palette, colorAmount) * (ambient + local);
}

float3 SceneLightTintAtLOD(float2 worldPos)
{
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    float ambient = SceneLightAmbientMagnitude();
    if (_SceneLightFieldOn < 0.5)
    {
        return keyColor * ambient;
    }

    float local = SceneLightFieldSampleLOD(worldPos).r;
    float colorAmount = saturate(local / SCENE_LIGHT_COLOR_RAMP);
    float3 palette = SceneLightPaletteColorAtLOD(SceneLightFieldUV(worldPos), keyColor);
    return lerp(keyColor, palette, colorAmount) * (ambient + local);
}

// The LOCAL field-light contribution at a world position — nearby point/area lights only, with NO
// ambient baseline: the light colour × how far the magnitude sits ABOVE the ambient rest. Zero at rest
// and when the field is off, so a consumer using this reacts only to local lights, never the global one.
float3 SceneLightLocalAt(float2 worldPos)
{
    if (_SceneLightFieldOn < 0.5)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float local = SceneLightFieldSample(worldPos).r;
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    return SceneLightPaletteColorAt(SceneLightFieldUV(worldPos), keyColor) * local;
}

float3 SceneLightLocalAtLOD(float2 worldPos)
{
    if (_SceneLightFieldOn < 0.5)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float local = SceneLightFieldSampleLOD(worldPos).r;
    float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
    return SceneLightPaletteColorAtLOD(SceneLightFieldUV(worldPos), keyColor) * local;
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
