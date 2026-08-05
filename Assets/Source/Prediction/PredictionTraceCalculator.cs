using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Prediction
{
    internal class PredictionTraceCalculator
    {
        private readonly IPredictionTraceConfig _config;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IDeflectorField _deflectorField;

        private readonly List<DeflectorCircle> _deflectors = new();

        public PredictionTraceCalculator(
            IPredictionTraceConfig config, IProjectileFlightConfig flightConfig, IDeflectorField deflectorField)
        {
            _config = config;
            _flightConfig = flightConfig;
            _deflectorField = deflectorField;
        }

        /// <summary>
        ///     Calculates the prediction trace, bouncing off the left/right/top walls and off any
        ///     balloon that would deflect the shot rather than let it through.
        /// </summary>
        /// <remarks>
        ///     Walls and deflections share one reflection budget. They cost the shot differently — a
        ///     wall spends a shield and a deflection does not — but the budget here is about how much
        ///     the telegraph gives away, not about what the shot can afford, and to the player both
        ///     are the same event: the line turned.
        /// </remarks>
        /// <param name="projectileContactRadius">
        ///     The shot's own contact radius. Every deflector is inflated by it, because contact
        ///     happens when the two circles touch — <c>ProjectileView</c> passes
        ///     <c>SurfaceRadius + _contactRadius</c> to the real deflection for the same reason.
        ///     Treating the shot as a point shrinks every target and draws grazing hits as misses.
        /// </param>
        public void Calculate(
            Vector3 origin, Vector3 direction, float projectileContactRadius, List<Vector3> results)
        {
            results.Clear();
            results.Add(origin);

            _deflectors.Clear();
            _deflectorField?.CollectDeflectors(_deflectors);

            var walls = new WallLimits(_flightConfig.LimitsClockwise);
            var stepsLeft = _config.PredictionTraceMaxSteps;
            var reflectsLeft = _config.PredictionTraceMaxReflections;

            while (stepsLeft > 0)
            {
                var shift = _config.PredictionTraceStep;
                var extended = origin + (direction * shift);
                var reflect = Vector3.zero;

                if (extended.x > walls.Right)
                {
                    reflect += Vector3.left;
                    shift = (walls.Right - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                if (extended.x < walls.Left)
                {
                    reflect += Vector3.right;
                    shift = (walls.Left - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                var topHit = false;
                if (extended.y > walls.Top)
                {
                    reflect += Vector3.down;
                    shift = (walls.Top - origin.y) / direction.y;
                    extended = origin + (direction * shift);
                    topHit = true;
                }

                // Tested against the step already clipped by any wall above, so a deflector sitting
                // beyond that wall cannot steal a bounce the wall reaches first.
                if (TryFindNearestDeflector(
                        origin, direction, shift, projectileContactRadius, out var contact, out var normal))
                {
                    // Clamped like the real deflection: a balloon in an edge column can sit within its
                    // own radius of a wall, and an unclamped contact starts the next step out of bounds.
                    origin = walls.ClampInside(contact);
                    results.Add(origin);
                    if (reflectsLeft <= 0)
                    {
                        return;
                    }

                    direction = Vector2.Reflect(direction, normal);
                    reflectsLeft--;
                    stepsLeft--;
                    continue;
                }

                origin = extended;
                stepsLeft--;

                if (reflect != Vector3.zero)
                {
                    results.Add(extended);

                    // The top wall ends the shot in flight, so the line ends with it.
                    if (topHit || reflectsLeft <= 0)
                    {
                        return;
                    }

                    direction = Vector2.Reflect(direction, reflect.normalized);
                    reflectsLeft--;
                }
            }

            // Points are otherwise only recorded AT a bounce, so a run that ends between them draws
            // nothing for its last leg. Rare before deflections existed — every upward shot ends on a
            // wall — but a deflection routinely sends the line downward, where there is no bottom
            // limit to terminate on, and the trace would stop dead at the balloon.
            if (results.Count == 0 || (results[results.Count - 1] - origin).sqrMagnitude > 1e-6f)
            {
                results.Add(origin);
            }
        }

        // Nearest, not first found: two balloons can both lie along one step and only the closer is hit.
        private bool TryFindNearestDeflector(
            Vector3 origin, Vector3 direction, float maxDistance, float projectileContactRadius,
            out Vector3 contact, out Vector2 normal)
        {
            contact = default;
            normal = default;
            var nearest = float.MaxValue;

            for (var i = 0; i < _deflectors.Count; i++)
            {
                var deflector = _deflectors[i];
                var contactRadius = deflector.Radius + projectileContactRadius;
                if (!CircleContact.TryFindEntry(
                        origin, direction, deflector.Center, contactRadius, out var entryNormal,
                        out var entryDistance))
                {
                    continue;
                }

                if (entryDistance > maxDistance || entryDistance >= nearest)
                {
                    continue;
                }

                nearest = entryDistance;
                normal = entryNormal;
                contact = deflector.Center + (entryNormal * contactRadius);
                contact.z = origin.z;
            }

            return nearest < float.MaxValue;
        }
    }
}
