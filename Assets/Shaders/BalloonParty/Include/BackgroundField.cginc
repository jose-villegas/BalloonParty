#ifndef BALLOONPARTY_BACKGROUNDFIELD_INCLUDED
#define BALLOONPARTY_BACKGROUNDFIELD_INCLUDED

// CONSUMER side of the shared cloud field. BackgroundFieldService bakes one scrolling cloud-density map into a
// global screen-space RT (_BackgroundDensityTex); everything that wants to live in the same clouds — the
// BackgroundCloud backdrop, sprite drop-shadows, the GI/light smear — samples it here with a single tap
// at its own world position. Consumers do NOT know the noise-roll parameters (those live on the blit
// material, see BackgroundFieldGen.cginc); they only need the baked map + its world bounds. Mirrors the
// project's other global fields (Disturbance, SceneLight), both RTs.

sampler2D _BackgroundDensityTex;
float2 _BackgroundFieldBoundsMin;
float2 _BackgroundFieldBoundsSize;
// 1 while BackgroundFieldService is publishing the RT, 0 otherwise (default). Lets consumers gate an effect
// by the cloud safely: with no field in the scene the gate resolves to a no-op instead of reading an
// unbound (black) RT and killing the effect everywhere.
float _BackgroundFieldActive;

// World XY -> RT UV, clamped so anything outside the field reads the edge texel rather than wrapping.
float2 BackgroundFieldUV(float2 wp)
{
    float2 size = max(_BackgroundFieldBoundsSize, 1e-4);
    return saturate((wp - _BackgroundFieldBoundsMin) / size);
}

// Cloud DENSITY in [0, 1] (R) — the thresholded cloud shape, what most consumers want (backdrop, shadows,
// GI smear). A single tap of the baked RT. Fragment stage.
float BackgroundFieldDensity(float2 wp)
{
    return tex2D(_BackgroundDensityTex, BackgroundFieldUV(wp)).r;
}

// Vertex-stage variant (tex2Dlod) for consumers that sample in the vertex shader.
float BackgroundFieldDensityLOD(float2 wp)
{
    return tex2Dlod(_BackgroundDensityTex, float4(BackgroundFieldUV(wp), 0.0, 0.0)).r;
}

// Smooth cloud INTENSITY in [0, 1] (G) — the un-thresholded field, for consumers that want to blend
// against the gradient rather than the near-binary density (thresholding it would segment the blend).
float BackgroundFieldNoise(float2 wp)
{
    return tex2D(_BackgroundDensityTex, BackgroundFieldUV(wp)).g;
}

float BackgroundFieldNoiseLOD(float2 wp)
{
    return tex2Dlod(_BackgroundDensityTex, float4(BackgroundFieldUV(wp), 0.0, 0.0)).g;
}

// A multiplier in [0, 1] for effects that should weaken where there's no cloud (e.g. the GI shadow smear
// shouldn't bleed shadow onto no-cloud ground). Resolves to 1.0 (no gating) when the field is inactive,
// so it's safe to call without BackgroundFieldService in the scene.
float BackgroundFieldGate(float2 wp)
{
    return lerp(1.0, BackgroundFieldDensity(wp), _BackgroundFieldActive);
}

#endif
