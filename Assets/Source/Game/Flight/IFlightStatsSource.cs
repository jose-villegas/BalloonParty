using BalloonParty.Projectile.Model;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     Per-shot <see cref="IFlightStats" />. Several shots can fly at once, so a reader always asks for
    ///     the shot its message is about. A shot's stats stay readable through its own
    ///     <c>ProjectileDestroyedMessage</c>; an unknown or null shot reads as all zeroes.
    /// </summary>
    internal interface IFlightStatsSource
    {
        IFlightStats Of(IProjectileModel projectile);
    }
}
