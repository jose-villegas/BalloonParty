using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Shared.Extensions
{
    internal static class ActorHitMessageExtensions
    {
        /// <summary>
        ///     Whether the hit took a layer of foam off a coated balloon (<see cref="ICoatable" />) rather than hurting
        ///     it: the coat turns any ordinary damaging hit into a pass-through. Read from the outcome alone, so it relies
        ///     on every coatable balloon popping to one hit; a coatable that took more would read its ordinary
        ///     survived hits as coat hits too.
        /// </summary>
        public static bool TookCoat(this in ActorHitMessage message)
        {
            return message.Outcome == HitOutcome.PassThrough
                   && message.Context.Damage > 0
                   && message.Actor is ICoatable;
        }

        /// <summary>Whether the hit came from a ripe cluster's ripple (<see cref="DamageFlags.Ripple" />).</summary>
        public static bool IsRipple(this in ActorHitMessage message)
        {
            return (message.Context.Flags & DamageFlags.Ripple) != 0;
        }
    }
}
