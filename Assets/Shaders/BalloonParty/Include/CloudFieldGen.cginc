#ifndef BALLOONPARTY_CLOUDFIELDGEN_INCLUDED
#define BALLOONPARTY_CLOUDFIELDGEN_INCLUDED

// GENERATION side of the shared cloud field — used ONLY by the CloudFieldDensity blit that fills the RT.
// The parameters below are the blit MATERIAL's properties (the one tuning surface for the cloud roll);
// consumers never see them — they tap the baked RT via CloudField.cginc. Kept separate from the consumer
// include so a plain consumer doesn't drag in the noise params it has no business knowing.

sampler2D _NoiseTex;
float  _NoisePeriod;
float  _NoiseScale;
float  _BaseScale;
float  _DetailScale;
float  _FineScale;
float4 _ScrollSpeedBase;
float4 _ScrollSpeedDetail;
float4 _ScrollSpeedFine;
float  _EdgeLow;
float  _EdgeHigh;
float  _AnimationSpeed;
float  _TimeOffset;
float  _DisplaceWorldScale;

// The disturbance field (globals set by DisturbanceFieldService) — baked INTO the density here so the
// RT is "already disturbed" and every consumer reacts to bounces/pops for free. Equilibrium is
// (R 0.5, GB 0.5), which resolves to no effect, so this is a no-op at rest.
sampler2D _DisturbanceTex;
float2 _FieldBoundsMin;
float2 _FieldBoundsSize;

// Built-in _Time at runtime; _TimeOffset is only fed in edit mode, where _Time is frozen.
float CloudGenTime()
{
    return _Time.y * _AnimationSpeed + _TimeOffset;
}

// One octave in [-1, 1] from the tileable baked noise (value in R), repeat-wrapped over _NoisePeriod.
float CloudGenOctave(float2 p)
{
    return tex2Dlod(_NoiseTex, float4(p / max(_NoisePeriod, 0.0001), 0.0, 0.0)).r * 2.0 - 1.0;
}

// The undisturbed thresholded cloud intensity in [0, 1] at a world position, from three scrolling octaves.
float CloudGenBaseDensity(float2 wp)
{
    float t = CloudGenTime();
    float2 pBase   = wp * _BaseScale   * _NoiseScale + _ScrollSpeedBase.xy   * t;
    float2 pDetail = wp * _DetailScale * _NoiseScale + _ScrollSpeedDetail.xy * t;
    float2 pFine   = wp * _FineScale   * _NoiseScale + _ScrollSpeedFine.xy   * t;

    float n = CloudGenOctave(pBase) * 0.50 + CloudGenOctave(pDetail) * 0.30
            + CloudGenOctave(pFine) * 0.20;
    return smoothstep(_EdgeLow, _EdgeHigh, n * 0.5 + 0.5);
}

// What the blit writes to each RT texel: the cloud density with the disturbance baked in — a repulsion
// bump (R > 0.5) thins the cloud, and the displacement (GB) crossfades toward fresh noise at the shoved
// position (so reformation reveals new cloud rather than rubber-banding stretched noise). At equilibrium
// (R 0.5, GB 0.5) both terms vanish, so it's exactly CloudGenBaseDensity at rest.
float CloudFieldGenerateDensity(float2 wp)
{
    float base = CloudGenBaseDensity(wp);

    float3 field = tex2Dlod(_DisturbanceTex, float4((wp - _FieldBoundsMin) / _FieldBoundsSize, 0, 0)).rgb;
    float thin = saturate((1.0 - field.r) * 2.0);
    float2 displace = (field.gb - 0.5) * 2.0 * _DisplaceWorldScale;
    float disturbance = saturate(length(displace) / (_DisplaceWorldScale * 0.5 + 0.001));

    float density = disturbance > 0.001 ? lerp(base, CloudGenBaseDensity(wp + displace), disturbance) : base;
    return density * thin;
}

#endif
