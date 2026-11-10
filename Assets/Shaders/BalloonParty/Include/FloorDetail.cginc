#ifndef BALLOONPARTY_FLOOR_DETAIL_INCLUDED
#define BALLOONPARTY_FLOOR_DETAIL_INCLUDED

// Granular microdetail for the floors (Ground.shader, and the sand under Water.shader), lit by the scene light.
// Reads DetailNoiseTileable (drawn by Tools/art/make_detail_noise.py): R = height, GB = its slope (0.5-biased),
// A = a per-texel speckle whose histogram is flattened, so a threshold of 1 - share flags exactly that share of
// the grains. Include SceneLight.cginc first.

// The look, as the shader's material sets it: x = relief (how much the bump shades toward the light), y = speckle
// (how much each grain's own value shades it), z = the share of grains that glint, w = how bright they glint.
struct FloorDetailLook
{
    float size;
    half4 amounts;
};

// At a point of the floor (floorPos, in the floor's own world units, so the grains travel with it), lit from the
// light's direction at worldPos: a pop's local light rakes the grains from the pop. x multiplies the floor's
// colour (the bump's shading and the speckle); y is the light the flagged grains glint, added on top. The tile is
// read a second time, turned and larger, so its repeat never shows; that layer's slope is turned back to match.
half2 FloorDetailAt(sampler2D detailTex, FloorDetailLook look, float2 floorPos, float2 worldPos)
{
    float2 uv = floorPos / max(look.size, 1e-3);
    half4 near = tex2D(detailTex, uv);
    half4 far = tex2D(detailTex, float2(0.8 * uv.x - 0.6 * uv.y, 0.6 * uv.x + 0.8 * uv.y) * 0.59 + float2(0.31, 0.67));

    half2 farSlope = far.gb * 2.0h - 1.0h;
    half2 slope = (near.gb * 2.0h - 1.0h) * 0.7h
                + half2(0.8h * farSlope.x + 0.6h * farSlope.y, -0.6h * farSlope.x + 0.8h * farSlope.y) * 0.3h;

    // A slope that climbs away from the light faces it.
    half facing = -dot(slope, (half2)SceneLightDirectionAt(worldPos));
    half shade = 1.0h + facing * look.amounts.x + (near.a - 0.5h) * look.amounts.y;

    // The 1-bit flag, softened over a narrow band of the flattened speckle so a grain's edge isn't a stair.
    // Under minification the mips average the speckle toward 0.5, so the flagged grains fade with distance.
    half share = look.amounts.z;
    half flagged = smoothstep(0.98h - share, 1.02h - share, near.a);
    half sparkle = flagged * saturate(facing * 2.0h) * look.amounts.w;
    return half2(shade, sparkle);
}

#endif
