#ifndef BALLOONPARTY_GROUND_INCLUDED
#define BALLOONPARTY_GROUND_INCLUDED

// The ground's shared globals, all pushed by GroundView. Unset (Launch, edit time) they read 0, so every
// consumer keeps its sky look and no reveal runs.

// How much of the backdrop the ground covers at rest: 1 = on the ground, 0 = in the sky.
float _GroundCoverage;

// The mist reveal between sky and ground: the ground shows inside a hole that opens from the middle (sky to
// ground) or closes back into it (ground to sky), behind a mist that hides the sky around it.
// x = 1 while a reveal runs (0 = use _GroundCoverage), y = hole radius in world units, z = the edge's soft width,
// w = how thick the mist is outside the hole (0..1).
float4 _GroundReveal;

// x = how far the hole's edge wanders from a circle, y = the edge's drift phase (unscaled seconds),
// z = the meadow's scale, w = the cloud backdrop's scale (the drop between the layers; read through
// GroundRevealGroundScale and GroundRevealSkyScale, which give 1 at rest).
// The hole is centred on the world origin, where the camera sits (the disturbance field and the blade tile
// assume the same), and so is the scale.
float4 _GroundRevealShape;

// Where the ground's origin shows, in world units: the ground travels with the board, so a world position
// shows the ground point (worldPos - _GroundScroll) / GroundRevealGroundScale().
float2 _GroundScroll;

// World size of the floor's lighter and darker patches, shared so the blades' drier tips sit over the
// floor's lighter patches.
float _GroundPatchSize;

// Bare-dirt patches: x = patch size (world units), y = noise threshold (higher = less dirt), z = edge
// softness. The floor shows dirt there and the blades thin out and shorten toward it.
float4 _GroundDirt;

// The meadow's average unlit colour: the GI overlay measures bounce against it (times the scene light) on
// ground levels, the way it measures against the sky's clear colour on sky levels.
float4 _GroundBounceReference;

// Signed world distance from the hole's edge: negative inside, where the ground shows. Two crossing sine waves
// bend the circle so the mist opens like parting cloud rather than an iris; no texture, so every consumer can
// afford it.
float GroundRevealDistance(float2 worldPos)
{
    float phase = _GroundRevealShape.y;
    float wander = sin(worldPos.x * 1.7 + worldPos.y * 0.6 + phase) * 0.55
                 + sin(worldPos.y * 2.3 - worldPos.x * 0.9 - phase * 1.3) * 0.45;
    return length(worldPos) + wander * _GroundRevealShape.x - _GroundReveal.y;
}

// How much of the backdrop the ground covers at a world position: 1 = on the ground, 0 = in the sky.
float GroundCoverageAt(float2 worldPos)
{
    [branch] if (_GroundReveal.x > 0.5)
    {
        return 1.0 - smoothstep(-max(_GroundReveal.z, 1e-3), 0.0, GroundRevealDistance(worldPos));
    }

    return saturate(_GroundCoverage);
}

// How large the meadow looks: below 1 while a reveal shows it deeper than the sky, 1 at rest.
float GroundRevealGroundScale()
{
    return _GroundReveal.x > 0.5 ? max(_GroundRevealShape.z, 1e-3) : 1.0;
}

// How large the cloud backdrop looks: above 1 while a reveal passes the camera through it, 1 at rest.
float GroundRevealSkyScale()
{
    return _GroundReveal.x > 0.5 ? max(_GroundRevealShape.w, 1e-3) : 1.0;
}

// The ground point a world position shows.
float2 GroundPointAt(float2 worldPos)
{
    return (worldPos - _GroundScroll) / GroundRevealGroundScale();
}

float2 GroundPatchUV(float2 groundPos)
{
    return groundPos / max(_GroundPatchSize, 1e-3);
}

// Offset so the dirt doesn't line up with the colour patches read from the same noise.
float2 GroundDirtUV(float2 groundPos)
{
    return groundPos / max(_GroundDirt.x, 1e-3) + float2(0.37, 0.71);
}

// 0 = grass, 1 = bare dirt, from the patch noise sampled at GroundDirtUV.
float GroundDirtMask(float noise)
{
    return _GroundDirt.x > 0.0 ? smoothstep(_GroundDirt.y, _GroundDirt.y + max(_GroundDirt.z, 1e-3), noise) : 0.0;
}

#endif
