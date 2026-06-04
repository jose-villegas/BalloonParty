// Gielis superformula SDF library for offline bush baking.
// Used by BushBake.shader and BushBakeLeaf.shader — NOT included
// in any runtime shader.
//
// The full Gielis superformula in polar coordinates:
//   r(θ) = ( |cos(m₁θ/4)/a|^n₂ + |sin(m₂θ/4)/b|^n₃ )^(-1/n₁)
// This replaces the CSG circle-cut lens in the runtime Bush.shader.

#ifndef GIELIS_SDF_INCLUDED
#define GIELIS_SDF_INCLUDED

// ── Superformula boundary radius at polar angle θ ──
// m  — lobe count (2 = ellipse-like, 3 = triangular, etc.)
// n1 — overall curvature exponent
// n2, n3 — lateral curvature exponents
float GielisRadius(float theta, float m, float n1, float n2, float n3)
{
    float t = m * theta * 0.25;
    float a = pow(abs(cos(t)), n2);
    float b = pow(abs(sin(t)), n3);
    return pow(max(a + b, 0.0001), -1.0 / n1);
}

// ── Signed distance from the Gielis superformula boundary ──
// Negative = inside, positive = outside.
float GielisSDF(float2 wp, float2 center, float radius,
                float2 leafDir, float m, float n1, float n2, float n3)
{
    float2 local = wp - center;
    float2 tang = float2(-leafDir.y, leafDir.x);
    float u = dot(local, leafDir);
    float v = dot(local, tang);

    float theta = atan2(v, u);
    float dist = length(local);
    float boundary = radius * GielisRadius(theta, m, n1, n2, n3);
    return dist - boundary;
}

// ── Per-leaf Gielis parameter jitter ──
// Varies m, n1, n2, n3 by a hash-driven offset so each leaf in the
// canopy has a subtly different shape.
void JitterGielisParams(float hash, float m, float n1, float n2, float n3,
                        out float mOut, out float n1Out, out float n2Out, out float n3Out)
{
    mOut  = m  + (hash - 0.5) * 0.6;
    n1Out = n1 + frac(hash * 7.13) * 0.3 - 0.15;
    n2Out = n2 + frac(hash * 13.37) * 0.2 - 0.1;
    n3Out = n3 + frac(hash * 23.71) * 0.2 - 0.1;
}

// ── Hue rotation (RGB) ──
// Rotates hue by `angle` radians. Used for per-leaf colour variation.
float3 HueRotate(float3 color, float angle)
{
    float cosA = cos(angle);
    float sinA = sin(angle);

    // Rodrigues' rotation around the (1,1,1)/√3 axis in RGB space
    float3 k = float3(0.57735, 0.57735, 0.57735);
    return color * cosA
         + cross(k, color) * sinA
         + k * dot(k, color) * (1.0 - cosA);
}

// ── Poisson-disk shadow jitter samples (8 taps) ──
static const float2 ShadowJitter[8] =
{
    float2( 0.2821,  0.0845),
    float2(-0.1734,  0.2635),
    float2(-0.2907, -0.1214),
    float2( 0.0693, -0.2978),
    float2( 0.1845,  0.2312),
    float2(-0.2456, -0.2156),
    float2( 0.2645, -0.1523),
    float2(-0.0812,  0.1967)
};

#endif // GIELIS_SDF_INCLUDED

