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
        private readonly AnimationCurve _cruiseRampCurve;

        // The view needs the same wall geometry for its cruise lookahead trace.
        internal WallLimits Walls => _walls;

        [Inject]
        internal ProjectileMotionResolver(IGameConfiguration config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
            _cruiseSpeedPerShield = config.CruiseSpeedPerShield;
            _cruiseRampCurve = config.CruiseRampCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>Advances one fixed step, mutating direction/shield count on a wall bounce.</summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            var speed = model.ComputeBuffedValue(ProjectileBuffId.Speed, model.Speed);

            // The earned long-flight reward: every cruise bounce adds a velocity TAP of
            // CruiseSpeedPerShield — cumulative, so a 13-shield bank accumulates 13 taps where a
            // 2-shield bank gets 2. The curve only re-paces the taps (linear = equal taps per
            // bounce): shapedTaps = curve(taps/bank) x bank, which reduces to exactly the tap
            // count on a linear curve.
            if (model.IsCruising.Value)
            {
                var startShields = Mathf.Max(model.CruiseStartShields, 1);
                var taps = Mathf.Clamp(
                    model.CruiseStartShields - model.ShieldsRemaining.Value, 0, startShields);
                var shapedTaps = _cruiseRampCurve.Evaluate(taps / (float)startShields) * startShields;
                speed *= 1f + _cruiseSpeedPerShield * shapedTaps;
            }

            position += model.Direction * (speed * deltaTime);
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
            model.ConsecutiveWallBounces++;

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
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
