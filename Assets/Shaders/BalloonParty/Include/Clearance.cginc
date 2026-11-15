#ifndef BALLOONPARTY_CLEARANCE_INCLUDED
#define BALLOONPARTY_CLEARANCE_INCLUDED

// The clear circles round the balls in flight (Projectile/Clearance, PLAN-PenClearance). An effect drawn over
// the ball multiplies its output by ClearanceAlpha, so it thins out round each ball instead of burying it.
// Globals, pushed every frame by ClearanceFieldTicker; never declare them in a Properties block. The array's
// length is ClearanceCircle.MaxCircles on the C# side: change both together.
float4 _ClearanceCircles[8]; // xy: world centre, z: radius (already x strength), w: strength 0..1
float  _ClearanceCount;      // 0 with no ball in flight, or with the fade switched off
float  _ClearanceFloor;      // alpha left at the centre of a full-strength circle
float  _ClearanceSoftness;   // width of the soft edge, as a share of the radius, inside the circle

// 1 outside every circle, falling smoothly to the floor across the soft edge just inside each one; the floor
// itself eases from 1 to _ClearanceFloor with the circle's strength. Overlapping circles take the lowest.
// The trail bend (ClearanceGeometry.Deflect) works in a band just OUTSIDE the circle, so a bent trail never
// lands where this fades.
inline half ClearanceAlpha(float2 worldXY)
{
    half alpha = 1.0h;
    int count = (int)_ClearanceCount;
    [loop]
    for (int i = 0; i < count; i++)
    {
        float4 circle = _ClearanceCircles[i];
        float radius = max(circle.z, 1e-4);
        float edge = smoothstep(radius * (1.0 - _ClearanceSoftness), radius, distance(worldXY, circle.xy));
        half floorAlpha = lerp(1.0h, (half)_ClearanceFloor, (half)circle.w);
        alpha = min(alpha, lerp(floorAlpha, 1.0h, (half)edge));
    }

    return alpha;
}

#endif
