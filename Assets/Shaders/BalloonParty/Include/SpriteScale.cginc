#ifndef BALLOONPARTY_SPRITE_SCALE_INCLUDED
#define BALLOONPARTY_SPRITE_SCALE_INCLUDED

// Sprite scaling utilities — shrinks the sprite within the quad so shadow/effects
// have transparent margins to render into.

// Scales UV inward from centre. scale=1 is full size; <1 shrinks.
inline float2 ScaleSpriteUV(float2 uv, float scale)
{
    return (uv - 0.5) / scale + 0.5;
}

// Returns 1 inside [0,1] UV bounds, 0 outside. Use after ScaleSpriteUV to mask overflow.
inline float SpriteBoundsMask(float2 uv)
{
    float2 inBounds = step(0.0, uv) * step(uv, 1.0);
    return inBounds.x * inBounds.y;
}

#endif // BALLOONPARTY_SPRITE_SCALE_INCLUDED
