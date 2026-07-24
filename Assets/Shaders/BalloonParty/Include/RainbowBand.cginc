#ifndef BP_RAINBOW_BAND_INCLUDED
#define BP_RAINBOW_BAND_INCLUDED

// Shared rainbow-band palette lookup. The band COLOURS + count are the level's allowed
// colours, pushed once per level as globals by LevelDifficultyResolver (never in a
// Properties block, so a serialized material value can't shadow them). Consumers (the
// rainbow balloon's scrolling diagonal bands, the paint blob's radial rings) compute their
// own band COORDINATE and hand it here to resolve the colour — the mapping stays with the
// consumer, only the palette banding is shared.
fixed4 _RainbowBandColor0;
fixed4 _RainbowBandColor1;
fixed4 _RainbowBandColor2;
fixed4 _RainbowBandColor3;
float  _RainbowBandCount;

inline fixed3 RainbowColorAt(int i)
{
    if (i <= 0) { return _RainbowBandColor0.rgb; }
    if (i == 1) { return _RainbowBandColor1.rgb; }
    if (i == 2) { return _RainbowBandColor2.rgb; }
    return _RainbowBandColor3.rgb;
}

// Maps a continuous band coordinate to a blended palette colour: floor(s) picks the band,
// frac(s) blends into the next over `edge`. Wraps across _RainbowBandCount, correct even for
// a negative (reverse-scrolling) coordinate.
inline fixed3 RainbowBandColor(float s, float edge)
{
    float cell = floor(s);
    float t    = frac(s);

    float n  = max(_RainbowBandCount, 1.0);
    float m  = fmod(fmod(cell, n) + n, n); // always in [0, n)
    int   i0 = (int)m;
    int   i1 = (int)fmod(m + 1.0, n);

    float blend = smoothstep(1.0 - max(edge, 1e-4), 1.0, t);
    return lerp(RainbowColorAt(i0), RainbowColorAt(i1), blend);
}

#endif
