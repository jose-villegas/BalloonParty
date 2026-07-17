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
        private readonly int _cruiseWallBounceThreshold;
        private readonly float _cruiseMaxSpeedMultiplier;
        private readonly AnimationCurve _cruiseRampCurve;

        [Inject]
        internal ProjectileMotionResolver(IGameConfiguration config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
            _cruiseWallBounceThreshold = config.CruiseWallBounceThreshold;
            _cruiseMaxSpeedMultiplier = config.CruiseMaxSpeedMultiplier;
            _cruiseRampCurve = config.CruiseRampCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>Advances one fixed step, mutating direction/shield count on a wall bounce.</summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            var speed = model.ComputeBuffedValue(ProjectileBuffId.Speed, model.Speed);

            // The earned long-flight reward: a cruising shot ramps from base speed toward the max
            // multiplier as bounces spend the shields it entered cruise with — the longer the empty
            // corridor runs, the faster it gets, peaking on its final shield.
            if (model.IsCruising.Value)
            {
                var startShields = Mathf.Max(model.CruiseStartShields, 1);
                var spentFraction = Mathf.Clamp01(
                    (model.CruiseStartShields - model.ShieldsRemaining.Value) / (float)startShields);
                speed *= Mathf.Lerp(1f, _cruiseMaxSpeedMultiplier, _cruiseRampCurve.Evaluate(spentFraction));
            }

            position += model.Direction * (speed * deltaTime);
            position = _walls.Clamp(position, out var reflect);

            if (reflect == Vector3.zero)
            {
                return ProjectileStep.Moved(position, model.Direction);
            }

            model.ShieldsRemaining.Value--;
            if (model.ShieldsRemaining.Value < 0)
            {
                return ProjectileStep.Destroyed(position, model.Direction);
            }

            // Consecutive wall bounces with no balloon contact = the shot is ping-ponging through
            // empty space (HitResolver resets the counter on any balloon touch). Deterministic
            // flight means this only resolves by traversing the corridor — make it a moment, not
            // a chore.
            model.ConsecutiveWallBounces++;
            if (_cruiseWallBounceThreshold > 0
                && !model.IsCruising.Value
                && model.ConsecutiveWallBounces >= _cruiseWallBounceThreshold)
            {
                model.CruiseStartShields = model.ShieldsRemaining.Value;
                model.IsCruising.Value = true;
            }

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
            return ProjectileStep.Bounced(position, model.Direction);
        }

        /// <summary>Reflects the projectile off a deflecting balloon at the ANALYTIC contact point.
        /// The trigger fires at a discrete fixed step, so the reported position sits up to a step
        /// length inside the balloon — a radial normal there is displaced by up to ~30° from the
        /// true tangency, turning aim→outcome into a step-phase staircase. Backtracking the travel
        /// ray to the exact circle entry keeps deflections the clean billiard the shot-geometry
        /// puzzle work relies on (see @ref plan_shot_geometry).</summary>
        internal void Deflect(
            IWriteableProjectileModel model, Vector3 projectilePosition, Vector3 balloonPosition, float contactRadius)
        {
            if (!TryComputeContactNormal(
                    projectilePosition, model.Direction, balloonPosition, contactRadius, out var surfaceNormal))
            {
                // Degenerate trigger (zero direction, no ray-circle crossing) — the penetrated radial
                // normal is still a sane reflection.
                surfaceNormal = ((Vector2)projectilePosition - (Vector2)balloonPosition).normalized;
            }

            model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);
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
            if (discriminant < 0f)
            {
                return false;
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
