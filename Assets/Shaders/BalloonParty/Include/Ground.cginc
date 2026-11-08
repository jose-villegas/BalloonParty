#ifndef BALLOONPARTY_GROUND_INCLUDED
#define BALLOONPARTY_GROUND_INCLUDED

// How much of the backdrop the ground covers at a world position: 1 = on the ground, 0 = in the sky.
// GroundView pushes the global as the scenario changes; unset (Launch, edit time) reads 0, so every
// consumer keeps its sky look. Takes the position so a sky/ground edge can vary it per pixel later
// without touching the consumers.
float _GroundCoverage;

float GroundCoverageAt(float2 worldPos)
{
    return saturate(_GroundCoverage);
}

#endif
