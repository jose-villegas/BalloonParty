#ifndef BALLOONPARTY_CLOUDFIELD_INCLUDED
#define BALLOONPARTY_CLOUDFIELD_INCLUDED

// CONSUMER side of the shared cloud field. CloudFieldService bakes one scrolling cloud-density map into a
// global screen-space RT (_CloudDensityTex); everything that wants to live in the same clouds — the
// BackgroundCloud backdrop, sprite drop-shadows, the GI/light smear — samples it here with a single tap
// at its own world position. Consumers do NOT know the noise-roll parameters (those live on the blit
// material, see CloudFieldGen.cginc); they only need the baked map + its world bounds. Mirrors the
// project's other global fields (Disturbance, SceneLight), both RTs.

sampler2D _CloudDensityTex;
float2 _CloudFieldBoundsMin;
float2 _CloudFieldBoundsSize;
// 1 while CloudFieldService is publishing the RT, 0 otherwise (default). Lets consumers gate an effect
// by the cloud safely: with no field in the scene the gate resolves to a no-op instead of reading an
// unbound (black) RT and killing the effect everywhere.
float _CloudFieldActive;

// World XY -> RT UV, clamped so anything outside the field reads the edge texel rather than wrapping.
float2 CloudFieldUV(float2 wp)
{
    float2 size = max(_CloudFieldBoundsSize, 1e-4);
    return saturate((wp - _CloudFieldBoundsMin) / size);
}

// Cloud DENSITY in [0, 1] (R) — the thresholded cloud shape, what most consumers want (backdrop, shadows,
// GI smear). A single tap of the baked RT. Fragment stage.
float CloudFieldDensity(float2 wp)
{
    return tex2D(_CloudDensityTex, CloudFieldUV(wp)).r;
}

// Vertex-stage variant (tex2Dlod) for consumers that sample in the vertex shader.
float CloudFieldDensityLOD(float2 wp)
{
    return tex2Dlod(_CloudDensityTex, float4(CloudFieldUV(wp), 0.0, 0.0)).r;
}

// Smooth cloud INTENSITY in [0, 1] (G) — the un-thresholded field, for consumers that want to blend
// against the gradient rather than the near-binary density (thresholding it would segment the blend).
float CloudFieldNoise(float2 wp)
{
    return tex2D(_CloudDensityTex, CloudFieldUV(wp)).g;
}

float CloudFieldNoiseLOD(float2 wp)
{
    return tex2Dlod(_CloudDensityTex, float4(CloudFieldUV(wp), 0.0, 0.0)).g;
}

// A multiplier in [0, 1] for effects that should weaken where there's no cloud (e.g. the GI shadow smear
// shouldn't bleed shadow onto no-cloud ground). Resolves to 1.0 (no gating) when the field is inactive,
// so it's safe to call without CloudFieldService in the scene.
float CloudFieldGate(float2 wp)
{
    return lerp(1.0, CloudFieldDensity(wp), _CloudFieldActive);
}

#endif
