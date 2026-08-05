using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Analytic ray-circle entry, shared by the projectile's real deflection
    ///     (<c>ProjectileMotionResolver</c>) and the aim telegraph
    ///     (<c>PredictionTraceCalculator</c>).
    /// </summary>
    /// <remarks>
    ///     One implementation on purpose. A telegraph that computed its own reflection would drift from
    ///     the flight it predicts, and the drift would be invisible until a player trusted the line and
    ///     missed — the failure this type exists to make impossible.
    /// </remarks>
    internal static class CircleContact
    {
        /// <summary>
        ///     Surface normal where a ray travelling <paramref name="direction" /> from
        ///     <paramref name="position" /> first enters the circle, or false when the forward ray never
        ///     does. Entry only — a position already inside the circle has no forward entry and returns
        ///     false, which callers handle as a penetration case.
        /// </summary>
        internal static bool TryFindEntryNormal(
            Vector2 position, Vector2 direction, Vector2 center, float radius, out Vector2 normal)
        {
            return TryFindEntry(position, direction, center, radius, out normal, out _);
        }

        /// <summary>
        ///     As <see cref="TryFindEntryNormal" />, also reporting how far along the ray the entry sits.
        ///     The trace needs the distance to pick the nearest of several circles on one step.
        /// </summary>
        internal static bool TryFindEntry(
            Vector2 position, Vector2 direction, Vector2 center, float radius, out Vector2 normal,
            out float entryDistance)
        {
            normal = default;
            entryDistance = 0f;
            if (radius <= 0f || direction.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            var travel = direction.normalized;
            var toPosition = position - center;
            var along = Vector2.Dot(toPosition, travel);
            var discriminant = (along * along) - toPosition.sqrMagnitude + (radius * radius);
            if (discriminant < 0f)
            {
                // A hair below zero is a tangent hit rounded wrong, not a miss.
                if (discriminant < -1e-6f)
                {
                    return false;
                }

                discriminant = 0f;
            }

            entryDistance = -along - Mathf.Sqrt(discriminant);
            if (entryDistance <= 0f)
            {
                return false;
            }

            normal = (toPosition + (travel * entryDistance)) / radius;
            return true;
        }
    }
}
