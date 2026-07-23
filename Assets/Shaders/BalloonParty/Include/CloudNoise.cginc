#ifndef BALLOONPARTY_CLOUDNOISE_INCLUDED
#define BALLOONPARTY_CLOUDNOISE_INCLUDED

// Shared cloud-noise octave sampler — used by both the density BAKE (BackgroundFieldGen.cginc,
// the RT-filling blit) and the PUFFS (PuffCloud.shader, one quad per cluster). Both sample the
// same baked tileable noise texture and used to carry their own copy of this helper; the copies
// drifted apart (PuffCloud gained a _LOW_QUALITY_CLOUD two-octave path the bake never needed).
// Only the single-octave sampler is unified here — the two three-octave blends stay apart at
// their call sites: PuffCloud's CloudNoise/CloudNoiseSoft carry a lowFrequency out-param, a
// low-quality branch, and take t as a parameter; BackgroundGenRawNoise instead shifts wp by
// _BackgroundWorldOffset and pulls t from BackgroundGenTime() internally. Same weights
// (0.50 / 0.30 / 0.20), different structure, so each stays at its call site.

// Uniforms shared verbatim by both call sites (identical name + type in both originals).
sampler2D _NoiseTex;
float     _NoisePeriod;

// One octave in [-1, 1] from the tileable baked noise (value in R), repeat-wrapped over
// _NoisePeriod. The tile is histogram-matched to the simplex the cloud thresholds were tuned
// against. tex2D variant — PuffCloud's per-fragment cloud shader, where normal screen-space
// derivatives are available for mip selection.
float CloudNoiseOctaveTex2D(float2 p)
{
    return tex2D(_NoiseTex, p / max(_NoisePeriod, 0.0001)).r * 2.0 - 1.0;
}

// Same octave, tex2Dlod variant — BackgroundFieldGen's blit runs outside normal fragment
// derivatives (a full-screen RT fill), so it must pin LOD 0 explicitly.
float CloudNoiseOctaveTex2Dlod(float2 p)
{
    return tex2Dlod(_NoiseTex, float4(p / max(_NoisePeriod, 0.0001), 0.0, 0.0)).r * 2.0 - 1.0;
}

#endif
