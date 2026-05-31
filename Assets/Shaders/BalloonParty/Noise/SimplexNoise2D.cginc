// SimplexNoise2D.cginc — 2D Simplex noise for procedural effects.
// Adapted from public-domain references (Ashima Arts / Stefan Gustavson).
// Usage: #include "Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc"
//        float n = SimplexNoise2D(float2 p);   // returns [-1, 1]

#ifndef BALLOON_PARTY_SIMPLEX_NOISE_2D
#define BALLOON_PARTY_SIMPLEX_NOISE_2D

float3 _snoise_mod289(float3 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float2 _snoise_mod289_2(float2 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float3 _snoise_permute(float3 x)
{
    return _snoise_mod289(((x * 34.0) + 1.0) * x);
}

// Returns a value in [-1, 1].
float SimplexNoise2D(float2 v)
{
    const float4 C = float4(
         0.211324865405187,   // (3 - sqrt(3)) / 6
         0.366025403784439,   // (sqrt(3) - 1) / 2
        -0.577350269189626,   // -1 + 2 * C.x
         0.024390243902439);  // 1 / 41

    // First corner
    float2 i = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    // Other corners
    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    // Permutations
    i = _snoise_mod289_2(i);
    float3 p = _snoise_permute(
        _snoise_permute(i.y + float3(0.0, i1.y, 1.0))
                             + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    // Gradients: 41 points on a line mapped to a diamond
    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    // Normalise gradients implicitly by scaling m
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    // Compute contributions from three corners
    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.y = a0.y * x12.x + h.y * x12.y;
    g.z = a0.z * x12.z + h.z * x12.w;

    return 130.0 * dot(m, g);
}

#endif // BALLOON_PARTY_SIMPLEX_NOISE_2D

