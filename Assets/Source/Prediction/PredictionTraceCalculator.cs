using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
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
        private readonly IBumperField _bumperField;
        private readonly IBumperBounceRule _bounceRule;
        private readonly IMushroomSettings _mushrooms;

        private readonly List<DeflectorCircle> _deflectors = new();
        private readonly List<PierceItemCircle> _pierceItems = new();

        /// <param name="bumperField">The board's bumpers (the meadow's mushrooms), with the rule the ball
        /// leaves one by and the chain's settings: the three the live flight has
        /// (<c>ProjectileMotionResolver</c>). Without them the line is the one it was before the meadow had
        /// mushrooms.</param>
        public PredictionTraceCalculator(
            IPredictionTraceConfig config, IProjectileFlightConfig flightConfig, IDeflectorField deflectorField,
            IPierceItemField pierceItemField, IBumperField bumperField = null, IBumperBounceRule bounceRule = null,
            IMushroomSettings mushrooms = null)
        {
            _config = config;
            _flightConfig = flightConfig;
            _deflectorField = deflectorField;
            _pierceItemField = pierceItemField;
            _bumperField = bumperField;
            _bounceRule = bounceRule;
            _mushrooms = mushrooms;
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
        ///     <para>
        ///         <b>Mushrooms</b> (@ref plan_meadow_mushrooms) bend the line the way they will bend the ball:
        ///         the same swept test (<see cref="BumperSweep" />), the same count
        ///         (<see cref="MushroomChainRule" />) and the same bounce rule, asked in the live flight's
        ///         order, so the forgiving kick shows in the line. A bounce spends the one reflection budget.
        ///         The bounce that completes a chain arms the ball, so the line is a lance's from that corner
        ///         on (<see cref="PredictionTraceEnd.PierceStartIndex" />): through balloons and through
        ///         mushrooms, whose caps the lance cuts. A cap already cut is not in the field and bends nothing.
        ///     </para>
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
        /// <param name="startsPiercing">
        ///     The shot leaves the muzzle already piercing (a ball loaded as a Snipe lance): the whole line runs
        ///     straight through balloons and mushrooms, turning only at walls, and its pierce starts at its
        ///     first point.
        /// </param>
        /// <param name="chain">
        ///     The ball's tally of mushroom bounces as it leaves the muzzle
        ///     (<c>ProjectileFlightState.MushroomChain</c>): the kick depends on the count. A ball that has
        ///     not flown has the default.
        /// </param>
        public void Calculate(
            Vector3 origin, Vector3 direction, float projectileContactRadius, List<Vector3> results,
            out PredictionTraceEnd end, bool startsPiercing = false, MushroomChainState chain = default)
        {
            results.Clear();
            results.Add(origin);
            var pierceStartIndex = startsPiercing ? 0 : -1;

            _deflectors.Clear();
            _deflectorField?.CollectDeflectors(_deflectors);

            _pierceItems.Clear();
            _pierceItemField?.CollectPierceItems(_pierceItems);

            var walls = new WallLimits(_flightConfig.LimitsClockwise);
            var stepsLeft = _config.MaxSegments;
            var reflectsLeft = _config.MaxReflections;
            var isPiercing = startsPiercing;

            // Null on a board without mushrooms, which then costs the loop one comparison a step.
            var bumpers = BumpersInPlay();
            var leftBumperId = MushroomChainState.NoBumper;
            var leftBumperDirection = Vector2.zero;

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

                // A mushroom on the step, tested like the balloons against the step already clipped by any
                // wall. Whatever the step meets before it still happens first; nothing past it is looked for.
                // Not for a lance: it flies through.
                var reach = shift;
                var bumperIndex = -1;
                var bumperEntry = 0f;
                var meetsBumper = bumpers != null && !isPiercing && TryFindBumper(
                    bumpers, walls, chain, stepStart, direction, shift,
                    (Vector2)direction == leftBumperDirection ? leftBumperId : MushroomChainState.NoBumper,
                    out bumperIndex, out bumperEntry);
                if (meetsBumper)
                {
                    reach = Mathf.Max(0f, bumperEntry);
                }

                if (!isPiercing)
                {
                    // Tested against the step already clipped by any wall above, so an event sitting
                    // beyond that wall cannot steal this iteration from the wall that reaches it first.
                    var stepEvent = ResolveStepEvent(
                        stepStart, direction, reach, projectileContactRadius, out var contact, out var normal,
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

                // A line that became a lance's on this step, at a pierce item before the mushroom, is not bent
                // by it either.
                if (meetsBumper && !isPiercing)
                {
                    // The live flight's order (ProjectileMotionResolver.BounceOffBumpers): the bounce is
                    // counted, then the rule is asked with the exact entry point and the chain as counted.
                    Vector2 heading = direction;
                    var bumperContact = new BumperContact(
                        (Vector2)stepStart + (heading * bumperEntry), heading, bumpers[bumperIndex]);
                    var reward = MushroomChainRule.OnBounce(ref chain, bumperContact.Bumper.Wall, _mushrooms.Rules);
                    var outgoing = _bounceRule.Resolve(bumperContact, bumpers, chain);
                    var outward = (bumperContact.Point - bumperContact.Bumper.Center).normalized;
                    origin = new Vector3(bumperContact.Point.x, bumperContact.Point.y, stepStart.z);

                    // Drawn on the mushroom's own skin, as a deflector's corner is: the ball's centre turns
                    // its own radius short of it.
                    var skin = bumperContact.Bumper.Center
                        + (outward * Mathf.Max(0f, bumperContact.Bumper.Radius - projectileContactRadius));
                    results.Add(walls.ClampInside(new Vector3(skin.x, skin.y, stepStart.z)));

                    // The bounce that completes a chain arms the ball where it happens: from this corner on
                    // the line is a lance's.
                    if (reward == MushroomReward.Arm)
                    {
                        isPiercing = true;
                        pierceStartIndex = results.Count - 1;
                    }

                    if (reflectsLeft <= 0)
                    {
                        end = new PredictionTraceEnd(pierceStartIndex, PredictionTraceEndKind.Bumper, outward);
                        return;
                    }

                    direction = outgoing;
                    leftBumperId = bumperContact.Bumper.Id;
                    leftBumperDirection = outgoing;
                    reflectsLeft--;
                    stepsLeft--;
                    continue;
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

                    // A wall ends a chain of mushroom bounces, in the line as in the flight.
                    if (bumpers != null)
                    {
                        MushroomChainRule.OnWall(ref chain);
                    }
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

        // The board's bumpers, or null when it has none or the calculator was built without them.
        private IReadOnlyList<BumperCircle> BumpersInPlay()
        {
            if (_bumperField == null || _bounceRule == null || _mushrooms == null)
            {
                return null;
            }

            var bumpers = _bumperField.Bumpers;
            return bumpers.Count > 0 ? bumpers : null;
        }

        // The first mushroom the ball would bounce off on this step, under the live flight's own conditions:
        // none once the stretch between two walls has had its bounces, not the one being left while the line
        // still runs the direction it left along, and none whose contact lies beyond the walls. The entry is
        // zero or less for a step that starts inside a circle: it lies behind the start, where the line came in.
        private bool TryFindBumper(
            IReadOnlyList<BumperCircle> bumpers, in WallLimits walls, in MushroomChainState chain, Vector3 stepStart,
            Vector3 direction, float length, int leavingId, out int index, out float entry)
        {
            index = -1;
            entry = 0f;
            if (MushroomChainRule.IsSpent(chain, _mushrooms.Rules))
            {
                return false;
            }

            return BumperSweep.TryFindFirst(bumpers, stepStart, direction, length, leavingId, out index, out entry)
                && walls.Contains(stepStart + (direction * entry));
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
