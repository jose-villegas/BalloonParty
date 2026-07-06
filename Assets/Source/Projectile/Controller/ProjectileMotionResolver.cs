using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Projectile flight rules as a plain, headless-testable object: advance, wall-bounce,
    ///     shield decrement, and the destroy decision. The view only applies the returned
    ///     <see cref="ProjectileStep" /> (transform, bounce VFX, shield-lost message, disturbance
    ///     stamp) — the gameplay decisions live here. Mirrors <see cref="ProjectileHitResolver" />,
    ///     and shares the wall layout with the aim prediction through <see cref="WallLimits" />.
    /// </summary>
    internal sealed class ProjectileMotionResolver
    {
        private readonly WallLimits _walls;

        [Inject]
        internal ProjectileMotionResolver(IGameConfiguration config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
        }

        /// <summary>
        ///     Advances one fixed step, mutating the model's direction and shield count on a
        ///     wall bounce, and returns what the view must present.
        /// </summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            position += model.Direction * (model.Speed * deltaTime);
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

            model.Direction = Vector2.Reflect(model.Direction, reflect.normalized);
            return ProjectileStep.Bounced(position, model.Direction);
        }

        /// <summary>Reflects the projectile off a deflecting balloon's surface normal.</summary>
        internal void Deflect(IWriteableProjectileModel model, Vector3 projectilePosition, Vector3 balloonPosition)
        {
            var surfaceNormal = ((Vector2)projectilePosition - (Vector2)balloonPosition).normalized;
            model.Direction = Vector2.Reflect(model.Direction, surfaceNormal);
        }
    }
}
