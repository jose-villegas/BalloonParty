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
        private readonly WallLimits _walls;
        private readonly float _cruiseSpeedPerShield;
        private readonly float _maxCruiseSpeedMultiplier;
        private readonly float _cruiseTapEaseDuration;
        private readonly int _cruisePiercingTapThreshold;
        private readonly AnimationCurve _cruiseTapCurve;
        private readonly AnimationCurve _lastShieldApproachCurve;
        private readonly float _lastShieldApproachDuration;

        // The view needs the same wall geometry for its cruise lookahead trace.
        internal WallLimits Walls => _walls;

        [Inject]
        internal ProjectileMotionResolver(IProjectileFlightConfig config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
            _cruiseSpeedPerShield = config.CruiseSpeedPerShield;
            _maxCruiseSpeedMultiplier = config.MaxCruiseSpeedMultiplier;
            _cruiseTapEaseDuration = config.CruiseTapEaseDuration;
            _cruisePiercingTapThreshold = config.CruisePiercingTapThreshold;
            _cruiseTapCurve = config.CruiseTapCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            // An un-authored (newly-added) serialized curve deserializes empty and evaluates to 0,
            // which would crawl the shot — fall back to a constant-1 no-op until it's authored.
            _lastShieldApproachCurve = config.LastShieldApproachCurve is { length: > 0 }
                ? config.LastShieldApproachCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _lastShieldApproachDuration = config.LastShieldApproachDuration;
        }

        /// <summary>Advances one fixed step, mutating direction/shield count on a wall bounce.</summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            var speed = ResolveFlightSpeed(model, deltaTime);

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
            position = _walls.Reflect(position, out var reflect, out var wallContact);

            if (reflect == Vector3.zero)
            {
                return ProjectileStep.Moved(position, model.Direction, speed);
            }

            model.ShieldsRemaining.Value--;
            if (model.ShieldsRemaining.Value < 0)
            {
                // A dead shot stops AT the wall — the mirrored continuation is for survivors.
                return ProjectileStep.Destroyed(wallContact, model.Direction, speed);
            }

            // Wall-discharge: a piercing shot that plowed through at least one tough on this segment
            // discharges at the wall — the view resolves the pending toughs, and the pierce (with its
            // speed buff) ends. If the segment had no toughs, piercing continues indefinitely.
            if (model.IsPiercing.Value && model.Flight.PendingPierceHits.Count > 0)
            {
                model.EndPierce();

                model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
                model.Flight.SegmentStartPosition = wallContact;
                model.Flight.SegmentElapsed = 0f;
                return ProjectileStep.Bounced(position, wallContact, model.Direction, speed);
            }

            // Consecutive wall bounces with no balloon contact = the shot may be ping-ponging empty
            // space (HitResolver resets the counter on any balloon touch). Entry into cruise is the
            // VIEW's call — it confirms with a physics lookahead the plain resolver can't run.
            model.Flight.ConsecutiveWallBounces++;
            if (model.IsCruising.Value)
            {
                model.Flight.TotalCruiseTaps++;

                // A new tap lands with this bounce — restart its freeze-then-pickup envelope.
                model.Flight.CruiseTapElapsed = 0f;

                // A long-enough cruise ARMS the shot: from this tap on it pierces everything it
                // touches (unbreakables included) for the rest of its life.
                if (_cruisePiercingTapThreshold > 0
                    && model.Flight.TotalCruiseTaps >= _cruisePiercingTapThreshold
                    && !model.IsPiercing.Value)
                {
                    model.IsPiercing.Value = true;
                }
            }

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
            model.Flight.SegmentStartPosition = wallContact;
            model.Flight.SegmentElapsed = 0f;
            return ProjectileStep.Bounced(position, wallContact, model.Direction, speed);
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
                || TryComputeForwardContactNormal(
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
        private static bool TryComputeForwardContactNormal(
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
            if (discriminant < 0f)
            {
                if (discriminant < -1e-6f)
                {
                    return false;
                }

                discriminant = 0f;
            }

            var entryDistance = -along - Mathf.Sqrt(discriminant);
            if (entryDistance <= 0f)
            {
                return false;
            }

            normal = (toPosition + travel * entryDistance) / radius;
            return true;
        }

        // The flight speed for this step. Both cruise wall-bounces and sweep corridor-clears feed a
        // single TotalCruiseTaps counter; speed scales with that count × CruiseSpeedPerShield.
        private float ResolveFlightSpeed(IWriteableProjectileModel model, float deltaTime)
        {
            var baseSpeed = model.ComputeBuffedValue(ProjectileBuffId.Speed, model.Speed);
            var taps = model.Flight.TotalCruiseTaps;

            if (taps <= 0)
            {
                return baseSpeed;
            }

            var speedBonus = _cruiseSpeedPerShield * taps;
            var target = 1f + speedBonus;
            if (_maxCruiseSpeedMultiplier > 0f)
            {
                target = Mathf.Min(target, _maxCruiseSpeedMultiplier);
            }

            var progress = _cruiseTapEaseDuration > 0f
                ? Mathf.Clamp01(model.Flight.CruiseTapElapsed / _cruiseTapEaseDuration)
                : 1f;

            model.Flight.CruiseTapElapsed += deltaTime;
            return baseSpeed * target * _cruiseTapCurve.Evaluate(progress);
        }
    }
}
