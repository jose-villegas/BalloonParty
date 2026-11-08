#ifndef BALLOONPARTY_GROUND_INCLUDED
#define BALLOONPARTY_GROUND_INCLUDED

// The ground's shared globals, all pushed by GroundView. Unset (Launch, edit time) they read 0, so every
// consumer keeps its sky look.

// How much of the backdrop the ground covers at a world position: 1 = on the ground, 0 = in the sky.
// Takes the position so a sky/ground edge can vary it per pixel later without touching the consumers.
float _GroundCoverage;

// The scenario root's displacement: the ground travels with the board, so a world position shows the
// ground point (worldPos - _GroundScroll).
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

float GroundCoverageAt(float2 worldPos)
{
    return saturate(_GroundCoverage);
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
