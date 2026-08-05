using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Shield
{
    /// <summary>
    ///     Steers shield-carrying balloons toward slots a shot can still reach, so a planned chain
    ///     degrades gracefully as the board settles instead of quietly becoming uncollectable.
    /// </summary>
    /// <remarks>
    ///     A nudge, not a rule. It is added to the actor's own balance bias rather than replacing it,
    ///     and it is small enough that support and pressure still decide the move — a shield should
    ///     not make a balloon hover in defiance of the board.
    /// </remarks>
    internal sealed class ShieldSlotPreference : IShieldSlotPreference
    {
        // Comfortably inside the range the existing biases occupy (small ints), and orders below
        // MoveWeightEvaluator.PressureGain, so a shove still wins every contested slot.
        private const int ReachableBonus = 6;
        private const int PerReflectionPenalty = 2;

        private readonly ShieldReachabilityField _field;

        internal ShieldSlotPreference(ShieldReachabilityField field)
        {
            _field = field;
        }

        public int WeightFor(ISlotActor actor, Vector2Int candidate)
        {
            if (actor is not IHasItemSlot host || host.Item.Value != ItemType.Shield)
            {
                return 0;
            }

            var reflections = _field.ReflectionsToReach(candidate);
            if (reflections == ShieldReachabilityField.Unreachable)
            {
                return 0;
            }

            // Straight-shot slots score highest and each reflection costs, so a shield drifts toward
            // the shots a player can actually see — the same reason the planner prefers walls.
            return Mathf.Max(0, ReachableBonus - (reflections * PerReflectionPenalty));
        }
    }
}
