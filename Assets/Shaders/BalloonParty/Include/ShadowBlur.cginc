#ifndef BALLOONPARTY_SHADOW_BLUR_INCLUDED
#define BALLOONPARTY_SHADOW_BLUR_INCLUDED

// 9-tap box-blur drop shadow from a sprite's alpha mask.
// Requires the consumer to declare: sampler2D _MainTex;

// Out-of-bounds taps read as transparent to prevent edge-pixel smearing from clamp wrap.
inline fixed SampleAlphaGuarded(sampler2D tex, float2 uv)
{
    float2 inBounds = step(0.0, uv) * step(uv, 1.0);
    return tex2D(tex, uv).a * inBounds.x * inBounds.y;
}

// 9-tap box blur centred on shadowUV.
// When softness == 0 all taps collapse to the same point (hard edge).
inline fixed SoftShadowAlpha9Tap(sampler2D tex, float2 shadowUV, float s)
{
    fixed a =
        SampleAlphaGuarded(tex, shadowUV + float2(-s, -s)) +
        SampleAlphaGuarded(tex, shadowUV + float2( 0, -s)) +
        SampleAlphaGuarded(tex, shadowUV + float2( s, -s)) +
        SampleAlphaGuarded(tex, shadowUV + float2(-s,  0)) +
        SampleAlphaGuarded(tex, shadowUV                 ) +
        SampleAlphaGuarded(tex, shadowUV + float2( s,  0)) +
        SampleAlphaGuarded(tex, shadowUV + float2(-s,  s)) +
        SampleAlphaGuarded(tex, shadowUV + float2( 0,  s)) +
        SampleAlphaGuarded(tex, shadowUV + float2( s,  s));
    return a / 9.0;
}

#endif // BALLOONPARTY_SHADOW_BLUR_INCLUDED
