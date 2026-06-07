// Fractal leaf vein system shared between BushBakeLeaf and BushBake.
//
// Coordinate convention — caller provides:
//   axial  : signed distance along the leaf direction from centre (world units)
//   perp   : signed perpendicular distance from the midrib (world units)
//   halfLen: half the leaf length (world units)
//   halfW  : half the leaf width  (world units)
//
// Returns a darkening multiplier in [veinDarken, 1] to multiply into colour.

#ifndef LEAF_VEINS_INCLUDED
#define LEAF_VEINS_INCLUDED

// Maximum lateral veins per level (loop bound).
#define MAX_LATERAL_VEINS 12

// Find the minimum perpendicular distance from point (stemT, ap) to any
// diagonal vein line. Each vein originates at (originT, 0) on the parent
// axis and extends diagonally with slope `slopeN` in normalised space.
//
// stemT  : normalised position along parent axis [0,1]
// ap     : absolute normalised perpendicular distance [0,1]
// count  : number of veins
// slopeN : rise-over-run of the vein in (stemT, perpN) space
//
// Returns: min perpendicular distance in normalised units

float MinVeinDist(float stemT, float ap, int count, float slopeN)
{
    float spacing = 1.0 / (float(count) + 1.0);
    float invLineLen = 1.0 / sqrt(slopeN * slopeN + 1.0);
    float best = 999.0;

    for (int i = 1; i <= MAX_LATERAL_VEINS; i++)
    {
        if (i > count) break;
        float originT = float(i) * spacing;
        float da = stemT - originT;

        // Perpendicular distance from (da, ap) to line through (0,0)
        // with direction (slopeN, 1)
        float dist = abs(da - ap * slopeN) * invLineLen;
        best = min(best, dist);
    }

    return best;
}

float FractalVeins(float axial, float perp, float halfLen, float halfW,
                   float veinWidth, float veinDarken, int veinDepth, float veinCount)
{
    float result = 1.0;

    // Normalise: stemT [0,1] along leaf, perpN [-1,1] across leaf
    float stemT = saturate((axial + halfLen) / max(2.0 * halfLen, 0.001));
    float perpN = perp / max(halfW, 0.001);
    float ap = abs(perpN);

    // ── Level 0: Midrib — line along perpN = 0 ──
    float midW = veinWidth * lerp(2.0, 0.4, stemT);
    float midLine = 1.0 - smoothstep(midW * 0.3, midW, ap);
    float midMask = smoothstep(0.0, 0.05, stemT) * smoothstep(1.0, 0.92, stemT);
    result *= lerp(1.0, veinDarken, midLine * midMask);

    if (veinDepth < 2) return result;

    // ── Level 1: Primary laterals — diagonal lines from midrib ──
    // Slope in normalised space: correct for aspect ratio so the visual
    // angle matches `angle` regardless of leaf proportions.
    float angle = 0.7;
    float slopeN = cos(angle) / max(sin(angle), 0.01)
                 * (halfW / max(halfLen, 0.001));

    float dist1 = MinVeinDist(stemT, ap, int(veinCount), slopeN);

    float latW = veinWidth * 0.5 * lerp(1.0, 0.15, ap);
    float latLine = 1.0 - smoothstep(latW * 0.3, latW, dist1);
    float latMask = smoothstep(0.06, 0.18, stemT)
                  * smoothstep(0.96, 0.82, stemT)
                  * smoothstep(0.03, 0.1, ap)
                  * smoothstep(1.0, 0.55, ap);
    result *= lerp(1.0, veinDarken, latLine * latMask);

    if (veinDepth < 3) return result;

    // ── Level 2: Secondary branches ──
    // Re-parameterise: along the nearest primary vein becomes the new axis.
    // Use the same MinVeinDist but with the primary vein distance as the
    // new perpendicular and ap as the new axial.
    float subCount = int(max(veinCount * 0.5, 2.0));
    float dist2 = MinVeinDist(ap, dist1 * 5.0, subCount, slopeN);
    float subW = veinWidth * 0.3 * lerp(1.0, 0.15, ap);
    float subLine = 1.0 - smoothstep(subW * 0.3, subW, dist2);
    float subMask = latMask
                  * smoothstep(0.1, 0.25, ap)
                  * smoothstep(0.9, 0.5, ap);
    result *= lerp(1.0, lerp(1.0, veinDarken, 0.5), subLine * subMask);

    if (veinDepth < 4) return result;

    // ── Level 3: Tertiary ──
    float dist3 = MinVeinDist(ap * 1.5, dist2 * 5.0, subCount, slopeN);
    float terW = veinWidth * 0.18;
    float terLine = 1.0 - smoothstep(terW * 0.3, terW, dist3);
    float terMask = subMask * 0.5;
    result *= lerp(1.0, lerp(1.0, veinDarken, 0.3), terLine * terMask);

    return result;
}

#endif // LEAF_VEINS_INCLUDED
