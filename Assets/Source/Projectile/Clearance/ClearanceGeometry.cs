using UnityEngine;

namespace BalloonParty.Projectile.Clearance
{
    /// <summary>
    ///     The bend a trail takes round a clear circle. A point inside the circle's outer band (its radius ×
    ///     (1 + softness)) is pushed out along the line from the centre, and everything inside is squeezed
    ///     into the band, so nothing deflected ends inside the circle itself. The map is smooth and
    ///     monotonic, and it meets the identity at the band's edge with a matching slope, so a trail
    ///     brushing past bends gently and one passing through never snaps.
    /// </summary>
    /// <remarks>
    ///     The band lies just outside the circle, while the effect fade's soft edge
    ///     (<c>Include/Clearance.cginc</c>) lies just inside it, so a bent trail is never also faded.
    /// </remarks>
    internal static class ClearanceGeometry
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        ///     Deflects <paramref name="point" /> out of <paramref name="circle" />. With r the distance to the
        ///     centre, R the radius and B = R(1 + softness), a point with r &lt; B lands at
        ///     R + (B − R)(r / B)^k, where k = B / (B − R) makes the slope 1 at the band's edge.
        /// </summary>
        internal static Vector2 Deflect(Vector2 point, in ClearanceCircle circle, float softness)
        {
            var radius = circle.Radius;
            if (radius <= Epsilon || softness <= Epsilon)
            {
                return point;
            }

            var band = radius * (1f + softness);
            var delta = point - circle.Center;
            var distance = delta.magnitude;
            if (distance >= band)
            {
                return point;
            }

            var exponent = band / (band - radius);
            var deflected = radius + (band - radius) * Mathf.Pow(distance / band, exponent);

            // A point dead on the centre has no direction of its own; up is as good as any, and stable.
            var direction = distance > Epsilon ? delta / distance : Vector2.up;
            return circle.Center + direction * deflected;
        }
    }
}
