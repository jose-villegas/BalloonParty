using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     The write half of <see cref="IFlightStatsSource" />, segregated so no reader can mutate what it
    ///     reads. Every method is called by the publisher of the corresponding message, immediately
    ///     before it publishes — so the count is fixed before any subscriber runs and no reader depends
    ///     on MessagePipe's subscription order, which is enforced nowhere. If a subscriber counted
    ///     instead, each reader would see a different total depending on where it happened to sit in
    ///     the subscriber list.
    /// </summary>
    internal interface IFlightStatsWriter
    {
        /// <summary>Counted against <c>msg.Context.Projectile</c>; a hit no shot caused counts nowhere.</summary>
        void Record(in ActorHitMessage msg);

        /// <summary>Call immediately before publishing <see cref="WallHitMessage" />.</summary>
        void RecordWallHit(IProjectileModel projectile);

        /// <summary>Call immediately before publishing <see cref="PierceDischargedMessage" />.</summary>
        void RecordPierceDischarge(IProjectileModel projectile);
    }
}
