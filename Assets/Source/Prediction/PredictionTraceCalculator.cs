using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Prediction
{
    internal class PredictionTraceCalculator
    {
        // What governs a single step while the shot isn't piercing yet — resolved once per iteration by
        // ResolveStepEvent, then applied by Calculate. Splitting "decide" from "apply" keeps the
        // deflector/pierce-item distance comparison out of the main loop's own branching.
        private enum StepEvent
        {
            None,
            Deflected,
            PiercedThrough,
            PiercedAndDeflected
        }

        private readonly IPredictionTraceConfig _config;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IDeflectorField _deflectorField;
        private readonly IPierceItemField _pierceItemField;

        private readonly List<DeflectorCircle> _deflectors = new();
        private readonly List<PierceItemCircle> _pierceItems = new();

        public PredictionTraceCalculator(
            IPredictionTraceConfig config, IProjectileFlightConfig flightConfig, IDeflectorField deflectorField,
            IPierceItemField pierceItemField)
        {
            _config = config;
            _flightConfig = flightConfig;
            _deflectorField = deflectorField;
            _pierceItemField = pierceItemField;
        }

        /// <summary>
        ///     Calculates the prediction trace, bouncing off all four walls and off any balloon that
        ///     would deflect the shot rather than let it through — until the path crosses a
        ///     pierce-item host, from which point on it just continues straight through every tough
        ///     instead, matching a piercing shot's real flight.
        /// </summary>
        /// <remarks>
        ///     Walls and deflections share one reflection budget. They cost the shot differently — a
        ///     wall spends a shield and a deflection does not — but the budget here is about how much
        ///     the telegraph gives away, not about what the shot can afford, and to the player both
        ///     are the same event: the line turned.
        /// </remarks>
        /// <param name="projectileContactRadius">
        ///     The shot's own contact radius. Every deflector (and pierce-item host) is inflated by it,
        ///     because contact happens when the two circles touch — <c>ProjectileView</c> passes
        ///     <c>SurfaceRadius + _contactRadius</c> to the real deflection for the same reason.
        ///     Treating the shot as a point shrinks every target and draws grazing hits as misses.
        /// </param>
        /// <param name="end">
        ///     Where piercing began (or -1), plus what the line ran into at the end and that contact's
        ///     surface normal. Piercing's index lets a view style just the trailing pierced run; the end
        ///     contact lets one draw the bounce leaving it without re-deriving a normal this already
        ///     solved.
        /// </param>
        public void Calculate(
            Vector3 origin, Vector3 direction, float projectileContactRadius, List<Vector3> results,
            out PredictionTraceEnd end)
        {
            results.Clear();
            results.Add(origin);
            var pierceStartIndex = -1;

            _deflectors.Clear();
            _deflectorField?.CollectDeflectors(_deflectors);

            _pierceItems.Clear();
            _pierceItemField?.CollectPierceItems(_pierceItems);

            var walls = new WallLimits(_flightConfig.LimitsClockwise);
            var stepsLeft = _config.MaxSegments;
            var reflectsLeft = _config.MaxReflections;
            var isPiercing = false;

            while (stepsLeft > 0)
            {
                var stepStart = origin;
                var shift = _config.SegmentLength;
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

                if (extended.y > walls.Top)
                {
                    reflect += Vector3.down;
                    shift = (walls.Top - origin.y) / direction.y;
                    extended = origin + (direction * shift);
                }

                // The bottom wall is a real, visible wall the live shot bounces off just like the
                // other three (ProjectileMotionResolver.Step reflects off it the same as any other).
                if (extended.y < walls.Bottom)
                {
                    reflect += Vector3.up;
                    shift = (walls.Bottom - origin.y) / direction.y;
                    extended = origin + (direction * shift);
                }

                if (!isPiercing)
                {
                    // Tested against the step already clipped by any wall above, so an event sitting
                    // beyond that wall cannot steal this iteration from the wall that reaches it first.
                    var stepEvent = ResolveStepEvent(
                        stepStart, direction, shift, projectileContactRadius, out var contact, out var normal,
                        out var surface, out var pierceContact);

                    if (stepEvent is StepEvent.PiercedThrough or StepEvent.PiercedAndDeflected)
                    {
                        isPiercing = true;
                        pierceStartIndex = results.Count;
                        results.Add(walls.ClampInside(pierceContact));
                    }

                    if (stepEvent is StepEvent.Deflected or StepEvent.PiercedAndDeflected)
                    {
                        // Clamped like the real deflection: a balloon in an edge column can sit within
                        // its own radius of a wall, and an unclamped contact starts the next step out of
                        // bounds.
                        origin = walls.ClampInside(contact);

                        // Drawn on the deflector's skin, not where the shot's CENTRE turns — those differ
                        // by the shot's radius, and a thin line whose corner floats a quarter-balloon
                        // short of the balloon reads as a bend that happened too early. The flight
                        // continues from the true contact above, so only the painted corner moves; the
                        // leg after it is offset by that same radius, which is invisible over its length.
                        results.Add(walls.ClampInside(surface));
                        if (reflectsLeft <= 0)
                        {
                            end = new PredictionTraceEnd(
                                pierceStartIndex, PredictionTraceEndKind.Deflector, normal);
                            return;
                        }

                        direction = Vector2.Reflect(direction, normal);
                        reflectsLeft--;
                        stepsLeft--;
                        continue;
                    }
                }

                origin = extended;
                stepsLeft--;

                if (reflect != Vector3.zero)
                {
                    results.Add(extended);

                    if (reflectsLeft <= 0)
                    {
                        end = new PredictionTraceEnd(
                            pierceStartIndex, PredictionTraceEndKind.Wall, reflect.normalized);
                        return;
                    }

                    direction = Vector2.Reflect(direction, reflect.normalized);
                    reflectsLeft--;
                }
            }

            // Points are otherwise only recorded AT a bounce, so a run that ends between them draws
            // nothing for its last leg — reachable once piercing, where nothing but the segment budget
            // itself can end the trace before it ever crosses another wall.
            if (results.Count == 0 || (results[results.Count - 1] - origin).sqrMagnitude > 1e-6f)
            {
                results.Add(origin);
            }

            // Fell out of the step budget rather than hitting anything — the line simply stops, and there
            // is no contact for a consumer to draw a bounce from.
            end = PredictionTraceEnd.OpenAir(pierceStartIndex);
        }

        // Decides what this iteration means for a not-yet-piercing shot, by comparing the nearest
        // deflector against the nearest pierce-item host within the same clipped step. The pierce point
        // governs whenever it's reached no later than the deflector: a DIFFERENT, farther deflector
        // (PiercedThrough) is skipped outright, matching "just continue" — while the SAME host
        // (PiercedAndDeflected, both reads of one circle) still bounces this one time, since live, the
        // item's activation lands a frame after the bounce it was hit on, and only what the shot meets
        // AFTER this contact is piercing, not this contact itself.
        private StepEvent ResolveStepEvent(
            Vector3 stepStart, Vector3 direction, float shift, float projectileContactRadius,
            out Vector3 contact, out Vector2 normal, out Vector3 surface, out Vector3 pierceContact)
        {
            var hasDeflector = TryFindNearestDeflector(
                stepStart, direction, shift, projectileContactRadius, out contact, out normal, out surface,
                out var deflectorDistance);
            var hasPierceItem = TryFindNearestPierceItem(
                stepStart, direction, shift, projectileContactRadius, out pierceContact, out var pierceDistance);

            if (hasPierceItem && (!hasDeflector || pierceDistance <= deflectorDistance))
            {
                return hasDeflector && pierceDistance >= deflectorDistance
                    ? StepEvent.PiercedAndDeflected
                    : StepEvent.PiercedThrough;
            }

            return hasDeflector ? StepEvent.Deflected : StepEvent.None;
        }

        // Nearest, not first found: two balloons can both lie along one step and only the closer is hit.
        private bool TryFindNearestDeflector(
            Vector3 origin, Vector3 direction, float maxDistance, float projectileContactRadius,
            out Vector3 contact, out Vector2 normal, out Vector3 surface, out float entryDistance)
        {
            contact = default;
            normal = default;
            surface = default;
            entryDistance = float.MaxValue;
            var nearest = float.MaxValue;

            for (var i = 0; i < _deflectors.Count; i++)
            {
                var deflector = _deflectors[i];
                var contactRadius = deflector.Radius + projectileContactRadius;
                if (!CircleContact.TryFindEntry(
                        origin, direction, deflector.Center, contactRadius, out var entryNormal,
                        out var candidateDistance))
                {
                    continue;
                }

                if (candidateDistance > maxDistance || candidateDistance >= nearest)
                {
                    continue;
                }

                nearest = candidateDistance;
                normal = entryNormal;
                contact = deflector.Center + (entryNormal * contactRadius);
                contact.z = origin.z;
                surface = deflector.Center + (entryNormal * deflector.Radius);
                surface.z = origin.z;
            }

            entryDistance = nearest;
            return nearest < float.MaxValue;
        }

        // Mirrors TryFindNearestDeflector: nearest wins, so a graze past a closer host doesn't skip ahead
        // to arm on a farther one first. Reports its own entry distance so the caller can compare it
        // directly against a same-step deflector's, rather than reconstructing either from a contact point.
        private bool TryFindNearestPierceItem(
            Vector3 origin, Vector3 direction, float maxDistance, float projectileContactRadius,
            out Vector3 contact, out float entryDistance)
        {
            contact = default;
            entryDistance = float.MaxValue;
            var nearest = float.MaxValue;

            for (var i = 0; i < _pierceItems.Count; i++)
            {
                var item = _pierceItems[i];
                var contactRadius = item.Radius + projectileContactRadius;
                if (!CircleContact.TryFindEntry(
                        origin, direction, item.Center, contactRadius, out var entryNormal,
                        out var candidateDistance))
                {
                    continue;
                }

                if (candidateDistance > maxDistance || candidateDistance >= nearest)
                {
                    continue;
                }

                nearest = candidateDistance;
                contact = item.Center + (entryNormal * contactRadius);
                contact.z = origin.z;
            }

            entryDistance = nearest;
            return nearest < float.MaxValue;
        }
    }
}
