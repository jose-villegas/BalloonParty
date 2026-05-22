using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Shared.Extensions
{
    internal static class SlotActorExtensions
    {
        /// <summary>
        /// Evaluates a hit against the actor if it implements <see cref="IHitable"/>;
        /// otherwise returns <paramref name="fallback"/>.
        /// </summary>
        public static HitOutcome EvaluateHit(
            this ISlotActor actor,
            int damage,
            HitOutcome fallback = HitOutcome.PassThrough)
        {
            return actor is IHitable hitable ? hitable.EvaluateHit(damage) : fallback;
        }
    }
}
