using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     The write half of <see cref="IProjectileRegistry" />. A shot that dies lands itself right
    ///     before it publishes <c>ProjectileDestroyedMessage</c> and retires itself right after, so every
    ///     subscriber sees it gone yet can still read its state. Every other exit (game over, reset, a
    ///     replaced held ball) is the thrower's, and it lands and retires together.
    /// </summary>
    internal interface IProjectileRegistryWriter : IProjectileRegistry
    {
        void Load(IProjectileModel projectile, Transform transform);

        /// <summary>The loaded shot left the thrower. No-op for a shot that isn't the loaded one.</summary>
        void Launch(IProjectileModel projectile);

        /// <summary>Puts a shot that never took a step back in the thrower (see ThrowerController).</summary>
        void Unlaunch(IProjectileModel projectile);

        /// <summary>The shot's flight ended. Idempotent; also drops it if it was still loaded.</summary>
        void Land(IProjectileModel projectile);

        /// <summary>Lands the shot if needed and fires <c>Retired</c> once. Idempotent.</summary>
        void Retire(IProjectileModel projectile);

        /// <summary>Retires every tracked shot.</summary>
        void Clear();
    }
}
