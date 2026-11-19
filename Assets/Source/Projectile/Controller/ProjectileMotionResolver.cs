using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>Plain, headless-testable projectile flight rules; the view only applies the returned <see cref="ProjectileStep" />.</summary>
    internal sealed class ProjectileMotionResolver
    {
        // The most bumpers one stretch of a step bounces the ball off. Past it the rest of the stretch is flown
        // untested and the next step picks the ball up, wherever it is.
        private const int MaxBumperBouncesPerStretch = 8;

        // A stretch shorter than this has no direction worth trusting, and nothing to fly.
        private const float MinStretchLength = 1e-6f;

        // The most walls one step's stretch is followed past when it is rebuilt: a corner is two.
        private const int MaxWallsPerStep = 4;

        private readonly WallLimits _walls;
        private readonly ProjectileTapResolver _taps;
        private readonly AnimationCurve _lastShieldApproachCurve;
        private readonly float _lastShieldApproachDuration;
        private readonly IBumperField _bumperField;
        private readonly IBumperBounceRule _bounceRule;
        private readonly IMushroomSettings _mushrooms;

        // One list for every ball: this resolver is shared, and a step refills it. So it holds a step's bounces
        // only until the next Step call, of any ball. The view copies them out before it tells anyone.
        private readonly List<BumperBounce> _stepBounces = new();
        private readonly List<CapCut> _stepCuts = new();

        // The view needs the same wall geometry for its cruise lookahead trace.
        internal WallLimits Walls => _walls;

        /// <summary>
        ///     The bumpers the ball bounced off during the last <see cref="Step" />, in order. Read them straight
        ///     after the step that reported them (<see cref="ProjectileStep.BumperBounces" />): the next step, of
        ///     any ball, starts the list again. Copy them before publishing anything about them: a subscriber
        ///     that stepped a ball would refill the list under the loop reading it.
        /// </summary>
        internal IReadOnlyList<BumperBounce> StepBumperBounces => _stepBounces;

        /// <summary>
        ///     The caps a lance cut during the last <see cref="Step" />, in order: each has already left the
        ///     bumper field. Read and copied as <see cref="StepBumperBounces" /> is, straight after the step.
        /// </summary>
        internal IReadOnlyList<CapCut> StepCapCuts => _stepCuts;

        [Inject]
        internal ProjectileMotionResolver(
            IProjectileFlightConfig config, ProjectileTapResolver taps, IBumperField bumperField,
            IBumperBounceRule bounceRule, IMushroomSettings mushrooms)
            : this(config, taps)
        {
            _bumperField = bumperField;
            _bounceRule = bounceRule;
            _mushrooms = mushrooms;
        }

        // With no bumper field every shot flies as it did before the meadow had mushrooms.
        internal ProjectileMotionResolver(IProjectileFlightConfig config, ProjectileTapResolver taps)
        {
            _walls = new WallLimits(config.LimitsClockwise);
            _taps = taps;
            // Blends between two real speeds (see ResolveFlightSpeed), so an empty curve is harmless
            // here — it just holds the arming speed — but a linear fallback keeps it accelerating.
            // An un-authored (newly-added) serialized curve deserializes empty and evaluates to 0,
            // which would crawl the shot — fall back to a constant-1 no-op until it's authored.
            _lastShieldApproachCurve = config.LastShieldApproachCurve is { length: > 0 }
                ? config.LastShieldApproachCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _lastShieldApproachDuration = config.LastShieldApproachDuration;
        }

        /// <summary>
        ///     Advances one fixed step, mutating direction/shield count on a wall bounce. On a board with bumpers
        ///     (the meadow's mushrooms) the step's stretch is flown against them before the walls: the ball
        ///     bounces off each one it meets, spending no shield, and the step reports how many
        ///     (<see cref="ProjectileStep.BumperBounces" />, each in <see cref="StepBumperBounces" />). The bounce
        ///     that arms the ball (<see cref="MushroomReward.Arm" />) ends the step where it happened: see
        ///     <see cref="BounceOffBumpers" />. A piercing ball bounces off none: it flies on unturned and cuts
        ///     every whole cap its stretch enters (<see cref="StepCapCuts" />). On a board without bumpers the
        ///     step is the walls alone, as it always was.
        /// </summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            var speed = _taps.ResolveSpeed(model, deltaTime);
            var from = position;
            position = Advance(model, position, speed, deltaTime);

            var bumpers = BumpersInPlay();
            if (bumpers == null)
            {
                return ResolveWalls(model, position, speed);
            }

            // After the bumpers, the leg that goes on to the walls: from the last bounce, or the step's start.
            var legStart = from;
            position = BounceOffBumpers(model, bumpers, ref legStart, position, afterWall: false);
            var step = ResolveWalls(model, position, speed);
            if (step.Outcome == ProjectileStepOutcome.Bounced)
            {
                step = BounceOffBumpersPastWall(model, bumpers, step, legStart, position);
            }

            return _stepBounces.Count == 0
                ? step
                : step.WithBumperBounces(_stepBounces.Count, _stepBounces[^1].Point);
        }

        // Where the step would take the ball with nothing in its way.
        private Vector3 Advance(IWriteableProjectileModel model, Vector3 position, float speed, float deltaTime)
        {
            // The 'last breath': on a doomed 0-shield segment (flagged by the view once the path to
            // the death wall is clear of any shield source), traverse origin -> wall over a FIXED
            // wall-clock time (normalized, so segment length doesn't change the moment's pace), the
            // curve easing the position. Once the timer completes, overshoot past the wall so the
            // shot crosses and dies rather than resting on it.
            if (model.IsLastShieldApproach.Value
                && _lastShieldApproachDuration > 1e-4f
                && _walls.TryFindCrossing(model.Flight.SegmentStartPosition, model.Direction, out var deathWall, out _))
            {
                var segmentLength = Vector3.Distance(model.Flight.SegmentStartPosition, deathWall);
                var normalizedTime = Mathf.Clamp01(model.Flight.SegmentElapsed / _lastShieldApproachDuration);
                var distance = normalizedTime >= 1f
                    ? segmentLength + (speed * deltaTime)
                    : segmentLength * Mathf.Clamp01(_lastShieldApproachCurve.Evaluate(normalizedTime));
                model.Flight.SegmentElapsed += deltaTime;
                position = model.Flight.SegmentStartPosition + (Vector3)(model.Direction.normalized * distance);
            }
            else
            {
                model.Flight.SegmentElapsed += deltaTime;
                position += model.Direction * (speed * deltaTime);
            }

            return position;
        }

        // The walls: mirrors a ball that crossed one back inside, and spends the shield the bounce costs.
        private ProjectileStep ResolveWalls(IWriteableProjectileModel model, Vector3 position, float speed)
        {
            position = _walls.Reflect(position, out var reflect, out var wallContact);

            if (reflect == Vector3.zero)
            {
                return ProjectileStep.Moved(position, model.Direction, speed);
            }

            if (model.ShieldsRemaining.Value <= 0)
            {
                // A dead shot stops AT the wall — the mirrored continuation is for survivors.
                return ProjectileStep.Destroyed(wallContact, model.Direction, speed);
            }

            model.ShieldsRemaining.Value--;

            // This surviving wall hit is now the one any tap belongs to. Both grant rules resolve against
            // it — the cruise bounce below, and the view's sweep once this Step returns — so the funnel
            // can tell a second claim on the same hit from a claim on the next one.
            model.Flight.WallHitSequence++;

            // A wall is the one thing that ends a chain of mushroom bounces, and it ends it here, where the
            // wall bounce is counted. On a flight that has met no mushroom this clears what is already clear.
            MushroomChainRule.OnWall(ref model.Flight.MushroomChain);

            // Wall-discharge: a piercing shot that plowed through at least one tough on this segment
            // discharges at the wall — the view resolves the pending toughs, and the pierce (with its
            // speed buff) ends unless a banked Snipe charge re-arms it. If the segment had no toughs,
            // piercing continues indefinitely.
            if (model.IsPiercing.Value && model.Flight.PendingPierceHits.Count > 0)
            {
                model.SpendPierce();

                model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
                model.Flight.SegmentStartPosition = wallContact;
                model.Flight.SegmentElapsed = 0f;
                return ProjectileStep.Bounced(position, wallContact, model.Direction, speed);
            }

            // Consecutive wall bounces with no balloon contact = the shot may be ping-ponging empty
            // space (HitResolver resets the counter on any balloon touch). Entry into cruise is the
            // VIEW's call — it confirms with a physics lookahead the plain resolver can't run.
            model.Flight.ConsecutiveWallBounces++;

            // An empty-corridor cruise bounce is one of the two rules that earn a tap; the funnel owns
            // whether it actually mints one (at most one per wall hit) and what the tap does.
            if (model.IsCruising.Value)
            {
                _taps.TryGrantTap(model);
            }

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
            model.Flight.SegmentStartPosition = wallContact;
            model.Flight.SegmentElapsed = 0f;
            return ProjectileStep.Bounced(position, wallContact, model.Direction, speed);
        }

        // The board's bumpers, or null when it has none: every level but a meadow's, and every resolver built
        // without a bumper field. Null sends Step down the path it had before there were mushrooms.
        private IReadOnlyList<BumperCircle> BumpersInPlay()
        {
            if (_bumperField == null)
            {
                return null;
            }

            // The last step's bounces and cuts have been answered for; this step's start the lists again.
            if (_stepBounces.Count > 0)
            {
                _stepBounces.Clear();
            }

            if (_stepCuts.Count > 0)
            {
                _stepCuts.Clear();
            }

            var bumpers = _bumperField.Bumpers;
            return bumpers.Count > 0 ? bumpers : null;
        }

        /// <summary>
        ///     Flies the stretch from <paramref name="from" /> to <paramref name="to" /> against the bumpers and
        ///     returns where it really ends. The whole stretch is swept, so no speed carries the ball through a
        ///     bumper, and the earliest entry wins. At each one the bounce is counted
        ///     (<see cref="MushroomChainRule.OnBounce" />), then the bounce rule is asked with the exact entry
        ///     point, the ball turns, and what is left of the stretch is flown along the new direction, as
        ///     <see cref="Deflect" /> carries a deflecting balloon's remainder. The bumper being left is not tested
        ///     again while the ball flies that direction. <paramref name="from" /> comes back as where the last
        ///     straight leg of the stretch began: the last bounce, or where it was.
        /// </summary>
        /// <remarks>
        ///     A ball the mushrooms no longer bounce (<see cref="MushroomReward.PassThrough" />: its stretch between
        ///     two walls has had every bounce it is allowed) is not turned and the bounce rule is not asked: it
        ///     flies on through them to a wall. A piercing shot is not turned either, and nothing is counted for
        ///     it: it cuts the caps on the stretch (<see cref="CutCaps" />) and the stretch ends where it was
        ///     going to. A ball found inside a bumper (set down there by the tail of a deflect) bounces at the
        ///     point its line came in, and what it has flown since is carried along the new direction too.
        ///     <para>
        ///         <b>The bounce that arms is the end of the step.</b> When the chain rule answers
        ///         <see cref="MushroomReward.Arm" /> the ball turns and stops
        ///         on the mushroom: what was left of the stretch is not flown, this step or later. The ball is
        ///         armed by whoever hears the step's bounces, after the step, so nothing the step did past that
        ///         bounce could be flown as the ball it had just become: a wall there would be settled as an
        ///         ordinary ball's (no pierce to discharge, a shield from a ball that might die there and be
        ///         armed dead). Ending the step on the mushroom means the next one flies from the bounce as the
        ///         lance, at the lance's speed, and no wall shares a step with the arming. It costs the ball
        ///         less than one step's length of travel, once per arming. The cap that armed the ball is the one
        ///         it is leaving, which no sweep tests while it flies that way, so it is not cut by its own lance.
        ///     </para>
        /// </remarks>
        private Vector3 BounceOffBumpers(
            IWriteableProjectileModel model, IReadOnlyList<BumperCircle> bumpers, ref Vector3 from, Vector3 to,
            bool afterWall)
        {
            if (model.IsPiercing.Value)
            {
                CutCaps(model, bumpers, from, to);
                return to;
            }

            var flight = model.Flight;
            var rules = _mushrooms.Rules;
            for (var bounce = 0; bounce < MaxBumperBouncesPerStretch; bounce++)
            {
                Vector2 stretch = to - from;
                var length = stretch.magnitude;
                if (length < MinStretchLength || MushroomChainRule.IsSpent(flight.MushroomChain, rules))
                {
                    break;
                }

                var heading = stretch / length;
                if (!BumperSweep.TryFindFirst(
                        bumpers, from, heading, length, flight.LeavingBumperId(model.Direction), out var index,
                        out var entry))
                {
                    break;
                }

                // Mushrooms stand clear of the walls. One that does not is not bounced off beyond them.
                var contact = new BumperContact((Vector2)from + (heading * entry), heading, bumpers[index]);
                if (!_walls.Contains(contact.Point))
                {
                    break;
                }

                var reward = MushroomChainRule.OnBounce(ref flight.MushroomChain, contact.Bumper.Wall, rules);
                if (reward == MushroomReward.PassThrough)
                {
                    break;
                }

                var outgoing = _bounceRule.Resolve(contact, bumpers, flight.MushroomChain);
                var point = new Vector3(contact.Point.x, contact.Point.y, from.z);
                _stepBounces.Add(new BumperBounce(
                    contact, _bumperField.SlotAt(index), (flight.SegmentStartPosition, point), outgoing,
                    flight.MushroomChain, reward, afterWall));
                TurnAtBumper(model, contact.Bumper.Id, point, outgoing);
                from = point;
                if (reward == MushroomReward.Arm)
                {
                    // Armed after this step, so the step ends here: see the remarks.
                    return point;
                }

                to = point + (Vector3)(outgoing * (length - entry));
            }

            return to;
        }

        // A lance's stretch from one point to another: every whole cap the ball's centre path enters is cut,
        // nearest first, and the ball is not turned. A cut cap leaves the field at once (IBumperField.Cut), so
        // the sweep that follows does not find it again, on this step or any later one, by this ball or another.
        // The chain is not asked: a cut counts nothing and breaks nothing. Like a bounce, a cut is not made
        // beyond the walls. The sweep ends because each cut takes a cap out of the field: a field that takes
        // none out (an index it does not know, a slot that holds no mushroom) ends it too, with nothing cut and
        // nothing announced, where it would otherwise be swept for the same cap without end.
        //
        // Every cap cut banks the shot one plain lance charge (decision K), whatever armed the lance: the
        // flight's own counter a Snipe picked up mid-pierce banks on, spent the same way, at the wall where
        // the pierce discharges, and lost with the shot. It is banked here, in the step, and not by whoever
        // hears of the cut afterwards: the stretch is swept before its wall is settled, so a cap cut in the
        // very step that reaches the discharge wall has already re-armed the lance there. No limit is kept:
        // a flight cuts no more caps than stand on the board, and no cap twice before it has grown back.
        private void CutCaps(
            IWriteableProjectileModel model, IReadOnlyList<BumperCircle> bumpers, Vector3 from, Vector3 to)
        {
            Vector2 stretch = to - from;
            var length = stretch.magnitude;
            if (length < MinStretchLength)
            {
                return;
            }

            var heading = stretch / length;
            var leaving = model.Flight.LeavingBumperId(model.Direction);
            var whole = bumpers.Count;
            while (BumperSweep.TryFindFirst(bumpers, from, heading, length, leaving, out var index, out var entry))
            {
                if (!_walls.Contains((Vector2)from + (heading * entry)))
                {
                    return;
                }

                var flight = model.Flight;
                var cut = new CapCut(
                    bumpers[index], _bumperField.SlotAt(index), from, heading, _mushrooms.Radius, flight.CapsCut + 1);
                _bumperField.Cut(index);
                if (bumpers.Count >= whole)
                {
                    return;
                }

                whole = bumpers.Count;
                flight.CapsCut++;
                flight.BankedPierceCharges++;
                _stepCuts.Add(cut);
            }
        }

        // A wall bounce flies the rest of its step back off the wall, and at speed that stretch reaches a
        // mushroom too. The stretch is rebuilt forward from the leg that met the wall (legStart to legEnd, the
        // end being where it would have got to with no walls): to where that leg crosses the wall, then on along
        // the mirrored heading for what is left of its length, wall by wall when a corner gives it two. Walking
        // back from where the step ended would not do: after a corner the ball's direction is not the direction
        // it flew the last of the stretch along. Each leg is flown against the bumpers like any other. With no
        // mushroom on the way the step is returned as the walls left it.
        private ProjectileStep BounceOffBumpersPastWall(
            IWriteableProjectileModel model, IReadOnlyList<BumperCircle> bumpers, in ProjectileStep step,
            Vector3 legStart, Vector3 legEnd)
        {
            Vector3 leg = (Vector2)(legEnd - legStart);
            var remaining = leg.magnitude;
            if (remaining < MinStretchLength)
            {
                return step;
            }

            var heading = leg / remaining;
            var at = legStart;
            for (var wall = 0; wall < MaxWallsPerStep; wall++)
            {
                if (!_walls.TryFindCrossing(at, heading, out var crossing, out var normal))
                {
                    return step;
                }

                remaining -= Vector3.Distance(at, crossing);
                if (remaining < MinStretchLength)
                {
                    return step;
                }

                // Off a wall the heading mirrors on that wall's axis, on both for a hit dead in the corner.
                heading = new Vector3(
                    normal.x != 0f ? -heading.x : heading.x, normal.y != 0f ? -heading.y : heading.y, 0f);
                at = crossing;
                var counted = _stepBounces.Count;
                var lastTurn = at;
                var end = BounceOffBumpers(model, bumpers, ref lastTurn, at + (heading * remaining), afterWall: true);
                if (_stepBounces.Count == counted)
                {
                    continue;
                }

                // Turned back at a wall with stretch to spare: the ball stops on the wall, and the next step
                // counts that bounce. A step never ends outside the walls.
                if (!_walls.Contains(end)
                    && _walls.TryFindCrossing(lastTurn, model.Direction, out var stop, out _))
                {
                    end = stop;
                }

                return step.Rerouted(end, model.Direction);
            }

            return step;
        }

        // What a bounce off a bumper does to the flight besides being counted. The ball turns. The turn starts a
        // new straight segment, which the last-shield glide and the sweeps of the next leg measure from. And the
        // shot's run of empty wall bounces is over, as it is on any balloon contact. No shield is spent. Only a
        // ball that is not piercing gets here.
        private static void TurnAtBumper(
            IWriteableProjectileModel model, int bumperId, Vector3 point, Vector2 outgoing)
        {
            model.Direction = outgoing;
            model.Flight.LeaveBumper(bumperId, outgoing);
            model.Flight.SegmentStartPosition = point;
            model.Flight.SegmentElapsed = 0f;
            model.Flight.ConsecutiveWallBounces = 0;
            if (model.IsCruising.Value)
            {
                model.IsCruising.Value = false;
            }
        }

        /// <summary>Reflects the projectile off a deflecting balloon at the ANALYTIC contact point,
        /// returning that point for the caller to snap the shot onto. The trigger fires at a discrete
        /// fixed step, so the reported position sits up to a step length inside the balloon — a radial
        /// normal there is displaced by up to ~30° from the true tangency, turning aim→outcome into a
        /// step-phase staircase. Backtracking the travel ray to the exact circle entry keeps
        /// deflections the clean billiard the shot-geometry puzzle work relies on
        /// (see @ref plan_shot_geometry).</summary>
        internal Vector3 Deflect(
            IWriteableProjectileModel model, Vector3 projectilePosition, Vector3 balloonPosition, float contactRadius)
        {
            Vector3 contactPoint;
            float remainder;

            var penetrated = TryComputeContactNormal(
                projectilePosition, model.Direction, balloonPosition, contactRadius, out var surfaceNormal);

            if (penetrated
                || CircleContact.TryFindEntryNormal(
                    projectilePosition, model.Direction, balloonPosition, contactRadius, out surfaceNormal))
            {
                model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);

                // Snap to the analytic contact point, then carry the already-travelled penetration
                // distance along the REFLECTED heading — the exact billiard continuation, losing
                // neither geometry nor time. Reflecting from the penetrated trigger position would
                // exit along a chord displaced by up to a fixed step — an error the Sinai dispersion
                // law (@ref plan_shot_geometry §3) amplifies ×10–20 per subsequent deflect.
                contactPoint = balloonPosition + (Vector3)(surfaceNormal * contactRadius);
                contactPoint.z = projectilePosition.z;

                // A balloon in an edge column can sit within its own collider radius of a wall, putting
                // the contact point (and the reflected continuation) OUTSIDE the field. Left un-clamped,
                // the next Step reads the out-of-bounds position as a wall crossing and spends a shield
                // — a phantom bounce that can kill a 0-shield shot at the deflect. Clamp so a deflect
                // never starts a step out of bounds; genuine wall hits still resolve on later steps.
                contactPoint = _walls.ClampInside(contactPoint);

                // Only a PENETRATED contact has surplus travel to re-route: its remainder is distance
                // already spent past the surface this frame. A forward (capsule-nose, still-short)
                // contact never reached the surface — snapping to the contact point compensates the
                // nose bias, and carrying the shortfall onward too would manufacture free travel.
                remainder = penetrated ? (projectilePosition - contactPoint).magnitude : 0f;
            }
            else
            {
                // Truly degenerate trigger (zero direction, or the ray's infinite line never crosses
                // the circle at all) — the penetrated radial normal is still a sane reflection, from
                // where the shot actually is.
                surfaceNormal = ((Vector2)projectilePosition - (Vector2)balloonPosition).normalized;
                model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);
                contactPoint = _walls.ClampInside(projectilePosition);
                remainder = 0f;
            }

            // Every deflect re-anchors the flight segment HERE, unconditionally — Step's
            // last-shield-approach branch recomputes position ABSOLUTELY from this anchor, so a path
            // that skipped this write (the degenerate branch used to) left it pointing at whatever wall
            // contact started the previous segment. The next Step then relocated the shot from that
            // stale, long-past anchor using the just-reflected heading — the graze-deflect teleport
            // (investigation H1). Re-anchoring on every path makes "a deflect starts a new segment" an
            // unconditional invariant instead of an analytic-path-only one.
            model.Flight.SegmentStartPosition = contactPoint;
            model.Flight.SegmentElapsed = 0f;
            return _walls.ClampInside(contactPoint + (Vector3)(model.Direction * remainder));
        }

        /// <summary>Backtracks the travel ray from the (penetrated) trigger position to its entry into
        /// the contact circle — smallest positive t with |position − t·direction − center| = radius —
        /// and returns the unit normal there. False when the ray's line never crosses the circle or the
        /// entry lies ahead of the position (both only reachable through degenerate trigger states).</summary>
        internal static bool TryComputeContactNormal(
            Vector2 position, Vector2 direction, Vector2 center, float radius, out Vector2 normal)
        {
            normal = default;
            if (radius <= 0f || direction.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            var travel = direction.normalized;
            var toPosition = position - center;
            var along = Vector2.Dot(toPosition, travel);
            var discriminant = along * along - toPosition.sqrMagnitude + radius * radius;

            // A true tangency has discriminant exactly 0, but mixed-precision evaluation can land it
            // a few billionths negative — clamp the noise band so knife-edge grazing contacts get the
            // analytic tangent normal instead of falling back to the penetrated radial one.
            if (discriminant < 0f)
            {
                if (discriminant < -1e-6f)
                {
                    return false;
                }

                discriminant = 0f;
            }

            var backtrack = along + Mathf.Sqrt(discriminant);
            if (backtrack < 0f)
            {
                return false;
            }

            normal = (toPosition - travel * backtrack) / radius;
            return true;
        }

        // Only ever consulted after TryComputeContactNormal has already returned false. The
        // projectile's swept CapsuleCollider2D extends past the sampled centre point (its nose sits up
        // to ~0.105 units ahead of transform.position along the heading), so OnTriggerEnter2D can fire
        // while the reported centre still sits a hair OUTSIDE the combined circle — on ~47% of live
        // deflects (graze-deflect investigation, H1/H3) that pushes TryComputeContactNormal's backtrack
        // negative and it bails, even though the ray plainly enters the circle a fraction of a unit
        // further along direction. This solves the SAME quadratic's other root — the entry ahead of
        // position instead of behind it — recovering the true tangent normal there instead of the
        // penetrated-radial one the degenerate fallback used to supply.

    }
}
