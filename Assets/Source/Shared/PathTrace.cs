using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Tracing a projectile's wall-reflected path ahead to test whether it stays clear. The
    ///     per-segment occupancy test belongs to the caller (live physics, analytic board sim, ...);
    ///     this owns only the shared skeleton — reflect off each wall via <see cref="WallLimits" />,
    ///     stopping at the first blocked segment.
    /// </summary>
    internal static class PathTrace
    {
        /// <summary>Is anything blocking the straight run from <paramref name="from" /> along
        /// <paramref name="direction" /> for <paramref name="length" /> world units? Return true to
        /// report the segment blocked.</summary>
        internal delegate bool SegmentBlocked(Vector2 from, Vector2 direction, float length);

        /// <summary>Traces the wall-reflected ray for <paramref name="bounces" /> crossings, asking
        /// <paramref name="blocked" /> about each segment up to its wall-crossing point. False as soon
        /// as a segment is blocked (or the ray finds no wall crossing); true only if every traced
        /// segment is clear.</summary>
        internal static bool IsClearAhead(
            in WallLimits walls, Vector3 position, Vector3 direction, int bounces, SegmentBlocked blocked)
        {
            for (var i = 0; i < bounces; i++)
            {
                if (!walls.TryFindCrossing(position, direction, out var crossing, out var wallNormal))
                {
                    return false;
                }

                if (blocked(position, direction, Vector3.Distance(position, crossing)))
                {
                    return false;
                }

                position = crossing;
                direction = Vector3.Reflect(direction, wallNormal.normalized);
            }

            return true;
        }
    }
}
