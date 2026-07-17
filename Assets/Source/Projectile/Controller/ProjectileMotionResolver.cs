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
        private readonly float _cruiseTapEaseDuration;
        private readonly int _cruisePiercingTapThreshold;
        private readonly AnimationCurve _cruiseTapCurve;
        private readonly AnimationCurve _lastShieldApproachCurve;
        private readonly float _lastShieldApproachDuration;

        // The view needs the same wall geometry for its cruise lookahead trace.
        internal WallLimits Walls => _walls;

        [Inject]
        internal ProjectileMotionResolver(IGameConfiguration config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
            _cruiseSpeedPerShield = config.CruiseSpeedPerShield;
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
            var baseSpeed = model.ComputeBuffedValue(ProjectileBuffId.Speed, model.Speed);
            var speed = baseSpeed;

            // The earned long-flight reward: every cruise bounce adds a velocity TAP of
            // CruiseSpeedPerShield — cumulative, so a 13-shield bank accumulates 13 taps where a
            // 2-shield bank gets 2. Each tap replays the animation envelope from t=0: the new
            // target speed scaled by curve(elapsed/duration), so a curve starting at 0 freezes the
            // shot for a beat before it picks up. The pierce scale bleeds this down as a piercing
            // shot plows through tough actors; the floor keeps it from ever dropping below base.
            if (model.IsCruising.Value)
            {
                var startShields = Mathf.Max(model.Flight.CruiseStartShields, 1);
                var taps = Mathf.Clamp(
                    model.Flight.CruiseStartShields - model.ShieldsRemaining.Value, 0, startShields);
                var target = 1f + _cruiseSpeedPerShield * taps;
                var progress = _cruiseTapEaseDuration > 0f
                    ? Mathf.Clamp01(model.Flight.CruiseTapElapsed / _cruiseTapEaseDuration)
                    : 1f;

                // The pierce decay floors the ramp at base ("min normal speed"); the per-tap freeze
                // animation rides on top and may still dip the shot to a momentary standstill.
                var cruiseSpeed = Mathf.Max(baseSpeed * target * model.Flight.CruisePierceSpeedScale, baseSpeed);
                speed = cruiseSpeed * _cruiseTapCurve.Evaluate(progress);
                model.Flight.CruiseTapElapsed += deltaTime;
            }

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
                    ? segmentLength + (baseSpeed * deltaTime)
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
                return ProjectileStep.Moved(position, model.Direction);
            }

            model.ShieldsRemaining.Value--;
            if (model.ShieldsRemaining.Value < 0)
            {
                // A dead shot stops AT the wall — the mirrored continuation is for survivors.
                return ProjectileStep.Destroyed(wallContact, model.Direction);
            }

            // Consecutive wall bounces with no balloon contact = the shot may be ping-ponging empty
            // space (HitResolver resets the counter on any balloon touch). Entry into cruise is the
            // VIEW's call — it confirms with a physics lookahead the plain resolver can't run.
            model.Flight.ConsecutiveWallBounces++;
            if (model.IsCruising.Value && model.Flight.CruisePierceSpeedScale < 1f)
            {
                // Only ONCE the shot has plowed a tough (scale decayed below 1) does a wall end the
                // run: cruise ends, speed returns to normal, AND the earned piercing is consumed —
                // the shot is a normal shot again (deflects off toughs). An armed shot cruising an
                // empty corridor, or one that has only popped 1-hit balloons, keeps both its speed
                // and its pierce; nothing has slowed it, so nothing is spent.
                model.IsCruising.Value = false;
                model.Flight.ConsecutiveWallBounces = 0;
                model.Flight.CruisePierceSpeedScale = 1f;
                model.IsPiercing.Value = false;
            }
            else if (model.IsCruising.Value)
            {
                // A new tap lands with this bounce — restart its freeze-then-pickup envelope.
                model.Flight.CruiseTapElapsed = 0f;

                // A long-enough cruise ARMS the shot: from this tap on it pierces everything it
                // touches (unbreakables included) for the rest of its life.
                var taps = model.Flight.CruiseStartShields - model.ShieldsRemaining.Value;
                if (_cruisePiercingTapThreshold > 0
                    && taps >= _cruisePiercingTapThreshold
                    && !model.IsPiercing.Value)
                {
                    model.IsPiercing.Value = true;
                }
            }

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
            model.Flight.SegmentStartPosition = wallContact;
            model.Flight.SegmentElapsed = 0f;
            return ProjectileStep.Bounced(position, wallContact, model.Direction);
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
            if (!TryComputeContactNormal(
                    projectilePosition, model.Direction, balloonPosition, contactRadius, out var surfaceNormal))
            {
                // Degenerate trigger (zero direction, no ray-circle crossing) — the penetrated radial
                // normal is still a sane reflection, from where the shot actually is.
                surfaceNormal = ((Vector2)projectilePosition - (Vector2)balloonPosition).normalized;
                model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);
                return projectilePosition;
            }

            model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);

            // Snap to the analytic contact point, then carry the already-travelled penetration
            // distance along the REFLECTED heading — the exact billiard continuation, losing neither
            // geometry nor time. Reflecting from the penetrated trigger position would exit along a
            // chord displaced by up to a fixed step — an error the Sinai dispersion law
            // (@ref plan_shot_geometry §3) amplifies ×10–20 per subsequent deflect.
            var contactPoint = balloonPosition + (Vector3)(surfaceNormal * contactRadius);
            contactPoint.z = projectilePosition.z;
            var remainder = (projectilePosition - contactPoint).magnitude;
            model.Flight.SegmentStartPosition = contactPoint;
            model.Flight.SegmentElapsed = 0f;
            return contactPoint + (Vector3)(model.Direction * remainder);
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
    }
}
