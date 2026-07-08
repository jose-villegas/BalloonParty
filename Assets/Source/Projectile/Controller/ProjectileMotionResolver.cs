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
        private const float SpeedBuffMultiplier = 2f;

        private readonly WallLimits _walls;

        [Inject]
        internal ProjectileMotionResolver(IGameConfiguration config)
        {
            _walls = new WallLimits(config.LimitsClockwise);
        }

        /// <summary>Advances one fixed step, mutating direction/shield count on a wall bounce.</summary>
        internal ProjectileStep Step(IWriteableProjectileModel model, Vector3 position, float deltaTime)
        {
            var speed = model.HasBuff<SpeedProjectileBuff>() ? model.Speed * SpeedBuffMultiplier : model.Speed;
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
